using System.Runtime.InteropServices;

namespace CodeSnifferDog.Modules.Tools.Common;

internal sealed class RipgrepAssetLocator
{
    private readonly string _baseDirectory;

    public RipgrepAssetLocator()
        : this(GetBaseDirectory())
    {
    }

    internal RipgrepAssetLocator(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _baseDirectory = Path.GetFullPath(baseDirectory.Trim());
    }

    public string GetExecutablePath()
    {
        string relativePath = GetRelativeExecutablePath();
        string candidatePath = Path.Combine(_baseDirectory, relativePath);

        if (File.Exists(candidatePath))
            return candidatePath;

        throw new FileNotFoundException(
            $"Ripgrep asset was not found under the application base directory. Expected path: {candidatePath}");
    }

    private static string GetBaseDirectory()
    {
        string? baseDirectory = AppContext.BaseDirectory;

        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new DirectoryNotFoundException("Application base directory could not be determined.");

        return Path.GetFullPath(baseDirectory);
    }

    private static string GetRelativeExecutablePath() =>
        Path.Combine(
            "assets",
            "ripgrep",
            GetArchitectureFolderName(),
            OperatingSystem.IsWindows() ? "rg.exe" : "rg");

    private static string GetArchitectureFolderName()
    {
        if (OperatingSystem.IsWindows())
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "win-arm64",
                Architecture.X64 => "win-x64",
                _ => throw new PlatformNotSupportedException($"Windows architecture is not supported: {RuntimeInformation.ProcessArchitecture}"),
            };

        if (OperatingSystem.IsLinux())
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "linux-arm64",
                Architecture.X64 => "linux-x64",
                _ => throw new PlatformNotSupportedException($"Linux architecture is not supported: {RuntimeInformation.ProcessArchitecture}"),
            };

        if (OperatingSystem.IsMacOS())
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "osx-arm64",
                Architecture.X64 => "osx-x64",
                _ => throw new PlatformNotSupportedException($"macOS architecture is not supported: {RuntimeInformation.ProcessArchitecture}"),
            };

        throw new PlatformNotSupportedException("The current operating system is not supported.");
    }
}
