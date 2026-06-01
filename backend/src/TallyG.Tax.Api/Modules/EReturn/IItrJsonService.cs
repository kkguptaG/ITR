using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Orchestrates the offline-filing flow: generate the ITR JSON for a return, validate it, persist a
/// single latest artifact per return ("save to list"), and stream it for download. Owner-scoped.
/// </summary>
public interface IItrJsonService
{
    Task<GenerateItrJsonResponse> GenerateAsync(Guid returnId, CancellationToken ct = default);
    Task<ValidationReportDto> ValidateAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>Return the LAST stored validation report for an artifact (no re-run) so the UI can
    /// show the issues + suggestions immediately on load.</summary>
    Task<ValidationReportDto> GetReportAsync(Guid fileId, CancellationToken ct = default);
    Task<IReadOnlyList<ItrJsonArtifactDto>> ListForReturnAsync(Guid returnId, CancellationToken ct = default);
    Task<PagedResult<ItrJsonArtifactDto>> ListMineAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ItrJsonArtifactDto> GetAsync(Guid fileId, CancellationToken ct = default);
    Task<ItrJsonDownload> DownloadAsync(Guid fileId, CancellationToken ct = default);
}
