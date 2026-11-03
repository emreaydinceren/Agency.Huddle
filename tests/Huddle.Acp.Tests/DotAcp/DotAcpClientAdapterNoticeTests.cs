using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers how <see cref="DotAcpClientAdapter"/> handles the extension notification <c>session/notice</c>,
/// which is a <c>notice</c> session update after <see cref="NdjsonMethodRewriteStream"/> renamed it: it
/// publishes an <see cref="AgentNotice"/> to the session's sink, and marks the update dispatched in the
/// ledger whatever becomes of it, because the stream counted the line as a <c>session/update</c>. The
/// adapter is driven directly, with the argument shapes dotacp hands it (a dictionary of Newtonsoft tokens).
/// </summary>
public sealed class DotAcpClientAdapterNoticeTests
{
    private const string SessionId = "sess-1";

    private const string Method = "session/notice";

    /// <summary>Gets each wire severity with the <see cref="NoticeSeverity"/> it reads as; an unknown or absent one reads as Info.</summary>
    public static TheoryData<string?, NoticeSeverity> Severities()
    {
        return new TheoryData<string?, NoticeSeverity>
        {
            { "info", NoticeSeverity.Info },
            { "warning", NoticeSeverity.Warning },
            { "error", NoticeSeverity.Error },
            { "critical", NoticeSeverity.Info },
            { null, NoticeSeverity.Info },
        };
    }

    /// <summary>A notice reaches the session's sink as one <see cref="AgentNotice"/> carrying its severity, title and description.</summary>
    [Fact(Timeout = 10000)]
    public async Task Notice_KnownSession_PublishesAgentNotice()
    {
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();

        await fixture.Adapter.ExtNotificationAsync(
            DotAcpClientAdapterNoticeTests.Method,
            DotAcpClientAdapterNoticeTests.Args(SessionId, "warning", "Auto mode unavailable", "The selected model does not support Auto mode."),
            TestContext.Current.CancellationToken);

        AgentEvent published = Assert.Single(fixture.Sink.Events);
        Assert.Equal(new AgentNotice(SessionId, NoticeSeverity.Warning, "Auto mode unavailable", "The selected model does not support Auto mode."), published);
    }

    /// <summary>Each wire severity maps to its <see cref="NoticeSeverity"/>; a severity this client does not know, or none, reads as Info.</summary>
    /// <param name="severity">The wire severity, or null to omit it.</param>
    /// <param name="expected">The severity the notice should carry.</param>
    [Theory(Timeout = 10000)]
    [MemberData(nameof(Severities))]
    public async Task Notice_Severity_MapsToNoticeSeverity(string? severity, NoticeSeverity expected)
    {
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();

        await fixture.Adapter.ExtNotificationAsync(DotAcpClientAdapterNoticeTests.Method, DotAcpClientAdapterNoticeTests.Args(SessionId, severity, "Title", null), TestContext.Current.CancellationToken);

        AgentNotice notice = Assert.IsType<AgentNotice>(Assert.Single(fixture.Sink.Events));
        Assert.Equal(expected, notice.Severity);
    }

    /// <summary>An absent or empty description is published as null, so a reader never shows a dangling separator.</summary>
    /// <param name="description">The wire description, or null to omit it.</param>
    [Theory(Timeout = 10000)]
    [InlineData(null)]
    [InlineData("")]
    public async Task Notice_NoDescription_PublishesNullDescription(string? description)
    {
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();

        await fixture.Adapter.ExtNotificationAsync(DotAcpClientAdapterNoticeTests.Method, DotAcpClientAdapterNoticeTests.Args(SessionId, "info", "Title", description), TestContext.Current.CancellationToken);

        AgentNotice notice = Assert.IsType<AgentNotice>(Assert.Single(fixture.Sink.Events));
        Assert.Null(notice.Description);
    }

    /// <summary>A notice the stream counted is marked dispatched once handled, so a Turn's wire-order barrier is released.</summary>
    [Fact(Timeout = 10000)]
    public async Task Notice_Handled_MarksTheUpdateDispatched()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();
        fixture.Ledger.NoteRead(SessionId);

        await fixture.Adapter.ExtNotificationAsync(DotAcpClientAdapterNoticeTests.Method, DotAcpClientAdapterNoticeTests.Args(SessionId, "info", "Title", null), cancellationToken);

        await fixture.Ledger.WaitUntilDispatchedAsync(SessionId, 1, cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.Single(fixture.Sink.Events);
    }

    /// <summary>A notice with no title is dropped with a Warning, and is still marked dispatched so the Turn is not held to the barrier's cap.</summary>
    [Fact(Timeout = 10000)]
    public async Task Notice_NoTitle_DroppedButStillDispatched()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();
        fixture.Ledger.NoteRead(SessionId);

        await fixture.Adapter.ExtNotificationAsync(DotAcpClientAdapterNoticeTests.Method, DotAcpClientAdapterNoticeTests.Args(SessionId, "warning", null, "No title"), cancellationToken);

        await fixture.Ledger.WaitUntilDispatchedAsync(SessionId, 1, cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.Empty(fixture.Sink.Events);
        Assert.Equal(["Dropping a session notice that carries no readable session id or title."], fixture.Logger.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message));
    }

    /// <summary>A notice for a session nobody registered is dropped with a Warning, and is still marked dispatched.</summary>
    [Fact(Timeout = 10000)]
    public async Task Notice_UnknownSession_DroppedButStillDispatched()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();
        fixture.Ledger.NoteRead("sess-other");

        await fixture.Adapter.ExtNotificationAsync(DotAcpClientAdapterNoticeTests.Method, DotAcpClientAdapterNoticeTests.Args("sess-other", "warning", "Title", null), cancellationToken);

        await fixture.Ledger.WaitUntilDispatchedAsync("sess-other", 1, cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.Empty(fixture.Sink.Events);
        Assert.Equal(["Dropping update for unknown session sess-other"], fixture.Logger.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message));
    }

    /// <summary>Any other extension notification is still ignored: it publishes nothing, even one shaped like a notice.</summary>
    [Fact(Timeout = 10000)]
    public async Task OtherExtensionNotification_PublishesNothing()
    {
        Fixture fixture = DotAcpClientAdapterNoticeTests.CreateFixture();

        await fixture.Adapter.ExtNotificationAsync("session/update", DotAcpClientAdapterNoticeTests.Args(SessionId, "warning", "Title", null), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Sink.Events);
    }

    /// <summary>Builds the params of a renamed notice the way dotacp hands them over: Newtonsoft tokens in a dictionary.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="severity">The severity, or null to omit it.</param>
    /// <param name="title">The title, or null to omit it.</param>
    /// <param name="description">The description, or null to omit it.</param>
    private static Dictionary<string, object> Args(string sessionId, string? severity, string? title, string? description)
    {
        JObject update = new() { ["sessionUpdate"] = "notice" };
        if (severity is not null)
        {
            update["severity"] = severity;
        }

        if (title is not null)
        {
            update["title"] = title;
        }

        if (description is not null)
        {
            update["description"] = description;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sessionId"] = new JValue(sessionId),
            ["update"] = update,
        };
    }

    private static Fixture CreateFixture()
    {
        ListLogger<DotAcpClientAdapter> logger = new();
        WireOrderLedger ledger = new();
        DotAcpClientAdapter adapter = new(logger, ledger);
        FakeSessionSink sink = new(DotAcpClientAdapterNoticeTests.SessionId);
        adapter.Register(sink);
        return new Fixture(adapter, logger, ledger, sink);
    }

    private sealed record Fixture(
        DotAcpClientAdapter Adapter,
        ListLogger<DotAcpClientAdapter> Logger,
        WireOrderLedger Ledger,
        FakeSessionSink Sink);
}
