using System.Diagnostics;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Runs the isolated local HTTP, Dapr, and EventStore acceptance journey.</summary>
internal static class MagicLinkLiveDaprFixture
{
    /// <summary>Runs the local proof with a bounded process lifetime.</summary>
    internal static async Task<(int ExitCode, string Output, string Error)> RunAsync(CancellationToken cancellationToken)
    {
        string root = FindRepositoryRoot();
        var start = new ProcessStartInfo("python3", "scripts/local_aspire_magic_link_journey.py")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The local Dapr fixture process could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The child exited after the HasExited check; keep the cancellation result.
                }
            }

            throw;
        }

        return (process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scripts", "local_aspire_magic_link_journey.py")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The local Dapr fixture script is unavailable in the repository.");
    }
}
