using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.References;

public interface IProjectReferenceValidator
{
    ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        ProjectReference project,
        CancellationToken cancellationToken);
}
