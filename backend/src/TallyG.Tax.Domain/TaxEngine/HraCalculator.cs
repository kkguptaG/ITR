namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// HRA exemption calculator under s.10(13A) (Ch.3 §3.7) — old regime only (disallowed in new).
///
/// Exemption is the <b>least of three</b>:
///   1. actual HRA received,
///   2. rent paid − 10% of salary,
///   3. 50% of salary (metro) or 40% (non-metro).
/// where salary = Basic + DA(forming part) + commission(% of turnover).
///
/// Computed <b>period-wise</b> and summed, because mid-year salary hikes / city changes
/// (common for our MSME/consultant users) make annual averaging wrong. Pure &amp; deterministic.
/// All thresholds (metro list, percentages) come from <see cref="HraRules"/> in the rule-set.
/// </summary>
public static class HraCalculator
{
    /// <summary>Compute the exempt and taxable HRA for one or more periods, summed.</summary>
    public static HraResult Compute(IReadOnlyList<HraPeriodInput> periods, HraRules rules)
    {
        if (periods.Count == 0)
        {
            return new HraResult(0m, 0m, Array.Empty<HraPeriodBreakdown>());
        }

        decimal totalReceived = 0m;
        decimal totalExempt = 0m;
        var breakdown = new List<HraPeriodBreakdown>(periods.Count);

        foreach (var p in periods)
        {
            var period = ComputePeriod(p, rules);
            totalReceived += period.HraReceived;
            totalExempt += period.Exempt;
            breakdown.Add(period);
        }

        var taxable = TaxMath.NonNegative(totalReceived - totalExempt);
        return new HraResult(totalExempt, taxable, breakdown);
    }

    /// <summary>Convenience overload for a single full-year computation.</summary>
    public static HraResult Compute(HraPeriodInput period, HraRules rules)
        => Compute(new[] { period }, rules);

    private static HraPeriodBreakdown ComputePeriod(HraPeriodInput p, HraRules rules)
    {
        // "salary" for HRA = Basic + DA(forming part) + commission. Caller supplies the composed figure.
        var salary = TaxMath.NonNegative(p.SalaryForHra);

        var candidateActual = TaxMath.NonNegative(p.HraReceived);
        var candidateRent = TaxMath.NonNegative(p.RentPaid - rules.RentMinusPctOfSalary * salary);
        var metroPct = rules.IsMetro(p.City) ? rules.MetroPct : rules.NonMetroPct;
        var candidatePct = metroPct * salary;

        var exempt = Math.Min(candidateActual, Math.Min(candidateRent, candidatePct));
        exempt = TaxMath.NonNegative(exempt);

        return new HraPeriodBreakdown(
            HraReceived: candidateActual,
            CandidateActual: candidateActual,
            CandidateRentMinus10Pct: candidateRent,
            CandidatePctOfSalary: candidatePct,
            IsMetro: rules.IsMetro(p.City),
            Exempt: exempt);
    }
}

/// <summary>One period (typically a month, or a full year) of HRA inputs.</summary>
public sealed record HraPeriodInput(
    decimal SalaryForHra,
    decimal HraReceived,
    decimal RentPaid,
    string? City);

/// <summary>Aggregate HRA exemption result with per-period explainability.</summary>
public sealed record HraResult(
    decimal TotalExempt,
    decimal TotalTaxable,
    IReadOnlyList<HraPeriodBreakdown> Periods);

/// <summary>The three least-of-three candidates for a single period, plus the chosen exemption.</summary>
public sealed record HraPeriodBreakdown(
    decimal HraReceived,
    decimal CandidateActual,
    decimal CandidateRentMinus10Pct,
    decimal CandidatePctOfSalary,
    bool IsMetro,
    decimal Exempt);
