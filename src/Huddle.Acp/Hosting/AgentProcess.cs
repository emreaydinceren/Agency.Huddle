namespace Agency.Huddle.Acp.Hosting;

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

internal sealed partial class AgentProcess : IAgentProcess
{
    private readonly Process process;

    private readonly ILogger logger;

    private readonly TaskCompletionSource<int> exitedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool disposed;

    internal AgentProcess(Process process, ILogger logger)
    {
        this.process = process;
        this.logger = logger;

        this.process.EnableRaisingEvents = true;
        this.process.Exited += this.OnProcessExited;

        if (this.process.HasExited)
        {
            this.exitedSource.TrySetResult(this.SafeExitCode());
        }

        _ = Task.Run(this.DrainStandardErrorAsync);
    }

    public Stream StandardInput => this.process.StandardInput.BaseStream;

    public Stream StandardOutput => this.process.StandardOutput.BaseStream;

    public Task<int> Exited => this.exitedSource.Task;

    public void Kill()
    {
        try
        {
            this.process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.process.Exited -= this.OnProcessExited;
        this.process.Dispose();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        this.exitedSource.TrySetResult(this.SafeExitCode());
    }

    private int SafeExitCode()
    {
        try
        {
            return this.process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private async Task DrainStandardErrorAsync()
    {
        try
        {
            while (true)
            {
                string? line = await this.process.StandardError.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                AgentProcess.LogAgentStderr(this.logger, line);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException || ex is IOException)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[agent stderr] {Line}")]
    private static partial void LogAgentStderr(ILogger logger, string line);
}