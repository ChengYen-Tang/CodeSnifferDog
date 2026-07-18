using CodeSnifferDog.Models.Common.Tools;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace CodeSnifferDog.Modules.Tools.Shell;

/// <summary>
/// Runs PowerShell 7 commands in-process through <c>Microsoft.PowerShell.SDK</c>.
/// </summary>
/// <remarks>
/// A fresh runspace per invocation prevents commands from leaking variables, locations, or module state into another
/// agent call. Cancellation stops the hosted PowerShell pipeline. Direct PowerShell process/job detachment APIs are
/// rejected, but native commands can themselves spawn detached children and are outside this in-process runner's
/// containment boundary. No external <c>pwsh</c> or platform-specific shell executable is required.
/// </remarks>
internal sealed class PowerShellCommandRunner : IShellCommandRunner
{
    /// <summary>
    /// Stores a normalized native-command exit code in the isolated runspace.
    /// </summary>
    private const string ExitCodeVariable = "CodeSnifferDogShellExitCode";

    /// <summary>
    /// Exit code returned when output is capped and the still-running pipeline is stopped before completion.
    /// </summary>
    private const int OutputLimitExceededExitCode = 1;

    /// <summary>
    /// Cmdlets that detach work from the foreground pipeline and therefore cannot meet the tool's cancellation contract.
    /// </summary>
    private static readonly string[] BackgroundCommandNames =
    [
        "Start-Process",
        "Start-Job",
        "Start-ThreadJob",
        "Register-ObjectEvent",
        "Invoke-Expression",
        "iex",
        "start",
        "saps",
    ];

    /// <inheritdoc />
    public async ValueTask<CommandExecutionResult> RunAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        string? backgroundCommand = FindDetachedCommand(command);

        if (backgroundCommand is not null)
            return CreateDetachedCommandResult(backgroundCommand);

        using Runspace runspace = RunspaceFactory.CreateRunspace(CreateInitialSessionState());
        runspace.Open();
        using PowerShell powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;

        SetWorkingDirectory(powerShell, workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        runspace.SessionStateProxy.SetVariable(ExitCodeVariable, 0);
        powerShell
            .AddScript(BuildScript(command), useLocalScope: true)
            .AddCommand("Out-String")
            .AddParameter("Width", 4096)
            .AddParameter("Stream");

        PowerShellOutputCapture capture = new();
        using PSDataCollection<PSObject> input = new();
        using PSDataCollection<PSObject> output = new();
        input.Complete();

        PowerShellInvocationController invocationController = new(powerShell);
        capture.LimitExceeded += invocationController.RequestStop;
        output.DataAdding += OnOutputAdding;
        output.DataAdded += OnOutputAdded;
        powerShell.Streams.Error.DataAdding += OnErrorAdding;

        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((PowerShellInvocationController)state!).RequestStop(),
            invocationController);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<PSDataCollection<PSObject>?> invocation = powerShell.InvokeAsync(input, output);
            invocationController.MarkInvocationStarted();
            cancellationToken.ThrowIfCancellationRequested();
            await invocation.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (capture.WasTruncated)
                return capture.CreateResult(OutputLimitExceededExitCode, pipelineWasStoppedEarly: true);

            return capture.CreateResult(GetExitCode(runspace, powerShell.HadErrors));
        }
        catch (PipelineStoppedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (PipelineStoppedException) when (capture.WasTruncated)
        {
            return capture.CreateResult(OutputLimitExceededExitCode, pipelineWasStoppedEarly: true);
        }
        catch (RuntimeException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (RuntimeException exception)
        {
            capture.CaptureTerminatingError(exception.Message);
            return capture.CreateResult(exitCode: 1);
        }
        finally
        {
            output.DataAdding -= OnOutputAdding;
            output.DataAdded -= OnOutputAdded;
            powerShell.Streams.Error.DataAdding -= OnErrorAdding;
            capture.LimitExceeded -= invocationController.RequestStop;
        }

        void OnOutputAdding(object? _, DataAddingEventArgs eventArgs)
            => capture.CaptureOutput(eventArgs.ItemAdded);

        void OnOutputAdded(object? _, DataAddedEventArgs __)
            => output.Clear();

        void OnErrorAdding(object? _, DataAddingEventArgs eventArgs)
            => capture.CaptureError(eventArgs.ItemAdded);
    }

    /// <summary>
    /// Creates the isolated command session with known detachment cmdlets removed.
    /// </summary>
    /// <returns>The session state for one PowerShell tool execution.</returns>
    private static InitialSessionState CreateInitialSessionState()
    {
        InitialSessionState initialSessionState = InitialSessionState.CreateDefault2();

        foreach (string commandName in BackgroundCommandNames)
            initialSessionState.Commands.Remove(commandName, type: null);

        return initialSessionState;
    }

    /// <summary>
    /// Detects syntax that would detach work from the foreground pipeline.
    /// </summary>
    /// <param name="command">PowerShell script supplied to the Shell tool.</param>
    /// <returns>The rejected command or parameter description, or <see langword="null"/> when no direct detachment API is used.</returns>
    private static string? FindDetachedCommand(string command)
    {
        ScriptBlockAst scriptBlock = Parser.ParseInput(command, out _, out _);

        foreach (CommandAst commandAst in scriptBlock.FindAll(static ast => ast is CommandAst, searchNestedScriptBlocks: true).OfType<CommandAst>())
        {
            string? commandName = commandAst.GetCommandName();

            if (commandName is not null && BackgroundCommandNames.Contains(commandName, StringComparer.OrdinalIgnoreCase))
                return commandName;

            if (commandName is null)
                return "dynamic command invocation";

            if (commandAst.CommandElements.OfType<CommandParameterAst>().Any(static parameter =>
                string.Equals(parameter.ParameterName, "AsJob", StringComparison.OrdinalIgnoreCase)))
            {
                return "-AsJob";
            }
        }

        foreach (TypeExpressionAst typeExpression in scriptBlock.FindAll(
            static ast => ast is TypeExpressionAst,
            searchNestedScriptBlocks: true).OfType<TypeExpressionAst>())
        {
            if (string.Equals(
                typeExpression.TypeName.FullName,
                "System.Diagnostics.Process",
                StringComparison.OrdinalIgnoreCase))
            {
                return "System.Diagnostics.Process";
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a non-executing result for background work that cannot be safely cancelled with the tool call.
    /// </summary>
    /// <param name="backgroundCommand">Command or parameter that requested detached execution.</param>
    /// <returns>The rejected command result.</returns>
    private static CommandExecutionResult CreateDetachedCommandResult(string backgroundCommand) => new()
    {
        ExitCode = 1,
        StandardOutput = string.Empty,
        StandardError = $"Shell does not permit '{backgroundCommand}' because it can detach a child process or job, or dynamically bypass direct detachment checks. Run only foreground operations.",
    };

    /// <summary>Moves the newly created runspace to the requested repository root.</summary>
    private static void SetWorkingDirectory(PowerShell powerShell, string workingDirectory)
    {
        powerShell
            .AddCommand("Set-Location")
            .AddParameter("LiteralPath", workingDirectory);
        _ = powerShell.Invoke();

        if (powerShell.HadErrors)
        {
            string errors = string.Join(Environment.NewLine, powerShell.Streams.Error.Select(static error => error.ToString()));
            throw new InvalidOperationException($"PowerShell could not set the repository working directory: {errors}");
        }
    }

    /// <summary>Builds a script that preserves native-command exit codes without exposing a process shell.</summary>
    private static string BuildScript(string command) => string.Concat(
        "$ProgressPreference = 'SilentlyContinue'", Environment.NewLine,
        "$PSModuleAutoLoadingPreference = 'None'", Environment.NewLine,
        "$global:", ExitCodeVariable, " = 0", Environment.NewLine,
        command, Environment.NewLine,
        "if ($null -ne $LASTEXITCODE) {", Environment.NewLine,
        "    $global:", ExitCodeVariable, " = [int]$LASTEXITCODE", Environment.NewLine,
        "} elseif (-not $?) {", Environment.NewLine,
        "    $global:", ExitCodeVariable, " = 1", Environment.NewLine,
        "}");

    /// <summary>Gets the normalized exit code produced by the isolated runspace.</summary>
    private static int GetExitCode(Runspace runspace, bool hadErrors)
    {
        object? value = runspace.SessionStateProxy.GetVariable(ExitCodeVariable);
        return value is int exitCode
            ? exitCode
            : hadErrors ? 1 : 0;
    }

    /// <summary>
    /// Coordinates an asynchronously started pipeline with cancellation and bounded-output stop requests.
    /// </summary>
    private sealed class PowerShellInvocationController
    {
        /// <summary>
        /// Stores the pipeline to stop once asynchronous invocation is active.
        /// </summary>
        private readonly PowerShell _powerShell;

        /// <summary>
        /// Tracks whether <see cref="PowerShell.InvokeAsync"/> has returned and can now be stopped safely.
        /// </summary>
        private int _invocationStarted;

        /// <summary>
        /// Tracks cancellation or output-limit requests that arrive before invocation starts.
        /// </summary>
        private int _stopRequested;

        /// <summary>
        /// Ensures that only one background worker calls <see cref="PowerShell.Stop"/>.
        /// </summary>
        private int _stopScheduled;

        /// <summary>
        /// Initializes a new controller for one PowerShell pipeline.
        /// </summary>
        /// <param name="powerShell">Pipeline instance to stop.</param>
        public PowerShellInvocationController(PowerShell powerShell)
        {
            _powerShell = powerShell;
        }

        /// <summary>
        /// Marks the asynchronous invocation as active and applies any earlier stop request.
        /// </summary>
        public void MarkInvocationStarted()
        {
            Volatile.Write(ref _invocationStarted, 1);

            if (Volatile.Read(ref _stopRequested) != 0)
                ScheduleStop();
        }

        /// <summary>
        /// Records a request to stop and stops immediately once the pipeline is active.
        /// </summary>
        public void RequestStop()
        {
            Volatile.Write(ref _stopRequested, 1);

            if (Volatile.Read(ref _invocationStarted) != 0)
                ScheduleStop();
        }

        /// <summary>
        /// Schedules stop work away from PowerShell's output callback to avoid a pipeline-thread deadlock.
        /// </summary>
        private void ScheduleStop()
        {
            if (Interlocked.Exchange(ref _stopScheduled, 1) != 0)
                return;

            _ = Task.Run(StopPipeline);
        }

        /// <summary>
        /// Stops an active pipeline while ignoring normal completion races.
        /// </summary>
        private void StopPipeline()
        {
            try
            {
                _powerShell.Stop();
            }
            catch (InvalidPowerShellStateException)
            {
            }
        }
    }
}
