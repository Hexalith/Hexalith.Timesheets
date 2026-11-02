using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

foreach (bool byPath in new[] { false, true })
{
    var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
    builder.Environment.EnvironmentName = "Development";
    string generatedPath = new Projects.Hexalith_Timesheets().ProjectPath;
    var selected = byPath ? builder.AddProject("timesheets", generatedPath) : builder.AddProject<Projects.Hexalith_Timesheets>("timesheets");
    selected.WithHttpEndpoint(name: "public", port: 18080, isProxied: false).WithHttpEndpoint(name: "internal", port: 8081, isProxied: false);
#pragma warning disable ASPIREEXTENSION001
    var launch = (ProjectLaunchConfiguration)await selected.Resource.CreateLaunchConfigurationAsync(ExecutableLaunchMode.NoDebug);
#pragma warning restore ASPIREEXTENSION001
    Console.WriteLine(JsonSerializer.Serialize(new { byPath, metadataSuppressBuild = selected.Resource.GetProjectMetadata().SuppressBuild, effectiveSuppressBuild = launch.SuppressBuild, launchArgumentOverrideCount = selected.Resource.Annotations.Count(annotation => annotation.GetType().Name is "LaunchToolArgsCallbackAnnotation" or "ProjectLaunchArgsOverrideAnnotation"), exactGeneratedProjectPath = launch.ProjectPath == generatedPath, projectResourceCount = builder.Resources.OfType<ProjectResource>().Count(), endpointCount = selected.Resource.Annotations.OfType<EndpointAnnotation>().Count(), resourceNameMatches = selected.Resource.Name == "timesheets" }));
}
