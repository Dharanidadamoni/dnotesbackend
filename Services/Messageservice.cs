namespace dnotes_backend.Services;
    using dnotes_backend.Data;
    using dnotes_backend.DTOs;
    using dnotes_backend.Helpers;
    using dnotes_backend.Models;
    using Microsoft.EntityFrameworkCore;

    public interface IMessageService
    {
        Task<MessageDto> CreateAsync(Guid userId, CreateMessageRequest request);
        Task<MessageDto> UpdateAsync(Guid userId, Guid messageId, UpdateMessageRequest request);
        Task<MessageDto> GetByIdAsync(Guid userId, Guid messageId);
        Task<PagedResponse<MessageSummaryDto>> GetAllAsync(Guid userId, int page, int pageSize, string? filter);
        Task DeleteAsync(Guid userId, Guid messageId);
        Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId);
    }

    public class MessageService : IMessageService
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _enc;

    public MessageService(AppDbContext db, IEncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    public MessageService(AppDbContext db) => _db = db;

        // ── CREATE ────────────────────────────────────
        public async Task<MessageDto> CreateAsync(Guid userId, CreateMessageRequest req)
        {
            var message = new Message
            {
                SenderId = userId,
                Title = req.Title.Trim(),
                EncryptedBody = req.EncryptedBody,
                DeliveryType = req.DeliveryType,
                EncryptedDeliveryDate = !string.IsNullOrEmpty(req.DeliveryDate)
    ? _enc.Encrypt(req.DeliveryDate)
    : null,
                IsDraft = req.IsDraft,
                WordCount = req.WordCount,
            };

            foreach (var r in req.Recipients)
            {
                message.Recipients.Add(new Recipient
                {
                    Email = r.Email.ToLower().Trim(),
                    Name = r.Name.Trim(),
                    Relationship = r.Relationship,
                });
            }

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            return await GetByIdAsync(userId, message.Id);
        }

        // ── UPDATE ────────────────────────────────────
        public async Task<MessageDto> UpdateAsync(Guid userId, Guid messageId, UpdateMessageRequest req)
        {
            var message = await GetMessageOrThrowAsync(userId, messageId);

            if (message.IsDelivered)
                throw new InvalidOperationException("Cannot edit a delivered message.");

            if (req.Title != null) message.Title = req.Title.Trim();
            if (req.EncryptedBody != null) message.EncryptedBody = req.EncryptedBody;
            if (req.DeliveryType != null) message.DeliveryType = req.DeliveryType;
        ////if (req.EncryptedDeliveryDate != null) message.EncryptedDeliveryDate = req.EncryptedDeliveryDate;
        if (req.DeliveryDate != null)
        {
            message.EncryptedDeliveryDate = _enc.Encrypt(req.DeliveryDate);
        }
        if (req.IsDraft != null) message.IsDraft = req.IsDraft.Value;
            if (req.WordCount != null) message.WordCount = req.WordCount.Value;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(userId, messageId);
        }

        // ── GET BY ID ─────────────────────────────────
        public async Task<MessageDto> GetByIdAsync(Guid userId, Guid messageId)
        {
            var message = await _db.Messages
                .Include(m => m.Recipients)
                .Where(m => m.Id == messageId && m.SenderId == userId)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Message not found.");

            return MapToDto(message);
        }

        // ── GET ALL (paginated) ───────────────────────
        public async Task<PagedResponse<MessageSummaryDto>> GetAllAsync(
            Guid userId, int page, int pageSize, string? filter)
        {
            var query = _db.Messages
                .Include(m => m.Recipients)
                .Where(m => m.SenderId == userId)
                .AsQueryable();

            if (filter == "saved") query = query.Where(m => !m.IsDraft);
            if (filter == "draft") query = query.Where(m => m.IsDraft);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(m => m.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageSummaryDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    IsDelivered = m.IsDelivered,
                    IsDraft = m.IsDraft,
                    WordCount = m.WordCount,
                    RecipientCount = m.Recipients.Count,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                    RecipientNames = m.Recipients.Select(r => r.Name).ToList()
                })
                .ToListAsync();

            return new PagedResponse<MessageSummaryDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        // ── DELETE ────────────────────────────────────
        public async Task DeleteAsync(Guid userId, Guid messageId)
        {
            var message = await GetMessageOrThrowAsync(userId, messageId);
            if (message.IsDelivered)
                throw new InvalidOperationException("Cannot delete a delivered message.");

            _db.Messages.Remove(message);
            await _db.SaveChangesAsync();
        }

        // ── DASHBOARD STATS ───────────────────────────
        public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId)
        {
            var user = await _db.Users
                .Include(u => u.Verifier)
                .Include(u => u.Messages)
                    .ThenInclude(m => m.Recipients)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("User not found.");

            return new DashboardStatsDto
            {
                TotalMessages = user.Messages.Count,
                SavedMessages = user.Messages.Count(m => !m.IsDraft),
                DraftMessages = user.Messages.Count(m => m.IsDraft),
                TotalRecipients = user.Messages.SelectMany(m => m.Recipients).Count(),
                HasVerifier = user.Verifier is not null,
                IsTriggered = user.IsTriggered,
            };
        }

        // ── PRIVATE ───────────────────────────────────
        private async Task<Message> GetMessageOrThrowAsync(Guid userId, Guid messageId)
        {
            return await _db.Messages
                .Include(m => m.Recipients)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId)
                ?? throw new KeyNotFoundException("Message not found.");
        }

    private MessageDto MapToDto(Message m) => new()
    {
        Id = m.Id,
        Title = m.Title,
        EncryptedBody = m.EncryptedBody,
        DeliveryType = m.DeliveryType,

        DeliveryDate = !string.IsNullOrEmpty(m.EncryptedDeliveryDate)
     ? _enc.Decrypt(m.EncryptedDeliveryDate)
     : null,

        IsDelivered = m.IsDelivered,
        IsDraft = m.IsDraft,
        WordCount = m.WordCount,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,

        Recipients = m.Recipients.Select(r => new RecipientDto
        {
            Id = r.Id,
            Email = r.Email,
            Name = r.Name,
            Relationship = r.Relationship,
            IsUnlocked = r.IsUnlocked,
            IsNotified = r.IsNotified,
        }).ToList()
    };
}

