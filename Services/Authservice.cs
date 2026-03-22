using dnotes_backend.Helpers;
using dnotes_backend.Data;
using dnotes_backend.DTOs;
using dnotes_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task RevokeTokenAsync(string refreshToken, string ipAddress);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtHelper _jwt;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IJwtHelper jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
    }

    // ── REGISTER ──────────────────────────────────
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, string ip)
    {
        // Check if email already exists
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new User
        {
            Email = req.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            LastLoginAt = DateTime.UtcNow,
            LastCheckInAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
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