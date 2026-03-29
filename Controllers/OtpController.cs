// ════════════════════════════════════════════════
// Controllers/OtpController.cs
// ════════════════════════════════════════════════
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/otp")]
[Authorize]
public class OtpController : ControllerBase
{
    private readonly IOtpService _otp;

    public OtpController(IOtpService otp) => _otp = otp;

    // POST /api/otp/send-email
    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmailOtp(
        [FromBody] SendOtpRequest req)
    {
        var userId = GetUserId();
        var maskedTo = await _otp.SendEmailOtpAsync(userId, req.Purpose);
        return Ok(ApiResponse<object>.Ok(
            new { sentTo = maskedTo },
            $"OTP sent to {maskedTo}"));
    }

    // POST /api/otp/send-phone
    [HttpPost("send-phone")]
    public async Task<IActionResult> SendPhoneOtp(
        [FromBody] SendPhoneOtpRequest req)
    {
        var userId = GetUserId();
        var maskedTo = await _otp.SendPhoneOtpAsync(userId, req.PhoneNumber, req.Purpose);
        return Ok(ApiResponse<object>.Ok(
            new { sentTo = maskedTo },
            $"OTP sent to {maskedTo}"));
    }

    // POST /api/otp/verify
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest req)
    {
        var userId = GetUserId();
        var isValid = await _otp.VerifyOtpAsync(userId, req.Purpose, req.Otp);

        if (!isValid)
            return BadRequest(ApiResponse.Fail(
                "Invalid or expired OTP. Please try again."));

        return Ok(ApiResponse.Ok("Verified successfully."));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}

public class SendOtpRequest
{
    [Required]
    public string Purpose { get; set; } = "email_verify";
}

public class SendPhoneOtpRequest
{
    [Required, Phone]
    public string PhoneNumber { get; set; } = string.Empty;
    [Required]
    public string Purpose { get; set; } = "phone_verify";
}

public class VerifyOtpRequest
{
    [Required, StringLength(6, MinimumLength = 6)]
    public string Otp { get; set; } = string.Empty;
    [Required]
    public string Purpose { get; set; } = string.Empty;
}