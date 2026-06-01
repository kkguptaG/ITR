namespace TallyG.Tax.Domain.Enums;

/// <summary>Lifecycle of a <see cref="Entities.TaxRuleSet"/> (Ch.3 §3.11).</summary>
public enum RuleSetStatus
{
    Draft = 0,
    Review = 1,
    Active = 2,
    Superseded = 3
}
