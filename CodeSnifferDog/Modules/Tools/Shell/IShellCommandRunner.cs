using CodeSnifferDog.Models.Common.Tools;

namespace CodeSnifferDog.Modules.Tools.Shell;

/// <summary>Executes one PowerShell command inside a repository working directory.</summary>
internal interface IShellCommandRunner
{
    /// <summary>Runs the supplied PowerShell command.</summary>
    ValueTask<CommandExecutionResult> RunAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken);
}
