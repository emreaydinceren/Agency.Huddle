namespace Agency.Huddle.Acp.Tests.Console;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.Console;
using Agency.Huddle.Console.Terminal;
using Xunit;

public sealed class ConsoleRendererTests
{
    [Fact(Timeout = 10000)]
    public async Task MessageChunks_ConcatenatedWithoutNewline()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new MessageChunk("sess-1", "Hel"));
        channel.Writer.TryWrite(new MessageChunk("sess-1", "lo"));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.StartsWith("Hello", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task ChunkThenToolCall_InsertsNewline()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new MessageChunk("sess-1", "Hello"));
        channel.Writer.TryWrite(new ToolCallStarted("sess-1", "call-1", "Read x", ToolKind.Read, ToolCallStatus.Pending, null));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Equal("Hello\n[tool:read] Read x (pending)\n", output.Text);
    }

    [Fact(Timeout = 10000)]
    public async Task Thought_UsesThoughtStyleAndPrefix()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new ThoughtChunk("sess-1", "thinking"));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Contains("[thought] thinking", output.Text, StringComparison.Ordinal);
        Assert.Contains(("[thought] ", ConsoleStyle.Thought), output.Segments);
        Assert.Contains(("thinking", ConsoleStyle.Thought), output.Segments);
    }

    [Fact(Timeout = 10000)]
    public async Task ToolCallUpdated_PrintsOnlyWhenStatusChanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new ToolCallStarted("sess-1", "call-1", "Read x", ToolKind.Read, ToolCallStatus.Pending, null));
        channel.Writer.TryWrite(new ToolCallUpdated("sess-1", "call-1", "Read x", ToolKind.Read, ToolCallStatus.Pending, null));
        channel.Writer.TryWrite(new ToolCallUpdated("sess-1", "call-1", "Read x", ToolKind.Read, ToolCallStatus.Completed, null));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Equal(1, output.Lines.Count(line => line.Contains("-> completed", StringComparison.Ordinal)));
        Assert.DoesNotContain(output.Lines, line => line.Contains("-> pending", StringComparison.Ordinal));
    }

    [Fact(Timeout = 10000)]
    public async Task PlanUpdated_PrintsEntriesWithStatusMarkers()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);
        List<PlanEntryInfo> entries = new List<PlanEntryInfo>
        {
            new PlanEntryInfo("a", PlanEntryPriority.Medium, PlanEntryStatus.Pending),
            new PlanEntryInfo("b", PlanEntryPriority.Medium, PlanEntryStatus.InProgress),
            new PlanEntryInfo("c", PlanEntryPriority.Medium, PlanEntryStatus.Completed),
        };

        channel.Writer.TryWrite(new PlanUpdated("sess-1", entries));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Contains("[plan]", output.Lines);
        Assert.Contains("  [ ] a", output.Lines);
        Assert.Contains("  [~] b", output.Lines);
        Assert.Contains("  [x] c", output.Lines);
    }

    [Fact(Timeout = 10000)]
    public async Task UsageUpdated_PrintsOneLine()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new UsageUpdated("sess-1", 1000, 250));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Contains("[usage] 250/1000", output.Lines);
    }

    [Fact(Timeout = 10000)]
    public async Task UnsupportedContent_PrintsPlaceholder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new UnsupportedContent("sess-1", "image"));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Contains("[unsupported content: image]", output.Lines);
    }

    [Fact(Timeout = 10000)]
    public async Task TurnCompleted_ReturnsStopReason_AndStopsReading()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));
        channel.Writer.TryWrite(new MessageChunk("sess-1", "should-not-render"));

        StopReason? result = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Equal(StopReason.EndTurn, result);
        Assert.DoesNotContain("should-not-render", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task TurnCompleted_AfterText_EndsLine()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new MessageChunk("sess-1", "Hello"));
        channel.Writer.TryWrite(new TurnCompleted("sess-1", StopReason.EndTurn));

        _ = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.EndsWith("\n", output.Text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10000)]
    public async Task ChannelCompleted_ReturnsNull()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.TryWrite(new MessageChunk("sess-1", "Hello"));
        channel.Writer.Complete();

        StopReason? result = await renderer.RenderTurnAsync(channel.Reader, cancellationToken);

        Assert.Null(result);
    }

    [Fact(Timeout = 10000)]
    public async Task ChannelFaulted_Throws()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>();
        RecordingConsoleOutput output = new RecordingConsoleOutput();
        ConsoleRenderer renderer = new ConsoleRenderer(output);

        channel.Writer.Complete(new AgentDisconnectedException());

        Exception thrown = await Assert.ThrowsAnyAsync<Exception>(
            () => renderer.RenderTurnAsync(channel.Reader, cancellationToken));

        Exception actual = thrown is ChannelClosedException closed && closed.InnerException is not null
            ? closed.InnerException
            : thrown;
        Assert.IsType<AgentDisconnectedException>(actual);
    }
}
