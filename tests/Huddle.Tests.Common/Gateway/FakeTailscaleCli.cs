using System.ComponentModel;
using System.Threading.Channels;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>One call a <see cref="FakeTailscaleCli"/> saw.</summary>
/// <param name="Args">The arguments, without the executable.</param>
/// <param name="Timeout">The timeout passed to <c>RunAsync</c>, or <see langword="null"/> for a started child.</param>
/// <param name="At">The clock time of the call.</param>
internal sealed record TailscaleCall(IReadOnlyList<string> Args, TimeSpan? Timeout, DateTimeOffset At)
{
    /// <summary>The arguments joined by single spaces.</summary>
    public string Text => string.Join(' ', this.Args);
}

/// <summary>A scripted <see cref="ITailscaleChild"/>: the test emits lines, exits it and counts kills.</summary>
/// <param name="id">The fake process id.</param>
/// <param name="clock">The clock that stamps the start and the exit.</param>
internal sealed class FakeTailscaleChild(int id, TimeProvider clock) : ITailscaleChild
{
    private readonly Channel<string> lines = Channel.CreateUnbounded<string>();
    private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int killCount;

    /// <inheritdoc />
    public int Id => id;

    /// <inheritdoc />
    public IAsyncEnumerable<string> Lines => this.lines.Reader.ReadAllAsync();

    /// <inheritdoc />
    public Task<int> Exited => this.exited.Task;

    /// <summary>The clock time the child was started.</summary>
    public DateTimeOffset StartedAt { get; } = clock.GetUtcNow();

    /// <summary>The clock time the child exited, or <see langword="null"/> while it runs.</summary>
    public DateTimeOffset? ExitedAt { get; private set; }

    /// <summary>How many times <see cref="KillAsync"/> was called.</summary>
    public int KillCount => Volatile.Read(ref this.killCount);

    /// <summary>Whether the process has exited.</summary>
    public bool HasExited => this.exited.Task.IsCompleted;

    /// <summary>Writes one output line.</summary>
    /// <param name="line">The line.</param>
    public void Emit(string line) => this.lines.Writer.TryWrite(line);

    /// <summary>Ends the process with <paramref name="code"/> and closes its output.</summary>
    /// <param name="code">The exit code.</param>
    public void Exit(int code = 0)
    {
        this.ExitedAt ??= clock.GetUtcNow();
        this.lines.Writer.TryComplete();
        this.exited.TrySetResult(code);
    }

    /// <inheritdoc />
    public ValueTask KillAsync()
    {
        Interlocked.Increment(ref this.killCount);
        this.Exit(-1);
        return ValueTask.CompletedTask;
    }
}

/// <summary>An <see cref="ITailscaleCli"/> with canned answers; it records every call and hands out <see cref="FakeTailscaleChild"/> instances.</summary>
/// <param name="clock">The clock that stamps calls, or <see langword="null"/> for the system clock.</param>
internal sealed class FakeTailscaleCli(TimeProvider? clock = null) : ITailscaleCli
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly Lock gate = new();
    private readonly Dictionary<string, (int Exit, string Out, string Err)> answers = new(StringComparer.Ordinal);
    private readonly HashSet<string> timeouts = new(StringComparer.Ordinal);
    private readonly List<TailscaleCall> runs = [];
    private readonly List<TailscaleCall> starts = [];
    private readonly List<FakeTailscaleChild> children = [];
    private bool missing;

    /// <summary>Runs a started child through this script right after it is created (before <c>StartAsync</c> returns).</summary>
    public Action<FakeTailscaleChild>? OnChildStarted { get; set; }

    /// <summary>The <c>RunAsync</c> calls, in order.</summary>
    public IReadOnlyList<TailscaleCall> Runs
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.runs];
            }
        }
    }

    /// <summary>The <c>StartAsync</c> calls, in order.</summary>
    public IReadOnlyList<TailscaleCall> Starts
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.starts];
            }
        }
    }

    /// <summary>The children handed out, in order.</summary>
    public IReadOnlyList<FakeTailscaleChild> Children
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.children];
            }
        }
    }

    /// <summary>Answers a <c>RunAsync</c> whose arguments joined by spaces equal <paramref name="argsText"/>.</summary>
    /// <param name="argsText">For example <c>status --json</c>.</param>
    /// <param name="stdout">The standard output.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="stderr">The standard error.</param>
    public FakeTailscaleCli Respond(string argsText, string stdout, int exitCode = 0, string stderr = "")
    {
        lock (this.gate)
        {
            this.answers[argsText] = (exitCode, stdout, stderr);
            this.timeouts.Remove(argsText);
        }

        return this;
    }

    /// <summary>Makes a <c>RunAsync</c> with these arguments throw <see cref="TimeoutException"/>.</summary>
    /// <param name="argsText">The joined arguments.</param>
    public FakeTailscaleCli TimeOut(string argsText)
    {
        lock (this.gate)
        {
            this.timeouts.Add(argsText);
        }

        return this;
    }

    /// <summary>Makes every call throw <see cref="Win32Exception"/>, as a missing executable does.</summary>
    public FakeTailscaleCli MissingCli()
    {
        lock (this.gate)
        {
            this.missing = true;
        }

        return this;
    }

    /// <inheritdoc />
    public Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        TailscaleCall call = new(args, timeout, this.clock.GetUtcNow());
        lock (this.gate)
        {
            this.runs.Add(call);
            if (this.missing)
            {
                throw new Win32Exception(2, "tailscale not found");
            }

            if (this.timeouts.Contains(call.Text))
            {
                throw new TimeoutException();
            }

            (int Exit, string Out, string Err) answer = this.answers.TryGetValue(call.Text, out (int Exit, string Out, string Err) found) ? found : (1, string.Empty, "unscripted");
            return Task.FromResult((answer.Exit, answer.Out, answer.Err));
        }
    }

    /// <inheritdoc />
    public Task<ITailscaleChild> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        FakeTailscaleChild child;
        lock (this.gate)
        {
            this.starts.Add(new TailscaleCall(args, null, this.clock.GetUtcNow()));
            if (this.missing)
            {
                throw new Win32Exception(2, "tailscale not found");
            }

            child = new FakeTailscaleChild(1000 + this.children.Count, this.clock);
            this.children.Add(child);
        }

        this.OnChildStarted?.Invoke(child);
        return Task.FromResult<ITailscaleChild>(child);
    }
}
