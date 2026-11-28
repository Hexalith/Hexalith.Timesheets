using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Configures the single tenant and Project admitted by the host workflow.</summary>
internal sealed class ProjectWorkflowOptions
{
    /// <summary>The opt-in configuration section.</summary>
    internal const string SectionName = "Timesheets:ProjectWorkflow";

    /// <summary>Gets or sets whether to bind the release workflow providers.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the enrolled tenant identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Gets or sets the enrolled Project identifier.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Gets or sets the Party IDs configured as approvers in the enrolled Project.</summary>
    public string[] ApproverPartyIds { get; set; } = [];

    /// <summary>Gets or sets the EventStore gateway HTTPS origin used by the authority query client.</summary>
    public string? AuthorityGatewayEndpoint { get; set; }

    /// <summary>Gets the validated authority gateway origin.</summary>
    internal Uri? AuthorityGatewayOrigin => Uri.TryCreate(AuthorityGatewayEndpoint, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/"
        && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : null;

    /// <summary>Gets or sets the deployed evidence policy.</summary>
    /// <remarks>The binder updates this instance independently of the kernel's shared defaults.</remarks>
    public TimesheetsEvidencePolicyOptions EvidencePolicy { get; set; } = new();

    /// <summary>Determines whether authority for the selected tenant and Project is configured.</summary>
    internal bool IsConfigured => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ProjectId)
        && AuthorityGatewayOrigin is not null && ApproverPartyIds.Length > 0 && ApproverPartyIds.All(id => !string.IsNullOrWhiteSpace(id));
}
