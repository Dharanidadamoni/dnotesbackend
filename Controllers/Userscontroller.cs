using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using dnotes_backend.Data;
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Controllers;

 //── DTOs(inline for simplicity) ──────────────────
//public class UpdateProfileRequest
//{
//    [MaxLength(100)] public string? FirstName { get; set; }
//    [MaxLength(100)] public string? LastName { get; set; }
//}

public class SleepModeRequest
{
    public bool Enable { get; set; }
    public DateTime? ResumeAt { get; set; }
}

public class CheckInResponse
{
    public DateTime CheckedInAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ── CONTROLLER ────────────────────────────────────
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    // GET /api/users/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var user = await _db.Users
            .Include(u => u.Verifier)
            .Include(u => u.Messages)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        var dto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt,
            HasVerifier = user.Verifier != null,
            IsTriggered = user.IsTriggered,
            SleepModeUntil = user.SleepModeUntil,
        };

        return Ok(ApiResponse<UserDto>.Ok(dto));
    }
    // POST /api/trigger/run-now   [DEV/ADMIN only]
    // Manually fire the trigger check — for testing
    [HttpPost("trigger/run-now")]
    [Authorize]
    public async Task<IActionResult> RunTriggerNow(
        [FromServices] IServiceScopeFactory scopeFactory,
        [FromServices] IEncryptionService enc,
        [FromServices] IEmailService email,
        [FromServices] AppDbContext db)
    {
        // Only allow in development!
        //if (!_env.IsDevelopment())
        //    return Forbid();

        // Run the same logic as DeathTriggerService
        var now = DateTime.UtcNow;

        TimeZoneInfo ist;
        try { ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch { ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }

        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(now, ist);
        var today = nowIst.ToString("yyyy-MM-dd");

        var messages = await db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipients)
            .Where(m =>
                !m.IsDelivered &&
                !m.IsDraft &&
                m.DeliveryType == "specific" &&
                m.EncryptedDeliveryDate != null)
            .ToListAsync();

        var triggered = new List<string>();

        foreach (var msg in messages)
        {
            try
            {
                var date = enc.Decrypt(msg.EncryptedDeliveryDate!);
                if (date == today)
                {
                    foreach (var r in msg.Recipients.Where(r => !r.IsNotified))
                    {
                        await email.SendDeathNotificationAsync(r, msg, msg.Sender);
                        r.IsNotified = true;
                        r.NotifiedAt = DateTime.UtcNow;
                        triggered.Add(r.Email);
                    }
                    msg.IsDelivered = true;
                    msg.DeliveredAt = DateTime.UtcNow;
                }
            }
            catch { /* log */ }
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            today,
            //checked    = messages.Count,
            triggered = triggered,
            message = $"Triggered {triggered.Count} deliveries for today ({today})"
        });
    }

    // PUT /api/users/profile
    //[HttpPut("profile")]
    //public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    //{
    //    var userId = GetUserId();
    //    var user = await _db.Users.FindAsync(userId)
    //        ?? throw new KeyNotFoundException("User not found.");

    //    if (request.FirstName != null) user.FirstName = request.FirstName.Trim();
    //    if (request.LastName != null) user.LastName = request.LastName.Trim();

    //    await _db.SaveChangesAsync();
    //    return Ok(ApiResponse.Ok("Profile updated."));
    //}

    // POST /api/users/checkin
    // Resets the death trigger timer — user confirms they're alive
    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn()
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        user.LastCheckInAt = DateTime.UtcNow;
        user.LastLoginAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CheckInResponse>.Ok(new CheckInResponse
        {
            CheckedInAt = user.LastCheckInAt!.Value,
            Message = "Check-in successful. Your timer has been reset."
        }));
    }

    // POST /api/users/sleep-mode
    // User going on vacation — pause triggers
    [HttpPost("sleep-mode")]
    public async Task<IActionResult> SetSleepMode([FromBody] SleepModeRequest request)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (request.Enable)
        {
            if (request.ResumeAt is null || request.ResumeAt <= DateTime.UtcNow)
                return BadRequest(ApiResponse.Fail("ResumeAt must be a future date."));

            if (request.ResumeAt > DateTime.UtcNow.AddMonths(6))
                return BadRequest(ApiResponse.Fail("Sleep mode cannot exceed 6 months."));

            user.SleepModeUntil = request.ResumeAt;
            await _db.SaveChangesAsync();

            return Ok(ApiResponse.Ok(
                $"Sleep mode enabled until {request.ResumeAt:dd MMM yyyy}."));
        }
        else
        {
            user.SleepModeUntil = null;
            user.LastCheckInAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(ApiResponse.Ok("Sleep mode disabled. Timer reset."));
        }
    }

    // DELETE /api/users/account
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Soft delete — deactivate instead of hard delete
        // (important: messages may already be delivered)
        user.IsDeactivated = true;
        user.Email = $"deleted_{userId}@deleted.com";
        user.PasswordHash = string.Empty;
        user.FirstName = "Deleted";
        user.LastName = "User";

        // Revoke all refresh tokens
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();
        tokens.ForEach(t => t.IsRevoked = true);

        await _db.SaveChangesAsync();

        Response.Cookies.Delete("refreshToken");
        return Ok(ApiResponse.Ok("Account deleted."));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}