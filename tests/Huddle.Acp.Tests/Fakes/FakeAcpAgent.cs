namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A scripted fake of the ACP agent process: speaks raw newline-delimited JSON-RPC 2.0 over a
/// <see cref="Stream"/>, at the wire level, with no protocol library involved.
/// </summary>
internal sealed class FakeAcpAgent : IAsyncDisposable
{
    private readonly Stream stream;

    private readonly SemaphoreSlim writeGate = new SemaphoreSlim(1, 1);

    private readonly Lock gate = new Lock();

    private readonly List<JsonObject> received = new List<JsonObject>();

    private readonly List<PendingWait> waiters = new List<PendingWait>();

    private readonly Dictionary<string, PromptContext> activePrompts = new Dictionary<string, PromptContext>();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<JsonObject>>();

    /// <summary>Every session id this fake has minted through <c>session/new</c> (RS §6.4 A-5). Closing does not remove one: RS §6.2 "Closing" - Claude Code keeps closed conversations on disk, so a closed id still resumes.</summary>
    private readonly HashSet<string> knownSessionIds = new HashSet<string>(StringComparer.Ordinal);

    private int nextRequestId = 999;

    /// <summary>
    /// The per-instance counter behind <see cref="DefaultNewSessionAsync"/>'s minted ids. A plain
    /// instance field, not a <c>static</c> counter shared by every fake in the process: two fakes
    /// in two tests must not observe each other's ids.
    /// </summary>
    private int nextSessionNumber;

    internal FakeAcpAgent(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        this.stream = stream;
        this.OnNewSession = this.DefaultNewSessionAsync;
    }

    internal Func<JsonObject, JsonObject> OnInitialize { get; set; } = FakeAcpAgent.DefaultInitialize;

    internal Func<JsonObject, Task<JsonObject>> OnNewSession { get; set; }

    internal Func<JsonObject, Task<JsonObject>> OnSetConfigOption { get; set; } = FakeAcpAgent.DefaultSetConfigOptionAsync;

    internal Func<PromptContext, Task<string>> OnPrompt { get; set; } = FakeAcpAgent.DefaultPromptAsync;

    internal Func<JsonObject, Task<JsonObject>> OnSessionClose { get; set; } = FakeAcpAgent.DefaultSessionCloseAsync;

    /// <summary>
    /// Scripts a <c>session/resume</c> answer for a known id (an unknown id always answers
    /// resource-not-found before this hook runs). Default: succeeds with an empty result.
    /// </summary>
    internal Func<JsonObject, Task<JsonObject>> OnResumeSession { get; set; } = FakeAcpAgent.DefaultResumeSessionAsync;

    internal List<JsonObject> Received
    {
        get
        {
            lock (this.gate)
            {
                return new List<JsonObject>(this.received);
            }
        }
    }

    /// <summary>
    /// Completes when a message with the given method arrives. Checks the already-received
    /// history and installs the waiter under the same lock used to record new messages, so a
    /// message that arrived before this call is found immediately and one arriving concurrently
    /// can never be missed between the check and the registration.
    /// </summary>
    internal async Task<JsonObject> WaitForAsync(string method, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(method);

        TaskCompletionSource<JsonObject> waiterSource = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.gate)
        {
            JsonObject? existing = null;
            foreach (JsonObject candidate in this.received)
            {
                if (string.Equals((string?)candidate["method"], method, StringComparison.Ordinal))
                {
                    existing = candidate;
                    break;
                }
            }

            if (existing is not null)
            {
                waiterSource.SetResult(existing);
            }
            else
            {
                this.waiters.Add(new PendingWait(method, waiterSource));
            }
        }

        using (CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout))
        {
            using (timeoutSource.Token.Register(static state => ((TaskCompletionSource<JsonObject>)state!).TrySetException(new TimeoutException("Timed out waiting for a message.")), waiterSource))
            {
                return await waiterSource.Task.ConfigureAwait(false);
            }
        }
    }

    /// <summary>Runs the read loop until the stream ends. Faults if a line is not valid JSON-RPC.</summary>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        using (StreamReader reader = new StreamReader(this.stream, new UTF8Encoding(false), false, 1024, leaveOpen: true))
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is IOException)
                {
                    return;
                }

                if (line is null)
                {
                    return;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                JsonNode? node = JsonNode.Parse(line);
                JsonObject message = node as JsonObject
                    ?? throw new InvalidOperationException("Received a line that is not a JSON object: " + line);

                if ((string?)message["jsonrpc"] != "2.0")
                {
                    throw new InvalidOperationException("Received a message without jsonrpc: \"2.0\": " + line);
                }

                this.Record(message);
                await this.DispatchAsync(message).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Disposes the stream to simulate the agent process crashing.</summary>
    internal void CloseOutput()
    {
        this.stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        this.writeGate.Dispose();
        await CastAndDisposeAsync(this.stream).ConfigureAwait(false);

        static async ValueTask CastAndDisposeAsync(Stream resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
            {
                await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                resource.Dispose();
            }
        }
    }

    internal async Task<JsonObject> SendRequestAsync(string method, JsonObject parameters)
    {
        int id = Interlocked.Increment(ref this.nextRequestId);
        TaskCompletionSource<JsonObject> completionSource = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.pendingRequests[id] = completionSource;

        JsonObject message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };

        await this.WriteMessageAsync(message).ConfigureAwait(false);
        return await completionSource.Task.ConfigureAwait(false);
    }

    internal async Task WriteMessageAsync(JsonObject message)
    {
        string json = message.ToJsonString();
        byte[] bytes = new UTF8Encoding(false).GetBytes(json + "\n");

        await this.writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.stream.WriteAsync(bytes).ConfigureAwait(false);
            await this.stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            this.writeGate.Release();
        }
    }

    private static JsonObject DefaultInitialize(JsonObject _)
    {
        return new JsonObject
        {
            ["protocolVersion"] = 1,
            ["agentInfo"] = new JsonObject
            {
                ["name"] = "fake-agent",
                ["version"] = "0.0.1",
            },
            ["agentCapabilities"] = new JsonObject
            {
                ["loadSession"] = false,
                ["sessionCapabilities"] = new JsonObject
                {
                    ["resume"] = new JsonObject(),
                    ["close"] = new JsonObject(),
                },
            },
            ["authMethods"] = new JsonArray(),
        };
    }

    /// <summary>Mints a distinct id per call (RS §6.4 A-5): "sess-1", "sess-2", and so on.</summary>
    private Task<JsonObject> DefaultNewSessionAsync(JsonObject _)
    {
        int number = Interlocked.Increment(ref this.nextSessionNumber);
        JsonObject result = new JsonObject
        {
            ["sessionId"] = "sess-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return Task.FromResult(result);
    }

    private static Task<JsonObject> DefaultSetConfigOptionAsync(JsonObject _)
    {
        return Task.FromResult(new JsonObject());
    }

    private static async Task<string> DefaultPromptAsync(PromptContext context)
    {
        await context.SendTextChunkAsync("Hi").ConfigureAwait(false);
        return "end_turn";
    }

    private static Task<JsonObject> DefaultSessionCloseAsync(JsonObject _)
    {
        return Task.FromResult(new JsonObject());
    }

    private static Task<JsonObject> DefaultResumeSessionAsync(JsonObject _)
    {
        return Task.FromResult(new JsonObject());
    }

    private void Record(JsonObject message)
    {
        lock (this.gate)
        {
            this.received.Add(message);

            string? method = (string?)message["method"];
            if (method is not null)
            {
                for (int i = this.waiters.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(this.waiters[i].Method, method, StringComparison.Ordinal))
                    {
                        this.waiters[i].Source.TrySetResult(message);
                        this.waiters.RemoveAt(i);
                    }
                }
            }
        }
    }

    private Task DispatchAsync(JsonObject message)
    {
        bool hasId = message.ContainsKey("id");
        string? method = (string?)message["method"];

        if (hasId && method is not null)
        {
            JsonNode id = message["id"]!;
            JsonObject parameters = message["params"] as JsonObject ?? new JsonObject();
            return this.HandleRequestAsync(id, method, parameters);
        }

        if (method is not null)
        {
            JsonObject parameters = message["params"] as JsonObject ?? new JsonObject();
            this.HandleNotification(method, parameters);
            return Task.CompletedTask;
        }

        if (hasId)
        {
            JsonNode id = message["id"]!;
            this.HandleResponse(id, message);
        }

        return Task.CompletedTask;
    }

    private async Task HandleRequestAsync(JsonNode id, string method, JsonObject parameters)
    {
        switch (method)
        {
            case "initialize":
                try
                {
                    JsonObject result = this.OnInitialize(parameters);
                    await this.WriteResultAsync(id, result).ConfigureAwait(false);
                }
                catch (FakeRpcError error)
                {
                    await this.WriteErrorAsync(id, error.Code, error.Message).ConfigureAwait(false);
                }

                break;

            case "session/new":
                try
                {
                    JsonObject result = await this.OnNewSession(parameters).ConfigureAwait(false);

                    // Recorded here, in the dispatch step, rather than inside DefaultNewSessionAsync -
                    // so a test that overrides OnNewSession with its own script still gets that id
                    // registered as resumable (correction item 8).
                    string? mintedId = (string?)result["sessionId"];
                    if (mintedId is not null)
                    {
                        lock (this.gate)
                        {
                            this.knownSessionIds.Add(mintedId);
                        }
                    }

                    await this.WriteResultAsync(id, result).ConfigureAwait(false);
                }
                catch (FakeRpcError error)
                {
                    await this.WriteErrorAsync(id, error.Code, error.Message).ConfigureAwait(false);
                }

                break;

            case "session/resume":
                await this.HandleResumeSessionAsync(id, parameters).ConfigureAwait(false);
                break;

            case "session/set_config_option":
                try
                {
                    JsonObject result = await this.OnSetConfigOption(parameters).ConfigureAwait(false);
                    await this.WriteResultAsync(id, result).ConfigureAwait(false);
                }
                catch (FakeRpcError error)
                {
                    await this.WriteErrorAsync(id, error.Code, error.Message).ConfigureAwait(false);
                }

                break;

            case "session/prompt":
                this.StartPrompt(id, parameters);
                break;

            case "session/close":
                try
                {
                    JsonObject result = await this.OnSessionClose(parameters).ConfigureAwait(false);
                    await this.WriteResultAsync(id, result).ConfigureAwait(false);
                }
                catch (FakeRpcError error)
                {
                    await this.WriteErrorAsync(id, error.Code, error.Message).ConfigureAwait(false);
                }

                break;

            default:
                await this.WriteErrorAsync(id, -32601, "Method not found: " + method).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Answers <c>session/resume</c>: resource-not-found (-32002, RS §6.4 A-2) for an id this fake
    /// never minted, else <see cref="OnResumeSession"/>. A closed id stays known - RS §6.2
    /// "Closing" - so <c>session/close</c> must never remove it from <see cref="knownSessionIds"/>.
    /// </summary>
    private async Task HandleResumeSessionAsync(JsonNode id, JsonObject parameters)
    {
        string sessionId = (string?)parameters["sessionId"] ?? string.Empty;
        bool known;
        lock (this.gate)
        {
            known = this.knownSessionIds.Contains(sessionId);
        }

        if (!known)
        {
            await this.WriteErrorAsync(id, -32002, "No session found for id: " + sessionId).ConfigureAwait(false);
            return;
        }

        try
        {
            JsonObject result = await this.OnResumeSession(parameters).ConfigureAwait(false);
            await this.WriteResultAsync(id, result).ConfigureAwait(false);
        }
        catch (FakeRpcError error)
        {
            await this.WriteErrorAsync(id, error.Code, error.Message).ConfigureAwait(false);
        }
    }

    private void HandleNotification(string method, JsonObject parameters)
    {
        if (string.Equals(method, "session/cancel", StringComparison.Ordinal))
        {
            string sessionId = (string?)parameters["sessionId"] ?? string.Empty;
            PromptContext? context;
            lock (this.gate)
            {
                this.activePrompts.TryGetValue(sessionId, out context);
            }

            context?.SignalCancel();
        }
    }

    private void HandleResponse(JsonNode idNode, JsonObject message)
    {
        if (!FakeAcpAgent.TryGetId(idNode, out int id))
        {
            return;
        }

        if (!this.pendingRequests.TryRemove(id, out TaskCompletionSource<JsonObject>? completionSource))
        {
            return;
        }

        JsonObject? errorObject = message["error"] as JsonObject;
        if (errorObject is not null)
        {
            int code = (int?)errorObject["code"] ?? 0;
            string errorMessage = (string?)errorObject["message"] ?? "error";
            completionSource.TrySetException(new FakeRpcError(code, errorMessage));
        }
        else
        {
            JsonObject result = message["result"] as JsonObject ?? new JsonObject();
            completionSource.TrySetResult(result);
        }
    }

    private void StartPrompt(JsonNode id, JsonObject parameters)
    {
        string sessionId = (string?)parameters["sessionId"] ?? string.Empty;
        PromptContext context = new PromptContext(this, sessionId, parameters);

        lock (this.gate)
        {
            this.activePrompts[sessionId] = context;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                string stopReason = await this.OnPrompt(context).ConfigureAwait(false);
                JsonObject result = new JsonObject
                {
                    ["stopReason"] = stopReason,
                };
                await this.WriteResultAsync(id, result).ConfigureAwait(false);
            }
            catch (FakeRpcError error)
            {
                await this.WriteErrorAsync(id, error.Code, error.Message).ConfigureAwait(false);
            }
            finally
            {
                lock (this.gate)
                {
                    this.activePrompts.Remove(sessionId);
                }
            }
        });
    }

    private Task WriteResultAsync(JsonNode id, JsonObject result)
    {
        JsonObject message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["result"] = result,
        };
        return this.WriteMessageAsync(message);
    }

    private Task WriteErrorAsync(JsonNode id, int code, string errorMessage)
    {
        JsonObject errorObject = new JsonObject
        {
            ["code"] = code,
            ["message"] = errorMessage,
        };
        JsonObject message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["error"] = errorObject,
        };
        return this.WriteMessageAsync(message);
    }

    private static bool TryGetId(JsonNode idNode, out int id)
    {
        if (idNode is JsonValue value && value.TryGetValue(out int intValue))
        {
            id = intValue;
            return true;
        }

        id = 0;
        return false;
    }

    private sealed class PendingWait
    {
        internal PendingWait(string method, TaskCompletionSource<JsonObject> source)
        {
            this.Method = method;
            this.Source = source;
        }

        internal string Method { get; }

        internal TaskCompletionSource<JsonObject> Source { get; }
    }
}
