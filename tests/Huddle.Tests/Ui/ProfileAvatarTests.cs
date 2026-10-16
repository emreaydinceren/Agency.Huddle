using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="ProfileAvatar"/>: an avatar that is a button, and the popover with the Teammate's
/// profile card that its click opens.
/// </summary>
public sealed class ProfileAvatarTests
{
    /// <summary>The avatar's button.</summary>
    private const string Button = "button.profile-avatar-button";

    /// <summary>The open popover's panel.</summary>
    private const string Panel = ".profile-popover";

    /// <summary>Renders the avatar for <paramref name="name"/> beside the popover and dialog hosts, inside a parent that counts the clicks that reach it.</summary>
    private static IRenderedComponent<ContainerFragment> RenderIt(MudBunitContext ctx, string name, string? team = null, Action? onParentClick = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create<MouseEventArgs>(ctx, () => onParentClick?.Invoke()));
            builder.AddAttribute(5, "class", "avatar-parent");
            builder.OpenComponent<ProfileAvatar>(2);
            builder.AddAttribute(3, nameof(ProfileAvatar.Name), name);
            builder.AddAttribute(4, nameof(ProfileAvatar.Team), team);
            builder.CloseComponent();
            builder.CloseElement();
        });

    /// <summary>Opens the popover and waits for the card to appear.</summary>
    private static async Task OpenAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find(Button).ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".roster-card")));
    }

    /// <summary>The button names the Teammate, announces a dialog, and nothing is open until it is clicked.</summary>
    [Fact]
    public async Task Closed_ButtonHasAccessibleNameAndNoPopover()
    {
        using ProfileFixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = RenderIt(ctx, "Ada");

        var button = cut.Find(Button);
        Assert.Equal("Show Ada's profile", button.GetAttribute("aria-label"));
        Assert.Equal("dialog", button.GetAttribute("aria-haspopup"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll(Panel));
        Assert.NotEmpty(cut.FindAll(Button + " .mud-avatar"));
    }

    /// <summary>Clicking the avatar opens the popover with the Teammate's name and title, and an Edit button.</summary>
    [Fact]
    public async Task Click_OpensCardWithNameTitleAndEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");

        await OpenAsync(cut);

        Assert.Equal("Ada", cut.Find(Panel + " .roster-name").TextContent.Trim());
        Assert.Equal("Lead", cut.Find(Panel + " .roster-title").TextContent);
        Assert.Equal("true", cut.Find(Button).GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll(Panel + " button[aria-label='Edit Ada']"));
    }

    /// <summary>
    /// The avatar stays one element in its parent while the popover is open. The message row is a
    /// two-column grid, so a second child (the popover's own wrapper) became a grid item and pushed the
    /// avatar out of its column; only a browser showed it.
    /// </summary>
    [Fact]
    public async Task Open_StaysOneChildOfItsParent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");

        await OpenAsync(cut);

        Assert.Single(cut.Find(".avatar-parent").Children);
    }

    /// <summary>A Teammate with no agent user has no Message button.</summary>
    [Fact]
    public async Task Message_AbsentWhenTeammateHasNoAgentUser()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");

        await OpenAsync(cut);

        Assert.Empty(cut.FindAll(Panel + " button[aria-label='Message Ada']"));
    }

    /// <summary>A Teammate with an agent user has a Message button.</summary>
    [Fact]
    public async Task Message_PresentWhenTeammateHasAgentUser()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        await fixture.AddAgentAsync("Ada", ct);
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");

        await OpenAsync(cut);

        Assert.Single(cut.FindAll(Panel + " button[aria-label='Message Ada']"));
    }

    /// <summary>The Human decides who is removed or re-rolled, so the popover offers none of those.</summary>
    [Fact]
    public async Task Card_HasNoRemoveRerollOrUndo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        await fixture.AddAgentAsync("Ada", ct);
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");

        await OpenAsync(cut);

        Assert.Empty(cut.FindAll(Panel + " .roster-remove, " + Panel + " .roster-reroll, " + Panel + " .roster-undo"));
        Assert.Empty(cut.FindAll(Panel + " [role=listitem]"));
    }

    /// <summary>A name with no Persona behind it says so instead of showing a card.</summary>
    [Fact]
    public async Task UnknownName_ShowsNoProfileText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "rawagent");

        await cut.InvokeAsync(() => cut.Find(Button).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("No profile for rawagent.", cut.Find(Panel + " .profile-popover-empty").TextContent));
        Assert.Empty(cut.FindAll(".roster-card"));
    }

    /// <summary>Message inside a Team opens the Team's own one-to-one and goes there.</summary>
    [Fact]
    public async Task Message_WithTeam_NavigatesToTeamOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead", "Garden");
        await fixture.AddAgentAsync("Ada", ct);
        fixture.AddTeam("Garden", null, "Ada");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada", "Garden");
        await OpenAsync(cut);

        await cut.InvokeAsync(() => cut.Find(Panel + " button[aria-label='Message Ada']").ClickAsync());

        string path = "";
        cut.WaitForAssertion(() =>
        {
            path = new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath;
            Assert.Matches("^/rooms/[0-9a-f]+$", path);
        });
        Room? room = await fixture.Directory.GetRoomAsync(path["/rooms/".Length..], ct);
        Assert.Equal("Garden", room?.Team);
        Assert.Empty(cut.FindAll(Panel));
    }

    /// <summary>Message outside a Team opens the root one-to-one and goes there.</summary>
    [Fact]
    public async Task Message_WithoutTeam_NavigatesToRootOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead", "Garden");
        await fixture.AddAgentAsync("Ada", ct);
        fixture.AddTeam("Garden", null, "Ada");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");
        await OpenAsync(cut);

        await cut.InvokeAsync(() => cut.Find(Panel + " button[aria-label='Message Ada']").ClickAsync());

        string path = "";
        cut.WaitForAssertion(() =>
        {
            path = new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath;
            Assert.Matches("^/rooms/[0-9a-f]+$", path);
        });
        Room? room = await fixture.Directory.GetRoomAsync(path["/rooms/".Length..], ct);
        Assert.NotNull(room);
        Assert.Null(room.Team);
    }

    /// <summary>Escape closes the popover.</summary>
    [Fact]
    public async Task Escape_ClosesPopover()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        await using MudBunitContext ctx = fixture.NewContext();
        var cut = RenderIt(ctx, "Ada");
        await OpenAsync(cut);

        await cut.InvokeAsync(() => cut.Find(Panel).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Panel)));
        Assert.Equal("false", cut.Find(Button).GetAttribute("aria-expanded"));
    }

    /// <summary>A Task assignee opts in to the profile button with ShowProfile and is a plain avatar otherwise.</summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task TaskAssignee_ShowProfile_ControlsTheButton(bool showProfile, int buttons)
    {
        using ProfileFixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.Render<Agency.Huddle.App.Components.Tasks.TaskAssignee>(parameters => parameters
            .Add(p => p.Name, "Ada")
            .Add(p => p.ShowProfile, showProfile));

        Assert.Equal(buttons, cut.FindAll(Button).Count);
        Assert.NotEmpty(cut.FindAll(".mud-avatar"));
    }

    /// <summary>A click on the avatar does not reach an ancestor's click handler, such as a Task card opening its detail.</summary>
    [Fact]
    public async Task Click_DoesNotReachAncestor()
    {
        using ProfileFixture fixture = new();
        await using MudBunitContext ctx = fixture.NewContext();
        int parentClicks = 0;
        var cut = RenderIt(ctx, "Ada", onParentClick: () => parentClicks++);

        await cut.InvokeAsync(() => cut.Find(Button).ClickAsync());

        Assert.Equal(0, parentClicks);
    }
}
