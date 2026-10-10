using System.Reflection;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Runtime;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class MagicLinkEventStoreDomainProcessorTests
{
    [Theory]
    [InlineData(nameof(CommitMagicLinkIssue))]
    [InlineData(nameof(CommitMagicLinkUse))]
    [InlineData(nameof(CommitMagicLinkTransition))]
    public void Verified_origin_guard_accepts_only_the_gateway_stamped_timesheets_origin(string commandName)
    {
        CommandEnvelope valid = Envelope(commandName);

        HasVerifiedOrigin(valid).ShouldBeTrue();
        HasVerifiedOrigin(valid with { Extensions = null }).ShouldBeFalse();
        HasVerifiedOrigin(valid with { Extensions = new Dictionary<string, string>
        {
            [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "other-domain"
        } }).ShouldBeFalse();
    }

    [Theory]
    [InlineData(nameof(CommitMagicLinkIssue))]
    [InlineData(nameof(CommitMagicLinkTransition))]
    public void Management_actor_guard_requires_the_verified_actor_to_match_the_envelope(string commandName)
    {
        CommandEnvelope valid = Envelope(commandName);

        HasVerifiedActor(valid, "actor-1").ShouldBeTrue();
        HasVerifiedActor(valid with { UserId = "actor-2" }, "actor-1").ShouldBeFalse();
        HasVerifiedActor(valid with { Extensions = new Dictionary<string, string>
        {
            [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
            [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "actor-2"
        } }, "actor-1").ShouldBeFalse();
        HasVerifiedActor(valid, " ").ShouldBeFalse();
    }

    private static CommandEnvelope Envelope(string commandName)
        => new("message-1", "tenant-1", "timesheets", "owner-1", commandName, [],
            "correlation-1", null, "actor-1", new Dictionary<string, string>
            {
                [EventStoreGatewayVerifiedOrigin.ExtensionKey] = "timesheets",
                [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "actor-1"
            });

    private static bool HasVerifiedOrigin(CommandEnvelope envelope)
        => (bool)typeof(MagicLinkEventStoreDomainProcessor)
            .GetMethod("HasVerifiedWorkloadOrigin", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [envelope])!;

    private static bool HasVerifiedActor(CommandEnvelope envelope, string actor)
        => (bool)typeof(MagicLinkEventStoreDomainProcessor)
            .GetMethod("HasVerifiedActor", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [envelope, actor])!;
}
