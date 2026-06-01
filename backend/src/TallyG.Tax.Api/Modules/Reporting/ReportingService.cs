using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Admin.Audit;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Reporting;

/// <summary>
/// Take-away document generation (docs 09). Renders the ITR-V acknowledgment, computation worksheet
/// and fee tax-invoice via <see cref="IPdfGenerator"/>, persists a copy through
/// <see cref="IFileStorage"/> (registering/refreshing a <see cref="Document"/> vault row under a
/// deterministic key so re-downloads do not pile up files), and returns the bytes to stream.
///
/// Access is owner-scoped: a taxpayer downloads their own artifacts; Ops/Admin/SuperAdmin may
/// download within their tenant (SuperAdmin across all). Every download is an auditable PII access
/// event (docs 09 §9.10) recorded via <see cref="IAuditWriterService"/>. Contains no tax logic.
/// No manual DI — Scrutor binds ReportingService : IReportingService scoped.
/// </summary>
public sealed class ReportingService : IReportingService
{
    private const string PdfContentType = "application/pdf";

    // Back-office roles that may pull a return owner's documents (docs 09 §9.7 RBAC).
    private static readonly string[] OperatorRoles = { "Ops", "Admin", "SuperAdmin" };

    private readonly AppDbContext _db;
    private readonly IPdfGenerator _pdf;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriterService _audit;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(
        AppDbContext db,
        IPdfGenerator pdf,
        IFileStorage storage,
        ICurrentUser currentUser,
        IAuditWriterService audit,
        ILogger<ReportingService> logger)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    // ------------------------------------------------------------ acknowledgment

    public async Task<GeneratedFile> GetAcknowledgmentAsync(Guid returnId, CancellationToken ct = default)
    {
        var taxReturn = await LoadReturnAsync(returnId, ct);

        if (taxReturn.Status is not (ReturnStatus.Filed or ReturnStatus.Processed)
            || string.IsNullOrWhiteSpace(taxReturn.AcknowledgmentNumber))
        {
            // Ch.9 §9.7: 409 when the return is not yet e-filed (no ITR-V to issue).
            throw AppException.Conflict(
                "The ITR-V acknowledgment is available only after the return is filed.",
                "REPORT.NOT_FILED");
        }

        var taxpayer = await LoadUserAsync(taxReturn.UserId, ct);
        var ay = await LoadAyAsync(taxReturn.AssessmentYearId, ct);
        var computation = await LatestComputationAsync(taxReturn.Id, ct);

        var title = $"ITR-V Acknowledgment — {ay?.Code ?? "AY"}";
        var lines = ReportContent.Acknowledgment(taxReturn, taxpayer, ay, computation);
        var fileName = $"ITRV-{taxReturn.AcknowledgmentNumber}.pdf";

        return await RenderStoreStreamAsync(
            taxReturn.TenantId, taxReturn.UserId, taxReturn.Id, "itr_v_ack",
            title, lines, fileName, ct);
    }

    // -------------------------------------------------------------- computation

    public async Task<GeneratedFile> GetComputationAsync(Guid returnId, CancellationToken ct = default)
    {
        var taxReturn = await LoadReturnAsync(returnId, ct);

        var computation = await LatestComputationAsync(taxReturn.Id, ct)
            ?? throw AppException.NotFound(
                "No finalized computation exists for this return yet.", "REPORT.NO_COMPUTATION");

        var taxpayer = await LoadUserAsync(taxReturn.UserId, ct);
        var ay = await LoadAyAsync(taxReturn.AssessmentYearId, ct);

        var title = $"Computation Worksheet — {ay?.Code ?? "AY"}";
        var lines = ReportContent.Computation(taxReturn, taxpayer, ay, computation);
        var fileName = $"Computation-{taxReturn.Id:N}.pdf";

        return await RenderStoreStreamAsync(
            taxReturn.TenantId, taxReturn.UserId, taxReturn.Id, "computation_sheet",
            title, lines, fileName, ct);
    }

    // ------------------------------------------------------------------ invoice

    public async Task<GeneratedFile> GetInvoiceAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await LoadPaymentAsync(paymentId, ct);

        if (payment.Status != PaymentStatus.Paid)
        {
            throw AppException.Conflict(
                "An invoice is available only for a captured payment.", "REPORT.PAYMENT_NOT_CAPTURED");
        }

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.PaymentId == payment.Id, ct)
            ?? throw AppException.NotFound("No invoice has been issued for this payment.", "REPORT.NO_INVOICE");

        var customer = await LoadUserAsync(payment.UserId, ct);
        var seller = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == payment.TenantId, ct);

        var title = $"Tax Invoice {invoice.Number}";
        var lines = ReportContent.Invoice(invoice, payment, customer, seller);
        var fileName = $"Invoice-{invoice.Number.Replace('/', '-')}.pdf";

        var file = await RenderStoreStreamAsync(
            payment.TenantId, payment.UserId, taxReturnId: payment.TaxReturnId,
            docType: "tax_invoice", title: title, lines: lines, fileName: fileName, ct: ct,
            onDocumentRegistered: doc =>
            {
                // Link the generated PDF back to the invoice row for downstream lookups.
                if (invoice.PdfDocumentId != doc.Id)
                {
                    invoice.PdfDocumentId = doc.Id;
                }
            });

        return file;
    }

    // ----------------------------------------------------------- list documents

    public async Task<IReadOnlyList<GeneratedDocumentDto>> ListReturnDocumentsAsync(
        Guid returnId, CancellationToken ct = default)
    {
        var taxReturn = await LoadReturnAsync(returnId, ct);

        var docs = await _db.Documents
            .Where(d => d.TaxReturnId == taxReturn.Id && d.Kind == DocumentKind.Other)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return docs
            .Select(d => new GeneratedDocumentDto(
                d.Id,
                DocTypeFromStorageKey(d.StoragePath),
                d.Kind, d.FileName, d.ContentType, d.SizeBytes, d.Sha256, d.Status, d.CreatedAt))
            .ToList();
    }

    // ============================================================== generation

    /// <summary>
    /// Render the PDF, store a copy under a deterministic vault key (overwriting any prior copy of
    /// the same logical doc), upsert the <see cref="Document"/> registry row, audit the access, and
    /// return the bytes to stream.
    /// </summary>
    private async Task<GeneratedFile> RenderStoreStreamAsync(
        Guid tenantId,
        Guid ownerUserId,
        Guid? taxReturnId,
        string docType,
        string title,
        IReadOnlyList<PdfLine> lines,
        string fileName,
        CancellationToken ct,
        Action<Document>? onDocumentRegistered = null)
    {
        var bytes = _pdf.Generate(title, lines);
        var sha = Convert.ToHexString(SHA256.HashData(bytes));
        var storageKey = BuildStorageKey(tenantId, taxReturnId, ownerUserId, docType);

        await _storage.SaveAsync(storageKey, bytes, PdfContentType, ct);

        // Upsert the vault registry row for this logical document (one row per storage key).
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.StoragePath == storageKey, ct);
        if (document is null)
        {
            document = new Document
            {
                TenantId = tenantId,
                UserId = ownerUserId,
                TaxReturnId = taxReturnId,
                Kind = DocumentKind.Other,
                FileName = fileName,
                ContentType = PdfContentType,
                StoragePath = storageKey,
                SizeBytes = bytes.Length,
                Sha256 = sha,
                Status = DocumentStatus.Verified
            };
            _db.Documents.Add(document);
        }
        else
        {
            document.FileName = fileName;
            document.SizeBytes = bytes.Length;
            document.Sha256 = sha;
            document.Status = DocumentStatus.Verified;
        }

        onDocumentRegistered?.Invoke(document);

        // Audit the generation + download (PII access event, docs 09 §9.10) on the same unit of work.
        _audit.Write("report.generated", docType, taxReturnId ?? document.Id, new
        {
            docType,
            contentHash = sha,
            sizeBytes = bytes.Length,
            by = _currentUser.UserId
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated {DocType} ({Bytes} bytes) for {Owner} by {ActorId}",
            docType, bytes.Length, ownerUserId, _currentUser.UserId);

        return new GeneratedFile(bytes, PdfContentType, fileName);
    }

    // ============================================================== scoping/io

    private async Task<TaxReturn> LoadReturnAsync(Guid returnId, CancellationToken ct)
    {
        var taxReturn = await _db.TaxReturns.FirstOrDefaultAsync(r => r.Id == returnId, ct)
            ?? throw AppException.NotFound("Return not found.", "REPORT.RETURN_NOT_FOUND");

        EnsureCanAccess(taxReturn.TenantId, taxReturn.UserId);
        return taxReturn;
    }

    private async Task<Payment> LoadPaymentAsync(Guid paymentId, CancellationToken ct)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw AppException.NotFound("Payment not found.", "REPORT.PAYMENT_NOT_FOUND");

        EnsureCanAccess(payment.TenantId, payment.UserId);
        return payment;
    }

    private async Task<User> LoadUserAsync(Guid userId, CancellationToken ct)
        => await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
           ?? throw AppException.NotFound("User not found.", "REPORT.USER_NOT_FOUND");

    private Task<AssessmentYear?> LoadAyAsync(Guid ayId, CancellationToken ct)
        => _db.AssessmentYears.FirstOrDefaultAsync(a => a.Id == ayId, ct);

    private Task<TaxComputation?> LatestComputationAsync(Guid returnId, CancellationToken ct)
        => _db.TaxComputations
            .Where(c => c.TaxReturnId == returnId)
            .OrderByDescending(c => c.IsRecommended)
            .ThenByDescending(c => c.ComputedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Owner-or-operator gate. The taxpayer who owns the resource may always read it; back-office
    /// roles may read within their tenant (SuperAdmin across all). Anyone else gets a 404 so the
    /// existence of another user's resource is never revealed.
    /// </summary>
    private void EnsureCanAccess(Guid tenantId, Guid ownerUserId)
    {
        if (ownerUserId == _currentUser.UserId)
        {
            return;
        }

        var isOperator = OperatorRoles.Any(_currentUser.IsInRole);
        if (isOperator && (_currentUser.IsInRole("SuperAdmin") || tenantId == _currentUser.TenantId))
        {
            return;
        }

        throw AppException.NotFound("Not found.", "REPORT.NOT_FOUND");
    }

    /// <summary>Deterministic vault key: generated/{tenant}/{return|user}/{docType}.pdf.</summary>
    private static string BuildStorageKey(Guid tenantId, Guid? taxReturnId, Guid ownerUserId, string docType)
    {
        var scope = taxReturnId is { } rid ? rid.ToString("N") : ownerUserId.ToString("N");
        return $"generated/{tenantId:N}/{scope}/{docType}.pdf";
    }

    /// <summary>Recover the logical doc type from a generated-document storage key.</summary>
    private static string DocTypeFromStorageKey(string storageKey)
    {
        var name = Path.GetFileNameWithoutExtension(storageKey);
        return string.IsNullOrWhiteSpace(name) ? "document" : name;
    }
}
