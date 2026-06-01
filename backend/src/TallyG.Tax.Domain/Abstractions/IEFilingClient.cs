namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Anti-corruption boundary over the ITD ERI e-filing endpoint.
/// The dev implementation returns a deterministic mock acknowledgment number and a
/// small ITR-V PDF so the saga completes without ERI certification.
/// </summary>
public interface IEFilingClient
{
    /// <summary>Submit a canonical ITR JSON payload for the given return + AY.</summary>
    Task<EFilingResult> SubmitAsync(
        Guid taxReturnId,
        string assessmentYearCode,
        string itrJson,
        CancellationToken ct = default);

    /// <summary>Poll the processing status of a previously submitted return.</summary>
    Task<EFilingResult> GetStatusAsync(string acknowledgmentNumber, CancellationToken ct = default);
}

/// <summary>Outcome of an e-file submission.</summary>
public sealed record EFilingResult(
    bool Accepted,
    string AcknowledgmentNumber,
    byte[]? ItrVPdf,
    string? FailureReason,
    DateTimeOffset SubmittedAt);
