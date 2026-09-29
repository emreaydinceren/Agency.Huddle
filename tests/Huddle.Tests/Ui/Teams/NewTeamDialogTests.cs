using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.5 (Creation): the name dialog behind "New team" and "New project". The dialog is
/// shown through <see cref="IDialogService"/> with a <see cref="NewTeamDialogMode"/>, the Team (Project
/// mode) and a snapshot of the Teams. Its problem text (the settled refusal from
/// <c>TeamNames.ValidateTeamName</c> / <c>ValidateProjectName</c>) renders in
/// <c>.new-team-dialog-problem</c>, which is absent while there is no problem; <b>Create</b> is disabled
/// synchronously after each keystroke and enabled only for a non-empty valid name; it closes with the
/// trimmed name, and Enter submits only when valid. The dialog validates the trimmed name.
/// </summary>
public sealed class NewTeamDialogTests
{
    private const string NameInput = ".new-team-dialog-name-field input";

    private const string CreateButton = ".new-team-dialog-create-button";

    private const string Problem = ".new-team-dialog-problem";

    private static readonly IReadOnlyList<TeamSummary> SampleTeams =
    [
        new("Business", ["Marketing"], ["Nova"], true),
        new("Sales", [], ["Iris"], true),
    ];

    /// <summary>Team mode: the title is the one the caller passed, the field is labelled "Team name", Create starts disabled and no problem is shown for the untouched field.</summary>
    [Fact]
    public async Task TeamMode_Initially_HasTitleAndLabel_CreateDisabled_NoProblem()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);

        Assert.Equal("New team", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Team name", cut.Find(".new-team-dialog-name-field label").TextContent.Trim());
        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(Problem));
    }

    /// <summary>Team mode: an existing Team (any case) and a comma each show their settled text and keep Create disabled.</summary>
    [Theory]
    [InlineData("Business", "A Team named \"Business\" already exists.")]
    [InlineData("business", "A Team named \"business\" already exists.")]
    [InlineData("Sales, EMEA", "A Team name can't contain commas, semicolons or square brackets.")]
    public async Task TeamMode_InvalidName_ShowsTheSettledText_AndKeepsCreateDisabled(string typed, string expected)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);

        await TypeAsync(cut, typed);

        Assert.Equal(expected, cut.Find(Problem).TextContent.Trim());
        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
    }

    /// <summary>Team mode: "memory" is a fine Team name (only a Project may not use it) and a fresh name enables Create with no problem shown.</summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("Research")]
    public async Task TeamMode_ValidName_EnablesCreate_WithNoProblem(string typed)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);

        await TypeAsync(cut, typed);

        Assert.False(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(Problem));
    }

    /// <summary>Clearing a valid name disables Create again, straight after the keystroke.</summary>
    [Fact]
    public async Task ClearingTheName_DisablesCreateAgain()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);
        await TypeAsync(cut, "Research");
        Assert.False(cut.Find(CreateButton).HasAttribute("disabled"));

        await TypeAsync(cut, string.Empty);

        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
    }

    /// <summary>Project mode: the title is the one the caller passed, the field is labelled "Project name" and Create starts disabled.</summary>
    [Fact]
    public async Task ProjectMode_Initially_HasTitleAndLabel_CreateDisabled_NoProblem()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "New project", NewTeamDialogMode.Project, "Business");

        Assert.Equal("New project", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Project name", cut.Find(".new-team-dialog-name-field label").TextContent.Trim());
        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(Problem));
    }

    /// <summary>Project mode: "memory" is reserved, and a name the Team already has (any case) shows the duplicate text naming the Team; both keep Create disabled.</summary>
    [Theory]
    [InlineData("memory", "\"memory\" is reserved for the Team's shared Memory.")]
    [InlineData("marketing", "A Project named \"marketing\" already exists in Business.")]
    public async Task ProjectMode_InvalidName_ShowsTheSettledText_AndKeepsCreateDisabled(string typed, string expected)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New project", NewTeamDialogMode.Project, "Business");

        await TypeAsync(cut, typed);

        Assert.Equal(expected, cut.Find(Problem).TextContent.Trim());
        Assert.True(cut.Find(CreateButton).HasAttribute("disabled"));
    }

    /// <summary>Project mode: a fresh name (with a space) enables Create with no problem shown, and a name only another Team has is fine.</summary>
    [Theory]
    [InlineData("Q4 Launch")]
    [InlineData("Notes")]
    public async Task ProjectMode_ValidName_EnablesCreate_WithNoProblem(string typed)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New project", NewTeamDialogMode.Project, "Business");

        await TypeAsync(cut, typed);

        Assert.False(cut.Find(CreateButton).HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(Problem));
    }

    /// <summary>Create closes the dialog with the trimmed name, in Team mode.</summary>
    [Fact]
    public async Task Create_TeamMode_ReturnsTheTrimmedName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);
        await TypeAsync(cut, "  Research  ");

        await cut.InvokeAsync(() => cut.Find(CreateButton).ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal("Research", Assert.IsType<string>(result.Data));
    }

    /// <summary>Create closes the dialog with the trimmed name, in Project mode.</summary>
    [Fact]
    public async Task Create_ProjectMode_ReturnsTheTrimmedName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New project", NewTeamDialogMode.Project, "Business");
        await TypeAsync(cut, " Q4 Launch ");

        await cut.InvokeAsync(() => cut.Find(CreateButton).ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal("Q4 Launch", Assert.IsType<string>(result.Data));
    }

    /// <summary>Enter in the name field submits a valid name, exactly as Create does.</summary>
    [Fact]
    public async Task Enter_WhenValid_Submits()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);
        await TypeAsync(cut, "Research");

        await cut.InvokeAsync(() => cut.Find(NameInput).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal("Research", Assert.IsType<string>(result.Data));
    }

    /// <summary>Enter on an invalid name (an existing Team) submits nothing: the dialog stays open with its problem showing.</summary>
    [Fact]
    public async Task Enter_WhenInvalid_DoesNotSubmit()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);
        await TypeAsync(cut, "Business");

        await cut.InvokeAsync(() => cut.Find(NameInput).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));
        await cut.InvokeAsync(static () => { });

        Assert.False(reference.Result.IsCompleted);
        Assert.Equal("New team", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("A Team named \"Business\" already exists.", cut.Find(Problem).TextContent.Trim());
    }

    /// <summary>Enter on an empty field submits nothing either.</summary>
    [Fact]
    public async Task Enter_WhenEmpty_DoesNotSubmit()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);

        await cut.InvokeAsync(() => cut.Find(NameInput).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));
        await cut.InvokeAsync(static () => { });

        Assert.False(reference.Result.IsCompleted);
        Assert.Equal("New team", cut.Find(".mud-dialog-title").TextContent.Trim());
    }

    /// <summary>Cancel closes the dialog with a cancelled result.</summary>
    [Fact]
    public async Task Cancel_ReturnsCancelled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", NewTeamDialogMode.Team, null);
        await TypeAsync(cut, "Research");

        await cut.InvokeAsync(() => cut.Find(".new-team-dialog-cancel-button").ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.True(result.Canceled);
    }

    /// <summary>Shows the dialog through <see cref="IDialogService"/> the way <c>TeamsNav</c> does and waits until it is on screen.</summary>
    private static async Task<IDialogReference> OpenAsync(MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, string title, NewTeamDialogMode mode, string? team)
    {
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<NewTeamDialog> parameters = new()
        {
            { x => x.Mode, mode },
            { x => x.Team, team },
            { x => x.Teams, SampleTeams },
        };

        IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<NewTeamDialog>(title, parameters, NewTeamDialog.Options));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        return reference;
    }

    /// <summary>Types <paramref name="text"/> into the name field (an <c>input</c> event, as <c>Immediate</c> listens to).</summary>
    private static Task TypeAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.InvokeAsync(() => cut.Find(NameInput).InputAsync(text));
}
