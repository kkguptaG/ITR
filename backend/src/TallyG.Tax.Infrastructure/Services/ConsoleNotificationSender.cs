using Microsoft.Extensions.Logging;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>STUB: dev notification sender that logs to the console (no SMS/email/WhatsApp).</summary>
public sealed class ConsoleNotificationSender : INotificationSender
{
    private readonly ILogger<ConsoleNotificationSender> _logger;

    public ConsoleNotificationSender(ILogger<ConsoleNotificationSender> logger) => _logger = logger;

    public Task SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        // STUB: no real provider.
        _logger.LogInformation(
            "[NOTIFY STUB] channel={Channel} to={Destination} template={Template} subject={Subject} body={Body}",
            message.Channel, message.Destination, message.TemplateCode, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
