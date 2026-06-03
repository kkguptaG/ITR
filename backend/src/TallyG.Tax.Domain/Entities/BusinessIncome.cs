using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Business / profession head detail incl. presumptive 44AD/44ADA/44AE and the
/// speculative (intraday) vs non-speculative (F&O) flag that routes to ITR-3 (Ch.3 §3.10).
/// </summary>
public class BusinessIncome : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string? NatureOfBusinessCode { get; set; }
    public string AccountingMethod { get; set; } = "mercantile";

    public bool IsPresumptive { get; set; }

    /// <summary>"44AD" | "44ADA" | "44AE" when presumptive.</summary>
    public string? PresumptiveSection { get; set; }

    public decimal Turnover { get; set; }
    public decimal GrossReceiptsDigital { get; set; }
    public decimal GrossReceiptsCash { get; set; }
    public decimal PresumptiveRatePct { get; set; }
    public decimal NetProfit { get; set; }

    /// <summary>True for intraday (speculative) business income.</summary>
    public bool SpeculativeFlag { get; set; }

    public decimal GstTurnoverReported { get; set; }

    // --- Financial particulars of business (ITR-4 Sugam "FinanclPartclrOfBusiness", ITR-3 no-account case) ---
    // The "no-account case" balance-sheet minimums the ITD requires even from presumptive filers.
    /// <summary>Proprietor / partner own capital (liabilities side).</summary>
    public decimal PartnerCapital { get; set; }
    public decimal SecuredLoans { get; set; }
    public decimal UnsecuredLoans { get; set; }
    public decimal SundryCreditors { get; set; }
    public decimal FixedAssets { get; set; }
    public decimal Inventory { get; set; }            // closing stock
    public decimal SundryDebtors { get; set; }
    public decimal BankBalance { get; set; }
    public decimal CashBalance { get; set; }

    /// <summary>
    /// 44AE goods-carriage vehicles (JSON list). Each item: {regNo, ownership(O|L|H), tonnage, months, income}.
    /// Empty "[]" when not a 44AE business.
    /// </summary>
    public string GoodsCarriageJson { get; set; } = "[]";

    /// <summary>Sparse ITR-3 schedules (jsonb on Postgres, text on Sqlite).</summary>
    public string BalanceSheetJson { get; set; } = "{}";
    public string PlJson { get; set; } = "{}";

    public TaxReturn? TaxReturn { get; set; }
}
