using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Sends templated transactional notifications (filing status, payment receipts).
/// The dev implementation logs to the console.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}

/// <summary>A single notification to deliver on one channel.</summary>
public sealed record NotificationMessage(
    NotificationChannel Channel,
    string Destination,
    string TemplateCode,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);
