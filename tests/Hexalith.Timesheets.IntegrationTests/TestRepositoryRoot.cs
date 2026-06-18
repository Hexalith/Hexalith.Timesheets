namespace Hexalith.Timesheets.IntegrationTests;

internal static class TestRepositoryRoot
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
}
