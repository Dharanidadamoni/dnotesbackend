using dnotes_backend.Helpers;
using dnotes_backend.Data;
using dnotes_backend.DTOs;
using dnotes_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Services;

public interface IAuthService
{
    Task ResendRegistrationOtpAsync(string email);
    Task SendLoginOtpAsync(string email);
    Task<AuthResponse> VerifyLoginOtpAsync(string email, string otp, string ip);
    Task<AuthResponse> VerifyRegistrationOtpAsync(string email, string otp, string ip);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, string ipAddress);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task RevokeTokenAsync(string refreshToken, string ipAddress);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<AuthResponse> LoginOrCreateGoogleAsync(string email, string firstName, string lastName, string ip);

}

public class AuthService : IAuthService
{
    private readonly IEmailService _emailService;
    private readonly AppDbContext _db;
    private readonly IJwtHelper _jwt;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IJwtHelper jwt, IConfiguration config, IEmailService emailService)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
        _emailService = emailService;
    }

    // ── REGISTER ──────────────────────────────────
    public async Task<RegisterResponse> RegisterAsync(
     RegisterRequest req, string ip)
    {
        // 1. Check duplicate email
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
            throw new InvalidOperationException(
                "An account with this email already exists.");

        // 2. Create user — NOT yet verified
        var user = new User
        {
            Email = req.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            EmailVerified = false,         // ← NOT verified yet
            LastCheckInAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 3. Generate OTP
        var otp = GenerateOtp();        // 6-digit secure OTP
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);

        _db.OtpRecords.Add(new OtpRecord
        {
            UserId = user.Id,
            Target = "email",
            Purpose = "register_verify",
            OtpHash = otpHash,
            SentTo = MaskEmail(user.Email),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });
        await _db.SaveChangesAsync();

        // 4. Send OTP email
        await _emailService.SendRegistrationOtpEmailAsync(
            user.Email, user.FirstName, otp);

        return new RegisterResponse
        {
            MaskedEmail = MaskEmail(user.Email),
            Message = "Account created. Please verify your email."
        };
    }
    public async Task SendLoginOtpAsync(string email)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower())
            ?? throw new KeyNotFoundException(
                "No account found with this email address.");

        if (user.IsDeactivated)
            throw new InvalidOperationException("This account has been deactivated.");

        // Invalidate old OTPs for this user+purpose
        var oldOtps = await _db.OtpRecords
            .Where(o => o.UserId == user.Id &&
                        o.Purpose == "login_otp" &&
                        !o.IsUsed)
            .ToListAsync();
        oldOtps.ForEach(o => o.IsUsed = true);

        // Generate 6-digit OTP
        var otp = GenerateOtp();
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);

        _db.OtpRecords.Add(new OtpRecord
        {
            UserId = user.Id,
            Target = "email",
            Purpose = "login_otp",
            OtpHash = otpHash,
            SentTo = MaskEmail(user.Email),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });
        await _db.SaveChangesAsync();

        // Send email
        await _emailService.SendOtpEmailAsync(user.Email, user.FirstName, otp,"email-verify");
    }
    public async Task<AuthResponse> VerifyRegistrationOtpAsync(
    string email, string otp, string ip)
    {
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower())
            ?? throw new KeyNotFoundException("Account not found.");

        // Find latest valid OTP for this user
        var record = await _db.OtpRecords
            .Where(o =>
                o.UserId == user.Id &&
                o.Purpose == "register_verify" &&
                !o.IsUsed &&
                o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (record is null)
            throw new InvalidOperationException(
                "OTP has expired. Please request a new one.");

        // Track attempts (max 3)
        record.Attempts++;
        if (record.Attempts > 3)
        {
            record.IsUsed = true;
            await _db.SaveChangesAsync();
            throw new InvalidOperationException(
                "Too many incorrect attempts. Please resend the OTP.");
        }

        // Verify
        var isValid = BCrypt.Net.BCrypt.Verify(otp, record.OtpHash);
        if (!isValid)
        {
            await _db.SaveChangesAsync();
            var remaining = 3 - record.Attempts;
            throw new UnauthorizedAccessException(
                $"Incorrect OTP. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
        }

        // ✅ OTP matched — mark verified
        record.IsUsed = true;
        user.EmailVerified = true;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastCheckInAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Now issue tokens — user is fully registered
        return await CreateAuthResponseAsync(user, ip);
    }
    // ── ALSO ADD: Resend OTP for registration ─────────

    public async Task ResendRegistrationOtpAsync(string email)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower())
            ?? throw new KeyNotFoundException("Account not found.");

        if (user.EmailVerified)
            throw new InvalidOperationException("Email is already verified.");

        // Invalidate old OTPs
        var old = await _db.OtpRecords
            .Where(o => o.UserId == user.Id &&
                        o.Purpose == "register_verify" &&
                        !o.IsUsed)
            .ToListAsync();
        old.ForEach(o => o.IsUsed = true);

        // New OTP
        var otp = GenerateOtp();
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);

        _db.OtpRecords.Add(new OtpRecord
        {
            UserId = user.Id,
            Target = "email",
            Purpose = "register_verify",
            OtpHash = otpHash,
            SentTo = MaskEmail(user.Email),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });
        await _db.SaveChangesAsync();

        await _emailService.SendRegistrationOtpEmailAsync(
            user.Email, user.FirstName, otp);
    }
    private static string GenerateOtp()
    {
        var bytes = new byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return (Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000).ToString("D6");
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
    /// <summary>
    /// Step 2: User submits OTP → verify → issue tokens → login
    /// </summary>
    public async Task<AuthResponse> VerifyLoginOtpAsync(
        string email, string otp, string ip)
    {
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower())
            ?? throw new KeyNotFoundException("Account not found.");

        // Find latest valid OTP
        var record = await _db.OtpRecords
            .Where(o =>
                o.UserId == user.Id &&
                o.Purpose == "login_otp" &&
                !o.IsUsed &&
                o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (record is null)
            throw new InvalidOperationException(
                "OTP has expired. Please request a new one.");

        // Increment attempt
        record.Attempts++;
        if (record.Attempts > 3)
        {
            record.IsUsed = true;
            await _db.SaveChangesAsync();
            throw new InvalidOperationException(
                "Too many incorrect attempts. Please request a new OTP.");
        }

        // Verify hash
        var isValid = BCrypt.Net.BCrypt.Verify(otp, record.OtpHash);
        if (!isValid)
        {
            await _db.SaveChangesAsync();
            throw new UnauthorizedAccessException(
                $"Incorrect OTP. {3 - record.Attempts} attempts remaining.");
        }

        // Mark OTP as used
        record.IsUsed = true;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastCheckInAt = DateTime.UtcNow;
        user.EmailVerified = true; // email confirmed via OTP
        await _db.SaveChangesAsync();

        return await CreateAuthResponseAsync(user, ip);
    }

    // ── LOGIN ─────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest req, string ip)
    {
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.IsDeactivated)
            throw new UnauthorizedAccessException("This account has been deactivated.");

        // Update last login + check-in
        user.LastLoginAt = DateTime.UtcNow;
        user.LastCheckInAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await CreateAuthResponseAsync(user, ip);
    }
    public async Task<AuthResponse> LoginOrCreateGoogleAsync(
    string email, string firstName, string lastName, string ip)
    {
        // Find existing user or create new one
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower());

        if (user is null)
        {
            // New user via Google — create account with random password
            user = new User
            {
                Email = email.ToLower().Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                LastLoginAt = DateTime.UtcNow,
                LastCheckInAt = DateTime.UtcNow,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LastCheckInAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return await CreateAuthResponseAsync(user, ip);
    }


    // ── REFRESH TOKEN ─────────────────────────────
    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ip)
    {
        var token = await _db.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.Verifier)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token is null || !token.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotate refresh token (issue new, revoke old)
        var newRefreshToken = CreateRefreshToken(token.UserId, ip);
        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ip;

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync();

        var accessToken = _jwt.GenerateAccessToken(token.User);
        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            User = MapToDto(token.User)
        };
    }

    // ── REVOKE (LOGOUT) ───────────────────────────
    public async Task RevokeTokenAsync(string refreshToken, string ip)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token is null || !token.IsActive)
            throw new KeyNotFoundException("Token not found.");

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ip;
        await _db.SaveChangesAsync();
    }

    // ── GET CURRENT USER ──────────────────────────
    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.Verifier)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        return MapToDto(user);
    }

    // ── CHANGE PASSWORD ───────────────────────────
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest req)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12);

        // Revoke all existing refresh tokens for security
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();
        tokens.ForEach(t => t.IsRevoked = true);

        await _db.SaveChangesAsync();
    }

    // ── PRIVATE HELPERS ───────────────────────────
    private async Task<AuthResponse> CreateAuthResponseAsync(User user, string ip)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = CreateRefreshToken(user.Id, ip);

        // Clean up old expired tokens
        var oldTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(oldTokens);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            User = MapToDto(user)
        };
    }

    private RefreshToken CreateRefreshToken(Guid userId, string ip)
    {
        var expiryDays = int.Parse(
            _config["JwtSettings:RefreshTokenExpiryDays"] ?? "30");

        return new RefreshToken
        {
            UserId = userId,
            Token = _jwt.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedByIp = ip
        };
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        FullName = user.FullName,
        CreatedAt = user.CreatedAt,
        HasVerifier = user.Verifier is not null,
        IsTriggered = user.IsTriggered,
        SleepModeUntil = user.SleepModeUntil
    };
}