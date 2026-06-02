using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// A bank account the assessee has fed for filing. The four fields are mandatory (they mirror the ITR
/// schema's BankDetailType). Exactly one account is flagged <see cref="UseForRefund"/> so any refund is
/// credited there. User-scoped (reused across that user's returns / assessment years).
/// </summary>
public class BankAccountDetail : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public string BankName { get; set; } = string.Empty;

    /// <summary>Account number. PII — should be encrypted at rest in production (no encryptor wired yet).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>SB / CA / CC / OD / NRO / OTH (the ITR AccountType enum).</summary>
    public string AccountType { get; set; } = "SB";

    public string Ifsc { get; set; } = string.Empty;

    /// <summary>Exactly one of a user's accounts is the refund account (enforced by the service).</summary>
    public bool UseForRefund { get; set; }
}
