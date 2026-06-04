namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// How the assessee acquired the capital asset (Ch.3 §3.6). For Gift / Inheritance / Will the cost is the
/// cost to the PREVIOUS owner (s.49(1)) and the holding period INCLUDES the previous owner's holding
/// (s.2(42A) Explanation 1(i)(b)) — both drive the dynamic term + indexation derivation.
/// </summary>
public enum CapitalGainAcquisitionMode
{
    Purchase = 0,
    Gift = 1,
    Inheritance = 2,
    Will = 3,
    Other = 99
}
