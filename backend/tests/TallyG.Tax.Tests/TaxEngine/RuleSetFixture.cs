namespace TallyG.Tax.Tests.TaxEngine;

/// <summary>
/// The canonical AY2025-26 rule-set JSON used by the golden tests.
///
/// IMPORTANT: this is a verbatim copy of <c>TallyG.Tax.Infrastructure.Persistence.SeedRuleSet.Ay2025_26Json</c>.
/// The test project references ONLY the Domain project (so the tax-engine golden tests stay a pure,
/// dependency-free unit suite), therefore it cannot import the Infrastructure seed constant directly.
/// Keep these two in sync: if the seed JSON changes, update this fixture — the golden expectations
/// below are computed against exactly these figures. A drift will (correctly) fail the golden tests.
/// </summary>
internal static class RuleSetFixture
{
    public const string Version = "1.0.0";

    public const string Ay2025_26Json = /*lang=json,strict*/ """
    {
      "assessment_year": "AY2025-26",
      "rule_set_version": "1.0.0",
      "effective_from": "2024-04-01",
      "currency_rounding": { "income": 10, "tax": 1, "method": "round_half_up" },
      "cess": 0.04,
      "regimes": {
        "new": {
          "is_default": true,
          "std_deduction_salary": 75000,
          "family_pension_deduction": { "rate": 0.3333, "cap": 25000 },
          "slabs": [
            { "upto": 300000,  "rate": 0.00 },
            { "upto": 700000,  "rate": 0.05 },
            { "upto": 1000000, "rate": 0.10 },
            { "upto": 1200000, "rate": 0.15 },
            { "upto": 1500000, "rate": 0.20 },
            { "upto": null,    "rate": 0.30 }
          ],
          "rebate_87a": { "income_threshold": 700000, "max_rebate": 25000, "marginal_relief": true },
          "surcharge_bands": [
            { "above": 5000000,  "rate": 0.10 },
            { "above": 10000000, "rate": 0.15 },
            { "above": 20000000, "rate": 0.25 }
          ],
          "surcharge_cap_special_income": 0.15,
          "disallowed_chapter_via": ["80C","80D_self","80E","80G","80TTA","80TTB","80CCD1","hra_10_13a","lta","24b_self_occupied"],
          "allowed_chapter_via": ["80CCD2","80CCH","80JJAA"]
        },
        "old": {
          "is_default": false,
          "std_deduction_salary": 50000,
          "family_pension_deduction": { "rate": 0.3333, "cap": 15000 },
          "slabs": [
            { "upto": 250000,  "rate": 0.00 },
            { "upto": 500000,  "rate": 0.05 },
            { "upto": 1000000, "rate": 0.20 },
            { "upto": null,    "rate": 0.30 }
          ],
          "slabs_senior_60_to_80": [
            { "upto": 300000,  "rate": 0.00 },
            { "upto": 500000,  "rate": 0.05 },
            { "upto": 1000000, "rate": 0.20 },
            { "upto": null,    "rate": 0.30 }
          ],
          "slabs_super_senior_80_plus": [
            { "upto": 500000,  "rate": 0.00 },
            { "upto": 1000000, "rate": 0.20 },
            { "upto": null,    "rate": 0.30 }
          ],
          "rebate_87a": { "income_threshold": 500000, "max_rebate": 12500, "marginal_relief": false },
          "surcharge_bands": [
            { "above": 5000000,  "rate": 0.10 },
            { "above": 10000000, "rate": 0.15 },
            { "above": 20000000, "rate": 0.25 },
            { "above": 50000000, "rate": 0.37 }
          ],
          "surcharge_cap_special_income": 0.15
        }
      },
      "deduction_caps": {
        "80C": 150000,
        "80CCD_1B": 50000,
        "80CCD_2_salary_pct": 0.14,
        "80D_self_below_60": 25000,
        "80D_self_senior": 50000,
        "80D_parents_below_60": 25000,
        "80D_parents_senior": 50000,
        "80D_preventive_health_checkup": 5000,
        "80TTA": 10000,
        "80TTB": 50000,
        "house_property_loss_setoff_cap": 200000
      },
      "capital_gains": {
        "ltcg_112a_exemption": 125000,
        "ltcg_112a_rate": 0.125,
        "stcg_111a_rate": 0.15,
        "ltcg_112_rate_with_indexation": 0.20,
        "ltcg_112_rate_without_indexation": 0.125,
        "crypto_115bbh_rate": 0.30,
        "grandfather_date_112a": "2018-01-31",
        "property_indexation_cutoff": "2024-07-23",
        "holding_months": { "listed_equity": 12, "immovable_property": 24, "unlisted_shares": 24, "gold": 24 }
      },
      "presumptive": {
        "44AD": { "turnover_ceiling": 20000000, "turnover_ceiling_low_cash": 30000000, "rate_digital": 0.06, "rate_cash": 0.08 },
        "44ADA": { "receipts_ceiling": 5000000, "receipts_ceiling_low_cash": 7500000, "rate": 0.50 }
      },
      "hra": {
        "metro_cities": ["Delhi","Mumbai","Kolkata","Chennai"],
        "metro_pct": 0.50,
        "non_metro_pct": 0.40,
        "rent_minus_pct_of_salary": 0.10
      },
      "itr_selector": {
        "income_cap_itr1_itr4": 5000000,
        "allow_ltcg112a_in_itr1": true,
        "ltcg112a_threshold": 125000
      },
      "pipeline_order": [
        "gross_income_per_head","exemptions","standard_deduction","gross_total_income",
        "chapter_via_deductions","total_income_rounded","split_special_rate_income",
        "slab_tax","surcharge","cess","rebate_87a","less_prepaid_taxes","interest_234abc","net_refund_or_payable"
      ]
    }
    """;

    /// <summary>Build a salaried-only computation input at the given gross (single employer).</summary>
    public static Domain.TaxEngine.TaxComputationInput Salaried(
        decimal gross,
        decimal hraExemption = 0m,
        IReadOnlyList<Domain.TaxEngine.DeductionInput>? deductions = null,
        int age = 35,
        decimal tdsPaid = 0m)
        => new()
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = Version,
            RulesJson = Ay2025_26Json,
            Age = age,
            Salaries = new[]
            {
                new Domain.TaxEngine.SalaryInput(
                    Employer: "Acme Corp",
                    Gross: gross,
                    Perquisites: 0m,
                    ExemptAllowances: 0m,
                    HraExemption: hraExemption,
                    ProfessionalTax: 0m),
            },
            Deductions = deductions ?? Array.Empty<Domain.TaxEngine.DeductionInput>(),
            TdsPaid = tdsPaid,
        };
}
