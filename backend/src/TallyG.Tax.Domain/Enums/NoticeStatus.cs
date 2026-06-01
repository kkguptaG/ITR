namespace TallyG.Tax.Domain.Enums;

/// <summary>State of an ITD notice (Ch.2 notices).</summary>
public enum NoticeStatus
{
    Open = 0,
    InProgress = 1,
    Responded = 2,
    Closed = 3,
    Escalated = 4
}
