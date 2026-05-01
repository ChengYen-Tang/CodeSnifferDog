using CodeSnifferDog.Models.Common.Tools;
using System.Diagnostics;

namespace CodeSnifferDog.Modules.Tools.Common;

internal sealed class CommandProcessRunner
{
    public static ValueTask<CommandExecutionResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        ProcessStartInfo startInfo = CreateStartInfo(fileName, workingDirectory);

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return RunAsync(startInfo, cancellationToken);
    }

    public static async ValueTask<CommandExecutionResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = CreateStartInfo(fileName, workingDirectory);
        startInfo.Arguments = arguments;
        return await RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        return new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static async ValueTask<CommandExecutionResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = startInfo,
        };

        process.Start();

        using CancellationTokenRegistration _ = cancellationToken.Register(static state =>
        {
            Process? currentProcess = state as Process;

            if (currentProcess?.HasExited == false)
                currentProcess.Kill(entireProcessTree: true);
        }, process);

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new CommandExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await standardOutputTask.ConfigureAwait(false),
            StandardError = await standardErrorTask.ConfigureAwait(false),
        };
    }
}
