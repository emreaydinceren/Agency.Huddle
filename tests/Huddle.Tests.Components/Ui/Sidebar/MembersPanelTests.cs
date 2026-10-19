using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="Agency.Huddle.App.Components.Sidebar.MembersPanel"/>: the full member list of the open Room (Spec Members, D-8).</summary>
public sealed class MembersPanelTests
{
    private const string Row = ".members-row";

    private static async Task<Room> NewRoomAsync(ProfileFixture fixture, CancellationToken ct, params string[] agentNames)
    {
        await fixture.InitializeAsync(ct);
        User human = await fixture.Directory.GetHumanAsync(ct);
        List<string> ids = [human.Id];
        foreach (string name in agentNames)
        {
            User agent = await fixture.AddAgentAsync(name, ct);
            ids.Add(agent.Id);
        }

        return await fixture.Directory.CreateRoomAsync("Room", ids, "Alpha", false, ct);
    }

    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> RenderPanel(MudBunitContext ctx, SidebarPanelContext panelCtx, PageSubject subject) =>
        ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<Agency.Huddle.App.Components.Sidebar.MembersPanel>(s => s.Add(c => c.Subject, subject).Add(c => c.TabId, "t1")));

    private static MudBunitContext NewContext(ProfileFixture fixture, RoomEvents events)
    {
        MudBunitContext ctx = fixture.NewContext();
        ctx.Services.AddSingleton(events);
        return ctx;
    }

    /// <summary>Agents are listed before Humans, and each group is ordered by name.</summary>
    [Fact]
    public async Task Members_AgentsFirstThenHumans_ByName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        Room room = await NewRoomAsync(fixture, ct, "Zed", "Ada");
        await using MudBunitContext ctx = NewContext(fixture, new RoomEvents(NullLogger<RoomEvents>.Instance));

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, new SidebarPanelContext(), new RoomSubject(room.Id));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(Row).Count));
        List<string> names = [.. cut.FindAll(Row + " .members-name").Select(e => e.TextContent.Trim())];
        Assert.Equal(["Ada", "Zed", "You"], names);
    }

    /// <summary>Every row is a ProfileAvatar, so a click opens the profile popover.</summary>
    [Fact]
    public async Task Members_EachRowIsAProfileAvatar()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        Room room = await NewRoomAsync(fixture, ct, "Ada");
        await using MudBunitContext ctx = NewContext(fixture, new RoomEvents(NullLogger<RoomEvents>.Instance));

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, new SidebarPanelContext(), new RoomSubject(room.Id));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(Row).Count));
        Assert.Equal(2, cut.FindAll(Row + " button.profile-avatar-button").Count);
        Assert.Equal("Show Ada's profile", cut.FindAll(Row + " button.profile-avatar-button")[0].GetAttribute("aria-label"));
    }

    /// <summary>An Agent's dot follows its presence; a Human has none.</summary>
    [Fact]
    public async Task Members_StatusDotFollowsPresence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        Room room = await NewRoomAsync(fixture, ct, "Ada");
        User agent = await fixture.Directory.FindUserByNameAsync("Ada", ct) ?? throw new InvalidOperationException("Ada was not created.");
        await using MudBunitContext ctx = NewContext(fixture, new RoomEvents(NullLogger<RoomEvents>.Instance));
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, new SidebarPanelContext(), new RoomSubject(room.Id));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Row + " .agent-dot.offline")));
        Assert.Single(cut.FindAll(".agent-dot"));

        fixture.Gateway.SetOnline(agent.Id);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Row + " .agent-dot.online")));
    }

    /// <summary>An invite raises RoomsChanged and the list reloads with the new member.</summary>
    [Fact]
    public async Task Members_RoomsChanged_Reloads()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        Room room = await NewRoomAsync(fixture, ct, "Ada");
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        await using MudBunitContext ctx = NewContext(fixture, events);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, new SidebarPanelContext(), new RoomSubject(room.Id));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(Row).Count));
        User joiner = await fixture.AddAgentAsync("Bob", ct);
        await fixture.Directory.AddMemberAsync(room.Id, joiner.Id, ct);

        events.PublishRoomsChanged();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(Row).Count));
    }

    /// <summary>The panel reports the number of members as its count.</summary>
    [Fact]
    public async Task Members_SetsTheCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        Room room = await NewRoomAsync(fixture, ct, "Ada", "Bob");
        await using MudBunitContext ctx = NewContext(fixture, new RoomEvents(NullLogger<RoomEvents>.Instance));
        SidebarPanelContext panelCtx = new();

        RenderPanel(ctx, panelCtx, new RoomSubject(room.Id));

        Assert.Equal(3, await WaitForCountAsync(panelCtx, 3));
    }

    /// <summary>A Room with no members shows the empty line and a count of zero.</summary>
    [Fact]
    public async Task Members_None_ShowsEmptyLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        await using MudBunitContext ctx = NewContext(fixture, new RoomEvents(NullLogger<RoomEvents>.Instance));
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, panelCtx, new RoomSubject("no-such-room"));

        cut.WaitForAssertion(() => Assert.Equal("No members in this chat.", cut.Find("[role=status]").TextContent.Trim()));
        Assert.Equal(0, panelCtx.Count);
    }

    /// <summary>After the panel is disposed, a RoomsChanged no longer reaches it.</summary>
    [Fact]
    public async Task Members_Disposed_Unsubscribes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        Room room = await NewRoomAsync(fixture, ct, "Ada");
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        await using MudBunitContext ctx = NewContext(fixture, events);
        SidebarPanelContext panelCtx = new();
        RenderPanel(ctx, panelCtx, new RoomSubject(room.Id));
        Assert.Equal(2, await WaitForCountAsync(panelCtx, 2));
        await ctx.DisposeComponentsOnDispatcherAsync();
        User joiner = await fixture.AddAgentAsync("Bob", ct);
        await fixture.Directory.AddMemberAsync(room.Id, joiner.Id, ct);

        events.PublishRoomsChanged();
        await Task.Delay(300, ct);

        Assert.Equal(2, panelCtx.Count);
    }

    private static async Task<int?> WaitForCountAsync(SidebarPanelContext panelCtx, int expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 100 && panelCtx.Count != expected; i++)
        {
            await Task.Delay(50, ct);
        }

        return panelCtx.Count;
    }
}
