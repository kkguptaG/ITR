namespace TallyG.Tax.Domain.Enums;

/// <summary>Account state for a <see cref="Entities.User"/>.</summary>
public enum UserStatus
{
    Active = 0,
    Locked = 1,
    Disabled = 2,
    Deleted = 3
}
