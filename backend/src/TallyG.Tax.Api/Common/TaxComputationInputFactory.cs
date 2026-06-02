using System.Text.Json;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;

namespace TallyG.Tax.Api.Common;

/// <summary>
/// Single source of truth for mapping a saved <see cref="TaxReturn"/> + its loaded heads to the
/// engine's <see cref="TaxComputationInput"/>. Used by BOTH the /tax/compute path (TaxService) and
/// the filing-snapshot path (ReturnService) so the two never diverge — prepaid taxes, brought-forward
/// losses (incl. capital), s.234 interest dates and Other-Sources "nature" all stay consistent.
/// Adding a new field to the engine input now means editing ONE place.
/// </summary>
internal static class TaxComputationInputFactory
{
    public static TaxComputationInput FromReturn(
        TaxReturn ret,
        string ayCode,
        string rulesJson,
        int age,
        DateOnly asOf,
        IReadOnlyList<SalaryDetail> salaries,
        IReadOnlyList<HouseProperty> houses,
        IReadOnlyList<CapitalGain> gains,
        IReadOnlyList<BusinessIncome> businesses,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Deduction> deductions,
        IReadOnlyList<Donation80G>? donations80G = null,
        IReadOnlyList<ExemptIncome>? exemptIncomes = null)
    {
        var ay = ret.AssessmentYear;
        return new TaxComputationInput
        {
            AssessmentYearCode = ayCode,
            RuleSetVersion = ret.RuleSetVersion,
            RulesJson = rulesJson,
            Age = age,
            Salaries = salaries.Select(s => new SalaryInput(
                // ProfitsInLieu (s.17(3)) folds into the taxable salary base (Gross).
                s.Employer, s.Gross + s.ProfitsInLieu, s.Perquisites, s.ExemptAllowances, s.HraExemption, s.ProfessionalTax)).ToList(),
            HouseProperties = houses.Select(h => new HousePropertyInput(
                h.Type, h.AnnualValue, h.MunicipalTaxPaid, h.InterestOnLoan)).ToList(),
            CapitalGains = gains.Select(c => new CapitalGainInput(
                c.AssetType, c.Term, c.TaxSection, c.SalePrice, c.CostOfAcquisition, c.CostOfImprovement,
                c.ExpensesOnTransfer, c.ExemptionAmount, c.AcquisitionDate, c.TransferDate,
                FairMarketValueOnGrandfatherDate: c.FairMarketValue31Jan2018 > 0m ? c.FairMarketValue31Jan2018 : null,
                IndexedCost: c.IndexedCost > 0m ? c.IndexedCost : null,
                ExemptionSection: c.ExemptionSection,
                ReinvestmentAmount: c.ReinvestmentAmount)).ToList(),
            BusinessIncomes = businesses.Select(b => new BusinessIncomeInput(
                b.IsPresumptive, b.PresumptiveSection, b.Turnover, b.GrossReceiptsDigital, b.GrossReceiptsCash,
                b.NetProfit, b.SpeculativeFlag)).ToList(),
            // Other-sources income (carrying its {"nature"} tag) PLUS any net agricultural income captured in
            // Schedule EI — fed as nature "agricultural" so the engine's partial-integration raises the rate
            // (s.2(2)/Finance Act) without taxing the exempt agri income itself.
            OtherIncomes = incomeSources
                .Where(s => s.Type == IncomeType.OtherSources)
                .Select(s => new OtherIncomeInput(s.Label ?? "Other", s.Amount, ExtractNature(s.SourceMetaJson)))
                .Concat((exemptIncomes ?? Array.Empty<ExemptIncome>())
                    .Where(e => e.Category == ExemptIncomeCategory.Agricultural && e.Amount > 0m)
                    .Select(e => new OtherIncomeInput(
                        string.IsNullOrWhiteSpace(e.Description) ? "Agricultural income" : e.Description, e.Amount, "agricultural")))
                .ToList(),
            Deductions = BuildDeductionInputs(deductions, donations80G ?? Array.Empty<Donation80G>()),
            // Prepaid taxes + brought-forward losses captured on the return.
            TdsPaid = ret.TdsPaid,
            TcsPaid = ret.TcsPaid,
            AdvanceTaxPaid = ret.AdvanceTaxPaid,
            SelfAssessmentTaxPaid = ret.SelfAssessmentTaxPaid,
            BroughtForwardHousePropertyLoss = ret.BroughtForwardHousePropertyLoss,
            BroughtForwardBusinessLoss = ret.BroughtForwardBusinessLoss,
            BroughtForwardShortTermCapitalLoss = ret.BroughtForwardShortTermCapitalLoss,
            BroughtForwardLongTermCapitalLoss = ret.BroughtForwardLongTermCapitalLoss,
            // AMT credit (s.115JD) + reliefs (s.89/90/91).
            BroughtForwardAmtCredit = ret.BroughtForwardAmtCredit,
            Relief89 = ret.Relief89,
            ForeignIncomeDoublyTaxed = ret.ForeignIncomeDoublyTaxed,
            ForeignTaxPaid = ret.ForeignTaxPaid,
            ForeignDtaaApplies = ret.ForeignDtaaApplies,
            // s.234A/B/C interest context: dates from the AY; "as of" = submitted date or today (draft).
            FilingDueDate = ay?.DueDateNonAudit,
            ActualFilingDate = ret.SubmittedAt is { } sub ? DateOnly.FromDateTime(sub.UtcDateTime) : asOf,
            PreviousYearStart = ay?.StartDate,
            PreviousYearEnd = ay?.EndDate,
            PresumptiveAdvanceTax = businesses.Any(b => b.IsPresumptive),
        };
    }

    /// <summary>
    /// Maps the return's deductions to engine inputs. When 80G donations were captured donee-wise (each
    /// with an explicit 100%/50% + with/without-qualifying-limit category), those drive the engine's 80G
    /// categorisation and 10%-of-adjusted-GTI cap — replacing the category-less generic 80G deduction line,
    /// which would otherwise fall to the engine's conservative 50%-with-limit default and under-deduct.
    /// </summary>
    private static List<DeductionInput> BuildDeductionInputs(
        IReadOnlyList<Deduction> deductions, IReadOnlyList<Donation80G> donations80G)
    {
        if (donations80G.Count == 0)
        {
            return deductions.Select(d => new DeductionInput(d.Section, d.Amount, d.SubType)).ToList();
        }

        var list = deductions
            .Where(d => !IsSection80G(d.Section))
            .Select(d => new DeductionInput(d.Section, d.Amount, d.SubType))
            .ToList();

        foreach (var g in donations80G)
        {
            // A cash donation over ₹2,000 is disallowed; the engine then applies the 100%/50% factor + the
            // qualifying-limit cap to the amount we pass.
            var eligibleBase = g.OtherModeAmount + (g.CashAmount <= 2_000m ? g.CashAmount : 0m);
            if (eligibleBase > 0m)
            {
                list.Add(new DeductionInput("80G", eligibleBase, Donation80GSubType(g.Category)));
            }
        }

        return list;
    }

    private static bool IsSection80G(string? section)
        => new string((section ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant() == "80G";

    // The SubType strings the engine's 80G categoriser recognises ("100" ⇒ 100%, "no_limit" ⇒ no qualifying limit).
    private static string Donation80GSubType(Donation80GCategory category) => category switch
    {
        Donation80GCategory.HundredPercentNoLimit => "100_no_limit",
        Donation80GCategory.FiftyPercentNoLimit => "50_no_limit",
        Donation80GCategory.HundredPercentWithLimit => "100_limit",
        _ => "50_limit",
    };

    /// <summary>Reads the optional {"nature":"..."} tag from an income source's SourceMetaJson.</summary>
    public static string? ExtractNature(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("nature", out var n)
                   && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
