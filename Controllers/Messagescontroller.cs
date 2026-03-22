using System.Security.Claims;
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
        => _messageService = messageService;

    // GET /api/messages?page=1&pageSize=10&filter=saved
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? filter = null)
    {
        var userId = GetUserId();
        var result = await _messageService.GetAllAsync(userId, page, pageSize, filter);
        return Ok(ApiResponse<PagedResponse<MessageSummaryDto>>.Ok(result));
    }

    // GET /api/messages/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var userId = GetUserId();
        var stats = await _messageService.GetDashboardStatsAsync(userId);
        return Ok(ApiResponse<DashboardStatsDto>.Ok(stats));
    }

    // GET /api/messages/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var message = await _messageService.GetByIdAsync(userId, id);
        return Ok(ApiResponse<MessageDto>.Ok(message));
    }

    // POST /api/messages
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMessageRequest request)
    {
        var userId = GetUserId();
        var message = await _messageService.CreateAsync(userId, request);
        return CreatedAtAction(nameof(GetById),
            new { id = message.Id },
            ApiResponse<MessageDto>.Ok(message, "Message saved"));
    }

    // PUT /api/messages/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMessageRequest request)
    {
        var userId = GetUserId();
        var message = await _messageService.UpdateAsync(userId, id, request);
        return Ok(ApiResponse<MessageDto>.Ok(message, "Message updated"));
    }

    // DELETE /api/messages/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        await _messageService.DeleteAsync(userId, id);
        return Ok(ApiResponse.Ok("Message deleted"));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? throw new UnauthorizedAccessException();
        return Guid.Parse(claim.Value);
    }
}