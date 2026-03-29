namespace dnotes_backend.Models;

public class OtpRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }

    /// <summary>
    /// "email" or "phone"
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// "email_verify" | "phone_verify" | "login" | "password_reset"
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// BCRYPT HASH of the 6-digit OTP — never stored in plain text
    /// </summary>
    public string OtpHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsUsed { get; set; } = false;
    public int Attempts { get; set; } = 0;  // max 3 attempts
    public string? SentTo { get; set; }         // masked email/phone for display

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired && Attempts < 3;

    // Navigation
    public User User { get; set; } = null!;
}