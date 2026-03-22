using System.Security.Claims;
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/verifier")]
public class VerifierController : ControllerBase
{
    private readonly IVerifierService _verifierService;

    public VerifierController(IVerifierService verifierService)
        => _verifierService = verifierService;

    // GET /api/verifier
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        var verifier = await _verifierService.GetVerifierAsync(userId);
        return Ok(ApiResponse<VerifierDto?>.Ok(verifier));
    }

    // POST /api/verifier
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Set([FromBody] SetVerifierRequest request)
    {
        var userId = GetUserId();
        var verifier = await _verifierService.SetVerifierAsync(userId, request);
        return Ok(ApiResponse<VerifierDto>.Ok(verifier,
            "Verifier saved. They have been notified by email."));
    }

    // DELETE /api/verifier
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Delete()
    {
        var userId = GetUserId();
        await _verifierService.DeleteVerifierAsync(userId);
        return Ok(ApiResponse.Ok("Verifier removed."));
    }

    // POST /api/verifier/{verifierId}/report-death  (no auth — verifier uses this)
    [HttpPost("{verifierId:guid}/report-death")]
    public async Task<IActionResult> ReportDeath(
        Guid verifierId, [FromBody] ReportDeathRequest request)
    {
        await _verifierService.ReportDeathAsync(verifierId, request);
        return Ok(ApiResponse.Ok(
            "Death reported. A 48-hour safety period has begun. " +
            "Messages will be delivered after 48 hours."));
    }

    // POST /api/verifier/cancel-report  (sender clicks "I'm alive")
    [HttpPost("cancel-report")]
    [Authorize]
    public async Task<IActionResult> CancelReport()
    {
        var userId = GetUserId();
        await _verifierService.CancelDeathReportAsync(userId);
        return Ok(ApiResponse.Ok("Death report cancelled. Your messages are safe."));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}
