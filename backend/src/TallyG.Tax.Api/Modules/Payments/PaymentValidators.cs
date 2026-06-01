using FluentValidation;

namespace TallyG.Tax.Api.Modules.Payments;

/// <summary>
/// FluentValidation rules for the Payments/Wallet/Coupons request DTOs. Discovered by the assembly
/// scan in Program.cs and run by the global validation filter; failures render as 422 problem+json.
/// </summary>
public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private static readonly string[] AllowedGateways = { "razorpay", "cashfree", "wallet" };

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.ReturnId).NotEmpty().WithMessage("returnId is required.");
        RuleFor(x => x.PlanCode).NotEmpty().WithMessage("planCode is required.").MaximumLength(60);
        RuleFor(x => x.CouponCode).MaximumLength(60);

        When(x => !string.IsNullOrWhiteSpace(x.Gateway), () =>
            RuleFor(x => x.Gateway!)
                .Must(g => AllowedGateways.Contains(g.Trim().ToLowerInvariant()))
                .WithMessage("gateway must be one of: razorpay, cashfree, wallet."));
    }
}

public sealed class VerifyPaymentRequestValidator : AbstractValidator<VerifyPaymentRequest>
{
    public VerifyPaymentRequestValidator()
    {
        RuleFor(x => x.GatewayPaymentId).NotEmpty().WithMessage("gatewayPaymentId is required.");
        RuleFor(x => x.Signature).NotEmpty().WithMessage("signature is required.");
    }
}

public sealed class GatewayWebhookRequestValidator : AbstractValidator<GatewayWebhookRequest>
{
    public GatewayWebhookRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("orderId is required.");
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage("paymentId is required.");
        RuleFor(x => x.Signature).NotEmpty().WithMessage("signature is required.");
    }
}

public sealed class WalletCreditRequestValidator : AbstractValidator<WalletCreditRequest>
{
    public WalletCreditRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("amount must be greater than zero.");
        RuleFor(x => x.Note).MaximumLength(280);
    }
}

public sealed class CouponValidateRequestValidator : AbstractValidator<CouponValidateRequest>
{
    public CouponValidateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("code is required.").MaximumLength(60);
        RuleFor(x => x.PlanCode).NotEmpty().WithMessage("planCode is required.").MaximumLength(60);
    }
}

public sealed class CouponApplyRequestValidator : AbstractValidator<CouponApplyRequest>
{
    public CouponApplyRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("code is required.").MaximumLength(60);
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage("paymentId is required.");
    }
}
