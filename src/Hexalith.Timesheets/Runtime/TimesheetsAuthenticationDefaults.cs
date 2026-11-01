using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Timesheets.Runtime;

/// <summary>Names the credential policies used by Timesheets HTTP endpoints.</summary>
internal static class TimesheetsAuthenticationDefaults
{
    /// <summary>The existing SDK human policy, including its credential-kind requirement.</summary>
    internal const string HumanPolicy = EventStoreHttpHostSecurityExtensions.HumanPolicy;

    /// <summary>The legacy domain-service fallback, preserving authenticated workload contribution calls.</summary>
    internal const string ExternalContributionPolicy = EventStoreDomainServicePolicies.AnyWorkload;

    /// <summary>The scheme and policy for canonical opaque magic-link credentials.</summary>
    internal const string CapabilityPolicy = "TimesheetsMagicLinkCapability";
}
