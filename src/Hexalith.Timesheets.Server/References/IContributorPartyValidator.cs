using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.References;

public interface IContributorPartyValidator
{
    ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        PartyReference contributor,
        CancellationToken cancellationToken);
}
