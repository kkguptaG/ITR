namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// Chart-of-accounts management for a user's standalone books: list / read / create / update / delete
/// ledgers, including the system-generated " (E)" heads the matcher creates. Auto-registered scoped
/// by Scrutor (LedgerService : ILedgerService).
/// </summary>
public interface ILedgerService
{
    Task<IReadOnlyList<LedgerDto>> ListAsync(string? group, bool? systemGeneratedOnly, bool? bankOnly, CancellationToken ct = default);

    Task<LedgerDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<LedgerDto> CreateAsync(CreateLedgerRequest request, CancellationToken ct = default);

    /// <summary>Update a ledger; clears the system-generated flag (the user has adopted the head).</summary>
    Task<LedgerDto> UpdateAsync(Guid id, UpdateLedgerRequest request, CancellationToken ct = default);

    /// <summary>Soft-delete a ledger. Blocked if it has posted vouchers or backs a bank import.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
