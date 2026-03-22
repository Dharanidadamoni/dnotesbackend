// ════════════════════════════════════════════════
// DTOs/VerifierDtos.cs
// ════════════════════════════════════════════════
using System.ComponentModel.DataAnnotations;

namespace dnotes_backend.DTOs;

public class SetVerifierRequest
{
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }
}

public class ReportDeathRequest
{
    [Required]
    public string VerifierToken { get; set; } = string.Empty; // unique link token

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public string? CertificateUrl { get; set; } // uploaded file URL
}

public class VerifierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasReportedDeath { get; set; }
    public DateTime CreatedAt { get; set; }
}