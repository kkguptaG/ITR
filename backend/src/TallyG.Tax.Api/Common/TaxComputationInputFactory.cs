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
        IReadOnlyList<Deduction> deductions)
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
                FairMarketValueOnGrandfatherDate: null,
                IndexedCost: c.IndexedCost > 0m ? c.IndexedCost : null)).ToList(),
            BusinessIncomes = businesses.Select(b => new BusinessIncomeInput(
                b.IsPresumptive, b.PresumptiveSection, b.Turnover, b.GrossReceiptsDigital, b.GrossReceiptsCash,
                b.NetProfit, b.SpeculativeFlag)).ToList(),
            OtherIncomes = incomeSources
                .Where(s => s.Type == IncomeType.OtherSources)
                .Select(s => new OtherIncomeInput(s.Label ?? "Other", s.Amount, ExtractNature(s.SourceMetaJson))).ToList(),
            Deductions = deductions.Select(d => new DeductionInput(d.Section, d.Amount, d.SubType)).ToList(),
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
