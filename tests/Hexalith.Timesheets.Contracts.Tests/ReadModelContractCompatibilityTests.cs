using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Timesheets.Contracts.Models.ActivityTypes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.Runtime;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

/// <summary>Locks persisted identities and old Server assembly names across shared ownership moves.</summary>
public sealed class ReadModelContractCompatibilityTests
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Preserves every physical catalog address and the projection/slot identity.</summary>
    [Fact]
    public void CanonicalCatalogAddressMatchesLegacyFacadeAndPhysicalKey()
    {
        var tenant = new TenantReference("tenant-compatibility");
        TenantActivityTypeCatalogReadModelAddress.StateKey(tenant)
            .ShouldBe("timesheets:magic-links:activity-type-catalog:tenant-compatibility:v1");
        MagicLinkActivityTypeCatalogReadModelAddress.StateKey(tenant)
            .ShouldBe(TenantActivityTypeCatalogReadModelAddress.StateKey(tenant));
        MagicLinkActivityTypeCatalogReadModelAddress.StateStoreName
            .ShouldBe(TenantActivityTypeCatalogReadModelAddress.StateStoreName);
        MagicLinkActivityTypeCatalogReadModelAddress.SlotName
            .ShouldBe(TenantActivityTypeCatalogReadModelAddress.SlotName);
        TenantActivityTypeCatalogReadModelAddress.ProjectionName.ShouldBe("tenant-activity-type-catalog");
        TenantActivityTypeCatalogReadModelAddress.StateStoreName.ShouldBe("timesheets-readmodels");
        TenantActivityTypeCatalogReadModelAddress.SlotName.ShouldBe("catalog");
        Should.Throw<ArgumentNullException>(() => TenantActivityTypeCatalogReadModelAddress.StateKey(null!));
        Should.Throw<ArgumentNullException>(() => MagicLinkActivityTypeCatalogReadModelAddress.StateKey(null!));
        TimesheetsEventStoreIntegration.DomainName.ShouldBe(TimesheetsDomain.Name);
        TimesheetsDomain.Name.ShouldBe("timesheets");
        TimeEntryCommentPublicationIssuer.CurrentVersion.ShouldBe(TimesheetsDomain.CommentPublicationVersion);
        TimesheetsDomain.CommentPublicationVersion.ShouldBe(2);
        TimesheetsEvidencePolicyOptions.FailClosedDefault.ProjectionCommentsAllowed.ShouldBeFalse();
    }

    /// <summary>Resolves all historical Server-qualified CLR names to their new owning assemblies.</summary>
    [Fact]
    public void ServerForwardsAllRelocatedPublicTypes()
    {
        Assembly server = typeof(TimesheetsServerMarker).Assembly;
        string[] pureNames = [
            "DirectoryEntry", "DirectoryPage", "DirectoryReference", "Entry", "NodeReference", "OwnerAdmission",
            "Page", "ReadModel", "TenantRoster", "TenantRosterPage", "TenantRosterPageReference", "TreeFormat",
            "TreeNode", "TreeRoot"
        ];
        string[] sdkNames = ["Projection", "TreeBuilder", "TreeReader"];
        string[] payloadNames = [
            "StoredMagicLinkExpired", "StoredMagicLinkIssued", "StoredMagicLinkRevoked", "StoredMagicLinkUsed",
            "StoredTimeEntryAdjusted", "StoredTimeEntryConfirmed", "StoredTimeEntryRecorded", "StoredTimeEntrySubmitted"
        ];
        var expected = pureNames.Select(name => (Name: "MagicLinkTokenHashCapabilityIndex" + name,
                Owner: "Hexalith.Timesheets.Contracts"))
            .Concat(sdkNames.Select(name => (Name: "MagicLinkTokenHashCapabilityIndex" + name,
                Owner: "Hexalith.Timesheets.ReadModels")))
            .Concat(payloadNames.Select(name => (Name: name, Owner: "Hexalith.Timesheets.ReadModels")));
        foreach ((string name, string owner) in expected)
        {
            string fullName = "Hexalith.Timesheets.Server.MagicLinks." + name;
            Type resolved = Type.GetType(fullName + ", " + server.FullName, throwOnError: true)!;
            resolved.FullName.ShouldBe(fullName);
            resolved.Assembly.GetName().Name.ShouldBe(owner);
            server.GetForwardedTypes().ShouldContain(resolved);
            if (name.StartsWith("Stored", StringComparison.Ordinal))
            {
                typeof(IEventPayload).IsAssignableFrom(resolved).ShouldBeTrue();
                object wrapper = Activator.CreateInstance(resolved, [null])!;
                JsonSerializer.Serialize(wrapper, resolved, s_json).ShouldBe("{\"event\":null}");
            }
        }
        Type policy = Type.GetType("Hexalith.Timesheets.Server.Policies.TimesheetsEvidencePolicyOptions, "
            + server.FullName, throwOnError: true)!;
        policy.ShouldBe(typeof(TimesheetsEvidencePolicyOptions));
        policy.Assembly.GetName().Name.ShouldBe("Hexalith.Timesheets.Contracts");
        server.GetForwardedTypes().Length.ShouldBe(26);
    }

    /// <summary>Round-trips historical JSON fields without renaming persisted shared shapes.</summary>
    /// <param name="type">The historical shared shape.</param>
    /// <param name="json">The persisted JSON fixture.</param>
    [Theory]
    [MemberData(nameof(PersistedShapes))]
    public void PersistedIndexJsonFieldsRemainCompatible(Type type, string json)
    {
        object value = JsonSerializer.Deserialize(json, type, s_json).ShouldNotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(json),
            JsonNode.Parse(JsonSerializer.Serialize(value, type, s_json))).ShouldBeTrue(type.FullName);
    }

    /// <summary>Gets complete historical JSON examples for each relocated pure index shape.</summary>
    public static TheoryData<Type, string> PersistedShapes => new()
    {
        { typeof(MagicLinkTokenHashCapabilityIndexEntry), """{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"owner-1"}}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexDirectoryEntry), """{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"owner-1"}}}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexDirectoryPage), """{"entries":[]}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexDirectoryReference), """{"pageId":3,"firstHash":"a","lastHash":"z"}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexNodeReference), """{"generation":2,"nodeId":3,"digest":"ABC"}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexOwnerAdmission), """{"tokenHash":"hash","candidate":{"tenant":{"tenantId":"tenant-1"},"capabilityId":{"value":"owner-1"}}}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexPage), """{"tokenHash":"hash","candidates":[]}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexReadModel), """{"entries":{},"storageVersion":6,"directoryPages":[],"contributions":{},"legacyContributionsUnverified":false,"candidatePageCounts":{},"candidatePageIds":{},"candidatePageDigests":{},"directoryPageDigests":{},"nextCandidatePageId":0}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexTenantRoster), """{"tenantId":"tenant-1","contributionCount":0,"pages":[]}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexTenantRosterPage), """{"rows":[]}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexTenantRosterPageReference), """{"page":{"generation":2,"nodeId":3,"digest":"ABC"},"firstPathPrefix":"owner"}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexTreeFormat), """{"storageVersion":7}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexTreeNode), """{"prefix":"owner","children":{},"row":null,"roster":null}""" },
        { typeof(MagicLinkTokenHashCapabilityIndexTreeRoot), """{"storageVersion":7,"nextNodeId":0,"tokenRoot":null,"tenantRoot":null,"ownerRoot":null}""" }
    };
}
