namespace Agency.Huddle.Tests.Questions;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;

/// <summary>
/// Pins <see cref="QuestionService"/> against Questions spec §6.4: the answer is composed as quoted
/// Questions with their answers and a closing Mention of the asker, posted as the Human exactly
/// once, and a Dismiss posts nothing. Real collaborators throughout (<see cref="SqliteTeamDirectory"/>,
/// <see cref="FileChatStore"/>, <see cref="ChatService"/>, <see cref="QuestionStore"/>) in one
/// <see cref="TempDataDir"/> per test, the same style <c>ProposalServiceTests</c> uses.
/// </summary>
public sealed partial class QuestionServiceTests
{
    /// <summary>
    /// §6.4's worked example, all three kinds on one card: a single choice, a multiple choice in
    /// option order, and a ranking on one line. The answer is posted as the Human, ends with the
    /// asker's Mention, and the card is gone afterwards.
    /// </summary>
    [Fact]
    public async Task Answer_ThreeKinds_PostsQuotedAnswersAsHumanMentioningAsker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = ThreeKindsCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);

        string? text = await fixture.Service.AnswerAsync(
            room.Id,
            card.Id,
            [new QuestionAnswer([0]), new QuestionAnswer([0, 2]), new QuestionAnswer([1, 0, 2])],
            ct);

        const string Expected =
            "> What is your main goal?\n\nStrength\n\n" +
            "> Which days can you train?\n\nMon, Fri\n\n" +
            "> Rank what matters most\n\n1. Speed · 2. Cost · 3. Quality\n\n" +
            "@Coach";
        Assert.Equal(Expected, text);
        IReadOnlyList<ChatMessage> history = await fixture.Store.ReadAllAsync(room.Id, ct);
        ChatMessage posted = Assert.Single(history);
        Assert.Equal(KnownIds.Human, posted.SenderId);
        Assert.Equal(Expected, posted.Text);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>
    /// The answer renders as the Human's words under each quoted Question, not inside the quote.
    /// A line directly after <c>&gt; question</c> would be a lazy continuation of the blockquote in
    /// CommonMark, so the spec's blank line between the two is load-bearing.
    /// </summary>
    [Fact]
    public async Task Answer_RenderedAsMarkdown_KeepsTheAnswerOutsideTheQuote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = ThreeKindsCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);

        string? text = await fixture.Service.AnswerAsync(
            room.Id,
            card.Id,
            [new QuestionAnswer([0]), new QuestionAnswer([0, 2]), new QuestionAnswer([1, 0, 2])],
            ct);

        Assert.NotNull(text);
        string html = MarkdownRenderer.ToHtml(text);
        List<string> quotes = [.. BlockquoteRegex().Matches(html).Select(match => match.Groups[1].Value)];
        Assert.Equal(3, quotes.Count);
        Assert.All(quotes, quote => Assert.DoesNotContain("<br", quote, StringComparison.Ordinal));
        Assert.DoesNotContain("Strength", quotes[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Speed", quotes[2], StringComparison.Ordinal);
    }

    /// <summary>Q12 and E-1: a second answer to the same card finds nothing, posts nothing, and returns <see langword="null"/>.</summary>
    [Fact]
    public async Task Answer_SecondAnswer_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = OneQuestionCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);
        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, [new QuestionAnswer([1])], ct);

        string? second = await fixture.Service.AnswerAsync(room.Id, card.Id, [new QuestionAnswer([0])], ct);

        Assert.Null(second);
        Assert.Single(await fixture.Store.ReadAllAsync(room.Id, ct));
    }

    /// <summary>E-2: a tap carrying a replaced card's id posts nothing, and the new card is untouched.</summary>
    [Fact]
    public async Task Answer_StaleCardId_PostsNothingAndKeepsTheNewCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions old = OneQuestionCard(room.Id, coach);
        PendingQuestions replacement = old with { Id = "card-2" };
        _ = fixture.Questions.TryPut(old);
        _ = fixture.Questions.TryPut(replacement);

        string? text = await fixture.Service.AnswerAsync(room.Id, old.Id, [new QuestionAnswer([0])], ct);

        Assert.Null(text);
        Assert.Empty(await fixture.Store.ReadAllAsync(room.Id, ct));
        Assert.Equal(replacement, fixture.Questions.Get(room.Id));
    }

    /// <summary>E-4: an asker renamed while its card waited is Mentioned under its current Name, so it still wakes.</summary>
    [Fact]
    public async Task Answer_AskerRenamed_MentionsNewName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = OneQuestionCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);
        Assert.True(fixture.Directory.RenameUser(coach.Id, "Trainer"));

        string? text = await fixture.Service.AnswerAsync(room.Id, card.Id, [new QuestionAnswer([0])], ct);

        Assert.Equal("> What is your main goal?\n\nStrength\n\n@Trainer", text);
    }

    /// <summary>E-5: an asker that no longer exists gets no Mention, and the Mention line is omitted rather than left dangling.</summary>
    [Fact]
    public async Task Answer_AskerDeleted_OmitsMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human], ct);
        PendingQuestions card = new(
            "card-1", room.Id, "agent-gone", "Coach",
            [new Question("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)],
            DateTimeOffset.UnixEpoch);
        _ = fixture.Questions.TryPut(card);

        string? text = await fixture.Service.AnswerAsync(room.Id, card.Id, [new QuestionAnswer([0])], ct);

        Assert.Equal("> What is your main goal?\n\nStrength", text);
    }

    /// <summary>Q5: Dismiss takes the card and posts nothing, so the asker is not woken.</summary>
    [Fact]
    public async Task Dismiss_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = OneQuestionCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);

        fixture.Service.Dismiss(room.Id, card.Id);

        Assert.Null(fixture.Questions.Get(room.Id));
        Assert.Empty(await fixture.Store.ReadAllAsync(room.Id, ct));
    }

    /// <summary>A Dismiss carrying a stale id leaves the current card alone.</summary>
    [Fact]
    public async Task Dismiss_StaleCardId_KeepsTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = OneQuestionCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);

        fixture.Service.Dismiss(room.Id, "not-the-card");

        Assert.Equal(card, fixture.Questions.Get(room.Id));
    }

    /// <summary>E-9: when the post fails (the Room was deleted mid-tap) the failure is logged, the card is already gone, and the result is <see langword="null"/>.</summary>
    [Fact]
    public async Task Answer_PostFails_ReturnsNullAndTheCardIsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        PendingQuestions card = new(
            "card-1", "no-such-room", "agent-coach", "Coach",
            [new Question("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)],
            DateTimeOffset.UnixEpoch);
        _ = fixture.Questions.TryPut(card);

        string? text = await fixture.Service.AnswerAsync("no-such-room", card.Id, [new QuestionAnswer([0])], ct);

        Assert.Null(text);
        Assert.Null(fixture.Questions.Get("no-such-room"));
    }

    /// <summary>
    /// §6.4's "answers match" table: an answer the card could not have built - wrong count, an
    /// index off the end, two choices for a single select, none for a multi select, a repeated or
    /// out-of-order choice, or a ranking that leaves an option out - is a UI bug and throws, rather
    /// than posting something the Human never chose.
    /// </summary>
    [Theory]
    [MemberData(nameof(MismatchedAnswers))]
    public async Task Answer_AnswersDoNotMatchTheCard_Throws(QuestionAnswer[] answers)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        PendingQuestions card = ThreeKindsCard(room.Id, coach);
        _ = fixture.Questions.TryPut(card);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AnswerAsync(room.Id, card.Id, answers, ct));

        Assert.Empty(await fixture.Store.ReadAllAsync(room.Id, ct));
    }

    /// <summary>The mismatched answers for <see cref="Answer_AnswersDoNotMatchTheCard_Throws"/>, each against <see cref="ThreeKindsCard"/>.</summary>
    public static TheoryData<QuestionAnswer[]> MismatchedAnswers() => new()
    {
        { [new QuestionAnswer([0])] },
        { [new QuestionAnswer([5]), new QuestionAnswer([0]), new QuestionAnswer([0, 1, 2])] },
        { [new QuestionAnswer([0, 1]), new QuestionAnswer([0]), new QuestionAnswer([0, 1, 2])] },
        { [new QuestionAnswer([0]), new QuestionAnswer([]), new QuestionAnswer([0, 1, 2])] },
        { [new QuestionAnswer([0]), new QuestionAnswer([2, 0]), new QuestionAnswer([0, 1, 2])] },
        { [new QuestionAnswer([0]), new QuestionAnswer([1, 1]), new QuestionAnswer([0, 1, 2])] },
        { [new QuestionAnswer([0]), new QuestionAnswer([0]), new QuestionAnswer([0, 1])] },
        { [new QuestionAnswer([0]), new QuestionAnswer([0]), new QuestionAnswer([0, 1, 1])] },
    };

    /// <summary>A Goal / Days / Rank card with one Question of each kind.</summary>
    /// <param name="roomId">The Room it waits in.</param>
    /// <param name="asker">The asking Agent.</param>
    private static PendingQuestions ThreeKindsCard(string roomId, User asker)
    {
        return new PendingQuestions(
            "card-1",
            roomId,
            asker.Id,
            asker.Name,
            [
                new Question("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect),
                new Question("Which days can you train?", ["Mon", "Wed", "Fri"], QuestionKind.MultiSelect),
                new Question("Rank what matters most", ["Cost", "Speed", "Quality"], QuestionKind.RankPriorities),
            ],
            DateTimeOffset.UnixEpoch);
    }

    /// <summary>A card with one single-select Question.</summary>
    /// <param name="roomId">The Room it waits in.</param>
    /// <param name="asker">The asking Agent.</param>
    private static PendingQuestions OneQuestionCard(string roomId, User asker)
    {
        return new PendingQuestions(
            "card-1",
            roomId,
            asker.Id,
            asker.Name,
            [new Question("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)],
            DateTimeOffset.UnixEpoch);
    }

    /// <summary>Matches the inside of every rendered blockquote.</summary>
    [GeneratedRegex("<blockquote>(.*?)</blockquote>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockquoteRegex();

    /// <summary>The real collaborators the service runs against, in one <see cref="TempDataDir"/>.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dataDir;

        private Fixture(TempDataDir dataDir, SqliteTeamDirectory directory, FileChatStore store, QuestionStore questions, QuestionService service)
        {
            this.dataDir = dataDir;
            this.Directory = directory;
            this.Store = store;
            this.Questions = questions;
            this.Service = service;
        }

        /// <summary>The Team Directory a test arranges Rooms and Members through.</summary>
        public SqliteTeamDirectory Directory { get; }

        /// <summary>The Transcript store a test reads the posted answer back from.</summary>
        public FileChatStore Store { get; }

        /// <summary>The store a test puts a card into before answering it.</summary>
        public QuestionStore Questions { get; }

        /// <summary>The <see cref="QuestionService"/> under test.</summary>
        public QuestionService Service { get; }

        /// <summary>Builds a fixture with an initialised Team Directory (Human Name <c>"You"</c>).</summary>
        /// <param name="ct">Cancels initialisation.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct)
        {
            TempDataDir dataDir = new();
            IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = dataDir.Path });
            SqliteTeamDirectory directory = new(options);
            await directory.InitializeAsync("You", ct);
            FileChatStore store = new(options, NullLogger<FileChatStore>.Instance);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            QuestionStore questions = new(events);
            ChatService chat = new(
                directory, store, events, new FakeMentionAliasSource(), options, new ProposalStore(events), questions, NullLogger<ChatService>.Instance);
            QuestionService service = new(questions, chat, directory, NullLogger<QuestionService>.Instance);

            return new Fixture(dataDir, directory, store, questions, service);
        }

        /// <summary>Registers an Agent User under <paramref name="name"/>.</summary>
        /// <param name="name">The Agent's Name.</param>
        /// <param name="ct">Cancels the write.</param>
        public async Task<User> AddAgentAsync(string name, CancellationToken ct)
        {
            User? agent = await this.Directory.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }

        /// <summary>Releases the temp data directory.</summary>
        public void Dispose()
        {
            this.dataDir.Dispose();
        }
    }
}
