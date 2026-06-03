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

    [Fact]
    public void Foreign_income_without_a_foreign_asset_disclosure_warns()
    {
        var fsi = new[] { new ForeignSourceIncome { CountryCode = "1", CountryName = "USA", TaxIdentificationNo = "123", Head = ForeignIncomeHead.OtherSources, IncomeFromOutsideIndia = 500_000m, TaxPaidOutsideIndia = 75_000m, ReliefSection = ForeignTaxReliefSection.Section90 } };
        var report = Svc.Validate(Ctx(foreignSourceIncomes: fsi, relief90And91: 75_000m), StubJson);
        Has(report, "FSI.NO_FOREIGN_ASSET").Should().BeTrue("foreign income is reported but no Schedule FA asset is disclosed");
    }

    [Fact]
    public void Foreign_tax_paid_but_no_credit_applied_warns()
    {
        var fsi = new[] { new ForeignSourceIncome { CountryCode = "1", CountryName = "USA", TaxIdentificationNo = "123", Head = ForeignIncomeHead.OtherSources, IncomeFromOutsideIndia = 500_000m, TaxPaidOutsideIndia = 75_000m, ReliefSection = ForeignTaxReliefSection.Section90 } };
        var report = Svc.Validate(Ctx(foreignSourceIncomes: fsi, relief90And91: 0m), StubJson);
        Has(report, "FSI.RELIEF_NOT_APPLIED").Should().BeTrue("foreign tax was paid but no s.90/91 relief was credited");
    }

    [Fact]
    public void Schedule5A_outside_a_portuguese_civil_code_state_warns()
    {
        var spouse = new SpouseIncomeApportionment { SpouseName = "Maria", SpousePan = "ABCPF1234M" };
        // Maharashtra (27) is not a Portuguese-CC jurisdiction -> warn.
        Has(Svc.Validate(Ctx(spouseApportionment: spouse, stateCode: "27"), StubJson), "SCHEDULE5A.JURISDICTION").Should().BeTrue();
        // Goa (10) is -> no warning.
        Has(Svc.Validate(Ctx(spouseApportionment: spouse, stateCode: "10"), StubJson), "SCHEDULE5A.JURISDICTION").Should().BeFalse();
    }

    [Fact]
    public void Vda_loss_is_flagged_as_ring_fenced_no_setoff_or_carry_forward()
    {
        // Bought ₹2L, sold ₹80k → a ₹1.2L VDA loss that s.115BBH(2) ring-fences.
        var gains = new[] { Vda(salePrice: 80_000m, cost: 200_000m) };
        Has(Svc.Validate(Ctx(gains: gains), StubJson), "VDA.LOSS_IGNORED").Should().BeTrue();
    }

    [Fact]
    public void Vda_with_improvement_or_transfer_expenses_warns_they_are_disallowed()
    {
        var gains = new[] { Vda(salePrice: 500_000m, cost: 200_000m, expenses: 5_000m) };
        Has(Svc.Validate(Ctx(gains: gains), StubJson), "VDA.DEDUCTION_DISALLOWED").Should().BeTrue();
    }

    [Fact]
    public void Vda_income_on_itr1_is_a_wrong_form_error()
    {
        var gains = new[] { Vda(salePrice: 500_000m, cost: 200_000m) };
        Has(Svc.Validate(Ctx(itrType: ItrType.ITR1, gains: gains), StubJson), "VDA.WRONG_FORM").Should().BeTrue();
    }

    [Fact]
    public void A_profitable_vda_on_itr2_raises_no_vda_issue()
    {
        var gains = new[] { Vda(salePrice: 500_000m, cost: 200_000m) };
        var report = Svc.Validate(Ctx(gains: gains), StubJson);
        Has(report, "VDA.LOSS_IGNORED").Should().BeFalse();
        Has(report, "VDA.WRONG_FORM").Should().BeFalse();
        Has(report, "VDA.DEDUCTION_DISALLOWED").Should().BeFalse();
    }

    // ----------------------------------------------------------------- builders
    private static ItrFilingContext Ctx(
        decimal refundOrPayable = 0m,
        Regime regime = Regime.New,
        IReadOnlyList<BankAccountDetail>? banks = null,
        IReadOnlyList<Deduction>? deductions = null,
        IReadOnlyList<BusinessIncome>? businesses = null,
        IReadOnlyList<ForeignSourceIncome>? foreignSourceIncomes = null,
        SpouseIncomeApportionment? spouseApportionment = null,
        decimal relief90And91 = 0m,
        string stateCode = "27",
        ItrType itrType = ItrType.ITR2,
        IReadOnlyList<CapitalGain>? gains = null)
        => new()
        {
            // AY2024-25 has no bundled schema → SchemaAvailable=false → no conformance noise.
            Return = new TaxReturn { ItrType = itrType, Regime = regime, RuleSetVersion = "AY2024-25" },
            User = new User { FullName = "Demo Taxpayer", Email = "demo@itrhelp.com", MobileE164 = "+919000000002", PanMasked = "ABCDE1234F" },
            Profile = new UserProfile { City = "Pune", StateCode = stateCode, Pincode = "411001", Dob = new DateOnly(1990, 1, 1) },
            Ay = new AssessmentYear { Code = "AY2024-25" },
            Computation = new TaxComputation { Regime = regime, GrossTotalIncome = 800_000m, TaxableIncome = 800_000m, RefundOrPayable = refundOrPayable, Relief90And91 = relief90And91 },
            Salaries = new[] { new SalaryDetail { Employer = "Acme", Gross = 900_000m } },
            BankAccounts = banks ?? Array.Empty<BankAccountDetail>(),
            Deductions = deductions ?? Array.Empty<Deduction>(),
            Businesses = businesses ?? Array.Empty<BusinessIncome>(),
            ForeignSourceIncomes = foreignSourceIncomes ?? Array.Empty<ForeignSourceIncome>(),
            SpouseApportionment = spouseApportionment,
            Gains = gains ?? Array.Empty<CapitalGain>(),
        };

    private static CapitalGain Vda(decimal salePrice, decimal cost, decimal expenses = 0m) => new()
    {
        AssetType = CapitalGainAssetType.CryptoVda,
        Term = CapitalGainTerm.Short,
        TaxSection = "115BBH",
        SalePrice = salePrice,
        CostOfAcquisition = cost,
        ExpensesOnTransfer = expenses,
    };

    private static BankAccountDetail Bank(bool useForRefund) => new()
    {
        BankName = "HDFC Bank", AccountNumber = "50100123456789", AccountType = "SB", Ifsc = "HDFC0001234", UseForRefund = useForRefund,
    };

    private static Deduction Deduct(string section, decimal amount) => new() { Section = section, Amount = amount };
}
