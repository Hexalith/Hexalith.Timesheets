using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.ActivityTypes;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public static class TenantActivityTypeAggregate
{
    public static TimesheetsDomainResult Handle(CreateTenantActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        string? labelError = ValidateLabel(command.Label);
        if (labelError is not null)
        {
            return Reject(TimesheetsRejectionCode.ValidationFailed, "Activity Type label is required.", "label", labelError);
        }

        if (command.DefaultBillableState == BillableState.Unknown)
        {
            return Reject(TimesheetsRejectionCode.ValidationFailed, "Billable default is required.", "defaultBillableState", "unknown");
        }

        if (state is not null
            && state.TryGet(command.ActivityTypeId.Value, out ActivityTypeState? existing)
            && existing is not null)
        {
            return existing.Scope == ActivityTypeScope.Tenant
                ? Reject(TimesheetsRejectionCode.ActivityTypeAlreadyExists, "Activity Type already exists.", "activityTypeId", "duplicate")
                : Reject(TimesheetsRejectionCode.ActivityTypeScopeMismatch, "Activity Type scope does not match tenant catalog.", "scope", "scope-mismatch");
        }

        return TimesheetsDomainResult.Success([
            new ActivityTypeCreated(
                command.ActivityTypeId,
                ActivityTypeScope.Tenant,
                null,
                NormalizeLabel(command.Label),
                command.DefaultBillableState)
        ]);
    }

    public static TimesheetsDomainResult Handle(RenameActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        string? labelError = ValidateLabel(command.Label);
        if (labelError is not null)
        {
            return Reject(TimesheetsRejectionCode.ValidationFailed, "Activity Type label is required.", "label", labelError);
        }

        ActivityTypeState? existing = FindTenantActivityType(command.ActivityTypeId, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        string normalized = NormalizeLabel(command.Label);
        return string.Equals(existing!.Label, normalized, StringComparison.Ordinal)
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeRenamed(command.ActivityTypeId, normalized)]);
    }

    public static TimesheetsDomainResult Handle(UpdateActivityTypeMetadata command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DefaultBillableState == BillableState.Unknown)
        {
            return Reject(TimesheetsRejectionCode.ValidationFailed, "Billable default is required.", "defaultBillableState", "unknown");
        }

        ActivityTypeState? existing = FindTenantActivityType(command.ActivityTypeId, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        return existing!.DefaultBillableState == command.DefaultBillableState
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeMetadataUpdated(command.ActivityTypeId, command.DefaultBillableState)]);
    }

    public static TimesheetsDomainResult Handle(DeactivateActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        ActivityTypeState? existing = FindTenantActivityType(command.ActivityTypeId, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        return !existing!.IsActive
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeDeactivated(command.ActivityTypeId)]);
    }

    public static TimesheetsDomainResult Handle(ReactivateActivityType command, ActivityTypeCatalogState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        ActivityTypeState? existing = FindTenantActivityType(command.ActivityTypeId, state, out TimesheetsDomainResult? rejection);
        if (rejection is not null)
        {
            return rejection;
        }

        return existing!.IsActive
            ? TimesheetsDomainResult.NoOp()
            : TimesheetsDomainResult.Success([new ActivityTypeReactivated(command.ActivityTypeId)]);
    }

    private static ActivityTypeState? FindTenantActivityType(
        ActivityTypeId activityTypeId,
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

        if (existing.Scope != ActivityTypeScope.Tenant || existing.Project is not null)
        {
            rejection = Reject(TimesheetsRejectionCode.ActivityTypeScopeMismatch, "Activity Type scope does not match tenant catalog.", "scope", "scope-mismatch");
            return null;
        }

        rejection = null;
        return existing;
    }

    private static string? ValidateLabel(string label)
        => string.IsNullOrWhiteSpace(label) ? "blank" : null;

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
