using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Agency.Huddle.App.Gateway;

/// <summary>The real <see cref="ITailscaleCli"/> (D-11): no shell, no window, redirected streams, tree kill, and on Windows a kill-on-close Job object.</summary>
/// <param name="configuredCommand"><c>Team:Gateway:Tailscale:Command</c>, tried after <c>PATH</c> and the platform default.</param>
internal sealed partial class ProcessTailscaleCli(string? configuredCommand = null) : ITailscaleCli
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;
    private static readonly Lazy<nint> Job = new(CreateKillOnCloseJob);

    /// <inheritdoc />
    public async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using Process process = Launch(args);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"tailscale did not finish within {timeout}.", ex);
        }
        finally
        {
            KillIfRunning(process);
        }

        return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited or disposed between the check and the kill: nothing left to release.
        }
    }

    /// <inheritdoc />
    public Task<ITailscaleChild> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        Process process = Launch(args);
        Channel<string> lines = Channel.CreateUnbounded<string>();
        process.OutputDataReceived += (_, e) => Forward(lines, e.Data);
        process.ErrorDataReceived += (_, e) => Forward(lines, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return Task.FromResult<ITailscaleChild>(new Child(process, lines));
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObject(nint attributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(nint job, int informationClass, ref ExtendedLimit information, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(nint job, nint process);

    private static void Forward(Channel<string> lines, string? data)
    {
        if (data is not null)
        {
            lines.Writer.TryWrite(data);
        }
    }

    private static nint CreateKillOnCloseJob()
    {
        nint job = CreateJobObject(0, null);
        if (job == 0)
        {
            return 0;
        }

        ExtendedLimit information = default;
        information.LimitFlags = JobObjectLimitKillOnJobClose;
        return SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref information, (uint)Marshal.SizeOf<ExtendedLimit>()) ? job : 0;
    }

    private static string DefaultPath()
        => DefaultPath(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

    /// <summary>Where the Tailscale installer puts the CLI on each platform.</summary>
    /// <param name="windows">Whether the host is Windows.</param>
    /// <param name="macOs">Whether the host is macOS.</param>
    /// <param name="programFiles">The Program Files folder, used on Windows.</param>
    /// <returns>The default path of the Tailscale CLI.</returns>
    internal static string DefaultPath(bool windows, bool macOs, string programFiles)
    {
        if (windows)
        {
            return Path.Combine(programFiles, "Tailscale", "tailscale.exe");
        }

        return macOs ? "/Applications/Tailscale.app/Contents/MacOS/Tailscale" : "/usr/bin/tailscale";
    }

    private static string? FromPath()
    {
        string file = OperatingSystem.IsWindows() ? "tailscale.exe" : "tailscale";
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private Process Launch(IReadOnlyList<string> args)
    {
        string executable = FromPath() ?? (File.Exists(DefaultPath()) ? DefaultPath() : configuredCommand) ?? "tailscale";
        ProcessStartInfo info = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        Process process = Process.Start(info) ?? throw new InvalidOperationException("tailscale did not start.");
        if (OperatingSystem.IsWindows() && Job.Value != 0)
        {
            _ = AssignProcessToJobObject(Job.Value, process.Handle);
        }

        return process;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    private sealed class Child : ITailscaleChild
    {
        private readonly Process process;
        private readonly Channel<string> lines;

        public Child(Process process, Channel<string> lines)
        {
            this.process = process;
            this.lines = lines;
            this.Id = process.Id;
            this.Exited = Task.Run(async () =>
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                lines.Writer.TryComplete();
                int exitCode = process.ExitCode;
                process.Dispose();
                return exitCode;
            });
        }

        public int Id { get; }

        public IAsyncEnumerable<string> Lines => this.lines.Reader.ReadAllAsync();

        public Task<int> Exited { get; }

        public ValueTask KillAsync()
        {
            try
            {
                this.process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited: nothing left to release.
            }

            return ValueTask.CompletedTask;
        }
    }
}
