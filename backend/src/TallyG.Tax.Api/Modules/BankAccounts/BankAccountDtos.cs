using FluentValidation;

namespace TallyG.Tax.Api.Modules.BankAccounts;

/// <summary>Bank account as returned to the client. The account number is masked (PII) — only the
/// last four digits are shown.</summary>
public sealed record BankAccountDto(
    Guid Id,
    string BankName,
    string AccountNumberMasked,
    string AccountType,
    string Ifsc,
    bool UseForRefund);

/// <summary>POST body to add a bank account. All four fields are mandatory (ITR BankDetailType).</summary>
public sealed record UpsertBankAccountRequest(
    string BankName,
    string AccountNumber,
    string AccountType,
    string Ifsc,
    bool UseForRefund = false);

public sealed class UpsertBankAccountRequestValidator : AbstractValidator<UpsertBankAccountRequest>
{
    // ITR AccountType enum.
    private static readonly string[] AccountTypes = { "SB", "CA", "CC", "OD", "NRO", "OTH" };

    public UpsertBankAccountRequestValidator()
    {
        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Bank name is required.")
            .MaximumLength(100);

        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Account number is required.")
            .Matches("^[0-9]{9,18}$").WithMessage("Account number must be 9–18 digits.");

        RuleFor(x => x.AccountType)
            .Must(t => t is not null && AccountTypes.Contains(t.Trim().ToUpperInvariant()))
            .WithMessage("Account type must be one of: SB, CA, CC, OD, NRO, OTH.");

        RuleFor(x => x.Ifsc)
            .NotEmpty().WithMessage("IFSC code is required.")
            .Matches("^[A-Z]{4}0[A-Z0-9]{6}$").WithMessage("Enter a valid 11-character IFSC (e.g. HDFC0001234).");
    }
}
