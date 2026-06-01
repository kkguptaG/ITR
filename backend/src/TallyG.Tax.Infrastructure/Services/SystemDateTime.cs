using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>Real system-clock implementation of <see cref="IDateTime"/>.</summary>
public sealed class SystemDateTime : IDateTime
{
    // IST is UTC+05:30 with no DST.
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly TodayIst => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(IstOffset).DateTime);
}
