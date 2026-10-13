using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryCommentPublicationIssuerTests
{
    [Theory]
    [InlineData(TimesheetsCommentPolicyDecision.Allowed)]
    [InlineData(TimesheetsCommentPolicyDecision.Excluded)]
    public void DefaultAuthorityOverwritesCallerPublicationDecision(
        TimesheetsCommentPolicyDecision callerDecision)
    {
        TimeEntryComment submitted = new("private", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = callerDecision,
            ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed,
            ServerPublicationVersion = 1,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ServerExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
        });
        TimeEntryComment issued = new TimeEntryCommentPublicationIssuer().Issue(Context(), submitted)
            .ShouldNotBeNull();

        issued.Policy.ServerPublicationVersion.ShouldBe(TimeEntryCommentPublicationIssuer.CurrentVersion);
        issued.Policy.ServerProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        issued.Policy.ProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        issued.Policy.ExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        issued.Policy.ServerExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        issued.Policy.RedactionRequirement.ShouldBe(
            TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure);
    }

    [Theory]
    [InlineData(TimesheetsCommentPolicyDecision.Allowed)]
    [InlineData(TimesheetsCommentPolicyDecision.Excluded)]
    public void ServerAuthorityCanAllowIndependentlyOfCallerDecision(
        TimesheetsCommentPolicyDecision callerDecision)
    {
        TimeEntryComment submitted = new("published", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = callerDecision
        });
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        TimeEntryComment issued = new TimeEntryCommentPublicationIssuer(authority)
            .Issue(Context(), submitted).ShouldNotBeNull();

        issued.Policy.ServerProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        issued.Policy.ProjectionInclusion.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
    }

    [Fact]
    public void CallerClearanceCannotPermitExternalDisplay()
    {
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayPublish(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        TimeEntryComment submitted = new("permitted", TimeEntryCommentPolicy.SensitiveDefault with
        {
            RedactionRequirement = TimesheetsCommentRedactionRequirement.NotRequired
        });

        TimeEntryComment issued = new TimeEntryCommentPublicationIssuer(authority)
            .Issue(Context(), submitted).ShouldNotBeNull();

        issued.Policy.ExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        issued.Policy.ServerExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        issued.Policy.RedactionRequirement.ShouldBe(
            TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure);
    }

    [Theory]
    [InlineData(TimesheetsCommentRedactionRequirement.NotRequired)]
    [InlineData(TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure)]
    public void SeparateServerClearancePermitsUnredactedExternalDisplay(
        TimesheetsCommentRedactionRequirement callerRequirement)
    {
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        authority.MayDisplayUnredactedExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        TimeEntryComment submitted = new("permitted", TimeEntryCommentPolicy.SensitiveDefault with
        {
            RedactionRequirement = callerRequirement
        });

        TimeEntryComment issued = new TimeEntryCommentPublicationIssuer(authority)
            .Issue(Context(), submitted).ShouldNotBeNull();

        issued.Policy.ExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        issued.Policy.ServerExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Allowed);
        issued.Policy.RedactionRequirement.ShouldBe(TimesheetsCommentRedactionRequirement.NotRequired);
        authority.Received(1).MayDisplayUnredactedExternally(Arg.Any<TimesheetsRequestContext>(),
            Arg.Is<TimeEntryComment>(comment => comment.Policy.RedactionRequirement
                == TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure));
    }

    [Fact]
    public void ExternalDisplayAuthorityCannotClearRequiredRedaction()
    {
        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        TimeEntryComment submitted = new("private", TimeEntryCommentPolicy.SensitiveDefault);

        TimeEntryComment issued = new TimeEntryCommentPublicationIssuer(authority)
            .Issue(Context(), submitted).ShouldNotBeNull();

        issued.Policy.RedactionRequirement.ShouldBe(
            TimesheetsCommentRedactionRequirement.RequiredBeforeExternalDisclosure);
        issued.Policy.ServerExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
    }

    private static TimesheetsRequestContext Context() => new(
        new TenantReference("tenant-1"), new PartyReference("operator-1"), "request-1");

}
