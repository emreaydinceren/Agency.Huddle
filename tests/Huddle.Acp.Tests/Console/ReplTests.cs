namespace Agency.Huddle.Acp.Tests.Console;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.Console;
using Agency.Huddle.Console.Terminal;
using Xunit;

public sealed class ReplTests
{
    [Fact(Timeout = 10000)]
    public async Task ExitCommand_StopsLoop_AndDisposesHost()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, _, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession session = new FakeAgentSession();
        host.OnStartSession = _ => Task.FromResult<IAgentSession>(session);
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(host.Disposed);
        Assert.True(session.Disposed);
    }

    [Fact(Timeout = 10000)]
    public async Task EofInput_StopsLoop()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (_, _, _, _, _, Repl repl) = ReplTests.CreateHarness();

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
    }

    [Fact(Timeout = 10000)]
    public async Task PlainLine_SendsPrompt_RendersEvents_ThenShowsPromptAgain()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession session = new FakeAgentSession();
        host.OnStartSession = _ => Task.FromResult<IAgentSession>(session);
        session.OnPrompt = _ =>
        {
            session.Channel.Writer.TryWrite(new MessageChunk(session.SessionId, "Hello"));
            session.Channel.Writer.TryWrite(new TurnCompleted(session.SessionId, StopReason.EndTurn));
            return Task.FromResult(new PromptResult(StopReason.EndTurn));
        };
        input.Lines.Enqueue("hi");
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { "hi" }, session.Prompts);
        Assert.Contains("Hello", output.Text, StringComparison.Ordinal);
        Assert.Contains("(stop: EndTurn)", output.Text, StringComparison.Ordinal);
        int promptCount = output.Segments.Count(segment => segment.Text == "> " && segment.Style == ConsoleStyle.Prompt);
        Assert.Equal(2, promptCount);
    }

    [Fact(Timeout = 10000)]
    public async Task EmptyLine_IsIgnored()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, _, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession session = new FakeAgentSession();
        host.OnStartSession = _ => Task.FromResult<IAgentSession>(session);
        input.Lines.Enqueue(string.Empty);
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Empty(session.Prompts);
    }

    [Fact(Timeout = 10000)]
    public async Task HelpCommand_PrintsCommands()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (_, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        input.Lines.Enqueue("/help");
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, ReplTests.CountOccurrences(output.Text, "/exit, /new, /help"));
    }

    [Fact(Timeout = 10000)]
    public async Task NewCommand_DisposesOldSession_StartsNew()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, _, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession firstSession = new FakeAgentSession("sess-1");
        FakeAgentSession secondSession = new FakeAgentSession("sess-2");
        int startCount = 0;
        host.OnStartSession = _ =>
        {
            startCount++;
            IAgentSession session = startCount == 1 ? firstSession : secondSession;
            return Task.FromResult(session);
        };
        input.Lines.Enqueue("/new");
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, host.SessionRequests.Count);
        Assert.True(firstSession.Disposed);
    }

    [Fact(Timeout = 10000)]
    public async Task Interrupt_WhileInFlight_CallsCancelAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession session = new FakeAgentSession();
        host.OnStartSession = _ => Task.FromResult<IAgentSession>(session);
        session.OnPrompt = async _ =>
        {
            while (session.CancelCount == 0)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            session.Channel.Writer.TryWrite(new TurnCompleted(session.SessionId, StopReason.Cancelled));
            return new PromptResult(StopReason.Cancelled);
        };
        input.Lines.Enqueue("hi");

        Task<int> runTask = repl.RunAsync(cancellationToken);
        await ReplTests.WaitUntilAsync(() => session.Prompts.Count > 0, cancellationToken);

        repl.Interrupt();

        int exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal(1, session.CancelCount);
        Assert.Contains("(stop: Cancelled)", output.Text, StringComparison.Ordinal);
        Assert.Equal(0, exitCode);
    }

    [Fact(Timeout = 10000)]
    public async Task Interrupt_WhileIdle_Exits()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (_, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        input.HoldNextRead();

        Task<int> runTask = repl.RunAsync(cancellationToken);
        await ReplTests.WaitUntilAsync(() => output.Text.Contains("> ", StringComparison.Ordinal), cancellationToken);

        repl.Interrupt();

        int exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal(0, exitCode);
    }

    [Fact(Timeout = 10000)]
    public async Task AuthRequired_PrintsClaudeLoginHint_ReturnsExitCode2()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, _, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        host.OnStartSession = _ => throw new AgentAuthenticationRequiredException(
            new[] { new AuthMethodInfo("claude-login", "Log in with Claude Code", null) });

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Contains("claude login", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task AgentException_DuringPrompt_PrintsErrorAndContinues()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession session = new FakeAgentSession();
        host.OnStartSession = _ => Task.FromResult<IAgentSession>(session);
        session.OnPrompt = _ =>
        {
            session.Channel.Writer.TryComplete();
            throw new AgentException("boom");
        };
        input.Lines.Enqueue("hi");
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Error: boom", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task AgentDisconnected_DuringPrompt_ReturnsOne()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        FakeAgentSession session = new FakeAgentSession();
        host.OnStartSession = _ => Task.FromResult<IAgentSession>(session);
        session.OnPrompt = _ =>
        {
            session.Channel.Writer.TryComplete();
            throw new AgentDisconnectedException();
        };
        input.Lines.Enqueue("hi");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("Agent disconnected.", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task AuthMethodsAdvertised_PrintsInfoHint()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, RecordingConsoleOutput output, _, _, Repl repl) = ReplTests.CreateHarness();
        host.Info = new AgentHostInfo(
            "fake-agent",
            "0.0.1",
            1,
            new[] { new AuthMethodInfo("claude-login", "Log in with Claude Code", null) },
            false);
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Agent advertises auth methods", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task StartSession_PassesCwdAndHandler()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, _, RecordingPermissionHandler permissionHandler, string cwd, Repl repl) = ReplTests.CreateHarness();
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        AgentSessionOptions options = Assert.Single(host.SessionRequests);
        Assert.Equal(cwd, options.Cwd);
        Assert.Same(permissionHandler, options.PermissionHandler);
    }

    [Fact(Timeout = 10000)]
    public async Task StartSession_WithSystemPrompt_PassesSameInstanceToAgentSessionOptions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");
        (FakeAgentHost host, ScriptedConsoleInput input, _, _, _, Repl repl) = ReplTests.CreateHarness(systemPrompt);
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        AgentSessionOptions options = Assert.Single(host.SessionRequests);
        Assert.Same(systemPrompt, options.SystemPrompt);
    }

    [Fact(Timeout = 10000)]
    public async Task StartSession_WithoutSystemPrompt_CapturedOptionsCarryNull()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (FakeAgentHost host, ScriptedConsoleInput input, _, _, _, Repl repl) = ReplTests.CreateHarness();
        input.Lines.Enqueue("/exit");

        int exitCode = await repl.RunAsync(cancellationToken);

        Assert.Equal(0, exitCode);
        AgentSessionOptions options = Assert.Single(host.SessionRequests);
        Assert.Null(options.SystemPrompt);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeoutSource.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeoutSource.Token).ConfigureAwait(false);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static (FakeAgentHost Host, ScriptedConsoleInput Input, RecordingConsoleOutput Output, RecordingPermissionHandler PermissionHandler, string Cwd, Repl Repl) CreateHarness(SystemPromptOptions? systemPrompt = null)
    {
        FakeAgentHost host = new FakeAgentHost();
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);
        RecordingPermissionHandler permissionHandler = new RecordingPermissionHandler();
        string cwd = Path.GetTempPath();
        ReplOptions options = new ReplOptions(cwd, false, systemPrompt);
        Repl repl = new Repl(host, input, output, renderer, permissionHandler, options, new ListLogger<Repl>());
        return (host, input, output, permissionHandler, cwd, repl);
    }
}
