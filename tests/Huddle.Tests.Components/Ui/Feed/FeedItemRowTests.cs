using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="FeedItemRow"/>: the summary and status line of every §7 row, chips, the clamp, dimming, grouping, dismiss and avatar clicks (Feed spec §6.12, §7).</summary>
public sealed class FeedItemRowTests
{
    private static readonly DateTimeOffset Now = FeedTestData.Now;

    private static FeedItem Item(FeedSourceType? type, FeedPayload? payload, bool notify = false, DateTimeOffset? at = null, DateTimeOffset? resolved = null, FeedActor? actor = null, long id = 1) =>
        FeedTestData.Item(type, payload, notify, at, resolved, actor, id);

    private static string Words(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private IRenderedComponent<FeedItemRow> Render(MudBunitContext ctx, IReadOnlyList<FeedItem> items, Action<FeedItem>? onDismiss = null, Action<string>? onTeammate = null) =>
        ctx.Render<FeedItemRow>(parameters =>
        {
            parameters.Add(row => row.Group, new FeedGroup(items));
            if (onDismiss is not null)
            {
                parameters.Add(row => row.OnDismiss, EventCallback.Factory.Create(this, onDismiss));
            }

            if (onTeammate is not null)
            {
                parameters.Add(row => row.OnTeammate, EventCallback.Factory.Create(this, onTeammate));
            }
        });

    /// <summary>Every source type of §7 reads as its summary, with the trailing context, and a Notification adds its status line.</summary>
    [Fact]
    public async Task Row_EachSpecRow_RendersSummaryAndStatusLine()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        string root = library.CreatePinnedRoot("Notes");
        _ = library.Resolve(root, "plan.md");
        FeedActor huddle = FeedActor.Huddle;
        (FeedItem Item, string Summary, string? Status)[] rows =
        [
            (Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-41", "Draft the pricing page", "Growth", null)), "Nova created #GRO-41 Draft the pricing page · in Growth", null),
            (Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-1", "T", "Growth", null)), "Nova turned on Requires your acceptance for #GRO-1 T · in Growth", null),
            (Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-1", "T", "Growth", "Publish")), "Nova set the Action \"Publish\" on #GRO-1 T · in Growth", null),
            (Item(FeedSourceType.TaskNeedsYou, new TaskNeedsYouPayload("GRO-2", "T", "Growth", 3, 3), notify: true), "Nova stopped to wait for your approval on #GRO-2 T · in Growth", "Asked 12:00 · proof 3/3 · waiting for you"),
            (Item(FeedSourceType.TaskAssignedToYou, new TaskAssignedPayload("GRO-3", "T", "Growth", null), notify: true), "Nova assigned you #GRO-3 T", "Asked 12:00 · waiting for you"),
            (Item(FeedSourceType.RequestAwaitsYou, new RequestAwaitsPayload("GRO-4", "T", "Growth", "Design", "request"), notify: true), "Nova finished the Request #GRO-4 T from Design · only you can confirm it", "Asked 12:00 · waiting for you"),
            (Item(FeedSourceType.GateDecisionDue, new GateDecisionPayload("GRO-5", "T", "Growth", "g1", "Needs a human", "Review", "Done"), notify: true), "Nova was stopped by a gate moving #GRO-5 T to Done · Needs a human", "Asked 12:00 · only you can move it anyway"),
            (Item(FeedSourceType.RoomMention, new RoomWaitPayload("r1", "Backend", null), notify: true), "Nova mentioned you in Backend", "Asked 12:00 · waiting for you"),
            (Item(FeedSourceType.RoomQuestion, new RoomWaitPayload("r1", "Backend", null), notify: true), "Nova asked you a Question in Backend", "Asked 12:00 · waiting for you"),
            (Item(FeedSourceType.RoomForm, new RoomWaitPayload("r1", "Backend", null), notify: true), "Nova is waiting for you to fill in a Form in Backend", "Asked 12:00 · waiting for you"),
            (Item(FeedSourceType.RoomBudget, new RoomWaitPayload("r1", "Backend", null), notify: true, actor: huddle), "Huddle Backend reached its Budget", "Paused 12:00 · waiting for you to continue or wrap up"),
            (Item(FeedSourceType.RoomCreated, new RoomCreatedPayload("r1", "Backend", null, ["Nova", "Kai", "Vera"])), "Nova opened Backend · 2 Teammates", null),
            (Item(FeedSourceType.RoomJoined, new RoomJoinedPayload("r1", "Backend", null, "Kai")), "Kai joined Backend", null),
            (Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null)), "Nova archived Backend", null),
            (Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload(Path.Combine(root, "plan.md"), "Notes", "plan.md", "edited", "Reworded the intro")), "Nova edited plan.md · Reworded the intro", null),
            (Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-1", "T", "Growth", null, "Sends a document outside the Team"), notify: true, actor: new FeedActor(FeedActorKind.Helper, "Needs Human (Helper)")), "Needs Human (Helper) turned on Requires your acceptance for #GRO-1 T · Sends a document outside the Team", "Asked 12:00 · you can turn it off on the Task"),
            (Item(FeedSourceType.HelperPaused, new HelperPausedPayload("NeedsHuman", "Needs Human", 50, 0), notify: true, actor: huddle), "Huddle paused Needs Human: its daily cap of 50 calls is reached · Teammates' Tasks are not judged until you continue or the day ends", "Paused 12:00 · Continue is on the Helper's card"),
            (Item(FeedSourceType.HelperFailing, new HelperFailingPayload("NeedsHuman", "Needs Human", 3, "The adapter isn't signed in"), notify: true, actor: huddle), "Huddle Needs Human failed 3 times in a row · The adapter isn't signed in", "Since 12:00 · Tasks are not being judged"),
        ];

        foreach ((FeedItem item, string summary, string? status) in rows)
        {
            await using MudBunitContext ctx = FeedTestData.NewContext(library);
            IRenderedComponent<FeedItemRow> cut = this.Render(ctx, [item]);

            Assert.Equal(summary, Words(cut.Find(".feed-row-summary").TextContent));
            if (status is null)
            {
                Assert.Empty(cut.FindAll(".feed-row-status"));
            }
            else
            {
                Assert.Equal(status, Words(cut.Find(".feed-row-status").TextContent));
            }
        }
    }

    /// <summary>An item this build cannot read says only "Something happened", with no actor and no status line.</summary>
    [Fact]
    public async Task Row_UnreadableItem_SaysSomethingHappened()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);

        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, [Item(null, null)]);

        Assert.Equal("Something happened", Words(cut.Find(".feed-row-summary").TextContent));
        Assert.Empty(cut.FindAll(".feed-row-status"));
    }

    /// <summary>The Task chip opens the Task, the Room chip the Room and the Document chip the Library address of the file.</summary>
    [Fact]
    public async Task Chips_LinkToTheirOwnTargets()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        string root = library.CreatePinnedRoot("Notes");
        LibraryPath note = library.Resolve(root, "plan.md");
        FakeTaskReferenceResolver tasks = new();
        tasks.Add(new TaskReference(new TaskId("GRO", 41), "Draft the pricing page", false, TaskState.InProgress));
        await using MudBunitContext ctx = FeedTestData.NewContext(library, tasks);

        IRenderedComponent<FeedItemRow> task = this.Render(ctx, [Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-41", "Draft the pricing page", "Growth", null))]);
        IRenderedComponent<FeedItemRow> room = this.Render(ctx, [Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null))]);
        IRenderedComponent<FeedItemRow> document = this.Render(ctx, [Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload(note.FullPath, "Notes", "plan.md", "edited", string.Empty))]);

        Assert.Equal("/tasks/item/GRO-41", task.Find(".feed-chip-task a").GetAttribute("href"));
        Assert.Equal("#GRO-41", task.Find(".feed-chip-task .feed-chip-id").TextContent);
        Assert.NotEmpty(task.FindAll(".feed-chip-task .task-state-dot"));
        Assert.Equal("/rooms/r1", room.Find(".feed-chip-room a").GetAttribute("href"));
        Assert.Equal(LibraryLinkQuery.BuildPageUri(note), document.Find(".feed-chip-document a").GetAttribute("href"));
    }

    /// <summary>The summary carries the three-line clamp class.</summary>
    [Fact]
    public async Task Summary_HasTheThreeLineClamp()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);

        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, [Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null))]);

        Assert.Contains("feed-clamp-3", cut.Find(".feed-row-summary").ClassList);
    }

    /// <summary>An unresolved Notification has the Accent dot and an Accent age; a resolved one is dimmed and shows the check.</summary>
    [Fact]
    public async Task Notification_ResolvedIsDimmedWithCheck_UnresolvedHasDot()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        FeedItem open = Item(FeedSourceType.RoomMention, new RoomWaitPayload("r1", "Backend", null), notify: true);
        FeedItem done = open with { ResolvedAt = Now.AddMinutes(-1) };

        IRenderedComponent<FeedItemRow> openRow = this.Render(ctx, [open]);
        IRenderedComponent<FeedItemRow> doneRow = this.Render(ctx, [done]);

        Assert.Single(openRow.FindAll(".feed-row-status .feed-dot"));
        Assert.Empty(openRow.FindAll(".feed-row-status .feed-check"));
        Assert.Contains("feed-row-unresolved", openRow.Find(".feed-row-time").ClassList);
        Assert.DoesNotContain("feed-row-resolved", openRow.Find(".feed-row").ClassList);
        Assert.Single(doneRow.FindAll(".feed-row-status .feed-check"));
        Assert.Empty(doneRow.FindAll(".feed-row-status .feed-dot"));
        Assert.Contains("feed-row-resolved", doneRow.Find(".feed-row").ClassList);
        Assert.DoesNotContain("feed-row-unresolved", doneRow.Find(".feed-row-time").ClassList);
    }

    /// <summary>The age is compact, and its tooltip holds the full date.</summary>
    [Fact]
    public async Task Time_ShowsTheCompactAgeWithTheFullDateInItsTooltip()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);

        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, [Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null), at: Now.AddMinutes(-19))]);

        Assert.Equal("19m", cut.Find(".feed-row-time").TextContent.Trim());
        Assert.Equal("Tue, Oct 20, 2026 11:41", cut.Find(".feed-row-time").GetAttribute("title"));
    }

    /// <summary>Neighbouring items draw as one line with "and N more Tasks", and the button expands the group into its rows.</summary>
    [Fact]
    public async Task Group_MoreButtonExpandsIntoMemberRows()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        FeedItem[] items =
        [
            Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-3", "C", "Growth", null), at: Now, id: 3),
            Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-2", "B", "Growth", null), at: Now.AddMinutes(-2), id: 2),
            Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-1", "A", "Growth", null), at: Now.AddMinutes(-4), id: 1),
        ];
        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, items);

        Assert.Single(cut.FindAll(".feed-row-summary"));
        Assert.Equal("and 2 more Tasks", cut.Find("button.feed-row-more").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find("button.feed-row-more").ClickAsync());

        Assert.Equal(3, cut.FindAll(".feed-row-summary").Count);
        Assert.Empty(cut.FindAll("button.feed-row-more"));
    }

    /// <summary>Room waits of one Team read as one sentence naming the Team and the count.</summary>
    [Fact]
    public async Task Group_RoomWaitsInOneTeam_ReadAsOneSentence()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        FeedItem[] items =
        [
            Item(FeedSourceType.RoomBudget, new RoomWaitPayload("r3", "C", "Growth"), notify: true, actor: FeedActor.Huddle, id: 3),
            Item(FeedSourceType.RoomBudget, new RoomWaitPayload("r2", "B", "Growth"), notify: true, actor: FeedActor.Huddle, at: Now.AddMinutes(-1), id: 2),
            Item(FeedSourceType.RoomBudget, new RoomWaitPayload("r1", "A", "Growth"), notify: true, actor: FeedActor.Huddle, at: Now.AddMinutes(-2), id: 1),
        ];

        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, items);

        Assert.Equal("In Growth, 3 Rooms stopped to wait for you", Words(cut.Find(".feed-row-summary").TextContent));
    }

    /// <summary>The dismiss x exists only on a Notification, only when a callback is given, and calls it with the item.</summary>
    [Fact]
    public async Task Dismiss_OnlyWhenCallbackSetAndNotification_CallsIt()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        FeedItem notification = Item(FeedSourceType.RoomMention, new RoomWaitPayload("r1", "Backend", null), notify: true, id: 7);
        FeedItem activity = Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null), id: 8);
        List<FeedItem> dismissed = [];

        IRenderedComponent<FeedItemRow> without = this.Render(ctx, [notification]);
        IRenderedComponent<FeedItemRow> activityRow = this.Render(ctx, [activity], onDismiss: dismissed.Add);
        IRenderedComponent<FeedItemRow> with = this.Render(ctx, [notification], onDismiss: dismissed.Add);
        await with.InvokeAsync(() => with.Find("button.feed-row-dismiss").ClickAsync());

        Assert.Empty(without.FindAll("button.feed-row-dismiss"));
        Assert.Empty(activityRow.FindAll("button.feed-row-dismiss"));
        Assert.Contains("hover-reveal-row", with.Find(".feed-row").ClassList);
        Assert.Equal([notification], dismissed);
    }

    /// <summary>An Agent's avatar is a button that reports the Teammate's Name; Huddle and the Human have no such button.</summary>
    [Fact]
    public async Task Avatar_ClickReportsTheTeammate_NotForHuddleOrHuman()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        List<string> clicked = [];
        FeedItem agent = Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null));
        FeedItem huddle = agent with { Actor = FeedActor.Huddle };
        FeedItem human = agent with { Actor = new FeedActor(FeedActorKind.Human, "Emre") };

        IRenderedComponent<FeedItemRow> agentRow = this.Render(ctx, [agent], onTeammate: clicked.Add);
        IRenderedComponent<FeedItemRow> huddleRow = this.Render(ctx, [huddle], onTeammate: clicked.Add);
        IRenderedComponent<FeedItemRow> humanRow = this.Render(ctx, [human], onTeammate: clicked.Add);
        await agentRow.InvokeAsync(() => agentRow.Find("button.feed-row-avatar-button").ClickAsync());

        Assert.Empty(huddleRow.FindAll("button.feed-row-avatar-button"));
        Assert.Empty(humanRow.FindAll("button.feed-row-avatar-button"));
        Assert.Equal(["Nova"], clicked);
        Assert.Equal("You", Words(humanRow.Find(".feed-row-summary .feed-row-actor").TextContent));
    }

    /// <summary>Clicking the row outside a chip opens the item's target.</summary>
    [Fact]
    public async Task RowClick_OpensTheItemsTarget()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, [Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r1", "Backend", null))]);

        await cut.InvokeAsync(() => cut.Find(".feed-row-time").ClickAsync());

        Assert.Equal("http://localhost/rooms/r1", ctx.Services.GetRequiredService<NavigationManager>().Uri);
    }

    /// <summary>A Helper's row wears the Helper mark instead of a Teammate's avatar, its chip links to the Helper's card, and the row opens it.</summary>
    [Fact]
    public async Task Row_HelperItem_DrawsTheMarkAndOpensTheHelperCard()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        FeedItem failing = Item(FeedSourceType.HelperFailing, new HelperFailingPayload("NeedsHuman", "Needs Human", 3, "x"), notify: true, actor: new FeedActor(FeedActorKind.Helper, "Needs Human (Helper)"));

        IRenderedComponent<FeedItemRow> cut = this.Render(ctx, [failing]);

        Assert.Empty(cut.FindAll(".mud-avatar"));
        Assert.NotNull(cut.Find(".feed-row-mark .mud-icon-root"));
        Assert.Equal("/settings/helpers#helper-NeedsHuman", cut.Find(".feed-chip-helper a").GetAttribute("href"));

        cut.Find(".feed-row").Click();

        Assert.Equal("settings/helpers#helper-NeedsHuman", navigation.ToBaseRelativePath(navigation.Uri));
    }
}
