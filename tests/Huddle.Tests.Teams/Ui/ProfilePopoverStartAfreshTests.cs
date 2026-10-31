using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Factories;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The popover a Room header's avatar opens offers <c>Start afresh</c> (Spec LR-R15, corrections C1 and
/// C9, texts T10 and T11): beside Edit and Message, only when the avatar was drawn by a Room's header
/// (both <c>StartAfresh*</c> parameters set), only for an Agent whose Adapter keeps one session per
/// Room, never for the Human; clicking asks inline, and only the confirm sends the request.
/// </summary>
public sealed class ProfilePopoverStartAfreshTests
{
    /// <summary>The avatar's button.</summary>
    private const string Button = "button.profile-avatar-button";

    /// <summary>The open popover's panel.</summary>
    private const string Panel = ".profile-popover";

    /// <summary>T10: the button's aria-label for Ada.</summary>
    private const string StartAfreshAda = Panel + " button[aria-label='Start Ada afresh in this Room']";

    /// <summary>The Room the header belongs to.</summary>
    private const string RoomId = "room-1";

    /// <summary>The Adapter whose profile keeps one session per Room.</summary>
    private const string PerRoomAdapter = "claude";

    /// <summary>The Adapter whose profile shares one session across Rooms.</summary>
    private const string SharedAdapter = "shared";

    /// <summary>An Adapter catalog with one per-Room Adapter first (the default) and one shared-session Adapter.</summary>
    private static AdapterCatalog BuildCatalog() =>
        new(TestOptions.Create(opts => opts.Acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = PerRoomAdapter, DisplayName = "Claude Code", Command = "node", SessionPerRoom = true },
                new AdapterProfileOptions { Id = SharedAdapter, DisplayName = "Shared", Command = "node", SessionPerRoom = false },
            ],
        }));

    /// <summary>A context whose services include the Adapter catalog the popover consults for <c>SessionPerRoom</c>.</summary>
    private static MudBunitContext NewContext(ProfileFixture fixture, AdapterCatalog catalog)
    {
        MudBunitContext ctx = fixture.NewContext();
        ctx.Services.AddSingleton(catalog);
        return ctx;
    }

    /// <summary>Renders the avatar for <paramref name="name"/>, with the two Start afresh parameters set when ids are given.</summary>
    private static IRenderedComponent<ContainerFragment> RenderIt(MudBunitContext ctx, string name, string? roomId, string? agentId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProfileAvatar>(0);
            builder.AddAttribute(1, nameof(ProfileAvatar.Name), name);
            builder.AddAttribute(2, nameof(ProfileAvatar.StartAfreshRoomId), roomId);
            builder.AddAttribute(3, nameof(ProfileAvatar.StartAfreshAgentId), agentId);
            builder.CloseComponent();
        });

    /// <summary>Opens the popover and waits for the card to appear.</summary>
    private static async Task OpenAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find(Button).ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".roster-card")));
    }

    /// <summary>Saves Ada on <paramref name="adapter"/> with an agent user and returns that user's id.</summary>
    private static async Task<string> AddAdaAsync(ProfileFixture fixture, string? adapter, CancellationToken ct)
    {
        await fixture.InitializeAsync(ct);
        fixture.AddAgent("Ada", "Lead", adapter: adapter);
        User user = await fixture.AddAgentAsync("Ada", ct);
        return user.Id;
    }

    /// <summary>T10: with both parameters set and a per-Room Adapter, the button shows beside Edit and Message with the settled label and aria-label.</summary>
    [Fact]
    public async Task PerRoomAdapter_ShowsStartAfreshButtonBesideEditAndMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, PerRoomAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", RoomId, agentId);

        await OpenAsync(cut);

        Assert.Single(cut.FindAll(Panel + " button[aria-label='Edit Ada']"));
        Assert.Single(cut.FindAll(Panel + " button[aria-label='Message Ada']"));
        var button = Assert.Single(cut.FindAll(StartAfreshAda));
        Assert.Equal("Start afresh", button.TextContent.Trim());
        Assert.Equal("Start Ada afresh in this Room", button.GetAttribute("aria-label"));
    }

    /// <summary>An Agent on no named Adapter runs on the default one, which keeps a session per Room, so it gets the button too.</summary>
    [Fact]
    public async Task DefaultAdapter_ShowsStartAfreshButton()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, null, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", RoomId, agentId);

        await OpenAsync(cut);

        Assert.Single(cut.FindAll(StartAfreshAda));
    }

    /// <summary>T11: clicking asks inline with the settled prompt, a <c>Start afresh</c> verb and Cancel, and sends nothing yet.</summary>
    [Fact]
    public async Task Click_ShowsSettledConfirmAndSendsNothingYet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, PerRoomAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", RoomId, agentId);
        await OpenAsync(cut);

        await cut.InvokeAsync(() => cut.Find(StartAfreshAda).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(
            "Start afresh? Ada keeps every Message but forgets its working notes for this Room.",
            cut.Find(Panel + " .inline-confirm-prompt").TextContent.Trim()));
        Assert.Equal("Start afresh", cut.Find(Panel + " .inline-confirm-confirm").TextContent.Trim());
        Assert.Equal("Cancel", cut.Find(Panel + " .inline-confirm-cancel").TextContent.Trim());
        Assert.Empty(fixture.Gateway.StartAfreshCalls);
    }

    /// <summary>Confirming records exactly one request for that Agent and Room, and closes the popover.</summary>
    [Fact]
    public async Task Confirm_SendsOneStartAfreshAndClosesPopover()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, PerRoomAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", RoomId, agentId);
        await OpenAsync(cut);
        await cut.InvokeAsync(() => cut.Find(StartAfreshAda).ClickAsync());

        await cut.InvokeAsync(() => cut.Find(Panel + " .inline-confirm-confirm").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Panel)));
        (string AgentId, string RoomId) call = Assert.Single(fixture.Gateway.StartAfreshCalls);
        Assert.Equal(agentId, call.AgentId);
        Assert.Equal(RoomId, call.RoomId);
    }

    /// <summary>Cancel records nothing and puts the Start afresh button back.</summary>
    [Fact]
    public async Task Cancel_SendsNothingAndRestoresButton()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, PerRoomAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", RoomId, agentId);
        await OpenAsync(cut);
        await cut.InvokeAsync(() => cut.Find(StartAfreshAda).ClickAsync());

        await cut.InvokeAsync(() => cut.Find(Panel + " .inline-confirm-cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(StartAfreshAda)));
        Assert.Empty(cut.FindAll(Panel + " .inline-confirm-prompt"));
        Assert.Empty(fixture.Gateway.StartAfreshCalls);
    }

    /// <summary>Any other page draws the avatar without the two parameters, so the popover has no Start afresh button.</summary>
    [Fact]
    public async Task WithoutParameters_NoButton()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await AddAdaAsync(fixture, PerRoomAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", null, null);

        await OpenAsync(cut);

        Assert.Single(cut.FindAll(Panel + " button[aria-label='Edit Ada']"));
        Assert.Empty(cut.FindAll(StartAfreshAda));
    }

    /// <summary>C9: an Adapter that shares one session across Rooms has nothing to start afresh, so no button.</summary>
    [Fact]
    public async Task SharedSessionAdapter_NoButton()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, SharedAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        var cut = RenderIt(ctx, "Ada", RoomId, agentId);

        await OpenAsync(cut);

        Assert.Single(cut.FindAll(Panel + " button[aria-label='Message Ada']"));
        Assert.Empty(cut.FindAll(StartAfreshAda));
    }

    /// <summary>C9: the Human's own avatar opens Edit only; even with both parameters set there is no Start afresh button.</summary>
    [Fact]
    public async Task HumanAvatar_NoButton()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        string agentId = await AddAdaAsync(fixture, PerRoomAdapter, ct);
        await using MudBunitContext ctx = NewContext(fixture, BuildCatalog());
        AgentProfiles withHuman = new(
            fixture.Personas,
            fixture.Types,
            fixture.Pool,
            fixture.Avatars,
            fixture.Catalog,
            fixture.Directory,
            fixture.Gateway,
            fixture.Health,
            BuildCatalog(),
            fixture.ModelCatalog,
            NullLogger<AgentProfiles>.Instance,
            new ConfiguredHumanIdentity("Sam"));
        ctx.Services.AddSingleton(withHuman);
        var cut = RenderIt(ctx, "Sam", RoomId, agentId);

        await OpenAsync(cut);

        Assert.Single(cut.FindAll(Panel + " button[aria-label='Edit Sam']"));
        Assert.Empty(cut.FindAll(Panel + " button[aria-label='Start Sam afresh in this Room']"));
        Assert.Empty(cut.FindAll(Panel + " button[aria-label^='Start ']"));
    }
}
