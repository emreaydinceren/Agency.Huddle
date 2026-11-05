using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="FeedCard"/>: the collapsed card, its expansion into the previews, the related items and the navigation buttons (Feed spec §6.13).</summary>
public sealed class FeedCardTests
{
    private static readonly DateTimeOffset Now = FeedTestData.Now;

    private static string Where(NavigationManager navigation) => navigation.ToBaseRelativePath(navigation.Uri);

    private static string Words(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static IRenderedComponent<FeedCard> Render(MudBunitContext ctx, FeedItem item, long seen = 0, bool expanded = false, Action? onToggle = null) =>
        ctx.Render<FeedCard>(parameters =>
        {
            parameters.Add(card => card.Group, new FeedGroup([item]));
            parameters.Add(card => card.TabSeenUpTo, seen);
            parameters.Add(card => card.Expanded, expanded);
            if (onToggle is not null)
            {
                parameters.Add(card => card.OnToggle, EventCallback.Factory.Create(ctx, onToggle));
            }
        });

    private static FeedItem NeedsYouItem(string id = "GRO-1", int minutesAgo = 20, bool resolved = false, int proofMet = 1, long itemId = 1) =>
        FeedTestData.Item(
            FeedSourceType.TaskNeedsYou,
            new TaskNeedsYouPayload(id, "Draft the pricing page", "Growth", proofMet, 2),
            notify: true,
            at: Now.AddMinutes(-minutesAgo),
            resolved: resolved ? Now.AddMinutes(-5) : null,
            id: itemId) with { TargetKey = id };

    /// <summary>The 3px bar shows while the item is above the Tab's seen mark, and the age turns Accent with it.</summary>
    [Fact]
    public async Task Card_UnseenBarFollowsTabSeenUpTo()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = NeedsYouItem(itemId: 5);

        IRenderedComponent<FeedCard> unseen = Render(ctx, item, seen: 4);
        IRenderedComponent<FeedCard> seen = Render(ctx, item, seen: 5);

        Assert.NotNull(unseen.Find(".feed-card-bar"));
        Assert.NotNull(unseen.Find(".feed-card-age-unseen"));
        Assert.Empty(seen.FindAll(".feed-card-bar"));
        Assert.Empty(seen.FindAll(".feed-card-age-unseen"));
    }

    /// <summary>Each kind of item wears its own badge on the avatar.</summary>
    [Theory]
    [InlineData(FeedSourceType.TaskNeedsYou, "check")]
    [InlineData(FeedSourceType.DocumentChanged, "document")]
    [InlineData(FeedSourceType.TaskAssignedToYou, "person")]
    [InlineData(FeedSourceType.RoomBudget, "gauge")]
    [InlineData(FeedSourceType.TaskGateOn, "lock")]
    [InlineData(FeedSourceType.GateDecisionDue, "lock")]
    [InlineData(FeedSourceType.RoomMention, "mention")]
    [InlineData(FeedSourceType.RoomQuestion, "question")]
    [InlineData(FeedSourceType.RoomForm, "form")]
    [InlineData(FeedSourceType.TaskCreated, "plus")]
    [InlineData(FeedSourceType.RoomCreated, "plus")]
    [InlineData(FeedSourceType.RoomJoined, "joined")]
    [InlineData(FeedSourceType.RoomArchived, "archived")]
    [InlineData(FeedSourceType.RequestAwaitsYou, "request")]
    [InlineData(FeedSourceType.HelperPaused, "gauge")]
    [InlineData(FeedSourceType.HelperFailing, "alert")]
    public async Task Card_KindBadge_NamesTheKind(FeedSourceType type, string badge)
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(type, PayloadFor(type));

        IRenderedComponent<FeedCard> card = Render(ctx, item);

        Assert.Equal(badge, card.Find(".feed-card-badge").GetAttribute("data-badge"));
    }

    private static FeedPayload PayloadFor(FeedSourceType type) => type switch
    {
        FeedSourceType.TaskCreated => new TaskCreatedPayload("GRO-1", "T", "Growth", null),
        FeedSourceType.TaskGateOn => new TaskGatePayload("GRO-1", "T", "Growth", null),
        FeedSourceType.TaskNeedsYou => new TaskNeedsYouPayload("GRO-1", "T", "Growth", 1, 2),
        FeedSourceType.TaskAssignedToYou => new TaskAssignedPayload("GRO-1", "T", "Growth", null),
        FeedSourceType.RequestAwaitsYou => new RequestAwaitsPayload("GRO-1", "T", "Growth", "Support", "request"),
        FeedSourceType.GateDecisionDue => new GateDecisionPayload("GRO-1", "T", "Growth", "g", "m", "Review", "Done"),
        FeedSourceType.RoomCreated => new RoomCreatedPayload("room-1", "Backend", "Growth", ["Nova", "You"]),
        FeedSourceType.RoomJoined => new RoomJoinedPayload("room-1", "Backend", "Growth", "Kai"),
        FeedSourceType.RoomArchived => new RoomArchivedPayload("room-1", "Backend", "Growth"),
        FeedSourceType.DocumentChanged => new DocumentChangedPayload("C:/none/plan.md", "Notes", "plan.md", "edited", "Tightened"),
        FeedSourceType.HelperPaused => new HelperPausedPayload("NeedsHuman", "Needs Human", 50, 0),
        FeedSourceType.HelperFailing => new HelperFailingPayload("NeedsHuman", "Needs Human", 3, "The adapter isn't signed in"),
        _ => new RoomWaitPayload("room-1", "Backend", "Growth"),
    };

    /// <summary>A Task item reads "Tasks", then the Team chip with its two-letter monogram.</summary>
    [Fact]
    public async Task Card_MetaLine_ShowsContextWordAndTeamChip()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem());

        Assert.Equal("Tasks • GR Growth", Words(card.Find(".feed-card-meta").TextContent));
        Assert.Equal("GR", card.Find(".feed-team-chip .mud-avatar.avatar-square").TextContent.Trim());
    }

    /// <summary>The Team chip draws the Team's own avatar from the catalog: its label, on a rounded square, in place of the monogram.</summary>
    [Fact]
    public async Task Card_TeamChip_ShowsTheTeamsOwnAvatar()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        host.Teams.Teams = [new TeamSummary("Growth", [], [], HasFolder: true) { Avatar = new Avatar("UP", null, null) }];
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem());

        Assert.Equal("UP", card.Find(".feed-team-chip .mud-avatar.avatar-square").TextContent.Trim());
    }

    /// <summary>A Room item carries the Room chip in its meta line, and a document item reads "Library" with no Team chip.</summary>
    [Fact]
    public async Task Card_MetaLine_RoomChipAndLibraryWord()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> room = Render(ctx, FeedTestData.Item(FeedSourceType.RoomMention, new RoomWaitPayload("room-1", "Backend", "Support Desk"), notify: true));
        IRenderedComponent<FeedCard> document = Render(ctx, FeedTestData.Item(FeedSourceType.DocumentChanged, PayloadFor(FeedSourceType.DocumentChanged)));

        Assert.Equal("Rooms • Backend • SD Support Desk", Words(room.Find(".feed-card-meta").TextContent));
        Assert.Equal("Library", Words(document.Find(".feed-card-meta").TextContent));
        Assert.Empty(document.FindAll(".feed-team-chip"));
    }

    /// <summary>An unresolved Notification reads "Waiting for you" with its age in minutes; a Budget stop adds the pause glyph.</summary>
    [Fact]
    public async Task Card_StatusPill_WaitingForYou()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem budget = FeedTestData.Item(FeedSourceType.RoomBudget, new RoomWaitPayload("room-1", "Backend", null), notify: true, at: Now.AddMinutes(-7));

        IRenderedComponent<FeedCard> waiting = Render(ctx, NeedsYouItem(minutesAgo: 20));
        IRenderedComponent<FeedCard> paused = Render(ctx, budget);

        Assert.Equal("Waiting for you · 20 min", Words(waiting.Find(".feed-pill-waiting").TextContent));
        Assert.Empty(waiting.FindAll(".feed-pill-pause"));
        Assert.Equal("Waiting for you · 7 min", Words(paused.Find(".feed-pill-waiting").TextContent));
        Assert.NotNull(paused.Find(".feed-pill-pause"));
    }

    /// <summary>The waiting minutes are redrawn when the minute timer fires.</summary>
    [Fact]
    public async Task Card_StatusPill_AgeMovesWithTheClock()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem(minutesAgo: 20));

        host.Clock.Advance(TimeSpan.FromMinutes(3));
        await card.InvokeAsync(card.Instance.RefreshAgeAsync);

        Assert.Equal("Waiting for you · 23 min", Words(card.Find(".feed-pill-waiting").TextContent));
        Assert.Equal("23 min", card.Find(".feed-card-age").TextContent.Trim());
    }

    /// <summary>A Task assigned to the Human reads "Assigned to you" with its due date when the Task is found.</summary>
    [Fact]
    public async Task Card_StatusPill_AssignedToYou_WithAndWithoutDueDate()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-7", due: new DateOnly(2026, 10, 27));
        await using MudBunitContext ctx = host.NewContext();
        FeedItem known = FeedTestData.Item(FeedSourceType.TaskAssignedToYou, new TaskAssignedPayload("GRO-7", "T", "Growth", null), notify: true);
        FeedItem unknown = FeedTestData.Item(FeedSourceType.TaskAssignedToYou, new TaskAssignedPayload("GRO-99", "T", "Growth", null), notify: true);

        IRenderedComponent<FeedCard> withDue = Render(ctx, known);
        IRenderedComponent<FeedCard> without = Render(ctx, unknown);

        Assert.Equal("Assigned to you · due Tue 27 Oct", Words(withDue.Find(".feed-pill-assigned").TextContent));
        Assert.Equal("Assigned to you", Words(without.Find(".feed-pill-assigned").TextContent));
    }

    /// <summary>A resolved Notification reads "Done" with the time it ended in the Human's zone.</summary>
    [Fact]
    public async Task Card_StatusPill_DoneWithTime()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem(resolved: true));

        Assert.Equal("Done · 11:55", Words(card.Find(".feed-pill-done").TextContent));
        Assert.Empty(card.FindAll(".feed-pill-waiting"));
    }

    /// <summary>An Activity item has no status pill.</summary>
    [Fact]
    public async Task Card_ActivityItem_HasNoPill()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, FeedTestData.Item(FeedSourceType.TaskCreated, PayloadFor(FeedSourceType.TaskCreated)));

        Assert.Empty(card.FindAll(".feed-pill"));
    }

    /// <summary>The chevron has aria-expanded and the label "Show details", and asks the parent to toggle; an expanded card draws its details.</summary>
    [Fact]
    public async Task Card_Chevron_AsksToToggle()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        int toggled = 0;
        IRenderedComponent<FeedCard> collapsed = Render(ctx, NeedsYouItem(), onToggle: () => toggled++);

        IElement chevron = collapsed.Find("button[aria-label='Show details']");
        chevron.Click();

        Assert.Equal("false", chevron.GetAttribute("aria-expanded"));
        Assert.Equal(1, toggled);
        Assert.Empty(collapsed.FindAll(".feed-card-details"));
        IRenderedComponent<FeedCard> open = Render(ctx, NeedsYouItem(), expanded: true);
        Assert.Equal("true", open.Find("button[aria-label='Show details']").GetAttribute("aria-expanded"));
        Assert.NotNull(open.Find(".feed-card-details"));
    }

    /// <summary>In a Cards list one card is expanded at a time and Escape collapses it.</summary>
    [Fact]
    public async Task List_Cards_OneExpandedAtATime_EscapeCollapses()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem other = FeedTestData.Item(FeedSourceType.TaskCreated, PayloadFor(FeedSourceType.TaskCreated), id: 1);
        IReadOnlyList<FeedItem> items = [NeedsYouItem("GRO-1", itemId: 2), other];
        IRenderedComponent<FeedList> list = ctx.Render<FeedList>(parameters =>
        {
            parameters.Add(l => l.Items, items);
            parameters.Add(l => l.Mode, FeedListMode.Cards);
            parameters.Add(l => l.TabSeenUpTo, 1L);
        });

        list.FindAll("button[aria-label='Show details']")[0].Click();
        Assert.Single(list.FindAll(".feed-card-details"));
        list.FindAll("button[aria-label='Show details']")[1].Click();
        Assert.Single(list.FindAll(".feed-card-details"));
        Assert.Equal("true", list.FindAll("button[aria-label='Show details']")[1].GetAttribute("aria-expanded"));
        list.Find(".feed-list").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(list.FindAll(".feed-card-details"));
        Assert.Equal(2, list.FindAll(".feed-card").Count);
        Assert.Single(list.FindAll(".feed-card-bar"));
    }

    /// <summary>The default list mode still draws rows, not cards.</summary>
    [Fact]
    public async Task List_DefaultMode_StillDrawsRows()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedList> list = ctx.Render<FeedList>(parameters => parameters.Add(l => l.Items, [NeedsYouItem()]));

        Assert.Single(list.FindAll(".feed-row"));
        Assert.Empty(list.FindAll(".feed-card"));
    }

    /// <summary>The Task preview shows the id, title, property grid, Proof list and the gate sentence, read from the store.</summary>
    [Fact]
    public async Task Preview_Task_ShowsPropertiesProofAndGateSentence()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1", status: TaskState.NeedsYou, gated: true, due: new DateOnly(2026, 10, 13), originRoomId: "room-1");
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem(), expanded: true);

        Assert.Equal("Task · Growth", Words(card.Find(".feed-card-caption").TextContent));
        Assert.Equal("GRO-0001", card.Find(".feed-preview-id").TextContent.Trim());
        Assert.Equal("Draft the pricing page", card.Find(".feed-preview-title").TextContent.Trim());
        Assert.Equal("Nova", Words(card.Find(".feed-preview-assignee").TextContent));
        Assert.Equal("Ada", Words(card.Find(".feed-preview-assigned-by").TextContent));
        Assert.NotNull(card.Find(".feed-preview .task-status-chip"));
        Assert.NotNull(card.Find(".feed-due-overdue"));
        Assert.Equal("Proof 1/2", Words(card.Find(".feed-proof-chip").TextContent));
        Assert.Equal(3, card.FindAll(".feed-proof-segment").Count);
        Assert.Equal(["Draft written", "Citation check"], card.FindAll(".feed-proof-label").Select(label => label.TextContent.Trim()));
        Assert.Equal("pending", Words(card.Find(".feed-proof-open").TextContent));
        Assert.Equal("Nova · done in the Room", Words(card.Find(".feed-proof-met").TextContent));
        Assert.Equal("1 of 2 Proof items are in. Accepting marks it Done; nothing is sent or published.", Words(card.Find(".feed-preview-footer").TextContent));
        Assert.NotNull(card.Find(".feed-preview-footer .mud-icon-root"));
    }

    /// <summary>A Task that is gone shows the deleted line and keeps the Feed part.</summary>
    [Fact]
    public async Task Preview_Task_Deleted()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem("GRO-404"), expanded: true);

        Assert.Equal("This Task was deleted", Words(card.Find(".feed-preview-deleted").TextContent));
        Assert.Empty(card.FindAll(".feed-preview-title"));
        Assert.NotNull(card.Find(".feed-related"));
    }

    /// <summary>The Room preview shows its Members, why it waits and the time of its last Message, never the Message text.</summary>
    [Fact]
    public async Task Preview_Room_MembersReasonAndLastMessageTime()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using FeedCardHost host = await FeedCardHost.CreateAsync(ct);
        Agency.Huddle.App.Data.Room room = await NeedsYouTaskTestHost.WaitingRoomAsync(host.NeedsYou, ct);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(FeedSourceType.RoomMention, new RoomWaitPayload(room.Id, room.Name, "Growth"), notify: true);

        IRenderedComponent<FeedCard> card = Render(ctx, item, expanded: true);
        card.WaitForAssertion(() => Assert.NotEmpty(card.FindAll(".feed-room-member")));

        Assert.Equal("Room · Growth", Words(card.Find(".feed-card-caption").TextContent));
        Assert.Equal(["Nova", "You"], card.FindAll(".feed-room-member-name").Select(member => Words(member.TextContent)).Order(StringComparer.Ordinal));
        Assert.Equal("Waiting on you: mention", Words(card.Find(".feed-room-reason").TextContent));
        Assert.Equal("Last message Oct 12 09:00", Words(card.Find(".feed-room-last").TextContent));
        Assert.DoesNotContain("which database", card.Markup, StringComparison.Ordinal);
    }

    /// <summary>A Room that is gone shows the deleted line.</summary>
    [Fact]
    public async Task Preview_Room_Deleted()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(FeedSourceType.RoomMention, new RoomWaitPayload("gone", "Gone", null), notify: true);

        IRenderedComponent<FeedCard> card = Render(ctx, item, expanded: true);
        card.WaitForAssertion(() => Assert.Equal("This Room was deleted", Words(card.Find(".feed-preview-deleted").TextContent)));

        Assert.Equal("Room", Words(card.Find(".feed-card-caption").TextContent));
    }

    /// <summary>The document preview shows the root, the path and the last three History entries, newest first.</summary>
    [Fact]
    public async Task Preview_Document_RootPathAndLastThreeEntries()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string root = host.Library.CreatePinnedRoot("Notes");
        LibraryPath path = host.Library.Resolve(root, "plan.md");
        await File.WriteAllTextAsync(path.FullPath, "# Plan", Xunit.TestContext.Current.CancellationToken);
        for (int i = 1; i <= 4; i++)
        {
            host.History.Append(path, new DocHistoryEntry(Now.AddMinutes(-60 + i), "Nova", DocActorKind.Agent, DocChangeKind.Edited, "Change " + i));
        }

        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload(path.FullPath, "Notes", "plan.md", "edited", "Change 4"));

        IRenderedComponent<FeedCard> card = Render(ctx, item, expanded: true);

        Assert.Equal("Document · Library", Words(card.Find(".feed-card-caption").TextContent));
        Assert.Equal("Notes plan.md", Words(card.Find(".feed-doc-where").TextContent));
        Assert.Equal(["Change 4", "Change 3", "Change 2"], card.FindAll(".feed-doc-summary").Select(summary => summary.TextContent.Trim()));
    }

    /// <summary>A document that is gone shows the deleted line.</summary>
    [Fact]
    public async Task Preview_Document_Deleted()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string root = host.Library.CreatePinnedRoot("Notes");
        LibraryPath path = host.Library.Resolve(root, "gone.md");
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload(path.FullPath, "Notes", "gone.md", "edited", "x"));

        IRenderedComponent<FeedCard> card = Render(ctx, item, expanded: true);

        Assert.Equal("This document was deleted", Words(card.Find(".feed-preview-deleted").TextContent));
    }

    /// <summary>"In the Feed" lists items about the target, the origin Room and the linked files, and tags the card's own item.</summary>
    [Fact]
    public async Task Related_ListsTargetOriginRoomAndAttachments_TagsThisItem()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string root = host.Library.CreatePinnedRoot("Notes");
        LibraryPath doc = host.Library.Resolve(root, "brief.md");
        _ = host.SeedTask("GRO-1", originRoomId: "room-1", description: TaskAttachmentText.AppendPathLine("Body", doc.FullPath));
        _ = host.Post(FeedSourceType.TaskCreated, "GRO-1", new TaskCreatedPayload("GRO-1", "Draft the pricing page", "Growth", null), minutesAgo: 30);
        _ = host.Post(FeedSourceType.RoomMention, "room-1", new RoomWaitPayload("room-1", "Backend", "Growth"), notify: true, minutesAgo: 25);
        _ = host.Post(FeedSourceType.DocumentChanged, doc.FullPath, new DocumentChangedPayload(doc.FullPath, "Notes", "brief.md", "edited", "x"), minutesAgo: 22);
        _ = host.Post(FeedSourceType.TaskCreated, "OTHER-1", new TaskCreatedPayload("OTHER-1", "Elsewhere", "Growth", null), minutesAgo: 21);
        FeedItem own = host.Post(FeedSourceType.TaskNeedsYou, "GRO-1", new TaskNeedsYouPayload("GRO-1", "Draft the pricing page", "Growth", 1, 2), notify: true, minutesAgo: 20);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, own, expanded: true);

        Assert.Equal("In the Feed", card.Find(".feed-related-caption").TextContent.Trim());
        Assert.Equal(4, card.FindAll(".feed-related .feed-row").Count);
        Assert.Single(card.FindAll(".feed-related-own"));
        Assert.Equal("This item", card.Find(".feed-related-own .feed-related-tag").TextContent.Trim());
    }

    /// <summary>Review in Needs you shows only for an unresolved Notification about a gated Task that waits in Needs You, and opens its anchor.</summary>
    [Fact]
    public async Task Buttons_ReviewInNeedsYou_OnlyForUnresolvedGatedTaskInNeedsYou()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1", status: TaskState.NeedsYou, gated: true);
        _ = host.SeedTask("GRO-2", status: TaskState.InProgress, gated: true);
        _ = host.SeedTask("GRO-3", status: TaskState.NeedsYou, gated: false);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> waiting = Render(ctx, NeedsYouItem("GRO-1"), expanded: true);
        IRenderedComponent<FeedCard> resolved = Render(ctx, NeedsYouItem("GRO-1", resolved: true), expanded: true);
        IRenderedComponent<FeedCard> notInNeedsYou = Render(ctx, NeedsYouItem("GRO-2"), expanded: true);
        IRenderedComponent<FeedCard> ungated = Render(ctx, NeedsYouItem("GRO-3"), expanded: true);
        IRenderedComponent<FeedCard> activity = Render(ctx, FeedTestData.Item(FeedSourceType.TaskCreated, new TaskCreatedPayload("GRO-1", "T", "Growth", null)), expanded: true);

        waiting.Find(".feed-btn-review").Click();

        Assert.Equal("needs-you#GRO-1", Where(ctx.Services.GetRequiredService<NavigationManager>()));
        Assert.Equal("Review in Needs you", Words(waiting.Find(".feed-btn-review").TextContent));
        Assert.Empty(resolved.FindAll(".feed-btn-review"));
        Assert.Empty(notInNeedsYou.FindAll(".feed-btn-review"));
        Assert.Empty(ungated.FindAll(".feed-btn-review"));
        Assert.Empty(activity.FindAll(".feed-btn-review"));
    }

    private static FeedItem WakesPausedItem(string waiting = "Nova", bool resolved = false) =>
        FeedTestData.Item(
            FeedSourceType.TaskWakesPaused,
            new TaskWakesPausedPayload("GRO-1", "Draft the pricing page", "Growth", waiting),
            notify: true,
            at: Now.AddMinutes(-20),
            resolved: resolved ? Now.AddMinutes(-5) : null) with { TargetKey = "GRO-1" };

    /// <summary>#353: Wake {Agent} shows only on an open pause Notification whose hand-off is no longer held, and a press wakes that Agent once.</summary>
    [Fact]
    public async Task Buttons_WakeAgent_OnlyWhenNothingIsHeld_AndWakesOnce()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1");
        await using MudBunitContext ctx = host.NewContext();
        FakeWakePauses pauses = new();
        ctx.Services.AddSingleton<IWakePauses>(pauses);

        pauses.Held = true;
        IRenderedComponent<FeedCard> held = Render(ctx, WakesPausedItem(), expanded: true);
        pauses.Held = false;
        IRenderedComponent<FeedCard> lost = Render(ctx, WakesPausedItem(), expanded: true);
        IRenderedComponent<FeedCard> closed = Render(ctx, WakesPausedItem(resolved: true), expanded: true);
        lost.Find(".feed-btn-wake").Click();

        Assert.Empty(held.FindAll(".feed-btn-wake"));
        Assert.Empty(closed.FindAll(".feed-btn-wake"));
        Assert.Equal("Wake Nova", Words(lost.Find(".feed-btn-wake").TextContent));
        Assert.Equal([("GRO-0001", "Nova")], pauses.Woken);
    }

    /// <summary>#353: when the Task now belongs to someone else, the button names the current assignee and the card says so.</summary>
    [Fact]
    public async Task Buttons_WakeAgent_TaskReassigned_NamesTheCurrentAssignee()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1", assignee: "Kai");
        await using MudBunitContext ctx = host.NewContext();
        ctx.Services.AddSingleton<IWakePauses>(new FakeWakePauses());

        IRenderedComponent<FeedCard> card = Render(ctx, WakesPausedItem(), expanded: true);

        Assert.Equal("Wake Kai", Words(card.Find(".feed-btn-wake").TextContent));
        Assert.Equal("Nova no longer has this Task. It is now assigned to Kai, so Wake starts Kai.", Words(card.Find(".feed-wake-note").TextContent));
    }

    /// <summary>Stands in for the wake service: whether a hand-off is held, and the Wakes it was asked for.</summary>
    private sealed class FakeWakePauses : IWakePauses
    {
        public bool Held { get; set; }

        public List<(string Task, string Waiting)> Woken { get; } = [];

        public bool AgentExists(string name) => true;

        public void RestorePaused(TaskId id)
        {
        }

        public bool IsHeld(TaskId id) => this.Held;

        public Task<string?> WakeAsync(TaskId id, string waiting, CancellationToken ct)
        {
            this.Woken.Add((id.ToString(), waiting));
            return Task.FromResult<string?>(waiting);
        }
    }

    /// <summary>"Open Task" and the origin Room's "Open Room" open their pages.</summary>
    [Fact]
    public async Task Buttons_Task_OpenTaskAndOriginRoom()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1", originRoomId: "room-1");
        await using MudBunitContext ctx = host.NewContext();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem(), expanded: true);

        Assert.Equal("Open Task", Words(card.Find(".feed-btn-open").TextContent));
        card.Find(".feed-btn-open").Click();
        Assert.Equal("tasks/item/GRO-1", Where(navigation));

        Assert.Equal("Open Room", Words(card.Find(".feed-btn-room").TextContent));
        card.Find(".feed-btn-room").Click();
        Assert.Equal("rooms/room-1", Where(navigation));
    }

    /// <summary>A Task without an origin Room has no "Open Room" button.</summary>
    [Fact]
    public async Task Buttons_Task_NoOriginRoom_NoRoomButton()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1");
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem(), expanded: true);

        Assert.Empty(card.FindAll(".feed-btn-room"));
    }

    /// <summary>A Room card opens its Room and a document card opens its document.</summary>
    [Fact]
    public async Task Buttons_RoomAndDocument_OpenTheirTargets()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        string root = host.Library.CreatePinnedRoot("Notes");
        LibraryPath path = host.Library.Resolve(root, "plan.md");
        await using MudBunitContext ctx = host.NewContext();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<FeedCard> room = Render(ctx, FeedTestData.Item(FeedSourceType.RoomMention, new RoomWaitPayload("room-9", "Backend", null), notify: true), expanded: true);
        IRenderedComponent<FeedCard> document = Render(ctx, FeedTestData.Item(FeedSourceType.DocumentChanged, new DocumentChangedPayload(path.FullPath, "Notes", "plan.md", "edited", "x")), expanded: true);

        Assert.Equal("Open Room", Words(room.Find(".feed-btn-open").TextContent));
        room.Find(".feed-btn-open").Click();
        Assert.Equal("rooms/room-9", Where(navigation));

        Assert.Equal("Open document", Words(document.Find(".feed-btn-open").TextContent));
        document.Find(".feed-btn-open").Click();
        Assert.Equal(LibraryLinkQuery.BuildPageUri(path).TrimStart('/'), Where(navigation));
    }

    /// <summary>Clicking the card's empty space expands it, as the chevron does, and opens nothing; the Open button and the chips open the target.</summary>
    [Fact]
    public async Task Card_BodyClick_Expands_AndOpensNothing()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        int toggled = 0;
        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYouItem(), onToggle: () => toggled++);

        card.Find("button[aria-label='Show details']").Click();
        Assert.Equal(1, toggled);
        card.Find(".feed-card-main").Click();

        Assert.Equal(2, toggled);
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>Expanding and clicking through a card writes nothing: no queued command, no new Feed row and the Task file is unchanged.</summary>
    [Fact]
    public async Task Card_Expanding_WritesNothing()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = host.SeedTask("GRO-1", status: TaskState.NeedsYou, gated: true, originRoomId: "room-1");
        FeedItem own = host.Post(FeedSourceType.TaskNeedsYou, "GRO-1", new TaskNeedsYouPayload("GRO-1", "Draft the pricing page", "Growth", 1, 2), notify: true);
        string taskText = await File.ReadAllTextAsync(task.Path, Xunit.TestContext.Current.CancellationToken);
        int rows = host.Feed.Store.Page(new FeedQuery(null, 50, null, null)).Count;
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, own, expanded: true);
        card.Find(".feed-btn-open").Click();
        card.Find(".feed-btn-review").Click();
        card.Find(".feed-btn-room").Click();

        Assert.False(host.Feed.Queue.Reader.TryRead(out _));
        Assert.Equal(rows, host.Feed.Store.Page(new FeedQuery(null, 50, null, null)).Count);
        Assert.Equal(taskText, await File.ReadAllTextAsync(task.Path, Xunit.TestContext.Current.CancellationToken));
    }

    private static FeedItem HelperItem(FeedSourceType type, FeedPayload payload) =>
        FeedTestData.Item(type, payload, notify: true, actor: FeedActor.Huddle) with { TargetKey = "helper:NeedsHuman" };

    /// <summary>A Helper's own gate-on item wears the Helper mark, not an Agent's avatar, and its name is the Task actor's.</summary>
    [Fact]
    public async Task Card_HelperActor_DrawsTheHelperMark()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(
            FeedSourceType.TaskGateOn,
            new TaskGatePayload("GRO-1", "Email the pricing sheet", "Growth", null, "Sends a document outside the Team"),
            notify: true,
            actor: new FeedActor(FeedActorKind.Helper, "Needs Human (Helper)"));

        IRenderedComponent<FeedCard> card = Render(ctx, item);

        Assert.Empty(card.FindAll(".mud-avatar:not(.avatar-square)")); // the Team chip's rounded square is the only avatar left
        Assert.Empty(card.FindAll(".feed-card-avatar-button"));
        Assert.NotNull(card.Find(".feed-card-mark .mud-icon-root"));
        Assert.Equal("Needs Human (Helper)", card.Find(".feed-card-actor").TextContent.Trim());
        Assert.Equal("Sends a document outside the Team", card.Find(".feed-card-purpose").TextContent.Trim());
    }

    /// <summary>A Helper item names the Helper in a chip that opens its card.</summary>
    [Fact]
    public async Task Card_HelperPaused_ChipOpensTheHelperCard()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        IRenderedComponent<FeedCard> card = Render(ctx, HelperItem(FeedSourceType.HelperPaused, PayloadFor(FeedSourceType.HelperPaused)));

        Assert.Equal("Huddle", card.Find(".feed-card-actor").TextContent.Trim());
        Assert.Equal("Needs Human", Words(card.Find(".feed-chip-helper").TextContent));
        Assert.Equal("Helpers", Words(card.Find(".feed-meta-context").TextContent));

        Assert.Equal("/settings/helpers#helper-NeedsHuman", card.Find(".feed-chip-helper a").GetAttribute("href"));
    }

    /// <summary>An expanded Helper item previews the Helper read-only: state, what it runs on, today's usage, the last result and the last error.</summary>
    [Fact]
    public async Task Preview_Helper_ShowsStateUsageLastResultAndError()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        HelperId id = host.Helper.Id;
        for (int i = 0; i < 50; i++)
        {
            _ = host.HelperBudget.TryReserve(id, 50);
        }

        _ = host.HelperBudget.TryReserve(id, 50);
        host.HelperStatuses.RecordSuccess(id, FeedTestData.Now.AddMinutes(-90), "Turned the gate on for GRO-41");
        _ = host.HelperStatuses.RecordFailure(id, FeedTestData.Now.AddMinutes(-10), "The adapter isn't signed in");
        await using MudBunitContext ctx = host.NewContext();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<FeedCard> card = Render(ctx, HelperItem(FeedSourceType.HelperPaused, PayloadFor(FeedSourceType.HelperPaused)), expanded: true);

        Assert.Equal("Helper · Settings", Words(card.Find(".feed-card-caption").TextContent));
        Assert.Equal("Scripted", Words(card.Find(".feed-helper-preview .feed-preview-title").TextContent));
        Assert.Equal("On", Words(card.Find(".feed-helper-state").TextContent));
        Assert.Equal("50 of 50 calls today · 1 skipped while paused", Words(card.Find(".feed-helper-usage").TextContent));
        Assert.Equal("10:30 · Turned the gate on for GRO-41", Words(card.Find(".feed-helper-last-run").TextContent));
        Assert.Equal("11:50 · The adapter isn't signed in", Words(card.Find(".feed-helper-last-error").TextContent));
        Assert.Equal("Open Helper settings", Words(card.Find(".feed-btn-open").TextContent));

        card.Find(".feed-btn-open").Click();

        Assert.Equal("settings/helpers#helper-NeedsHuman", Where(navigation));
    }

    /// <summary>A Helper that is not registered any more says so instead of previewing it.</summary>
    [Fact]
    public async Task Preview_Helper_Unregistered_SaysItIsGone()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = HelperItem(FeedSourceType.HelperFailing, new HelperFailingPayload("Retired", "Retired", 3, "x"));

        IRenderedComponent<FeedCard> card = Render(ctx, item, expanded: true);

        Assert.Equal("This Helper is no longer available", Words(card.Find(".feed-preview-deleted").TextContent));
    }

    /// <summary>A Task the Needs Human Helper gated shows the Helper's own line under the gate sentence; a Task a person gated does not.</summary>
    [Fact]
    public async Task Preview_Task_HelperGatedTask_ShowsTheHelpersLine()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.Tasks.SeedOnDisk(TestTasks.Make(
            id: "GRO-1",
            title: "Draft the pricing page",
            status: TaskState.InProgress,
            assignee: "Nova",
            location: new TaskLocation("Growth", null, false),
            changeLog: [TestTasks.Entry("2026-10-20T11:00:00Z", "Scripted (Helper)", "human gate on (reason: Sends a document outside the Team)")]) with { HumanGate = true });
        _ = host.SeedTask("GRO-2", gated: true);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> helperGated = Render(ctx, FeedTestData.Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-1", "Draft the pricing page", "Growth", null, "Sends a document outside the Team"), notify: true) with { TargetKey = "GRO-1" }, expanded: true);
        IRenderedComponent<FeedCard> personGated = Render(ctx, FeedTestData.Item(FeedSourceType.TaskGateOn, new TaskGatePayload("GRO-2", "Draft the pricing page", "Growth", null)) with { TargetKey = "GRO-2" }, expanded: true);

        Assert.Equal(
            "Turned on by the Scripted Helper: Sends a document outside the Team. If you turn it off, the Helper won't ask about this Task again.",
            Words(helperGated.Find(".feed-preview-helper-line").TextContent));
        Assert.Empty(personGated.FindAll(".feed-preview-helper-line"));
    }
}
