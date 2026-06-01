using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// Filing-fee payments + pricing (docs 04 §4.2). Thin actions delegating to <see cref="IPaymentService"/>;
/// errors surface as RFC 7807 problem+json via the global middleware. Action sub-resources use the
/// project ":verb" convention (e.g. <c>{id}:verify</c>). Webhooks are anonymous but signature-verified.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    /// <summary>Header carrying the client-generated idempotency token for money-moving POSTs.</summary>
    public const string IdempotencyHeader = "Idempotency-Key";

    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments) => _payments = payments;

    /// <summary>List the active filing-fee plans/SKUs.</summary>
    [HttpGet("pricing/plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PlanDto>> GetPlans(CancellationToken ct) => _payments.GetPlansAsync(ct);

    /// <summary>Create a payment order for a return's filing fee (idempotent via Idempotency-Key).</summary>
    [HttpPost("payments/orders")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status200OK)]
    public Task<CreateOrderResponse> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
        => _payments.CreateOrderAsync(request, ReadIdempotencyKey(), ct);

    /// <summary>Verify the gateway signature, capture the payment, issue the invoice, mark the return Paid.</summary>
    [HttpPost("payments/{id:guid}:verify")]
    [ProducesResponseType(typeof(VerifyPaymentResponse), StatusCodes.Status200OK)]
    public Task<VerifyPaymentResponse> Verify(Guid id, [FromBody] VerifyPaymentRequest request, CancellationToken ct)
        => _payments.VerifyAsync(id, request, ct);

    /// <summary>List the caller's payments.</summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(PagedResult<PaymentDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<PaymentDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _payments.ListAsync(page, pageSize, ct);

    /// <summary>Get one of the caller's payments.</summary>
    [HttpGet("payments/{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    public Task<PaymentDto> Get(Guid id, CancellationToken ct) => _payments.GetAsync(id, ct);

    /// <summary>Download/inspect the GST invoice for a captured payment.</summary>
    [HttpGet("payments/{id:guid}/invoice")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    public Task<InvoiceDto> GetInvoice(Guid id, CancellationToken ct) => _payments.GetInvoiceAsync(id, ct);

    /// <summary>Refund a captured payment (Ops/Admin only).</summary>
    [HttpPost("payments/{id:guid}/refund")]
    [Authorize(Roles = "Ops,Admin,SuperAdmin")]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status200OK)]
    public Task<RefundResponse> Refund(Guid id, [FromBody] RefundRequest request, CancellationToken ct)
        => _payments.RefundAsync(id, request, ct);

    /// <summary>Razorpay webhook (anonymous; HMAC-verified against the order/payment ids).</summary>
    [HttpPost("webhooks/razorpay")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RazorpayWebhook([FromBody] GatewayWebhookRequest request, CancellationToken ct)
    {
        await _payments.HandleWebhookAsync("razorpay", request, ct);
        return Ok(new { received = true });
    }

    /// <summary>Cashfree webhook (anonymous; signature-verified).</summary>
    [HttpPost("webhooks/cashfree")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CashfreeWebhook([FromBody] GatewayWebhookRequest request, CancellationToken ct)
    {
        await _payments.HandleWebhookAsync("cashfree", request, ct);
        return Ok(new { received = true });
    }

    private string? ReadIdempotencyKey()
        => Request.Headers.TryGetValue(IdempotencyHeader, out var values)
            ? values.ToString()
            : null;
}
