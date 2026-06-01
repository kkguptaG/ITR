using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Rolls an itemised salary breakup (the Schedule S component grid) up into the flat
/// <see cref="SalaryDetail"/> fields the tax engine and the ITR-JSON mapper consume.
/// Pure + deterministic → unit-tested; introduces no new computation path.
///
/// Schedule S mapping:
///   • entity.Gross           = Σ 17(1) salary + Σ gross s.10 allowances (incl. HRA received)
///   • entity.Perquisites     = Σ 17(2) perquisites
///   • entity.ProfitsInLieu   = Σ 17(3) profits in lieu  (taxed via the engine-input fold)
///   • entity.Hra             = Σ HRA received
///   • entity.HraExemption    = Σ HRA exempt part   (engine gates this to the OLD regime, s.10(13A))
///   • entity.ExemptAllowances= Σ exempt part of NON-HRA allowances
/// StdDeduction is applied by the engine per regime; ProfessionalTax stays user-entered.
/// </summary>
public static class SalaryRollup
{
    public static void Apply(SalaryDetail parent, IEnumerable<SalaryComponent> components)
    {
        decimal salary171 = 0m, perquisites172 = 0m, profitsInLieu173 = 0m;
        decimal allowanceGross = 0m, allowanceExempt = 0m;
        decimal hraReceived = 0m, hraExempt = 0m;

        foreach (var c in components)
        {
            var total = Max0(c.Total);
            var exempt = Clamp(c.Exempt, 0m, total); // exemption can never exceed the gross amount

            switch (c.Category)
            {
                case SalaryComponentCategory.Perquisite:
                    perquisites172 += total;
                    break;

                case SalaryComponentCategory.ProfitInLieu:
                    profitsInLieu173 += total;
                    break;

                case SalaryComponentCategory.Allowance:
                    allowanceGross += total;
                    if (c.IsHra)
                    {
                        hraReceived += total;
                        hraExempt += exempt;
                    }
                    else
                    {
                        allowanceExempt += exempt;
                    }
                    break;

                case SalaryComponentCategory.Salary:
                default:
                    salary171 += total;
                    break;
            }
        }

        parent.Gross = salary171 + allowanceGross;
        parent.Perquisites = perquisites172;
        parent.ProfitsInLieu = profitsInLieu173;
        parent.Hra = hraReceived;
        parent.HraExemption = hraExempt;
        parent.ExemptAllowances = allowanceExempt;
    }

    private static decimal Max0(decimal v) => v < 0m ? 0m : v;

    private static decimal Clamp(decimal v, decimal lo, decimal hi) => v < lo ? lo : (v > hi ? hi : v);
}
