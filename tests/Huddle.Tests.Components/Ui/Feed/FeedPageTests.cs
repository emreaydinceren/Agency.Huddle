using System.Text.RegularExpressions;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="FeedPage"/>: the header, the filter bar, filters in the address, paging, live items, the Tab-seen mark and the empty states (Feed spec 6.9, FD-18).</summary>
public sealed partial class FeedPageTests
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static TeamSummary Team(string name) => new(name, [], [], true);

    private static string Text(IElement element) => Whitespace().Replace(element.TextContent, " ").Trim();

    /// <summary>A Team filter option's name: the text beside the Team's avatar, whose own monogram is not part of the name.</summary>
    private static string OptionName(IElement item) => item.QuerySelector(".feed-team-option-name") is { } name ? Text(name) : Text(item);

    private static MudBunitContext NewContext(FeedCardHost host, TimeProvider? clock = null)
    {
        MudBunitContext ctx = host.NewContext();
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog { Teams = [Team("Growth"), Team("Platform")] });
        if (clock is not null)
        {
            ctx.Services.AddSingleton(clock);
        }

        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx, string? category = null, string? agent = null, string? team = null, TabPageContext? page = null)
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<FeedPage>(0);
            builder.AddAttribute(1, nameof(FeedPage.Category), category);
            builder.AddAttribute(2, nameof(FeedPage.Agent), agent);
            builder.AddAttribute(3, nameof(FeedPage.Team), team);
            builder.CloseComponent();
        };
        if (page is not null)
        {
            RenderFragment inner = content;
            content = builder =>
            {
                builder.OpenComponent<CascadingValue<TabPageContext>>(0);
                builder.AddAttribute(1, "Value", page);
                builder.AddAttribute(2, "ChildContent", inner);
                builder.CloseComponent();
            };
        }

        return ctx.RenderWithPopovers(content);
    }

    private static FeedItem Post(FeedCardHost host, string actor, string team, int minutesAgo, string id) =>
        host.Feed.Store.Apply(
        [
            new PostCommand(
                new FeedMessage(FeedSourceType.TaskCreated, id, new FeedActor(FeedActorKind.Agent, actor), false, new TaskCreatedPayload(id, "Draft " + id, team, null)),
                FeedTestData.Now.AddMinutes(-minutesAgo)),
        ]).Added.Single();

    private static string[] Segments(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".mud-toggle-item").Select(Text)];

    private static long[] CardIds(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("[data-feed-id]").Select(card => long.Parse(card.GetAttribute("data-feed-id")!, System.Globalization.CultureInfo.InvariantCulture))];

    private static async Task Choose(IRenderedComponent<ContainerFragment> cut, string label)
    {
        IElement item = cut.FindAll(".mud-toggle-item").First(item => Text(item).StartsWith(label, StringComparison.Ordinal));
        await item.ClickAsync(new());
    }

    private static TabPageContext OpenFeedTab(MudBunitContext ctx, string url)
    {
        Tab tab = ctx.Services.GetRequiredService<TabService>().Open(url);
        return new TabPageContext(url, tab.Id);
    }

    /// <summary>The header has the title Feed, the subtitle, a Live chip and the read-only note.</summary>
    [Fact]
    public async Task Header_ShowsTitleSubtitleLiveChipAndReadOnlyNote()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("Feed", Text(cut.Find("h1")));
        Assert.Equal("What Agents and Huddle did, newest first. Each line links to the Task, Room or document it's about.", Text(cut.Find(".page-header-subtitle")));
        Assert.Equal("Live", Text(cut.Find(".feed-live-chip")));
        Assert.Equal("Read-only — the Feed never changes the work and never shows message text.", Text(cut.Find(".feed-readonly-text")));
    }

    /// <summary>The Category control has seven segments, each with its count under the current filters.</summary>
    [Fact]
    public async Task Categories_ShowSixSegmentsWithTheirCounts()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.Feed.PostNeedsYou();
        _ = host.Feed.PostTaskCreated(1);
        _ = host.Feed.PostTaskCreated(2);
        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal(["All 3", "Needs you 1", "Tasks 2", "Documents 0", "Rooms 0", "Gates 1", "Helpers 0", "Devices 0"], Segments(cut));
    }

    /// <summary>Choosing a Category navigates the Tab to it and keeps the Agent and Team in the address.</summary>
    [Fact]
    public async Task ChoosingACategory_NavigatesTheTab_KeepingAgentAndTeam()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = NewContext(host);
        TabPageContext page = OpenFeedTab(ctx, "/feed?agent=Nova&team=Growth");
        IRenderedComponent<ContainerFragment> cut = Render(ctx, agent: "Nova", team: "Growth", page: page);

        await Choose(cut, "Tasks");

        Assert.Equal("/feed?category=tasks&agent=Nova&team=Growth", ctx.Services.GetRequiredService<TabService>().Active!.Url);
        Assert.EndsWith("/feed?category=tasks&agent=Nova&team=Growth", ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri, StringComparison.Ordinal);
    }

    /// <summary>Choosing All removes the category parameter and leaves the others.</summary>
    [Fact]
    public async Task ChoosingAll_RemovesOnlyTheCategory()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = NewContext(host);
        TabPageContext page = OpenFeedTab(ctx, "/feed?category=tasks&agent=Nova");
        IRenderedComponent<ContainerFragment> cut = Render(ctx, category: "tasks", agent: "Nova", page: page);

        await Choose(cut, "All");

        Assert.Equal("/feed?agent=Nova", ctx.Services.GetRequiredService<TabService>().Active!.Url);
    }

    /// <summary>The Team select lists All teams and then each Team in catalog order, and choosing one navigates to <c>?team=</c>.</summary>
    [Fact]
    public async Task TeamSelect_ListsTeamsInOrder_AndNavigates()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, category: "rooms");
        Assert.NotNull(cut.Find("[aria-label='Team']"));

        await cut.InvokeAsync(() => cut.Find("div.mud-input-control").MouseDownAsync());

        string[] options = [.. cut.FindAll(".mud-list-item").Select(OptionName)];
        Assert.Equal(["All teams", "Growth", "Platform"], options);
        await cut.FindAll(".mud-list-item").Single(item => OptionName(item) == "Platform").ClickAsync(new());
        Assert.EndsWith("/feed?category=rooms&team=Platform", ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri, StringComparison.Ordinal);
    }

    /// <summary>The Agent chip is drawn only while an Agent is set, and its x removes only that parameter.</summary>
    [Fact]
    public async Task AgentChip_ShowsWhileSet_AndClearRemovesOnlyTheAgent()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext plain = NewContext(host);
        Assert.Empty(Render(plain).FindAll(".feed-agent-chip"));
        await using MudBunitContext ctx = NewContext(host);
        TabPageContext page = OpenFeedTab(ctx, "/feed?category=tasks&agent=Nova");
        IRenderedComponent<ContainerFragment> cut = Render(ctx, category: "tasks", agent: "Nova", page: page);

        Assert.Equal("Agent: Nova", Text(cut.Find(".feed-agent-chip .task-filter-chip-button")));
        await cut.Find(".feed-agent-chip .task-filter-chip-clear").ClickAsync(new());

        Assert.Equal("/feed?category=tasks", ctx.Services.GetRequiredService<TabService>().Active!.Url);
    }

    /// <summary>Clicking an Agent's avatar in a card sets the Agent in the address.</summary>
    [Fact]
    public async Task AvatarClick_NavigatesToTheAgent()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = Post(host, "Nova", "Growth", 1, "GRO-1");
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.Find("button.feed-card-avatar-button").ClickAsync(new());

        Assert.EndsWith("/feed?agent=Nova", ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri, StringComparison.Ordinal);
    }

    /// <summary>The Agent and Team parameters narrow the cards to that actor and Team, and an unknown category reads as All.</summary>
    [Fact]
    public async Task Filters_NarrowTheCards_AndAnUnknownCategoryReadsAsAll()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem first = Post(host, "Nova", "Growth", 4, "GRO-1");
        FeedItem second = Post(host, "Kai", "Platform", 3, "PLA-1");
        FeedItem third = Post(host, "Nova", "Platform", 2, "PLA-2");

        async Task<long[]> IdsAsync(string? category = null, string? agent = null, string? team = null)
        {
            await using MudBunitContext ctx = NewContext(host);
            return CardIds(Render(ctx, category, agent, team));
        }

        Assert.Equal([third.Id, second.Id, first.Id], await IdsAsync(category: "nonsense"));
        Assert.Equal([third.Id, first.Id], await IdsAsync(agent: "Nova"));
        Assert.Equal([third.Id, second.Id], await IdsAsync(team: "Platform"));
        Assert.Equal([third.Id], await IdsAsync(agent: "Nova", team: "Platform"));
        await using MudBunitContext last = NewContext(host);
        Assert.Equal("All 3", Segments(Render(last, category: "nonsense"))[0]);
    }

    /// <summary>"Show more" loads the next page below the lowest id shown; an item inserted meanwhile repeats nothing and loses nothing.</summary>
    [Fact]
    public async Task ShowMore_PagesByIdWithoutRepeatsOrGaps()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        host.Feed.Options.Value.Feed.PageSize = 2;
        FeedItem[] items = [.. Enumerable.Range(1, 5).Select(n => Post(host, "Nova", n % 2 == 0 ? "Growth" : "Platform", 10 - n, "GRO-" + n))];
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        Assert.Equal([items[4].Id, items[3].Id], CardIds(cut));

        _ = Post(host, "Nova", "Growth", 0, "GRO-6");
        await cut.Find(".feed-show-more").ClickAsync(new());
        await cut.Find(".feed-show-more").ClickAsync(new());

        Assert.Equal([items[4].Id, items[3].Id, items[2].Id, items[1].Id, items[0].Id], CardIds(cut));
        Assert.Empty(cut.FindAll(".feed-show-more"));
    }

    /// <summary>A page that holds fewer items than a page has no "Show more".</summary>
    [Fact]
    public async Task ShowMore_IsAbsentWhenEverythingFits()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = Post(host, "Nova", "Growth", 1, "GRO-1");
        await using MudBunitContext ctx = NewContext(host);

        Assert.Empty(Render(ctx).FindAll(".feed-show-more"));
    }

    /// <summary>A live item that matches the filter is drawn at once from the top, and one that does not match is ignored.</summary>
    [Fact]
    public async Task LiveItem_AtTheTop_IsDrawn_AndANonMatchingOneIsIgnored()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = Post(host, "Nova", "Growth", 5, "GRO-1");
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, agent: "Nova");
        FeedItem mine = Post(host, "Nova", "Growth", 1, "GRO-2");
        FeedItem other = Post(host, "Kai", "Growth", 0, "GRO-3");

        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsAdded([other, mine]));

        cut.WaitForAssertion(() => Assert.Contains(mine.Id, CardIds(cut)));
        Assert.DoesNotContain(other.Id, CardIds(cut));
        Assert.Empty(cut.FindAll(".feed-new-pill"));
        Assert.Equal("All 2", Segments(cut)[0]);
    }

    /// <summary>A live item that arrives while the list is scrolled waits behind the "New items" pill, and the counts are re-read.</summary>
    [Fact]
    public async Task LiveItem_WhileScrolled_WaitsBehindThePill()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = Post(host, "Nova", "Growth", 5, "GRO-1");
        await using MudBunitContext ctx = NewContext(host);
        ctx.JSInterop.Setup<bool>("huddleFeed.isScrolled", _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        FeedItem fresh = Post(host, "Kai", "Platform", 0, "PLA-1");

        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsAdded([fresh]));

        cut.WaitForAssertion(() => Assert.Equal("↑ 1 new item", Text(cut.Find(".feed-new-pill"))));
        Assert.DoesNotContain(fresh.Id, CardIds(cut));
        Assert.Equal("All 2", Segments(cut)[0]);
    }

    /// <summary>A resolve re-reads the counts: Needs you falls by one and the shown card is reloaded.</summary>
    [Fact]
    public async Task ItemsChanged_RereadsTheCounts()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem ask = host.Feed.PostNeedsYou();
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        Assert.Equal("Needs you 1", Segments(cut)[1]);

        host.Feed.Resolve(ask);
        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsChanged());

        cut.WaitForAssertion(() => Assert.Equal("Needs you 0", Segments(cut)[1]));
        Assert.Contains(ask.Id, CardIds(cut));
    }

    /// <summary>In the Needs you view a resolved item leaves the query, so its card must disappear rather than stay drawn as still waiting.</summary>
    [Fact]
    public async Task ItemsChanged_InNeedsYouView_DropsTheResolvedCard()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem ask = host.Feed.PostNeedsYou();
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, category: "needs-you");
        Assert.Contains(ask.Id, CardIds(cut));

        host.Feed.Resolve(ask);
        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsChanged());

        cut.WaitForAssertion(() => Assert.DoesNotContain(ask.Id, CardIds(cut)));
    }

    /// <summary>Two seconds after the page is shown it marks the Tab seen up to the newest item once, and cards above the old mark keep their unseen bar.</summary>
    [Fact]
    public async Task TabSeen_IsMarkedOnceTwoSecondsAfterTheFirstShow()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem older = Post(host, "Nova", "Growth", 5, "GRO-1");
        FeedItem newest = Post(host, "Kai", "Platform", 1, "PLA-1");
        _ = host.Feed.Store.Apply([new MarkTabSeenCommand(older.Id, FeedTestData.Now)]);
        FakeTimeProvider clock = new(FeedTestData.Now);
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        await using MudBunitContext ctx = NewContext(host, clock);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        Assert.Single(cut.FindAll(".feed-card-bar"));

        clock.Advance(TimeSpan.FromMilliseconds(1900));
        Assert.Empty(host.Feed.ApplyQueued());
        clock.Advance(TimeSpan.FromMilliseconds(200));
        clock.Advance(TimeSpan.FromSeconds(10));

        MarkTabSeenCommand mark = Assert.IsType<MarkTabSeenCommand>(Assert.Single(host.Feed.ApplyQueued()));
        Assert.Equal(newest.Id, mark.UpToId);
        Assert.Single(cut.FindAll(".feed-card-bar"));
    }

    /// <summary>Each empty state names what is missing, and the header and filter bar stay drawn.</summary>
    /// <param name="category">The <c>category</c> parameter.</param>
    /// <param name="agent">The <c>agent</c> parameter.</param>
    /// <param name="team">The <c>team</c> parameter.</param>
    /// <param name="title">The expected title.</param>
    /// <param name="text">The expected sentence.</param>
    [Theory]
    [InlineData(null, null, null, "Nothing yet", "What your Agents do appears here.")]
    [InlineData(null, "Nova", null, "Nothing from Nova yet.", "")]
    [InlineData("tasks", null, null, "Nothing here yet.", "Try All, or another Team.")]
    [InlineData(null, null, "Growth", "Nothing here yet.", "Try All, or another Team.")]
    [InlineData("needs-you", null, null, "Nothing needs you", "Asks from your Agents appear here.")]
    public async Task EmptyStates_NameWhatIsMissing_AndKeepTheHeader(string? category, string? agent, string? team, string title, string text)
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, category, agent, team);

        Assert.Equal(title, Text(cut.Find(".empty-state-title")));
        Assert.Equal(text, Text(cut.Find(".empty-state-text")));
        Assert.Equal("Feed", Text(cut.Find("h1")));
        Assert.Equal(8, cut.FindAll(".mud-toggle-item").Count);
    }

    /// <summary>With the Feed turned off the page draws only a plain empty state.</summary>
    [Fact]
    public async Task Disabled_DrawsOnlyAnEmptyState()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        host.Feed.Options.Value.Feed.Enabled = false;
        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("The Feed is turned off.", Text(cut.Find(".empty-state-title")));
        Assert.Equal("Set Team:Feed:Enabled to turn it on.", Text(cut.Find(".empty-state-text")));
        Assert.Empty(cut.FindAll("h1"));
        Assert.Empty(cut.FindAll(".mud-toggle-item"));
    }

    /// <summary>Source pin: the Feed link is the first entry of the Activity bar's bottom toolbar, above Archived chats, with an icon, label and tooltip, and only while the Feed is enabled.</summary>
    [Fact]
    public void MainLayout_PutsTheFeedLinkAtTheTopOfTheActivityBar_OnlyWhenEnabled()
    {
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        int top = layout.IndexOf("<Top>", StringComparison.Ordinal);
        int bottom = layout.IndexOf("<Bottom>", StringComparison.Ordinal);
        int guard = layout.IndexOf("@if (this.FeedOptions.Value.Feed.Enabled)", StringComparison.Ordinal);
        int link = layout.IndexOf("Href=\"/feed\"", StringComparison.Ordinal);
        int archived = layout.IndexOf("<ArchivedChats", StringComparison.Ordinal);
        Assert.True(top >= 0 && top < guard && guard < link && link < bottom && bottom < archived, "The Feed link must sit in the Top slot, behind the Enabled guard, above the Bottom slot that holds Archived chats");
        string tag = layout[layout.LastIndexOf("<MudTooltip", link, StringComparison.Ordinal)..layout.IndexOf("</MudTooltip>", link, StringComparison.Ordinal)];
        Assert.Contains("Text=\"Feed\"", tag, StringComparison.Ordinal); // contains-ok: a source pin on one element of a layout file
        Assert.Contains("aria-label=\"Feed\"", tag, StringComparison.Ordinal); // contains-ok: a source pin on one element of a layout file
        Assert.Contains("Icons.Material.Outlined.DynamicFeed", tag, StringComparison.Ordinal); // contains-ok: a source pin on one element of a layout file
    }

    /// <summary>
    /// The Tab's own address is the filter. A component keeps the last value of a parameter that a new address no longer supplies, so
    /// after choosing Needs you the address /feed (All) still arrives with Category "needs-you"; the page must read All from the address.
    /// </summary>
    [Fact]
    public async Task TabAddressWithoutAFilter_BeatsAStaleParameter()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = NewContext(host);
        TabPageContext page = OpenFeedTab(ctx, "/feed");

        IRenderedComponent<ContainerFragment> cut = Render(ctx, category: "needs-you", agent: "Nova", team: "Growth", page: page);

        Assert.Equal("All 0", Text(cut.Find(".huddle-segmented-active-item")));
        Assert.Empty(cut.FindAll(".feed-agent-chip"));
    }
}
