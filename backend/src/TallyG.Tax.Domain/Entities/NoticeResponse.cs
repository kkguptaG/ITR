using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>A response to an ITD notice (Ch.2 §2.8).</summary>
public class NoticeResponse : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid NoticeId { get; set; }

    public string ResponseText { get; set; } = string.Empty;

    /// <summary>agree | disagree | rectification | revised_return.</summary>
    public string? ResponseType { get; set; }

    public string? FilePath { get; set; }
    public Guid? RespondedByUserId { get; set; }
    public string? AcknowledgementNo { get; set; }
    public DateTimeOffset? SubmittedToItdAt { get; set; }

    public Notice? Notice { get; set; }
}
