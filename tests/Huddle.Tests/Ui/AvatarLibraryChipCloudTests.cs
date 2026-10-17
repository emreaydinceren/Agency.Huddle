using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Avatars;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins U26 rows UX-R19: the Avatar Library renders its Professions and Tags through two shared
/// <see cref="ChipCloud"/>s drawing the Filter chip, not through its own chip markup, and <c>app.css</c> no longer
/// carries the duplicate avatar-library chip rules.
/// </summary>
public sealed class AvatarLibraryChipCloudTests
{
    private const string ProfessionsCloud = ".chip-cloud[aria-label='Professions']";

    private const string TagsCloud = ".chip-cloud[aria-label='Tags']";

    /// <summary>The wash is retired: no chip carries <c>is-quiet</c> and the parameter is gone; only the chip that is set carries <c>is-set</c>, the Filter chip's set state.</summary>
    [Fact]
    public async Task ChipCloud_HasNoQuietStyle_AndMarksOnlyTheSetChip()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipCloud> cloud = ctx.Render<ChipCloud>(p => p
            .Add(c => c.Title, "Tags")
            .Add(c => c.Pool, ["a", "b"])
            .Add(c => c.Selected, ["a"]));

        Assert.Null(typeof(ChipCloud).GetProperty("Quiet"));
        Assert.Empty(cloud.FindAll(".is-quiet"));
        Assert.Single(cloud.FindAll(".chip-cloud-chip.is-set"));
        Assert.Equal(2, cloud.FindAll(".chip-cloud-chip").Count);
    }

    /// <summary>The library shows two <see cref="ChipCloud"/>s titled Professions and Tags, and none of the old avatar-library chip markup.</summary>
    [Fact]
    public async Task Library_RendersTwoChipClouds()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.Equal(["Professions", "Tags"], harness.Cut.FindAll(".chip-cloud-title").Select(static t => t.TextContent.Trim()));
        Assert.Equal(2, harness.Cut.FindAll(".chip-cloud").Count);
        Assert.NotEmpty(harness.Cut.FindAll(ProfessionsCloud + " .chip-cloud-chip"));
        Assert.NotEmpty(harness.Cut.FindAll(TagsCloud + " .chip-cloud-chip"));
        Assert.Empty(harness.Cut.FindAll(".is-quiet"));
        Assert.Empty(harness.Cut.FindAll(".avatar-library-chip, .avatar-library-chip-text, .avatar-library-cloud, .avatar-library-cloud-title, .avatar-library-tags-all"));
    }

    /// <summary>Pressing a profession chip selects it: it moves first, reads <c>aria-pressed</c> true and gains the two brackets and the X.</summary>
    [Fact]
    public async Task Library_PressingAProfession_SelectsItWithBracketsAndClose()
    {
        await using Harness harness = await Harness.OpenAsync();

        await harness.Cut.InvokeAsync(() => harness.Chip(ProfessionsCloud, "Chef").ClickAsync());

        IElement first = harness.Cut.FindAll(ProfessionsCloud + " .chip-cloud-chip")[0];
        Assert.Equal("Chef", first.QuerySelector(".chip-cloud-chip-text")?.TextContent.Trim());
        Assert.Equal("true", first.GetAttribute("aria-pressed"));
        Assert.Equal(2, first.QuerySelectorAll(".chip-cloud-bracket").Length);
        Assert.Single(first.QuerySelectorAll(".chip-cloud-close"));
    }

    /// <summary>The Tags cloud is capped at 12 behind the shared "Show all" toggle, which expands and collapses it.</summary>
    [Fact]
    public async Task Library_TagsCloud_ShowAllTogglesTheCap()
    {
        await using Harness harness = await Harness.OpenAsync();
        string chips = TagsCloud + " .chip-cloud-chip";
        int capped = harness.Cut.FindAll(chips).Count;

        Assert.Equal("Show all", harness.Cut.Find(TagsCloud + " .chip-cloud-all").TextContent.Trim());

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(TagsCloud + " .chip-cloud-all").ClickAsync());

        Assert.True(harness.Cut.FindAll(chips).Count >= capped);
        Assert.Equal("Show fewer", harness.Cut.Find(TagsCloud + " .chip-cloud-all").TextContent.Trim());
    }

    /// <summary>UX-R19: <c>app.css</c> has no <c>.avatar-library-chip</c> rule left and no quiet chip: the cloud wears the Filter chip.</summary>
    [Fact]
    public void Stylesheet_HasNoDuplicateAvatarLibraryChipRules()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.DoesNotContain(".avatar-library-chip", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".avatar-library-cloud", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".avatar-library-tags-all", css, StringComparison.Ordinal);
        Assert.DoesNotContain("is-quiet", css, StringComparison.Ordinal);
        Assert.Matches(@"\.chip-cloud-chip\.is-set", css);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly MudBunitContext ctx;

        private Harness(MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut)
        {
            this.ctx = ctx;
            this.Cut = cut;
        }

        /// <summary>The rendered dialog provider fragment.</summary>
        public IRenderedComponent<ContainerFragment> Cut { get; }

        /// <summary>Opens the library through <see cref="IDialogService"/> and waits for the first chips.</summary>
        public static async Task<Harness> OpenAsync()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<IAvatarLibraryLoader>(new FakeAvatarLibraryLoader());
            IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
            IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();

            _ = await cut.InvokeAsync(() => dialogService.ShowAsync<AvatarLibraryDialog>(null, new DialogParameters(), AvatarLibraryDialog.Options));
            cut.WaitForAssertion(() => Assert.True(cut.FindAll(".avatar-library-thumb").Count > 0), TimeSpan.FromSeconds(5));
            return new Harness(ctx, cut);
        }

        /// <summary>The chip of <paramref name="cloud"/> whose text is <paramref name="text"/>.</summary>
        public IElement Chip(string cloud, string text) =>
            this.Cut.FindAll(cloud + " .chip-cloud-chip").First(c => c.QuerySelector(".chip-cloud-chip-text")?.TextContent.Trim() == text);

        /// <inheritdoc />
        public async ValueTask DisposeAsync() => await this.ctx.DisposeAsync();
    }
}
