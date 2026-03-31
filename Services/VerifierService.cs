// ════════════════════════════════════════════════
// Services/VerifierService.cs
// ════════════════════════════════════════════════

using dnotes_backend.Data;
using dnotes_backend.DTOs;
using dnotes_backend.Models;
using dnotes_backend.Services;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Services;

public interface IVerifierService
{
    Task<VerifierDto> SetVerifierAsync(Guid userId, SetVerifierRequest request);
    Task<VerifierDto?> GetVerifierAsync(Guid userId);
    Task DeleteVerifierAsync(Guid userId);
    Task ReportDeathAsync(Guid verifierId, ReportDeathRequest request);
    Task CancelDeathReportAsync(Guid userId); // sender clicks "I'm alive"
}

public class VerifierService : IVerifierService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public VerifierService(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<VerifierDto> SetVerifierAsync(Guid userId, SetVerifierRequest req)
    {
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Replace existing verifier if any
        if (user.Verifier != null)
            _db.Verifiers.Remove(user.Verifier);

        var verifier = new Verifier
        {
            UserId = userId,
            Name = req.Name.Trim(),
            Email = req.Email.ToLower().Trim(),
            Phone = req.Phone,
            Status = VerifierStatus.Active,
        };

        _db.Verifiers.Add(verifier);
        await _db.SaveChangesAsync();

        // Send welcome email to verifier immediately
        await _email.SendVerifierWelcomeAsync(verifier, user);

        return MapToDto(verifier);
    }

    public async Task<VerifierDto?> GetVerifierAsync(Guid userId)
    {
        var verifier = await _db.Verifiers
            .FirstOrDefaultAsync(v => v.UserId == userId);
        return verifier is null ? null : MapToDto(verifier);
    }

    public async Task DeleteVerifierAsync(Guid userId)
    {
        var verifier = await _db.Verifiers
            .FirstOrDefaultAsync(v => v.UserId == userId)
            ?? throw new KeyNotFoundException("Verifier not found.");
        _db.Verifiers.Remove(verifier);
        await _db.SaveChangesAsync();
    }

    public async Task ReportDeathAsync(Guid verifierId, ReportDeathRequest req)
    {
        var verifier = await _db.Verifiers
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == verifierId)
            ?? throw new KeyNotFoundException("Verifier not found.");

        if (verifier.HasReportedDeath)
            throw new InvalidOperationException("Death already reported.");

        verifier.HasReportedDeath = true;
        verifier.DeathReportedAt = DateTime.UtcNow;
        verifier.CertificateNumber = req.CertificateNumber;
        verifier.CertificateUrl = req.CertificateUrl;
        verifier.Status = VerifierStatus.DeathReported;

        // Start 48-hour cooling period on the user
        verifier.User.SleepModeUntil = DateTime.UtcNow.AddHours(48);

        await _db.SaveChangesAsync();

        // Send final warning to sender email
        // (in case they're alive and want to cancel)
        await _email.SendDeathReportedToSenderAsync(verifier.User, verifier);
    }

    public async Task CancelDeathReportAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.Verifier != null)
        {
            user.Verifier.HasReportedDeath = false;
            user.Verifier.DeathReportedAt = null;
            user.Verifier.Status = VerifierStatus.Active;
        }

        user.LastCheckInAt = DateTime.UtcNow;
        user.SleepModeUntil = null;
        user.IsTriggered = false;

        await _db.SaveChangesAsync();
    }

    private static VerifierDto MapToDto(Verifier v) => new()
    {
        Id = v.Id,
        Name = v.Name,
        Email = v.Email,
        Phone = v.Phone,
        Status = v.Status.ToString(),
        HasReportedDeath = v.HasReportedDeath,
        CreatedAt = v.CreatedAt,
    };
}