using System.Security.Claims;
using dnotes_backend.Models;
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var ip = GetIpAddress();
        var response = await _authService.RegisterAsync(request, ip);

        _logger.LogInformation("New user registered: {Email}", request.Email);

        return Ok(ApiResponse<AuthResponse>.Ok(
            new AuthResponse { AccessToken = response.AccessToken, User = response.User },
            "Account created successfully"
        ));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = GetIpAddress();


        var response = await _authService.LoginAsync(request, ip);
        SetRefreshTokenCookie(response.RefreshToken);
        return Ok(ApiResponse<AuthResponse>.Ok(
            new AuthResponse { AccessToken = response.AccessToken, User = response.User },
            "Login successful"
        ));
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ApiResponse.Fail("No refresh token found"));

        var ip = GetIpAddress();
        var response = await _authService.RefreshTokenAsync(refreshToken, ip);
        SetRefreshTokenCookie(response.RefreshToken);

        return Ok(ApiResponse<AuthResponse>.Ok(
            new AuthResponse { AccessToken = response.AccessToken, User = response.User }
        ));
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var ip = GetIpAddress();
            await _authService.RevokeTokenAsync(refreshToken, ip);
        }

        // Clear the cookie
        Response.Cookies.Delete("refreshToken");
        return Ok(ApiResponse.Ok("Logged out successfully"));
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        var user = await _authService.GetCurrentUserAsync(userId);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    // PUT /api/auth/change-password
    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetCurrentUserId();
        await _authService.ChangePasswordAsync(userId, request);

        // Clear cookie to force re-login
        Response.Cookies.Delete("refreshToken");
        return Ok(ApiResponse.Ok("Password changed. Please log in again."));
    }

    // ── PRIVATE HELPERS ───────────────────────────
    private void SetRefreshTokenCookie(string token)
    {
        var isHttps = Request.IsHttps;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps, // require HTTPS in production; allow HTTP in dev
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(30),
            Path = "/" // allow SPA to send cookie on API calls
        };

        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException("User ID not found in token");

        return Guid.Parse(claim.Value);
    }

    private string GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            return forwardedFor.ToString().Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}