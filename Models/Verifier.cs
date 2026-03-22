namespace dnotes_backend.Models;

public class Verifier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsConfirmed { get; set; } = false;
    public bool HasReportedDeath { get; set; } = false;
    public DateTime? DeathReportedAt { get; set; }
    public string? CertificateUrl { get; set; } // uploaded death certificate
    public string? CertificateNumber { get; set; }
    public VerifierStatus Status { get; set; } = VerifierStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReminderSentAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}

public enum VerifierStatus
{
    Pending = 0,
    Active = 1,
    DeathReported = 2,
    Confirmed = 3
}