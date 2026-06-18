using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.References;

public sealed class DenyAllWorkReferenceValidator : IWorkReferenceValidator
{
    public ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        return ValueTask.FromResult(ReferenceValidationResult.Invalid("Work reference validation is not configured."));
    }
}
