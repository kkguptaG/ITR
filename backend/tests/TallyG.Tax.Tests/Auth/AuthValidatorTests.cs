using FluentAssertions;
using TallyG.Tax.Api.Modules.Auth;
using Xunit;

namespace TallyG.Tax.Tests.Auth;

/// <summary>
/// Guards the OTP-request validator against the null-dereference regression where a
/// missing <c>Purpose</c> / <c>Identifier</c> made the <c>.Must(...)</c> predicate run
/// against a null string — throwing a NullReferenceException (HTTP 500) instead of
/// surfacing a clean validation failure (HTTP 422). The fix adds
/// <c>Cascade(CascadeMode.Stop)</c> so a failed <c>NotEmpty()</c> short-circuits the
/// chain. These tests fail (by throwing) if that cascade is ever removed.
/// </summary>
public class AuthValidatorTests
{
    private readonly OtpRequestRequestValidator _validator = new();

    [Fact]
    public void Missing_purpose_fails_validation_without_throwing()
    {
        var result = _validator.Validate(new OtpRequestRequest("demo@itrhelp.com", null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OtpRequestRequest.Purpose));
    }

    [Fact]
    public void Missing_identifier_fails_validation_without_throwing()
    {
        var result = _validator.Validate(new OtpRequestRequest(null!, "login"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OtpRequestRequest.Identifier));
    }

    [Theory]
    [InlineData("demo@itrhelp.com", "login")]
    [InlineData("demo@itrhelp.com", "signup")]
    [InlineData("9876543210", "reset")]
    public void Valid_identifier_and_purpose_pass(string identifier, string purpose)
    {
        _validator.Validate(new OtpRequestRequest(identifier, purpose)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Unknown_purpose_is_rejected()
    {
        _validator.Validate(new OtpRequestRequest("demo@itrhelp.com", "delete-everything"))
            .IsValid.Should().BeFalse();
    }
}
