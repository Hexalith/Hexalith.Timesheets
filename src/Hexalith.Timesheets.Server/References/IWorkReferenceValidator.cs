using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.References;

public interface IWorkReferenceValidator
{
    ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken);
}
