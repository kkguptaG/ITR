using FluentAssertions;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests;

/// <summary>
/// Guards the enum surfaces the Support + Notices modules (Notices/Tickets/Notifications/Consent)
/// depend on. The module services parse/emit these names as strings on the wire, so an accidental
/// rename here would silently break the API contract — these assertions fail the build instead.
/// (The test project references Domain only, per the backend contract, so we verify the contract
/// at the enum level rather than spinning up the Api service graph.)
/// </summary>
public class SupportEnumContractTests
{
    [Fact]
    public void NoticeStatus_names_match_the_api_status_vocabulary()
    {
        Enum.GetNames<NoticeStatus>()
            .Should().BeEquivalentTo("Open", "InProgress", "Responded", "Closed", "Escalated");
    }

    [Fact]
    public void TicketStatus_names_match_the_api_status_vocabulary()
    {
        Enum.GetNames<TicketStatus>()
            .Should().BeEquivalentTo("Open", "Pending", "Resolved", "Closed");
    }

    [Fact]
    public void NotificationChannel_includes_inapp_and_the_outbound_stub_channels()
    {
        Enum.IsDefined(NotificationChannel.InApp).Should().BeTrue();
        Enum.IsDefined(NotificationChannel.Email).Should().BeTrue();
        Enum.IsDefined(NotificationChannel.Sms).Should().BeTrue();
        Enum.IsDefined(NotificationChannel.WhatsApp).Should().BeTrue();
    }

    [Fact]
    public void NotificationStatus_covers_the_inapp_lifecycle_used_by_the_service()
    {
        // NotificationService transitions Queued -> Sent (on persist/dispatch), -> Read (on mark-read),
        // and -> Failed (on a sender error). All four must exist.
        Enum.IsDefined(NotificationStatus.Queued).Should().BeTrue();
        Enum.IsDefined(NotificationStatus.Sent).Should().BeTrue();
        Enum.IsDefined(NotificationStatus.Read).Should().BeTrue();
        Enum.IsDefined(NotificationStatus.Failed).Should().BeTrue();
    }
}
