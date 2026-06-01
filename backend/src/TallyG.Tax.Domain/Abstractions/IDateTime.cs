namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Abstraction over the system clock so time-dependent logic stays testable and the
/// tax engine never reads the wall clock directly (purity requirement, Ch.3).
/// </summary>
public interface IDateTime
{
    /// <summary>Current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current date in Asia/Kolkata (IST) — used for filing-deadline checks.</summary>
    DateOnly TodayIst { get; }
}
