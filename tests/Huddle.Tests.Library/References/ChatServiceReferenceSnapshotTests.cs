using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.References;

/// <summary>A posted Message carries the reference snapshot built from its text, whoever sent it (spec §8.2, AT-T7).</summary>
public sealed class ChatServiceReferenceSnapshotTests
{
    private static readonly MessageReference FixLogin = new(ReferenceKind.Task, "PLAT-0001", "PLAT-0001", "Fix login", "In Progress");

    private static TaskItem PlatformTask() => Tasks.TestTasks.Make(
        id: "PLAT-0001",
        title: "Fix login",
        status: TaskState.InProgress,
        location: new TaskLocation("Platform", null, false));

    /// <summary>A Human post that names a Task is stored with the Task's snapshot.</summary>
    [Fact]
    public async Task HumanPost_GetsSnapshot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, "please look at PLAT-0001", ct: ct);

        Assert.Equal([ChatServiceReferenceSnapshotTests.FixLogin], message.References);
    }

    /// <summary>An Agent post gets the snapshot too (OP-3).</summary>
    [Fact]
    public async Task AgentPost_GetsSnapshot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, fixture.Nova.Id, "done with PLAT-0001", ct: ct);

        Assert.Equal([ChatServiceReferenceSnapshotTests.FixLogin], message.References);
    }

    /// <summary>A Message that references nothing has null References and a JSONL line with no <c>references</c> key.</summary>
    [Fact]
    public async Task NoReferences_ReferencesNullAndLineUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, "just words", ct: ct);

        Assert.Null(message.References);
        string line = (await File.ReadAllTextAsync(fixture.TranscriptPath(room.Id), ct)).TrimEnd();
        Assert.DoesNotContain("references", line, StringComparison.Ordinal);
        using var document = System.Text.Json.JsonDocument.Parse(line);
        Assert.Equal("just words", document.RootElement.GetProperty("text").GetString());
    }

    /// <summary>An id-shaped word that names no Task is not recorded.</summary>
    [Fact]
    public async Task UnresolvedId_NotInSnapshot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, "the file is UTF-8 and ZZZ-0009 is unknown", ct: ct);

        Assert.Null(message.References);
    }

    /// <summary>The Mentions of a Message are the same whether or not a snapshotter is wired (D-13).</summary>
    [Fact]
    public async Task Mentions_SameWithAndWithoutReferences()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        List<string> without = [];
        List<string> with = [];
        RoomEvents plainEvents = new(NullLogger<RoomEvents>.Instance);
        plainEvents.MessagePosted += e => without.AddRange(e.Mentions.Select(m => m.Id));
        RoomEvents snapshotEvents = new(NullLogger<RoomEvents>.Instance);
        snapshotEvents.MessagePosted += e => with.AddRange(e.Mentions.Select(m => m.Id));

        await fixture.Chat(null, plainEvents).PostAsync(room.Id, KnownIds.Human, "@Nova see PLAT-0001", ct: ct);
        await fixture.Chat(fixture.Snapshotter(), snapshotEvents).PostAsync(room.Id, KnownIds.Human, "@Nova see PLAT-0001", ct: ct);

        Assert.Equal([fixture.Nova.Id], without);
        Assert.Equal(without, with);
    }

    /// <summary>A lookup that throws is logged and the Message is posted with null References.</summary>
    [Fact]
    public async Task ThrowingSnapshotter_PostsWithNullReferences()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(new ThrowingCatalog()), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, "see [Marketing](/teams/Marketing)", ct: ct);

        Assert.Equal("see [Marketing](/teams/Marketing)", message.Text);
        Assert.Null(message.References);
    }

    /// <summary>A Team catalog whose lookup fails, standing in for a store that throws.</summary>
    private sealed class ThrowingCatalog : ITeamCatalog
    {
        /// <inheritdoc />
        public IReadOnlyList<TeamSummary> Teams => [];

#pragma warning disable CS0067 // Changed is part of ITeamCatalog; this stand-in never changes, so it never raises it.
        /// <inheritdoc />
        public event Action? Changed;
#pragma warning restore CS0067

        /// <inheritdoc />
        public TeamSummary? Find(string team) => throw new InvalidOperationException("catalog down");

        /// <inheritdoc />
        public bool ProjectExists(string team, string project) => throw new InvalidOperationException("catalog down");
    }
}
