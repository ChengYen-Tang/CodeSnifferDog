using CodeSnifferDog.Models.Common.Tools;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SharedTokenEstimator = CodeSnifferDog.Modules.Estimation.TokenEstimator;

namespace CodeSnifferDog.Modules.Tools.Common;

/// <summary>
/// Starts external processes and captures their output for tool execution.
/// </summary>
internal sealed class CommandProcessRunner
{
    /// <summary>
    /// Runs a process specified by an executable name and argument list.
    /// </summary>
    /// <param name="fileName">Executable file name.</param>
    /// <param name="arguments">Argument list.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="cancellationToken">Token that cancels process execution.</param>
    /// <returns>The command execution result.</returns>
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

    /// <summary>
    /// Runs a process specified by an executable name and raw argument string.
    /// </summary>
    /// <param name="fileName">Executable file name.</param>
    /// <param name="arguments">Raw argument string.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="cancellationToken">Token that cancels process execution.</param>
    /// <returns>The command execution result.</returns>
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

    /// <summary>
    /// Creates a baseline <see cref="ProcessStartInfo"/> for redirected execution.
    /// </summary>
    /// <param name="fileName">Executable file name.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <returns>The configured start info.</returns>
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

    /// <summary>
    /// Runs a process from prebuilt start info and captures its output.
    /// </summary>
    /// <param name="startInfo">Prepared process start info.</param>
    /// <param name="cancellationToken">Token that cancels process execution.</param>
    /// <returns>The command execution result.</returns>
    private static async ValueTask<CommandExecutionResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = startInfo,
        };

        using WindowsProcessJob? processJob = WindowsProcessJob.TryCreate();

        process.Start();
        processJob?.TryAssign(process);

        using CancellationTokenRegistration _ = cancellationToken.Register(static state =>
        {
            Process? currentProcess = state as Process;

            if (currentProcess?.HasExited == false)
                currentProcess.Kill(entireProcessTree: true);
        }, process);

        CommandOutputCaptureBudget outputBudget = new(CommandOutputLimiter.MaxCombinedOutputBytes);
        Task<CommandStreamCapture> standardOutputTask = CommandStreamCapture.ReadAsync(
            process.StandardOutput,
            outputBudget,
            cancellationToken);
        Task<CommandStreamCapture> standardErrorTask = CommandStreamCapture.ReadAsync(
            process.StandardError,
            outputBudget,
            cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        CommandStreamCapture standardOutput = await standardOutputTask.ConfigureAwait(false);
        CommandStreamCapture standardError = await standardErrorTask.ConfigureAwait(false);
        int originalLines = standardOutput.OriginalLines + standardError.OriginalLines;
        long originalBytes = standardOutput.OriginalBytes + standardError.OriginalBytes;

        if (standardOutput.WasTruncated || standardError.WasTruncated)
            return CommandOutputLimiter.CreateTruncatedResult(
                process.ExitCode,
                standardOutput.CapturedText,
                standardError.CapturedText,
                originalLines,
                originalBytes);

        return new CommandExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput.CapturedText,
            StandardError = standardError.CapturedText,
        };
    }

    /// <summary>
    /// Kills a process tree while swallowing races where the process already exited.
    /// </summary>
    /// <param name="process">Process to kill.</param>
    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

/// <summary>
/// Tracks the remaining combined output byte budget while stdout and stderr are drained concurrently.
/// </summary>
internal sealed class CommandOutputCaptureBudget
{
    /// <summary>
    /// Synchronizes budget updates from stdout and stderr capture tasks.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Stores the remaining combined UTF-8 byte budget.
    /// </summary>
    private int _remainingBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandOutputCaptureBudget"/> class.
    /// </summary>
    /// <param name="maxBytes">Maximum combined UTF-8 bytes to retain.</param>
    public CommandOutputCaptureBudget(int maxBytes)
    {
        _remainingBytes = Math.Max(0, maxBytes);
    }

    /// <summary>
    /// Captures the portion of one chunk that still fits within the shared budget.
    /// </summary>
    /// <param name="value">Chunk text to capture.</param>
    /// <param name="wasTruncated">Whether any portion of the chunk was omitted.</param>
    /// <returns>The captured chunk prefix.</returns>
    public string CapturePrefix(string value, out bool wasTruncated)
    {
        lock (_gate)
        {
            string captured = CommandOutputLimiter.TakeUtf8Prefix(value, _remainingBytes, out int usedBytes);
            _remainingBytes -= usedBytes;
            wasTruncated = captured.Length < value.Length;
            return captured;
        }
    }
}

/// <summary>
/// Captures one redirected process stream while bounding retained text.
/// </summary>
internal sealed class CommandStreamCapture
{
    /// <summary>
    /// Defines the character buffer size used while draining redirected process streams.
    /// </summary>
    private const int BufferSize = 4096;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandStreamCapture"/> class.
    /// </summary>
    /// <param name="capturedText">Text retained within the shared output budget.</param>
    /// <param name="originalBytes">Original stream byte count.</param>
    /// <param name="originalLines">Original stream line count.</param>
    /// <param name="wasTruncated">Whether any stream text was omitted.</param>
    private CommandStreamCapture(
        string capturedText,
        long originalBytes,
        int originalLines,
        bool wasTruncated)
    {
        CapturedText = capturedText;
        OriginalBytes = originalBytes;
        OriginalLines = originalLines;
        WasTruncated = wasTruncated;
    }

    /// <summary>
    /// Gets the retained stream text.
    /// </summary>
    public string CapturedText { get; }

    /// <summary>
    /// Gets the original stream UTF-8 byte count.
    /// </summary>
    public long OriginalBytes { get; }

    /// <summary>
    /// Gets the original logical stream line count.
    /// </summary>
    public int OriginalLines { get; }

    /// <summary>
    /// Gets whether stream text was omitted because the shared budget was exhausted.
    /// </summary>
    public bool WasTruncated { get; }

    /// <summary>
    /// Drains one redirected process stream while retaining only text that fits within the shared output budget.
    /// </summary>
    /// <param name="reader">Redirected stream reader to drain.</param>
    /// <param name="budget">Shared combined stdout/stderr capture budget.</param>
    /// <param name="cancellationToken">Token that cancels stream reading.</param>
    /// <returns>The bounded stream capture result.</returns>
    public static async Task<CommandStreamCapture> ReadAsync(
        TextReader reader,
        CommandOutputCaptureBudget budget,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[BufferSize];
        StringBuilder capturedTextBuilder = new();
        long originalBytes = 0;
        int newlineCount = 0;
        bool hasContent = false;
        bool endsWithNewline = false;
        bool wasTruncated = false;

        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);

            if (read == 0)
                break;

            string chunk = new(buffer, 0, read);
            originalBytes += SharedTokenEstimator.GetUtf8ByteCount(chunk);
            hasContent = true;
            endsWithNewline = chunk[^1] == '\n';

            foreach (char character in chunk)
            {
                if (character == '\n')
                    newlineCount++;
            }

            string capturedChunk = budget.CapturePrefix(chunk, out bool chunkWasTruncated);
            capturedTextBuilder.Append(capturedChunk);
            wasTruncated |= chunkWasTruncated;
        }

        int originalLines = newlineCount + (hasContent && !endsWithNewline ? 1 : 0);

        return new CommandStreamCapture(
            capturedTextBuilder.ToString(),
            originalBytes,
            originalLines,
            wasTruncated);
    }
}

/// <summary>
/// Wraps a Windows job object so child processes are terminated when the parent process is closed.
/// </summary>
internal sealed class WindowsProcessJob : IDisposable
{
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private readonly IntPtr _handle;

    private WindowsProcessJob(IntPtr handle)
        =>
        _handle = handle;

    /// <summary>
    /// Tries to create a Windows job object for process-tree cleanup.
    /// </summary>
    /// <returns>The created job object, or <see langword="null"/> when unavailable.</returns>
    public static WindowsProcessJob? TryCreate()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        IntPtr handle = CreateJobObjectW(IntPtr.Zero, null);

        if (handle == IntPtr.Zero)
            return null;

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limitInformation = new()
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JobObjectLimitKillOnJobClose,
            },
        };

        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr limitInformationPointer = Marshal.AllocHGlobal(length);

        try
        {
            Marshal.StructureToPtr(limitInformation, limitInformationPointer, fDeleteOld: false);

            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, limitInformationPointer, (uint)length))
            {
                CloseHandle(handle);
                return null;
            }

            return new WindowsProcessJob(handle);
        }
        finally
        {
            Marshal.FreeHGlobal(limitInformationPointer);
        }
    }

    /// <summary>
    /// Tries to assign a process to the job object.
    /// </summary>
    /// <param name="process">Process to assign.</param>
    public void TryAssign(Process process)
    {
        try
        {
            AssignProcessToJobObject(_handle, process.Handle);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose()
        =>
        CloseHandle(_handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        uint jobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Mirrors the Win32 basic job-object limit information structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        /// <summary>
        /// Gets or sets the per-process user-mode time limit.
        /// </summary>
        public long PerProcessUserTimeLimit;

        /// <summary>
        /// Gets or sets the cumulative per-job user-mode time limit.
        /// </summary>
        public long PerJobUserTimeLimit;

        /// <summary>
        /// Gets or sets the job-object limit flags.
        /// </summary>
        public uint LimitFlags;

        /// <summary>
        /// Gets or sets the minimum working-set size.
        /// </summary>
        public UIntPtr MinimumWorkingSetSize;

        /// <summary>
        /// Gets or sets the maximum working-set size.
        /// </summary>
        public UIntPtr MaximumWorkingSetSize;

        /// <summary>
        /// Gets or sets the maximum number of active processes allowed in the job.
        /// </summary>
        public uint ActiveProcessLimit;

        /// <summary>
        /// Gets or sets the processor affinity mask.
        /// </summary>
        public UIntPtr Affinity;

        /// <summary>
        /// Gets or sets the process priority class.
        /// </summary>
        public uint PriorityClass;

        /// <summary>
        /// Gets or sets the process scheduling class.
        /// </summary>
        public uint SchedulingClass;
    }

    /// <summary>
    /// Mirrors the Win32 job-object I/O counters structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        /// <summary>
        /// Gets or sets the number of read operations.
        /// </summary>
        public ulong ReadOperationCount;

        /// <summary>
        /// Gets or sets the number of write operations.
        /// </summary>
        public ulong WriteOperationCount;

        /// <summary>
        /// Gets or sets the number of non-read and non-write operations.
        /// </summary>
        public ulong OtherOperationCount;

        /// <summary>
        /// Gets or sets the number of bytes transferred by read operations.
        /// </summary>
        public ulong ReadTransferCount;

        /// <summary>
        /// Gets or sets the number of bytes transferred by write operations.
        /// </summary>
        public ulong WriteTransferCount;

        /// <summary>
        /// Gets or sets the number of bytes transferred by other operations.
        /// </summary>
        public ulong OtherTransferCount;
    }

    /// <summary>
    /// Mirrors the Win32 extended job-object limit information structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        /// <summary>
        /// Gets or sets the basic job-object limits.
        /// </summary>
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;

        /// <summary>
        /// Gets or sets the accumulated I/O counters.
        /// </summary>
        public IO_COUNTERS IoInfo;

        /// <summary>
        /// Gets or sets the per-process memory limit.
        /// </summary>
        public UIntPtr ProcessMemoryLimit;

        /// <summary>
        /// Gets or sets the per-job memory limit.
        /// </summary>
        public UIntPtr JobMemoryLimit;

        /// <summary>
        /// Gets or sets the peak memory used by any single process in the job.
        /// </summary>
        public UIntPtr PeakProcessMemoryUsed;

        /// <summary>
        /// Gets or sets the peak memory used by the entire job.
        /// </summary>
        public UIntPtr PeakJobMemoryUsed;
    }
}
