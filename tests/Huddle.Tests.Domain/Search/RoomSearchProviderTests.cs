using Agency.Huddle.App.Data;
using Agency.Huddle.App.Search;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Search;

/// <summary>
/// Pins <see cref="RoomSearchProvider"/> (spec §8.5 and §7.5, GS-T6): one card per Room with its
/// match count, the newest matching Message, the two passes, the skip-and-fail rules for an
/// unreadable Transcript, cancellation between Rooms, and the searched text.
/// </summary>
public sealed class RoomSearchProviderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Several matching Messages in one Room give one hit whose count is every match.</summary>
    [Fact]
    public async Task OneHitPerRoom_WithTheMatchCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Launch", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "deploy one", T0), ct);
        await chats.AppendAsync(room.Id, Message("m2", "Kai", "nothing here", T0.AddMinutes(1)), ct);
        await chats.AppendAsync(room.Id, Message("m3", "Nova", "deploy two", T0.AddMinutes(2)), ct);
        await chats.AppendAsync(room.Id, Message("m4", "Kai", "DEPLOY three", T0.AddMinutes(3)), ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("deploy"), ct);

        RoomHit hit = Assert.IsType<RoomHit>(Assert.Single(result.Hits));
        Assert.Equal(3, hit.MatchCount);
    }

    /// <summary>The newest matching Message gives its Id, sender, time and snippet; a tie in time goes to the later line of the file.</summary>
    [Fact]
    public async Task NewestMatchingMessage_IdSenderTimeAndSnippet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Launch", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "deploy one", T0), ct);
        await chats.AppendAsync(room.Id, Message("m2", "Kai", "unrelated", T0.AddMinutes(2)), ct);
        await chats.AppendAsync(room.Id, Message("m3", "Kai", "We will deploy the build tonight", T0.AddMinutes(1)), ct);
        await chats.AppendAsync(room.Id, Message("m4", "Mira", "deploy again", T0.AddMinutes(1)), ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("deploy"), ct);

        Assert.Equal(new RoomHit(room.Id, "Launch", 3, "m4", "Mira", "deploy again", T0.AddMinutes(1), false, MatchTier.ContentMatch), Assert.Single(result.Hits));
    }

    /// <summary>A Room whose name matches but whose Transcript has no match is still a hit, with no Message fields and a count of zero.</summary>
    [Fact]
    public async Task RoomNameMatch_WithNoMessages_IsAHit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Launch Planning", [], ct);
        RoomSearchProvider provider = Provider(harness, Chats(harness));

        SearchResult result = await provider.SearchAsync(Query("launch"), ct);

        Assert.Equal(new RoomHit(room.Id, "Launch Planning", 0, null, null, null, null, false, MatchTier.NamePrefix), Assert.Single(result.Hits));
    }

    /// <summary>An Archived Room is found only by the archived pass, and is marked as archived there.</summary>
    [Fact]
    public async Task ArchivedRoom_OnlyInTheArchivedPass()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room retro = await harness.Directory.CreateRoomAsync("Retro", [], ct);
        await chats.AppendAsync(retro.Id, Message("r1", "Nova", "launch notes", T0), ct);
        await harness.Directory.SetRoomArchivedAsync(retro.Id, true, ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult active = await provider.SearchAsync(Query("launch"), ct);
        SearchResult archived = await provider.SearchAsync(Query("launch", SearchScope.Archived), ct);

        Assert.Empty(active.Hits);
        Assert.Equal(new RoomHit(retro.Id, "Retro", 1, "r1", "Nova", "launch notes", T0, true, MatchTier.ContentMatch), Assert.Single(archived.Hits));
    }

    /// <summary>The active pass returns the live Room and never the Archived Room that matches the same phrase.</summary>
    [Fact]
    public async Task ActivePass_NeverReturnsAnArchivedRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room desk = await harness.Directory.CreateRoomAsync("Launch Desk", [], ct);
        Room old = await harness.Directory.CreateRoomAsync("Launch Old", [], ct);
        await harness.Directory.SetRoomArchivedAsync(old.Id, true, ct);
        RoomSearchProvider provider = Provider(harness, Chats(harness));

        SearchResult result = await provider.SearchAsync(Query("launch"), ct);

        Assert.Equal(new RoomHit(desk.Id, "Launch Desk", 0, null, null, null, null, false, MatchTier.NamePrefix), Assert.Single(result.Hits));
    }

    /// <summary>A Room whose Transcript is locked is skipped, and the other Rooms still give their hits.</summary>
    [Fact]
    public async Task UnreadableTranscript_SkipsThatRoomOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room locked = await harness.Directory.CreateRoomAsync("Locked", [], ct);
        Room open = await harness.Directory.CreateRoomAsync("Open", [], ct);
        await chats.AppendAsync(locked.Id, Message("l1", "Nova", "deploy", T0), ct);
        await chats.AppendAsync(open.Id, Message("o1", "Kai", "deploy", T0.AddMinutes(1)), ct);
        using FileStream hold = new(TranscriptPath(harness, locked.Id), FileMode.Open, FileAccess.Read, FileShare.None);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("deploy"), ct);

        Assert.Equal(new RoomHit(open.Id, "Open", 1, "o1", "Kai", "deploy", T0.AddMinutes(1), false, MatchTier.ContentMatch), Assert.Single(result.Hits));
    }

    /// <summary>When every Room in the pass has an unreadable Transcript, the provider throws so the group is marked Failed.</summary>
    [Fact]
    public async Task EveryRoomUnreadable_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room first = await harness.Directory.CreateRoomAsync("First", [], ct);
        Room second = await harness.Directory.CreateRoomAsync("Second", [], ct);
        await chats.AppendAsync(first.Id, Message("f1", "Nova", "deploy", T0), ct);
        await chats.AppendAsync(second.Id, Message("s1", "Nova", "deploy", T0), ct);
        using FileStream holdFirst = new(TranscriptPath(harness, first.Id), FileMode.Open, FileAccess.Read, FileShare.None);
        using FileStream holdSecond = new(TranscriptPath(harness, second.Id), FileMode.Open, FileAccess.Read, FileShare.None);
        RoomSearchProvider provider = Provider(harness, chats);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SearchAsync(Query("deploy"), ct));

        Assert.Equal("No Transcript could be read.", error.Message);
    }

    /// <summary>A cancelled search reads no further Transcript after the Room it was reading, and throws.</summary>
    [Fact]
    public async Task CancelledToken_StopsBeforeTheNextRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room alpha = await harness.Directory.CreateRoomAsync("Alpha", [], ct);
        Room beta = await harness.Directory.CreateRoomAsync("Beta", [], ct);
        await chats.AppendAsync(alpha.Id, Message("a1", "Nova", "deploy", T0), ct);
        await chats.AppendAsync(beta.Id, Message("b1", "Nova", "deploy", T0), ct);
        using CancellationTokenSource cancellation = new();
        CancelAfterFirstReadChatStore store = new(chats, cancellation);
        RoomSearchProvider provider = Provider(harness, store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SearchAsync(Query("deploy"), cancellation.Token));

        Assert.Equal(1, store.Reads);
    }

    /// <summary>The phrase matches a Message whatever the case of either side.</summary>
    [Fact]
    public async Task MatchIsCaseInsensitive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Quiet", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "DEPLOY NOW", T0), ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("deploy"), ct);

        Assert.Equal(new RoomHit(room.Id, "Quiet", 1, "m1", "Nova", "DEPLOY NOW", T0, false, MatchTier.ContentMatch), Assert.Single(result.Hits));
    }

    /// <summary>Only the Message text is searched: a sender's name and a Message Id that contain the phrase do not match.</summary>
    [Fact]
    public async Task OnlyMessageTextIsSearched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Quiet", [], ct);
        await chats.AppendAsync(room.Id, Message("deploy-1", "Deploy Bot", "nothing to see", T0), ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("deploy"), ct);

        Assert.Empty(result.Hits);
    }

    private static FileChatStore Chats(TaskToolHarness harness)
    {
        return new FileChatStore(harness.Options, NullLogger<FileChatStore>.Instance);
    }

    private static RoomSearchProvider Provider(TaskToolHarness harness, IChatStore chats)
    {
        return new RoomSearchProvider(harness.Directory, chats, NullLogger<RoomSearchProvider>.Instance);
    }

    private static SearchQuery Query(string text, SearchScope scope = SearchScope.Active)
    {
        return new SearchQuery(text, SearchFilter.Rooms, null, scope, SearchLimits.SingleFilterCap);
    }

    private static ChatMessage Message(string id, string sender, string text, DateTimeOffset timestamp)
    {
        return new ChatMessage(id, timestamp, sender + "-id", sender, text);
    }

    private static string TranscriptPath(TaskToolHarness harness, string roomId)
    {
        return Path.Combine(harness.Options.Value.DataDir, "rooms", roomId + ".jsonl");
    }

    /// <summary>A Transcript store that cancels the search once its first Room has been read.</summary>
    private sealed class CancelAfterFirstReadChatStore(IChatStore inner, CancellationTokenSource cancellation) : IChatStore
    {
        private int reads;

        /// <summary>How many Transcripts the search has read so far.</summary>
        public int Reads => this.reads;

        /// <summary>Appends through the real store.</summary>
        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            return inner.AppendAsync(roomId, message, ct);
        }

        /// <summary>Reads through the real store, then cancels the search after the first read.</summary>
        public async Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
        {
            this.reads++;
            IReadOnlyList<ChatMessage> messages = await inner.ReadAllAsync(roomId, ct);
            if (this.reads == 1)
            {
                cancellation.Cancel();
            }

            return messages;
        }

        /// <summary>Deletes through the real store.</summary>
        public Task DeleteAsync(string roomId, CancellationToken ct = default)
        {
            return inner.DeleteAsync(roomId, ct);
        }

        /// <summary>Asks the real store.</summary>
        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default)
        {
            return inner.HasMessagesAsync(roomId, ct);
        }
    }
}
