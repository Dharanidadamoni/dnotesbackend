using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using dnotes_backend.Data;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Controllers;

// ── DTOs ──────────────────────────────────────────
public class UpdateProfileRequest
{
    [MaxLength(100)] public string? FirstName { get; set; }
    [MaxLength(100)] public string? LastName { get; set; }
    [Phone] public string? PhoneNumber { get; set; }
    [Phone] public string? SecondaryPhoneNumber { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    [MaxLength(100)] public string? Mandal { get; set; }
    [MaxLength(100)] public string? Village { get; set; }
    [MaxLength(200)] public string? Street { get; set; }
    [MaxLength(50)] public string? HouseNumber { get; set; }
    [MaxLength(10)] public string? Pincode { get; set; }
}

public class UpdateIdentityRequest
{
    /// <summary>
    /// Raw Aadhaar number — will be encrypted before storage
    /// </summary>
    [StringLength(12, MinimumLength = 12)]
    public string? AadhaarNumber { get; set; }

    /// <summary>
    /// Raw PAN number — will be encrypted before storage
    /// </summary>
    [StringLength(10, MinimumLength = 10)]
    public string? PanNumber { get; set; }
}

public class ProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } // masked for display
    public string? SecondaryPhoneNumber { get; set; } // masked
    public bool PhoneVerified { get; set; }
    public bool EmailVerified { get; set; }
    public bool IdentityVerified { get; set; }
    public bool HasAadhaar { get; set; }
    public bool HasPan { get; set; }
    public string? State { get; set; }
    public string? Mandal { get; set; }
    public string? Village { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? Pincode { get; set; }
    public bool HasVerifier { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? SleepModeUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── CONTROLLER ────────────────────────────────────
[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _enc;

    public ProfileController(AppDbContext db, IEncryptionService enc)
    {
        _db = db;
        _enc = enc;
    }

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        return Ok(ApiResponse<ProfileDto>.Ok(MapToDto(user)));
    }

    // PUT /api/profile
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (req.FirstName != null) user.FirstName = req.FirstName.Trim();
        if (req.LastName != null) user.LastName = req.LastName.Trim();
        if (req.State != null) user.State = req.State.Trim();
        if (req.Mandal != null) user.Mandal = req.Mandal.Trim();
        if (req.Village != null) user.Village = req.Village.Trim();
        if (req.Street != null) user.Street = req.Street.Trim();
        if (req.HouseNumber != null) user.HouseNumber = req.HouseNumber.Trim();
        if (req.Pincode != null) user.Pincode = req.Pincode.Trim();

        // Phone numbers — encrypt before storing
        if (req.PhoneNumber != null)
        {
            user.PhoneNumber = _enc.Encrypt(req.PhoneNumber.Trim());
            user.PhoneVerified = false; // needs re-verification
        }
        if (req.SecondaryPhoneNumber != null)
        {
            user.SecondaryPhoneNumber = _enc.Encrypt(req.SecondaryPhoneNumber.Trim());
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Profile updated."));
    }

    // PUT /api/profile/identity
    [HttpPut("identity")]
    public async Task<IActionResult> UpdateIdentity(
        [FromBody] UpdateIdentityRequest req)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Encrypt identity documents — nobody can read them from DB
        if (!string.IsNullOrEmpty(req.AadhaarNumber))
            user.AadhaarEncrypted = _enc.Encrypt(req.AadhaarNumber.Trim());

        if (!string.IsNullOrEmpty(req.PanNumber))
            user.PanEncrypted = _enc.Encrypt(req.PanNumber.Trim().ToUpper());

        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok(
            "Identity documents saved securely. " +
            "They are encrypted and cannot be read by anyone."));
    }

    // ── PRIVATE ───────────────────────────────────
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }

    private ProfileDto MapToDto(Models.User user)
    {
        // Decrypt and mask phone for display
        string? maskedPhone = null;
        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            try
            {
                var decrypted = _enc.Decrypt(user.PhoneNumber);
                maskedPhone = decrypted.Length >= 4
                    ? new string('*', decrypted.Length - 4) + decrypted[^4..]
                    : "****";
            }
            catch { maskedPhone = "****"; }
        }

        string? maskedSecondary = null;
        if (!string.IsNullOrEmpty(user.SecondaryPhoneNumber))
        {
            try
            {
                var decrypted = _enc.Decrypt(user.SecondaryPhoneNumber);
                maskedSecondary = decrypted.Length >= 4
                    ? new string('*', decrypted.Length - 4) + decrypted[^4..]
                    : "****";
            }
            catch { maskedSecondary = "****"; }
        }

        return new ProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            PhoneNumber = maskedPhone,
            SecondaryPhoneNumber = maskedSecondary,
            PhoneVerified = user.PhoneVerified,
            EmailVerified = user.EmailVerified,
            IdentityVerified = user.IdentityVerified,
            HasAadhaar = !string.IsNullOrEmpty(user.AadhaarEncrypted),
            HasPan = !string.IsNullOrEmpty(user.PanEncrypted),
            State = user.State,
            Mandal = user.Mandal,
            Village = user.Village,
            Street = user.Street,
            HouseNumber = user.HouseNumber,
            Pincode = user.Pincode,
            HasVerifier = user.Verifier != null,
            IsTriggered = user.IsTriggered,
            SleepModeUntil = user.SleepModeUntil,
            CreatedAt = user.CreatedAt,
        };
    }
}