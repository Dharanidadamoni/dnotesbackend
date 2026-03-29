using System.Security.Cryptography;
using System.Text;
using dnotes_backend.Data;
using dnotes_backend.DTOs;
using dnotes_backend.Models;
using dnotes_backend.Services;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;

namespace dnotes_backend.Services;

public interface IRazorpayService
{
    Task<RazorpayOrderResponse> CreateOrderAsync(Guid recipientId);
    Task<bool> VerifyPaymentAsync(VerifyPaymentRequest request);
    Task<UnlockedMessageDto> GetUnlockedMessageAsync(Guid recipientId);
}

public class RazorpayService : IRazorpayService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly ILogger<RazorpayService> _logger;
    private readonly string _keyId;
    private readonly string _keySecret;

    public RazorpayService(
        AppDbContext db,
        IConfiguration config,
        IEmailService email,
        ILogger<RazorpayService> logger)
    {
        _db = db;
        _config = config;
        _email = email;
        _logger = logger;
        _keyId = config["Razorpay:KeyId"]!;
        _keySecret = config["Razorpay:KeySecret"]!;
    }

    // ── CREATE ORDER ──────────────────────────────
    public async Task<RazorpayOrderResponse> CreateOrderAsync(Guid recipientId)
    {
        // Validate recipient exists and message was delivered
        var recipient = await _db.Recipients
            .Include(r => r.Message)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(r => r.Id == recipientId)
            ?? throw new KeyNotFoundException("Recipient not found.");

        if (!recipient.IsNotified)
            throw new InvalidOperationException("This message has not been delivered yet.");

        if (recipient.IsUnlocked)
            throw new InvalidOperationException("Message is already unlocked.");

        // Get amount from config (in paise — ₹399 = 39900 paise)
        var amountInPaise = long.Parse(_config["Razorpay:AmountInPaise"] ?? "39900");

        // Create Razorpay order
        var client = new RazorpayClient(_keyId, _keySecret);
        var options = new Dictionary<string, object>
        {
            { "amount",   amountInPaise },
            { "currency", "INR" },
            { "receipt", $"rcpt_{recipientId.ToString("N").Substring(0, 20)}" }, 
            { "notes", new Dictionary<string, string>
                {
                    { "recipientId", recipientId.ToString() },
                    { "messageId",   recipient.MessageId.ToString() },
                    { "senderName",  recipient.Message.Sender.FullName }
                }
            }
        };

        var order = client.Order.Create(options);
        var orderId = order["id"].ToString()!;

        //_logger.LogInformation(
        //    "Razorpay order created: {OrderId} for recipient {RecipientId}",
        //    orderId, recipientId);

        // Save pending unlock
        var existing = await _db.MessageUnlocks
            .FirstOrDefaultAsync(u => u.RecipientId == recipientId);

        if (existing is null)
        {
            _db.MessageUnlocks.Add(new MessageUnlock
            {
                RecipientId = recipientId,
                StripePaymentIntentId = orderId, // reusing field for razorpay order id
                AmountPaid = amountInPaise / 100m,
                Currency = "INR",
                Status = "pending",
            });
        }
        else
        {
            existing.StripePaymentIntentId = orderId;
            existing.Status = "pending";
        }

        await _db.SaveChangesAsync();

        return new RazorpayOrderResponse
        {
            OrderId = orderId,
            Amount = amountInPaise,
            Currency = "INR",
            KeyId = _keyId,       // sent to frontend for checkout
            SenderName = recipient.Message.Sender.FullName,
            MessageTitle = recipient.Message.Title,
            RecipientName = recipient.Name,
        };
    }

    // ── VERIFY PAYMENT (after user pays) ──────────
    public async Task<bool> VerifyPaymentAsync(VerifyPaymentRequest request)
    {
        // Step 1: Verify Razorpay signature (HMAC SHA256)
        var isValid = VerifySignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            request.RazorpaySignature);

        if (!isValid)
        {
            _logger.LogWarning(
                "Invalid Razorpay signature for order {OrderId}", request.RazorpayOrderId);
            return false;
        }

        // Step 2: Mark recipient as unlocked
        var recipient = await _db.Recipients
            .Include(r => r.MessageUnlock)
            .Include(r => r.Message)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(r => r.Id == request.RecipientId);

        if (recipient is null || recipient.IsUnlocked) return false;

        recipient.IsUnlocked = true;

        if (recipient.MessageUnlock != null)
        {
            recipient.MessageUnlock.StripeSessionId = request.RazorpayPaymentId;
            recipient.MessageUnlock.Status = "completed";
            recipient.MessageUnlock.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Step 3: Send confirmation email
        await _email.SendPaymentConfirmationAsync(
            recipient.Email,
            recipient.Message.Sender.FullName);

        _logger.LogInformation(
            "Payment verified and message unlocked for recipient {RecipientId}",
            request.RecipientId);

        return true;
    }

    // ── GET UNLOCKED MESSAGE ──────────────────────
    public async Task<UnlockedMessageDto> GetUnlockedMessageAsync(Guid recipientId)
    {
        var recipient = await _db.Recipients
            .Include(r => r.Message)
                .ThenInclude(m => m.Sender)
            .Include(r => r.MessageUnlock)
            .FirstOrDefaultAsync(r => r.Id == recipientId)
            ?? throw new KeyNotFoundException("Recipient not found.");

        if (!recipient.IsUnlocked)
            throw new UnauthorizedAccessException("Message is locked. Payment required.");

        return new UnlockedMessageDto
        {
            RecipientId = recipient.Id,
            SenderName = recipient.Message.Sender.FullName,
            MessageTitle = recipient.Message.Title,
            EncryptedBody = recipient.Message.EncryptedBody,
            DeliveryType = recipient.Message.DeliveryType,
            UnlockedAt = recipient.MessageUnlock!.CompletedAt ?? DateTime.UtcNow,
            MessageCreatedAt = recipient.Message.CreatedAt,
        };
    }

    // ── PRIVATE: Verify Razorpay Signature ────────
    private bool VerifySignature(string orderId, string paymentId, string signature)
    {
        // Razorpay signature = HMAC-SHA256(orderId + "|" + paymentId, keySecret)
        var payload = $"{orderId}|{paymentId}";
        var keyBytes = Encoding.UTF8.GetBytes(_keySecret);
        var msgBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        var expectedSig = Convert.ToHexString(hash).ToLower();

        return expectedSig == signature.ToLower();
    }
}