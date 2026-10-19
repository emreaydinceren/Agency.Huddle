using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// Pins the Reference purpose of the Search providers (spec §5.2, D-4, D-29): the picker names things and reads no
/// Transcript or file content, while the Top Bar box still searches content. Covers the Rooms and the Library.
/// </summary>
public sealed class ReferencePurposeProviderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A Room whose name matches is found even when its Transcript is locked, so no Transcript is read.</summary>
    [Fact]
    public async Task Rooms_Reference_NeverReadsATranscript()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Launch Planning", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "deploy one", T0), ct);
        using FileStream hold = new(TranscriptPath(harness, room.Id), FileMode.Open, FileAccess.Read, FileShare.None);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("launch", SearchPurpose.Reference), ct);

        Assert.Equal(new RoomHit(room.Id, "Launch Planning", 0, null, null, null, null, false, MatchTier.NamePrefix), Assert.Single(result.Hits));
    }

    /// <summary>A Room whose Messages contain the phrase but whose name does not is absent from the picker.</summary>
    [Fact]
    public async Task Rooms_Reference_MessageTextMatchOnly_IsAbsent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Quiet", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "login failed", T0), ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("login", SearchPurpose.Reference), ct);

        Assert.Empty(result.Hits);
    }

    /// <summary>A Room whose name matches with an empty Transcript is a row with no count, snippet or Message.</summary>
    [Fact]
    public async Task Rooms_Reference_NameMatch_WithEmptyTranscript_IsPresent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Launch Planning", [], ct);
        RoomSearchProvider provider = Provider(harness, Chats(harness));

        SearchResult result = await provider.SearchAsync(Query("launch", SearchPurpose.Reference), ct);

        Assert.Equal(new RoomHit(room.Id, "Launch Planning", 0, null, null, null, null, false, MatchTier.NamePrefix), Assert.Single(result.Hits));
    }

    /// <summary>The same Room data gives the Top Bar box its content hit, so the box still searches Message text.</summary>
    [Fact]
    public async Task Rooms_Box_StillMatchesMessageText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Quiet", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "login failed", T0), ct);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("login", SearchPurpose.Box), ct);

        Assert.Equal(new RoomHit(room.Id, "Quiet", 1, "m1", "Nova", "login failed", T0, false, MatchTier.ContentMatch), Assert.Single(result.Hits));
    }

    /// <summary>Under the Reference purpose the Transcript is never opened, even when the Room name does not match.</summary>
    [Fact]
    public async Task Rooms_Reference_LockedTranscriptNameMiss_ReturnsEmptyWithoutReading()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileChatStore chats = Chats(harness);
        Room room = await harness.Directory.CreateRoomAsync("Quiet", [], ct);
        await chats.AppendAsync(room.Id, Message("m1", "Nova", "login failed", T0), ct);
        using FileStream hold = new(TranscriptPath(harness, room.Id), FileMode.Open, FileAccess.Read, FileShare.None);
        RoomSearchProvider provider = Provider(harness, chats);

        SearchResult result = await provider.SearchAsync(Query("login", SearchPurpose.Reference), ct);

        Assert.Empty(result.Hits);
    }

    /// <summary>A Room whose Team name matches, and whose own name does not, is found at the NameContains tier.</summary>
    [Fact]
    public async Task Rooms_Reference_MatchedByTeamName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Standup", [], "Platform", false, ct);
        _ = await harness.Directory.CreateRoomAsync("Elsewhere", [], "Marketing", false, ct);
        RoomSearchProvider provider = Provider(harness, Chats(harness));

        SearchResult result = await provider.SearchAsync(Query("platf", SearchPurpose.Reference), ct);

        Assert.Equal(new RoomHit(room.Id, "Standup", 0, null, null, null, null, false, MatchTier.NameContains, "Platform", false), Assert.Single(result.Hits));
    }

    /// <summary>An empty phrase lists every Room of the pass, newest Created first, each at ExactName.</summary>
    [Fact]
    public async Task Rooms_Reference_EmptyBrowsesNewestFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ManualTimeProvider clock = new(T0);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, clock);
        _ = await harness.Directory.CreateRoomAsync("Oldest", [], ct);
        clock.UtcNow = T0.AddMinutes(1);
        _ = await harness.Directory.CreateRoomAsync("Middle", [], ct);
        clock.UtcNow = T0.AddMinutes(2);
        _ = await harness.Directory.CreateRoomAsync("Newest", [], ct);
        RoomSearchProvider provider = Provider(harness, Chats(harness));

        SearchResult result = await provider.SearchAsync(Query(string.Empty, SearchPurpose.Reference), ct);

        Assert.Equal(["Newest", "Middle", "Oldest"], result.Hits.Select(hit => hit.Title));
        Assert.All(result.Hits, hit => Assert.Equal(MatchTier.ExactName, hit.Tier));
    }

    /// <summary>Both purposes fill a Room hit's Team and IsTeamRoom from the Room, and the base TeamName stays null.</summary>
    [Fact]
    public async Task Rooms_BothPurposes_CarryTeamAndTeamRoomFlag()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = await harness.Directory.CreateRoomAsync("Zeta root", [], ct);
        _ = await harness.Directory.CreateRoomAsync("Zeta scoped", [], "Platform", false, ct);
        _ = await harness.Directory.CreateRoomAsync("Zeta team room", [], "Platform", true, ct);
        RoomSearchProvider provider = Provider(harness, Chats(harness));

        foreach (SearchPurpose purpose in new[] { SearchPurpose.Reference, SearchPurpose.Box })
        {
            SearchResult result = await provider.SearchAsync(Query("zeta", purpose), ct);

            Dictionary<string, RoomHit> byName = result.Hits.Cast<RoomHit>().ToDictionary(hit => hit.RoomName, StringComparer.Ordinal);
            Assert.Null(byName["Zeta root"].Team);
            Assert.False(byName["Zeta root"].IsTeamRoom);
            Assert.Equal("Platform", byName["Zeta scoped"].Team);
            Assert.False(byName["Zeta scoped"].IsTeamRoom);
            Assert.Equal("Platform", byName["Zeta team room"].Team);
            Assert.True(byName["Zeta team room"].IsTeamRoom);
            Assert.All(result.Hits, hit => Assert.Null(hit.TeamName));
        }
    }

    /// <summary>A file whose text holds the phrase but whose name does not is absent under Reference; a folder is never a hit.</summary>
    [Fact]
    public async Task Library_Reference_NoContentHitNoFolder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Docs");
        WriteFile(Path.Combine(root, "notes.md"), "the zebra is here");
        Directory.CreateDirectory(Path.Combine(root, "zebra-folder"));

        SearchResult result = await LibraryProvider(fixture).SearchAsync(LibraryQuery("zebra", SearchPurpose.Reference), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>Folders that match the phrase never use up the cap under Reference: the files that match still fill the list (finding 12).</summary>
    [Fact]
    public async Task Library_Reference_ManyMatchingFolders_StillReturnFiles()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Docs");
        for (int number = 0; number < SearchLimits.SingleFilterCap + 5; number++)
        {
            Directory.CreateDirectory(Path.Combine(root, string.Concat("a-zebra-", number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))));
        }

        for (int number = 0; number < 3; number++)
        {
            WriteFile(Path.Combine(root, string.Concat("z-zebra-", number.ToString(System.Globalization.CultureInfo.InvariantCulture), ".md")), "x");
        }

        SearchResult result = await LibraryProvider(fixture).SearchAsync(LibraryQuery("zebra", SearchPurpose.Reference), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Hits.Count);
    }

    /// <summary>A file whose name matches is a hit under Reference, from a pinned root.</summary>
    [Fact]
    public async Task Library_Reference_NameMatch_Appears()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Docs");
        WriteFile(Path.Combine(root, "zebra-plan.md"), "nothing relevant");

        SearchResult result = await LibraryProvider(fixture).SearchAsync(LibraryQuery("zebra", SearchPurpose.Reference), TestContext.Current.CancellationToken);

        LibraryHit hit = Assert.IsType<LibraryHit>(Assert.Single(result.Hits));
        Assert.Equal("zebra-plan.md", hit.Title);
    }

    /// <summary>A file under the Uploads root is found by its name under Reference.</summary>
    [Fact]
    public async Task Library_Reference_FindsAnUploadByName()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        WriteFile(Path.Combine(fixture.DataDir, "Uploads", "quarterly-report.pdf"), "x");

        SearchResult result = await LibraryProvider(fixture).SearchAsync(LibraryQuery("quarterly", SearchPurpose.Reference), TestContext.Current.CancellationToken);

        LibraryHit hit = Assert.IsType<LibraryHit>(Assert.Single(result.Hits));
        Assert.Equal("uploads", hit.RootId);
    }

    /// <summary>A file whose full path holds a backtick is dropped under Reference but found by the box.</summary>
    [Fact]
    public async Task Library_Reference_BacktickPath_IsAbsent()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Docs");
        WriteFile(Path.Combine(root, "tick`y-zebra.md"), "x");
        LibrarySearchProvider provider = LibraryProvider(fixture);

        SearchResult reference = await provider.SearchAsync(LibraryQuery("zebra", SearchPurpose.Reference), TestContext.Current.CancellationToken);
        SearchResult box = await provider.SearchAsync(LibraryQuery("zebra", SearchPurpose.Box), TestContext.Current.CancellationToken);

        Assert.Empty(reference.Hits);
        Assert.Single(box.Hits);
    }

    /// <summary>The Top Bar box purpose still finds a file by its content.</summary>
    [Fact]
    public async Task Library_Box_StillMatchesContent()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Docs");
        WriteFile(Path.Combine(root, "notes.md"), "the zebra is here");

        SearchResult result = await LibraryProvider(fixture).SearchAsync(LibraryQuery("zebra", SearchPurpose.Box), TestContext.Current.CancellationToken);

        Assert.Equal("notes.md", Assert.Single(result.Hits).Title);
    }

    /// <summary>A phrase shorter than the minimum gives the Library nothing under Reference.</summary>
    [Fact]
    public async Task Library_Reference_UnderThree_Empty()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Docs");
        WriteFile(Path.Combine(root, "ze-plan.md"), "x");

        SearchResult result = await LibraryProvider(fixture).SearchAsync(LibraryQuery("ze", SearchPurpose.Reference), TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits);
    }

    /// <summary>An empty Reference phrase gives the Active Tasks in Suggest order, never a Closed one, and nothing in the Archived pass.</summary>
    [Fact]
    public async Task Tasks_Reference_EmptyIsSuggestOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Older", T0, false));
        harness.SeedOnDisk(MakeTask("PLAT-0002", "Newer", T0.AddHours(1), false));
        harness.SeedOnDisk(MakeTask("PLAT-0003", "Closed one", T0.AddHours(2), true));
        TaskSearchProvider provider = new(harness.Store, harness.Personas, harness.Options);

        SearchResult active = await provider.SearchAsync(new SearchQuery(string.Empty, SearchFilter.Tasks, null, SearchScope.Active, 50, SearchPurpose.Reference), ct);
        SearchResult archived = await provider.SearchAsync(new SearchQuery(string.Empty, SearchFilter.Tasks, null, SearchScope.Archived, 50, SearchPurpose.Reference), ct);
        SearchResult box = await provider.SearchAsync(new SearchQuery(string.Empty, SearchFilter.Tasks, null, SearchScope.Active, 50), ct);

        Assert.Equal(["Newer", "Older"], active.Hits.Select(hit => hit.Title));
        Assert.Empty(archived.Hits);
        Assert.Empty(box.Hits);
    }

    /// <summary>Under Reference with an empty phrase the ranker keeps the provider's order, cut to the cap.</summary>
    [Fact]
    public void Ranker_ReferenceEmpty_KeepsProviderOrder()
    {
        SearchHit[] hits =
        [
            new TeamHit("Zulu", null, 0, MatchTier.NameContains),
            new TeamHit("Alpha", null, 0, MatchTier.ExactName),
            new TeamHit("Mike", null, 0, MatchTier.NameContains),
        ];

        SearchResult result = SearchResultRanker.Rank(
            new SearchResult(hits, false),
            new SearchQuery(string.Empty, SearchFilter.All, null, SearchScope.Active, 2, SearchPurpose.Reference));

        Assert.Equal(["Zulu", "Alpha"], result.Hits.Select(hit => hit.Title));
        Assert.True(result.Truncated);
    }

    /// <summary>Under Reference with a phrase the ranker orders as it does for the box.</summary>
    [Fact]
    public void Ranker_ReferenceNonEmpty_RanksAsBox()
    {
        SearchHit[] hits =
        [
            new TeamHit("Zulu", null, 0, MatchTier.NameContains),
            new TeamHit("Alpha", null, 0, MatchTier.ExactName),
        ];

        SearchResult result = SearchResultRanker.Rank(
            new SearchResult(hits, false),
            new SearchQuery("a", SearchFilter.All, null, SearchScope.Active, 50, SearchPurpose.Reference));

        Assert.Equal(["Alpha", "Zulu"], result.Hits.Select(hit => hit.Title));
    }

    /// <summary>Builds a Task on the Platform Team with one Change log entry at <paramref name="updated"/>.</summary>
    /// <param name="id">The Task's id.</param>
    /// <param name="title">The Task's title.</param>
    /// <param name="updated">When the Task was last updated.</param>
    /// <param name="closed">Whether the Task is Closed.</param>
    /// <returns>The Task.</returns>
    private static TaskItem MakeTask(string id, string title, DateTimeOffset updated, bool closed)
    {
        return TestTasks.Make(id: id, title: title, location: new TaskLocation("Platform", null, closed), changeLog: [new ChangeLogEntry(updated, "Human", "created")]);
    }

    /// <summary>A Library query with the given purpose.</summary>
    /// <param name="text">The phrase.</param>
    /// <param name="purpose">The purpose the search runs under.</param>
    /// <returns>The query over the Library, Active pass.</returns>
    private static SearchQuery LibraryQuery(string text, SearchPurpose purpose)
    {
        return new SearchQuery(text, SearchFilter.Library, null, SearchScope.Active, SearchLimits.SingleFilterCap, purpose);
    }

    /// <summary>The Library provider over the fixture's roots.</summary>
    /// <param name="fixture">The fixture whose roots are searched.</param>
    /// <returns>The provider.</returns>
    private static LibrarySearchProvider LibraryProvider(LibraryFileServiceFixture fixture)
    {
        return new LibrarySearchProvider(fixture.RootStore, fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions { DataDir = fixture.DataDir }), null);
    }

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, creating its folder first.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">The file's text.</param>
    private static void WriteFile(string path, string text)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        File.WriteAllText(path, text);
    }

    /// <summary>Makes a Room search query with the given purpose.</summary>
    /// <param name="text">The phrase.</param>
    /// <param name="purpose">The purpose the search runs under.</param>
    /// <returns>The query over the Rooms, active pass.</returns>
    private static SearchQuery Query(string text, SearchPurpose purpose)
    {
        return new SearchQuery(text, SearchFilter.Rooms, null, SearchScope.Active, SearchLimits.SingleFilterCap, purpose);
    }

    /// <summary>A Transcript store over real files in the test data folder.</summary>
    /// <param name="harness">The harness that owns the folder.</param>
    /// <returns>The store.</returns>
    private static FileChatStore Chats(TaskToolHarness harness)
    {
        return harness.Options.CreateChatStore();
    }

    /// <summary>The Room provider over the harness's directory.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="chats">The Transcript store.</param>
    /// <returns>The provider.</returns>
    private static RoomSearchProvider Provider(TaskToolHarness harness, IChatStore chats)
    {
        return new RoomSearchProvider(harness.Directory, chats, NullLogger<RoomSearchProvider>.Instance);
    }

    /// <summary>A Message from a sender.</summary>
    /// <param name="id">The Message Id.</param>
    /// <param name="sender">The sender's Name.</param>
    /// <param name="text">The Message text.</param>
    /// <param name="timestamp">When it was sent.</param>
    /// <returns>The Message.</returns>
    private static ChatMessage Message(string id, string sender, string text, DateTimeOffset timestamp)
    {
        return new ChatMessage(id, timestamp, sender + "-id", sender, text);
    }

    /// <summary>The path of one Room's Transcript file.</summary>
    /// <param name="harness">The harness that owns the data folder.</param>
    /// <param name="roomId">The Room's Id.</param>
    /// <returns>The file path.</returns>
    private static string TranscriptPath(TaskToolHarness harness, string roomId)
    {
        return Path.Combine(harness.Options.Value.DataDir, "rooms", roomId + ".jsonl");
    }
}
