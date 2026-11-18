using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins what the transcript and the two Agent cards draw for goldfish mode (Goldfish slice 4, step 4): the start row (C4d) and
/// return row (C4e) as slim system rows recognised by their stamp and their text, the "· goldfish" stamp on an Agent's
/// Message (C5d), and the Question and Elicitation card titles.
/// </summary>
public sealed class GoldfishRowsTests
{
    private const string Brief = "Review the login flow.\nCheck **bold** and [a link](http://example.com) for @Nova.";

    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>C4d: the start row is a system row whose summary is the first line and whose detail is "Their brief:" then the brief word for word.</summary>
    [Fact]
    public async Task StartRow_DrawsSummaryAndVerbatimBriefDetail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        string brief = "Review the login flow.";

        IRenderedComponent<MessageList> cut = RenderList(ctx, StartRowMessage("m-1", brief));

        IElement row = cut.Find("details.system-row[data-message-id='m-1']");
        Assert.Equal("Ruth went into goldfish mode. They don't remember this Room.", row.QuerySelector(".system-row-text")?.TextContent.Trim());
        string detail = row.QuerySelector(".system-row-detail")?.TextContent.Trim() ?? string.Empty;
        Assert.Equal(("Their brief:", brief), (detail[..GoldfishLine.BriefLabel.Length], detail[GoldfishLine.BriefLabel.Length..].Trim()));
        Assert.NotEmpty(row.QuerySelectorAll("summary svg"));
    }

    /// <summary>C4d: a multi-line brief holding a mention and Markdown is shown as plain text, never as pills, links or emphasis.</summary>
    [Fact]
    public async Task StartRow_BriefWithMentionAndMarkdown_IsPlainText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<MessageList> cut = RenderList(ctx, StartRowMessage("m-1", Brief));

        IElement detail = cut.Find("details.system-row .system-row-detail");
        Assert.Equal(Brief, detail.TextContent.Trim()[GoldfishLine.BriefLabel.Length..].Trim());
        Assert.Empty(detail.QuerySelectorAll("a, strong, em, code, [class*=mention]"));
    }

    /// <summary>C4e: a Finished return row drops the leading "@" in its summary and its detail lists the start and end times, the Turns and the spend.</summary>
    [Fact]
    public async Task ReturnRow_Finished_DrawsSummaryAndDetailFromTheStore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Ended, rig.Room.Id, ct, turns: 3, spend: 0.12m, currency: "USD");
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage line = ReturnRowMessage("m-2", GoldfishExit.Finished);

        IRenderedComponent<MessageList> cut = RenderList(ctx, line);

        IElement row = cut.Find("details.system-row[data-message-id='m-2']");
        Assert.Equal("Ruth is back. Goldfish finished: 2 findings, result above.", row.QuerySelector(".system-row-text")?.TextContent.Trim());
        string detail = row.QuerySelector(".system-row-detail")?.TextContent.Trim() ?? string.Empty;
        Assert.Equal(GoldfishLine.ReturnDetail(T0, line.Timestamp, 3, 0.12m, "USD"), detail);
        Assert.EndsWith(", 3 Turns, spent 0.120 USD", detail, StringComparison.Ordinal); // contains-ok: pins the literal spend suffix beside the builder-derived equality above
    }

    /// <summary>C4e: a possession the store no longer holds leaves the summary and shows no detail.</summary>
    [Fact]
    public async Task ReturnRow_PossessionPruned_ShowsNoDetail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<MessageList> cut = RenderList(ctx, ReturnRowMessage("m-2", GoldfishExit.Finished));

        IElement row = cut.Find("details.system-row[data-message-id='m-2']");
        Assert.Equal(("Ruth is back. Goldfish finished: 2 findings, result above.", 0), (row.QuerySelector(".system-row-text")?.TextContent.Trim(), row.QuerySelectorAll(".system-row-detail").Length));
    }

    /// <summary>The result row carries the stamp but is neither a start nor a return row, so it stays an ordinary Huddle row with Huddle's mark and no goldfish stamp.</summary>
    [Fact]
    public async Task ResultRow_StaysAnOrdinaryHuddleRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage result = new("m-3", T0, KnownIds.Huddle, "Huddle", GoldfishLine.ResultText("Review the login flow.", "All good.", null, partial: false)) { Goldfish = "p1" };

        IRenderedComponent<MessageList> cut = RenderList(ctx, result);

        Assert.Equal((0, 1, 0), (cut.FindAll("details.system-row").Count, cut.FindAll(".message-row .huddle-mark").Count, cut.FindAll(".message-goldfish").Count));
    }

    /// <summary>C5d: an Agent's Message carrying the stamp shows "Ruth · goldfish" and a fish with the tooltip.</summary>
    [Fact]
    public async Task AgentMessage_Stamped_ShowsStampAndFishWithTooltip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage reply = new("m-4", T0, rig.Ruth.Id, "Ruth", "Found two issues.") { Goldfish = "p1" };

        IRenderedComponent<MessageList> cut = RenderList(ctx, reply);

        IElement fish = cut.Find(".message-meta .message-goldfish");
        Assert.Equal(("Ruth · goldfish", GoldfishStamp.Tooltip, 1), (cut.Find(".message-sender").TextContent.Trim(), fish.GetAttribute("title"), fish.QuerySelectorAll("svg").Length));
    }

    /// <summary>C5d: an Agent's Message without the stamp shows the plain name and no fish.</summary>
    [Fact]
    public async Task AgentMessage_Unstamped_ShowsNoStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage reply = new("m-4", T0, rig.Ruth.Id, "Ruth", "Found two issues.");

        IRenderedComponent<MessageList> cut = RenderList(ctx, reply);

        Assert.Equal(("Ruth", 0), (cut.Find(".message-sender").TextContent.Trim(), cut.FindAll(".message-goldfish").Count));
    }

    /// <summary>The Human's Message, a task-wake row and a Huddle row that is not goldfish draw as before, with no stamp.</summary>
    [Fact]
    public async Task HumanTaskWakeAndPlainHuddleRows_AreUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage human = new("m-5", T0, KnownIds.Human, "You", "hello");
        ChatMessage wake = new("m-6", T0, KnownIds.Human, "You", "@Grace Task PLAT-0004 \"Fix login\" (Done, Platform) was changed by You:\n- status: Review -> Done\nCall get_task with taskId PLAT-0004 for the full task.");
        ChatMessage huddle = new("m-7", T0, KnownIds.Huddle, "Huddle", "Welcome back.");

        IRenderedComponent<MessageList> cut = RenderList(ctx, human, wake, huddle);

        Assert.Equal((1, 1, 1, 0), (cut.FindAll("div.message-row.human[data-message-id='m-5']").Count, cut.FindAll("details.system-row[data-message-id='m-6']").Count, cut.FindAll("[data-message-id='m-7'] .huddle-mark").Count, cut.FindAll(".message-goldfish").Count));
    }

    /// <summary>The start and return rows are recognised from their stamp and text alone, so they still draw once the possession has ended and nothing is live.</summary>
    [Fact]
    public async Task Rows_StillDraw_AfterThePossessionEnded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Ended, rig.Room.Id, ct, turns: 1);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<MessageList> cut = RenderList(ctx, StartRowMessage("m-1", "Review the login flow."), ReturnRowMessage("m-2", GoldfishExit.CalledBack));

        Assert.Equal((0, 2), (rig.Live.Snapshot.Count, cut.FindAll("details.system-row").Count));
    }

    /// <summary>A stamped Question card is titled "Ruth · goldfish is asking".</summary>
    [Fact]
    public async Task QuestionCard_Stamped_TitleCarriesTheStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Questions.TryPut(rig.QuestionCard(GoldfishStamp.Apply("Ruth")));
        await using MudBunitContext ctx = rig.NewCardContext();

        IRenderedComponent<QuestionCard> cut = ctx.Render<QuestionCard>(parameters => parameters.Add(p => p.RoomId, rig.Room.Id));

        Assert.Equal("Ruth · goldfish is asking", cut.Find(".question-card-title").TextContent.Trim());
    }

    /// <summary>A Question card asked outside goldfish mode keeps the plain title.</summary>
    [Fact]
    public async Task QuestionCard_Plain_TitleHasNoStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Questions.TryPut(rig.QuestionCard("Ruth"));
        await using MudBunitContext ctx = rig.NewCardContext();

        IRenderedComponent<QuestionCard> cut = ctx.Render<QuestionCard>(parameters => parameters.Add(p => p.RoomId, rig.Room.Id));

        Assert.Equal("Ruth is asking", cut.Find(".question-card-title").TextContent.Trim());
    }

    /// <summary>The card shows the Agent's current name, so a rename after the stamped ask reads "Rue · goldfish is asking".</summary>
    [Fact]
    public async Task QuestionCard_StampedThenRenamed_ShowsTheCurrentNameStamped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, agentName: "Rue");
        _ = rig.Questions.TryPut(rig.QuestionCard(GoldfishStamp.Apply("Ruth")));
        await using MudBunitContext ctx = rig.NewCardContext();

        IRenderedComponent<QuestionCard> cut = ctx.Render<QuestionCard>(parameters => parameters.Add(p => p.RoomId, rig.Room.Id));

        Assert.Equal("Rue · goldfish is asking", cut.Find(".question-card-title").TextContent.Trim());
    }

    /// <summary>An Elicitation card asked by an Agent who is in goldfish mode in that card's Room carries the stamp; the card's own <c>RoomId</c> is the Room asked about.</summary>
    [Fact]
    public async Task ElicitationCard_AgentPossessedInThisRoom_TitleCarriesTheStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        _ = rig.Elicitations.Add(rig.Room.Id, rig.Ruth.Id, rig.Ruth.Name, FormOf(SingleQuestionSchema, SingleQuestionMessage));
        await using MudBunitContext ctx = rig.NewCardContext();

        IRenderedComponent<ContainerFragment> cut = RenderElicitation(ctx, rig.Room.Id);

        Assert.Equal("Ruth · goldfish is asking", cut.Find(".elicitation-card-title").TextContent.Trim());
    }

    /// <summary>An Elicitation card keeps the plain title when the Agent is not in goldfish mode, or is in goldfish mode in another Room.</summary>
    [Fact]
    public async Task ElicitationCard_NotPossessedHere_TitleHasNoStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.OtherRoom.Id, ct);
        _ = rig.Elicitations.Add(rig.Room.Id, rig.Ruth.Id, rig.Ruth.Name, FormOf(SingleQuestionSchema, SingleQuestionMessage));
        await using MudBunitContext ctx = rig.NewCardContext();

        IRenderedComponent<ContainerFragment> cut = RenderElicitation(ctx, rig.Room.Id);

        Assert.Equal("Ruth is asking", cut.Find(".elicitation-card-title").TextContent.Trim());
    }

    /// <summary>The Elicitation card follows <see cref="GoldfishLive.Changed"/>: when the possession ends the title loses the stamp with no other event.</summary>
    [Fact]
    public async Task ElicitationCard_PossessionEnds_TitleLosesTheStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        _ = rig.Elicitations.Add(rig.Room.Id, rig.Ruth.Id, rig.Ruth.Name, FormOf(SingleQuestionSchema, SingleQuestionMessage));
        await using MudBunitContext ctx = rig.NewCardContext();
        IRenderedComponent<ContainerFragment> cut = RenderElicitation(ctx, rig.Room.Id);
        Assert.Equal("Ruth · goldfish is asking", cut.Find(".elicitation-card-title").TextContent.Trim());

        Assert.True(rig.Store.BeginReturn("p1", GoldfishExit.Finished, null));

        cut.WaitForAssertion(() => Assert.Equal("Ruth is asking", cut.Find(".elicitation-card-title").TextContent.Trim()));
    }

    /// <summary>B2: the goldfish's first Message keeps its header, stamp and fish when the warm Agent's Message is directly before it.</summary>
    [Fact]
    public async Task AgentMessage_StampedAfterTheSameAgentsPlainOne_KeepsItsHeaderAndStamp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage warm = new("m-1", T0, rig.Ruth.Id, "Ruth", "The goldfish copy is running.");
        ChatMessage goldfish = new("m-2", T0.AddSeconds(5), rig.Ruth.Id, "Ruth", "Found two issues.") { Goldfish = "p1" };

        IRenderedComponent<MessageList> cut = RenderList(ctx, warm, goldfish);

        Assert.Equal(
            ("Ruth · goldfish", 1, 0),
            (cut.Find("[data-message-id='m-2'] .message-sender").TextContent.Trim(), cut.FindAll("[data-message-id='m-2'] .message-goldfish").Count, cut.FindAll("[data-message-id='m-2'].grouped").Count));
    }

    /// <summary>B2: the warm Agent's Message after a goldfish one also starts a group, so the plain name shows again.</summary>
    [Fact]
    public async Task AgentMessage_PlainAfterAStampedOne_StartsANewGroup()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        ChatMessage goldfish = new("m-1", T0, rig.Ruth.Id, "Ruth", "Found two issues.") { Goldfish = "p1" };
        ChatMessage warm = new("m-2", T0.AddSeconds(5), rig.Ruth.Id, "Ruth", "Thanks, I have it.");

        IRenderedComponent<MessageList> cut = RenderList(ctx, goldfish, warm);

        Assert.Equal(("Ruth", 0), (cut.Find("[data-message-id='m-2'] .message-sender").TextContent.Trim(), cut.FindAll("[data-message-id='m-2'] .message-goldfish").Count));
    }

    /// <summary>B7: a stamped Question card whose asker has no Draft left in the Room has its options enabled, so the card can answer the goldfish.</summary>
    [Fact]
    public async Task QuestionCard_StampedAndTheAskerHasNoDraft_OptionsAreEnabled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Waiting, rig.Room.Id, ct);
        _ = rig.Questions.TryPut(rig.QuestionCard(GoldfishStamp.Apply("Ruth")));
        await using MudBunitContext ctx = rig.NewCardContext();

        IRenderedComponent<QuestionCard> cut = ctx.Render<QuestionCard>(parameters => parameters.Add(p => p.RoomId, rig.Room.Id));

        Assert.Equal((false, 0), (cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), "Postgres", StringComparison.Ordinal)).HasAttribute("disabled"), cut.FindAll(".question-card-writing").Count));
    }

    private static ChatMessage StartRowMessage(string id, string brief) =>
        new(id, T0, KnownIds.Huddle, "Huddle", GoldfishLine.StartRow("Ruth", brief)) { Goldfish = "p1" };

    private static ChatMessage ReturnRowMessage(string id, GoldfishExit exit) =>
        new(id, T0.AddMinutes(14), KnownIds.Huddle, "Huddle", GoldfishLine.ReturnLine("Ruth", exit, "You", TimeSpan.FromMinutes(14), isMember: true, findings: 2)) { Goldfish = "p1" };

    private static IRenderedComponent<MessageList> RenderList(MudBunitContext ctx, params ChatMessage[] messages) =>
        ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.HumanIds, [KnownIds.Human]));

    private static IRenderedComponent<ContainerFragment> RenderElicitation(MudBunitContext ctx, string roomId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ElicitationCard>(0);
            builder.AddAttribute(1, nameof(ElicitationCard.RoomId), roomId);
            builder.CloseComponent();
        });

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly TeamWebApplicationFactory factory = new();

        private Rig(TempDataDir dir, SqliteTeamDirectory team, GoldfishStore store, GoldfishLive live, ChatStack stack, User ruth, Room room, Room otherRoom)
        {
            this.dir = dir;
            this.Team = team;
            this.Store = store;
            this.Live = live;
            this.Stack = stack;
            this.Ruth = ruth;
            this.Room = room;
            this.OtherRoom = otherRoom;
            this.Elicitations = new ElicitationStore(stack.Events);
            this.Drafts = new Drafts();
        }

        public SqliteTeamDirectory Team { get; }

        public GoldfishStore Store { get; }

        public GoldfishLive Live { get; }

        public ChatStack Stack { get; }

        public User Ruth { get; }

        public Room Room { get; }

        public Room OtherRoom { get; }

        public ElicitationStore Elicitations { get; }

        public Drafts Drafts { get; }

        public QuestionStore Questions => this.Stack.Questions;

        public static async Task<Rig> CreateAsync(CancellationToken ct, string agentName = "Ruth")
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User ruth = await team.UpsertAgentUserAsync(agentName, null, ct) ?? throw new InvalidOperationException("The Agent was not created.");
            Room room = await team.CreateRoomAsync("Work", [KnownIds.Human, ruth.Id], ct);
            Room other = await team.CreateRoomAsync("Elsewhere", [KnownIds.Human, ruth.Id], ct);
            GoldfishStore store = new(dir.Options());
            GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
            ChatStack stack = ChatServiceFactory.Create(team, dir.Options());
            return new Rig(dir, team, store, live, stack, ruth, room, other);
        }

        /// <summary>A context holding what <see cref="MessageList"/> injects, plus this rig's <see cref="GoldfishLive"/>.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.factory.Services.GetRequiredService<AvatarStore>());
            ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            ctx.Services.AddSingleton(this.factory.Services.GetRequiredService<IOptions<TeamOptions>>());
            ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
            ctx.Services.AddSingleton(this.factory.Services.GetRequiredService<ContextFill>());
            ctx.Services.AddSingleton(this.factory.Services.GetRequiredService<App.Acp.PersonaStore>());
            ctx.Services.AddSingleton(this.Live);
            return ctx;
        }

        /// <summary>A context holding what the Question and Elicitation cards inject, plus this rig's <see cref="GoldfishLive"/>.</summary>
        public MudBunitContext NewCardContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamDirectory>(this.Team);
            ctx.Services.AddSingleton(this.Stack.Events);
            ctx.Services.AddSingleton(this.Stack.Questions);
            ctx.Services.AddSingleton(new QuestionService(this.Stack.Questions, this.Stack.Chat, this.Team, NullLogger<QuestionService>.Instance));
            ctx.Services.AddSingleton(this.Drafts);
            ctx.Services.AddSingleton(this.Elicitations);
            ctx.Services.AddSingleton(new ElicitationService(this.Elicitations, this.Stack.Chat, this.Team, NullLogger<ElicitationService>.Instance));
            ctx.Services.AddSingleton(this.Live);
            return ctx;
        }

        public PendingQuestions QuestionCard(string askerName) =>
            new(Guid.CreateVersion7().ToString("N"), this.Room.Id, this.Ruth.Id, askerName, [new Question("Which database?", ["Postgres", "SQLite"], QuestionKind.SingleSelect)], T0);

        public async Task PossessAsync(string id, PossessionState state, string roomId, CancellationToken ct, int turns = 0, decimal spend = 0m, string? currency = null)
        {
            bool ended = state == PossessionState.Ended;
            Possession row = new(id, this.Ruth.Id, roomId, null, "Review the login flow.", state, ended ? GoldfishExit.Finished : null, null, T0, ended ? T0.AddMinutes(14) : null, turns, spend, currency, null);
            Assert.True(this.Store.TryInsert(row));
            await this.Live.RefreshAsync(ct);
        }

        public void Dispose()
        {
            this.Live.Dispose();
            this.factory.Dispose();
            this.dir.Dispose();
        }
    }
}
