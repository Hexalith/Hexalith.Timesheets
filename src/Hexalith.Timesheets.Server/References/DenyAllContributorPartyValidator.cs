using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.References;

public sealed class DenyAllContributorPartyValidator : IContributorPartyValidator
{
    public ValueTask<ReferenceValidationResult> ValidateAsync(
        TimesheetsRequestContext context,
        PartyReference contributor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(contributor);

        return ValueTask.FromResult(ReferenceValidationResult.Invalid("Contributor party validation is not configured."));
    }
}
