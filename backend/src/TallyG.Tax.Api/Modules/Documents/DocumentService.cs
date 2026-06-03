using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Documents;

/// <summary>
/// Documents application service. Implements the two-step pre-signed upload (Decision Log D-2),
/// a synchronous extraction stub (Ch.5), the confidence gate (review when a money field &lt; 0.92),
/// the HITL approve step, and tenant/ownership-scoped read + download.
///
/// No manual DI registration — Scrutor binds DocumentService : IDocumentService scoped.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    /// <summary>Money-field confidence gate (Ch.5 §5.2.4): below this an extraction needs human review.</summary>
    private const decimal ReviewThreshold = 0.92m;

    private static readonly TimeSpan UploadUrlTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DownloadUrlTtl = TimeSpan.FromMinutes(2);

    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/heic",
        "text/csv",
        "application/json",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/octet-stream"
    };

    private const long MaxSizeBytes = 50L * 1024 * 1024; // 50 MB ceiling (Ch.5 §5.1.1)

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IExtractionService _extractor;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        AppDbContext db,
        IFileStorage storage,
        IExtractionService extractor,
        ICurrentUser currentUser,
        IDateTime clock,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _extractor = extractor;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    // ----------------------------------------------------------- :initiate-upload

    public async Task<InitiateUploadResponse> InitiateUploadAsync(InitiateUploadRequest request, CancellationToken ct = default)
    {
        RequireAuthenticated();

        var kind = ParseKind(request.Kind);
        var fileName = SanitizeFileName(request.FileName);
        var contentType = NormalizeContentType(request.ContentType);

        if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw AppException.Validation(
                $"Content type '{contentType}' is not allowed.", "DOCUMENT.CONTENT_TYPE_UNSUPPORTED");
        }

        // If a return id is supplied it must belong to the caller (defence in depth on top of the
        // tenant query filter): never let a user attach a document to someone else's return.
        if (request.ReturnId is { } returnId)
        {
            await EnsureReturnOwnedAsync(returnId, ct);
        }

        var documentId = Guid.NewGuid();
        var storageKey = BuildStorageKey(documentId, fileName);

        var document = new Document
        {
            Id = documentId,
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            TaxReturnId = request.ReturnId,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType,
            StoragePath = storageKey,
            SizeBytes = 0,
            // The row exists before the bytes land; we model that with Scanning (pending-upload).
            // It flips to Uploaded once bytes arrive, then Extracted/NeedsReview after :complete.
            Status = DocumentStatus.Scanning
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        // STUB: dev uses a loopback PUT endpoint that streams to IFileStorage; prod returns an
        // S3 pre-signed PUT URL (SSE-KMS, content-length-range, 5-min TTL) — same client contract.
        var presigned = await _storage.CreateUploadUrlAsync(storageKey, contentType, UploadUrlTtl, ct);

        _logger.LogInformation(
            "Initiated upload {DocumentId} ({Kind}) for user {UserId}", documentId, kind, _currentUser.UserId);

        return new InitiateUploadResponse(
            documentId,
            presigned.Url,
            presigned.Method,
            presigned.Headers,
            presigned.ExpiresAt);
    }

    // -------------------------------------------------------------- receive bytes

    public async Task ReceiveBytesAsync(Guid documentId, Stream body, string? contentType, CancellationToken ct = default)
    {
        // This leg targets the canonical {id}:upload-bytes route. It is owner-scoped.
        var document = await LoadOwnedDocumentAsync(documentId, ct);
        await StoreBytesAsync(document, body, contentType, ct);
    }

    public async Task ReceiveBytesByKeyAsync(string storageKey, Stream body, string? contentType, CancellationToken ct = default)
    {
        // This leg backs the dev loopback URL (…/documents/_local-upload?key=…). The key encodes
        // the document id, so we resolve + scope the document from it.
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw AppException.Validation("A storage key is required.", "DOCUMENT.KEY_REQUIRED");
        }

        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.StoragePath == storageKey, ct)
            ?? throw AppException.NotFound("No document matches that upload key.", "DOCUMENT.NOT_FOUND");

        EnsureCanAccess(document);
        await StoreBytesAsync(document, body, contentType, ct);
    }

    private async Task StoreBytesAsync(Document document, Stream body, string? contentType, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await body.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            throw AppException.Validation("Uploaded file is empty.", "DOCUMENT.EMPTY");
        }

        if (bytes.Length > MaxSizeBytes)
        {
            throw AppException.Validation(
                $"File exceeds the {MaxSizeBytes / (1024 * 1024)} MB limit.", "DOCUMENT.TOO_LARGE");
        }

        await _storage.SaveAsync(document.StoragePath, bytes, document.ContentType, ct);

        document.SizeBytes = bytes.Length;
        document.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        document.Status = DocumentStatus.Uploaded;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Stored {Bytes} bytes for document {DocumentId}", bytes.Length, document.Id);
    }

    // -------------------------------------------------------------------- :complete

    public async Task<DocumentDto> CompleteAsync(Guid documentId, CompleteUploadRequest request, CancellationToken ct = default)
    {
        var document = await LoadOwnedDocumentAsync(documentId, ct);

        // The object must actually exist before we declare the upload complete.
        if (!await _storage.ExistsAsync(document.StoragePath, ct))
        {
            throw new AppException(
                "DOCUMENT.NOT_UPLOADED",
                "No uploaded bytes were found for this document. Complete the upload first.",
                409);
        }

        if (document.Status == DocumentStatus.Scanning)
        {
            // Bytes exist on disk but the row never flipped (e.g. a direct S3 PUT in prod). Recover.
            await HydrateMetadataFromStorageAsync(document, ct);
        }

        // Carry a client-supplied hash if we did not compute one (S3 path).
        if (string.IsNullOrEmpty(document.Sha256) && !string.IsNullOrWhiteSpace(request.Sha256))
        {
            document.Sha256 = request.Sha256.Trim();
        }

        document.Status = DocumentStatus.Extracting;
        await _db.SaveChangesAsync(ct);

        // STUB: synchronous extraction. In production this enqueues onto the ocr-extract SQS queue
        // and a worker pool processes it asynchronously (Ch.5 §5.2); the API call would return with
        // status Extracting and the client would poll. Here we run inline so the demo flow completes.
        var content = await _storage.ReadAsync(document.StoragePath, ct);
        var input = new ExtractionInput(document.Id, document.Kind, document.FileName, document.ContentType, content);
        var result = await _extractor.ExtractAsync(input, ct);

        var needsReview = result.AggregateConfidence < ReviewThreshold;
        var extractionStatus = needsReview ? DocumentStatus.NeedsReview : DocumentStatus.Extracted;

        var extraction = new DocumentExtraction
        {
            TenantId = document.TenantId,
            DocumentId = document.Id,
            Status = extractionStatus,
            ConfidenceScore = result.AggregateConfidence,
            FieldsJson = SerializeFields(result)
        };

        _db.DocumentExtractions.Add(extraction);
        document.Status = extractionStatus;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Completed document {DocumentId}: class={DocClass} confidence={Confidence} status={Status}",
            document.Id, result.DocClass, result.AggregateConfidence, extractionStatus);

        return ToDto(document, hasExtraction: true);
    }

    // -------------------------------------------------------------------- list/get

    public async Task<PagedResult<DocumentDto>> ListAsync(DocumentListQuery query, CancellationToken ct = default)
    {
        RequireAuthenticated();

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        // Tenant filter is global (query filter on the entity is not present, so scope explicitly).
        var q = _db.Documents.AsNoTracking()
            .Where(d => d.TenantId == _currentUser.TenantId && d.UserId == _currentUser.UserId);

        if (query.ReturnId is { } returnId)
        {
            q = q.Where(d => d.TaxReturnId == returnId);
        }

        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            var kind = ParseKind(query.Kind);
            q = q.Where(d => d.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<DocumentStatus>(query.Status, true, out var status))
        {
            q = q.Where(d => d.Status == status);
        }

        var total = await q.LongCountAsync(ct);

        var rows = await q
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                Doc = d,
                HasExtraction = _db.DocumentExtractions.Any(e => e.DocumentId == d.Id)
            })
            .ToListAsync(ct);

        var items = rows.Select(r => ToDto(r.Doc, r.HasExtraction)).ToList();
        return new PagedResult<DocumentDto>(items, page, pageSize, total);
    }

    public async Task<DocumentDto> GetAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await LoadAccessibleDocumentAsync(documentId, ct);
        var hasExtraction = await _db.DocumentExtractions.AnyAsync(e => e.DocumentId == documentId, ct);
        return ToDto(document, hasExtraction);
    }

    public async Task<ExtractionDto> GetExtractionAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await LoadAccessibleDocumentAsync(documentId, ct);
        var extraction = await LatestExtractionAsync(document.Id, ct)
            ?? throw AppException.NotFound("This document has not been extracted yet.", "DOCUMENT.EXTRACTION_NOT_FOUND");

        return ToExtractionDto(document, extraction);
    }

    // --------------------------------------------------------- extraction:approve

    public async Task<ApproveExtractionResponse> ApproveExtractionAsync(
        Guid documentId, ApproveExtractionRequest request, CancellationToken ct = default)
    {
        var document = await LoadAccessibleDocumentAsync(documentId, ct);
        var extraction = await LatestExtractionAsync(document.Id, ct)
            ?? throw AppException.NotFound("This document has not been extracted yet.", "DOCUMENT.EXTRACTION_NOT_FOUND");

        if (extraction.Status == DocumentStatus.Verified)
        {
            throw AppException.Conflict("This extraction is already verified.", "DOCUMENT.ALREADY_VERIFIED");
        }

        var fields = DeserializeFields(extraction.FieldsJson);

        // HITL corrections: a reviewer can overwrite values before acceptance. An override is treated
        // as fully confident (source = human) per Ch.5 — the human is authoritative over the model.
        if (request.FieldOverrides is { Count: > 0 })
        {
            foreach (var (key, value) in request.FieldOverrides)
            {
                fields[key] = new StoredField(value, 1.0m, "user");
            }

            extraction.FieldsJson = SerializeFields(fields);
        }

        extraction.Status = DocumentStatus.Verified;
        extraction.ReviewedByUserId = _currentUser.UserId;
        extraction.ReviewedAt = _clock.UtcNow;
        document.Status = DocumentStatus.Verified;

        var incomeUpserted = 0;
        var deductionsUpserted = 0;

        if (request.MapToReturn && document.TaxReturnId is { } returnId)
        {
            var taxReturn = await _db.TaxReturns
                .FirstOrDefaultAsync(r => r.Id == returnId && r.TenantId == document.TenantId, ct);

            if (taxReturn is not null)
            {
                (incomeUpserted, deductionsUpserted) = await MapFieldsOntoReturnAsync(taxReturn, document, fields, ct);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Approved extraction {ExtractionId} for document {DocumentId}; mapped {Income} income / {Deductions} deductions",
            extraction.Id, document.Id, incomeUpserted, deductionsUpserted);

        return new ApproveExtractionResponse(ToExtractionDto(document, extraction), incomeUpserted, deductionsUpserted);
    }

    // ------------------------------------------------------------------- download

    public async Task<DocumentDownload> GetDownloadAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await LoadAccessibleDocumentAsync(documentId, ct);

        var bytes = await _storage.ReadAsync(document.StoragePath, ct)
            ?? throw AppException.NotFound("The stored file is no longer available.", "DOCUMENT.BYTES_MISSING");

        // A download mint is an auditable PII access (Ch.5 §5.1.6); log it.
        _logger.LogInformation(
            "Document {DocumentId} downloaded by user {UserId}", document.Id, _currentUser.UserId);

        return new DocumentDownload(bytes, document.ContentType, document.FileName);
    }

    public async Task<DocumentDownload> GetDownloadByKeyAsync(string storageKey, CancellationToken ct = default)
    {
        RequireAuthenticated();

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw AppException.Validation("A storage key is required.", "DOCUMENT.KEY_REQUIRED");
        }

        var document = await _db.Documents.FirstOrDefaultAsync(d => d.StoragePath == storageKey, ct)
            ?? throw AppException.NotFound("Document not found.", "DOCUMENT.NOT_FOUND");

        EnsureCanAccess(document);

        var bytes = await _storage.ReadAsync(document.StoragePath, ct)
            ?? throw AppException.NotFound("The stored file is no longer available.", "DOCUMENT.BYTES_MISSING");

        return new DocumentDownload(bytes, document.ContentType, document.FileName);
    }

    // =============================================================== field mapping

    /// <summary>
    /// Deterministic mapping (Ch.5 §5.3) of canonical extraction fields onto the return's income
    /// sources and deductions. Idempotent per (document, income type / section): re-approving updates
    /// the rows this document owns rather than duplicating them.
    /// </summary>
    private async Task<(int income, int deductions)> MapFieldsOntoReturnAsync(
        TaxReturn taxReturn, Document document, IReadOnlyDictionary<string, StoredField> fields, CancellationToken ct)
    {
        var incomeByType = new Dictionary<IncomeType, (decimal Amount, string Label)>();
        var otherByNature = new Dictionary<string, (decimal Amount, string Label)>();
        var deductionBySection = new Dictionary<string, decimal>();

        foreach (var (key, field) in fields)
        {
            if (!TryParseMoney(field.Value, out var amount) || amount <= 0)
            {
                // Non-money fields (names, TAN, PAN, dates) do not map onto income/deduction rows.
                continue;
            }

            switch (key)
            {
                // ---- salary head (AIS/TIS only: gross-only line). Form 16 is handled separately below,
                // where its full breakup + TDS map onto a SalaryDetail the engine/generator consume. ----
                case "ais.salary_gross":
                case "tis.salary_processed_value":
                    Accumulate(incomeByType, IncomeType.Salary, amount, "Salary");
                    break;

                // ---- other sources: kept as separate nature-tagged rows so Schedule OS itemises them and
                // the AIS/26AS reconciliation matches them head-by-head (not lumped into one untagged row). ----
                case "ais.interest_savings_bank":
                case "bank.interest_savings_bank":
                    AccumulateOther(otherByNature, "savings_interest", amount, "Interest — savings bank");
                    break;
                case "ais.interest_term_deposit":
                case "bank.interest_fixed_deposit":
                    AccumulateOther(otherByNature, "fd_interest", amount, "Interest — term deposit");
                    break;
                case "ais.interest_others":
                    AccumulateOther(otherByNature, "interest", amount, "Interest — other");
                    break;
                case "ais.interest_income_tax_refund":
                    AccumulateOther(otherByNature, "refund_interest", amount, "Interest on income-tax refund");
                    break;
                case "ais.dividend_income":
                    AccumulateOther(otherByNature, "dividend", amount, "Dividend");
                    break;

                // ---- capital gains ----
                case "capgain.equity_stcg_111a":
                case "capgain.equity_ltcg_112a":
                case "ais.sft_mutual_fund_redemption":
                    Accumulate(incomeByType, IncomeType.CapitalGains, amount, "Capital gains");
                    break;

                // ---- business (presumptive turnover) ----
                case "gst.turnover_total":
                    Accumulate(incomeByType, IncomeType.Business, amount, "Business receipts");
                    break;

                // ---- deductions ----
                case "form16.part_b.deduction_80c":
                case "80c.lic_premium":
                case "80c.elss":
                    AccumulateDeduction(deductionBySection, "80C", amount);
                    break;

                case "form16.part_b.deduction_80d":
                case "80d.health_premium_self":
                    AccumulateDeduction(deductionBySection, "80D", amount);
                    break;
            }
        }

        var income = await UpsertIncomeSourcesAsync(taxReturn, document, incomeByType, ct);
        income += await UpsertOtherIncomeByNatureAsync(taxReturn, document, otherByNature, ct);
        var deductions = await UpsertDeductionsAsync(taxReturn, document, deductionBySection, ct);

        // Form 16 carries a full salary breakup + salary TDS — map it onto a SalaryDetail (+ a deductor-wise
        // TdsEntry), the entities the engine, ITR generator and Schedule S actually read.
        if (document.Kind == DocumentKind.Form16)
        {
            income += await MapForm16SalaryAndTdsAsync(taxReturn, document, fields, ct);
        }

        return (income, deductions);
    }

    /// <summary>
    /// Form 16 → a SalaryDetail (gross 17(1), HRA exemption, standard deduction, professional tax,
    /// employer + TAN) and, when TDS was deducted, a salary <see cref="TdsEntry"/> that feeds Schedule
    /// TDS1 and the refund credit. Idempotent: the SalaryDetail is matched by <c>Form16DocumentId</c> and
    /// the TDS entry by (return, salary head, deductor TAN). Returns 1 when a salary row was upserted.
    /// </summary>
    private async Task<int> MapForm16SalaryAndTdsAsync(
        TaxReturn taxReturn, Document document, IReadOnlyDictionary<string, StoredField> fields, CancellationToken ct)
    {
        decimal Money(string key) => fields.TryGetValue(key, out var f) && TryParseMoney(f.Value, out var amt) ? Math.Max(0m, amt) : 0m;
        string? Text(string key) => fields.TryGetValue(key, out var f) && !string.IsNullOrWhiteSpace(f.Value) ? f.Value.Trim() : null;

        var gross = Money("form16.part_b.gross_salary_17_1");
        if (gross <= 0m)
        {
            return 0;   // nothing salary-like was extracted
        }

        var employer = Text("form16.part_a.employer_name") ?? "Employer (Form 16)";
        var tan = Text("form16.part_a.employer_tan");
        var tds = Money("form16.part_b.tds_total");

        var salary = await _db.SalaryDetails.FirstOrDefaultAsync(
            s => s.TaxReturnId == taxReturn.Id && s.Form16DocumentId == document.Id, ct);
        if (salary is null)
        {
            salary = new SalaryDetail { TenantId = taxReturn.TenantId, TaxReturnId = taxReturn.Id, Form16DocumentId = document.Id };
            _db.SalaryDetails.Add(salary);
        }

        salary.Employer = employer;
        salary.Tan = tan;
        salary.Gross = gross;
        salary.HraExemption = Money("form16.part_b.hra_exempt_10_13a");
        salary.StdDeduction = Money("form16.part_b.std_deduction_16ia");
        salary.ProfessionalTax = Money("form16.part_b.professional_tax_16iii");

        if (tds > 0m && !string.IsNullOrWhiteSpace(tan))
        {
            var entry = await _db.TdsEntries.FirstOrDefaultAsync(
                t => t.TaxReturnId == taxReturn.Id && t.Head == TdsHead.Salary && t.DeductorTan == tan, ct);
            if (entry is null)
            {
                entry = new TdsEntry
                {
                    TenantId = taxReturn.TenantId, UserId = taxReturn.UserId, TaxReturnId = taxReturn.Id,
                    Head = TdsHead.Salary, DeductorTan = tan,
                };
                _db.TdsEntries.Add(entry);
            }

            entry.DeductorName = employer;
            entry.IncomeOffered = gross;
            entry.TaxDeducted = tds;

            // Keep the return's TDS-credit rollup consistent (computed in-memory: existing entries that
            // aren't this one, plus this one's amount), so the refund/payable reflects the Form 16 TDS.
            var others = await _db.TdsEntries
                .Where(t => t.TaxReturnId == taxReturn.Id && t.Id != entry.Id)
                .SumAsync(t => t.TaxDeducted, ct);
            taxReturn.TdsPaid = others + entry.TaxDeducted;
        }

        return 1;
    }

    private async Task<int> UpsertIncomeSourcesAsync(
        TaxReturn taxReturn, Document document, Dictionary<IncomeType, (decimal Amount, string Label)> incomeByType, CancellationToken ct)
    {
        if (incomeByType.Count == 0)
        {
            return 0;
        }

        var existing = await _db.IncomeSources
            .Where(s => s.TaxReturnId == taxReturn.Id)
            .ToListAsync(ct);

        var count = 0;
        foreach (var (type, value) in incomeByType)
        {
            // Match an income row this document already created (tagged via SourceMetaJson), else add.
            var row = existing.FirstOrDefault(s => s.Type == type && SourceDocumentId(s.SourceMetaJson) == document.Id);
            if (row is null)
            {
                row = new IncomeSource
                {
                    TenantId = taxReturn.TenantId,
                    TaxReturnId = taxReturn.Id,
                    Type = type,
                    Label = value.Label,
                    Amount = value.Amount,
                    SourceMetaJson = BuildSourceMeta(document.Id, document.Kind)
                };
                _db.IncomeSources.Add(row);
            }
            else
            {
                row.Amount = value.Amount;
                row.Label = value.Label;
            }

            count++;
        }

        return count;
    }

    /// <summary>Upsert one OtherSources income row per nature (savings/fd/other/refund interest, dividend),
    /// tagged with the nature in SourceMetaJson so Schedule OS itemises it and the reconciliation matches it.
    /// Idempotent per (document, nature).</summary>
    private async Task<int> UpsertOtherIncomeByNatureAsync(
        TaxReturn taxReturn, Document document, Dictionary<string, (decimal Amount, string Label)> otherByNature, CancellationToken ct)
    {
        if (otherByNature.Count == 0)
        {
            return 0;
        }

        var existing = await _db.IncomeSources
            .Where(s => s.TaxReturnId == taxReturn.Id && s.Type == IncomeType.OtherSources)
            .ToListAsync(ct);

        var count = 0;
        foreach (var (nature, value) in otherByNature)
        {
            var row = existing.FirstOrDefault(s => SourceDocumentId(s.SourceMetaJson) == document.Id
                && string.Equals(TaxComputationInputFactory.ExtractNature(s.SourceMetaJson), nature, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new IncomeSource
                {
                    TenantId = taxReturn.TenantId,
                    TaxReturnId = taxReturn.Id,
                    Type = IncomeType.OtherSources,
                    Label = value.Label,
                    Amount = value.Amount,
                    SourceMetaJson = BuildSourceMeta(document.Id, document.Kind, nature)
                };
                _db.IncomeSources.Add(row);
            }
            else
            {
                row.Amount = value.Amount;
                row.Label = value.Label;
            }

            count++;
        }

        return count;
    }

    private async Task<int> UpsertDeductionsAsync(
        TaxReturn taxReturn, Document document, Dictionary<string, decimal> deductionBySection, CancellationToken ct)
    {
        if (deductionBySection.Count == 0)
        {
            return 0;
        }

        var existing = await _db.Deductions
            .Where(d => d.TaxReturnId == taxReturn.Id)
            .ToListAsync(ct);

        var count = 0;
        foreach (var (section, amount) in deductionBySection)
        {
            var row = existing.FirstOrDefault(d => d.Section == section && d.ProofDocumentId == document.Id);
            if (row is null)
            {
                row = new Deduction
                {
                    TenantId = taxReturn.TenantId,
                    TaxReturnId = taxReturn.Id,
                    Section = section,
                    Amount = amount,
                    Description = $"From {document.Kind} ({document.FileName})",
                    ProofDocumentId = document.Id
                };
                _db.Deductions.Add(row);
            }
            else
            {
                row.Amount = amount;
            }

            count++;
        }

        return count;
    }

    private static void Accumulate(
        Dictionary<IncomeType, (decimal Amount, string Label)> map, IncomeType type, decimal amount, string label)
    {
        if (map.TryGetValue(type, out var current))
        {
            map[type] = (current.Amount + amount, current.Label);
        }
        else
        {
            map[type] = (amount, label);
        }
    }

    private static void AccumulateOther(
        Dictionary<string, (decimal Amount, string Label)> map, string nature, decimal amount, string label)
    {
        if (map.TryGetValue(nature, out var current))
        {
            map[nature] = (current.Amount + amount, current.Label);
        }
        else
        {
            map[nature] = (amount, label);
        }
    }

    private static void AccumulateDeduction(Dictionary<string, decimal> map, string section, decimal amount)
        => map[section] = (map.TryGetValue(section, out var current) ? current : 0m) + amount;

    // =============================================================== persistence helpers

    private async Task HydrateMetadataFromStorageAsync(Document document, CancellationToken ct)
    {
        var bytes = await _storage.ReadAsync(document.StoragePath, ct);
        if (bytes is null)
        {
            return;
        }

        document.SizeBytes = bytes.Length;
        if (string.IsNullOrEmpty(document.Sha256))
        {
            document.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        }

        document.Status = DocumentStatus.Uploaded;
    }

    private async Task<DocumentExtraction?> LatestExtractionAsync(Guid documentId, CancellationToken ct)
        => await _db.DocumentExtractions
            .Where(e => e.DocumentId == documentId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Load a document the caller OWNS (mutating operations: upload/complete).</summary>
    private async Task<Document> LoadOwnedDocumentAsync(Guid documentId, CancellationToken ct)
    {
        RequireAuthenticated();

        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw AppException.NotFound("Document not found.", "DOCUMENT.NOT_FOUND");

        if (document.TenantId != _currentUser.TenantId || document.UserId != _currentUser.UserId)
        {
            // Do not disclose existence across owners.
            throw AppException.NotFound("Document not found.", "DOCUMENT.NOT_FOUND");
        }

        return document;
    }

    /// <summary>
    /// Load a document the caller may READ. Owners always can; staff/CA/Ops roles may read within
    /// the same tenant (the CA-assignment scoping of Ch.5 §5.1.6 is enforced by the CA module).
    /// </summary>
    private async Task<Document> LoadAccessibleDocumentAsync(Guid documentId, CancellationToken ct)
    {
        RequireAuthenticated();

        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw AppException.NotFound("Document not found.", "DOCUMENT.NOT_FOUND");

        EnsureCanAccess(document);
        return document;
    }

    private void EnsureCanAccess(Document document)
    {
        if (document.TenantId != _currentUser.TenantId)
        {
            throw AppException.NotFound("Document not found.", "DOCUMENT.NOT_FOUND");
        }

        var isOwner = document.UserId == _currentUser.UserId;
        var isStaff = _currentUser.IsInRole("Admin")
                      || _currentUser.IsInRole("Ops")
                      || _currentUser.IsInRole("Reviewer")
                      || _currentUser.IsInRole("CA");

        if (!isOwner && !isStaff)
        {
            throw AppException.Forbidden("You cannot access this document.", "DOCUMENT.FORBIDDEN");
        }
    }

    private async Task EnsureReturnOwnedAsync(Guid returnId, CancellationToken ct)
    {
        var owned = await _db.TaxReturns.AnyAsync(
            r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct);

        if (!owned)
        {
            throw AppException.NotFound("Return not found.", "RETURN.NOT_FOUND");
        }
    }

    private void RequireAuthenticated()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }
    }

    // =============================================================== mapping helpers

    private DocumentDto ToDto(Document d, bool hasExtraction) => new(
        d.Id,
        d.TaxReturnId,
        d.Kind.ToString(),
        d.FileName,
        d.ContentType,
        d.SizeBytes,
        d.Status.ToString(),
        d.Sha256,
        hasExtraction,
        d.CreatedAt,
        d.UpdatedAt);

    private ExtractionDto ToExtractionDto(Document document, DocumentExtraction extraction)
    {
        var stored = DeserializeFields(extraction.FieldsJson);
        var fields = stored
            .Select(kv => new ExtractedFieldDto(kv.Key, kv.Value.Value, kv.Value.Confidence))
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .ToList();

        var docClass = stored.TryGetValue(MetaDocClassKey, out var dc) ? dc.Value : document.Kind.ToString();
        var needsReview = extraction.Status == DocumentStatus.NeedsReview;

        return new ExtractionDto(
            extraction.Id,
            extraction.DocumentId,
            docClass,
            extraction.Status.ToString(),
            extraction.ConfidenceScore,
            extraction.FieldsJson,
            fields,
            needsReview,
            extraction.ReviewedByUserId,
            extraction.ReviewedAt,
            extraction.CreatedAt);
    }

    // =============================================================== JSON field model

    // The DocumentExtraction.FieldsJson is a portable string map (jsonb on Postgres, text on Sqlite):
    //   { "_docClass": {value,confidence,source}, "form16.part_b.gross_salary_17_1": {...}, ... }
    private const string MetaDocClassKey = "_docClass";

    private sealed record StoredField(string Value, decimal Confidence, string Source);

    private static string SerializeFields(ExtractionResult result)
    {
        var map = new Dictionary<string, StoredField>(StringComparer.Ordinal)
        {
            [MetaDocClassKey] = new StoredField(result.DocClass, result.AggregateConfidence, "rule")
        };

        foreach (var f in result.Fields)
        {
            map[f.Key] = new StoredField(f.Value, f.Confidence, "ocr");
        }

        return JsonSerializer.Serialize(map, JsonOptions);
    }

    private static string SerializeFields(IDictionary<string, StoredField> fields)
        => JsonSerializer.Serialize(fields, JsonOptions);

    private static Dictionary<string, StoredField> DeserializeFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, StoredField>(StringComparer.Ordinal);
        }

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, StoredField>>(json, JsonOptions);
            return map is null
                ? new Dictionary<string, StoredField>(StringComparer.Ordinal)
                : new Dictionary<string, StoredField>(map, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Tolerate an unexpected shape rather than 500 — return an empty map.
            return new Dictionary<string, StoredField>(StringComparer.Ordinal);
        }
    }

    // =============================================================== small utilities

    private string BuildStorageKey(Guid documentId, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ext.Length > 12)
        {
            ext = string.Empty; // ignore absurd "extensions"
        }

        // tenant/user/document.ext — mirrors the vault layout (Ch.5 §5.1.5) in a flat dev form.
        return $"{_currentUser.TenantId:N}/{_currentUser.UserId:N}/{documentId:N}{ext}".ToLowerInvariant();
    }

    private static string BuildSourceMeta(Guid documentId, DocumentKind kind)
        => JsonSerializer.Serialize(new { sourceDocumentId = documentId, sourceKind = kind.ToString() }, JsonOptions);

    private static string BuildSourceMeta(Guid documentId, DocumentKind kind, string nature)
        => JsonSerializer.Serialize(new { sourceDocumentId = documentId, sourceKind = kind.ToString(), nature }, JsonOptions);

    private static Guid? SourceDocumentId(string sourceMetaJson)
    {
        if (string.IsNullOrWhiteSpace(sourceMetaJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(sourceMetaJson);
            if (doc.RootElement.TryGetProperty("sourceDocumentId", out var prop)
                && prop.ValueKind == JsonValueKind.String
                && Guid.TryParse(prop.GetString(), out var id))
            {
                return id;
            }
        }
        catch (JsonException)
        {
            // ignore malformed meta
        }

        return null;
    }

    private static bool TryParseMoney(string? value, out decimal amount)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private static DocumentKind ParseKind(string? kind)
    {
        if (!string.IsNullOrWhiteSpace(kind) && Enum.TryParse<DocumentKind>(kind.Trim(), true, out var parsed))
        {
            return parsed;
        }

        throw AppException.Validation($"Unsupported document kind '{kind}'.", "DOCUMENT.KIND_UNSUPPORTED");
    }

    private static string SanitizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw AppException.Validation("A file name is required.", "DOCUMENT.FILENAME_REQUIRED");
        }

        // Keep just the leaf and strip path separators to avoid traversal surprises in the key.
        name = Path.GetFileName(name.Replace('\\', '/'));
        return name.Length > 255 ? name[^255..] : name;
    }

    private static string NormalizeContentType(string? contentType)
    {
        var value = (contentType ?? string.Empty).Trim();
        return value.Length == 0 ? "application/octet-stream" : value.ToLowerInvariant();
    }
}
