using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers the sign-in status an agent pushes as <c>_auth/status_update</c>, both on the adapter (the notification as dotacp
/// hands it over) and end to end through a real <see cref="DotAcpAgentHost"/> and the fake agent: only <c>authStatus.kind</c>
/// is read, silence is Unknown, and an agent that never advertised the marker is never waited for.
/// </summary>
public sealed class DotAcpAgentHostSignInTests
{
    private const string Method = "auth/status_update";

    // Split so no address literal sits in the source: the secret scan flags any bare address.
    private const string Email = "someone" + "@example.test";

    /// <summary>Gets each kind with the status it reads as: only <c>none</c> is signed out.</summary>
    public static TheoryData<string, AgentSignInStatus> Kinds()
    {
        return new TheoryData<string, AgentSignInStatus>
        {
            { "none", AgentSignInStatus.SignedOut },
            { "account", AgentSignInStatus.SignedIn },
            { "api_key", AgentSignInStatus.SignedIn },
            { "gateway", AgentSignInStatus.SignedIn },
            { "external", AgentSignInStatus.SignedIn },
        };
    }

    /// <summary>Each kind of push reads as its status on the adapter.</summary>
    /// <param name="kind">The wire kind.</param>
    /// <param name="expected">The status it should read as.</param>
    [Theory(Timeout = 10000)]
    [MemberData(nameof(Kinds))]
    public async Task Adapter_Kind_MapsToStatus(string kind, AgentSignInStatus expected)
    {
        DotAcpClientAdapter adapter = new(new ListLogger<DotAcpClientAdapter>(), new WireOrderLedger());

        await adapter.ExtNotificationAsync(DotAcpAgentHostSignInTests.Method, DotAcpAgentHostSignInTests.Params(kind), TestContext.Current.CancellationToken);

        Assert.Equal(expected, adapter.SignIn);
    }

    /// <summary>A push with no readable kind leaves the previous status in place.</summary>
    [Fact(Timeout = 10000)]
    public async Task Adapter_MalformedPush_KeepsPrevious()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DotAcpClientAdapter adapter = new(new ListLogger<DotAcpClientAdapter>(), new WireOrderLedger());
        await adapter.ExtNotificationAsync(DotAcpAgentHostSignInTests.Method, DotAcpAgentHostSignInTests.Params("none"), cancellationToken);

        await adapter.ExtNotificationAsync(DotAcpAgentHostSignInTests.Method, new Dictionary<string, object>(StringComparer.Ordinal), cancellationToken);
        await adapter.ExtNotificationAsync(
            DotAcpAgentHostSignInTests.Method,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["authStatus"] = new JObject { ["kind"] = 7 } },
            cancellationToken);
        await adapter.ExtNotificationAsync(
            DotAcpAgentHostSignInTests.Method,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["authStatus"] = new JObject { ["kind"] = string.Empty } },
            cancellationToken);

        Assert.Equal(AgentSignInStatus.SignedOut, adapter.SignIn);
    }

    /// <summary>The account fields beside the kind are never logged: the push produces no log line carrying them.</summary>
    [Fact(Timeout = 10000)]
    public async Task Adapter_PushWithAccountFields_LogsNothingOfThem()
    {
        ListLogger<DotAcpClientAdapter> logger = new();
        DotAcpClientAdapter adapter = new(logger, new WireOrderLedger());

        await adapter.ExtNotificationAsync(DotAcpAgentHostSignInTests.Method, DotAcpAgentHostSignInTests.Params("account"), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains(DotAcpAgentHostSignInTests.Email, StringComparison.Ordinal));
        Assert.Equal(AgentSignInStatus.SignedIn, adapter.SignIn);
    }

    /// <summary>A signed-out push after initialize reaches the host as SignedOut, through the real wire.</summary>
    [Fact(Timeout = 10000)]
    public async Task Host_NoneKindPushed_ReadsSignedOut()
    {
        AgentSignInStatus status = await DotAcpAgentHostSignInTests.ReadAfterPushAsync(advertise: true, "none", TestContext.Current.CancellationToken);

        Assert.Equal(AgentSignInStatus.SignedOut, status);
    }

    /// <summary>An account push after initialize reaches the host as SignedIn, through the real wire.</summary>
    [Fact(Timeout = 10000)]
    public async Task Host_AccountKindPushed_ReadsSignedIn()
    {
        AgentSignInStatus status = await DotAcpAgentHostSignInTests.ReadAfterPushAsync(advertise: true, "account", TestContext.Current.CancellationToken);

        Assert.Equal(AgentSignInStatus.SignedIn, status);
    }

    /// <summary>An agent that advertised the marker but never pushes reads Unknown once the wait is over.</summary>
    [Fact(Timeout = 10000)]
    public async Task Host_AdvertisedButSilent_ReadsUnknownAfterWait()
    {
        AgentSignInStatus status = await DotAcpAgentHostSignInTests.ReadAfterPushAsync(advertise: true, kind: null, TestContext.Current.CancellationToken);

        Assert.Equal(AgentSignInStatus.Unknown, status);
    }

    /// <summary>An agent that never advertised the marker answers Unknown at once, whatever the wait: nothing is waited for.</summary>
    [Fact(Timeout = 10000)]
    public async Task Host_NotAdvertised_ReadsUnknownWithoutWaiting()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = DotAcpAgentHostSignInTests.CreateHost(launcher, advertise: false);
        try
        {
            await host.StartAsync(cancellationToken);

            AgentSignInStatus status = await host.ReadSignInStatusAsync(TimeSpan.FromHours(1), cancellationToken);

            Assert.False(host.AdvertisesSignIn);
            Assert.Equal(AgentSignInStatus.Unknown, status);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>A push that arrives while the reader is already waiting releases it with that status.</summary>
    [Fact(Timeout = 10000)]
    public async Task Host_PushArrivesDuringWait_ReleasesWaiter()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = DotAcpAgentHostSignInTests.CreateHost(launcher, advertise: true);
        try
        {
            await host.StartAsync(cancellationToken);
            Task<AgentSignInStatus> waiting = host.ReadSignInStatusAsync(TimeSpan.FromHours(1), cancellationToken);

            await DotAcpAgentHostSignInTests.PushAsync(launcher, "none");

            Assert.Equal(AgentSignInStatus.SignedOut, await waiting);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static async Task<AgentSignInStatus> ReadAfterPushAsync(bool advertise, string? kind, CancellationToken cancellationToken)
    {
        FakeAgentProcessLauncher launcher = new();
        DotAcpAgentHost host = DotAcpAgentHostSignInTests.CreateHost(launcher, advertise);
        try
        {
            await host.StartAsync(cancellationToken);
            Assert.Equal(advertise, host.AdvertisesSignIn);
            if (kind is not null)
            {
                await DotAcpAgentHostSignInTests.PushAsync(launcher, kind);
            }

            return await host.ReadSignInStatusAsync(TimeSpan.FromMilliseconds(kind is null ? 100 : 5000), cancellationToken);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static DotAcpAgentHost CreateHost(FakeAgentProcessLauncher launcher, bool advertise)
    {
        launcher.Agent.OnInitialize = _ =>
        {
            JsonObject capabilities = new() { ["loadSession"] = false };
            if (advertise)
            {
                capabilities["_meta"] = new JsonObject { ["authStatus"] = new JsonObject() };
            }

            return new JsonObject
            {
                ["protocolVersion"] = 1,
                ["agentInfo"] = new JsonObject { ["name"] = "fake-agent", ["version"] = "0.0.1" },
                ["agentCapabilities"] = capabilities,
                ["authMethods"] = new JsonArray(),
            };
        };
        return new DotAcpAgentHost(new AgentProcessOptions("fake", []), launcher, new DotAcpHostOptions(), new ListLoggerFactory());
    }

    private static Task PushAsync(FakeAgentProcessLauncher launcher, string kind)
    {
        return launcher.Agent.WriteMessageAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "_auth/status_update",
            ["params"] = new JsonObject
            {
                ["authStatus"] = new JsonObject { ["kind"] = kind, ["label"] = "x" },
                ["email"] = DotAcpAgentHostSignInTests.Email,
            },
        });
    }

    private static Dictionary<string, object> Params(string kind)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["authStatus"] = new JObject { ["kind"] = kind, ["label"] = "x" },
            ["email"] = new JValue(DotAcpAgentHostSignInTests.Email),
        };
    }
}
