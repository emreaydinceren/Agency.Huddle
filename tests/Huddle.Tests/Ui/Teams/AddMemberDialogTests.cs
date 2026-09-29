using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.7 (Add member): the dialog behind the Members tab's Add button. It is shown through
/// <see cref="IDialogService"/> with a snapshot of the Team and of the candidates (Personas not yet in it).
/// Its <c>Teammate</c> autocomplete matches Name, Alias and Title ignoring case; the restart warning
/// (<c>.add-member-dialog-warning</c>, <c>role="status"</c>) appears only after a pick; <b>Add</b> is
/// disabled until a pick, calls <see cref="ITeamMembership.Add"/> once and closes with the Name; each
/// failing outcome keeps the dialog open with <c>.add-member-dialog-error</c> (<c>role="alert"</c>).
/// </summary>
public sealed class AddMemberDialogTests
{
    private const string Field = ".add-member-dialog-field";

    private const string AddButton = ".add-member-dialog-add-button";

    private const string CancelButton = ".add-member-dialog-cancel-button";

    private const string Warning = ".add-member-dialog-warning";

    private const string Error = ".add-member-dialog-error";

    private static readonly TeamSummary Business = new("Business", ["Marketing"], ["Iris"], true);

    private static readonly IReadOnlyList<TeammateChoice> Candidates =
    [
        new("Kim", "Editor", "Scribe"),
        new("Nova", "Researcher", "N"),
    ];

    /// <summary>The title the caller passes is the dialog's title, the field is labelled "Teammate", Add starts disabled and neither the warning nor an error shows.</summary>
    [Fact]
    public async Task Initially_HasTitleAndLabel_AddDisabled_NoWarning_NoError()
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        Assert.Equal("Add member to Business", harness.Cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Teammate", harness.Cut.Find(Field + " label").TextContent.Trim());
        Assert.True(harness.Cut.Find(AddButton).HasAttribute("disabled"));
        Assert.Equal("Add", harness.Cut.Find(AddButton).TextContent.Trim());
        Assert.Equal("Cancel", harness.Cut.Find(CancelButton).TextContent.Trim());
        Assert.Empty(harness.Cut.FindAll(Warning));
        Assert.Empty(harness.Cut.FindAll(Error));
    }

    /// <summary>While the Team is still null the dialog renders no field and no buttons.</summary>
    [Fact]
    public async Task NullTeam_RendersNothing()
    {
        await using Harness harness = await Harness.OpenAsync(null, Candidates);

        Assert.Empty(harness.Cut.FindAll(Field));
        Assert.Empty(harness.Cut.FindAll(AddButton));
        Assert.Empty(harness.Cut.FindAll(CancelButton));
    }

    /// <summary>The search matches the Name, ignoring case: "ki" finds only Kim.</summary>
    [Theory]
    [InlineData("ki")]
    [InlineData("KI")]
    public async Task Search_ByName_IgnoresCase(string term)
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        Assert.Equal(["Kim"], await harness.SearchNamesAsync(term));
    }

    /// <summary>The search matches the Alias: "scr" reaches Kim only through her Alias "Scribe".</summary>
    [Fact]
    public async Task Search_ByAlias_FindsTheTeammate()
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        Assert.Equal(["Kim"], await harness.SearchNamesAsync("scr"));
    }

    /// <summary>The search matches the Title, ignoring case: "edit" reaches Kim only through her Title "Editor", "RESEA" reaches Nova only through hers.</summary>
    [Theory]
    [InlineData("edit", "Kim")]
    [InlineData("RESEA", "Nova")]
    public async Task Search_ByTitle_FindsTheTeammate(string term, string expected)
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        Assert.Equal([expected], await harness.SearchNamesAsync(term));
    }

    /// <summary>A null or empty term returns every candidate, in the order given.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Search_NullOrEmptyTerm_ReturnsEveryCandidate(string? term)
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        Assert.Equal(["Kim", "Nova"], await harness.SearchNamesAsync(term));
    }

    /// <summary>A term matching nothing returns nothing.</summary>
    [Fact]
    public async Task Search_NoMatch_ReturnsNothing()
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        Assert.Empty(await harness.SearchNamesAsync("zzz"));
    }

    /// <summary>Typing text that is no Name without picking leaves Add disabled and shows no warning.</summary>
    [Fact]
    public async Task TypingFreeText_WithoutAPick_KeepsAddDisabled_AndShowsNoWarning()
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(Field + " input").InputAsync("zzz"));
        await harness.Cut.InvokeAsync(static () => { });

        Assert.True(harness.Cut.Find(AddButton).HasAttribute("disabled"));
        Assert.Empty(harness.Cut.FindAll(Warning));
    }

    /// <summary>After picking Kim the warning names Kim, is a status (not an alert) and Add is enabled.</summary>
    [Fact]
    public async Task PickingKim_ShowsTheRestartWarning_AndEnablesAdd()
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        await harness.PickAsync("ki");

        Assert.Equal("Adding Kim restarts it and clears its conversation memory.", harness.Cut.Find(Warning + " .mud-alert-message").TextContent);
        Assert.Equal("status", harness.Cut.Find(Warning).GetAttribute("role"));
        Assert.False(harness.Cut.Find(AddButton).HasAttribute("disabled"));
        Assert.Empty(harness.Cut.FindAll(Error));
    }

    /// <summary>The warning follows the pick: picking Nova names Nova, not Kim.</summary>
    [Fact]
    public async Task PickingNova_WarningNamesNova()
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);

        await harness.PickAsync("nova");

        Assert.Equal("Adding Nova restarts it and clears its conversation memory.", harness.Cut.Find(Warning + " .mud-alert-message").TextContent);
    }

    /// <summary>Add calls the membership service once with the Team and the Name, then closes with the Name.</summary>
    [Fact]
    public async Task Add_Added_CallsMembershipOnce_AndClosesWithTheName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);
        await harness.PickAsync("ki");

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(AddButton).ClickAsync());

        DialogResult? result = await harness.Reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal("Kim", Assert.IsType<string>(result.Data));
        Assert.Equal(["Add:Business/Kim"], harness.Membership.Calls);
    }

    /// <summary>Each failing outcome keeps the dialog open and shows its settled text in an alert (role="alert").</summary>
    [Theory]
    [InlineData(MembershipOutcome.AlreadyMember, null, "Kim is already in Business.")]
    [InlineData(MembershipOutcome.NotFound, null, "Kim no longer exists.")]
    [InlineData(MembershipOutcome.Rejected, "The persona file is read-only.", "The persona file is read-only.")]
    public async Task Add_FailingOutcome_KeepsTheDialogOpen_AndShowsTheError(MembershipOutcome outcome, string? problem, string expected)
    {
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);
        harness.Membership.MembershipResult = new MembershipResult(outcome, problem);
        await harness.PickAsync("ki");

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(AddButton).ClickAsync());

        Assert.Equal(expected, harness.Cut.Find(Error + " .mud-alert-message").TextContent);
        Assert.Equal("alert", harness.Cut.Find(Error).GetAttribute("role"));
        Assert.False(harness.Reference.Result.IsCompleted);
        Assert.Equal("Add member to Business", harness.Cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(["Add:Business/Kim"], harness.Membership.Calls);
    }

    /// <summary>Cancel closes the dialog with a cancelled result and never calls the membership service.</summary>
    [Fact]
    public async Task Cancel_ReturnsCancelled_WithoutCallingMembership()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Harness harness = await Harness.OpenAsync(Business, Candidates);
        await harness.PickAsync("ki");

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(CancelButton).ClickAsync());

        DialogResult? result = await harness.Reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.True(result.Canceled);
        Assert.Empty(harness.Membership.Calls);
    }

    /// <summary>The bUnit context, the dialog on screen and its fakes, disposed together.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly MudBunitContext ctx;
        private readonly TempDataDir dir;
        private readonly AvatarStore avatars;

        private Harness(MudBunitContext ctx, TempDataDir dir, AvatarStore avatars, FakeTeamMembership membership, IRenderedComponent<ContainerFragment> cut, IDialogReference reference)
        {
            this.ctx = ctx;
            this.dir = dir;
            this.avatars = avatars;
            this.Membership = membership;
            this.Cut = cut;
            this.Reference = reference;
        }

        /// <summary>The fake membership service the dialog was given.</summary>
        public FakeTeamMembership Membership { get; }

        /// <summary>The rendered dialog provider fragment.</summary>
        public IRenderedComponent<ContainerFragment> Cut { get; }

        /// <summary>The open dialog's reference.</summary>
        public IDialogReference Reference { get; }

        /// <summary>Shows the dialog through <see cref="IDialogService"/> the way the Members tab does and waits until it is on screen.</summary>
        public static async Task<Harness> OpenAsync(TeamSummary? team, IReadOnlyList<TeammateChoice> candidates)
        {
            MudBunitContext ctx = new();
            TempDataDir dir = new();
            AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
            FakeTeamMembership membership = new();
            ctx.Services.AddSingleton(avatars);
            ctx.Services.AddSingleton<ITeamMembership>(membership);
            IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
            IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
            DialogParameters<AddMemberDialog> parameters = new()
            {
                { x => x.Team, team },
                { x => x.Candidates, candidates },
            };

            IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<AddMemberDialog>("Add member to Business", parameters, new DialogOptions()));
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
            return new Harness(ctx, dir, avatars, membership, cut, reference);
        }

        /// <summary>Awaits the autocomplete's own <c>SearchFunc</c> for <paramref name="term"/> and returns the found Names in order.</summary>
        public async Task<string[]> SearchNamesAsync(string? term)
        {
            IEnumerable<TeammateChoice> found = await this.Search(term);
            return [.. found.Select(static choice => choice.Name)];
        }

        /// <summary>Searches for <paramref name="term"/> through the autocomplete's own <c>SearchFunc</c>, then invokes <c>ValueChanged</c> with the first result (never constructing a choice by hand).</summary>
        public async Task PickAsync(string term)
        {
            IRenderedComponent<MudAutocomplete<TeammateChoice>> autocomplete = this.Cut.FindComponent<MudAutocomplete<TeammateChoice>>();
            TeammateChoice first = (await this.Search(term)).First();
            await this.Cut.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(first));
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await this.ctx.DisposeAsync();
            this.avatars.Dispose();
            this.dir.Dispose();
        }

        private async Task<IEnumerable<TeammateChoice>> Search(string? term)
        {
            IRenderedComponent<MudAutocomplete<TeammateChoice>> autocomplete = this.Cut.FindComponent<MudAutocomplete<TeammateChoice>>();
            Func<string?, CancellationToken, Task<IEnumerable<TeammateChoice>>?> search = autocomplete.Instance.SearchFunc
                ?? throw new InvalidOperationException("Autocomplete has no SearchFunc.");
            Task<IEnumerable<TeammateChoice>>? searchTask = search(term, Xunit.TestContext.Current.CancellationToken);
            if (searchTask is null)
            {
                throw new InvalidOperationException("SearchFunc returned null.");
            }

            return await searchTask;
        }
    }
}
