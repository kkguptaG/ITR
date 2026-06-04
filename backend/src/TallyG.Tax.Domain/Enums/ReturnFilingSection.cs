namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The section of the Income-tax Act under which the return is furnished, mapped to the ITD
/// <c>ReturnFileSec</c> code emitted in FilingStatus. A revised return (139(5)) additionally carries
/// the original return's acknowledgment number and filing date.
/// </summary>
public enum ReturnFilingSection
{
    /// <summary>s.139(1) — original return on or before the due date. ITD code 11.</summary>
    Original = 11,

    /// <summary>s.139(4) — belated return after the due date. ITD code 12.</summary>
    Belated = 12,

    /// <summary>
    /// s.139(5) — revised return (corrects an earlier filing). ITD code 17.
    /// (Code 13 is 142(1) — a notice-response return — NOT revised.)
    /// </summary>
    Revised = 17,

    /// <summary>
    /// s.139(8A) — updated return (ITR-U): correct/disclose income up to 48 months after the AY,
    /// paying additional tax u/s 140B (25/50/60/70% by time tier). ITD code 21. ITR-2/3 only.
    /// </summary>
    Updated = 21,
}
