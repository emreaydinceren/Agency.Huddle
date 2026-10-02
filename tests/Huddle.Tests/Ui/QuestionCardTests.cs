using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins Questions spec §6.7: <see cref="QuestionCard"/> renders the waiting Questions where the
/// conversation happened, answers through the real (internal) <see cref="QuestionService"/> with no
/// Envelope, holds its options back while the asker is still writing, and stays in step with
/// <see cref="RoomEvents.QuestionsChanged"/> whether it caused the change or another tab did. Built
/// like <c>ProposalCardTests</c>: real <see cref="SqliteTeamDirectory"/>, <see cref="ChatService"/>,
/// <see cref="QuestionStore"/> and <see cref="QuestionService"/> sharing one <see cref="TempDataDir"/>,
/// registered into a plain <see cref="MudBunitContext"/>.
/// </summary>
public sealed class QuestionCardTests
{
    /// <summary>Q3: a card holding one single select Question has no Send button, and one tap on an option posts the answer.</summary>
    [Fact]
    public async Task SingleQuestion_OneTapSendsAndCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(room, coach, new Question("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Send");

        await cut.InvokeAsync(() => FindButton(cut, "Cardio").ClickAsync());

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
        IReadOnlyList<Agency.Huddle.Contracts.ChatMessage> history = await fixture.Store.ReadAllAsync(room.Id, ct);
        Agency.Huddle.Contracts.ChatMessage posted = Assert.Single(history);
        Assert.Equal(KnownIds.Human, posted.SenderId);
        Assert.Equal("> What is your main goal?\n\nCardio\n\n@Coach", posted.Text);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>The header names the asker and each Question is a labelled group (§6.7's accessibility rule), with options exposing <c>aria-pressed</c>.</summary>
    [Fact]
    public async Task Renders_HeaderGroupsAndPressedState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(
            room,
            coach,
            new Question("Which days can you train?", ["Mon", "Wed"], QuestionKind.MultiSelect),
            new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        Assert.Equal("Coach is asking", cut.Find(".question-card-title").TextContent.Trim());
        Assert.Equal("Choose any.", cut.Find(".question-card-caption").TextContent.Trim());
        Assert.NotNull(cut.Find("[role=group][aria-label='Which days can you train?']"));
        Assert.NotNull(cut.Find("[role=group][aria-label='Goal?']"));
        Assert.All(cut.FindAll("button[aria-pressed]"), button => Assert.Equal("false", button.GetAttribute("aria-pressed")));
        Assert.Equal(4, cut.FindAll("button[aria-pressed]").Count);
    }

    /// <summary>§6.7: Send is disabled until every single select has a choice and every multi select has at least one.</summary>
    [Fact]
    public async Task Send_DisabledUntilEveryQuestionIsAnswered()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(
            room,
            coach,
            new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect),
            new Question("Days?", ["Mon", "Wed", "Fri"], QuestionKind.MultiSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);
        Assert.True(FindButton(cut, "Send").HasAttribute("disabled"));

        await cut.InvokeAsync(() => FindButton(cut, "Strength").ClickAsync());
        Assert.True(FindButton(cut, "Send").HasAttribute("disabled"));

        await cut.InvokeAsync(() => FindButton(cut, "Wed").ClickAsync());
        Assert.False(FindButton(cut, "Send").HasAttribute("disabled"));

        await cut.InvokeAsync(() => FindButton(cut, "Wed").ClickAsync());
        Assert.True(FindButton(cut, "Send").HasAttribute("disabled"));
    }

    /// <summary>Choosing a single select option clears the others; a multi select toggles, and sends in option order whatever order it was tapped.</summary>
    [Fact]
    public async Task SingleClears_MultiToggles_AndSendPostsBoth()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(
            room,
            coach,
            new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect),
            new Question("Days?", ["Mon", "Wed", "Fri"], QuestionKind.MultiSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        await cut.InvokeAsync(() => FindButton(cut, "Strength").ClickAsync());
        await cut.InvokeAsync(() => FindButton(cut, "Cardio").ClickAsync());
        await cut.InvokeAsync(() => FindButton(cut, "Fri").ClickAsync());
        await cut.InvokeAsync(() => FindButton(cut, "Mon").ClickAsync());

        Assert.Equal("false", FindButton(cut, "Strength").GetAttribute("aria-pressed"));
        Assert.Equal("true", FindButton(cut, "Cardio").GetAttribute("aria-pressed"));
        Assert.Equal("true", FindButton(cut, "Fri").GetAttribute("aria-pressed"));
        Assert.Equal("true", FindButton(cut, "Mon").GetAttribute("aria-pressed"));
        Assert.Equal("false", FindButton(cut, "Wed").GetAttribute("aria-pressed"));

        await cut.InvokeAsync(() => FindButton(cut, "Send").ClickAsync());

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
        Agency.Huddle.Contracts.ChatMessage posted = Assert.Single(await fixture.Store.ReadAllAsync(room.Id, ct));
        Assert.Equal("> Goal?\n\nCardio\n\n> Days?\n\nMon, Fri\n\n@Coach", posted.Text);
    }

    /// <summary>
    /// Q10 and D-9: a ranking starts in the Agent's order, up and down buttons reorder it (read
    /// aloud as "Move Speed up"), the end rows cannot move off the list, and Send posts the new
    /// order on one line.
    /// </summary>
    [Fact]
    public async Task Rank_UpAndDownReorderAndSendPostsTheOrder()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(
            room, coach, new Question("Rank what matters most", ["Cost", "Speed", "Quality"], QuestionKind.RankPriorities)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);
        Assert.Equal("Most important first.", cut.Find(".question-card-caption").TextContent.Trim());
        Assert.False(FindButton(cut, "Send").HasAttribute("disabled"));
        Assert.True(FindButton(cut, "Move Cost up").HasAttribute("disabled"));
        Assert.True(FindButton(cut, "Move Quality down").HasAttribute("disabled"));

        await cut.InvokeAsync(() => FindButton(cut, "Move Speed up").ClickAsync());

        string[] labels = [.. cut.FindAll(".question-card-rank-label").Select(label => label.TextContent.Trim())];
        Assert.Equal(["1. Speed", "2. Cost", "3. Quality"], labels);

        await cut.InvokeAsync(() => FindButton(cut, "Send").ClickAsync());

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
        Agency.Huddle.Contracts.ChatMessage posted = Assert.Single(await fixture.Store.ReadAllAsync(room.Id, ct));
        Assert.Equal("> Rank what matters most\n\n1. Speed · 2. Cost · 3. Quality\n\n@Coach", posted.Text);
    }

    /// <summary>
    /// Q11 and D-8: while the asker has a Draft in the Room its options are disabled, with the
    /// caption saying why, so the asker's framing Message lands before the answer. They are enabled
    /// again as soon as the Draft is gone.
    /// </summary>
    [Fact]
    public async Task AskerWriting_DisablesOptionsUntilTheDraftEnds()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(
            room,
            coach,
            new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect),
            new Question("Days?", ["Mon", "Wed"], QuestionKind.MultiSelect)));
        fixture.Drafts.Append("message-1", room.Id, coach.Id, coach.Name, "Two quick questions first");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        Assert.Equal("Coach is still writing…", cut.Find(".question-card-writing").TextContent.Trim());
        Assert.True(FindButton(cut, "Strength").HasAttribute("disabled"));
        Assert.True(FindButton(cut, "Dismiss") is not null);

        fixture.Drafts.Complete("message-1");
        fixture.Events.PublishDraftChanged(room.Id);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".question-card-writing")));
        Assert.False(FindButton(cut, "Strength").HasAttribute("disabled"));
    }

    /// <summary>E-8: an asker whose Draft is in a different Room does not hold this card back.</summary>
    [Fact]
    public async Task AskerWritingInAnotherRoom_DoesNotDisableOptions()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        Room other = await fixture.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, coach.Id], ct);
        _ = fixture.Questions.TryPut(Fixture.Card(room, coach, new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        fixture.Drafts.Append("message-1", other.Id, coach.Id, coach.Name, "writing elsewhere");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        Assert.False(FindButton(cut, "Strength").HasAttribute("disabled"));
    }

    /// <summary>Q5: Dismiss removes the card and posts nothing, so the asker is not woken.</summary>
    [Fact]
    public async Task Dismiss_PostsNothingAndCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(room, coach, new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        await cut.InvokeAsync(() => FindButton(cut, "Dismiss").ClickAsync());

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
        Assert.Empty(await fixture.Store.ReadAllAsync(room.Id, ct));
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>Q12: a second tab answers the same card by calling the service directly, and this card, which did not cause the change, still hides on <see cref="RoomEvents.QuestionsChanged"/>.</summary>
    [Fact]
    public async Task OtherTabAnswers_CardDisappearsOnQuestionsChanged()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        PendingQuestions card = Fixture.Card(room, coach, new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect));
        _ = fixture.Questions.TryPut(card);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);
        Assert.Equal("Coach is asking", cut.Find(".question-card-title").TextContent.Trim());

        string? text = await fixture.Service.AnswerAsync(room.Id, card.Id, [new QuestionAnswer([0])], ct);

        Assert.NotNull(text);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".question-card-title")));
    }

    /// <summary>A card replaced by the same asker shows the new Questions and drops the choices made on the old one.</summary>
    [Fact]
    public async Task Replaced_ShowsNewQuestionsAndResetsChoices()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(
            room,
            coach,
            new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect),
            new Question("Days?", ["Mon", "Wed"], QuestionKind.MultiSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);
        await cut.InvokeAsync(() => FindButton(cut, "Strength").ClickAsync());
        Assert.Equal("true", FindButton(cut, "Strength").GetAttribute("aria-pressed"));

        _ = fixture.Questions.TryPut(Fixture.Card(
            room,
            coach,
            new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect),
            new Question("How long per session?", ["30 min", "60 min"], QuestionKind.SingleSelect)));

        cut.WaitForAssertion(() => Assert.Contains("How long per session?", cut.FindAll("[role=group]").Select(group => group.GetAttribute("aria-label"))));
        Assert.Equal("false", FindButton(cut, "Strength").GetAttribute("aria-pressed"));
    }

    /// <summary>E-4: an asker renamed while its card waited is shown under its current Name.</summary>
    [Fact]
    public async Task AskerRenamed_HeaderShowsTheCurrentName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(room, coach, new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        Assert.True(fixture.Directory.RenameUser(coach.Id, "Trainer"));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        Assert.Equal("Trainer is asking", cut.Find(".question-card-title").TextContent.Trim());
    }

    /// <summary>§6.7: the card is not rendered for an Archived Room, even if a card was stored directly.</summary>
    [Fact]
    public async Task ArchivedRoom_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(room, coach, new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        Assert.True(string.IsNullOrWhiteSpace(cut.Markup));
    }

    /// <summary>A Room with no waiting card renders nothing at all.</summary>
    [Fact]
    public async Task NoCard_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, _) = await fixture.RoomWithCoachAsync(ct);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);

        Assert.True(string.IsNullOrWhiteSpace(cut.Markup));
    }

    /// <summary>rules.md: every <see cref="RoomEvents"/> subscriber unsubscribes in <c>Dispose</c>, so a disposed card leaves nothing behind on the singleton hub.</summary>
    [Fact]
    public async Task Dispose_UnsubscribesFromBothEvents()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        (Room room, User coach) = await fixture.RoomWithCoachAsync(ct);
        _ = fixture.Questions.TryPut(Fixture.Card(room, coach, new Question("Goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect)));
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<QuestionCard> cut = RenderCard(ctx, room.Id);
        Assert.Equal(1, SubscriberCount(fixture.Events, "QuestionsChanged"));
        Assert.Equal(1, SubscriberCount(fixture.Events, "DraftChanged"));

        cut.Instance.Dispose();

        Assert.Equal(0, SubscriberCount(fixture.Events, "QuestionsChanged"));
        Assert.Equal(0, SubscriberCount(fixture.Events, "DraftChanged"));
    }

    /// <summary>How many handlers are subscribed to <paramref name="eventName"/> on <paramref name="events"/>, read from the event's backing field.</summary>
    /// <param name="events">The hub to inspect.</param>
    /// <param name="eventName">The event's name; its backing field has the same name.</param>
    private static int SubscriberCount(RoomEvents events, string eventName)
    {
        System.Reflection.FieldInfo? field = typeof(RoomEvents).GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (field.GetValue(events) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>Renders the card for a Room.</summary>
    /// <param name="ctx">The context to render into.</param>
    /// <param name="roomId">The Room id passed as <see cref="QuestionCard.RoomId"/>.</param>
    private static IRenderedComponent<QuestionCard> RenderCard(MudBunitContext ctx, string roomId)
    {
        return ctx.Render<QuestionCard>(parameters => parameters.Add(p => p.RoomId, roomId));
    }

    /// <summary>The first rendered button whose trimmed text, or its <c>aria-label</c>, equals <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered card to search.</param>
    /// <param name="text">The button's expected label or <c>aria-label</c>.</param>
    private static IElement FindButton(IRenderedComponent<QuestionCard> cut, string text)
    {
        return cut.FindAll("button").First(button =>
            string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal)
            || string.Equals(button.GetAttribute("aria-label"), text, StringComparison.Ordinal));
    }

    /// <summary>The real collaborators <see cref="QuestionCard"/> needs, sharing one <see cref="TempDataDir"/>.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dataDir;

        private Fixture(
            TempDataDir dataDir,
            SqliteTeamDirectory directory,
            FileChatStore store,
            RoomEvents events,
            QuestionStore questions,
            QuestionService service,
            Drafts drafts)
        {
            this.dataDir = dataDir;
            this.Directory = directory;
            this.Store = store;
            this.Events = events;
            this.Questions = questions;
            this.Service = service;
            this.Drafts = drafts;
        }

        /// <summary>The Team Directory a test arranges Rooms and Members through.</summary>
        public SqliteTeamDirectory Directory { get; }

        /// <summary>The Transcript store a test reads a posted answer back from.</summary>
        public FileChatStore Store { get; }

        /// <summary>The hub the card subscribes to.</summary>
        public RoomEvents Events { get; }

        /// <summary>The store a test puts a card into before rendering.</summary>
        public QuestionStore Questions { get; }

        /// <summary>The service a test can call directly to stand in for another tab.</summary>
        public QuestionService Service { get; }

        /// <summary>The open Drafts the card reads to know the asker is still writing.</summary>
        public Drafts Drafts { get; }

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

            return new Fixture(dataDir, directory, store, events, questions, service, new Drafts());
        }

        /// <summary>Creates a Room of the Human and a Teammate named Coach.</summary>
        /// <param name="ct">Cancels the writes.</param>
        public async Task<(Room Room, User Coach)> RoomWithCoachAsync(CancellationToken ct)
        {
            User? coach = await this.Directory.UpsertAgentUserAsync("Coach", null, ct);
            Assert.NotNull(coach);
            Room room = await this.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
            return (room, coach);
        }

        /// <summary>Builds a card for <paramref name="room"/> asked by <paramref name="coach"/>, with a fresh id.</summary>
        /// <param name="room">The Room it waits in.</param>
        /// <param name="coach">The asking Agent.</param>
        /// <param name="questions">The Questions on the card.</param>
        public static PendingQuestions Card(Room room, User coach, params Question[] questions)
        {
            return new PendingQuestions(Guid.CreateVersion7().ToString("N"), room.Id, coach.Id, coach.Name, questions, TimeProvider.System.GetUtcNow());
        }

        /// <summary>Registers every real collaborator <see cref="QuestionCard"/> injects into <paramref name="ctx"/>'s container.</summary>
        /// <param name="ctx">The bUnit context to register into.</param>
        public void RegisterInto(MudBunitContext ctx)
        {
            ctx.Services.AddSingleton<ITeamDirectory>(this.Directory);
            ctx.Services.AddSingleton(this.Events);
            ctx.Services.AddSingleton(this.Questions);
            ctx.Services.AddSingleton(this.Service);
            ctx.Services.AddSingleton(this.Drafts);
        }

        /// <summary>Releases the temp data directory.</summary>
        public void Dispose()
        {
            this.dataDir.Dispose();
        }
    }
}
