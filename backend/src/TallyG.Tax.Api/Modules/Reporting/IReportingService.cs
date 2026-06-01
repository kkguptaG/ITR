using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>
/// Server-side take-away document generation (docs 09). Renders the ITR-V acknowledgment, the
/// computation worksheet, and the fee tax-invoice as PDF bytes via <c>IPdfGenerator</c>, stores a
/// copy in the document vault via <c>IFileStorage</c> (registering a <c>Documents</c> row), and
/// streams the bytes back. User-scoped: only the owner (or a back-office operator) may download.
/// Contains zero tax logic — it is a pure projection of the persisted <c>TaxComputation</c> trace
/// and the <c>Invoice</c> rows. Auto-registered scoped (ReportingService : IReportingService).
/// </summary>
public interface IReportingService
{
    /// <summary>GET /returns/{id}/acknowledgment — the ITR-V acknowledgment PDF for a filed return.</summary>
    Task<GeneratedFile> GetAcknowledgmentAsync(Guid returnId, CancellationToken ct = default);

    /// <summary>GET /returns/{id}/computation — the computation worksheet PDF for a computed return.</summary>
    Task<GeneratedFile> GetComputationAsync(Guid returnId, CancellationToken ct = default);

    /// <summary>GET /payments/{id}/invoice:pdf — the GST tax-invoice PDF for a captured payment.</summary>
    Task<GeneratedFile> GetInvoiceAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>GET /returns/{id}/documents — the generated artifacts registered against a return.</summary>
    Task<IReadOnlyList<GeneratedDocumentDto>> ListReturnDocumentsAsync(Guid returnId, CancellationToken ct = default);
}

/// <summary>A generated PDF ready to stream: bytes + content type + suggested download filename.</summary>
public sealed record GeneratedFile(byte[] Content, string ContentType, string FileName);
