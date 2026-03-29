
using dnotes_backend.Data;
using dnotes_backend.Models;
using dnotes_backend.Services;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Services;

public interface IOtpService
{
    Task<string> SendEmailOtpAsync(Guid userId, string purpose);
    Task<string> SendPhoneOtpAsync(Guid userId, string phoneNumber, string purpose);
    Task<bool> VerifyOtpAsync(Guid userId, string purpose, string otp);
}

public class OtpService : IOtpService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ISmsService _sms;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<OtpService> _logger;

    // OTP valid for 10 minutes
    private const int OTP_EXPIRY_MINUTES = 10;

    public OtpService(
        AppDbContext db,
        IEmailService email,
        ISmsService sms,
        IEncryptionService encryption,
        ILogger<OtpService> logger)
    {
        _db = db;
        _email = email;
        _sms = sms;
        _encryption = encryption;
        _logger = logger;
    }

    // ── SEND EMAIL OTP ────────────────────────────
    public async Task<string> SendEmailOtpAsync(Guid userId, string purpose)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var otp = GenerateOtp();
        var maskedTo = MaskEmail(user.Email);

        await SaveOtpAsync(userId, "email", purpose, otp, maskedTo);
        await _email.SendOtpEmailAsync(user.Email, user.FirstName, otp, purpose);

        _logger.LogInformation(
            "Email OTP sent to {Masked} for {Purpose}", maskedTo, purpose);

        return maskedTo; // return masked email so frontend can show "OTP sent to r****@gmail.com"
    }

    // ── SEND PHONE OTP ────────────────────────────
    public async Task<string> SendPhoneOtpAsync(
        Guid userId, string phoneNumber, string purpose)
    {
        var otp = GenerateOtp();
        var masked = MaskPhone(phoneNumber);

        await SaveOtpAsync(userId, "phone", purpose, otp, masked);
        await _sms.SendOtpSmsAsync(phoneNumber, otp);

        _logger.LogInformation(
            "Phone OTP sent to {Masked} for {Purpose}", masked, purpose);

        return masked;
    }

    // ── VERIFY OTP ────────────────────────────────
    public async Task<bool> VerifyOtpAsync(Guid userId, string purpose, string otp)
    {
        // Get latest unused, unexpired OTP for this user+purpose
        var record = await _db.OtpRecords
            .Where(o =>
                o.UserId == userId &&
                o.Purpose == purpose &&
                !o.IsUsed &&
                o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (record is null)
        {
            _logger.LogWarning(
                "No valid OTP found for user {UserId} purpose {Purpose}",
                userId, purpose);
            return false;
        }

        // Increment attempt count
        record.Attempts++;

        if (record.Attempts > 3)
        {
            record.IsUsed = true;
            await _db.SaveChangesAsync();
            throw new InvalidOperationException(
                "Too many attempts. Please request a new OTP.");
        }

        // Verify using BCrypt (OTP was stored as hash)
        var isValid = BCrypt.Net.BCrypt.Verify(otp, record.OtpHash);

        if (isValid)
        {
            record.IsUsed = true;

            // Mark relevant field as verified
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                if (record.Target == "email") user.EmailVerified = true;
                if (record.Target == "phone") user.PhoneVerified = true;
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "OTP verification {Result} for user {UserId} purpose {Purpose}",
            isValid ? "SUCCESS" : "FAILED", userId, purpose);

        return isValid;
    }

    // ── PRIVATE HELPERS ───────────────────────────

    private async Task SaveOtpAsync(
        Guid userId, string target, string purpose, string otp, string maskedTo)
    {
        // Invalidate any existing unused OTPs for same user+purpose
        var existing = await _db.OtpRecords
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.IsUsed)
            .ToListAsync();
        existing.ForEach(o => o.IsUsed = true);

        // Store OTP as BCrypt hash — NEVER store plain OTP
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);

        _db.OtpRecords.Add(new OtpRecord
        {
            UserId = userId,
            Target = target,
            Purpose = purpose,
            OtpHash = otpHash,
            SentTo = maskedTo,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRY_MINUTES),
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Generate cryptographically secure 6-digit OTP
    /// </summary>
    private static string GenerateOtp()
    {
        var bytes = new byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
        return number.ToString("D6"); // always 6 digits with leading zeros
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "****";
        var local = parts[0];
        var masked = local.Length <= 2
            ? new string('*', local.Length)
            : local[0] + new string('*', local.Length - 2) + local[^1];
        return $"{masked}@{parts[1]}";
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 4) return "****";
        return new string('*', phone.Length - 4) + phone[^4..];
    }
}