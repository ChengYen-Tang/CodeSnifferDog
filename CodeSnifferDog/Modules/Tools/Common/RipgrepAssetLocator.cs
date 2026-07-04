using System.Runtime.InteropServices;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Locates the bundled ripgrep executable for the current platform.
/// </summary>
internal sealed class RipgrepAssetLocator
{
    private readonly string _baseDirectory;

    public RipgrepAssetLocator()
        : this(GetBaseDirectory())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RipgrepAssetLocator"/> class.
    /// </summary>
    /// <param name="baseDirectory">Base directory that contains bundled assets.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is blank.</exception>
    internal RipgrepAssetLocator(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _baseDirectory = Path.GetFullPath(baseDirectory.Trim());
    }

    /// <summary>
    /// Gets the full path of the bundled ripgrep executable.
    /// </summary>
    /// <returns>The full executable path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the bundled ripgrep executable cannot be found.</exception>
    public string GetExecutablePath()
    {
        string relativePath = GetRelativeExecutablePath();
        string candidatePath = Path.Combine(_baseDirectory, relativePath);

        if (File.Exists(candidatePath))
            return candidatePath;

        throw new FileNotFoundException(
            $"Ripgrep asset was not found under the application base directory. Expected path: {candidatePath}");
    }

    /// <summary>
    /// Gets the normalized application base directory.
    /// </summary>
    /// <returns>The normalized base directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the application base directory cannot be determined.</exception>
    private static string GetBaseDirectory()
    {
        string? baseDirectory = AppContext.BaseDirectory;

        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new DirectoryNotFoundException("Application base directory could not be determined.");

        return Path.GetFullPath(baseDirectory);
    }

    /// <summary>
    /// Gets the platform-relative path to the ripgrep executable.
    /// </summary>
    /// <returns>The relative executable path.</returns>
    private static string GetRelativeExecutablePath() =>
        Path.Combine(
            "assets",
            "ripgrep",
            GetArchitectureFolderName(),
            OperatingSystem.IsWindows() ? "rg.exe" : "rg");

    /// <summary>
    /// Gets the asset folder name that matches the current operating system and architecture.
    /// </summary>
    /// <returns>The asset folder name.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
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
