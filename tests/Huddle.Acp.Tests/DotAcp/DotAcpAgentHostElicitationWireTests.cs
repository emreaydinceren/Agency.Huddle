using System.Text.Json.Nodes;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers the wire half of the elicitation bridge (Elicitation E-1, E-1b): what
/// <see cref="DotAcpAgentHost"/> advertises in <c>initialize</c> when
/// <see cref="DotAcpHostOptions.AdvertiseElicitationForm"/> is set, and the rewrite of an inbound
/// <c>elicitation/create</c> so stable dotacp routes it as an extension method.
/// </summary>
public sealed class DotAcpAgentHostElicitationWireTests
{
    private const int MethodNotFound = -32601;

    /// <summary>With the option on, <c>initialize</c> carries <c>clientCapabilities.elicitation</c> equal to exactly <c>{"form":{}}</c>: form mode only, never <c>url</c> (which would start the adapter's MCP OAuth path).</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_WithOption_AdvertisesFormObjectOnly()
    {
        JsonObject capabilities = await DotAcpAgentHostElicitationWireTests.StartAndReadCapabilitiesAsync(
            new DotAcpHostOptions(AdvertiseElicitationForm: true),
            TestContext.Current.CancellationToken);

        JsonObject? elicitation = capabilities["elicitation"] as JsonObject;
        Assert.NotNull(elicitation);
        Assert.Equal("""{"form":{}}""", elicitation.ToJsonString());
    }

    /// <summary>With the option on, the capabilities are exactly fs, terminal and elicitation: the existing file-system and terminal answers stay and nothing else is added.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_WithOption_CapabilitiesAreFsTerminalAndElicitationOnly()
    {
        JsonObject capabilities = await DotAcpAgentHostElicitationWireTests.StartAndReadCapabilitiesAsync(
            new DotAcpHostOptions(AdvertiseElicitationForm: true),
            TestContext.Current.CancellationToken);

        JsonNode expected = JsonNode.Parse("""{"fs":{"readTextFile":false,"writeTextFile":false},"terminal":false,"elicitation":{"form":{}}}""")
            ?? throw new InvalidOperationException("The expected literal did not parse.");
        Assert.True(JsonNode.DeepEquals(expected, capabilities), capabilities.ToJsonString());
    }

    /// <summary>By default the host advertises nothing about elicitation: no <c>elicitation</c> key at all, and the capabilities are the same fs and terminal answers as before.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_Default_AdvertisesNothing()
    {
        JsonObject capabilities = await DotAcpAgentHostElicitationWireTests.StartAndReadCapabilitiesAsync(
            new DotAcpHostOptions(),
            TestContext.Current.CancellationToken);

        JsonNode expected = JsonNode.Parse("""{"fs":{"readTextFile":false,"writeTextFile":false},"terminal":false}""")
            ?? throw new InvalidOperationException("The expected literal did not parse.");
        Assert.False(capabilities.ContainsKey("elicitation"));
        Assert.True(JsonNode.DeepEquals(expected, capabilities), capabilities.ToJsonString());
    }

    /// <summary>The option is off unless asked for, however the options are built: no arguments, the probe host's construction and the Console's construction all leave it false.</summary>
    [Fact]
    public void HostOptions_ProbeAndConsoleConstructions_LeaveAdvertisementOff()
    {
        DotAcpHostOptions noArguments = new();
        DotAcpHostOptions probe = new("Team.App", TraceWire: true);
        DotAcpHostOptions console = new(ClientVersion: "1.2.3", TraceWire: false);

        Assert.False(noArguments.AdvertiseElicitationForm);
        Assert.False(probe.AdvertiseElicitationForm);
        Assert.False(console.AdvertiseElicitationForm);
    }

    /// <summary>With the option on, an <c>elicitation/create</c> request from the agent reaches the client as the extension method <c>_elicitation/create</c> rather than being refused as an unknown method (-32601).</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_WithOption_AgentElicitationCreate_IsRoutedAsAnExtensionMethod()
    {
        int? errorCode = await DotAcpAgentHostElicitationWireTests.SendElicitationCreateAsync(
            new DotAcpHostOptions(AdvertiseElicitationForm: true),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(DotAcpAgentHostElicitationWireTests.MethodNotFound, errorCode);
    }

    /// <summary>With the option off, the same request is left alone and refused as an unknown method (-32601): no rewrite is installed for a host that never advertised elicitation.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_Default_AgentElicitationCreate_IsMethodNotFound()
    {
        int? errorCode = await DotAcpAgentHostElicitationWireTests.SendElicitationCreateAsync(
            new DotAcpHostOptions(),
            TestContext.Current.CancellationToken);

        Assert.Equal(DotAcpAgentHostElicitationWireTests.MethodNotFound, errorCode);
    }

    /// <summary>Disposing a host that reads through the rewrite stream disposes that stream's inner stream (so the process pipe is released) and the shutdown still completes without killing the process.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_Dispose_ForwardsAndHostShutdownStillCompletes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher inner = new();
        WrappingLauncher launcher = new(inner);
        DotAcpAgentHost host = new(
            new AgentProcessOptions("fake", []),
            launcher,
            new DotAcpHostOptions(AdvertiseElicitationForm: true),
            new ListLoggerFactory());

        await host.StartAsync(cancellationToken);
        await host.DisposeAsync();

        Assert.NotNull(launcher.Output);
        await launcher.Output.WhenDisposed.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.True(launcher.Output.WhenDisposed.IsCompletedSuccessfully);
        Assert.False(inner.Process.Killed);
        Assert.True(inner.Process.Exited.IsCompleted);
    }

    /// <summary>Starts a host with <paramref name="options"/> against a default fake agent and returns the <c>clientCapabilities</c> object the agent received in <c>initialize</c>.</summary>
    /// <param name="options">The host options under test.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<JsonObject> StartAndReadCapabilitiesAsync(DotAcpHostOptions options, CancellationToken cancellationToken)
    {
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = new(new AgentProcessOptions("fake", []), launcher, options, new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            JsonObject initialize = launcher.Agent.Received.Single(message => string.Equals((string?)message["method"], "initialize", StringComparison.Ordinal));
            JsonObject? capabilities = (initialize["params"] as JsonObject)?["clientCapabilities"] as JsonObject;
            Assert.NotNull(capabilities);
            return capabilities;
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>Starts a host, has the fake agent send it an <c>elicitation/create</c> request, and returns the JSON-RPC error code of the answer (null when it was a success).</summary>
    /// <param name="options">The host options under test.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<int?> SendElicitationCreateAsync(DotAcpHostOptions options, CancellationToken cancellationToken)
    {
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = new(new AgentProcessOptions("fake", []), launcher, options, new ListLoggerFactory());

        try
        {
            await host.StartAsync(cancellationToken);

            JsonObject parameters = new()
            {
                ["mode"] = "form",
                ["sessionId"] = "sess-1",
                ["message"] = "Pick one",
                ["requestedSchema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
            };

            try
            {
                _ = await launcher.Agent.SendRequestAsync("elicitation/create", parameters);
                return null;
            }
            catch (FakeRpcError error)
            {
                return error.Code;
            }
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>An <see cref="IAgentProcessLauncher"/> that launches through a <see cref="FakeAgentProcessLauncher"/> but hands the host a process whose standard output is a <see cref="ChunkedReadStream"/> it can inspect.</summary>
    private sealed class WrappingLauncher(FakeAgentProcessLauncher inner) : IAgentProcessLauncher
    {
        /// <summary>Gets the tracking stream handed to the host as standard output, or null before <see cref="Launch"/>.</summary>
        internal ChunkedReadStream? Output { get; private set; }

        public IAgentProcess Launch(AgentProcessOptions options)
        {
            IAgentProcess process = inner.Launch(options);
            this.Output = new ChunkedReadStream(process.StandardOutput, int.MaxValue);
            return new WrappedProcess(process, this.Output);
        }
    }

    /// <summary>An <see cref="IAgentProcess"/> that forwards everything to another except its standard output.</summary>
    private sealed class WrappedProcess(IAgentProcess inner, Stream output) : IAgentProcess
    {
        public Stream StandardInput => inner.StandardInput;

        public Stream StandardOutput => output;

        public Task<int> Exited => inner.Exited;

        public void Kill()
        {
            inner.Kill();
        }

        public void Dispose()
        {
            inner.Dispose();
        }
    }
}
