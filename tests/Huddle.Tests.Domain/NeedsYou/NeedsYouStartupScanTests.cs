using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Fakes;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>Pins <see cref="NeedsYouStartupScan"/> (Needs You spec §6.3) against real Transcripts on disk.</summary>
public sealed class NeedsYouStartupScanTests
{
    /// <summary>A Room whose Transcript ends with an unanswered Mention is listed, with that Message's time and sender.</summary>
    [Fact]
    public async Task UnansweredMention_IsSeeded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        ChatMessage ask = new("m1", new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero), nova.Id, "Nova", "which database, @You?");
        await fixture.Transcript.AppendAsync(room.Id, ask, ct);

        await RunScanAsync(fixture, ct);

        NeedsYouEntry entry = Assert.Single(fixture.Tracker.Snapshot());
        Assert.Equal(room.Id, entry.RoomId);
        Assert.Equal(ask.Timestamp, entry.Since);
        Assert.Equal("Nova", entry.AskedBy);
    }

    /// <summary>A Room where the Human spoke last is not listed.</summary>
    [Fact]
    public async Task AnsweredMention_IsNotSeeded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        await fixture.Transcript.AppendAsync(room.Id, new ChatMessage("m1", DateTimeOffset.UnixEpoch, nova.Id, "Nova", "@You?"), ct);
        await fixture.Transcript.AppendAsync(room.Id, new ChatMessage("m2", DateTimeOffset.UnixEpoch, KnownIds.Human, "You", "yes"), ct);

        await RunScanAsync(fixture, ct);

        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>An Archived Room is scanned too, so unarchiving brings its entry back: the list hides it, the tracker does not.</summary>
    [Fact]
    public async Task ArchivedRoom_IsStillSeeded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        await fixture.Transcript.AppendAsync(room.Id, new ChatMessage("m1", DateTimeOffset.UnixEpoch, nova.Id, "Nova", "@You?"), ct);
        await fixture.Team.SetRoomArchivedAsync(room.Id, true, ct);

        await RunScanAsync(fixture, ct);

        Assert.Single(fixture.Tracker.Snapshot());
    }

    /// <summary>One Room failing to scan does not stop the Rooms after it.</summary>
    [Fact]
    public async Task OneRoomFailing_DoesNotStopTheRest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room broken = await fixture.CreateRoomAsync("Broken", [nova], ct);
        Room fine = await fixture.CreateRoomAsync("Fine", [nova], ct);
        await fixture.Transcript.AppendAsync(fine.Id, new ChatMessage("m1", DateTimeOffset.UnixEpoch, nova.Id, "Nova", "@You?"), ct);
        ThrowingTailStore store = new(fixture.Transcript, broken.Id);

        await RunScanAsync(fixture, store, ct);

        Assert.Equal(fine.Id, Assert.Single(fixture.Tracker.Snapshot()).RoomId);
    }

    private static Task RunScanAsync(NeedsYouFixture fixture, CancellationToken ct) => RunScanAsync(fixture, fixture.Transcript, ct);

    private static async Task RunScanAsync(NeedsYouFixture fixture, IChatStore store, CancellationToken ct)
    {
        NeedsYouStartupScan scan = new(
            fixture.Tracker,
            fixture.Team,
            store,
            new FakeMentionAliasSource(),
            NullLogger<NeedsYouStartupScan>.Instance);

        await scan.StartAsync(ct);
        Assert.NotNull(scan.ExecuteTask);
        await scan.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(10), ct);
    }

    /// <summary>A Transcript store whose tail read throws for one Room.</summary>
    /// <param name="inner">The real store every other Room reads through.</param>
    /// <param name="failingRoomId">The Room whose tail read throws.</param>
    private sealed class ThrowingTailStore(IChatStore inner, string failingRoomId) : ChatStoreDecorator(inner)
    {
        /// <inheritdoc/>
        public override Task<IReadOnlyList<ChatMessage>> ReadTailAsync(string roomId, int count, CancellationToken ct = default)
        {
            return string.Equals(roomId, failingRoomId, StringComparison.Ordinal)
                ? throw new IOException("disk gone")
                : base.ReadTailAsync(roomId, count, ct);
        }
    }
}
