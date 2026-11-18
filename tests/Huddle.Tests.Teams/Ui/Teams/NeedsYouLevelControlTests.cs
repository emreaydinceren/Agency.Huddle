using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the Needs You level control that Settings, the Team page's Process tab and a Project's dialog share (ADR-0088): what a scope sets and
/// inherits, what is in force, saving through the setters, the refusal of a bad cap, and the cap's Yes/No question.
/// </summary>
public sealed class NeedsYouLevelControlTests
{
    private const string Run = "Run inside the Team";

    private const string Limits =
        "What this cannot catch: Huddle asks before a merge or push it can see in an Agent's command, so a script or a git alias saved earlier in a git config file can still merge or push without asking. "
        + "An Agent can also create a Task straight into a project set to Run inside the Team, and one Accept on a push Task lets the Team push anything for up to 24 hours. "
        + "For a guarantee, protect the branch on your git server.";

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, string? team = null, string? project = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<NeedsYouLevelControl>(0);
            if (team is not null)
            {
                builder.AddAttribute(1, nameof(NeedsYouLevelControl.Team), team);
            }

            if (project is not null)
            {
                builder.AddAttribute(2, nameof(NeedsYouLevelControl.Project), project);
            }

            builder.CloseComponent();
        });

    private static async Task ChooseAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        IElement item = cut.FindAll(".needs-you-level-choice .mud-toggle-item").First(item => string.Equals(item.TextContent.Trim(), label, StringComparison.Ordinal));
        await item.ClickAsync(new());
    }

    private static Task TypeCapAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.InvokeAsync(() => cut.Find(".needs-you-level-cap input").InputAsync(new ChangeEventArgs { Value = text }));

    private static Task SaveAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(() => cut.Find(".needs-you-level-save").ClickAsync(new()));

    private static string Effective(IRenderedComponent<ContainerFragment> cut) =>
        string.Join(' ', cut.Find(".needs-you-level-effective").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A Team that sets nothing shows the level it inherits from Settings, and Inherit is the chosen segment.</summary>
    [Fact]
    public async Task Team_SetsNothing_ShowsTheInheritedLevelFromSettings()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx, globalLevel: "run-inside-team");

        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);

        Assert.Equal("Inherits Run inside the Team from Settings.", Effective(cut));
        Assert.Equal("Inherit", cut.Find(".needs-you-level-choice .mud-toggle-item.segmented-active-item").TextContent.Trim());
    }

    /// <summary>Choosing Run inside the Team with a cap and pressing Save saves both in team.db and shows the level in force.</summary>
    [Fact]
    public async Task Team_ChooseRunWithCap_SaveStoresLevelAndCap()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);

        await cut.InvokeAsync(() => ChooseAsync(cut, Run));
        await TypeCapAsync(cut, "5");
        await SaveAsync(cut);

        Assert.Equal((NeedsYouLevel.RunInsideTheTeam, 5m), rig.Levels.Own(NeedsYouLevelUiRig.Team, null));
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.RunInsideTheTeam, 5m), rig.Store.Get(NeedsYouCaps.TeamKey(rig.Ids.TeamId(NeedsYouLevelUiRig.Team))));
        cut.WaitForAssertion(() => Assert.Equal("In force: Run inside the Team.", Effective(cut)));
        Assert.Equal("Run inside the Team", cut.Find(".needs-you-level-choice .mud-toggle-item.segmented-active-item").TextContent.Trim());
        Assert.Equal("5", cut.Find(".needs-you-level-cap input").GetAttribute("value"));
    }

    /// <summary>A cap that is not a number above zero is refused under the field and nothing is written.</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task Team_InvalidCap_IsRefusedInlineAndNothingIsWritten(string cap)
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);
        await cut.InvokeAsync(() => ChooseAsync(cut, Run));

        await TypeCapAsync(cut, cap);
        await SaveAsync(cut);

        Assert.Equal("Enter an amount above 0, such as 5 or 2.50, or leave it empty for no cap.", cut.Find(".needs-you-level-cap .mud-input-helper-text").TextContent.Trim());
        Assert.Equal((null, null), rig.Levels.Own(NeedsYouLevelUiRig.Team, null));
        Assert.Null(rig.Ids.FindTeamId(NeedsYouLevelUiRig.Team));
    }

    /// <summary>Choosing Inherit again clears the Team's own level and cap.</summary>
    [Fact]
    public async Task Team_ChooseInherit_SaveClearsTheTeamLevel()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        Assert.True(rig.Levels.SetTeam(NeedsYouLevelUiRig.Team, NeedsYouLevel.RunInsideTheTeam, 5m));
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);

        await cut.InvokeAsync(() => ChooseAsync(cut, "Inherit"));
        await SaveAsync(cut);

        Assert.Equal((null, null), rig.Levels.Own(NeedsYouLevelUiRig.Team, null));
        cut.WaitForAssertion(() => Assert.Equal("Inherits Ask me from Settings.", Effective(cut)));
    }

    /// <summary>A Project that sets nothing shows the level it inherits from its Team, and saving stores the Project's own level.</summary>
    [Fact]
    public async Task Project_InheritsTheTeamLevel_AndSavesItsOwn()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        Assert.True(rig.Levels.SetTeam(NeedsYouLevelUiRig.Team, NeedsYouLevel.RunInsideTheTeam, null));
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team, NeedsYouLevelUiRig.Project);
        Assert.Equal("Inherits Run inside the Team from the Team.", Effective(cut));

        await cut.InvokeAsync(() => ChooseAsync(cut, "Ask me"));
        await SaveAsync(cut);

        Assert.Equal((NeedsYouLevel.AskMe, (decimal?)null), rig.Levels.Own(NeedsYouLevelUiRig.Team, NeedsYouLevelUiRig.Project));
        cut.WaitForAssertion(() => Assert.Equal("In force: Ask me.", Effective(cut)));
    }

    /// <summary>On Settings the global level is saved in the store, which wins over the configuration from then on.</summary>
    [Fact]
    public async Task Global_ChooseRun_SaveWritesTheStore()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => ChooseAsync(cut, Run));
        await TypeCapAsync(cut, "2.5");
        await SaveAsync(cut);

        SavedNeedsYouLevel? saved = rig.Store.Get(NeedsYouCaps.GlobalKey);
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, saved?.Level);
        Assert.Equal(2.5m, saved?.SpendCap);
    }

    /// <summary>The note under Run inside the Team says what still waits and what Huddle only asks about when it sees it, so the copy promises no more than the code does.</summary>
    [Fact]
    public async Task Global_RunInsideTheTeam_NamesWhatStillWaits()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => ChooseAsync(cut, Run));

        Assert.Equal(
            "Run inside the Team: a local merge into main of a Task's own branch once its Proof is complete, a Sprint and new Backlog Tasks go ahead without you. Every Task with an Action (send, pay, publish, sign, delete) and every connector call still wait for you, and Huddle still asks you before every push it sees in an Agent's command.",
            cut.Find(".needs-you-level-note").TextContent.Trim());
        Assert.Equal("When the Team has spent this much running inside the Team, it asks you before going on.", cut.Find(".needs-you-level-cap .mud-input-helper-text").TextContent.Trim());
    }

    /// <summary>A Team at its cap shows "Cap reached: asking you" and the question; Yes lets it spend one more cap and the card goes.</summary>
    [Fact]
    public async Task Team_CapReached_ShowsTheQuestion_AndYesGrantsAndRemovesIt()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.OpenTheTeamQuestion();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);
        Assert.Equal("Cap reached: asking you. Dev ran inside the Team up to its cap, so it asks you as Ask me does until you answer or change the cap.", Effective(cut));
        Assert.Equal("Let it spend another 5 USD", cut.Find(".needs-you-cap-grant").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".needs-you-cap-grant").ClickAsync(new()));

        Assert.Empty(rig.Caps.Pending);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-cap-ask")));
        Assert.False(rig.Levels.For(NeedsYouLevelUiRig.Team, null).CapReached);
    }

    /// <summary>No keeps the scope at Ask me: the question is answered and gone, and the level stays at the cap.</summary>
    [Fact]
    public async Task Team_CapReached_NoDeclinesAndRemovesTheQuestion()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.OpenTheTeamQuestion();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);

        await cut.InvokeAsync(() => cut.Find(".needs-you-cap-decline").ClickAsync(new()));

        Assert.Empty(rig.Caps.Pending);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-cap-ask")));
        Assert.True(rig.Levels.For(NeedsYouLevelUiRig.Team, null).CapReached);
    }

    /// <summary>The Settings control names what the gate cannot catch under Ask me, so the Human is not told every push is held.</summary>
    [Fact]
    public async Task Global_AskMe_ShowsWhatTheGateCannotCatch()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal(Limits, cut.Find(".needs-you-level-limits").TextContent.Trim());
    }

    /// <summary>A Team set to Run inside the Team still shows what the gate cannot catch.</summary>
    [Fact]
    public async Task Team_RunInsideTheTeam_ShowsWhatTheGateCannotCatch()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        Assert.True(rig.Levels.SetTeam(NeedsYouLevelUiRig.Team, NeedsYouLevel.RunInsideTheTeam, null));

        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);

        Assert.Equal(Limits, cut.Find(".needs-you-level-limits").TextContent.Trim());
    }

    /// <summary>A project's control shows what the gate cannot catch.</summary>
    [Fact]
    public async Task Project_ShowsWhatTheGateCannotCatch()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team, NeedsYouLevelUiRig.Project);

        Assert.Equal(Limits, cut.Find(".needs-you-level-limits").TextContent.Trim());
    }

    /// <summary>A control for a Team that does not exist, which cannot set a level, still shows what the gate cannot catch.</summary>
    [Fact]
    public async Task UnknownTeam_ControlUnavailable_StillShowsWhatTheGateCannotCatch()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, "No Such Team");

        Assert.NotEmpty(cut.FindAll(".needs-you-level-unavailable"));
        Assert.Equal(Limits, cut.Find(".needs-you-level-limits").TextContent.Trim());
    }

    /// <summary>A change made elsewhere repaints the control on <c>NeedsYouLevels.Changed</c>.</summary>
    [Fact]
    public async Task Control_LevelChangedElsewhere_Repaints()
    {
        await using MudBunitContext ctx = new();
        using NeedsYouLevelUiRig rig = new(ctx);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, NeedsYouLevelUiRig.Team);
        Assert.Equal("Inherits Ask me from Settings.", Effective(cut));

        rig.Levels.SetGlobal(NeedsYouLevel.RunInsideTheTeam, null);

        cut.WaitForAssertion(() => Assert.Equal("Inherits Run inside the Team from Settings.", Effective(cut)));
    }
}
