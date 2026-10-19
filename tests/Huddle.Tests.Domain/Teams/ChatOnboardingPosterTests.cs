using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="ChatOnboardingPoster"/> over a real directory and <see cref="ChatService"/>: the Team-aware post lands in the
/// Team Room with a Mention (T18) and falls back to the root one-to-one (T19), while <c>PostAsync</c> stays root-only (T20).
/// </summary>
public sealed class ChatOnboardingPosterTests
{
    private sealed record Setup(ChatOnboardingPoster Poster, SqliteTeamDirectory Directory, List<MessagePostedEvent> Posts, User Nova, User Kai);

    private static async Task<Setup> CreateAsync(TempDataDir dir, CancellationToken ct)
    {
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        ChatService chat = new(
            directory, store, events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance);
        List<MessagePostedEvent> posts = [];
        events.MessagePosted += posts.Add;
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        User? kai = await directory.UpsertAgentUserAsync("Kai", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(kai);
        return new Setup(new ChatOnboardingPoster(chat, directory), directory, posts, nova, kai);
    }

    private static Task<Room> TeamRoomAsync(Setup setup, CancellationToken ct) =>
        setup.Directory.CreateRoomAsync("Marketing", [KnownIds.Human, setup.Nova.Id, setup.Kai.Id], "Marketing", true, ct);

    /// <summary>T18: the post lands in the Team Room as the Human, and its text really Mentions the recipient under the parser the reply gate uses.</summary>
    [Fact]
    public async Task PostToTeamAsync_TeamRoomWithRecipient_PostsThereMentioningRecipient()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Setup setup = await CreateAsync(dir, ct);
        Room teamRoom = await TeamRoomAsync(setup, ct);

        bool posted = await setup.Poster.PostToTeamAsync("Marketing", "Nova", "Weekly digest please.", ct);

        Assert.True(posted);
        MessagePostedEvent post = Assert.Single(setup.Posts);
        Assert.Equal(teamRoom.Id, post.Room.Id);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal("@Nova Weekly digest please.", post.Message.Text);
        User mentioned = Assert.Single(MentionParser.Parse(post.Message.Text, post.Members));
        Assert.Equal(setup.Nova.Id, mentioned.Id);
        Assert.Equal(setup.Nova.Id, Assert.Single(post.Mentions).Id);
    }

    /// <summary>T19: the Human archived the Team Room, so the post goes to the recipient's root one-to-one.</summary>
    [Fact]
    public async Task PostToTeamAsync_TeamRoomArchived_FallsBackToRootOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Setup setup = await CreateAsync(dir, ct);
        Room teamRoom = await TeamRoomAsync(setup, ct);
        await setup.Directory.SetRoomArchivedAsync(teamRoom.Id, true, ct);

        bool posted = await setup.Poster.PostToTeamAsync("Marketing", "Nova", "Weekly digest please.", ct);

        Assert.True(posted);
        MessagePostedEvent post = Assert.Single(setup.Posts);
        Assert.NotEqual(teamRoom.Id, post.Room.Id);
        Assert.Null(post.Room.Team);
        Assert.Equal(SortedIds(KnownIds.Human, setup.Nova.Id), SortedMembers(post));
        Assert.Equal("Weekly digest please.", post.Message.Text);
    }

    /// <summary>A Team with no Team Room posts to the recipient's root one-to-one.</summary>
    [Fact]
    public async Task PostToTeamAsync_NoTeamRoom_FallsBackToRootOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Setup setup = await CreateAsync(dir, ct);

        bool posted = await setup.Poster.PostToTeamAsync("Marketing", "Nova", "Weekly digest please.", ct);

        Assert.True(posted);
        MessagePostedEvent post = Assert.Single(setup.Posts);
        Assert.Null(post.Room.Team);
        Assert.Equal(SortedIds(KnownIds.Human, setup.Nova.Id), SortedMembers(post));
    }

    /// <summary>A recipient who is not a Member of the Team Room is posted to at the root, not in the Team Room.</summary>
    [Fact]
    public async Task PostToTeamAsync_RecipientNotInTeamRoom_FallsBackToRootOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Setup setup = await CreateAsync(dir, ct);
        User? sol = await setup.Directory.UpsertAgentUserAsync("Sol", null, ct);
        Assert.NotNull(sol);
        _ = await TeamRoomAsync(setup, ct);

        bool posted = await setup.Poster.PostToTeamAsync("Marketing", "Sol", "Weekly digest please.", ct);

        Assert.True(posted);
        MessagePostedEvent post = Assert.Single(setup.Posts);
        Assert.Null(post.Room.Team);
        Assert.Equal(SortedIds(KnownIds.Human, sol.Id), SortedMembers(post));
    }

    /// <summary>A recipient with no user row cannot be reached, so nothing is posted and the call reports false.</summary>
    [Fact]
    public async Task PostToTeamAsync_UnknownRecipient_ReturnsFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Setup setup = await CreateAsync(dir, ct);
        _ = await TeamRoomAsync(setup, ct);

        bool posted = await setup.Poster.PostToTeamAsync("Marketing", "Nobody", "Weekly digest please.", ct);

        Assert.False(posted);
        Assert.Empty(setup.Posts);
    }

    /// <summary>T20: <c>PostAsync</c> stays root-only - with a Team Room present, the onboarding prompt still goes to the manager's root one-to-one, unMentioned.</summary>
    [Fact]
    public async Task PostAsync_TeamRoomExists_StillPostsToRootOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Setup setup = await CreateAsync(dir, ct);
        Room teamRoom = await TeamRoomAsync(setup, ct);

        bool posted = await setup.Poster.PostAsync("Nova", "Welcome aboard.", ct);

        Assert.True(posted);
        MessagePostedEvent post = Assert.Single(setup.Posts);
        Assert.NotEqual(teamRoom.Id, post.Room.Id);
        Assert.Null(post.Room.Team);
        Assert.Equal("Welcome aboard.", post.Message.Text);
    }

    /// <summary>ADR-0042: a refused post in the Team Room returns false and is not retried in the root one-to-one.</summary>
    [Fact]
    public async Task PostToTeamAsync_TeamRoomPostRefused_ReturnsFalse_AndDoesNotFallBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir otherDir = new();
        Setup setup = await CreateAsync(dir, ct);
        await TeamRoomAsync(setup, ct);
        await setup.Directory.CreateRoomAsync("Nova", [KnownIds.Human, setup.Nova.Id], ct);

        // The ChatService behind the poster knows none of these Rooms, so every post it is asked to make is refused with a ChatException.
        SqliteTeamDirectory emptyDirectory = new(otherDir.Options());
        await emptyDirectory.InitializeAsync("You", ct);
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        List<MessagePostedEvent> posts = [];
        events.MessagePosted += posts.Add;
        ChatService refusing = new(
            emptyDirectory, otherDir.CreateChatStore(), events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance);
        ChatOnboardingPoster poster = new(refusing, setup.Directory);

        bool posted = await poster.PostToTeamAsync("Marketing", "Nova", "Weekly digest please.", ct);

        Assert.False(posted);
        Assert.Empty(posts);
        Assert.Empty(setup.Posts);
    }

    private static string[] SortedMembers(MessagePostedEvent post) => SortedIds(post.Members.Select(m => m.Id).ToArray());

    private static string[] SortedIds(params string[] ids) => [.. ids.Order(StringComparer.Ordinal)];
}
