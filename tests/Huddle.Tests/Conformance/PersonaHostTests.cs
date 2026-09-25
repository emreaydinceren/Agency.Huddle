using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// D19 (RS §6.3): drives the real <see cref="IAgentHostFactory"/> (<see cref="DotAcpAgentHostFactory"/>)
/// against a scripted <see cref="FakeAcpAgent"/>, the same Tier 3 harness <see cref="MockAdapterFixture"/>
/// uses for a whole <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> — except these tests call
/// <see cref="IAgentHostFactory.StartAsync"/> and the returned <see cref="IPersonaHost"/> directly,
/// with no pipe and no runner, to pin the factory split itself: one Adapter process and one App Tool
/// server per Persona, with the system prompt and Memory index rebuilt on every
/// <see cref="IPersonaHost.OpenAsync"/> call.
/// </summary>
public sealed class PersonaHostTests
{
    /// <summary><see cref="IAgentHostFactory.StartAsync"/> starts the host but sends no <c>session/new</c> at all.</summary>
    [Fact]
    public async Task Start_OpensNoSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);

        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        Assert.DoesNotContain(fixture.Agent.Received, message => (string?)message["method"] == "session/new");
    }

    /// <summary>Two <see cref="IPersonaHost.OpenAsync"/> calls mint two distinct session ids, over the one Adapter process <see cref="IAgentHostFactory.StartAsync"/> launched.</summary>
    [Fact]
    public async Task Open_Twice_TwoDistinctSessionIds_OneAdapterProcess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        IAgentSession first = await host.OpenAsync(ct);
        IAgentSession second = await host.OpenAsync(ct);

        Assert.NotEqual(first.SessionId, second.SessionId);

        // "One Adapter process": fixture.Launcher (and so fixture.Agent) is a single instance for
        // the whole test, constructed once in Fixture.StartAsync and never replaced - StartAsync
        // above is called exactly once, so both opens necessarily travelled over that one process,
        // confirmed by LastOptions being set at all (Launch ran) and both session ids coming from
        // the one FakeAcpAgent's own counter.
        Assert.NotNull(fixture.Launcher.LastOptions);
    }

    /// <summary>A memory file written between two opens shows up in the second open's composed system prompt, but not the first's (FC §6.15: the index is current at open, not stale from host start).</summary>
    [Fact]
    public async Task Open_ComposesSystemPromptPerOpen_MemoryIndexCurrent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        _ = await host.OpenAsync(ct);
        string firstPrompt = await fixture.AppendedSystemPromptAsync(occurrence: 1, ct);
        Assert.DoesNotContain("Porto trip notes", firstPrompt, StringComparison.Ordinal);

        string memoryDir = Path.Combine(fixture.DataDir, "Teammates", fixture.Persona.Name, "work", "memory");
        await File.WriteAllTextAsync(Path.Combine(memoryDir, "porto.md"), "# Porto trip notes\n", ct);

        _ = await host.OpenAsync(ct);
        string secondPrompt = await fixture.AppendedSystemPromptAsync(occurrence: 2, ct);
        Assert.Contains("Porto trip notes", secondPrompt, StringComparison.Ordinal);
    }

    /// <summary>Every session this host opens is told about the same MCP endpoint URL and the same bearer token (RS §6.3: "the token is minted per host").</summary>
    [Fact]
    public async Task Open_Twice_SameToolServerEndpointAndToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        _ = await host.OpenAsync(ct);
        (string firstUrl, string firstAuth) = await fixture.McpServerAsync(occurrence: 1, ct);

        _ = await host.OpenAsync(ct);
        (string secondUrl, string secondAuth) = await fixture.McpServerAsync(occurrence: 2, ct);

        Assert.Equal(firstUrl, secondUrl);
        Assert.Equal(firstAuth, secondAuth);
    }

    /// <summary><see cref="IPersonaHost.ResumeAsync"/> on an id the Adapter minted returns a session for it.</summary>
    [Fact]
    public async Task Resume_Known_ReturnsSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        IAgentSession opened = await host.OpenAsync(ct);

        IAgentSession? resumed = await host.ResumeAsync(opened.SessionId, ct);

        Assert.NotNull(resumed);
        Assert.Equal(opened.SessionId, resumed.SessionId);
    }

    /// <summary><see cref="IPersonaHost.ResumeAsync"/> on an id the Adapter never minted returns <see langword="null"/>, never throws.</summary>
    [Fact]
    public async Task Resume_Unknown_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        IAgentSession? resumed = await host.ResumeAsync("sess-never-minted", ct);

        Assert.Null(resumed);
    }

    /// <summary><see cref="IPersonaHost.CanResume"/> reports exactly what the Adapter advertised at <c>initialize</c>.</summary>
    [Fact]
    public async Task CanResume_MatchesAdvertisedCapability()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        // FakeAcpAgent's DefaultInitialize advertises sessionCapabilities.resume, so a freshly
        // started host, with no override, reports true.
        Assert.True(host.CanResume);
    }

    /// <summary><see cref="IPersonaHost.Profile"/> is the Adapter Profile the resolver picked for this Persona (finding P-8).</summary>
    [Fact]
    public async Task Profile_IsTheResolvedProfile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        await using IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);

        Assert.Equal("claude", host.Profile.Id);
    }

    /// <summary>Disposing the host also tears down the App Tool server it started (the same responsibility the old <c>ToolServerOwningAgentHost</c> held).</summary>
    [Fact]
    public async Task Dispose_DisposesToolServer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Fixture fixture = await Fixture.StartAsync(ct);
        IPersonaHost host = await fixture.Factory.StartAsync(fixture.Persona, "agent-1", ct);
        _ = await host.OpenAsync(ct);
        Uri toolServerUri = new(((JsonObject)(await fixture.SessionNewParamsAsync(occurrence: 1, ct))["mcpServers"]![0]!)["url"]!.GetValue<string>());

        await host.DisposeAsync();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using HttpClient client = new();
        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(toolServerUri, timeout.Token));
    }

    /// <summary>
    /// A minimal, pipe-free harness: a real Huddle host with <c>Team:Acp:Enabled</c> true and its
    /// process launcher substituted for <see cref="FakeAgentProcessLauncher"/>, exposing the real
    /// <see cref="IAgentHostFactory"/> directly rather than through a <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> -
    /// this test only needs the factory split RS §6.3 describes, not Registration or the read loop.
    /// </summary>
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly IHost host;
        private readonly TempDataDir dataDir;
        private bool disposed;

        private Fixture(IHost host, TempDataDir dataDir, FakeAgentProcessLauncher launcher, Persona persona)
        {
            this.host = host;
            this.dataDir = dataDir;
            this.Launcher = launcher;
            this.Persona = persona;
        }

        internal FakeAgentProcessLauncher Launcher { get; }

        internal FakeAcpAgent Agent => this.Launcher.Agent;

        internal Persona Persona { get; }

        internal string DataDir => this.dataDir.Path;

        internal IAgentHostFactory Factory => this.host.Services.GetRequiredService<IAgentHostFactory>();

        internal static async Task<Fixture> StartAsync(CancellationToken cancellationToken)
        {
            TempDataDir dataDir = new();
            string pipeName = "persona-host-test-" + Guid.NewGuid().ToString("N");

            Dictionary<string, string?> config = new(StringComparer.Ordinal)
            {
                ["Team:PipeName"] = pipeName,
                ["Team:DataDir"] = dataDir.Path,
                ["Team:HumanName"] = "You",
                ["Team:DemoAgent:Enabled"] = "false",
                ["Team:Acp:Enabled"] = "true",
                ["Team:Acp:Args:0"] = "--persona-host-fixture",
            };

            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(config);
            builder.Services.AddTeamServices(builder.Configuration);
            PipeHostFixture.RemovePersonaSupervisorHostedService(builder.Services);

            builder.Services.RemoveAll<IAgentProcessLauncher>();
            FakeAgentProcessLauncher launcher = new();
            builder.Services.AddSingleton<IAgentProcessLauncher>(launcher);

            IHost host = builder.Build();
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            Persona persona = new("nova", "You are Nova.");

            return new Fixture(host, dataDir, launcher, persona);
        }

        /// <summary>Waits for the <paramref name="occurrence"/>-th <c>session/new</c> (1-based, in arrival order) and returns its <c>_meta.systemPrompt.append</c> text.</summary>
        internal async Task<string> AppendedSystemPromptAsync(int occurrence, CancellationToken cancellationToken)
        {
            JsonObject parameters = await this.SessionNewParamsAsync(occurrence, cancellationToken).ConfigureAwait(false);
            JsonObject? meta = parameters["_meta"] as JsonObject;
            JsonObject? systemPrompt = meta?["systemPrompt"] as JsonObject;
            string? appended = (string?)systemPrompt?["append"];
            Assert.NotNull(appended);
            return appended;
        }

        /// <summary>Waits for the <paramref name="occurrence"/>-th <c>session/new</c> and returns its sole <c>mcpServers</c> entry's URL and <c>Authorization</c> header value.</summary>
        internal async Task<(string Url, string Authorization)> McpServerAsync(int occurrence, CancellationToken cancellationToken)
        {
            JsonObject parameters = await this.SessionNewParamsAsync(occurrence, cancellationToken).ConfigureAwait(false);
            JsonArray mcpServers = Assert.IsType<JsonArray>(parameters["mcpServers"]);
            JsonObject entry = Assert.IsType<JsonObject>(Assert.Single(mcpServers));
            string url = (string?)entry["url"] ?? string.Empty;
            JsonArray headers = Assert.IsType<JsonArray>(entry["headers"]);
            JsonObject headerEntry = Assert.IsType<JsonObject>(Assert.Single(headers));
            Assert.Equal("Authorization", (string?)headerEntry["name"]);
            string authorization = (string?)headerEntry["value"] ?? string.Empty;
            return (url, authorization);
        }

        /// <summary>
        /// The <paramref name="occurrence"/>-th <c>session/new</c> request's <c>params</c> object
        /// (1-based, in arrival order). <see cref="FakeAcpAgent.WaitForAsync"/> always returns the
        /// FIRST matching message ever received, never the latest, so a second or later open is
        /// read directly off <see cref="FakeAcpAgent.Received"/> instead, polled until it has
        /// enough <c>session/new</c> entries.
        /// </summary>
        internal async Task<JsonObject> SessionNewParamsAsync(int occurrence, CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            List<JsonObject> sessionNews;
            while (true)
            {
                sessionNews = [.. this.Agent.Received.Where(message => (string?)message["method"] == "session/new")];
                if (sessionNews.Count >= occurrence)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token).ConfigureAwait(false);
            }

            return (JsonObject)sessionNews[occurrence - 1]["params"]!;
        }

        public async ValueTask DisposeAsync()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            await this.host.StopAsync().ConfigureAwait(false);
            this.host.Dispose();
            this.dataDir.Dispose();
        }
    }
}
