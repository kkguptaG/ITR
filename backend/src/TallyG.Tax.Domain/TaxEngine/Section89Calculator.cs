namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Relief u/s 89(1) for salary received in arrears or in advance (the Form 10E computation).
///
/// Relief = (tax on the CURRENT year's income INCLUDING the arrears − tax EXCLUDING the arrears)
///          − Σ over each earlier year the arrears relate to of
///            (tax on that year's income INCLUDING its arrears slice − tax EXCLUDING it).
///
/// If the second term is ≥ the first, the arrears did not push the assessee into a higher bracket
/// across the spread, so there is no relief (the method returns 0; relief is never negative).
///
/// This is a PURE function over already-computed tax amounts. Each earlier year's tax must be
/// computed against THAT year's slabs (which differ year to year), so the caller supplies the
/// per-year with/without-arrears figures (from prior ITRs / Form 10E) and this method nets them —
/// the engine never silently applies a wrong year's slabs to historical income.
/// </summary>
public static class Section89Calculator
{
    /// <summary>Tax for one earlier year, computed with and without that year's slice of the arrears.</summary>
    public sealed record YearTax(decimal TaxWithArrears, decimal TaxWithoutArrears);

    public static decimal ComputeRelief(
        decimal currentYearTaxWithArrears,
        decimal currentYearTaxWithoutArrears,
        IReadOnlyList<YearTax> priorYears)
    {
        var additionalTaxThisYear = currentYearTaxWithArrears - currentYearTaxWithoutArrears;

        decimal additionalTaxPriorYears = 0m;
        foreach (var y in priorYears ?? Array.Empty<YearTax>())
        {
            additionalTaxPriorYears += y.TaxWithArrears - y.TaxWithoutArrears;
        }

        var relief = additionalTaxThisYear - additionalTaxPriorYears;
        return relief > 0m ? relief : 0m;
    }
}
