namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Computes interest under s.234A (default in furnishing the return — late filing), s.234B (default
/// in payment of advance tax) and s.234C (deferment of advance tax). Pure + deterministic: all
/// dates/amounts come from <see cref="TaxComputationInput"/>; the monthly rate and the s.208
/// advance-tax threshold come from the <see cref="RuleSet"/>. Every figure is added to the trace.
///
/// HONESTY: 234C is exact only when quarterly advance-tax payment dates are supplied
/// (<see cref="TaxComputationInput.AdvanceTaxInstallments"/>). If advance tax was reported WITHOUT
/// dates, 234C is left uncomputed with a note (we don't guess the timing). If NO advance tax was
/// paid at all, 234C is computed exactly (paid-by-date is unambiguously zero — the common
/// salaried-with-balance-due case). The monthly rate is "1% per month or part of a month".
/// </summary>
public static class InterestCalculator
{
    // Cumulative advance-tax due by 15-Jun / 15-Sep / 15-Dec / 15-Mar, with the statutory
    // safe-harbour relaxation (12% / 36%) for the first two installments, and the period (in
    // months) interest runs for each installment.
    private static readonly decimal[] Required = { 0.15m, 0.45m, 0.75m, 1.00m };
    private static readonly decimal[] SafeHarbour = { 0.12m, 0.36m, 0.75m, 1.00m };
    private static readonly int[] InstallmentMonths = { 3, 3, 3, 1 };

    /// <summary>Returns the per-section 234A/B/C interest breakdown (and total) and appends a trace
    /// line per applicable section.</summary>
    public static InterestBreakdown Compute(TaxComputationInput input, decimal totalTax, RuleSet rs, List<TraceLine> trace)
    {
        var rate = rs.InterestMonthlyRate <= 0m ? 0.01m : rs.InterestMonthlyRate;
        var tdsTcs = input.TdsPaid + input.TcsPaid;
        var advance = input.AdvanceTaxPaid;

        var i234A = Compute234A(input, totalTax, tdsTcs, advance, rate, trace);

        // "Assessed tax" for 234B/234C = tax on total income less TDS/TCS (advance tax is the credit
        // compared against, not part of the base).
        var assessedTax = TaxMath.NonNegative(totalTax - tdsTcs);

        decimal i234B = 0m, i234C = 0m;
        if (assessedTax >= rs.AdvanceTaxThreshold) // s.208: advance tax only if liability ≥ threshold
        {
            i234B = Compute234B(input, assessedTax, advance, rate, trace);
            i234C = Compute234C(input, assessedTax, advance, rate, trace);
        }

        return new InterestBreakdown(i234A, i234B, i234C, i234A + i234B + i234C);
    }

    private static decimal Compute234A(
        TaxComputationInput input, decimal totalTax, decimal tdsTcs, decimal advance,
        decimal rate, List<TraceLine> trace)
    {
        if (input.FilingDueDate is not { } due || input.ActualFilingDate is not { } filed || filed <= due)
        {
            return 0m; // filed on/before the due date (or no dates) → no 234A
        }

        // s.234A base excludes self-assessment tax — it is paid at/after the due date, which is
        // precisely the default 234A penalises; only advance tax + TDS/TCS reduce the base.
        var unpaid = TaxMath.NonNegative(totalTax - tdsTcs - advance);
        if (unpaid <= 0m)
        {
            return 0m;
        }

        var months = MonthsCeil(due, filed);
        var interest = RoundInterest(unpaid * rate * months);
        if (interest > 0m)
        {
            trace.Add(new TraceLine("Interest.234A",
                $"s.234A late-filing interest: {months} mo @ {rate:P0} on unpaid ₹{unpaid:N0} (due {due:dd-MMM-yyyy} → filed {filed:dd-MMM-yyyy})",
                interest, "s.234A"));
        }

        return interest;
    }

    private static decimal Compute234B(
        TaxComputationInput input, decimal assessedTax, decimal advance, decimal rate, List<TraceLine> trace)
    {
        // 234B applies only if advance tax paid is less than 90% of assessed tax.
        if (advance >= assessedTax * 0.90m)
        {
            return 0m;
        }

        if (input.PreviousYearEnd is not { } pyEnd || input.ActualFilingDate is not { } filed)
        {
            return 0m;
        }

        var ayStart = pyEnd.AddDays(1); // 1 Apr of the assessment year
        if (filed <= ayStart)
        {
            return 0m;
        }

        var shortfall = TaxMath.NonNegative(assessedTax - advance);
        var months = MonthsCeil(ayStart, filed);
        var interest = RoundInterest(shortfall * rate * months);
        if (interest > 0m)
        {
            trace.Add(new TraceLine("Interest.234B",
                $"s.234B advance-tax default: {months} mo @ {rate:P0} on shortfall ₹{shortfall:N0} (advance ₹{advance:N0} < 90% of assessed ₹{assessedTax:N0})",
                interest, "s.234B"));
        }

        return interest;
    }

    private static decimal Compute234C(
        TaxComputationInput input, decimal assessedTax, decimal advance, decimal rate, List<TraceLine> trace)
    {
        if (input.PreviousYearStart is not { } pyStart)
        {
            return 0m;
        }

        var installments = input.AdvanceTaxInstallments;
        var haveDates = installments.Count > 0;

        // Advance tax reported but no dates → we can't place it in time; don't guess.
        if (!haveDates && advance > 0m)
        {
            trace.Add(new TraceLine("Interest.234C",
                $"s.234C (deferment) not computed: enter quarterly advance-tax payment dates (₹{advance:N0} reported) for an exact figure.",
                0m, "s.234C"));
            return 0m;
        }

        var dueDates = input.PresumptiveAdvanceTax
            ? new[] { new DateOnly(pyStart.Year + 1, 3, 15) }
            : new[]
            {
                new DateOnly(pyStart.Year, 6, 15),
                new DateOnly(pyStart.Year, 9, 15),
                new DateOnly(pyStart.Year, 12, 15),
                new DateOnly(pyStart.Year + 1, 3, 15),
            };
        var required = input.PresumptiveAdvanceTax ? new[] { 1.00m } : Required;
        var safeHarbour = input.PresumptiveAdvanceTax ? new[] { 1.00m } : SafeHarbour;
        var months = input.PresumptiveAdvanceTax ? new[] { 1 } : InstallmentMonths;

        decimal total = 0m;
        for (var i = 0; i < dueDates.Length; i++)
        {
            var paidByDate = haveDates
                ? installments.Where(p => p.PaidOn <= dueDates[i]).Sum(p => p.Amount)
                : 0m; // no advance tax paid on time
            if (paidByDate >= safeHarbour[i] * assessedTax)
            {
                continue; // installment met within the statutory relaxation
            }

            var shortfall = TaxMath.NonNegative(required[i] * assessedTax - paidByDate);
            total += shortfall * rate * months[i];
        }

        var interest = RoundInterest(total);
        if (interest > 0m)
        {
            var note = haveDates ? string.Empty : " (assumes no advance tax paid by the quarterly due dates)";
            trace.Add(new TraceLine("Interest.234C", $"s.234C advance-tax deferment interest{note}", interest, "s.234C"));
        }

        return interest;
    }

    /// <summary>Months from <paramref name="start"/> to <paramref name="end"/>, counting any part of a
    /// month as a full month (the s.234 convention).</summary>
    private static int MonthsCeil(DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            return 0;
        }

        var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
        if (end.Day > start.Day)
        {
            months++;
        }

        return Math.Max(0, months);
    }

    private static decimal RoundInterest(decimal value)
        => Math.Round(TaxMath.NonNegative(value), 0, MidpointRounding.AwayFromZero);
}

/// <summary>Per-section interest breakdown u/s 234A/B/C plus the total (s.234F late-fee is computed
/// elsewhere and is 0 here). All amounts are whole rupees.</summary>
public readonly record struct InterestBreakdown(decimal S234A, decimal S234B, decimal S234C, decimal Total);
