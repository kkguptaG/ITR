namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// Which line of Schedule EI (Exempt Income) an item is reported on. The buckets map to the
/// schedule's three usable rows: exempt interest, net agricultural income, and "others".
/// </summary>
public enum ExemptIncomeCategory
{
    /// <summary>Exempt interest (e.g. PPF, tax-free bonds, post-office savings within limit) → InterestInc.</summary>
    Interest = 0,

    /// <summary>Net agricultural income (exempt u/s 10(1), used only for the rate) → NetAgriIncOrOthrIncRule7.</summary>
    Agricultural = 1,

    /// <summary>Any other exempt income (e.g. share of firm profit u/s 10(2A), maturity u/s 10(10D)) → Others.</summary>
    Other = 2
}
