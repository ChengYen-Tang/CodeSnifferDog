namespace CodeSnifferDog.Tests.Shared;

internal static class TestRepositoryPaths
{
    public static string RootPath { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodeSnifferDog.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the CodeSnifferDog repository root from the test output directory.");
    }
}
