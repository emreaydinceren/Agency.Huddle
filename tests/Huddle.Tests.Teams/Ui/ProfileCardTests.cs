using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.TeamTypes;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="ProfileCard"/> on its own: the roster card shared by Build a team and the avatar
/// profile popover, and every optional part it draws or omits.
/// </summary>
public sealed class ProfileCardTests
{
    /// <summary>A profile with only the required text; every optional part is absent.</summary>
    private static readonly AgentProfile Plain = new("Ada", "Engineer", "Writes the code.", Avatar.None);

    /// <summary>Renders the card for <paramref name="profile"/>, letting the caller set any other parameter.</summary>
    private static IRenderedComponent<ProfileCard> RenderIt(MudBunitContext ctx, AgentProfile profile, Action<ComponentParameterCollectionBuilder<ProfileCard>>? configure = null) =>
        ctx.Render<ProfileCard>(parameters =>
        {
            parameters.Add(p => p.Profile, profile);
            configure?.Invoke(parameters);
        });

    /// <summary>Name, title and description each render in their own element.</summary>
    [Fact]
    public async Task Renders_NameTitleAndDescription()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Equal("Ada", cut.Find(".roster-name").TextContent.Trim());
        Assert.Equal("Engineer", cut.Find(".roster-title").TextContent);
        Assert.Equal("Writes the code.", cut.Find(".roster-description").TextContent);
        Assert.NotNull(cut.Find(".roster-avatar-ring[aria-hidden=true] .roster-avatar"));
    }

    /// <summary>Adapter, Model and Effort render as three label and value pairs, each value under its own label.</summary>
    [Fact]
    public async Task Runtime_Set_DrawsLabelledAdapterModelAndEffort()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { Runtime = new ProfileRuntime("Claude Code", "opus", "high") });

        string[] labels = [.. cut.FindAll(".roster-runtime dt").Select(label => label.TextContent)];
        string[] values = [.. cut.FindAll(".roster-runtime dd").Select(value => value.TextContent)];
        Assert.Equal(["Adapter", "Model", "Effort"], labels);
        Assert.Equal(["Claude Code", "opus", "high"], values);
        Assert.Equal("Claude Code", cut.Find(".roster-runtime-adapter").TextContent);
        Assert.Equal("opus", cut.Find(".roster-runtime-model").TextContent);
        Assert.Equal("high", cut.Find(".roster-runtime-effort").TextContent);
    }

    /// <summary>A profile with no runtime, such as the Human's or a roster draft, draws no runtime block.</summary>
    [Fact]
    public async Task Runtime_Unset_DrawsNothing()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-runtime"));
    }

    /// <summary>Unconnected connectors draw one informational "Not connected yet" line, and no Needs setup line.</summary>
    [Fact]
    public async Task UnconnectedConnectors_Set_DrawsNotConnectedYetLine()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { UnconnectedConnectors = ["email:read", "calendar:read"] });

        Assert.Equal("Not connected yet: email:read; calendar:read", cut.Find(".roster-connect").TextContent.Trim());
        Assert.Empty(cut.FindAll(".roster-setup"));
    }

    /// <summary>With no unconnected connectors the card draws no "Not connected yet" line.</summary>
    [Fact]
    public async Task UnconnectedConnectors_Empty_DrawsNothing()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-connect"));
    }

    /// <summary>A null state draws no status dot.</summary>
    [Fact]
    public async Task State_Null_DrawsNoStatusDot()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".agent-dot"));
    }

    /// <summary>A set state draws the presence badge on the avatar rim, not a dot beside the name.</summary>
    [Fact]
    public async Task State_Set_DrawsStatusDot()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { State = PersonaState.Online });

        Assert.Single(cut.FindAll(".roster-card-head .presence-anchor > .agent-dot.on-avatar.online"));
        Assert.Empty(cut.FindAll(".roster-name .agent-dot"));
    }

    /// <summary>A null tier draws no tier chip.</summary>
    [Fact]
    public async Task Tier_Null_DrawsNoTierChip()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-tier"));
    }

    /// <summary>A set tier draws a lower-cased chip carrying the tier's class.</summary>
    [Theory]
    [InlineData(ModelTier.Strongest, "strongest")]
    [InlineData(ModelTier.Balanced, "balanced")]
    [InlineData(ModelTier.Fast, "fast")]
    public async Task Tier_Set_DrawsTierChip(ModelTier tier, string expected)
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { Tier = tier });

        var chip = cut.Find(".roster-tier");
        Assert.True(chip.ClassList.Contains($"roster-tier-{expected}"));
        Assert.Equal(expected, chip.TextContent);
    }

    /// <summary>The skills chip counts the skills and lists their names in its title.</summary>
    [Fact]
    public async Task Skills_Present_DrawChipWithCountAndTitle()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { SkillNames = ["lint", "review"] });

        var chip = cut.Find(".roster-skills");
        Assert.Equal("2 skills", chip.TextContent);
        Assert.Equal("lint, review", chip.GetAttribute("title"));
    }

    /// <summary>One skill is singular.</summary>
    [Fact]
    public async Task Skills_One_IsSingular()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { SkillNames = ["lint"] });

        Assert.Equal("1 skill", cut.Find(".roster-skills").TextContent);
    }

    /// <summary>No skills draws no skills chip.</summary>
    [Fact]
    public async Task Skills_Empty_DrawNoChip()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-skills"));
    }

    /// <summary>Setup reasons render as one "Needs setup" line joined with semicolons.</summary>
    [Fact]
    public async Task SetupReasons_Present_DrawNeedsSetupLine()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { SetupReasons = ["no adapter", "no model"] });

        Assert.Equal("Needs setup: no adapter; no model", cut.Find(".roster-setup").TextContent);
    }

    /// <summary>No setup reasons draws no setup line.</summary>
    [Fact]
    public async Task SetupReasons_Empty_DrawNoLine()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-setup"));
    }

    /// <summary>The Manager flag draws the badge and the manager class.</summary>
    [Fact]
    public async Task Manager_True_DrawsBadgeAndClass()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.Manager, true));

        Assert.Equal("Manager", cut.Find(".roster-manager-badge").TextContent);
        Assert.True(cut.Find(".roster-card").ClassList.Contains("roster-card-manager"));
    }

    /// <summary>Without the Manager flag there is no badge and no manager class.</summary>
    [Fact]
    public async Task Manager_False_DrawsNeither()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-manager-badge"));
        Assert.False(cut.Find(".roster-card").ClassList.Contains("roster-card-manager"));
    }

    /// <summary>The Existing flag adds the existing class; its absence does not.</summary>
    [Fact]
    public async Task Existing_TogglesClass()
    {
        await using MudBunitContext ctx = new();

        var on = RenderIt(ctx, Plain, p => p.Add(c => c.Existing, true));
        var off = RenderIt(ctx, Plain);

        Assert.True(on.Find(".roster-card").ClassList.Contains("roster-card-existing"));
        Assert.False(off.Find(".roster-card").ClassList.Contains("roster-card-existing"));
    }

    /// <summary>Membership None draws no state chip.</summary>
    [Fact]
    public async Task Membership_None_DrawsNoChip()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-state"));
    }

    /// <summary>Membership Member draws the "On the Team" chip.</summary>
    [Fact]
    public async Task Membership_Member_DrawsMemberChip()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.Membership, ProfileMembership.Member));

        var chip = cut.Find(".roster-state");
        Assert.True(chip.ClassList.Contains("roster-state-member"));
        Assert.Equal("On the Team", chip.TextContent);
    }

    /// <summary>Membership New draws the "New" chip.</summary>
    [Fact]
    public async Task Membership_New_DrawsNewChip()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.Membership, ProfileMembership.New));

        var chip = cut.Find(".roster-state");
        Assert.True(chip.ClassList.Contains("roster-state-new"));
        Assert.Equal("New", chip.TextContent);
    }

    /// <summary>The membership chip comes first inside the chips row, then tier, then skills.</summary>
    [Fact]
    public async Task Chips_AreInOrder_MembershipTierSkills()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { Tier = ModelTier.Fast, SkillNames = ["lint"] }, p => p.Add(c => c.Membership, ProfileMembership.New));

        string[] classes = [.. cut.Find(".roster-chips").Children.Select(static child => child.ClassList[0]).Where(static name => name != "roster-sep")];
        Assert.Equal(["roster-state", "roster-tier", "roster-skills"], classes);
    }

    /// <summary>The facts on the chips line are separated by " ·", so "On the Team" and "1 skill" no longer read as one phrase (#479).</summary>
    [Fact]
    public async Task Chips_FactsAreSeparatedByMiddleDots()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { SkillNames = ["lint"] }, p => p.Add(c => c.Membership, ProfileMembership.Member));

        Assert.Equal(["On the Team", "·", "1 skill"], cut.Find(".roster-chips").Children.Select(static child => child.TextContent.Trim()).ToArray());
    }

    /// <summary>A lone fact has no separator (#479).</summary>
    [Fact]
    public async Task Chips_ALoneFact_HasNoSeparator()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain with { SkillNames = ["lint"] });

        Assert.Empty(cut.FindAll(".roster-sep"));
    }

    /// <summary>RemovingText adds the removed class and the removing line with that text.</summary>
    [Fact]
    public async Task RemovingText_Set_DrawsClassAndLine()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.RemovingText, "Will be deleted when you save"));

        Assert.True(cut.Find(".roster-card").ClassList.Contains("roster-card-removed"));
        Assert.Equal("Will be deleted when you save", cut.Find(".roster-removing").TextContent);
    }

    /// <summary>Without RemovingText there is no removed class and no removing line.</summary>
    [Fact]
    public async Task RemovingText_Null_DrawsNeither()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.False(cut.Find(".roster-card").ClassList.Contains("roster-card-removed"));
        Assert.Empty(cut.FindAll(".roster-removing"));
    }

    /// <summary>ListItem puts the listitem role on the root.</summary>
    [Fact]
    public async Task ListItem_True_RootHasListItemRole()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.ListItem, true));

        Assert.Equal("listitem", cut.Find(".roster-card").GetAttribute("role"));
    }

    /// <summary>Without ListItem the root carries no role.</summary>
    [Fact]
    public async Task ListItem_False_RootHasNoRole()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Null(cut.Find(".roster-card").GetAttribute("role"));
    }

    /// <summary>The caller's Class is appended to the root's classes.</summary>
    [Fact]
    public async Task Class_IsAppendedToRoot()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.Class, "extra"));

        Assert.True(cut.Find(".roster-card").ClassList.Contains("extra"));
    }

    /// <summary>No Actions fragment means no actions container at all.</summary>
    [Fact]
    public async Task Actions_Null_DrawsNoActionsContainer()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".roster-actions"));
    }

    /// <summary>An Actions fragment renders inside the actions container.</summary>
    [Fact]
    public async Task Actions_Set_RendersFragmentInsideContainer()
    {
        await using MudBunitContext ctx = new();
        RenderFragment actions = static builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "probe");
            builder.AddContent(2, "Go");
            builder.CloseElement();
        };

        var cut = RenderIt(ctx, Plain, p => p.Add(c => c.Actions, actions));

        Assert.Equal("Go", cut.Find(".roster-actions .probe").TextContent);
    }

    /// <summary>With <c>OnAvatarClick</c> set the card draws a spelled-out "Edit avatar…" button under the avatar, and it raises the same callback as the avatar.</summary>
    [Fact]
    public async Task OnAvatarClick_Set_DrawsTheEditAvatarButton_WhichRaisesTheCallback()
    {
        await using MudBunitContext ctx = new();
        int clicks = 0;

        var cut = RenderIt(ctx, Plain, parameters => parameters.Add(p => p.OnAvatarClick, EventCallback.Factory.Create(this, () => clicks++)));
        await cut.InvokeAsync(() => cut.Find(".avatar-edit-column .mud-button-text").ClickAsync());

        Assert.Equal("Edit avatar…", cut.Find(".avatar-edit-column .mud-button-text").TextContent.Trim());
        Assert.Equal(1, clicks);
    }

    /// <summary>Without <c>OnAvatarClick</c> the avatar is plain and there is no edit button.</summary>
    [Fact]
    public async Task OnAvatarClick_NotSet_HasNoEditAvatarButton()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderIt(ctx, Plain);

        Assert.Empty(cut.FindAll(".avatar-edit-column"));
        Assert.Empty(cut.FindAll("button"));
    }

    /// <summary>A profile with a sign-in warning draws it as its own line, with a "Check again" button only when a callback is given, and the button is off while checking.</summary>
    [Fact]
    public async Task SignInWarning_DrawsItsLine_AndCheckAgainOnlyWithACallback()
    {
        await using MudBunitContext ctx = new();
        AgentProfile warned = Plain with { SignInWarning = "Claude does not seem to be signed in." };
        int checks = 0;

        var without = RenderIt(ctx, warned);
        Assert.Equal("Claude does not seem to be signed in.", without.Find(".roster-signin-text").TextContent.Trim());
        Assert.Empty(without.FindAll("button.roster-signin-check"));

        var with = RenderIt(ctx, warned, parameters => parameters.Add(p => p.OnCheckSignIn, EventCallback.Factory.Create(this, () => checks++)));
        await with.InvokeAsync(() => with.Find("button.roster-signin-check").ClickAsync());
        Assert.Equal(1, checks);

        var busy = RenderIt(ctx, warned, parameters =>
        {
            parameters.Add(p => p.OnCheckSignIn, EventCallback.Factory.Create(this, () => checks++));
            parameters.Add(p => p.CheckingSignIn, true);
        });
        Assert.True(busy.Find("button.roster-signin-check").HasAttribute("disabled"));
        Assert.Equal("Checking…", busy.Find("button.roster-signin-check").TextContent.Trim());
        Assert.Empty(RenderIt(ctx, Plain).FindAll(".roster-signin"));
    }
}
