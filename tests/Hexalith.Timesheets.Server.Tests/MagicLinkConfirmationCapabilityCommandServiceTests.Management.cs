using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed partial class MagicLinkConfirmationCapabilityCommandServiceTests
{
    /// <summary>Rejects incomplete issuance at both aggregate and command-service boundaries.</summary>
    /// <param name="field">The required field to omit.</param>
    [Theory]
    [InlineData("capabilityId")]
    [InlineData("contributor")]
    [InlineData("timeEntryId")]
    [InlineData("source")]
    public async Task IssueRejectsMissingRequiredIdentityOrAuditFields(string field)
    {
        IssueMagicLinkConfirmationCapability command = IssueCommand();
        command = field switch
        {
            "capabilityId" => command with { CapabilityId = null! },
            "contributor" => command with { Scope = command.Scope with { Contributor = null! } },
            "timeEntryId" => command with { Scope = command.Scope with { TimeEntryId = null! } },
            "source" => command with { Source = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        TimesheetsDomainResult aggregate = MagicLinkConfirmationCapability.HandleIssue(
            command, null, Context().Tenant, Context().Actor, new("hash-only"), IssuedAtUtc());
        aggregate.IsRejection.ShouldBeTrue();
        TimesheetsRejection rejection = aggregate.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
        rejection.FieldErrors.ShouldContain(error => error.Field == field && error.Code == "required");

        Fixture fixture = AuthorizedProjectFixture();
        MagicLinkCapabilityCommandResult service = await fixture.CreateService().IssueAsync(
            Context(), command, null, FreshCatalog(), IssuedAtUtc(), TestContext.Current.CancellationToken);
        service.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        service.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
        service.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
    }

    /// <summary>Checks expiry cannot return a state-changing result after authorization refusal.</summary>
    [Fact]
    public async Task ExpiryDenialProducesNoDomainEventsEvenWhenCapabilityHasExpired()
    {
        Fixture fixture = new();
        fixture.TenantValidator.ValidateAsync(Arg.Any<TimesheetsRequestContext>(),
            TimesheetsOperation.MagicLinkIssuance, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(TimesheetsTenantAccessState.InsufficientRole, "denied"));

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().ExpireAsync(
            Context(), ExpireCommand(), IssuedState(), ExpiresAtUtc(), TestContext.Current.CancellationToken);

        result.Authorization.IsAuthorized.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.WasDispatched.ShouldBeFalse();
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Preserves a fail-closed compatibility path for legacy synchronous callers.</summary>
    [Fact]
    public void LegacySynchronousExpiryFailsClosedWithoutIssuingExpiryEvent()
    {
#pragma warning disable CS0618 // Deliberately verify the retained legacy entry point fails closed.
        TimesheetsDomainResult result = AuthorizedProjectFixture().CreateService().Expire(
            ExpireCommand(), IssuedState(), Context(), ExpiresAtUtc());
#pragma warning restore CS0618

        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }
}
