using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// The server side of the <see cref="FreshStartNote"/> (Spec LR-R21, LR-R22; corrections C4, C12): a Member
/// Agent's note is kept per Room and Agent, survives an app restart, raises a Room event, and is refused
/// from anyone else.
/// </summary>
public sealed class FreshStartNoteHandlingTests
{
    private static readonly FreshStartNote FirstNote = new("PLACEHOLDER", "the session was lost", 12, 3400, false);

    /// <summary>A Member's note is stored under the connection's Agent name and the event fires once for that Room.</summary>
    [Fact]
    public async Task Note_FromAMember_IsStoredUnderTheAgentName_AndRaisesTheEventOnce()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        List<string> raised = [];
        TaskCompletionSource<string> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Services.GetRequiredService<RoomEvents>().FreshStartNotesChanged += room =>
        {
            raised.Add(room);
            first.TrySetResult(room);
        };

        await client.WriteAsync(FirstNote with { RoomId = roomId }, ct);
        using var registration = ct.Register(() => first.TrySetCanceled(ct));
        await first.Task;

        var stored = Assert.Single(fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>().For(roomId));
        Assert.Equal("echo", stored.AgentName);
        Assert.Equal(FirstNote with { RoomId = roomId }, stored.Note);
        Assert.Equal([roomId], raised);
    }

    /// <summary>A second note from the same Agent for the same Room replaces the first.</summary>
    [Fact]
    public async Task Note_SecondFromTheSameAgent_ReplacesTheFirst()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        var notes = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>();
        FreshStartNote second = new(roomId, "the session expired", 40, 9000, true);

        await client.WriteAsync(FirstNote with { RoomId = roomId }, ct);
        await Eventually.UntilAsync(() => notes.For(roomId).Count == 1, ct);
        await client.WriteAsync(second, ct);
        await Eventually.UntilAsync(() => notes.For(roomId).Any(n => n.Note == second), ct);

        var stored = Assert.Single(notes.For(roomId));
        Assert.Equal(second, stored.Note);
    }

    /// <summary>A new <c>FreshStartNotes</c> over the same Data Dir, which is what an app restart builds, still returns the note.</summary>
    [Fact]
    public async Task Note_AfterARestart_IsStillReturned()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        var notes = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>();
        await client.WriteAsync(FirstNote with { RoomId = roomId }, ct);
        await Eventually.UntilAsync(() => notes.For(roomId).Count == 1, ct);

        Agency.Huddle.App.Pipes.FreshStartNotes restarted = NewNotesOverTheSameDataDir(fixture);

        var stored = Assert.Single(restarted.For(roomId));
        Assert.Equal("echo", stored.AgentName);
        Assert.Equal(FirstNote with { RoomId = roomId }, stored.Note);
    }

    /// <summary>After <c>Dismiss</c> a new instance over the same Data Dir returns nothing, and so does the dismissed one.</summary>
    [Fact]
    public async Task Dismiss_RemovesTheNote_ForTheNextInstanceToo()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        var notes = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>();
        await client.WriteAsync(FirstNote with { RoomId = roomId }, ct);
        await Eventually.UntilAsync(() => notes.For(roomId).Count == 1, ct);

        notes.Dismiss(roomId, "echo");

        Assert.Empty(notes.For(roomId));
        Assert.Empty(NewNotesOverTheSameDataDir(fixture).For(roomId));
    }

    /// <summary>A note from an Agent that is not a Member of the Room gets the membership check's <see cref="ProtocolError"/> and stores nothing.</summary>
    [Fact]
    public async Task Note_FromANonMember_IsRefusedAndNothingIsStored()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (echo, echoRoomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = echo;
        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        Assert.IsType<Welcome>(await alpha.ReadAsync(ct));
        bool raised = false;
        fixture.Services.GetRequiredService<RoomEvents>().FreshStartNotesChanged += room => raised = room.Length > 0;

        await alpha.WriteAsync(FirstNote with { RoomId = echoRoomId }, ct);

        var error = Assert.IsType<ProtocolError>(await alpha.ReadAsync(ct));
        Assert.Equal(ErrorCodes.NotMember, error.Code);
        Assert.Empty(fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>().For(echoRoomId));
        Assert.Empty(NewNotesOverTheSameDataDir(fixture).For(echoRoomId));
        Assert.False(raised);
    }

    /// <summary>A note whose room id is not a valid id is refused with a <see cref="ProtocolError"/> and nothing is written.</summary>
    [Fact]
    public async Task Note_WithAnInvalidRoomId_IsRefused()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, _) = await ConnectEchoAsync(fixture, ct);
        await using var __ = client;

        await client.WriteAsync(FirstNote with { RoomId = "..\\..\\escape" }, ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.BadMessage, error.Code);
        Assert.False(Directory.Exists(Path.Combine(DataDirOf(fixture), "escape")));
    }

    /// <summary>Negative counts are display data: they are clamped to 0, as <see cref="ToolActivity"/> sanitises its members.</summary>
    [Fact]
    public async Task Note_WithNegativeCounts_IsClampedToZero()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        var notes = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>();

        await client.WriteAsync(new FreshStartNote(roomId, "the session was lost", -5, -100, false), ct);
        await Eventually.UntilAsync(() => notes.For(roomId).Count == 1, ct);

        var stored = Assert.Single(notes.For(roomId));
        Assert.Equal(0, stored.Note.MessageCount);
        Assert.Equal(0, stored.Note.EstimatedTokens);
    }

    /// <summary>After a note the connection is not sent a <c>BadMessage</c>, so the runner stays healthy.</summary>
    [Fact]
    public async Task Note_FromAMember_IsNotAnswered_WithBadMessage()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using var fixture = await PipeHostFixture.StartAsync(cts);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        var notes = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.FreshStartNotes>();

        await client.WriteAsync(FirstNote with { RoomId = roomId }, ct);
        await Eventually.UntilAsync(() => notes.For(roomId).Count == 1, ct);

        using var quietCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        quietCts.CancelAfter(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadAsync(quietCts.Token));
    }

    /// <summary>Connects as <c>echo</c>, who is a Member of the Room of the same name, and returns the client and that Room's id.</summary>
    private static async Task<(JsonLineStream Client, string RoomId)> ConnectEchoAsync(PipeHostFixture fixture, CancellationToken ct)
    {
        JsonLineStream client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        return (client, Assert.Single(welcome.Rooms).Id);
    }

    /// <summary>The host's Data Dir.</summary>
    private static string DataDirOf(PipeHostFixture fixture) =>
        fixture.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;

    /// <summary>Builds what an app restart builds: fresh notes over a fresh store reading the same Data Dir.</summary>
    private static Agency.Huddle.App.Pipes.FreshStartNotes NewNotesOverTheSameDataDir(PipeHostFixture fixture)
    {
        LastCatchUpStore store = new(
            fixture.Services.GetRequiredService<IOptions<TeamOptions>>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LastCatchUpStore>.Instance);
        return new Agency.Huddle.App.Pipes.FreshStartNotes(store);
    }
}
