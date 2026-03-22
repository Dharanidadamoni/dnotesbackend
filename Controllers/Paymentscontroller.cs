
using dnotes_backend.DTOs;
using dnotes_backend.Helpers;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace dnotes_backend.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IRazorpayService _razorpay;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IRazorpayService razorpay, ILogger<PaymentsController> logger)
    {
        _razorpay = razorpay;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    // POST /api/payments/create-order
    // Called when receiver clicks "Unlock message"
    // Returns Razorpay order details for frontend checkout
    // ─────────────────────────────────────────────
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = await _razorpay.CreateOrderAsync(request.RecipientId);
        return Ok(ApiResponse<RazorpayOrderResponse>.Ok(order,
            "Order created. Proceed to payment."));
    }

    // ─────────────────────────────────────────────
    // POST /api/payments/verify
    // Called AFTER user completes payment on Razorpay checkout
    // Frontend sends back the payment details for verification
    // ─────────────────────────────────────────────
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        var isValid = await _razorpay.VerifyPaymentAsync(request);

        if (!isValid)
        {
            _logger.LogWarning("Payment verification failed for recipient {Id}",
                request.RecipientId);
            return BadRequest(ApiResponse.Fail(
                "Payment verification failed. Please contact support."));
        }

        return Ok(ApiResponse.Ok("Payment verified. Message unlocked successfully!"));
    }

    // ─────────────────────────────────────────────
    // GET /api/payments/unlock/{recipientId}
    // Called after successful payment to retrieve message
    // ─────────────────────────────────────────────
    [HttpGet("unlock/{recipientId:guid}")]
    public async Task<IActionResult> GetUnlockedMessage(Guid recipientId)
    {
        var message = await _razorpay.GetUnlockedMessageAsync(recipientId);
        return Ok(ApiResponse<UnlockedMessageDto>.Ok(message));
    }
}