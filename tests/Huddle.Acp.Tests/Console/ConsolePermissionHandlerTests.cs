namespace Agency.Huddle.Acp.Tests.Console;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.Console;
using Agency.Huddle.Console.Terminal;
using Xunit;

public sealed class ConsolePermissionHandlerTests
{
    [Fact(Timeout = 10000)]
    public async Task Decide_RendersTitleKindAndNumberedOptions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("1");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        _ = await handler.DecideAsync(CreateContext(), cancellationToken);

        Assert.Contains("Permission requested: [edit] Write hello.txt", output.Lines);
        Assert.Contains("  input: {\"path\":\"hello.txt\"}", output.Lines);
        Assert.Contains("  1) Allow once", output.Lines);
        Assert.Contains("  2) Allow always", output.Lines);
        Assert.Contains("  3) Reject", output.Lines);
        Assert.Contains(("Choose [1-3]: ", ConsoleStyle.Prompt), output.Segments);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_ValidNumber_ReturnsSelected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("2");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        PermissionDecision decision = await handler.DecideAsync(CreateContext(), cancellationToken);

        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("always", selected.OptionId);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_InvalidThenValid_RepromptsOnce()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("x");
        input.Lines.Enqueue("1");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        PermissionDecision decision = await handler.DecideAsync(CreateContext(), cancellationToken);

        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("allow", selected.OptionId);
        Assert.Equal(1, CountOccurrences(output.Text, "Invalid choice."));
        Assert.Equal(2, CountOccurrences(output.Text, "Choose [1-3]: "));
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_OutOfRange_Reprompts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("9");
        input.Lines.Enqueue("3");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        PermissionDecision decision = await handler.DecideAsync(CreateContext(), cancellationToken);

        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("reject", selected.OptionId);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_Eof_ReturnsCancelled()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        PermissionDecision decision = await handler.DecideAsync(CreateContext(), cancellationToken);

        Assert.Same(PermissionDecision.Cancelled, decision);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_CancellationToken_ReturnsCancelled()
    {
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);
        CancellationToken cancelledToken = new CancellationToken(canceled: true);

        PermissionDecision decision = await handler.DecideAsync(CreateContext(), cancelledToken)
            .WaitAsync(TestContext.Current.CancellationToken);

        Assert.Same(PermissionDecision.Cancelled, decision);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_DiscardsPendingInputBeforePrompt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("stray");
        input.Lines.Enqueue("1");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        _ = await handler.DecideAsync(CreateContext(), cancellationToken);

        Assert.Equal(1, input.DiscardCount);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_WhenDiscardDisabled_DoesNotDiscardPendingInput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("2");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output, discardPendingInput: false);

        PermissionDecision decision = await handler.DecideAsync(CreateContext(), cancellationToken);

        Assert.Equal(0, input.DiscardCount);
        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("always", selected.OptionId);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_ConcurrentRequests_Serialized()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.HoldNextRead();
        input.Lines.Enqueue("1");
        input.Lines.Enqueue("1");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        Task<PermissionDecision> firstTask = handler.DecideAsync(CreateContext(), cancellationToken);

        bool firstRendered = SpinWait.SpinUntil(
            () => CountOccurrences(output.Text, "Permission requested:") == 1,
            TimeSpan.FromSeconds(2));
        Assert.True(firstRendered, "Timed out waiting for the first prompt to render.");

        Task<PermissionDecision> secondTask = handler.DecideAsync(CreateContext(), cancellationToken);

        // The second call is queued behind the handler's serialising gate; give it a bounded
        // chance to run and confirm it has NOT rendered while the first decision is still open.
        await Task.Delay(100, cancellationToken);
        Assert.Equal(1, CountOccurrences(output.Text, "Permission requested:"));

        input.ReleaseHold();

        PermissionDecision firstDecision = await firstTask.WaitAsync(cancellationToken);
        PermissionDecision secondDecision = await secondTask.WaitAsync(cancellationToken);

        Assert.IsType<SelectedDecision>(firstDecision);
        Assert.IsType<SelectedDecision>(secondDecision);
        Assert.Equal(2, CountOccurrences(output.Text, "Permission requested:"));
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_TruncatesRawInputTo500Chars()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string rawInput = new string('a', 600);
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("1");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        _ = await handler.DecideAsync(CreateContext(rawInput), cancellationToken);

        string expected = "  input: " + new string('a', 500) + "…";
        Assert.Contains(expected, output.Lines);
    }

    [Fact(Timeout = 10000)]
    public async Task Decide_NoRawInput_OmitsInputLine()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ScriptedConsoleInput input = new ScriptedConsoleInput();
        input.Lines.Enqueue("1");
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsolePermissionHandler handler = new ConsolePermissionHandler(input, output);

        _ = await handler.DecideAsync(CreateContext(rawInputJson: null), cancellationToken);

        Assert.DoesNotContain(output.Lines, line => line.StartsWith("  input:", StringComparison.Ordinal));
    }

    private static PermissionRequestContext CreateContext(string? rawInputJson = "{\"path\":\"hello.txt\"}")
    {
        ToolCallInfo toolCall = new ToolCallInfo("call-1", "Write hello.txt", ToolKind.Edit, ToolCallStatus.Pending, rawInputJson);
        List<PermissionOptionInfo> options = new List<PermissionOptionInfo>
        {
            new PermissionOptionInfo("allow", "Allow once", PermissionOptionKind.AllowOnce),
            new PermissionOptionInfo("always", "Allow always", PermissionOptionKind.AllowAlways),
            new PermissionOptionInfo("reject", "Reject", PermissionOptionKind.RejectOnce),
        };
        return new PermissionRequestContext("sess-1", toolCall, options);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
