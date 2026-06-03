using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Accounting;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Bank-statement import application service. Mirrors the Documents pipeline (store → parse → match →
/// HITL review → commit) but, instead of mapping onto a tax return, it posts double-entry vouchers
/// into the user's standalone books and auto-creates any " (E)" ledgers the user adopts.
///
/// No manual DI — Scrutor binds BankImportService : IBankImportService scoped.
/// </summary>
public sealed class BankImportService : IBankImportService
{
    private const long MaxSizeBytes = 25L * 1024 * 1024; // 25 MB ceiling for a statement
    private const decimal ReviewConfidence = 0.80m;      // below this a line is flagged for review

    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "text/csv",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/octet-stream"
    };

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IBankStatementParser _parser;
    private readonly ILedgerMatchingService _matcher;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<BankImportService> _logger;

    public BankImportService(
        AppDbContext db,
        IFileStorage storage,
        IBankStatementParser parser,
        ILedgerMatchingService matcher,
        ICurrentUser currentUser,
        IDateTime clock,
        ILogger<BankImportService> logger)
    {
        _db = db;
        _storage = storage;
        _parser = parser;
        _matcher = matcher;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    // ===================================================================== upload

    public async Task<BankImportDetailDto> UploadAsync(
        Stream body, string fileName, string? contentType, Guid? bankLedgerId, CancellationToken ct = default)
    {
        RequireAuthenticated();

        var name = SanitizeFileName(fileName);
        var ct2 = NormalizeContentType(contentType);
        if (!AllowedContentTypes.Contains(ct2, StringComparer.OrdinalIgnoreCase))
        {
            throw AppException.Validation(
                $"Content type '{ct2}' is not supported. Upload a PDF, Excel (.xlsx) or CSV statement.",
                "BANKIMPORT.CONTENT_TYPE_UNSUPPORTED");
        }

        var bytes = await ReadBytesAsync(body, ct);

        var bankLedger = await EnsureBankLedgerAsync(bankLedgerId, ct);

        var importId = Guid.NewGuid();
        var storageKey = BuildStorageKey(importId, name);
        await _storage.SaveAsync(storageKey, bytes, ct2, ct);

        var import = new BankStatementImport
        {
            Id = importId,
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            BankLedgerId = bankLedger.Id,
            FileName = name,
            ContentType = ct2,
            StoragePath = storageKey,
            SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            Status = BankImportStatus.Parsing
        };
        _db.BankStatementImports.Add(import);
        await _db.SaveChangesAsync(ct);

        // Parse + match. The parser never throws; an empty result simply means "nothing recognised".
        var parsed = _parser.Parse(bytes, ct2, name);
        var existing = await OwnedLedgersAsync(ct);

        var warnings = parsed.Warnings.ToList();
        var generatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matched = 0;
        var rowIndex = 0;

        foreach (var p in parsed.Lines)
        {
            var (direction, amount) = Normalize(p.Debit, p.Credit);
            if (amount <= 0)
            {
                continue;
            }

            var suggestion = _matcher.Suggest(p.Narration, direction, existing);

            var line = new BankStatementLine
            {
                TenantId = _currentUser.TenantId,
                ImportId = import.Id,
                RowIndex = ++rowIndex,
                TxnDate = p.Date,
                Narration = p.Narration,
                ReferenceNo = p.ReferenceNo,
                Debit = p.Debit,
                Credit = p.Credit,
                RunningBalance = p.Balance,
                Direction = direction,
                Amount = amount,
                SuggestedLedgerId = suggestion.ExistingLedgerId,
                SuggestedLedgerName = suggestion.LedgerName,
                SuggestedGroup = suggestion.Group,
                SuggestionIsNewLedger = suggestion.IsNew,
                MatchConfidence = decimal.Round(suggestion.Confidence, 4),
                MatchMethod = suggestion.Method,
                MatchRationale = suggestion.Rationale,
                Status = BankLineStatus.Suggested
            };
            _db.BankStatementLines.Add(line);

            if (suggestion.IsNew)
            {
                generatedNames.Add(LedgerNaming.Strip(suggestion.LedgerName));
            }
            else
            {
                matched++;
            }
        }

        import.LineCount = rowIndex;
        import.MatchedCount = matched;
        import.GeneratedLedgerCount = generatedNames.Count;
        import.PeriodFrom = parsed.PeriodFrom;
        import.PeriodTo = parsed.PeriodTo;
        import.ParseWarningsJson = AccountingMappers.SerializeWarnings(warnings);
        import.Status = rowIndex == 0
            ? BankImportStatus.Failed
            : BankImportStatus.NeedsReview;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Imported statement {ImportId} '{File}': {Lines} lines, {Matched} matched, {Generated} new ledgers proposed",
            import.Id, name, rowIndex, matched, generatedNames.Count);

        return await BuildDetailAsync(import.Id, ct);
    }

    // ===================================================================== list/get

    public async Task<PagedResult<BankImportDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        RequireAuthenticated();
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = _db.BankStatementImports.AsNoTracking()
            .Where(i => i.TenantId == _currentUser.TenantId && i.UserId == _currentUser.UserId);

        var total = await q.LongCountAsync(ct);
        var rows = await q
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var names = await BankLedgerNamesAsync(rows.Select(r => r.BankLedgerId), ct);
        var items = rows
            .Select(r => AccountingMappers.ToDto(r, names.GetValueOrDefault(r.BankLedgerId, "Bank")))
            .ToList();

        return new PagedResult<BankImportDto>(items, page, pageSize, total);
    }

    public Task<BankImportDetailDto> GetAsync(Guid id, CancellationToken ct = default)
        => BuildDetailAsync(id, ct);

    // ===================================================================== post

    public async Task<PostImportResponse> PostAsync(Guid id, PostImportRequest request, CancellationToken ct = default)
    {
        var import = await LoadOwnedImportAsync(id, ct);

        var lines = await _db.BankStatementLines
            .Where(l => l.ImportId == import.Id)
            .OrderBy(l => l.RowIndex)
            .ToListAsync(ct);

        // Mutable working set of the user's ledgers, indexed by id and by normalised (stripped) name,
        // so repeated proposals collapse onto a single created head within this commit.
        var ledgers = await _db.Ledgers
            .Where(l => l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId)
            .ToListAsync(ct);
        var byId = ledgers.ToDictionary(l => l.Id);
        var byName = new Dictionary<string, Ledger>(StringComparer.Ordinal);
        foreach (var l in ledgers)
        {
            byName.TryAdd(NameKey(l.Name), l);
        }

        var decisions = (request.Decisions ?? Array.Empty<LineDecision>())
            .GroupBy(d => d.LineId)
            .ToDictionary(g => g.Key, g => g.Last());

        var createdLedgers = new List<Ledger>();
        var vouchersPosted = 0;
        var skipped = 0;

        foreach (var line in lines)
        {
            if (line.Status == BankLineStatus.Posted)
            {
                continue; // idempotent: never double-post
            }

            decisions.TryGetValue(line.Id, out var decision);

            // Skip when asked to, or when there is no decision and we are not auto-accepting suggestions.
            if (decision?.Skip == true)
            {
                line.Status = BankLineStatus.Skipped;
                skipped++;
                continue;
            }

            if (decision is null && !request.PostUnlistedSuggestions)
            {
                continue; // leave untouched for a later commit
            }

            var counter = ResolveCounterLedger(line, decision, import.BankLedgerId, byId, byName, createdLedgers);
            PostVoucher(import, line, counter);
            vouchersPosted++;
        }

        // Refresh denormalised counts and lifecycle.
        import.PostedCount = lines.Count(l => l.Status == BankLineStatus.Posted);
        var allResolved = lines.All(l => l.Status is BankLineStatus.Posted or BankLineStatus.Skipped);
        import.Status = allResolved && import.PostedCount > 0 ? BankImportStatus.Posted : BankImportStatus.NeedsReview;
        if (allResolved && import.PostedAt is null)
        {
            import.PostedAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Posted import {ImportId}: {Vouchers} vouchers, {Ledgers} ledgers created, {Skipped} skipped",
            import.Id, vouchersPosted, createdLedgers.Count, skipped);

        var bankName = (await BankLedgerNamesAsync(new[] { import.BankLedgerId }, ct))
            .GetValueOrDefault(import.BankLedgerId, "Bank");
        var movements = await LedgerBalances.ComputeAsync(
            _db, _currentUser.TenantId, createdLedgers.Select(l => l.Id).ToList(), ct);

        var createdDtos = createdLedgers
            .Select(l => AccountingMappers.ToDto(
                l,
                movements.TryGetValue(l.Id, out var m) ? m.Debits : 0m,
                movements.TryGetValue(l.Id, out var m2) ? m2.Credits : 0m,
                movements.TryGetValue(l.Id, out var m3) ? m3.Vouchers : 0))
            .ToList();

        return new PostImportResponse(
            AccountingMappers.ToDto(import, bankName), vouchersPosted, createdLedgers.Count, skipped, createdDtos);
    }

    // ===================================================================== delete

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var import = await LoadOwnedImportAsync(id, ct);

        var hasPosted = await _db.BankStatementLines
            .AnyAsync(l => l.ImportId == import.Id && l.Status == BankLineStatus.Posted, ct);
        if (hasPosted)
        {
            throw AppException.Conflict(
                "This statement has posted vouchers and cannot be deleted.", "BANKIMPORT.POSTED");
        }

        import.DeletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // =============================================================== posting helpers

    /// <summary>Resolve the counter-ledger for a line from an explicit decision or its suggestion.</summary>
    private Ledger ResolveCounterLedger(
        BankStatementLine line,
        LineDecision? decision,
        Guid bankLedgerId,
        Dictionary<Guid, Ledger> byId,
        Dictionary<string, Ledger> byName,
        List<Ledger> created)
    {
        // 1) Explicit existing ledger.
        if (decision?.LedgerId is { } chosenId)
        {
            if (!byId.TryGetValue(chosenId, out var chosen))
            {
                throw AppException.NotFound("The chosen ledger was not found in your books.", "LEDGER.NOT_FOUND");
            }

            GuardNotBank(chosen, bankLedgerId);
            return chosen;
        }

        // 2) Explicit new ledger the user named in the review screen (adopted → not flagged generated).
        if (decision?.NewLedger is { } spec)
        {
            return GetOrCreate(LedgerNaming.Strip(spec.Name), spec.Group, systemGenerated: false, byId, byName, created, bankLedgerId);
        }

        // 3) Accept the matcher's suggestion.
        if (!line.SuggestionIsNewLedger && line.SuggestedLedgerId is { } existingId && byId.TryGetValue(existingId, out var suggested))
        {
            GuardNotBank(suggested, bankLedgerId);
            return suggested;
        }

        var name = LedgerNaming.Strip(line.SuggestedLedgerName ?? "Suspense");
        var group = line.SuggestedGroup ?? LedgerGroup.Suspense;
        return GetOrCreate(name, group, systemGenerated: true, byId, byName, created, bankLedgerId);
    }

    private Ledger GetOrCreate(
        string baseName,
        LedgerGroup group,
        bool systemGenerated,
        Dictionary<Guid, Ledger> byId,
        Dictionary<string, Ledger> byName,
        List<Ledger> created,
        Guid bankLedgerId)
    {
        var key = NameKey(baseName);
        if (byName.TryGetValue(key, out var existing))
        {
            GuardNotBank(existing, bankLedgerId);
            return existing;
        }

        var ledger = new Ledger
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Name = systemGenerated ? LedgerNaming.Mark(baseName) : baseName,
            Group = group,
            Nature = LedgerGroupMeta.NatureOf(group),
            IsBank = group == LedgerGroup.BankAccounts,
            IsSystemGenerated = systemGenerated
        };
        _db.Ledgers.Add(ledger);
        byId[ledger.Id] = ledger;
        byName[key] = ledger;
        created.Add(ledger);
        return ledger;
    }

    private void PostVoucher(BankStatementImport import, BankStatementLine line, Ledger counter)
    {
        // Money in → Receipt (Dr Bank, Cr counter); money out → Payment (Dr counter, Cr Bank).
        DrCr bankSide, counterSide;
        VoucherType type;
        if (line.Direction == DrCr.Credit)
        {
            type = VoucherType.Receipt;
            bankSide = DrCr.Debit;
            counterSide = DrCr.Credit;
        }
        else
        {
            type = VoucherType.Payment;
            bankSide = DrCr.Credit;
            counterSide = DrCr.Debit;
        }

        // A transfer to another bank/cash head is a contra, not a receipt/payment.
        if (counter.IsBank || counter.Group == LedgerGroup.CashInHand)
        {
            type = VoucherType.Contra;
        }

        var date = line.TxnDate
                   ?? import.PeriodTo
                   ?? DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var voucher = new Voucher
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            ImportId = import.Id,
            BankStatementLineId = line.Id,
            Type = type,
            Date = date,
            Narration = line.Narration,
            ReferenceNo = line.ReferenceNo,
            Amount = line.Amount,
            Entries =
            {
                new VoucherEntry { TenantId = _currentUser.TenantId, LedgerId = import.BankLedgerId, Direction = bankSide, Amount = line.Amount },
                new VoucherEntry { TenantId = _currentUser.TenantId, LedgerId = counter.Id, Direction = counterSide, Amount = line.Amount }
            }
        };
        _db.Vouchers.Add(voucher);

        line.VoucherId = voucher.Id;
        line.ChosenLedgerId = counter.Id;
        line.Status = BankLineStatus.Posted;
    }

    private static void GuardNotBank(Ledger counter, Guid bankLedgerId)
    {
        if (counter.Id == bankLedgerId)
        {
            throw AppException.Validation(
                "The counter-ledger cannot be the same bank account the statement is imported against.",
                "BANKIMPORT.SELF_POSTING");
        }
    }

    // =============================================================== bank ledger

    private async Task<Ledger> EnsureBankLedgerAsync(Guid? bankLedgerId, CancellationToken ct)
    {
        if (bankLedgerId is { } id)
        {
            var ledger = await _db.Ledgers.FirstOrDefaultAsync(
                l => l.Id == id && l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId, ct)
                ?? throw AppException.NotFound("Bank ledger not found.", "LEDGER.NOT_FOUND");

            if (!ledger.IsBank && ledger.Group != LedgerGroup.BankAccounts)
            {
                throw AppException.Validation(
                    "The selected ledger is not a bank account. Pick a bank ledger or leave it blank.",
                    "BANKIMPORT.NOT_A_BANK_LEDGER");
            }

            return ledger;
        }

        // Default: reuse the user's single bank ledger if they have exactly one, else a generated head.
        var banks = await _db.Ledgers
            .Where(l => l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId && l.IsBank)
            .ToListAsync(ct);
        if (banks.Count == 1)
        {
            return banks[0];
        }

        var defaultName = LedgerNaming.Mark("Bank Account");
        var existing = banks.FirstOrDefault(b => NameKey(b.Name) == NameKey(defaultName));
        if (existing is not null)
        {
            return existing;
        }

        var created = new Ledger
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Name = defaultName,
            Group = LedgerGroup.BankAccounts,
            Nature = LedgerGroupMeta.NatureOf(LedgerGroup.BankAccounts),
            IsBank = true,
            IsSystemGenerated = true
        };
        _db.Ledgers.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    // =============================================================== loaders / utils

    private async Task<BankImportDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var import = await LoadOwnedImportAsync(id, ct);
        var lines = await _db.BankStatementLines.AsNoTracking()
            .Where(l => l.ImportId == import.Id)
            .OrderBy(l => l.RowIndex)
            .ToListAsync(ct);

        var bankName = (await BankLedgerNamesAsync(new[] { import.BankLedgerId }, ct))
            .GetValueOrDefault(import.BankLedgerId, "Bank");

        return new BankImportDetailDto(
            AccountingMappers.ToDto(import, bankName),
            lines.Select(AccountingMappers.ToDto).ToList());
    }

    private async Task<BankStatementImport> LoadOwnedImportAsync(Guid id, CancellationToken ct)
    {
        RequireAuthenticated();

        var import = await _db.BankStatementImports.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw AppException.NotFound("Statement import not found.", "BANKIMPORT.NOT_FOUND");

        if (import.TenantId != _currentUser.TenantId || import.UserId != _currentUser.UserId)
        {
            throw AppException.NotFound("Statement import not found.", "BANKIMPORT.NOT_FOUND");
        }

        return import;
    }

    private async Task<IReadOnlyList<Ledger>> OwnedLedgersAsync(CancellationToken ct)
        => await _db.Ledgers.AsNoTracking()
            .Where(l => l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId)
            .ToListAsync(ct);

    private async Task<Dictionary<Guid, string>> BankLedgerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var set = ids.Distinct().ToList();
        if (set.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _db.Ledgers.AsNoTracking()
            .Where(l => set.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
    }

    private static (DrCr Direction, decimal Amount) Normalize(decimal? debit, decimal? credit)
    {
        var d = debit ?? 0m;
        var c = credit ?? 0m;
        if (c > 0 && c >= d)
        {
            return (DrCr.Credit, c);
        }

        return (DrCr.Debit, d);
    }

    private async Task<byte[]> ReadBytesAsync(Stream body, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await body.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            throw AppException.Validation("The uploaded statement is empty.", "BANKIMPORT.EMPTY");
        }

        if (bytes.Length > MaxSizeBytes)
        {
            throw AppException.Validation(
                $"The statement exceeds the {MaxSizeBytes / (1024 * 1024)} MB limit.", "BANKIMPORT.TOO_LARGE");
        }

        return bytes;
    }

    private string BuildStorageKey(Guid importId, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ext.Length > 12)
        {
            ext = string.Empty;
        }

        return $"{_currentUser.TenantId:N}/{_currentUser.UserId:N}/bank/{importId:N}{ext}".ToLowerInvariant();
    }

    private static string NameKey(string name) => LedgerNaming.Strip(name).ToLowerInvariant();

    private static string SanitizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw AppException.Validation("A file name is required.", "BANKIMPORT.FILENAME_REQUIRED");
        }

        name = Path.GetFileName(name.Replace('\\', '/'));
        return name.Length > 255 ? name[^255..] : name;
    }

    private static string NormalizeContentType(string? contentType)
    {
        var value = (contentType ?? string.Empty).Trim();
        return value.Length == 0 ? "application/octet-stream" : value.ToLowerInvariant();
    }

    // ===================================================================== push-to-return

    public async Task<int> PushToReturnAsync(Guid importId, Guid returnId, CancellationToken ct = default)
    {
        var import = await LoadOwnedImportAsync(importId, ct);

        // Verify the return is owned by the same user.
        var taxReturn = await _db.TaxReturns
            .FirstOrDefaultAsync(r => r.Id == returnId && r.TenantId == _currentUser.TenantId && r.UserId == _currentUser.UserId, ct)
            ?? throw AppException.NotFound("Tax return not found.", "RETURN.NOT_FOUND");

        // Collect posted CREDIT lines whose counter-ledger is OtherIncome or SalesIncome.
        var lines = await (
            from l in _db.BankStatementLines
            join v in _db.Vouchers on l.VoucherId equals v.Id
            join led in _db.Ledgers on l.ChosenLedgerId equals led.Id
            where l.ImportId == import.Id
               && l.Status == BankLineStatus.Posted
               && l.Direction == DrCr.Credit         // money into the bank = income
               && (led.Group == LedgerGroup.OtherIncome || led.Group == LedgerGroup.SalesIncome)
            group l by new { l.ChosenLedgerId, led.Name } into g
            select new
            {
                g.Key.ChosenLedgerId,
                LedgerName = g.Key.Name,
                Total = g.Sum(x => x.Amount),
            }
        ).ToListAsync(ct);

        if (lines.Count == 0)
        {
            return 0;
        }

        var existing = await _db.IncomeSources
            .Where(s => s.TaxReturnId == returnId)
            .ToListAsync(ct);

        var count = 0;
        foreach (var line in lines)
        {
            if (line.Total <= 0m || line.ChosenLedgerId is null)
            {
                continue;
            }

            var nature = LedgerNameToNature(line.LedgerName);
            var label = line.LedgerName;
            var sourceMeta = JsonSerializer.Serialize(new
            {
                nature,
                sourceImportId = import.Id.ToString(),
                sourceLedgerId = line.ChosenLedgerId.ToString(),
            });

            // Idempotent: match by (return, import, ledger) embedded in the source meta.
            var row = existing.FirstOrDefault(s => s.Type == IncomeType.OtherSources
                && s.SourceMetaJson is not null
                && s.SourceMetaJson.Contains(line.ChosenLedgerId.ToString()!));

            if (row is null)
            {
                row = new IncomeSource
                {
                    TenantId = taxReturn.TenantId,
                    TaxReturnId = returnId,
                    Type = IncomeType.OtherSources,
                    Label = label,
                    Amount = line.Total,
                    SourceMetaJson = sourceMeta,
                };
                _db.IncomeSources.Add(row);
            }
            else
            {
                row.Amount = line.Total;
                row.Label = label;
            }

            count++;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>Map a counter-ledger name to the IncomeSource nature tag the engine and Schedule OS use.</summary>
    private static string LedgerNameToNature(string name)
    {
        var n = LedgerNaming.Strip(name).ToLowerInvariant();
        if (n.Contains("saving") || n.Contains("sb interest") || n.Contains("sbint"))
            return "savings_interest";
        if (n.Contains("fd") || n.Contains("fixed deposit") || n.Contains("term deposit") || n.Contains("rdinterest"))
            return "fd_interest";
        if (n.Contains("refund interest") || n.Contains("itrefund"))
            return "refund_interest";
        if (n.Contains("dividend") || n.Contains("div "))
            return "dividend";
        // Generic interest or sales income → the broadest catch-all
        if (n.Contains("interest"))
            return "interest";
        return "normal";
    }

    private void RequireAuthenticated()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AppException("AUTH.UNAUTHENTICATED", "Not authenticated.", 401);
        }
    }
}
