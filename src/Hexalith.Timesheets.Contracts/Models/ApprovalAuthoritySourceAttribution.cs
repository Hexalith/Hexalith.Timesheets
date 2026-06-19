using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record ApprovalAuthoritySourceAttribution(
    ApprovalAuthorityAction Action,
    ApprovalAuthoritySource Source,
    ApprovalAuthorityDecisionState DecisionState,
    string PolicyKey,
    string PolicyVersion,
    ProjectionFreshnessMetadata Freshness);
