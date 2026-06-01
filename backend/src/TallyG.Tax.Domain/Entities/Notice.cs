using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>An ITD notice received post-filing (Ch.2 §2.8). V1 passive vault (Decision Log D-6).</summary>
public class Notice : BaseEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TaxReturnId { get; set; }

    /// <summary>e.g. "143(1)", "139(9)", "142(1)", "148".</summary>
    public string NoticeType { get; set; } = string.Empty;
    public string? Section { get; set; }
    public string? Din { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly? DueDate { get; set; }

    public string? Summary { get; set; }
    public decimal? DemandAmount { get; set; }
    public decimal? RefundAmount { get; set; }

    public NoticeStatus Status { get; set; } = NoticeStatus.Open;
    public string? FilePath { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<NoticeResponse> Responses { get; set; } = new List<NoticeResponse>();
}
