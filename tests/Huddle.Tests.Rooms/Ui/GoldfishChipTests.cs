using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MudBlazor;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the goldfish chip in a Room's header (Goldfish slice 4, step 3, C2c): it shows only while <see cref="GoldfishLive"/>
/// reports an Agent in goldfish mode in that Room, says so with the minutes spent, and its "Call {Name} back" button posts the
/// comeback Message as the Human through the same service call the Composer uses.
/// </summary>
public sealed class GoldfishChipTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>With no possession in the Room the chip draws nothing: no chip and no button.</summary>
    [Fact]
    public async Task Chip_NoPossession_DrawsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        Assert.Equal((0, 0), (cut.FindAll(".status-chip").Count, cut.FindAll("button").Count));
    }

    /// <summary>A Working possession reads "{Name} in goldfish mode · {n} min" in the Info status chip, with the button beside it.</summary>
    [Fact]
    public async Task Chip_Working_ReadsInGoldfishModeWithMinutes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        IElement chip = cut.Find(".status-chip");
        Assert.Equal(("Ruth in goldfish mode · 3 min", true, "Call Ruth back"), (chip.TextContent.Trim(), chip.ClassList.Contains("status-chip-info"), CallButton(cut).TextContent.Trim()));
    }

    /// <summary>A Waiting possession reads "{Name} (goldfish) is waiting · {n} min".</summary>
    [Fact]
    public async Task Chip_Waiting_ReadsIsWaitingWithMinutes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Waiting, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        Assert.Equal("Ruth (goldfish) is waiting · 3 min", cut.Find(".status-chip").TextContent.Trim());
    }

    /// <summary>The minute count follows the clock: nine minutes later the once-a-minute timer re-renders "12 min".</summary>
    [Fact]
    public async Task Chip_ClockAdvances_ReRendersTheMinutes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();
        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);
        Assert.Equal("Ruth in goldfish mode · 3 min", cut.Find(".status-chip").TextContent.Trim());

        rig.Clock.Advance(TimeSpan.FromMinutes(9));

        cut.WaitForAssertion(() => Assert.Equal("Ruth in goldfish mode · 12 min", cut.Find(".status-chip").TextContent.Trim()));
    }

    /// <summary>A possession in another Room does not show in this Room's header.</summary>
    [Fact]
    public async Task Chip_PossessionInAnotherRoom_DrawsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, "another-room", ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        Assert.Equal((0, 0), (cut.FindAll(".status-chip").Count, cut.FindAll("button").Count));
    }

    /// <summary>The button posts exactly "@Ruth comeback" into the Room as the Human, as the Composer's submit does.</summary>
    [Fact]
    public async Task CallBack_Click_PostsTheComebackAsTheHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();
        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        await cut.InvokeAsync(() => CallButton(cut).Click());

        ChatMessage posted = Assert.Single(await rig.Stack.Store.ReadAllAsync(rig.Room.Id, ct));
        Assert.Equal((KnownIds.Human, "@Ruth comeback"), (posted.SenderId, posted.Text));
    }

    /// <summary>After a click the button stays disabled, so the Human cannot send a second comeback while the possession is still open.</summary>
    [Fact]
    public async Task CallBack_Click_DisablesTheButtonUntilThePossessionEnds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();
        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        await cut.InvokeAsync(() => CallButton(cut).Click());

        cut.WaitForAssertion(() => Assert.True(CallButton(cut).HasAttribute("disabled")));
    }

    /// <summary>When the possession moves to Returning the chip and its button leave the header.</summary>
    [Fact]
    public async Task Chip_PossessionEnds_Disappears()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();
        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);
        Assert.Single(cut.FindAll(".status-chip"));

        Assert.True(rig.Store.BeginReturn("p1", GoldfishExit.Finished, null));

        cut.WaitForAssertion(() => Assert.Equal((0, 0), (cut.FindAll(".status-chip").Count, cut.FindAll("button").Count)));
    }

    /// <summary>A possession already Returning shows nothing: only Starting, Working and Waiting are live.</summary>
    [Fact]
    public async Task Chip_Returning_DrawsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        Assert.True(rig.Store.BeginReturn("p1", GoldfishExit.Finished, null));
        await rig.Live.RefreshAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        Assert.Equal((0, 0), (cut.FindAll(".status-chip").Count, cut.FindAll("button").Count));
    }

    /// <summary>The live chip never shows spend, even when the row has some.</summary>
    [Fact]
    public async Task Chip_RowWithSpend_ShowsNoSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct, spend: 0.12m, currency: "USD");
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        Assert.Equal("Ruth in goldfish mode · 3 min", cut.Find(".status-chip").TextContent.Trim());
    }

    /// <summary>The chip carries the fresh-eyes tooltip, <see cref="GoldfishStamp.Tooltip"/>.</summary>
    [Fact]
    public async Task Chip_Working_HasTheGoldfishTooltip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, rig.Room.Id, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx);

        Assert.Equal(GoldfishStamp.Tooltip, cut.FindComponent<MudTooltip>().Instance.Text);
    }

    /// <summary>The posted text is one the server's comeback recogniser accepts, for a Name with and without a space.</summary>
    [Theory]
    [InlineData("Ruth")]
    [InlineData("Mary Ann")]
    public async Task ComebackText_IsRecognisedByMatches(string name)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        User agent = await rig.Team.UpsertAgentUserAsync(name, null, ct) ?? throw new InvalidOperationException("Agent was not created.");

        Func<string, string> build = GoldfishComeback.Text;
        string text = build(name);
        bool matches = GoldfishComeback.Matches(text, agent, [agent], [], false);

        Assert.Equal(("@" + name + " comeback", true), (text, matches));
    }

    /// <summary>A failed post (the Room is gone) raises an error snackbar and leaves the button usable again.</summary>
    [Fact]
    public async Task CallBack_ChatException_ShowsSnackbarAndReEnables()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", PossessionState.Working, "missing-room", ct);
        await using MudBunitContext ctx = rig.NewContext();
        IRenderedComponent<ContainerFragment> cut = rig.Render(ctx, "missing-room");

        await cut.InvokeAsync(() => CallButton(cut).Click());

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.WaitForAssertion(() => Assert.Equal(Severity.Error, Assert.Single(snackbar.ShownSnackbars).Severity));
        cut.WaitForAssertion(() => Assert.False(CallButton(cut).HasAttribute("disabled")));
    }

    private static IElement CallButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Call Ruth back");

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;

        private Rig(TempDataDir dir, SqliteTeamDirectory team, GoldfishStore store, GoldfishLive live, ChatStack stack, User ruth, Room room)
        {
            this.dir = dir;
            this.Team = team;
            this.Store = store;
            this.Live = live;
            this.Stack = stack;
            this.Ruth = ruth;
            this.Room = room;
        }

        public SqliteTeamDirectory Team { get; }

        public GoldfishStore Store { get; }

        public GoldfishLive Live { get; }

        public ChatStack Stack { get; }

        public User Ruth { get; }

        public Room Room { get; }

        public FakeTimeProvider Clock { get; } = new(T0.AddMinutes(3));

        public static async Task<Rig> CreateAsync(CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User ruth = await team.UpsertAgentUserAsync("Ruth", null, ct) ?? throw new InvalidOperationException("Ruth was not created.");
            Room room = await team.CreateRoomAsync("Work", [KnownIds.Human, ruth.Id], ct);
            GoldfishStore store = new(dir.Options());
            GoldfishLive live = new(store, team, NullLogger<GoldfishLive>.Instance);
            ChatStack stack = ChatServiceFactory.Create(team, dir.Options());
            return new Rig(dir, team, store, live, stack, ruth, room);
        }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.Live);
            ctx.Services.AddSingleton(this.Stack.Chat);
            ctx.Services.AddSingleton<TimeProvider>(this.Clock);
            return ctx;
        }

        public IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, string? roomId = null) =>
            ctx.RenderWithPopovers(builder =>
            {
                builder.OpenComponent<App.Components.Chat.GoldfishChip>(0);
                builder.AddAttribute(1, nameof(App.Components.Chat.GoldfishChip.RoomId), roomId ?? this.Room.Id);
                builder.CloseComponent();
            });

        public async Task PossessAsync(string id, PossessionState state, string roomId, CancellationToken ct, decimal spend = 0m, string? currency = null)
        {
            Possession row = new(id, this.Ruth.Id, roomId, null, "Look into it", state, null, null, T0, null, 0, spend, currency, null);
            Assert.True(this.Store.TryInsert(row));
            await this.Live.RefreshAsync(ct);
        }

        public void Dispose()
        {
            this.Live.Dispose();
            this.dir.Dispose();
        }
    }
}
