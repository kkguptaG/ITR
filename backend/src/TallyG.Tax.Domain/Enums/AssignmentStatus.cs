namespace TallyG.Tax.Domain.Enums;

/// <summary>CA assignment lifecycle (Ch.2 ca_assignments).</summary>
public enum AssignmentStatus
{
    Unassigned = 0,
    Assigned = 1,
    InReview = 2,
    Completed = 3
}
