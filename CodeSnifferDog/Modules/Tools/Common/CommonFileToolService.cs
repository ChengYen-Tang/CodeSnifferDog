using CodeSnifferDog.Models.Common.Tools;
using System.Text;
using SharedTokenEstimator = CodeSnifferDog.Modules.Estimation.TokenEstimator;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Reads bounded file ranges for the common tool set.
/// </summary>
internal sealed class CommonFileToolService
{
    /// <summary>
    /// Defines the maximum file range payload returned by the ranged file reader.
    /// </summary>
    internal const int MaxReadFileRangeTokens = 8_000;

    /// <summary>
    /// Stores the UTF-8 byte budget corresponding to <see cref="MaxReadFileRangeTokens"/>.
    /// </summary>
    private static readonly int MaxReadFileRangeBytes =
        SharedTokenEstimator.GetUtf8ByteBudget(MaxReadFileRangeTokens);

    /// <summary>
    /// Stores the normalized repository root path used for relative path resolution.
    /// </summary>
    private readonly string _repositoryRootPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonFileToolService"/> class.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root used to resolve relative file paths.</param>
    public CommonFileToolService(string repositoryRootPath)
    {
        _repositoryRootPath = ValidateRepositoryRootPath(repositoryRootPath);
    }

    /// <summary>
    /// Reads a bounded line range from one file.
    /// </summary>
    /// <param name="args">Range read arguments.</param>
    /// <param name="cancellationToken">Token that cancels file reading.</param>
    /// <returns>The range read result.</returns>
    public async ValueTask<CommandExecutionResult> ReadFileRangeAsync(
        ReadFileRangeArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Path);

        if (args.OffsetLine <= 0)
            throw new ArgumentOutOfRangeException(nameof(args.OffsetLine), args.OffsetLine, "OffsetLine must be greater than zero.");

        if (args.LimitLines <= 0)
            throw new ArgumentOutOfRangeException(nameof(args.LimitLines), args.LimitLines, "LimitLines must be greater than zero.");

        string filePath = ResolveFilePath(args.Path);

        if (!File.Exists(filePath))
            return Error(args, filePath, $"File not found: {filePath}");

        FileInfo fileInfo = new(filePath);
        StringBuilder contentBuilder = new();
        int totalLines = 0;
        int contentBytes = 0;
        long exclusiveEndLine = (long)args.OffsetLine + args.LimitLines;

        await using FileStream stream = File.OpenRead(filePath);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (true)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            totalLines++;

            if (totalLines < args.OffsetLine || totalLines >= exclusiveEndLine)
                continue;

            int lineBytes = SharedTokenEstimator.GetUtf8ByteCount(line) + SharedTokenEstimator.GetUtf8ByteCount(Environment.NewLine);

            if (contentBytes + lineBytes > MaxReadFileRangeBytes)
            {
                return Error(
                    $"Requested file range is too large to return safely. Original lines: at least {totalLines}, original bytes: {fileInfo.Length}. Use ReadFileRange with a smaller offsetLine/limitLines.");
            }

            contentBuilder.AppendLine(line);
            contentBytes += lineBytes;

            if (totalLines + 1 >= exclusiveEndLine)
                break;
        }

        string content = contentBuilder.ToString();
        return new CommandExecutionResult
        {
            ExitCode = 0,
            StandardOutput = content,
            StandardError = string.Empty,
        };
    }

    /// <summary>
    /// Resolves a repository-relative or absolute file path to a full path.
    /// </summary>
    /// <param name="path">Path supplied by the tool caller.</param>
    /// <returns>The resolved full path.</returns>
    private string ResolveFilePath(string path)
    {
        string trimmedPath = path.Trim();
        string candidatePath = Path.IsPathRooted(trimmedPath)
            ? trimmedPath
            : Path.Combine(_repositoryRootPath, trimmedPath);

        return Path.GetFullPath(candidatePath);
    }

    /// <summary>
    /// Creates a failed range-read result with no content payload.
    /// </summary>
    /// <param name="args">Original range read arguments.</param>
    /// <param name="path">Resolved file path.</param>
    /// <param name="message">Failure message returned to the caller.</param>
    /// <returns>The failed range-read result.</returns>
    private static CommandExecutionResult Error(ReadFileRangeArgs args, string path, string message) =>
        Error(message);

    private static CommandExecutionResult Error(string message) =>
        new()
        {
            ExitCode = 1,
            StandardOutput = string.Empty,
            StandardError = message,
        };

    /// <summary>
    /// Validates and normalizes the repository root path.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path to validate.</param>
    /// <returns>The normalized full path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="repositoryRootPath"/> is blank.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist.</exception>
    private static string ValidateRepositoryRootPath(string repositoryRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        string fullPath = Path.GetFullPath(repositoryRootPath.Trim());

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Repository root path does not exist: {fullPath}");

        return fullPath;
    }
}
