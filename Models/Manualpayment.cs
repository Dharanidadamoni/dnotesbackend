using dnotes_backend.Models;

namespace dnotes_backend.Models;

public class ManualPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }

    /// <summary>
    /// "upi" | "bank_transfer" | "other"
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Transaction ID / UTR number submitted by user
    /// </summary>
    public string TransactionRef { get; set; } = string.Empty;

    /// <summary>
    /// Screenshot URL (encrypted)
    /// </summary>
    public string? ScreenshotUrl { get; set; }

    public decimal AmountPaid { get; set; }

    /// <summary>
    /// "pending" | "verified" | "rejected"
    /// </summary>
    public string Status { get; set; } = "pending";

    public string? AdminNote { get; set; }
    public Guid? VerifiedByAdmin { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Recipient Recipient { get; set; } = null!;
}