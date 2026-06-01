// Reporting module — generated-document DTOs.
// Public contract for the take-away PDFs (docs 09: ITR-V acknowledgment, computation worksheet,
// fee tax-invoice). These endpoints STREAM the PDF bytes; the metadata records below back the
// "list generated documents" view. camelCase on the wire.

using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>One generated artifact registered against a return (GET /returns/{id}/documents).</summary>
public sealed record GeneratedDocumentDto(
    Guid DocumentId,
    string DocType,        // itr_v_ack | computation_sheet | tax_invoice
    DocumentKind Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Sha256,
    DocumentStatus Status,
    DateTimeOffset CreatedAt);
