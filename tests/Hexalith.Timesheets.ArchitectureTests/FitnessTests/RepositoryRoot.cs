namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

internal static class RepositoryRoot
{
    public static DirectoryInfo Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Timesheets.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Timesheets repository root.");
    }

    public static string PathTo(params string[] segments)
    {
        return Path.Combine([Find().FullName, .. segments]);
    }

    /// <summary>
    /// Lists Timesheets-owned project files. Any path with a <c>references</c> segment is a sibling submodule checkout,
    /// including those inside in-repository worktrees; the checkout directory name cannot decide ownership because the
    /// Actions checkout is itself named <c>Hexalith.Timesheets</c>.
    /// </summary>
    public static string[] TimesheetsProjectFiles()
    {
        string root = Find().FullName;

        return Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains("references", StringComparer.Ordinal))
            .ToArray();
    }
}
