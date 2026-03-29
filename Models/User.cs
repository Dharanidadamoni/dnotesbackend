using dnotes_backend.Models;

namespace dnotes_backend.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // ── CONTACT ───────────────────────────────────
    public string? PhoneNumber { get; set; }  // encrypted
    public string? SecondaryPhoneNumber { get; set; }  // encrypted
    public bool PhoneVerified { get; set; } = false;
    public bool EmailVerified { get; set; } = false;

    // ── ADDRESS ───────────────────────────────────
    public string? State { get; set; }
    public string? Mandal { get; set; }
    public string? Village { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? Pincode { get; set; }

    // ── IDENTITY (stored encrypted) ───────────────
    public string? AadhaarEncrypted { get; set; }  // AES-256 encrypted
    public string? PanEncrypted { get; set; }  // AES-256 encrypted
    public bool IdentityVerified { get; set; } = false;

    // ── AUTH ──────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastCheckInAt { get; set; }
    public bool IsTriggered { get; set; } = false;
    public bool IsDeactivated { get; set; } = false;
    public DateTime? SleepModeUntil { get; set; }
    public string? ProfileImageUrl { get; set; }

    // ── REMINDER TRACKING ─────────────────────────
    public DateTime? LastReminderSentAt { get; set; }
    public int ReminderCount { get; set; } = 0;

    // Computed
    public string FullName => $"{FirstName} {LastName}".Trim();

    // Navigation
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<OtpRecord> OtpRecords { get; set; } = new List<OtpRecord>();
    public Verifier? Verifier { get; set; }
}