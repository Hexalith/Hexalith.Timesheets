using System.Text.Json;

using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

using MagicLinkCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class MagicLinkCapabilityStateTests
{
    [Fact]
    public void IllegalLifecycleEventsLeaveEveryFieldUnchanged()
    {
        object[] terminals = [Used(), Revoked(), Expired()];
        object[][] prefixes = [[], [Issued()], [Issued(), Used()], [Issued(), Revoked()], [Issued(), Expired()]];
        foreach (object[] prefix in prefixes)
        {
            foreach (object next in new object[] { Issued() }.Concat(terminals))
            {
                bool allowed = prefix.Length == 0 ? next is MagicLinkConfirmationCapabilityIssued
                    : prefix.Length == 1 && next is not MagicLinkConfirmationCapabilityIssued;
                if (allowed) continue;
                var state = new MagicLinkCapabilityState();
                foreach (object item in prefix) Apply(state, item);
                string before = JsonSerializer.Serialize(state);

                Should.Throw<InvalidOperationException>(() => Apply(state, next));

                JsonSerializer.Serialize(state).ShouldBe(before);
            }
        }
    }

    [Fact]
    public void WrappedValidHistoryAndRejectedCommandsPreserveTerminalResult()
    {
        foreach (object terminal in new object[] { new StoredMagicLinkUsed(Used()),
            new StoredMagicLinkRevoked(Revoked()), new StoredMagicLinkExpired(Expired()) })
        {
            var state = new MagicLinkCapabilityState();
            state.Apply(new MagicLinkCommitRejected());
            state.Exists.ShouldBeFalse();
            state.Apply(new StoredMagicLinkIssued(Issued()));
            Apply(state, terminal);
            string before = JsonSerializer.Serialize(state);
            state.Apply(new MagicLinkCommitRejected());
            JsonSerializer.Serialize(state).ShouldBe(before);
            state.Exists.ShouldBeTrue();
            state.IsTerminal.ShouldBeTrue();
            Should.Throw<InvalidOperationException>(() => state.Apply(new StoredMagicLinkIssued(Issued())));
            JsonSerializer.Serialize(state).ShouldBe(before);
            if (terminal is StoredMagicLinkUsed)
            {
                state.UsedAtUtc.ShouldBe(Used().UsedAtUtc);
                state.UseMetadata.ShouldBe(Used().Source);
            }
        }
    }

    private static void Apply(MagicLinkCapabilityState state, object item)
    {
        switch (item)
        {
            case MagicLinkConfirmationCapabilityIssued value: state.Apply(value); break;
            case MagicLinkConfirmationCapabilityUsed value: state.Apply(value); break;
            case MagicLinkConfirmationCapabilityRevoked value: state.Apply(value); break;
            case MagicLinkConfirmationCapabilityExpired value: state.Apply(value); break;
            case StoredMagicLinkUsed value: state.Apply(value); break;
            case StoredMagicLinkRevoked value: state.Apply(value); break;
            case StoredMagicLinkExpired value: state.Apply(value); break;
            default: throw new InvalidOperationException();
        }
    }

    private static MagicLinkConfirmationCapabilityIssued Issued() => new(
        new("capability-1"), new("tenant-1"), new("contributor-1"),
        TimeEntryTargetReference.ForProject(new ProjectReference("project-1")), new("activity-1"), new("entry-1"),
        MagicLinkTargetKind.ExistingTimeEntry, MagicLinkAllowedAction.ConfirmOrAdjust, new("hash-1"),
        DateTimeOffset.UnixEpoch.AddDays(2), new("operator-1"), DateTimeOffset.UnixEpoch,
        new MagicLinkAuditMetadata("timesheets", "issue-1"), true);

    private static MagicLinkConfirmationCapabilityUsed Used() => new(new("capability-1"), new("tenant-1"),
        new("contributor-1"), new("entry-1"), DateTimeOffset.UnixEpoch.AddHours(1), new("magic-link", "use-1"));

    private static MagicLinkConfirmationCapabilityRevoked Revoked() => new(new("capability-1"), new("tenant-1"),
        new("operator-1"), DateTimeOffset.UnixEpoch.AddHours(1), new("timesheets", "revoke-1"));

    private static MagicLinkConfirmationCapabilityExpired Expired() => new(new("capability-1"), new("tenant-1"),
        DateTimeOffset.UnixEpoch.AddDays(2), new("timesheets", "expire-1"));
}
