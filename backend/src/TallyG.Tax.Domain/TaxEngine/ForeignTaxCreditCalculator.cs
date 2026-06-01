namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Relief on doubly-taxed income: foreign tax credit u/s 90/90A (where a DTAA exists) and u/s 91
/// (unilateral, where there is no DTAA).
///
/// Relief = doubly-taxed income × LOWER of (average Indian rate, foreign rate), capped at the foreign
/// tax actually paid. Average Indian rate = Indian tax (before this relief) ÷ total taxable income;
/// foreign rate = foreign tax paid ÷ foreign income. For s.91 the "lower of the two rates" rule is the
/// statute; for s.90 the treaty governs, but the lower-of-rates credit is the standard FTC method
/// (Rule 128) and is what we apply here.
///
/// PENDING CA REVIEW: per-country sourcing, Form 67 filing, and treaty-article specifics are not modelled.
/// </summary>
public static class ForeignTaxCreditCalculator
{
    public sealed record FtcResult(decimal Relief, string Section, decimal AverageIndianRate, decimal ForeignRate);

    public static FtcResult Compute(
        decimal doublyTaxedForeignIncome,
        decimal foreignTaxPaid,
        decimal indianTaxBeforeRelief,
        decimal totalTaxableIncome,
        bool dtaaApplies,
        List<TraceLine> trace)
    {
        var section = dtaaApplies ? "90/90A" : "91";

        if (doublyTaxedForeignIncome <= 0m || foreignTaxPaid <= 0m || indianTaxBeforeRelief <= 0m || totalTaxableIncome <= 0m)
        {
            return new FtcResult(0m, section, 0m, 0m);
        }

        var averageIndianRate = indianTaxBeforeRelief / totalTaxableIncome;
        var foreignRate = foreignTaxPaid / doublyTaxedForeignIncome;
        var rate = Math.Min(averageIndianRate, foreignRate);
        var relief = Math.Min(doublyTaxedForeignIncome * rate, foreignTaxPaid);

        trace.Add(new TraceLine($"Relief.{(dtaaApplies ? "90" : "91")}",
            $"Relief u/s {section}: ₹{doublyTaxedForeignIncome:N0} × {rate:P2} (lower of {averageIndianRate:P2} Indian / {foreignRate:P2} foreign), capped at foreign tax ₹{foreignTaxPaid:N0}",
            relief, $"s.{section}"));

        return new FtcResult(relief, section, averageIndianRate, foreignRate);
    }
}
