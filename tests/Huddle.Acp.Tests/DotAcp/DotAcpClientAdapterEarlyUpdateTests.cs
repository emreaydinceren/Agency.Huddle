using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers how <see cref="DotAcpClientAdapter"/> treats an update for a session that is not registered yet while a
/// <c>session/new</c> or <c>session/resume</c> is in flight (<see cref="DotAcpClientAdapter.BeginSessionStart"/>): it is
/// held and replayed in order when the session registers, and dropped with the usual Warning when the last start ends
/// without it. Driven through the notice path, which shares the delivery with <c>session/update</c>.
/// </summary>
public sealed class DotAcpClientAdapterEarlyUpdateTests
{
    private const string SessionId = "sess-new";

    /// <summary>An update held during the start is published when the session registers, before an update that arrives after.</summary>
    [Fact(Timeout = 10000)]
    public async Task HeldUpdate_SessionRegisters_PublishedInWireOrder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (DotAcpClientAdapter adapter, ListLogger<DotAcpClientAdapter> logger) = DotAcpClientAdapterEarlyUpdateTests.Create();
        FakeSessionSink sink = new(SessionId);

        using (adapter.BeginSessionStart())
        {
            await adapter.ExtNotificationAsync("session/notice", DotAcpClientAdapterEarlyUpdateTests.Notice(SessionId, "first"), cancellationToken);
            adapter.Register(sink);
        }

        await adapter.ExtNotificationAsync("session/notice", DotAcpClientAdapterEarlyUpdateTests.Notice(SessionId, "second"), cancellationToken);

        Assert.Equal(["first", "second"], sink.Events.OfType<AgentNotice>().Select(notice => notice.Title));
        Assert.Empty(DotAcpClientAdapterEarlyUpdateTests.Warnings(logger));
    }

    /// <summary>An update held for a session that never registers is dropped with the usual Warning once the start ends, and not before.</summary>
    [Fact(Timeout = 10000)]
    public async Task HeldUpdate_StartEndsUnregistered_DroppedWithWarning()
    {
        (DotAcpClientAdapter adapter, ListLogger<DotAcpClientAdapter> logger) = DotAcpClientAdapterEarlyUpdateTests.Create();
        IDisposable start = adapter.BeginSessionStart();

        await adapter.ExtNotificationAsync("session/notice", DotAcpClientAdapterEarlyUpdateTests.Notice("sess-other", "lost"), TestContext.Current.CancellationToken);
        Assert.Empty(DotAcpClientAdapterEarlyUpdateTests.Warnings(logger));
        start.Dispose();

        Assert.Equal(["Dropping update for unknown session sess-other"], DotAcpClientAdapterEarlyUpdateTests.Warnings(logger));
    }

    /// <summary>Disposing a start twice ends it once: a second, still open start keeps holding updates.</summary>
    [Fact(Timeout = 10000)]
    public async Task Start_DisposedTwice_EndsOnlyOnce()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (DotAcpClientAdapter adapter, ListLogger<DotAcpClientAdapter> logger) = DotAcpClientAdapterEarlyUpdateTests.Create();
        IDisposable first = adapter.BeginSessionStart();
        using IDisposable second = adapter.BeginSessionStart();
        first.Dispose();
        first.Dispose();

        await adapter.ExtNotificationAsync("session/notice", DotAcpClientAdapterEarlyUpdateTests.Notice(SessionId, "held"), cancellationToken);
        FakeSessionSink sink = new(SessionId);
        adapter.Register(sink);

        Assert.Equal(["held"], sink.Events.OfType<AgentNotice>().Select(notice => notice.Title));
        Assert.Empty(DotAcpClientAdapterEarlyUpdateTests.Warnings(logger));
    }

    /// <summary>Past the cap, an early update is dropped at once with the Warning, so a start that never ends cannot grow the hold without bound.</summary>
    [Fact(Timeout = 10000)]
    public async Task HeldUpdates_PastTheCap_DroppedAtOnce()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (DotAcpClientAdapter adapter, ListLogger<DotAcpClientAdapter> logger) = DotAcpClientAdapterEarlyUpdateTests.Create();
        using IDisposable start = adapter.BeginSessionStart();

        for (int index = 0; index < 257; index++)
        {
            await adapter.ExtNotificationAsync("session/notice", DotAcpClientAdapterEarlyUpdateTests.Notice(SessionId, "n"), cancellationToken);
        }

        Assert.Equal(["Dropping update for unknown session sess-new"], DotAcpClientAdapterEarlyUpdateTests.Warnings(logger));
        FakeSessionSink sink = new(SessionId);
        adapter.Register(sink);
        Assert.Equal(256, sink.Events.Count);
    }

    /// <summary>Builds an adapter with a recording logger and a fresh ledger.</summary>
    private static (DotAcpClientAdapter Adapter, ListLogger<DotAcpClientAdapter> Logger) Create()
    {
        ListLogger<DotAcpClientAdapter> logger = new();
        return (new DotAcpClientAdapter(logger, new WireOrderLedger()), logger);
    }

    /// <summary>The messages logged at Warning or above, in order.</summary>
    /// <param name="logger">The adapter's logger.</param>
    private static string[] Warnings(ListLogger<DotAcpClientAdapter> logger) =>
        [.. logger.Entries.Where(entry => entry.Level >= LogLevel.Warning).Select(entry => entry.Message)];

    /// <summary>Builds the params of a renamed notice the way dotacp hands them over: Newtonsoft tokens in a dictionary.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="title">The notice's title.</param>
    private static Dictionary<string, object> Notice(string sessionId, string title) => new(StringComparer.Ordinal)
    {
        ["sessionId"] = new JValue(sessionId),
        ["update"] = new JObject { ["sessionUpdate"] = "notice", ["severity"] = "info", ["title"] = title },
    };
}
