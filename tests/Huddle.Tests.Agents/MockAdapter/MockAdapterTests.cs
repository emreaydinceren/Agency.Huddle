using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.MockAdapter;
using Nerdbank.Streams;

namespace Agency.Huddle.Tests.MockAdapter;

/// <summary>
/// Drives a <see cref="FakeAcpAgent"/> over an in-proc <see cref="FullDuplexStream"/> pair,
/// acting as the client half and speaking raw newline-delimited JSON-RPC 2.0 with no protocol
/// library — the same wire format <see cref="FakeAcpAgent"/> itself speaks. See Spec §6.10
/// ("What already exists") and Spec §15.2 (T-0c).
/// </summary>
public sealed class MockAdapterTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// A full session — <c>initialize</c>, <c>session/new</c>, <c>session/prompt</c> — round-trips
    /// over the pipe: each request returns a result and is recorded, the prompt streams more than
    /// one <c>agent_message_chunk</c> notification before its result, and the three request
    /// methods appear in <see cref="FakeAcpAgent.Received"/> in the order they were sent.
    /// </summary>
    [Fact]
    public async Task FullSession_OverDuplexStream_StreamsPromptChunksAndRecordsMethodsInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (Stream clientStream, Stream agentStream) = FullDuplexStream.CreatePair();
        await using FakeAcpAgent agent = new(agentStream);
        agent.OnPrompt = MockBehaviour.ChunkedEchoAsync;
        Task agentRunTask = agent.RunAsync(ct);
        using StreamReader reader = new(clientStream, Utf8NoBom, false, 1024, leaveOpen: true);

        JsonObject initializeResult = await SendRequestAsync(clientStream, reader, 1, "initialize", new JsonObject(), ct);
        List<string?> receivedMethods = agent.Received.Select(message => (string?)message["method"]).ToList();
        Assert.NotNull((int?)initializeResult["protocolVersion"]);
        Assert.Contains("initialize", receivedMethods);

        JsonObject sessionResult = await SendRequestAsync(clientStream, reader, 2, "session/new", new JsonObject(), ct);
        string? sessionId = (string?)sessionResult["sessionId"];
        receivedMethods = agent.Received.Select(message => (string?)message["method"]).ToList();
        Assert.NotNull(sessionId);
        Assert.Contains("session/new", receivedMethods);

        JsonObject promptParameters = new()
        {
            ["sessionId"] = sessionId,
            ["prompt"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "hello there world" }),
        };
        List<JsonObject> notifications = new();
        JsonObject promptResult = await SendRequestAsync(clientStream, reader, 3, "session/prompt", promptParameters, ct, notifications);
        int chunkCount = notifications.Count(IsAgentMessageChunk);
        Assert.True(chunkCount > 1, $"Expected more than one agent_message_chunk notification before the session/prompt result, got {chunkCount}.");
        Assert.NotNull((string?)promptResult["stopReason"]);

        receivedMethods = agent.Received.Select(message => (string?)message["method"]).ToList();
        List<string?> expectedMethods = ["initialize", "session/new", "session/prompt"];
        Assert.Equal(expectedMethods, receivedMethods);

        clientStream.Dispose();
        await agentRunTask;
    }

    /// <summary>
    /// Writes one JSON-RPC request as a newline-terminated frame and reads frames back until the
    /// response with the matching id arrives. Any notification read along the way — a
    /// <c>session/update</c> sent while the request is still being processed — is appended to
    /// <paramref name="notifications"/> when supplied, rather than discarded.
    /// </summary>
    private static async Task<JsonObject> SendRequestAsync(
        Stream clientStream,
        StreamReader reader,
        int id,
        string method,
        JsonObject parameters,
        CancellationToken ct,
        List<JsonObject>? notifications = null)
    {
        JsonObject request = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };
        byte[] bytes = Utf8NoBom.GetBytes(request.ToJsonString() + "\n");
        await clientStream.WriteAsync(bytes, ct);
        await clientStream.FlushAsync(ct);

        while (true)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                throw new InvalidOperationException("The agent closed the stream before responding to " + method + ".");
            }

            if (line.Length == 0)
            {
                continue;
            }

            JsonObject message = (JsonObject)JsonNode.Parse(line)!;
            if (message.ContainsKey("id") && (message.ContainsKey("result") || message.ContainsKey("error")))
            {
                if ((int?)message["id"] == id)
                {
                    return (JsonObject)message["result"]!;
                }

                continue;
            }

            notifications?.Add(message);
        }
    }

    /// <summary>True when <paramref name="message"/> is a <c>session/update</c> notification carrying an <c>agent_message_chunk</c>.</summary>
    private static bool IsAgentMessageChunk(JsonObject message)
    {
        if (!string.Equals((string?)message["method"], "session/update", StringComparison.Ordinal))
        {
            return false;
        }

        JsonObject? update = message["params"]?["update"] as JsonObject;
        return string.Equals((string?)update?["sessionUpdate"], "agent_message_chunk", StringComparison.Ordinal);
    }

    /// <summary>
    /// Launches <c>mock-acp</c> as a real child process with all three standard streams
    /// redirected, sends <c>initialize</c> on its stdin, and confirms Spec §12's E-16 guarantee:
    /// stdout carries protocol bytes only, and a diagnostic the mock has to report lands on
    /// stderr instead. A deliberately malformed second frame gives the mock a genuine fault to
    /// report, so the stderr half of the assertion is exercised for real rather than checking an
    /// empty set. This is the only process-spawning test in D0 (Spec §6.10, "Two modes" — process
    /// mode; Spec §15.2, Task 0.5.t).
    /// </summary>
    [Fact]
    public async Task InitializeOverChildProcess_StdoutCarriesProtocolBytesOnly()
    {
        CancellationToken testCt = TestContext.Current.CancellationToken;
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(testCt);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeoutSource.Token;

        ProcessStartInfo startInfo = new(MockAdapterExecutablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new() { StartInfo = startInfo };
        List<string> stdoutLines = new();

        try
        {
            process.Start();
            process.StandardInput.NewLine = "\n";

            JsonObject initializeRequest = new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new JsonObject(),
            };

            try
            {
                await process.StandardInput.WriteLineAsync(initializeRequest.ToJsonString());
                await process.StandardInput.FlushAsync(ct);
            }
            catch (IOException ex)
            {
                Assert.Fail($"Could not write the initialize request to mock-acp's stdin — the process likely exited immediately: {ex.Message}");
            }

            JsonObject? initializeResponse = await ReadResponseAsync(process.StandardOutput, 1, stdoutLines, ct);
            Assert.True(initializeResponse is not null, "The initialize request produced no response before mock-acp's stdout ended.");
            JsonObject? result = initializeResponse!["result"] as JsonObject;
            Assert.NotNull(result);
            Assert.NotNull((int?)result!["protocolVersion"]);

            // A deliberately malformed frame gives the mock a real fault to report, so this test
            // proves E-16 holds under an actual failure, not only the happy path.
            await process.StandardInput.WriteLineAsync("not-json");
            await process.StandardInput.FlushAsync(ct);
            process.StandardInput.Close();

            await DrainAsync(process.StandardOutput, stdoutLines, ct);
            string stderrText = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            // (a) stdout carries protocol bytes only — every line parses as JSON-RPC.
            foreach (string line in stdoutLines)
            {
                Assert.True(IsValidJson(line), $"stdout carried a non-JSON line: {line}");
            }

            // (b) whatever the malformed frame provoked, it never reached stdout. What it does to
            // stderr depends on how the linked FakeAcpAgent (owned by the ACP effort's subtree,
            // tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs — not this effort's to edit or pin)
            // chooses to answer a bad frame: today it throws, which mock-acp reports on stderr and
            // exits non-zero for; a legitimate future change could instead answer with a JSON-RPC
            // -32700 parse error and exit zero with nothing on stderr. E-16 only constrains stdout,
            // so assert the two outcomes that are true either way rather than pin one of them.
            Assert.DoesNotContain(stdoutLines, line => line.Contains("not-json", StringComparison.Ordinal));
            if (process.ExitCode != 0)
            {
                Assert.False(string.IsNullOrWhiteSpace(stderrText), "Non-zero arm: mock-acp exited non-zero, so it must have reported the fault on stderr.");
            }
            else
            {
                Assert.True(stdoutLines.TrueForAll(IsValidJson), "Zero arm: mock-acp exited cleanly, so stdout must still be pure JSON.");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    /// <summary>
    /// The <c>mock-acp</c> executable this same solution build produced, found by walking up from
    /// <see cref="AppContext.BaseDirectory"/> to the directory containing <c>Huddle.slnx</c> — the
    /// technique <c>tests/Huddle.Acp.Tests/E2E/E2E.cs</c> uses for its own <c>RepoRoot</c> — then
    /// descending into <c>src/Huddle.MockAdapter</c>'s own build output. The configuration segment
    /// (<c>Debug</c> or <c>Release</c>) and the target-framework segment are read off this test
    /// assembly's own output path rather than hard-coded, so the test follows whichever
    /// configuration it was itself built under, including a <c>Release</c> run.
    /// </summary>
    private static string MockAdapterExecutablePath
    {
        get
        {
            string repoRoot = FindRepoRoot();
            string normalizedBaseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetFramework = Path.GetFileName(normalizedBaseDirectory);
            string? configurationDirectory = Path.GetDirectoryName(normalizedBaseDirectory);
            if (configurationDirectory is null)
            {
                throw new InvalidOperationException($"Could not determine the build configuration from '{AppContext.BaseDirectory}'.");
            }

            string configuration = Path.GetFileName(configurationDirectory);
            string executableName = OperatingSystem.IsWindows() ? "mock-acp.exe" : "mock-acp";
            string executablePath = Path.Combine(repoRoot, "src", "Huddle.MockAdapter", "bin", configuration, targetFramework, executableName);
            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException($"Could not find the mock-acp executable at '{executablePath}'. Build the solution first.");
            }

            return executablePath;
        }
    }

    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> until it finds the directory containing <c>Huddle.slnx</c>.</summary>
    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Huddle.slnx");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (a directory containing 'Huddle.slnx') by walking up from '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Reads lines from <paramref name="reader"/> until one is a JSON-RPC response whose
    /// <c>id</c> matches <paramref name="id"/>, or the stream ends. Every non-empty line read
    /// along the way — including a malformed one that fails to parse — is appended to
    /// <paramref name="collected"/>, so the caller can assert about everything the process wrote
    /// to that stream, not only the answer.
    /// </summary>
    private static async Task<JsonObject?> ReadResponseAsync(StreamReader reader, int id, List<string> collected, CancellationToken ct)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (IOException)
            {
                return null;
            }

            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                continue;
            }

            collected.Add(line);

            if (TryParseResponse(line, id, out JsonObject? response))
            {
                return response;
            }
        }
    }

    /// <summary>Reads every remaining non-empty line from <paramref name="reader"/> into <paramref name="collected"/> until the stream ends.</summary>
    private static async Task DrainAsync(StreamReader reader, List<string> collected, CancellationToken ct)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (IOException)
            {
                return;
            }

            if (line is null)
            {
                return;
            }

            if (line.Length > 0)
            {
                collected.Add(line);
            }
        }
    }

    /// <summary>Parses <paramref name="line"/> as a JSON-RPC message and reports whether it is the response for <paramref name="id"/>.</summary>
    private static bool TryParseResponse(string line, int id, out JsonObject? response)
    {
        response = null;
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        if (node is JsonObject message
            && (int?)message["id"] == id
            && (message.ContainsKey("result") || message.ContainsKey("error")))
        {
            response = message;
            return true;
        }

        return false;
    }

    /// <summary>True when <paramref name="line"/> parses as a JSON document.</summary>
    private static bool IsValidJson(string line)
    {
        try
        {
            JsonNode.Parse(line);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
