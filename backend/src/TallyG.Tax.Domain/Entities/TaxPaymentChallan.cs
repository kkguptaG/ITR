using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A self-paid tax challan (advance tax or self-assessment tax) — the building block of the ITR's
/// Schedule IT / TaxPayments. Return-scoped. The four fields mirror the schema's TaxPayment type.
/// </summary>
public class TaxPaymentChallan : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxReturnId { get; set; }

    /// <summary>Advance tax (s.208/211) vs self-assessment tax (s.140A).</summary>
    public ChallanKind Kind { get; set; } = ChallanKind.SelfAssessment;

    /// <summary>7-char bank BSR code (3 digits + 4 alphanumeric).</summary>
    public string BsrCode { get; set; } = string.Empty;

    /// <summary>Date the tax was deposited.</summary>
    public DateOnly DepositDate { get; set; }

    /// <summary>Challan serial number (CIN).</summary>
    public int ChallanSerial { get; set; }

    public decimal Amount { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
