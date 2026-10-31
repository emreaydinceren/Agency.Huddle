using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>Tests for <see cref="RoomSessionHarness"/>: the dispose order and the adoption of a hand-built session.</summary>
public sealed class RoomSessionHarnessTests
{
    /// <summary>Disposing the harness cancels the run token before it disposes the session, so the consumer loop can end.</summary>
    [Fact]
    public async Task DisposeAsync_OpenSession_CancelsRunTokenBeforeSessionIsDisposed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession agentSession = new();
        FakeRoomSessionOwner owner = new();
        RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(agentSession));
        bool cancelledWhenSessionDisposed = false;
        agentSession.OnDisposed = () => cancelledWhenSessionDisposed = harness.RunToken.IsCancellationRequested;
        await harness.Room.OpenAsync(ct);

        await harness.DisposeAsync();

        Assert.True(agentSession.Disposed);
        Assert.True(cancelledWhenSessionDisposed);
    }

    /// <summary>Disposing the harness disposes the run token source last, and a second dispose does nothing.</summary>
    [Fact]
    public async Task DisposeAsync_AdoptedSession_DisposesTheAdoptedTokenSourceOnce()
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: RoomSessionHarness.FixedOpen(new FakeAgentSession()),
            owner: new FakeRoomSessionOwner(),
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new Agency.Huddle.App.Acp.AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            runToken: runCts.Token);
        RoomSessionHarness harness = RoomSessionHarness.Adopt(session, runCts);

        await harness.DisposeAsync();
        await harness.DisposeAsync();

        Assert.True(runCts.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => runCts.Token);
    }

    /// <summary>Adopt exposes the very session it was given as <see cref="RoomSessionHarness.Room"/>.</summary>
    [Fact]
    public async Task Adopt_HandBuiltSession_RoomIsThatSession()
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: null,
            open: RoomSessionHarness.FixedOpen(new FakeAgentSession()),
            owner: new FakeRoomSessionOwner(),
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new Agency.Huddle.App.Acp.AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            runToken: runCts.Token);

        await using RoomSessionHarness harness = RoomSessionHarness.Adopt(session, runCts);

        Assert.Same(session, harness.Room);
    }

    /// <summary>The room id is an explicit option: <c>room-1</c> unless the test says otherwise, and <see langword="null"/> when it asks for it.</summary>
    [Fact]
    public async Task Create_RoomIdOption_DefaultsToRoomOneAndAcceptsNull()
    {
        FakeAgentSession agentSession = new();
        await using RoomSessionHarness defaulted = RoomSessionHarness.Create(new FakeRoomSessionOwner(), RoomSessionHarness.FixedOpen(agentSession));
        await using RoomSessionHarness unscoped = RoomSessionHarness.Create(
            new FakeRoomSessionOwner(), RoomSessionHarness.FixedOpen(agentSession), new RoomSessionHarnessOptions { RoomId = null });

        Assert.Equal("room-1", defaulted.Room.RoomId);
        Assert.Null(unscoped.Room.RoomId);
    }

    /// <summary>FixedOpen hands back the same session on every call.</summary>
    [Fact]
    public async Task FixedOpen_CalledTwice_ReturnsTheSameSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession agentSession = new();
        Func<CancellationToken, Task<Agency.Huddle.Acp.Abstractions.IAgentSession>> open = RoomSessionHarness.FixedOpen(agentSession);

        Assert.Same(agentSession, await open(ct));
        Assert.Same(agentSession, await open(ct));
    }
}
