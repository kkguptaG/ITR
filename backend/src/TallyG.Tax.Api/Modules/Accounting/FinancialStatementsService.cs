using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>One ledger-group line on a financial statement (group name + net balance, whole rupees).</summary>
public sealed record GroupBalanceDto(string Group, decimal Amount);

/// <summary>Profit &amp; Loss derived from the books: income less expenses = net profit/(loss).</summary>
public sealed record ProfitAndLossDto(
    IReadOnlyList<GroupBalanceDto> Income,
    decimal TotalIncome,
    IReadOnlyList<GroupBalanceDto> Expenses,
    decimal TotalExpenses,
    decimal NetProfit);

/// <summary>Balance Sheet derived from the books: assets vs. liabilities + capital (incl. net profit).</summary>
public sealed record BalanceSheetDto(
    IReadOnlyList<GroupBalanceDto> Assets,
    decimal TotalAssets,
    IReadOnlyList<GroupBalanceDto> LiabilitiesAndCapital,
    decimal TotalLiabilitiesAndCapital,
    bool IsBalanced);

/// <summary>Financial statements derived from a user's double-entry books (the ITR-3 BS/P&amp;L source).</summary>
public sealed record FinancialStatementsDto(ProfitAndLossDto ProfitAndLoss, BalanceSheetDto BalanceSheet);

public interface IFinancialStatementsService
{
    Task<FinancialStatementsDto> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Derives a Balance Sheet and Profit &amp; Loss from the user's double-entry ledgers — net balance per
/// ledger (opening balance + posted movement, in the group's natural direction), aggregated by
/// <see cref="LedgerGroup"/>. This is the source for ITR-3's Schedule BP / PARTA_BS / PARTA_PL, so a
/// regular-books filer's financials come from their books rather than re-entry. Owner/tenant-scoped.
/// Scrutor binds FinancialStatementsService : IFinancialStatementsService scoped.
/// </summary>
public sealed class FinancialStatementsService : IFinancialStatementsService
{
    // Debit-nature groups carry a debit balance (assets, expenses); the rest are credit-nature
    // (liabilities, capital, income). Suspense is treated as an asset (debit) by convention.
    private static readonly HashSet<LedgerGroup> DebitNature = new()
    {
        LedgerGroup.BankAccounts, LedgerGroup.CashInHand, LedgerGroup.SundryDebtors,
        LedgerGroup.PurchaseAccounts, LedgerGroup.DirectExpenses, LedgerGroup.IndirectExpenses,
        LedgerGroup.FixedAssets, LedgerGroup.Investments, LedgerGroup.Suspense,
    };

    private static readonly LedgerGroup[] IncomeGroups = { LedgerGroup.SalesIncome, LedgerGroup.OtherIncome };
    private static readonly LedgerGroup[] ExpenseGroups = { LedgerGroup.PurchaseAccounts, LedgerGroup.DirectExpenses, LedgerGroup.IndirectExpenses };
    private static readonly LedgerGroup[] AssetGroups = { LedgerGroup.FixedAssets, LedgerGroup.Investments, LedgerGroup.SundryDebtors, LedgerGroup.BankAccounts, LedgerGroup.CashInHand, LedgerGroup.Suspense };
    private static readonly LedgerGroup[] LiabilityGroups = { LedgerGroup.CapitalAccount, LedgerGroup.LoansAndLiabilities, LedgerGroup.SundryCreditors, LedgerGroup.DutiesAndTaxes };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public FinancialStatementsService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<FinancialStatementsDto> GetAsync(CancellationToken ct = default)
    {
        var ledgers = await _db.Ledgers
            .Where(l => l.TenantId == _currentUser.TenantId && l.UserId == _currentUser.UserId)
            .ToListAsync(ct);

        var movements = await LedgerBalances.ComputeAsync(
            _db, _currentUser.TenantId, ledgers.Select(l => l.Id).ToList(), ct);

        // Net balance per group, in the group's natural (debit/credit) direction.
        var byGroup = new Dictionary<LedgerGroup, decimal>();
        foreach (var l in ledgers)
        {
            var m = movements.TryGetValue(l.Id, out var mv) ? mv : default;
            var movement = DebitNature.Contains(l.Group) ? m.Debits - m.Credits : m.Credits - m.Debits;
            var balance = l.OpeningBalance + movement;
            byGroup[l.Group] = (byGroup.TryGetValue(l.Group, out var v) ? v : 0m) + balance;
        }

        decimal Bal(LedgerGroup g) => byGroup.TryGetValue(g, out var v) ? Round(v) : 0m;
        List<GroupBalanceDto> Rows(IEnumerable<LedgerGroup> groups) =>
            groups.Select(g => new GroupBalanceDto(g.ToString(), Bal(g))).Where(r => r.Amount != 0m).ToList();

        var income = Rows(IncomeGroups);
        var expenses = Rows(ExpenseGroups);
        var totalIncome = income.Sum(r => r.Amount);
        var totalExpenses = expenses.Sum(r => r.Amount);
        var netProfit = totalIncome - totalExpenses;

        var assets = Rows(AssetGroups);
        var totalAssets = assets.Sum(r => r.Amount);

        var liabilities = Rows(LiabilityGroups);
        // Net profit accrues to the capital account (closes P&L into the balance sheet).
        if (netProfit != 0m)
        {
            liabilities.Add(new GroupBalanceDto("NetProfitToCapital", netProfit));
        }

        var totalLiabilities = liabilities.Sum(r => r.Amount);

        return new FinancialStatementsDto(
            new ProfitAndLossDto(income, totalIncome, expenses, totalExpenses, netProfit),
            new BalanceSheetDto(assets, totalAssets, liabilities, totalLiabilities, totalAssets == totalLiabilities));
    }

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
