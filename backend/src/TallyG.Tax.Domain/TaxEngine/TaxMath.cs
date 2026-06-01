namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Pure rounding helpers implementing the statutory rounding of s.288A (total income to the
/// nearest ₹10) and s.288B (tax to the nearest ₹1), driven by the rule-set <see cref="RoundingPolicy"/>.
/// Uses <see cref="decimal"/> throughout (base-10, exact to the paisa) — never binary float.
/// </summary>
public static class TaxMath
{
    /// <summary>Round <paramref name="value"/> to the nearest <paramref name="step"/> (half-up).</summary>
    public static decimal RoundToStep(decimal value, decimal step)
    {
        if (step <= 0m)
        {
            return value;
        }

        // Half-up at the step boundary: floor(value/step + 0.5) * step.
        var units = decimal.Floor(value / step + 0.5m);
        return units * step;
    }

    /// <summary>Round total/taxable income per the policy (typically nearest ₹10).</summary>
    public static decimal RoundIncome(decimal value, RoundingPolicy policy)
        => RoundToStep(value, policy.IncomeStep);

    /// <summary>Round a tax figure per the policy (typically nearest ₹1).</summary>
    public static decimal RoundTax(decimal value, RoundingPolicy policy)
        => RoundToStep(value, policy.TaxStep);

    /// <summary>Clamp a value to be non-negative (losses below a head do not create negative tax).</summary>
    public static decimal NonNegative(decimal value) => value < 0m ? 0m : value;
}
