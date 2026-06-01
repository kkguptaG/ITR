using Microsoft.Extensions.Logging;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// STUB: dev OTP "sender" that logs the code to the console instead of dispatching SMS/email.
/// Returns the code so the auth flow can surface it as devOtp in Development.
/// </summary>
public sealed class ConsoleOtpSender : IOtpSender
{
    private readonly ILogger<ConsoleOtpSender> _logger;

    public ConsoleOtpSender(ILogger<ConsoleOtpSender> logger) => _logger = logger;

    public Task<string> SendAsync(
        string destination, NotificationChannel channel, OtpPurpose purpose, string code, CancellationToken ct = default)
    {
        // STUB: no real provider. The code is logged for local testing.
        _logger.LogInformation(
            "[OTP STUB] channel={Channel} purpose={Purpose} to={Destination} code={Code}",
            channel, purpose, destination, code);
        return Task.FromResult(code);
    }
}
