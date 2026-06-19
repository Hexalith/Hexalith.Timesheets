using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public static class ProjectActivityTypeAggregate
{
    public static TimesheetsDomainResult Handle(CreateProjectActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsDomainResult? validation = ValidateProject(command.Project)
            ?? ValidateLabel(command.Label)
            ?? ValidateBillable(command.DefaultBillableState);
        if (validation is not null)
        {
            return validation;
        }

        if (state is not null
            && state.TryGet(command.ActivityTypeId.Value, out ActivityTypeState? existing)
            && existing is not null)
        {
            return existing.Scope == ActivityTypeScope.Project && existing.Project == command.Project
                ? Reject(TimesheetsRejectionCode.ActivityTypeAlreadyExists, "Activity Type already exists.", "activityTypeId", "duplicate")
                : Reject(TimesheetsRejectionCode.ActivityTypeScopeMismatch, "Activity Type scope does not match project catalog.", "scope", "scope-mismatch");
        }

        return TimesheetsDomainResult.Success([
            new ActivityTypeCreated(
                command.ActivityTypeId,
                ActivityTypeScope.Project,
                command.Project,
                NormalizeLabel(command.Label),
                command.DefaultBillableState)
        ]);
    }

    public static TimesheetsDomainResult Handle(RenameProjectActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsDomainResult? validation = ValidateProject(command.Project) ?? ValidateLabel(command.Label);
        if (validation is not null)
        {
            return validation;
        }

        ActivityTypeState? existing = FindProjectActivityType(command.ActivityTypeId, command.Project, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        string normalized = NormalizeLabel(command.Label);
        return string.Equals(existing!.Label, normalized, StringComparison.Ordinal)
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeRenamed(command.ActivityTypeId, normalized)]);
    }

    public static TimesheetsDomainResult Handle(UpdateProjectActivityTypeMetadata command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsDomainResult? validation = ValidateProject(command.Project) ?? ValidateBillable(command.DefaultBillableState);
        if (validation is not null)
        {
            return validation;
        }

        ActivityTypeState? existing = FindProjectActivityType(command.ActivityTypeId, command.Project, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        return existing!.DefaultBillableState == command.DefaultBillableState
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeMetadataUpdated(command.ActivityTypeId, command.DefaultBillableState)]);
    }

    public static TimesheetsDomainResult Handle(DeactivateProjectActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsDomainResult? validation = ValidateProject(command.Project);
        if (validation is not null)
        {
            return validation;
        }

        ActivityTypeState? existing = FindProjectActivityType(command.ActivityTypeId, command.Project, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        return !existing!.IsActive
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeDeactivated(command.ActivityTypeId)]);
    }

    public static TimesheetsDomainResult Handle(ReactivateProjectActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsDomainResult? validation = ValidateProject(command.Project);
        if (validation is not null)
        {
            return validation;
        }

        ActivityTypeState? existing = FindProjectActivityType(command.ActivityTypeId, command.Project, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        return existing!.IsActive
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeReactivated(command.ActivityTypeId)]);
    }

    public static TimesheetsDomainResult Handle(ConfigureProjectActivityTypeCatalogRestriction command)
    {
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsDomainResult? validation = ValidateProject(command.Project)
            ?? ValidateDistinct(command.AllowedTenantActivityTypeIds, "allowedTenantActivityTypeIds")
            ?? ValidateDistinct(command.AllowedProjectActivityTypeIds, "allowedProjectActivityTypeIds");
        if (validation is not null)
        {
            return validation;
        }

        return TimesheetsDomainResult.Success([
            new ProjectActivityTypeCatalogRestrictionConfigured(
                command.Project,
                command.IsRestricted,
                command.AllowedTenantActivityTypeIds,
                command.AllowedProjectActivityTypeIds)
        ]);
    }

    private static ActivityTypeState? FindProjectActivityType(
        ActivityTypeId activityTypeId,
        ProjectReference project,
        ActivityTypeCatalogState? state,
        out TimesheetsDomainResult? rejection)
    {
        if (state is null
            || !state.TryGet(activityTypeId.Value, out ActivityTypeState? existing)
            || existing is null)
        {
            rejection = Reject(TimesheetsRejectionCode.ActivityTypeNotFound, "Activity Type was not found.", "activityTypeId", "not-found");
            return null;
        }

        if (existing.Scope != ActivityTypeScope.Project || existing.Project != project)
        {
            rejection = Reject(TimesheetsRejectionCode.ActivityTypeScopeMismatch, "Activity Type scope does not match project catalog.", "scope", "scope-mismatch");
            return null;
        }

        rejection = null;
        return existing;
    }

    private static TimesheetsDomainResult? ValidateProject(ProjectReference? project)
        => project is null
            ? Reject(TimesheetsRejectionCode.ValidationFailed, "Project reference is required.", "project", "required")
            : null;

    private static TimesheetsDomainResult? ValidateLabel(string label)
        => string.IsNullOrWhiteSpace(label)
            ? Reject(TimesheetsRejectionCode.ValidationFailed, "Activity Type label is required.", "label", "blank")
            : null;

    private static TimesheetsDomainResult? ValidateBillable(BillableState state)
        => state == BillableState.Unknown
            ? Reject(TimesheetsRejectionCode.ValidationFailed, "Billable default is required.", "defaultBillableState", "unknown")
            : null;

    private static TimesheetsDomainResult? ValidateDistinct(IReadOnlyList<ActivityTypeId> activityTypeIds, string field)
    {
        ArgumentNullException.ThrowIfNull(activityTypeIds);

        HashSet<string> uniqueIds = new(StringComparer.Ordinal);
        return activityTypeIds.Any(activityTypeId => !uniqueIds.Add(activityTypeId.Value))
            ? Reject(TimesheetsRejectionCode.ValidationFailed, "Activity Type IDs must be unique.", field, "duplicate")
            : null;
    }

    private static string NormalizeLabel(string label) => label.Trim();

    private static TimesheetsDomainResult Reject(
        TimesheetsRejectionCode code,
        string message,
        string field,
        string fieldCode)
        => TimesheetsDomainResult.Rejection([
            new TimesheetsRejection(
                code,
                message,
                [
                    new(field, fieldCode, message)
                ])
        ]);
}
