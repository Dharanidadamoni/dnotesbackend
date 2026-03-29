using System.Security.Claims;
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Models;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IOtpService _otpService;
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _config;


    public AuthController(IAuthService authService, ILogger<AuthController> logger, IConfiguration config, IOtpService otpservice)
    {
        _otpService = otpservice;
        _authService = authService;
        _logger = logger;
        _config = config;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var ip = GetIpAddress();
        var response = await _authService.RegisterAsync(request, ip);

        return Ok(ApiResponse<RegisterResponse>.Ok(response,
            "Account created. Check your email for verification code."));
    }
    // POST /api/auth/verify-registration
    [HttpPost("verify-registration")]
    public async Task<IActionResult> VerifyRegistration(
        [FromBody] VerifyLoginOtpRequest request) // reuse same DTO
    {
        var ip = GetIpAddress();
        var response = await _authService.VerifyRegistrationOtpAsync(
            request.Email, request.Otp, ip);

        SetRefreshTokenCookie(response.RefreshToken);

        return Ok(ApiResponse<AuthResponse>.Ok(
            new AuthResponse
            {
                AccessToken = response.AccessToken,
                User = response.User
            },
            "Email verified. Welcome to DeathNote!"));
    }
    // POST /api/auth/resend-registration-otp
    [HttpPost("resend-registration-otp")]
    public async Task<IActionResult> ResendRegistrationOtp(
        [FromBody] SendLoginOtpRequest request)
    {
        await _authService.ResendRegistrationOtpAsync(request.Email);
        return Ok(ApiResponse.Ok("New OTP sent to your email."));
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
    // POST /api/auth/send-login-otp
    [HttpPost("send-login-otp")]
    public async Task<IActionResult> SendLoginOtp(
        [FromBody] SendLoginOtpRequest request)
    {
        await _authService.SendLoginOtpAsync(request.Email);
        return Ok(ApiResponse.Ok(
            "OTP sent to your email address."));
    }

    // POST /api/auth/verify-login-otp
    [HttpPost("verify-login-otp")]
    public async Task<IActionResult> VerifyLoginOtp(
        [FromBody] VerifyLoginOtpRequest request)
    {
        var ip = GetIpAddress();
        var response = await _authService.VerifyLoginOtpAsync(
            request.Email, request.Otp, ip);

        SetRefreshTokenCookie(response.RefreshToken);

        return Ok(ApiResponse<AuthResponse>.Ok(
            new AuthResponse
            {
                AccessToken = response.AccessToken,
                User = response.User,
            },
            "Login successful"));
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
    //GET /api/auth/google
    //Redirects user to Google login page
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action("GoogleCallback", "Auth");
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };
        return Challenge(properties, "Google");
    }
    //[HttpGet("google")]
    //public IActionResult GoogleLogin()
    //{
    //    return Challenge(new AuthenticationProperties(), "Google");
    //}
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync("Google");

        if (!result.Succeeded)
            return Redirect($"{_config["AppSettings:FrontendUrl"]}/login?error=google_failed");

        var email = result.Principal?.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var firstName = result.Principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? "";
        var lastName = result.Principal?.FindFirst(ClaimTypes.Surname)?.Value ?? "";

        if (string.IsNullOrEmpty(email))
            return Redirect($"{_config["AppSettings:FrontendUrl"]}/login?error=no_email");

        var ip = GetIpAddress();
        var response = await _authService.LoginOrCreateGoogleAsync(email, firstName, lastName, ip);

        SetRefreshTokenCookie(response.RefreshToken);

        // Redirect to frontend with access token in URL fragment
        // Frontend reads it and stores in context
        var frontendUrl = _config["AppSettings:FrontendUrl"];
        return Redirect($"{frontendUrl}/auth/callback?token={response.AccessToken}");
    }
}