namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

public sealed class DotAcpAgentSessionTests
{
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_SendsSingleTextContentBlock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            _ = await harness.Session.PromptAsync("hello", cancellationToken);

            JsonObject prompt = await harness.Launcher.Agent.WaitForAsync("session/prompt", TimeSpan.FromSeconds(2));
            JsonNode parameters = prompt["params"]!;
            Assert.Equal("sess-1", (string?)parameters["sessionId"]);
            JsonArray promptBlocks = Assert.IsType<JsonArray>(parameters["prompt"]);
            Assert.Single(promptBlocks);
            Assert.Equal("text", (string?)promptBlocks[0]!["type"]);
            Assert.Equal("hello", (string?)promptBlocks[0]!["text"]);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>A text-only <see cref="AgentPrompt"/> sends exactly the one text block the string overload always has.</summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_TextOnlyAgentPrompt_SendsSingleTextContentBlock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            _ = await harness.Session.PromptAsync(new AgentPrompt("hello"), cancellationToken);

            JsonObject prompt = await harness.Launcher.Agent.WaitForAsync("session/prompt", TimeSpan.FromSeconds(2));
            JsonArray promptBlocks = Assert.IsType<JsonArray>(prompt["params"]!["prompt"]);
            Assert.Single(promptBlocks);
            Assert.Equal("text", (string?)promptBlocks[0]!["type"]);
            Assert.Equal("hello", (string?)promptBlocks[0]!["text"]);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>An empty block list is the same as none: still exactly one text block on the wire.</summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_EmptyBlockList_SendsSingleTextContentBlock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            _ = await harness.Session.PromptAsync(new AgentPrompt("hello", []), cancellationToken);

            JsonObject prompt = await harness.Launcher.Agent.WaitForAsync("session/prompt", TimeSpan.FromSeconds(2));
            JsonArray promptBlocks = Assert.IsType<JsonArray>(prompt["params"]!["prompt"]);
            Assert.Single(promptBlocks);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>
    /// An image block arrives after the text, as <c>type:"image"</c> with the base64 of the bytes,
    /// the MIME type, and no <c>uri</c> value: <c>claude-agent-acp</c> drops an image that has only a
    /// <c>uri</c>, so the bytes must travel in <c>data</c> (design D-3).
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_ImageBlock_SendsTextThenBase64Image()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            byte[] bytes = [0x41, 0x42, 0x43];
            AgentPrompt prompt = new("look at this", [new AgentImageBlock("image/png", bytes)]);

            _ = await harness.Session.PromptAsync(prompt, cancellationToken);

            JsonObject request = await harness.Launcher.Agent.WaitForAsync("session/prompt", TimeSpan.FromSeconds(2));
            JsonArray promptBlocks = Assert.IsType<JsonArray>(request["params"]!["prompt"]);
            Assert.Equal(2, promptBlocks.Count);
            Assert.Equal("text", (string?)promptBlocks[0]!["type"]);
            Assert.Equal("look at this", (string?)promptBlocks[0]!["text"]);
            Assert.Equal("image", (string?)promptBlocks[1]!["type"]);
            Assert.Equal("QUJD", (string?)promptBlocks[1]!["data"]);
            Assert.Equal("image/png", (string?)promptBlocks[1]!["mimeType"]);
            Assert.Null((string?)promptBlocks[1]!["uri"]);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>A text resource arrives as <c>type:"resource"</c> carrying the URI, the MIME type and the text.</summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_TextResourceBlock_SendsEmbeddedResource()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            AgentPrompt prompt = new("read it", [new AgentTextResourceBlock("file:///E:/a%20b/n.md", "text/markdown", "# t")]);

            _ = await harness.Session.PromptAsync(prompt, cancellationToken);

            JsonObject request = await harness.Launcher.Agent.WaitForAsync("session/prompt", TimeSpan.FromSeconds(2));
            JsonArray promptBlocks = Assert.IsType<JsonArray>(request["params"]!["prompt"]);
            Assert.Equal(2, promptBlocks.Count);
            Assert.Equal("resource", (string?)promptBlocks[1]!["type"]);
            JsonObject resource = Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(promptBlocks[1])["resource"]);
            Assert.Equal("file:///E:/a%20b/n.md", (string?)resource["uri"]);
            Assert.Equal("text/markdown", (string?)resource["mimeType"]);
            Assert.Equal("# t", (string?)resource["text"]);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>Several blocks keep the order they were given in, after the text.</summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_TwoImageBlocks_KeepsGivenOrder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            AgentPrompt prompt = new(
                "two",
                [new AgentImageBlock("image/png", new byte[] { 1 }), new AgentImageBlock("image/jpeg", new byte[] { 2 })]);

            _ = await harness.Session.PromptAsync(prompt, cancellationToken);

            JsonObject request = await harness.Launcher.Agent.WaitForAsync("session/prompt", TimeSpan.FromSeconds(2));
            JsonArray promptBlocks = Assert.IsType<JsonArray>(request["params"]!["prompt"]);
            Assert.Equal(["text", "image", "image"], promptBlocks.Select(block => (string?)block!["type"]));
            Assert.Equal("image/png", (string?)promptBlocks[1]!["mimeType"]);
            Assert.Equal("image/jpeg", (string?)promptBlocks[2]!["mimeType"]);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>The string overload on the interface sends the same single text block as an <see cref="AgentPrompt"/> would.</summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_StringOverload_DelegatesToAgentPrompt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentSession session = new() { OnPrompt = _ => Task.FromResult(new PromptResult(StopReason.EndTurn)) };

        _ = await ((IAgentSession)session).PromptAsync("hello", cancellationToken);

        AgentPrompt received = Assert.Single(session.AgentPrompts);
        Assert.Equal("hello", received.Text);
        Assert.Null(received.Blocks);
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_ReturnsMappedStopReason()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = _ => Task.FromResult("max_tokens");

            PromptResult result = await harness.Session.PromptAsync("hello", cancellationToken);

            Assert.Equal(StopReason.MaxTokens, result.StopReason);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_StreamsChunksInOrder_ThenTurnCompleted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                await context.SendTextChunkAsync("a").ConfigureAwait(false);
                await context.SendTextChunkAsync("b").ConfigureAwait(false);
                await context.SendTextChunkAsync("c").ConfigureAwait(false);
                return "end_turn";
            };

            Task<PromptResult> promptTask = harness.Session.PromptAsync("hello", cancellationToken);
            List<AgentEvent> events = await DotAcpAgentSessionTests.ReadUntilTurnCompletedAsync(
                harness.Session.Events, cancellationToken);
            PromptResult result = await promptTask;

            Assert.Equal(
                new AgentEvent[]
                {
                    new MessageChunk("sess-1", "a"),
                    new MessageChunk("sess-1", "b"),
                    new MessageChunk("sess-1", "c"),
                    new TurnCompleted("sess-1", StopReason.EndTurn),
                },
                events);
            Assert.Equal(StopReason.EndTurn, result.StopReason);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>
    /// D30 ordering defect regression (docs/engineering/known-limits.md, "Second known flake"):
    /// StreamJsonRpc completes our outbound <c>session/prompt</c> request through a different path
    /// than the one that dispatches an inbound <c>session/update</c> notification's target method, so
    /// <see cref="DotAcpAgentSession.PromptAsync"/> can observe the response - and be ready to publish
    /// <see cref="TurnCompleted"/> - before a same-Turn <see cref="MessageChunk"/> the peer wrote to
    /// the wire first has even reached <see cref="DotAcpClientAdapter.SessionUpdateAsync"/>. This
    /// forces that interleaving deterministically at the source, rather than relying on real
    /// thread-pool timing to reproduce it (which is what made the defect read as a flake): the fake
    /// agent answers <c>session/prompt</c> immediately and only writes its <c>session/update</c> 20ms
    /// later, from a background task, so the response is on the wire strictly before the chunk. Before
    /// the quiet-window wait in <see cref="DotAcpAgentSession.PromptAsync"/>, the reader observes
    /// <c>TurnCompleted</c> with nothing after it and the late chunk is left unread; the fix must wait
    /// long enough for it to arrive and be published first.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task PromptAsync_ChunkDispatchedAfterResponse_StillPrecedesTurnCompleted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = context =>
            {
                _ = Task.Run(
                    async () =>
                    {
                        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                        await context.SendTextChunkAsync("late").ConfigureAwait(false);
                    },
                    cancellationToken);
                return Task.FromResult("end_turn");
            };

            Task<PromptResult> promptTask = harness.Session.PromptAsync("hello", cancellationToken);
            List<AgentEvent> events = await DotAcpAgentSessionTests.ReadUntilTurnCompletedAsync(
                harness.Session.Events, cancellationToken);
            PromptResult result = await promptTask;

            Assert.Equal(
                new AgentEvent[]
                {
                    new MessageChunk("sess-1", "late"),
                    new TurnCompleted("sess-1", StopReason.EndTurn),
                },
                events);
            Assert.Equal(StopReason.EndTurn, result.StopReason);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_ThoughtAndToolCallEvents_ArePublished()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                await context.SendThoughtChunkAsync("thinking").ConfigureAwait(false);
                await context.SendToolCallAsync("call-1", "Read note.txt", "read", "pending").ConfigureAwait(false);
                await context.SendToolCallUpdateAsync("call-1", "completed").ConfigureAwait(false);
                return "end_turn";
            };

            Task<PromptResult> promptTask = harness.Session.PromptAsync("hello", cancellationToken);
            List<AgentEvent> events = await DotAcpAgentSessionTests.ReadUntilTurnCompletedAsync(
                harness.Session.Events, cancellationToken);
            _ = await promptTask;

            ThoughtChunk thought = Assert.IsType<ThoughtChunk>(events[0]);
            Assert.Equal("thinking", thought.Text);

            ToolCallStarted started = Assert.IsType<ToolCallStarted>(events[1]);
            Assert.Equal(ToolKind.Read, started.Kind);
            Assert.Equal(ToolCallStatus.Pending, started.Status);

            ToolCallUpdated updated = Assert.IsType<ToolCallUpdated>(events[2]);
            Assert.Equal(ToolCallStatus.Completed, updated.Status);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_WhileInFlight_ThrowsInvalidOperation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            harness.Launcher.Agent.OnPrompt = async _ =>
            {
                await gate.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                return "end_turn";
            };

            Task<PromptResult> firstPrompt = harness.Session.PromptAsync("hello", cancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Session.PromptAsync("hello again", cancellationToken));

            gate.TrySetResult();
            _ = await firstPrompt.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            gate.TrySetResult();
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_PermissionRequest_RoutedToSessionHandler_AgentReceivesOptionId()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Handler.Decision = new SelectedDecision("always");
            JsonObject? outcome = null;
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                outcome = await context.RequestPermissionAsync(
                    DotAcpAgentSessionTests.CreateToolCall("call-1"),
                    DotAcpAgentSessionTests.CreatePermissionOptions()).ConfigureAwait(false);
                return "end_turn";
            };

            PromptResult result = await harness.Session.PromptAsync("hello", cancellationToken);

            Assert.Equal(StopReason.EndTurn, result.StopReason);
            Assert.NotNull(outcome);
            Assert.Equal("always", (string?)outcome!["optionId"]);
            PermissionRequestContext request = Assert.Single(harness.Handler.Requests);
            Assert.Equal(ToolKind.Edit, request.ToolCall.Kind);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_TwoConcurrentPermissionRequests_BothAnswered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            JsonObject? outcomeOne = null;
            JsonObject? outcomeTwo = null;
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                Task<JsonObject> requestOne = context.RequestPermissionAsync(
                    DotAcpAgentSessionTests.CreateToolCall("call-1"),
                    DotAcpAgentSessionTests.CreatePermissionOptions());
                Task<JsonObject> requestTwo = context.RequestPermissionAsync(
                    DotAcpAgentSessionTests.CreateToolCall("call-2"),
                    DotAcpAgentSessionTests.CreatePermissionOptions());

                JsonObject[] outcomes = await Task.WhenAll(requestOne, requestTwo).ConfigureAwait(false);
                outcomeOne = outcomes[0];
                outcomeTwo = outcomes[1];
                return "end_turn";
            };

            _ = await harness.Session.PromptAsync("hello", cancellationToken);

            Assert.NotNull(outcomeOne);
            Assert.NotNull(outcomeTwo);
            Assert.Equal("selected", (string?)outcomeOne!["outcome"]);
            Assert.Equal("selected", (string?)outcomeTwo!["outcome"]);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task CancelAsync_SendsSessionCancelNotification()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                await context.SendTextChunkAsync("a").ConfigureAwait(false);
                await context.WaitForCancelAsync().WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                return "cancelled";
            };

            Task<PromptResult> promptTask = harness.Session.PromptAsync("hello", cancellationToken);
            AgentEvent firstEvent = await harness.Session.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            Assert.Equal(new MessageChunk("sess-1", "a"), firstEvent);

            await harness.Session.CancelAsync(cancellationToken);

            JsonObject cancelMessage = await harness.Launcher.Agent.WaitForAsync("session/cancel", TimeSpan.FromSeconds(2));
            Assert.Equal("sess-1", (string?)cancelMessage["params"]!["sessionId"]);

            _ = await promptTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task CancelAsync_PromptReturnsCancelled()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                await context.SendTextChunkAsync("a").ConfigureAwait(false);
                await context.WaitForCancelAsync().WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                return "cancelled";
            };

            Task<PromptResult> promptTask = harness.Session.PromptAsync("hello", cancellationToken);
            _ = await harness.Session.Events.ReadAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            await harness.Session.CancelAsync(cancellationToken);
            PromptResult result = await promptTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            JsonObject cancelMessage = await harness.Launcher.Agent.WaitForAsync("session/cancel", TimeSpan.FromSeconds(2));
            Assert.Equal("sess-1", (string?)cancelMessage["params"]!["sessionId"]);
            Assert.Equal(StopReason.Cancelled, result.StopReason);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task CancelAsync_PendingPermission_AnsweredWithCancelledOutcome()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        TaskCompletionSource gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Handler.Gate = gate;
        try
        {
            JsonObject? outcome = null;
            harness.Launcher.Agent.OnPrompt = async context =>
            {
                outcome = await context.RequestPermissionAsync(
                    DotAcpAgentSessionTests.CreateToolCall("call-1"),
                    DotAcpAgentSessionTests.CreatePermissionOptions()).ConfigureAwait(false);
                await context.WaitForCancelAsync().WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                return "cancelled";
            };

            Task<PromptResult> promptTask = harness.Session.PromptAsync("hello", cancellationToken);

            await DotAcpAgentSessionTests.WaitUntilAsync(
                () => harness.Handler.Requests.Count > 0, TimeSpan.FromSeconds(5), cancellationToken);

            await harness.Session.CancelAsync(cancellationToken);
            PromptResult result = await promptTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            Assert.NotNull(outcome);
            Assert.Equal("cancelled", (string?)outcome!["outcome"]);
            Assert.Equal(StopReason.Cancelled, result.StopReason);
            Assert.False(gate.Task.IsCompleted);
        }
        finally
        {
            gate.TrySetResult();
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task CancelAsync_WhenIdle_IsNoOp()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            await harness.Session.CancelAsync(cancellationToken);

            Assert.DoesNotContain(
                harness.Launcher.Agent.Received,
                message => string.Equals((string?)message["method"], "session/cancel", StringComparison.Ordinal));
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task PromptAsync_AgentError_ThrowsAgentException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = _ => throw new FakeRpcError(-32603, "boom");

            AgentException exception = await Assert.ThrowsAsync<AgentException>(
                () => harness.Session.PromptAsync("hello", cancellationToken));

            Assert.Contains("boom", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task AgentClosesStdout_PromptFaultsWithAgentDisconnected_AndChannelCompletes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnPrompt = context =>
            {
                harness.Launcher.Agent.CloseOutput();
                return Task.FromResult("end_turn");
            };

            await Assert.ThrowsAsync<AgentDisconnectedException>(
                () => harness.Session.PromptAsync("hello", cancellationToken));

            await Assert.ThrowsAsync<AgentDisconnectedException>(
                () => harness.Session.Events.Completion.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken));
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_CompletesChannel_AndUnregisters()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            ChannelReader<AgentEvent> events = harness.Session.Events;
            string sessionId = harness.Session.SessionId;

            await harness.Session.DisposeAsync();

            await events.Completion.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Assert.True(events.Completion.IsCompletedSuccessfully);

            JsonObject update = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "session/update",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = sessionId,
                    ["update"] = new JsonObject
                    {
                        ["sessionUpdate"] = "agent_message_chunk",
                        ["content"] = new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = "after dispose",
                        },
                    },
                },
            };
            await harness.Launcher.Agent.WriteMessageAsync(update);
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

            Assert.False(events.TryRead(out _));
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>Spec §6.8: disposal sends exactly one <c>session/close</c> carrying the session id.</summary>
    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_SendsSessionCloseWithSessionId()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        string sessionId = harness.Session.SessionId;
        try
        {
            await harness.Session.DisposeAsync();

            JsonObject closeMessage = await harness.Launcher.Agent.WaitForAsync("session/close", TimeSpan.FromSeconds(2));
            Assert.Equal(sessionId, (string?)closeMessage["params"]!["sessionId"]);

            int closeCount = harness.Launcher.Agent.Received.Count(
                message => string.Equals((string?)message["method"], "session/close", StringComparison.Ordinal));
            Assert.Equal(1, closeCount);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>Spec §6.8: a failing <c>session/close</c> is a courtesy, never a condition of our own teardown - disposal still completes.</summary>
    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_SessionCloseFails_DisposalStillCompletes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            harness.Launcher.Agent.OnSessionClose = _ => throw new FakeRpcError(-32603, "close failed");

            await harness.Session.DisposeAsync();

            await harness.Session.Events.Completion.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Assert.True(harness.Session.Events.Completion.IsCompletedSuccessfully);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>Spec §6.8: the <c>disposed</c> flag is the idempotency guard - a second disposal sends nothing further.</summary>
    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_CalledTwice_SendsSessionCloseOnlyOnce()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            await harness.Session.DisposeAsync();
            _ = await harness.Launcher.Agent.WaitForAsync("session/close", TimeSpan.FromSeconds(2));

            await harness.Session.DisposeAsync();

            int closeCount = harness.Launcher.Agent.Received.Count(
                message => string.Equals((string?)message["method"], "session/close", StringComparison.Ordinal));
            Assert.Equal(1, closeCount);
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    /// <summary>Spec §6.8: disposal is bounded by a short timeout when the peer never answers <c>session/close</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task DisposeAsync_SessionCloseNeverAnswered_CompletesWithinBound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Harness harness = await Harness.CreateAsync(cancellationToken);
        try
        {
            TaskCompletionSource<JsonObject> neverCompletes = new TaskCompletionSource<JsonObject>();
            harness.Launcher.Agent.OnSessionClose = _ => neverCompletes.Task;

            Stopwatch stopwatch = Stopwatch.StartNew();
            await harness.Session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            stopwatch.Stop();

            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"Disposal took {stopwatch.Elapsed}, expected it bounded by the 2s session/close timeout.");
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    private static JsonObject CreateToolCall(string toolCallId)
    {
        return new JsonObject
        {
            ["toolCallId"] = toolCallId,
            ["title"] = "Write hello.txt",
            ["kind"] = "edit",
            ["status"] = "pending",
        };
    }

    private static JsonObject[] CreatePermissionOptions()
    {
        return new JsonObject[]
        {
            new JsonObject { ["optionId"] = "allow", ["name"] = "Allow once", ["kind"] = "allow_once" },
            new JsonObject { ["optionId"] = "always", ["name"] = "Allow always", ["kind"] = "allow_always" },
            new JsonObject { ["optionId"] = "reject", ["name"] = "Reject", ["kind"] = "reject_once" },
        };
    }

    private static async Task<List<AgentEvent>> ReadUntilTurnCompletedAsync(
        ChannelReader<AgentEvent> reader, CancellationToken cancellationToken)
    {
        List<AgentEvent> events = new List<AgentEvent>();
        using CancellationTokenSource timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);

        while (true)
        {
            AgentEvent agentEvent = await reader.ReadAsync(linkedSource.Token);
            events.Add(agentEvent);

            if (agentEvent is TurnCompleted)
            {
                return events;
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);

        while (!condition())
        {
            linkedSource.Token.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(20), linkedSource.Token);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly Lock gate = new Lock();

        private bool disposed;

        private Harness(
            FakeAgentProcessLauncher launcher,
            DotAcpAgentHost host,
            RecordingPermissionHandler handler,
            IAgentSession session)
        {
            this.Launcher = launcher;
            this.Host = host;
            this.Handler = handler;
            this.Session = session;
        }

        internal FakeAgentProcessLauncher Launcher { get; }

        internal DotAcpAgentHost Host { get; }

        internal RecordingPermissionHandler Handler { get; }

        internal IAgentSession Session { get; }

        internal static async Task<Harness> CreateAsync(CancellationToken cancellationToken)
        {
            FakeAgentProcessLauncher launcher = new FakeAgentProcessLauncher();
            DotAcpAgentHost host = new DotAcpAgentHost(
                new AgentProcessOptions("fake", []),
                launcher,
                new DotAcpHostOptions(),
                new ListLoggerFactory());
            await host.StartAsync(cancellationToken);

            RecordingPermissionHandler handler = new RecordingPermissionHandler();
            IAgentSession session = await host.StartSessionAsync(
                new AgentSessionOptions(Path.GetTempPath(), handler), cancellationToken);

            return new Harness(launcher, host, handler, session);
        }

        public async ValueTask DisposeAsync()
        {
            lock (this.gate)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
            }

            await this.Session.DisposeAsync();
            await this.Host.DisposeAsync();
        }
    }
}
