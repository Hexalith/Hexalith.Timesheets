using System.Text.Json;

using Hexalith.Timesheets.Contracts;
using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.Ui;
using Hexalith.Timesheets.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Timesheets.Contracts.Tests;

public sealed class ActivityTypeCatalogE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Activity_type_catalog_operator_workflow_metadata_exposes_keyboard_reachable_commands()
    {
        TimesheetsMetadataDescriptor command = Descriptor("timesheets.command.activity-type-catalog");
        TimesheetsMetadataDescriptor projection = Descriptor("timesheets.projection.activity-type-catalog");

        command.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerGeneratedForm);
        projection.Pattern.ShouldBe(TimesheetsCompositionPattern.FrontComposerProjectionView);

        string[] workflowActions = projection.Actions.Select(static action => action.Name).ToArray();
        workflowActions.ShouldBe(
        [
            "create-tenant-activity-type",
            "rename-activity-type",
            "update-billable-default",
            "deactivate-activity-type",
            "reactivate-activity-type"
        ]);

        projection.Actions.Select(static action => action.Label).ShouldBe(
        [
            "Create tenant Activity Type",
            "Rename Activity Type",
            "Update billable default",
            "Deactivate Activity Type",
            "Reactivate Activity Type"
        ]);

        projection.Actions.All(static action =>
            !string.IsNullOrWhiteSpace(action.Intent) &&
            !string.IsNullOrWhiteSpace(action.Label)).ShouldBeTrue();
        command.Actions.Select(static action => action.Name).ShouldContain("create-tenant-activity-type");
    }

    [Fact]
    public void Activity_type_catalog_projection_metadata_exposes_textual_state_and_freshness_cues()
    {
        TimesheetsMetadataDescriptor projection = Descriptor("timesheets.projection.activity-type-catalog");

        projection.Fields.Select(static field => field.Name).ShouldContain("activeState");
        projection.Fields.Select(static field => field.Name).ShouldContain("statusText");
        projection.Fields.Select(static field => field.Name).ShouldContain("projectionFreshness");
        projection.Fields.Single(static field => field.Name == "statusText")
            .HelpText.ShouldBe("Active and inactive state is shown as text.");

        projection.StateBadges.Select(static badge => badge.StateVocabulary).ShouldBe(
        [
            nameof(ActivityTypeActiveState),
            nameof(ProjectionFreshnessState)
        ]);
    }

    [Fact]
    public void Activity_type_catalog_read_model_round_trips_active_and_inactive_rows_for_capture_selection()
    {
        ActivityTypeCatalogReadModel model = new(
            [
                new(
                    new ActivityTypeId("discovery"),
                    ActivityTypeScope.Tenant,
                    null,
                    "Discovery",
                    true,
                    BillableState.Billable),
                new(
                    new ActivityTypeId("legacy-support"),
                    ActivityTypeScope.Tenant,
                    null,
                    "Legacy support",
                    false,
                    BillableState.NonBillable)
            ],
            ProjectionFreshnessMetadata.Fresh);

        string json = JsonSerializer.Serialize(model, JsonOptions);

        json.ShouldContain("\"statusText\":\"Active\"");
        json.ShouldContain("\"statusText\":\"Inactive\"");
        json.ShouldContain("\"isAvailableForCapture\":false");
        AssertJsonOmitsCallerAuthority(json);

        ActivityTypeCatalogReadModel? roundTripped = JsonSerializer.Deserialize<ActivityTypeCatalogReadModel>(json, JsonOptions);

        roundTripped.ShouldNotBeNull();
        roundTripped.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        roundTripped.Items.Count.ShouldBe(2);
        roundTripped.Items.Single(static item => item.ActivityTypeId.Value == "legacy-support")
            .IsAvailableForCapture.ShouldBeFalse();
    }

    [Fact]
    public void Activity_type_catalog_command_payloads_round_trip_without_server_authority_fields()
    {
        CreateTenantActivityType create = RoundTrip(new CreateTenantActivityType(
            new ActivityTypeId("discovery"),
            "Discovery",
            BillableState.Billable));
        RenameActivityType rename = RoundTrip(new RenameActivityType(
            new ActivityTypeId("discovery"),
            "Customer discovery"));
        UpdateActivityTypeMetadata update = RoundTrip(new UpdateActivityTypeMetadata(
            new ActivityTypeId("discovery"),
            BillableState.NonBillable));
        DeactivateActivityType deactivate = RoundTrip(new DeactivateActivityType(new ActivityTypeId("discovery")));
        ReactivateActivityType reactivate = RoundTrip(new ReactivateActivityType(new ActivityTypeId("discovery")));

        create.ActivityTypeId.Value.ShouldBe("discovery");
        rename.Label.ShouldBe("Customer discovery");
        update.DefaultBillableState.ShouldBe(BillableState.NonBillable);
        deactivate.ActivityTypeId.Value.ShouldBe("discovery");
        reactivate.ActivityTypeId.Value.ShouldBe("discovery");
    }

    private static TimesheetsMetadataDescriptor Descriptor(string name)
        => TimesheetsMetadataCatalog.Descriptors.Single(descriptor => descriptor.Name == name);

    private static T RoundTrip<T>(T value)
        where T : class
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        AssertJsonOmitsCallerAuthority(json);

        T? roundTripped = JsonSerializer.Deserialize<T>(json, JsonOptions);

        return roundTripped ?? throw new InvalidOperationException($"Could not deserialize {typeof(T).Name}.");
    }

    private static void AssertJsonOmitsCallerAuthority(string json)
    {
        string normalizedJson = json.ToLowerInvariant();
        string[] forbiddenPropertyNames =
        [
            "tenantId",
            "userId",
            "correlationId",
            "messageId",
            "causationId",
            "authorization",
            "claimsPrincipal",
            "jwt",
            "token",
            "stream",
            "sequence",
            nameof(ProjectReference.ProjectId)
        ];

        foreach (string forbiddenPropertyName in forbiddenPropertyNames)
        {
            normalizedJson.Contains(
                $"\"{forbiddenPropertyName.ToLowerInvariant()}\"",
                StringComparison.Ordinal).ShouldBeFalse(forbiddenPropertyName);
        }
    }
}
