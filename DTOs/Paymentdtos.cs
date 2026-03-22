using System.ComponentModel.DataAnnotations;

namespace dnotes_backend.DTOs;

// ── REQUESTS ──────────────────────────────────────
public class CreateOrderRequest
{
    [Required]
    public Guid RecipientId { get; set; }
}

public class VerifyPaymentRequest
{
    [Required]
    public string RazorpayOrderId { get; set; } = string.Empty;

    [Required]
    public string RazorpayPaymentId { get; set; } = string.Empty;

    [Required]
    public string RazorpaySignature { get; set; } = string.Empty;

    [Required]
    public Guid RecipientId { get; set; }
}

// ── RESPONSES ─────────────────────────────────────
public class RazorpayOrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string KeyId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string MessageTitle { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
}

public class UnlockedMessageDto
{
    public Guid RecipientId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageTitle { get; set; } = string.Empty;
    public string EncryptedBody { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public DateTime UnlockedAt { get; set; }
    public DateTime MessageCreatedAt { get; set; }
}
