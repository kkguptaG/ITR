using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// One leg of a double-entry <see cref="Voucher"/>: a ledger, the side it is posted on
/// (<see cref="DrCr"/>) and the amount. A voucher has two or more entries whose debits and credits
/// are equal.
/// </summary>
public class VoucherEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid VoucherId { get; set; }
    public Guid LedgerId { get; set; }

    public DrCr Direction { get; set; }

    public decimal Amount { get; set; }

    public Voucher? Voucher { get; set; }
}
