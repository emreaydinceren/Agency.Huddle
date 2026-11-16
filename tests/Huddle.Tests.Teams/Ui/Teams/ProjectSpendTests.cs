using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins #557: <c>ProjectSpend</c> shows what the Project's own Rooms spent, with its note, and follows <see cref="PersonaSpend.SpendChanged"/>.</summary>
public sealed class ProjectSpendTests
{
    private const string NoteText = "Counts Turns run in this Project's Rooms.";

    /// <summary>Only the Project's own Rooms are counted, and the note is shown.</summary>
    [Fact]
    public async Task Spend_CountsOnlyTheProjectsOwnRooms_AndShowsTheNote()
    {
        using TempDataDir dir = new();
        Rig rig = new(dir);
        RoomScopeSeed.Place(dir, "mine-1", "Business", "Marketing");
        RoomScopeSeed.Place(dir, "mine-2", "Business", "Marketing");
        RoomScopeSeed.Place(dir, "other-project", "Business", "Sales");
        RoomScopeSeed.Place(dir, "other-team", "Support", "Marketing");
        RoomScopeSeed.Place(dir, "no-project", "Business", null);
        rig.Rooms.Add("mine-1", "USD", 1.5m);
        rig.Rooms.Add("mine-2", "USD", 2m);
        rig.Rooms.Add("other-project", "USD", 100m);
        rig.Rooms.Add("other-team", "USD", 1000m);
        rig.Rooms.Add("no-project", "USD", 10000m);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ProjectSpend> cut = ctx.Render<ProjectSpend>(p => p.Add(x => x.Team, "Business").Add(x => x.Project, "Marketing"));

        Assert.Equal(["3.500 USD"], cut.FindAll(".team-spend-amount").Select(item => item.TextContent.Trim()));
        Assert.Equal("Spent", cut.Find(".team-spend .mud-typography").TextContent.Trim());
        Assert.Equal(NoteText, cut.Find(".project-spend-note").TextContent.Trim());
    }

    /// <summary>A Project with no spend draws nothing: no "Spent" and no note.</summary>
    [Fact]
    public async Task Spend_NothingSpent_DrawsNothing()
    {
        using TempDataDir dir = new();
        Rig rig = new(dir);
        RoomScopeSeed.Place(dir, "empty", "Business", "Marketing");
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ProjectSpend> cut = ctx.Render<ProjectSpend>(p => p.Add(x => x.Team, "Business").Add(x => x.Project, "Marketing"));

        Assert.Empty(cut.FindAll(".team-spend"));
        Assert.DoesNotContain("Spent", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(NoteText, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>New spend in a Project Room repaints the total with no render from the test, and disposing the component unsubscribes it.</summary>
    [Fact]
    public async Task Spend_SpendChanged_RepaintsTotal_AndDisposeUnsubscribes()
    {
        using TempDataDir dir = new();
        Rig rig = new(dir);
        RoomScopeSeed.Place(dir, "room-1", "Business", "Marketing");
        rig.Spend.Add("Nova", "s1", 1m, "USD", roomId: "room-1");
        await using MudBunitContext ctx = rig.NewContext();
        int before = rig.Spend.SubscriberCount;

        IRenderedComponent<ProjectSpend> cut = ctx.Render<ProjectSpend>(p => p.Add(x => x.Team, "Business").Add(x => x.Project, "Marketing"));
        Assert.Equal(["1.000 USD"], cut.FindAll(".team-spend-amount").Select(item => item.TextContent.Trim()));
        Assert.Equal(before + 1, rig.Spend.SubscriberCount);

        rig.Spend.Add("Nova", "s1", 3m, "USD", roomId: "room-1");

        cut.WaitForAssertion(() => Assert.Equal(["3.000 USD"], cut.FindAll(".team-spend-amount").Select(item => item.TextContent.Trim())));

        ((IDisposable)cut.Instance).Dispose();

        Assert.Equal(before, rig.Spend.SubscriberCount);
    }

    /// <summary>The stores a <c>ProjectSpend</c> reads, over one data folder.</summary>
    private sealed class Rig
    {
        public Rig(TempDataDir dir)
        {
            this.Rooms = new RoomSpendStore(dir.Options());
            this.Spend = new PersonaSpend(NullLogger<PersonaSpend>.Instance, new PersonaSpendStore(dir.Options()), this.Rooms);
        }

        public RoomSpendStore Rooms { get; }

        public PersonaSpend Spend { get; }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.Rooms);
            ctx.Services.AddSingleton(this.Spend);
            return ctx;
        }
    }
}
