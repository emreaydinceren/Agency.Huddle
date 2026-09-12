namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

public sealed class DotAcpClientAdapterTests
{
    private const string SessionId = "sess-1";

    [Fact(Timeout = 10000)]
    public async Task SessionUpdate_KnownSession_PublishesMappedEvent()
    {
        Fixture fixture = CreateFixture();
        dotacp.protocol.SessionNotification notification = CreateMessageChunkNotification(DotAcpClientAdapterTests.SessionId, "hi");

        await fixture.Adapter.SessionUpdateAsync(notification, TestContext.Current.CancellationToken);

        AgentEvent published = Assert.Single(fixture.Sink.Events);
        Assert.Equal(new MessageChunk(DotAcpClientAdapterTests.SessionId, "hi"), published);
    }

    [Fact(Timeout = 10000)]
    public async Task SessionUpdate_UnknownSession_DropsAndLogsWarning()
    {
        Fixture fixture = CreateFixture();
        dotacp.protocol.SessionNotification notification = CreateMessageChunkNotification("unknown-session", "hi");

        await fixture.Adapter.SessionUpdateAsync(notification, TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Sink.Events);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact(Timeout = 10000)]
    public async Task SessionUpdate_100Chunks_PreservesOrder()
    {
        Fixture fixture = CreateFixture();
        List<Task> tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            dotacp.protocol.SessionNotification notification = CreateMessageChunkNotification(
                DotAcpClientAdapterTests.SessionId,
                i.ToString(CultureInfo.InvariantCulture));

            tasks.Add(fixture.Adapter.SessionUpdateAsync(notification, TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        IReadOnlyList<AgentEvent> events = fixture.Sink.Events;
        Assert.Equal(100, events.Count);
        for (int i = 0; i < 100; i++)
        {
            MessageChunk chunk = Assert.IsType<MessageChunk>(events[i]);
            Assert.Equal(i.ToString(CultureInfo.InvariantCulture), chunk.Text);
        }
    }

    [Fact(Timeout = 10000)]
    public async Task RequestPermission_ForwardsContextAndReturnsSelectedOutcome()
    {
        Fixture fixture = CreateFixture();
        RecordingPermissionHandler handler = new RecordingPermissionHandler
        {
            Decision = new SelectedDecision("always"),
        };
        fixture.Sink.PermissionHandler = handler;

        dotacp.protocol.RequestPermissionResponse response = await fixture.Adapter.RequestPermissionAsync(
            CreatePermissionRequest(DotAcpClientAdapterTests.SessionId), TestContext.Current.CancellationToken);

        dotacp.protocol.SelectedPermissionOutcome selected = Assert.IsType<dotacp.protocol.SelectedPermissionOutcome>(response.Outcome);
        Assert.Equal("always", (string)selected.OptionId);
        PermissionRequestContext context = Assert.Single(handler.Requests);
        Assert.Equal(3, context.Options.Count);
    }

    [Fact(Timeout = 10000)]
    public async Task RequestPermission_HandlerCancelled_ReturnsCancelledOutcome()
    {
        Fixture fixture = CreateFixture();
        RecordingPermissionHandler handler = new RecordingPermissionHandler
        {
            Decision = PermissionDecision.Cancelled,
        };
        fixture.Sink.PermissionHandler = handler;

        dotacp.protocol.RequestPermissionResponse response = await fixture.Adapter.RequestPermissionAsync(
            CreatePermissionRequest(DotAcpClientAdapterTests.SessionId), TestContext.Current.CancellationToken);

        Assert.IsType<dotacp.protocol.RequestPermissionOutcomeCancelled>(response.Outcome);
    }

    [Fact(Timeout = 10000)]
    public async Task RequestPermission_PromptCancellationFires_ReturnsCancelledWithoutHandlerCompleting()
    {
        Fixture fixture = CreateFixture();
        RecordingPermissionHandler handler = new RecordingPermissionHandler
        {
            Gate = new TaskCompletionSource(),
        };
        fixture.Sink.PermissionHandler = handler;

        Task<dotacp.protocol.RequestPermissionResponse> responseTask = fixture.Adapter.RequestPermissionAsync(
            CreatePermissionRequest(DotAcpClientAdapterTests.SessionId), TestContext.Current.CancellationToken);

        fixture.Sink.PromptCts.Cancel();

        dotacp.protocol.RequestPermissionResponse response = await responseTask.WaitAsync(
            TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.IsType<dotacp.protocol.RequestPermissionOutcomeCancelled>(response.Outcome);
        Assert.False(handler.Gate.Task.IsCompleted);
    }

    [Fact(Timeout = 10000)]
    public async Task RequestPermission_UnknownSession_ReturnsCancelledOutcome()
    {
        Fixture fixture = CreateFixture();

        dotacp.protocol.RequestPermissionResponse response = await fixture.Adapter.RequestPermissionAsync(
            CreatePermissionRequest("unknown-session"), TestContext.Current.CancellationToken);

        Assert.IsType<dotacp.protocol.RequestPermissionOutcomeCancelled>(response.Outcome);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact(Timeout = 10000)]
    public async Task RequestPermission_HandlerThrows_ReturnsCancelledAndLogsError()
    {
        Fixture fixture = CreateFixture();
        RecordingPermissionHandler handler = new RecordingPermissionHandler
        {
            ThrowOnDecide = true,
        };
        fixture.Sink.PermissionHandler = handler;

        dotacp.protocol.RequestPermissionResponse response = await fixture.Adapter.RequestPermissionAsync(
            CreatePermissionRequest(DotAcpClientAdapterTests.SessionId), TestContext.Current.CancellationToken);

        Assert.IsType<dotacp.protocol.RequestPermissionOutcomeCancelled>(response.Outcome);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact(Timeout = 10000)]
    public async Task ReadTextFile_ThrowsNotSupported()
    {
        Fixture fixture = CreateFixture();
        dotacp.protocol.ReadTextFileRequest request = new dotacp.protocol.ReadTextFileRequest
        {
            SessionId = DotAcpClientAdapterTests.SessionId,
            Path = "E:\\x\\a.txt",
            Line = 2,
            Limit = 3,
        };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => fixture.Adapter.ReadTextFileAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 10000)]
    public async Task WriteTextFile_ThrowsNotSupported()
    {
        Fixture fixture = CreateFixture();
        dotacp.protocol.WriteTextFileRequest request = new dotacp.protocol.WriteTextFileRequest
        {
            SessionId = DotAcpClientAdapterTests.SessionId,
            Path = "E:\\x\\b.txt",
            Content = "hello",
        };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => fixture.Adapter.WriteTextFileAsync(request, TestContext.Current.CancellationToken));
    }

    public static TheoryData<string> TerminalMethodCalls()
    {
        return new TheoryData<string>
        {
            "CreateTerminalAsync",
            "KillTerminalAsync",
            "TerminalOutputAsync",
            "ReleaseTerminalAsync",
            "WaitForTerminalExitAsync",
        };
    }

    [Theory(Timeout = 10000)]
    [MemberData(nameof(TerminalMethodCalls))]
    public async Task TerminalMethods_ThrowNotSupported(string methodName)
    {
        Fixture fixture = CreateFixture();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Func<Task> invoke = methodName switch
        {
            "CreateTerminalAsync" => () => fixture.Adapter.CreateTerminalAsync(
                new dotacp.protocol.CreateTerminalRequest
                {
                    SessionId = DotAcpClientAdapterTests.SessionId,
                    Command = "node",
                    Cwd = "E:\\x",
                },
                cancellationToken),
            "KillTerminalAsync" => () => fixture.Adapter.KillTerminalAsync(
                new dotacp.protocol.KillTerminalRequest
                {
                    SessionId = DotAcpClientAdapterTests.SessionId,
                    TerminalId = "term-1",
                },
                cancellationToken),
            "TerminalOutputAsync" => () => fixture.Adapter.TerminalOutputAsync(
                new dotacp.protocol.TerminalOutputRequest
                {
                    SessionId = DotAcpClientAdapterTests.SessionId,
                    TerminalId = "term-1",
                },
                cancellationToken),
            "ReleaseTerminalAsync" => () => fixture.Adapter.ReleaseTerminalAsync(
                new dotacp.protocol.ReleaseTerminalRequest
                {
                    SessionId = DotAcpClientAdapterTests.SessionId,
                    TerminalId = "term-1",
                },
                cancellationToken),
            "WaitForTerminalExitAsync" => () => fixture.Adapter.WaitForTerminalExitAsync(
                new dotacp.protocol.WaitForTerminalExitRequest
                {
                    SessionId = DotAcpClientAdapterTests.SessionId,
                    TerminalId = "term-1",
                },
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown terminal method name."),
        };

        await Assert.ThrowsAsync<NotSupportedException>(invoke);
    }

    [Fact(Timeout = 10000)]
    public async Task ExtMethod_ThrowsNotSupported()
    {
        Fixture fixture = CreateFixture();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => fixture.Adapter.ExtMethodAsync("_ext/foo", new object(), TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 10000)]
    public async Task ExtNotification_DoesNotThrow()
    {
        Fixture fixture = CreateFixture();

        Exception? exception = await Record.ExceptionAsync(
            () => fixture.Adapter.ExtNotificationAsync(
                "_auth/status_update", new object(), TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }

    [Fact]
    public void OnDisconnected_FaultsAllSinksWithAgentDisconnectedException()
    {
        ListLogger<DotAcpClientAdapter> logger = new ListLogger<DotAcpClientAdapter>();
        DotAcpClientAdapter adapter = new DotAcpClientAdapter(logger);
        FakeSessionSink sinkOne = new FakeSessionSink("sess-1");
        FakeSessionSink sinkTwo = new FakeSessionSink("sess-2");
        adapter.Register(sinkOne);
        adapter.Register(sinkTwo);

        adapter.OnDisconnected(null!);

        Assert.IsType<AgentDisconnectedException>(sinkOne.FaultException);
        Assert.IsType<AgentDisconnectedException>(sinkTwo.FaultException);
        Assert.Empty(adapter.Sinks);
    }

    [Fact(Timeout = 10000)]
    public async Task Unregister_StopsRouting()
    {
        Fixture fixture = CreateFixture();

        bool unregistered = fixture.Adapter.Unregister(DotAcpClientAdapterTests.SessionId);
        await fixture.Adapter.SessionUpdateAsync(
            CreateMessageChunkNotification(DotAcpClientAdapterTests.SessionId, "hi"),
            TestContext.Current.CancellationToken);

        Assert.True(unregistered);
        Assert.Empty(fixture.Sink.Events);
    }

    private static Fixture CreateFixture()
    {
        ListLogger<DotAcpClientAdapter> logger = new ListLogger<DotAcpClientAdapter>();
        DotAcpClientAdapter adapter = new DotAcpClientAdapter(logger);
        FakeSessionSink sink = new FakeSessionSink(DotAcpClientAdapterTests.SessionId);
        adapter.Register(sink);
        return new Fixture(adapter, logger, sink);
    }

    private static dotacp.protocol.SessionNotification CreateMessageChunkNotification(string sessionId, string text)
    {
        return new dotacp.protocol.SessionNotification
        {
            SessionId = sessionId,
            Update = new dotacp.protocol.SessionUpdateAgentMessageChunk
            {
                Content = new dotacp.protocol.TextContent { Text = text },
            },
        };
    }

    private static dotacp.protocol.RequestPermissionRequest CreatePermissionRequest(string sessionId)
    {
        return new dotacp.protocol.RequestPermissionRequest
        {
            SessionId = sessionId,
            ToolCall = new dotacp.protocol.ToolCallUpdate
            {
                ToolCallId = "call-1",
                Title = "Write hello.txt",
                Kind = dotacp.protocol.ToolKind.Edit,
                Status = dotacp.protocol.ToolCallStatus.Pending,
            },
            Options = new dotacp.protocol.PermissionOption[]
            {
                new dotacp.protocol.PermissionOption
                {
                    OptionId = "allow",
                    Name = "Allow once",
                    Kind = dotacp.protocol.PermissionOptionKind.AllowOnce,
                },
                new dotacp.protocol.PermissionOption
                {
                    OptionId = "always",
                    Name = "Allow always",
                    Kind = dotacp.protocol.PermissionOptionKind.AllowAlways,
                },
                new dotacp.protocol.PermissionOption
                {
                    OptionId = "reject",
                    Name = "Reject",
                    Kind = dotacp.protocol.PermissionOptionKind.RejectOnce,
                },
            },
        };
    }

    private sealed record Fixture(
        DotAcpClientAdapter Adapter,
        ListLogger<DotAcpClientAdapter> Logger,
        FakeSessionSink Sink);
}
