using CodeSnifferDog.Models.Common.Tools;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

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

        return new CommandExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await standardOutputTask.ConfigureAwait(false),
            StandardError = await standardErrorTask.ConfigureAwait(false),
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
