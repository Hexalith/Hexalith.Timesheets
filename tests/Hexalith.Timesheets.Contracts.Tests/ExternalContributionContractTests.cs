using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class ExternalContributionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Submit_external_time_entry_round_trips_without_authority_or_approval_fields()
    {
        SubmitExternalTimeEntry command = new(
            new TimeEntryId("external-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-external-1"),
            new ActivityTypeId("activity-1"),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            new ExternalContributionSource("supplier-api", "request-1"));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"sourceSystem\":\"supplier-api\"");
        json.ShouldContain("\"externalRequestId\":\"request-1\"");
        json.ShouldNotContain("\"tenant\"");
        json.ShouldNotContain("\"actor\"");
        json.ShouldNotContain("\"approval\"");
        json.ShouldNotContain("\"messageId\"");
        json.ShouldNotContain("\"correlationId\"");
        json.ShouldNotContain("\"token\"");
        json.ShouldNotContain("\"payload\"");

        SubmitExternalTimeEntry? roundTripped = JsonSerializer.Deserialize<SubmitExternalTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.ShouldBe(new TimeEntryId("external-entry-1"));
        roundTripped.Contributor.ShouldBe(new PartyReference("party-external-1"));
        roundTripped.Source.ShouldBe(new ExternalContributionSource("supplier-api", "request-1"));
    }

    [Fact]
    public void Confirm_external_time_entry_round_trips_as_contributor_evidence_not_approval()
    {
        ConfirmExternalTimeEntry command = new(
            new TimeEntryId("external-entry-1"),
            new PartyReference("party-external-1"),
            new ExternalContributionSource("supplier-api", "confirm-1"));

        string json = JsonSerializer.Serialize(command, JsonOptions);

        json.ShouldContain("\"timeEntryId\"");
        json.ShouldContain("\"contributor\"");
        json.ShouldContain("\"source\"");
        json.ShouldNotContain("\"approver\"");
        json.ShouldNotContain("\"approval\"");
        json.ShouldNotContain("\"tenant\"");
        json.ShouldNotContain("\"token\"");

        ConfirmExternalTimeEntry? roundTripped = JsonSerializer.Deserialize<ConfirmExternalTimeEntry>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.ShouldBe(new TimeEntryId("external-entry-1"));
        roundTripped.Contributor.ShouldBe(new PartyReference("party-external-1"));
        roundTripped.Source.ExternalRequestId.ShouldBe("confirm-1");
    }

    [Fact]
    public void Contributor_confirmation_event_records_evidence_without_approval_authority()
    {
        TimeEntryContributorConfirmed confirmed = new(
            new TimeEntryId("external-entry-1"),
            new PartyReference("party-external-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            new ExternalContributionSource("supplier-api", "confirm-1"));

        string json = JsonSerializer.Serialize(confirmed, JsonOptions);

        json.ShouldContain("\"confirmedAtUtc\"");
        json.ShouldContain("\"sourceSystem\":\"supplier-api\"");
        json.ShouldNotContain("\"approver\"");
        json.ShouldNotContain("\"approvalDecision\"");
        json.ShouldNotContain("\"lock\"");
        json.ShouldNotContain("\"token\"");

        TimeEntryContributorConfirmed? roundTripped = JsonSerializer.Deserialize<TimeEntryContributorConfirmed>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.TimeEntryId.ShouldBe(new TimeEntryId("external-entry-1"));
        roundTripped.Contributor.ShouldBe(new PartyReference("party-external-1"));
        roundTripped.Source.ExternalRequestId.ShouldBe("confirm-1");
    }

    [Fact]
    public void Openapi_declares_external_contribution_commands_and_evidence_without_authority_fields()
    {
        JsonNode artifact = JsonNode.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json")))
            ?? throw new InvalidOperationException("OpenAPI artifact could not be parsed.");
        JsonObject schemas = artifact["components"]?["schemas"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI schemas are missing.");

        schemas.ContainsKey("SubmitExternalTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("ConfirmExternalTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("ExternalContributionSource").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryContributorConfirmed").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryContributorConfirmationEvidence").ShouldBeTrue();

        JsonObject submitProperties = schemas["SubmitExternalTimeEntry"].ShouldNotBeNull()["properties"]?.AsObject()
            ?? throw new InvalidOperationException("SubmitExternalTimeEntry properties are missing.");
        submitProperties.ContainsKey("tenant").ShouldBeFalse();
        submitProperties.ContainsKey("actor").ShouldBeFalse();
        submitProperties.ContainsKey("approval").ShouldBeFalse();
        submitProperties.ContainsKey("messageId").ShouldBeFalse();
        submitProperties.ContainsKey("correlationId").ShouldBeFalse();
        submitProperties.ContainsKey("token").ShouldBeFalse();

        string submitSchema = schemas["SubmitExternalTimeEntry"].ShouldNotBeNull().ToJsonString();
        submitSchema.ShouldContain("source");

        JsonObject evidenceProperties = schemas["TimeEntryContributorConfirmationEvidence"].ShouldNotBeNull()["properties"]?.AsObject()
            ?? throw new InvalidOperationException("TimeEntryContributorConfirmationEvidence properties are missing.");
        evidenceProperties.ContainsKey("approver").ShouldBeFalse();
        evidenceProperties.ContainsKey("approvalDecision").ShouldBeFalse();

        string evidenceSchema = schemas["TimeEntryContributorConfirmationEvidence"].ShouldNotBeNull().ToJsonString();
        evidenceSchema.ShouldContain("confirmedAtUtc");
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
