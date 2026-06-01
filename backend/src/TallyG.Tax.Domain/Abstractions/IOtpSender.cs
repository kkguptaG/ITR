using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Delivers a one-time passcode over SMS/email/WhatsApp.
/// The dev implementation logs the code to the console and echoes it back so login
/// works without a real SMS provider (the API surfaces it as "devOtp" in Development).
/// </summary>
public interface IOtpSender
{
    /// <summary>
    /// Send the plaintext <paramref name="code"/> to <paramref name="destination"/>.
    /// Returns the code that was "sent" (so dev flows can surface it).
    /// </summary>
    Task<string> SendAsync(
        string destination,
        NotificationChannel channel,
        OtpPurpose purpose,
        string code,
        CancellationToken ct = default);
}
