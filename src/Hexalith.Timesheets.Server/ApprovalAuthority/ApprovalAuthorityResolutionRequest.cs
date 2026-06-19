using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public sealed record ApprovalAuthorityResolutionRequest(
    TimesheetsAuthorizationRequest AuthorizationRequest,
    ApprovalAuthorityAction Action,
    PartyReference? Contributor);
