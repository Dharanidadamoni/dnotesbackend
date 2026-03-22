using System.Security.Claims;
using dnotes_backend.Data;
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/messages/{messageId:guid}/recipients")]
[Authorize]
public class RecipientsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecipientsController(AppDbContext db) => _db = db;

    // GET /api/messages/{messageId}/recipients
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid messageId)
    {
        var userId = GetUserId();

        // Verify message belongs to this user
        var message = await _db.Messages
            .Include(m => m.Recipients)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message is null)
            return NotFound(ApiResponse.Fail("Message not found."));

        var recipients = message.Recipients.Select(r => new RecipientDto
        {
            Id = r.Id,
            Email = r.Email,
            Name = r.Name,
            Relationship = r.Relationship,
            IsUnlocked = r.IsUnlocked,
            IsNotified = r.IsNotified,
        }).ToList();

        return Ok(ApiResponse<List<RecipientDto>>.Ok(recipients));
    }

    // POST /api/messages/{messageId}/recipients
    [HttpPost]
    public async Task<IActionResult> Add(
        Guid messageId,
        [FromBody] CreateRecipientRequest request)
    {
        var userId = GetUserId();

        var message = await _db.Messages
            .Include(m => m.Recipients)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message is null)
            return NotFound(ApiResponse.Fail("Message not found."));

        if (message.IsDelivered)
            return BadRequest(ApiResponse.Fail("Cannot add recipients to a delivered message."));

        // Prevent duplicate email on same message
        if (message.Recipients.Any(r =>
            r.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(ApiResponse.Fail("This recipient is already added."));

        var recipient = new Models.Recipient
        {
            MessageId = messageId,
            Email = request.Email.ToLower().Trim(),
            Name = request.Name.Trim(),
            Relationship = request.Relationship,
        };

        _db.Recipients.Add(recipient);
        await _db.SaveChangesAsync();

        var dto = new RecipientDto
        {
            Id = recipient.Id,
            Email = recipient.Email,
            Name = recipient.Name,
            Relationship = recipient.Relationship,
            IsUnlocked = false,
            IsNotified = false,
        };

        return CreatedAtAction(nameof(GetAll),
            new { messageId },
            ApiResponse<RecipientDto>.Ok(dto, "Recipient added."));
    }

    // PUT /api/messages/{messageId}/recipients/{recipientId}
    [HttpPut("{recipientId:guid}")]
    public async Task<IActionResult> Update(
        Guid messageId,
        Guid recipientId,
        [FromBody] CreateRecipientRequest request)
    {
        var userId = GetUserId();

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message is null)
            return NotFound(ApiResponse.Fail("Message not found."));

        var recipient = await _db.Recipients
            .FirstOrDefaultAsync(r => r.Id == recipientId && r.MessageId == messageId);

        if (recipient is null)
            return NotFound(ApiResponse.Fail("Recipient not found."));

        recipient.Name = request.Name.Trim();
        recipient.Email = request.Email.ToLower().Trim();
        recipient.Relationship = request.Relationship;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Recipient updated."));
    }

    // DELETE /api/messages/{messageId}/recipients/{recipientId}
    [HttpDelete("{recipientId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, Guid recipientId)
    {
        var userId = GetUserId();

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

        if (message is null)
            return NotFound(ApiResponse.Fail("Message not found."));

        var recipient = await _db.Recipients
            .FirstOrDefaultAsync(r => r.Id == recipientId && r.MessageId == messageId);

        if (recipient is null)
            return NotFound(ApiResponse.Fail("Recipient not found."));

        if (recipient.IsNotified)
            return BadRequest(ApiResponse.Fail("Cannot remove a recipient who has already been notified."));

        _db.Recipients.Remove(recipient);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Recipient removed."));
    }

    // ── PRIVATE ───────────────────────────────────
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}
