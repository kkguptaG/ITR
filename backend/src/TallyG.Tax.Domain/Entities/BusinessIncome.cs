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

    /// <summary>Sparse ITR-3 schedules (jsonb on Postgres, text on Sqlite).</summary>
    public string BalanceSheetJson { get; set; } = "{}";
    public string PlJson { get; set; } = "{}";

    public TaxReturn? TaxReturn { get; set; }
}
