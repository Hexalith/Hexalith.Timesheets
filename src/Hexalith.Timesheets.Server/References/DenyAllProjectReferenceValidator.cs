using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.References;

public sealed class DenyAllProjectReferenceValidator : IProjectReferenceValidator
{
    public ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        ProjectReference project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(project);

        return ValueTask.FromResult(ReferenceValidationResult.Invalid("Project reference validation is not configured."));
    }
}
