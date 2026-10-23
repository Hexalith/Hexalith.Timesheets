using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class MagicLinkContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void IssueMagicLinkCommandRoundTripsWithoutAuthorityOrTokenFields()
    {
        IssueMagicLinkConfirmationCapability command = IssueCommand();

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"capabilityId\"");
        json.ShouldContain("\"allowedAction\":\"Confirm\"");
        json.ShouldNotContain("\"tenant\"");
        json.ShouldNotContain("\"actor\"");
        json.ShouldNotContain("\"issuer\"");
        json.ShouldNotContain("\"correlationId\"");
        json.ShouldNotContain("\"messageId\"");
        json.ShouldNotContain("\"token\"");
        json.ShouldNotContain("\"tokenHash\"");
        json.ShouldNotContain("\"comment\"");

        IssueMagicLinkConfirmationCapability? roundTripped =
            JsonSerializer.Deserialize<IssueMagicLinkConfirmationCapability>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.CapabilityId.ShouldBe(new MagicLinkCapabilityId("capability-1"));
        roundTripped.Scope.Contributor.ShouldBe(new PartyReference("party-1"));
        roundTripped.AllowedAction.ShouldBe(MagicLinkAllowedAction.Confirm);
    }

    [Fact]
    public void IssuedEventStoresHashAndSafeMetadataWithoutRawTokenMaterial()
    {
        MagicLinkConfirmationCapabilityIssued issued = new(
            new MagicLinkCapabilityId("capability-1"),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-1"),
            new TimeEntryId("entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("sha256-hash"),
            ExpiresAtUtc(),
            new PartyReference("issuer-1"),
            IssuedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true);

        string json = JsonSerializer.Serialize(issued, JsonOptions);

        json.ShouldContain("\"tokenHash\"");
        json.ShouldContain("\"isSingleUse\":true");
        json.ShouldNotContain("\"oneTimeToken\"");
        json.ShouldNotContain("\"rawToken\"");
        json.ShouldNotContain("\"bearer\"");
        json.ShouldNotContain("\"decoded\"");
        json.ShouldNotContain("\"comment\"");

        MagicLinkConfirmationCapabilityIssued? roundTripped =
            JsonSerializer.Deserialize<MagicLinkConfirmationCapabilityIssued>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TokenHash.ShouldBe(new MagicLinkTokenHash("sha256-hash"));
        roundTripped.Source.ShouldBe(new MagicLinkAuditMetadata("timesheets", "issue-1"));
    }

    [Fact]
    public void ConfirmMagicLinkCommandAndUsedEventRoundTripWithoutRawTokenMaterial()
    {
        ConfirmTimeThroughMagicLink command = new();
        MagicLinkConfirmationCapabilityUsed used = new(
            new MagicLinkCapabilityId("capability-1"),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            new TimeEntryId("entry-1"),
            IssuedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1"));

        string commandJson = JsonSerializer.Serialize(command, JsonOptions);
        string eventJson = JsonSerializer.Serialize(used, JsonOptions);

        // The command body carries no caller-supplied audit/source, token, tenant, or actor material.
        commandJson.ShouldNotContain("source", Case.Insensitive);
        commandJson.ShouldNotContain("token", Case.Insensitive);
        commandJson.ShouldNotContain("tenant", Case.Insensitive);
        commandJson.ShouldNotContain("actor", Case.Insensitive);
        eventJson.ShouldContain("\"usedAtUtc\"");
        eventJson.ShouldNotContain("oneTimeToken");
        eventJson.ShouldNotContain("rawToken");
        eventJson.ShouldNotContain("tokenHash");

        JsonSerializer.Deserialize<ConfirmTimeThroughMagicLink>(commandJson, JsonOptions).ShouldNotBeNull();
        JsonSerializer.Deserialize<MagicLinkConfirmationCapabilityUsed>(eventJson, JsonOptions)
            .ShouldNotBeNull()
            .Source.ShouldBe(new MagicLinkAuditMetadata("magic-link", "capability-1"));
    }

    [Fact]
    public void AdjustMagicLinkCommandRoundTripsWithOnlyEditableFields()
    {
        AdjustTimeThroughMagicLink command = new(
            new DateOnly(2026, 6, 20),
            75,
            new ActivityTypeId("activity-1"),
            BillableState.NonBillable);

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"serviceDate\"");
        json.ShouldContain("\"durationMinutes\":75");
        json.ShouldContain("\"activityTypeId\"");
        json.ShouldContain("\"billableState\":\"NonBillable\"");
        json.ShouldNotContain("tenant", Case.Insensitive);
        json.ShouldNotContain("contributor", Case.Insensitive);
        json.ShouldNotContain("timeEntry", Case.Insensitive);
        json.ShouldNotContain("target", Case.Insensitive);
        json.ShouldNotContain("approval", Case.Insensitive);
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("hash", Case.Insensitive);
        json.ShouldNotContain("source", Case.Insensitive);

        JsonSerializer.Deserialize<AdjustTimeThroughMagicLink>(json, JsonOptions)
            .ShouldNotBeNull()
            .DurationMinutes.ShouldBe(75);
    }

    [Fact]
    public void DisplayResponseShapeContainsOnlySafeConfirmationDetails()
    {
        MagicLinkConfirmationDisplayResponse response = new(
            new DateOnly(2026, 6, 19),
            60,
            "minutes",
            new ActivityTypeId("activity-1"),
            "Delivery",
            BillableState.Billable,
            "Project");

        string json = JsonSerializer.Serialize(response, JsonOptions);

        json.ShouldContain("\"proposedDate\"");
        json.ShouldContain("\"durationUnit\":\"minutes\"");
        json.ShouldContain("\"billableState\":\"Billable\"");
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("hash", Case.Insensitive);
        json.ShouldNotContain("tenant", Case.Insensitive);
        json.ShouldNotContain("party", Case.Insensitive);
        json.ShouldNotContain("approval", Case.Insensitive);
    }

    [Fact]
    public void AdjustmentDisplayResponseMarksEditableAndReadonlyFieldsWithoutAuthorityMaterial()
    {
        MagicLinkAdjustmentDisplayResponse response = new(
            new DateOnly(2026, 6, 19),
            60,
            "minutes",
            new ActivityTypeId("activity-1"),
            "Delivery",
            BillableState.Billable,
            "Project",
            ["serviceDate", "durationMinutes", "activityTypeId", "billableState", "comment"],
            ["target", "contributor", "tenant", "timeEntryId", "approvalState"]);

        string json = JsonSerializer.Serialize(response, JsonOptions);

        json.ShouldContain("\"editableFields\"");
        json.ShouldContain("\"readOnlyFields\"");
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("hash", Case.Insensitive);
        json.ShouldNotContain("raw", Case.Insensitive);
    }

    [Fact]
    public void ReadModelAndMetadataAreFreeOfRawTokenFields()
    {
        MagicLinkConfirmationCapabilityReadModel readModel = new(
            new MagicLinkCapabilityId("capability-1"),
            new TenantReference("tenant-1"),
            new PartyReference("party-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new ActivityTypeId("activity-1"),
            new TimeEntryId("entry-1"),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.Confirm,
            MagicLinkCapabilityState.Issued,
            MagicLinkExpiryState.Active,
            ExpiresAtUtc(),
            new PartyReference("issuer-1"),
            IssuedAtUtc(),
            ProjectionFreshnessMetadata.Fresh)
        {
            IssueMetadata = new MagicLinkAuditMetadata("timesheets", "issue-1"),
            StateBadgeText = "Issued",
            ExpiryBadgeText = "Active"
        };

        string readModelJson = JsonSerializer.Serialize(readModel, JsonOptions);
        string metadataJson = JsonSerializer.Serialize(TimesheetsMetadataCatalog.Descriptors, JsonOptions);

        readModelJson.ShouldNotContain("token", Case.Insensitive);
        readModelJson.ShouldNotContain("decoded", Case.Insensitive);
        readModelJson.ShouldNotContain("comment", Case.Insensitive);
        metadataJson.ShouldContain("timesheets.projection.magic-link-confirmation-capabilities");
        metadataJson.ShouldContain("State status");
        metadataJson.ShouldContain("Expiry status");
        metadataJson.ShouldNotContain("rawToken", Case.Insensitive);
        metadataJson.ShouldNotContain("tokenHash", Case.Insensitive);
    }

    [Fact]
    public void InvalidLinkDenialContractHasOneSafeRecoveryPathWithoutAuthorityMaterial()
    {
        MagicLinkInvalidLinkDenial denial = MagicLinkInvalidLinkDenial.Default;

        string json = JsonSerializer.Serialize(denial, JsonOptions);

        denial.Title.ShouldBe("This link is expired or unavailable.");
        denial.Detail.ShouldBe("Request a new confirmation link from the sender.");
        denial.RecoveryPath.ShouldBe(denial.Detail);
        json.ShouldNotContain("reason", Case.Insensitive);
        json.ShouldNotContain("tenant", Case.Insensitive);
        json.ShouldNotContain("project", Case.Insensitive);
        json.ShouldNotContain("work", Case.Insensitive);
        json.ShouldNotContain("party", Case.Insensitive);
        json.ShouldNotContain("timeEntry", Case.Insensitive);
        json.ShouldNotContain("duration", Case.Insensitive);
        json.ShouldNotContain("comment", Case.Insensitive);
        json.ShouldNotContain("activityType", Case.Insensitive);
        json.ShouldNotContain("approval", Case.Insensitive);
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("hash", Case.Insensitive);
    }

    [Fact]
    public void OpenapiDeclaresMagicLinkSchemasAdditivelyWithoutAuthorityBodyFields()
    {
        JsonObject schemas = LoadSchemas();

        schemas.ContainsKey("IssueMagicLinkConfirmationCapability").ShouldBeTrue();
        schemas.ContainsKey("RevokeMagicLinkConfirmationCapability").ShouldBeTrue();
        schemas.ContainsKey("ExpireMagicLinkConfirmationCapability").ShouldBeTrue();
        schemas.ContainsKey("ConfirmTimeThroughMagicLink").ShouldBeTrue();
        schemas.ContainsKey("AdjustTimeThroughMagicLink").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkConfirmationCapabilityIssued").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkConfirmationCapabilityUsed").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkConfirmationCapabilityReadModel").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkConfirmationDisplayResponse").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkAdjustmentDisplayResponse").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkIssueResponse").ShouldBeTrue();
        schemas.ContainsKey("MagicLinkInvalidLinkDenial").ShouldBeTrue();

        JsonObject issueProperties = schemas["IssueMagicLinkConfirmationCapability"].ShouldNotBeNull()["properties"]?.AsObject()
            ?? throw new InvalidOperationException("IssueMagicLinkConfirmationCapability properties are missing.");
        issueProperties.ContainsKey("tenant").ShouldBeFalse();
        issueProperties.ContainsKey("actor").ShouldBeFalse();
        issueProperties.ContainsKey("issuer").ShouldBeFalse();
        issueProperties.ContainsKey("token").ShouldBeFalse();
        issueProperties.ContainsKey("tokenHash").ShouldBeFalse();

        string readModelSchema = schemas["MagicLinkConfirmationCapabilityReadModel"].ShouldNotBeNull().ToJsonString();
        readModelSchema.ShouldNotContain("oneTimeToken");
        readModelSchema.ShouldNotContain("tokenHash");
        readModelSchema.ShouldNotContain("rawToken");

        string confirmSchema = schemas["ConfirmTimeThroughMagicLink"].ShouldNotBeNull().ToJsonString();
        confirmSchema.ShouldNotContain("token", Case.Insensitive);
        confirmSchema.ShouldNotContain("tenant", Case.Insensitive);
        confirmSchema.ShouldNotContain("actor", Case.Insensitive);

        string adjustSchema = schemas["AdjustTimeThroughMagicLink"].ShouldNotBeNull().ToJsonString();
        adjustSchema.ShouldNotContain("tenant", Case.Insensitive);
        adjustSchema.ShouldNotContain("contributor", Case.Insensitive);
        adjustSchema.ShouldNotContain("target", Case.Insensitive);
        adjustSchema.ShouldNotContain("approval", Case.Insensitive);
        adjustSchema.ShouldNotContain("token", Case.Insensitive);

        string displaySchema = schemas["MagicLinkConfirmationDisplayResponse"].ShouldNotBeNull().ToJsonString();
        displaySchema.ShouldNotContain("token", Case.Insensitive);
        displaySchema.ShouldNotContain("tokenHash", Case.Insensitive);
        displaySchema.ShouldNotContain("tenant", Case.Insensitive);
        displaySchema.ShouldNotContain("party", Case.Insensitive);

        string responseSchema = schemas["MagicLinkIssueResponse"].ShouldNotBeNull().ToJsonString();
        responseSchema.ShouldContain("oneTimeToken");

        string invalidSchema = schemas["MagicLinkInvalidLinkDenial"].ShouldNotBeNull().ToJsonString();
        invalidSchema.ShouldContain("recoveryPath");
        invalidSchema.ShouldNotContain("token", Case.Insensitive);
        invalidSchema.ShouldNotContain("hash", Case.Insensitive);
        invalidSchema.ShouldNotContain("tenant", Case.Insensitive);
        invalidSchema.ShouldNotContain("party", Case.Insensitive);
    }

    private static IssueMagicLinkConfirmationCapability IssueCommand()
        => new(
            new MagicLinkCapabilityId("capability-1"),
            new MagicLinkConfirmationScope(
                new PartyReference("party-1"),
                TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
                new ActivityTypeId("activity-1"),
                new TimeEntryId("entry-1"),
                MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm,
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"));

    private static DateTimeOffset IssuedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ExpiresAtUtc() => new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private static JsonObject LoadSchemas()
    {
        JsonNode artifact = JsonNode.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json")))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");

        return artifact["components"]?["schemas"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI schemas are missing.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Timesheets.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
