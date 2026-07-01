using CodeSnifferDog.Models.Common.Tools;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

internal sealed class WindowsProcessJob : IDisposable
{
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private readonly IntPtr _handle;

    private WindowsProcessJob(IntPtr handle)
        =>
        _handle = handle;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
