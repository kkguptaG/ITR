using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TallyG.Tax.Api.Modules.EReturn;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.EReturn;

/// <summary>
/// The business-rule layer in <see cref="ItrJsonValidationService"/> — the cross-field checks that catch
/// portal-rejection conditions BEYOND structural schema conformance (refund⇒bank, regime⇄deduction
/// interlock, Chapter VI-A ceilings, presumptive-scheme eligibility). Each rule is asserted by its code.
/// An AY with no bundled schema is used so schema-conformance noise doesn't crowd the assertions.
/// </summary>
public class ItrBusinessRulesTests
{
    private static readonly ItrJsonValidationService Svc = new();
    private const string StubJson = "{\"ITR\":{\"ITR2\":{}}}";

    private static bool Has(ValidationReportDto r, string code) => r.Issues.Any(i => i.Code == code);

    [Fact]
    public void Refund_due_without_a_bank_account_is_an_error()
    {
        var report = Svc.Validate(Ctx(refundOrPayable: 8_400m), StubJson);
        Has(report, "REFUND.BANK_MISSING").Should().BeTrue();
    }

    [Fact]
    public void Refund_due_with_an_unflagged_account_warns_but_is_not_an_error()
    {
        var banks = new[] { Bank(useForRefund: false) };
        var report = Svc.Validate(Ctx(refundOrPayable: 8_400m, banks: banks), StubJson);

        Has(report, "REFUND.BANK_MISSING").Should().BeFalse();
        Has(report, "REFUND.NO_ACCOUNT_FLAGGED").Should().BeTrue();
    }

    [Fact]
    public void New_regime_disallowed_deductions_are_flagged_and_old_regime_is_not()
    {
        var ded = new[] { Deduct("80C", 100_000m), Deduct("80CCD(2)", 40_000m) };

        var newRegime = Svc.Validate(Ctx(regime: Regime.New, deductions: ded), StubJson);
        Has(newRegime, "REGIME.DEDUCTION_IGNORED").Should().BeTrue("80C is disallowed under the new regime");

        var oldRegime = Svc.Validate(Ctx(regime: Regime.Old, deductions: ded), StubJson);
        Has(oldRegime, "REGIME.DEDUCTION_IGNORED").Should().BeFalse("the old regime allows 80C");
    }

    [Fact]
    public void Eighty_C_claimed_over_the_ceiling_warns()
    {
        var ded = new[] { Deduct("80C", 120_000m), Deduct("80CCC", 60_000m) };   // ₹1.8L > ₹1.5L
        var report = Svc.Validate(Ctx(regime: Regime.Old, deductions: ded), StubJson);
        Has(report, "DEDUCTION.80C_CAP").Should().BeTrue();
    }

    [Fact]
    public void Presumptive_44AD_turnover_over_the_cap_is_an_error()
    {
        var biz = new[] { new BusinessIncome { IsPresumptive = true, PresumptiveSection = "44AD", Turnover = 35_000_000m, GrossReceiptsCash = 0m, NetProfit = 3_000_000m } };
        var report = Svc.Validate(Ctx(businesses: biz), StubJson);
        Has(report, "PRESUMPTIVE.44AD_TURNOVER").Should().BeTrue();
    }

    [Fact]
    public void Presumptive_44ADA_below_minimum_margin_warns()
    {
        var biz = new[] { new BusinessIncome { IsPresumptive = true, PresumptiveSection = "44ADA", Turnover = 1_000_000m, NetProfit = 100_000m } };
        var report = Svc.Validate(Ctx(businesses: biz), StubJson);
        Has(report, "PRESUMPTIVE.44ADA_MARGIN").Should().BeTrue();
    }

    // ----------------------------------------------------------------- builders
    private static ItrFilingContext Ctx(
        decimal refundOrPayable = 0m,
        Regime regime = Regime.New,
        IReadOnlyList<BankAccountDetail>? banks = null,
        IReadOnlyList<Deduction>? deductions = null,
        IReadOnlyList<BusinessIncome>? businesses = null)
        => new()
        {
            // AY2024-25 has no bundled schema → SchemaAvailable=false → no conformance noise.
            Return = new TaxReturn { ItrType = ItrType.ITR2, Regime = regime, RuleSetVersion = "AY2024-25" },
            User = new User { FullName = "Demo Taxpayer", Email = "demo@itrhelp.com", MobileE164 = "+919000000002", PanMasked = "ABCDE1234F" },
            Profile = new UserProfile { City = "Pune", StateCode = "27", Pincode = "411001", Dob = new DateOnly(1990, 1, 1) },
            Ay = new AssessmentYear { Code = "AY2024-25" },
            Computation = new TaxComputation { Regime = regime, GrossTotalIncome = 800_000m, TaxableIncome = 800_000m, RefundOrPayable = refundOrPayable },
            Salaries = new[] { new SalaryDetail { Employer = "Acme", Gross = 900_000m } },
            BankAccounts = banks ?? Array.Empty<BankAccountDetail>(),
            Deductions = deductions ?? Array.Empty<Deduction>(),
            Businesses = businesses ?? Array.Empty<BusinessIncome>(),
        };

    private static BankAccountDetail Bank(bool useForRefund) => new()
    {
        BankName = "HDFC Bank", AccountNumber = "50100123456789", AccountType = "SB", Ifsc = "HDFC0001234", UseForRefund = useForRefund,
    };

    private static Deduction Deduct(string section, decimal amount) => new() { Section = section, Amount = amount };
}
