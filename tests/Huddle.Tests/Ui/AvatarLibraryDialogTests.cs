using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.Tests.Avatars;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the Avatar Library picker (Spec §6.5, §6.6 and §6.9): a modal <c>MudDialog</c> with a skin switch, a
/// debounced text box, two static chip clouds whose selected chips float left with an X, a shown-of-total count
/// line with Clear all, and a paged grid of text-free thumbnail buttons. It is driven by
/// <see cref="FakeAvatarLibraryLoader"/>'s 120 records (see that class for the exact fixture).
/// <para>
/// Markup contract: <c>.avatar-library-filters</c> (never scrolls) holds <c>button.avatar-library-skin</c> x4,
/// <c>.avatar-library-search</c> (a <c>MudTextField&lt;string&gt;</c>), <c>.avatar-library-professions</c> and
/// <c>.avatar-library-tags</c> (each holding <c>button.avatar-library-chip</c> with <c>.avatar-library-chip-text</c>;
/// a selected chip has <c>aria-pressed="true"</c>, two <c>.avatar-library-chip-bracket</c> and one
/// <c>.avatar-library-chip-close</c>), <c>.avatar-library-tags-all</c> (toggle: Show all / Show fewer),
/// <c>.avatar-library-count</c> and <c>.avatar-library-clear</c>. <c>.avatar-library-grid</c> (the only scroller)
/// holds <c>button.avatar-library-thumb</c> and <c>.avatar-library-more</c>. Messages: <c>.avatar-library-error</c>
/// (+ <c>-hint</c>) and <c>.avatar-library-empty</c> (+ <c>-hint</c>).
/// </para>
/// </summary>
public sealed class AvatarLibraryDialogTests
{
    private const string Thumb = "button.avatar-library-thumb";

    private const string Skin = "button.avatar-library-skin";

    private const string Professions = ".avatar-library-professions";

    private const string Tags = ".avatar-library-tags";

    private const string Count = ".avatar-library-count";

    private const string Clear = ".avatar-library-clear";

    private const string More = ".avatar-library-more";

    private const string TagsAll = ".avatar-library-tags-all";

    private const string SearchInput = ".avatar-library-search input";

    private static readonly string[] AlphabeticalProfessions = ["Accountant", "Artist", "Butler", "Chef", "Nurse", "Pilot"];

    /// <summary>It opens as a modal dialog titled "Choose an avatar" with the shared options, and no drawer exists.</summary>
    [Fact]
    public async Task Opens_AsDialog_NotDrawer()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.Single(harness.Cut.FindAll(".mud-dialog"));
        Assert.Empty(harness.Cut.FindAll(".mud-drawer"));
        Assert.Equal("Choose an avatar", harness.Cut.Find(".mud-dialog-title").TextContent.Trim());
        DialogOptions options = Agency.Huddle.App.Components.Shared.AvatarLibraryDialog.Options;
        Assert.True(options.FullWidth);
        Assert.Equal(MaxWidth.Large, options.MaxWidth);
        Assert.True(options.CloseButton);
        Assert.True(options.CloseOnEscapeKey);
        Assert.True(options.BackdropClick);
    }

    /// <summary>It shows the first 48 of 120, Show more adds 48 each press until it vanishes at 120.</summary>
    [Fact]
    public async Task ShowsFirst48_AndShowMoreAdds48()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.Equal(48, harness.Thumbs.Count);
        Assert.Equal("48 of 120", harness.CountText);
        Assert.Equal("Show more", harness.Cut.Find(More).TextContent.Trim());

        await harness.ClickAsync(More);
        Assert.Equal(96, harness.Thumbs.Count);
        Assert.Equal("96 of 120", harness.CountText);

        await harness.ClickAsync(More);
        Assert.Equal(120, harness.Thumbs.Count);
        Assert.Equal("120 of 120", harness.CountText);
        Assert.Empty(harness.Cut.FindAll(More));
    }

    /// <summary>The skin switch has All/Light/Tan/Dark with aria-pressed; Dark shows 24, Light (fair+light) 48, and any change returns to the first 48.</summary>
    [Fact]
    public async Task SkinSwitch_FiltersGrid()
    {
        await using Harness harness = await Harness.OpenAsync();
        Assert.Equal(["All", "Light", "Tan", "Dark"], [.. harness.Cut.FindAll(Skin).Select(static b => b.TextContent.Trim())]);
        Assert.Equal("true", harness.SkinButton("All").GetAttribute("aria-pressed"));

        await harness.ClickAsync(More);
        Assert.Equal(96, harness.Thumbs.Count);

        await harness.Cut.InvokeAsync(() => harness.SkinButton("Dark").ClickAsync());
        Assert.Equal(24, harness.Thumbs.Count);
        Assert.Equal("24 of 120", harness.CountText);
        Assert.Equal("true", harness.SkinButton("Dark").GetAttribute("aria-pressed"));
        Assert.Equal("false", harness.SkinButton("All").GetAttribute("aria-pressed"));
        Assert.All(harness.Thumbs, static t => Assert.StartsWith("dark ", t.GetAttribute("aria-label"), StringComparison.Ordinal));

        await harness.Cut.InvokeAsync(() => harness.SkinButton("Light").ClickAsync());
        Assert.Equal(48, harness.Thumbs.Count);
        Assert.All(harness.Thumbs, static t => Assert.Matches("^(fair|light) ", t.GetAttribute("aria-label")));

        await harness.Cut.InvokeAsync(() => harness.SkinButton("All").ClickAsync());
        Assert.Equal(48, harness.Thumbs.Count);
        Assert.Equal("48 of 120", harness.CountText);
    }

    /// <summary>Typing "butl" narrows the grid to the 20 Butlers.</summary>
    [Fact]
    public async Task TextBox_FiltersGrid()
    {
        await using Harness harness = await Harness.OpenAsync();

        await harness.TypeAsync("butl");

        Assert.Equal(20, harness.Thumbs.Count);
        Assert.Equal("20 of 120", harness.CountText);
        Assert.All(harness.Thumbs, static t => Assert.EndsWith(" Butler", t.GetAttribute("aria-label"), StringComparison.Ordinal));
    }

    /// <summary>Pressing a profession chip floats it to the far left, pressed and with a close X, narrows the grid, and moves no other chip.</summary>
    [Fact]
    public async Task ProfessionChip_Selected_FloatsLeftWithClose()
    {
        await using Harness harness = await Harness.OpenAsync();
        Assert.Equal(AlphabeticalProfessions, harness.ChipTexts(Professions));

        await harness.PressChipAsync(Professions, "Chef");

        Assert.Equal(["Chef", "Accountant", "Artist", "Butler", "Nurse", "Pilot"], harness.ChipTexts(Professions));
        IElement first = harness.Chips(Professions)[0];
        Assert.Equal("true", first.GetAttribute("aria-pressed"));
        Assert.NotNull(first.QuerySelector(".avatar-library-chip-close"));
        Assert.Equal("20 of 120", harness.CountText);
        Assert.Single(harness.Chips(Professions), static c => c.QuerySelector(".avatar-library-chip-close") is not null);
    }

    /// <summary>Pressing the selected chip's X deselects it: the cloud returns to alphabetical order and the grid to 48 of 120.</summary>
    [Fact]
    public async Task ProfessionChip_Close_RestoresOrder()
    {
        await using Harness harness = await Harness.OpenAsync();
        await harness.PressChipAsync(Professions, "Chef");

        IElement close = Assert.IsAssignableFrom<IElement>(harness.Chips(Professions)[0].QuerySelector(".avatar-library-chip-close"));
        await harness.Cut.InvokeAsync(() => close.ClickAsync());

        Assert.Equal(AlphabeticalProfessions, harness.ChipTexts(Professions));
        Assert.Equal("48 of 120", harness.CountText);
        Assert.Empty(harness.Cut.FindAll(Professions + " .avatar-library-chip-close"));
    }

    /// <summary>Pressing a tag chip floats it left with a close X, keeps the rest in order and narrows the grid to the 3 records carrying it.</summary>
    [Fact]
    public async Task TagChip_Selected_FloatsLeftWithClose()
    {
        await using Harness harness = await Harness.OpenAsync();
        IReadOnlyList<string> before = harness.ChipTexts(Tags);

        await harness.PressChipAsync(Tags, "tag05");

        IReadOnlyList<string> after = harness.ChipTexts(Tags);
        Assert.Equal("tag05", after[0]);
        Assert.Equal(before.Where(static t => t != "tag05"), after.Skip(1));
        Assert.Equal("true", harness.Chips(Tags)[0].GetAttribute("aria-pressed"));
        Assert.NotNull(harness.Chips(Tags)[0].QuerySelector(".avatar-library-chip-close"));
        Assert.Equal("3 of 120", harness.CountText);
    }

    /// <summary>The tag cloud shows its first 40 pool entries (glasses and vest lead) and Show all reveals all 52.</summary>
    [Fact]
    public async Task TagCloud_CappedAtForty_ShowAllReveals()
    {
        await using Harness harness = await Harness.OpenAsync();

        IReadOnlyList<string> capped = harness.ChipTexts(Tags);
        Assert.Equal(40, capped.Count);
        Assert.Equal(["glasses", "vest", "tag00"], [.. capped.Take(3)]);
        Assert.DoesNotContain("tag49", capped);
        Assert.Equal("Show all", harness.Cut.Find(TagsAll).TextContent.Trim());

        await harness.ClickAsync(TagsAll);

        Assert.Equal(52, harness.ChipTexts(Tags).Count);
        Assert.Contains("tag49", harness.ChipTexts(Tags));
    }

    /// <summary>A tag selected from beyond the cap stays visible, leading the cloud, after the cloud is collapsed again; excluded tags never appear.</summary>
    [Fact]
    public async Task SelectedTag_StaysVisibleBeyondCap()
    {
        await using Harness harness = await Harness.OpenAsync();
        await harness.ClickAsync(TagsAll);
        await harness.PressChipAsync(Tags, "tag49");

        await harness.ClickAsync(TagsAll);

        IReadOnlyList<string> texts = harness.ChipTexts(Tags);
        Assert.Equal(41, texts.Count);
        Assert.Equal("tag49", texts[0]);
        Assert.Single(texts, static t => t == "tag49");
        Assert.Equal("Show all", harness.Cut.Find(TagsAll).TextContent.Trim());
        Assert.DoesNotContain(texts, static t => t == "man" || t == "woman" || t.StartsWith("bg-", StringComparison.Ordinal) || t.StartsWith("skin-", StringComparison.Ordinal));
    }

    /// <summary>Clear all drops the skin, the text and every selected chip, returning to the opening view.</summary>
    [Fact]
    public async Task ClearAll_ResetsEverything()
    {
        await using Harness harness = await Harness.OpenAsync();
        await harness.Cut.InvokeAsync(() => harness.SkinButton("Dark").ClickAsync());
        await harness.TypeAsync("chef");
        await harness.PressChipAsync(Tags, "glasses");
        await harness.PressChipAsync(Professions, "Chef");
        Assert.Equal("4 of 120", harness.CountText);

        await harness.ClickAsync(Clear);

        harness.Cut.WaitForAssertion(() => Assert.Equal("48 of 120", harness.CountText), TimeSpan.FromSeconds(5));
        Assert.Equal("true", harness.SkinButton("All").GetAttribute("aria-pressed"));
        Assert.Equal(string.Empty, harness.Cut.Find(SearchInput).GetAttribute("value") ?? string.Empty);
        Assert.Empty(harness.Cut.FindAll("button.avatar-library-chip[aria-pressed=true]"));
        Assert.Equal(AlphabeticalProfessions, harness.ChipTexts(Professions));
    }

    /// <summary>A text matching nothing shows "No avatars match." and 0 of 120.</summary>
    [Fact]
    public async Task NoMatch_ShowsEmptyState()
    {
        await using Harness harness = await Harness.OpenAsync();

        await harness.TypeAsync("zzzz");

        Assert.Equal("No avatars match.", harness.Cut.Find(".avatar-library-empty").TextContent.Trim());
        Assert.Equal("0 of 120", harness.CountText);
        Assert.Empty(harness.Thumbs);
        Assert.Empty(harness.Cut.FindAll(More));
    }

    /// <summary>The first thumbnail's aria-label is the raw skin tone, gender and profession: "fair man Accountant"; the second "light woman Artist".</summary>
    [Fact]
    public async Task Thumbnail_HasAriaLabel_RawSkinGenderProfession()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.Equal("fair man Accountant", harness.Thumbs[0].GetAttribute("aria-label"));
        Assert.Equal("light woman Artist", harness.Thumbs[1].GetAttribute("aria-label"));
        Assert.Equal("button", harness.Thumbs[0].GetAttribute("type"));
        IElement image = Assert.IsAssignableFrom<IElement>(harness.Thumbs[0].QuerySelector("img"));
        Assert.Equal("/avatar-library/av-000.png", image.GetAttribute("src"));
        Assert.Equal(string.Empty, image.GetAttribute("alt"));
    }

    /// <summary>No thumbnail, nor anything inside one, has a title attribute, and no thumbnail has visible text.</summary>
    [Fact]
    public async Task Thumbnail_HasNoTitleOrVisibleText()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.All(harness.Thumbs, static t =>
        {
            Assert.False(t.HasAttribute("title"));
            Assert.Empty(t.QuerySelectorAll("[title]"));
            Assert.Equal(string.Empty, t.TextContent.Trim());
        });
        Assert.Empty(harness.Cut.FindAll(".avatar-library-grid [title]"));
    }

    /// <summary>Clicking a thumbnail closes the dialog with Ok(file name).</summary>
    [Fact]
    public async Task Click_ClosesWithFileName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Harness harness = await Harness.OpenAsync();

        await harness.Cut.InvokeAsync(() => harness.Thumbs[3].ClickAsync());

        DialogResult? result = await harness.Reference.Result.WaitAsync(TimeSpan.FromSeconds(5), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal("av-003.png", Assert.IsType<string>(result.Data));
    }

    /// <summary>An unreadable index shows the message and no filters, count or grid.</summary>
    [Fact]
    public async Task Unreadable_ShowsMessage()
    {
        await using Harness harness = await Harness.OpenAsync(new FakeAvatarLibraryLoader(unreadable: true));

        Assert.Equal("The avatar library could not be read.", harness.Cut.Find(".avatar-library-error").TextContent.Trim());
        Assert.Empty(harness.Cut.FindAll(Skin));
        Assert.Empty(harness.Cut.FindAll(".avatar-library-search"));
        Assert.Empty(harness.Cut.FindAll("button.avatar-library-chip"));
        Assert.Empty(harness.Cut.FindAll(Count));
        Assert.Empty(harness.Cut.FindAll(Clear));
        Assert.Empty(harness.Cut.FindAll(Thumb));
    }

    /// <summary>The text box debounces by 200 ms and still filters once the pause has passed.</summary>
    [Fact]
    public async Task Search_DebouncesTwoHundredMilliseconds()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.Equal(200d, harness.Cut.FindComponent<MudTextField<string>>().Instance.DebounceInterval);
        await harness.TypeAsync("butl");

        Assert.Equal(20, harness.Thumbs.Count);
    }

    /// <summary>Escape in the text box clears it without closing; a second Escape closes the dialog with Cancel.</summary>
    [Fact]
    public async Task SearchEscape_ClearsThenCloses()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Harness harness = await Harness.OpenAsync();
        await harness.TypeAsync("butl");

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(SearchInput).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        harness.Cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(string.Empty, harness.Cut.Find(SearchInput).GetAttribute("value") ?? string.Empty);
                Assert.Equal("48 of 120", harness.CountText);
            },
            TimeSpan.FromSeconds(5));
        Assert.False(harness.Reference.Result.IsCompleted);

        await harness.Cut.InvokeAsync(() => harness.Cut.Find(SearchInput).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        DialogResult? result = await harness.Reference.Result.WaitAsync(TimeSpan.FromSeconds(5), ct);
        Assert.NotNull(result);
        Assert.True(result.Canceled);
    }

    /// <summary>All the filter controls live in the non-scrolling filters block; only thumbnails and Show more live in the scrolling grid.</summary>
    [Fact]
    public async Task Filters_AreOutsideTheScrollingGrid()
    {
        await using Harness harness = await Harness.OpenAsync();

        IElement grid = harness.Cut.Find(".avatar-library-grid");
        IElement filters = harness.Cut.Find(".avatar-library-filters");
        Assert.Null(grid.QuerySelector(".avatar-library-skin, .avatar-library-search, .avatar-library-chip, .avatar-library-count, .avatar-library-clear, .avatar-library-tags-all"));
        Assert.Equal(4, filters.QuerySelectorAll(Skin).Length);
        Assert.NotNull(filters.QuerySelector(".avatar-library-search"));
        Assert.NotNull(filters.QuerySelector(Professions));
        Assert.NotNull(filters.QuerySelector(Tags));
        Assert.NotNull(filters.QuerySelector(Count));
        Assert.NotNull(filters.QuerySelector(Clear));
        Assert.Equal(48, grid.QuerySelectorAll(Thumb).Length);
        Assert.Empty(filters.QuerySelectorAll(Thumb));
    }

    /// <summary>A selected chip carries its state in the bracket, the X and aria-pressed, and the pressed skin button in aria-pressed, not colour alone.</summary>
    [Fact]
    public async Task SelectedState_IsNotColourAlone()
    {
        await using Harness harness = await Harness.OpenAsync();
        await harness.PressChipAsync(Professions, "Butler");

        IElement chip = harness.Chips(Professions)[0];
        Assert.Equal("true", chip.GetAttribute("aria-pressed"));
        Assert.Equal(2, chip.QuerySelectorAll(".avatar-library-chip-bracket").Length);
        Assert.Single(chip.QuerySelectorAll(".avatar-library-chip-close"));
        Assert.All(harness.Chips(Professions).Skip(1), static c =>
        {
            Assert.Equal("false", c.GetAttribute("aria-pressed"));
            Assert.Empty(c.QuerySelectorAll(".avatar-library-chip-bracket, .avatar-library-chip-close"));
        });
        Assert.Equal("true", harness.SkinButton("All").GetAttribute("aria-pressed"));
        Assert.Equal("false", harness.SkinButton("Tan").GetAttribute("aria-pressed"));
    }

    /// <summary>Pressing a chip in either cloud moves only that chip: every other chip keeps its relative order and none disappears.</summary>
    [Fact]
    public async Task Chip_Press_NeverMovesAnotherChip()
    {
        await using Harness harness = await Harness.OpenAsync();
        IReadOnlyList<string> professionsBefore = harness.ChipTexts(Professions);
        IReadOnlyList<string> tagsBefore = harness.ChipTexts(Tags);

        await harness.PressChipAsync(Professions, "Pilot");
        await harness.PressChipAsync(Tags, "tag10");

        IReadOnlyList<string> professionsAfter = harness.ChipTexts(Professions);
        IReadOnlyList<string> tagsAfter = harness.ChipTexts(Tags);
        Assert.Equal(professionsBefore.Count, professionsAfter.Count);
        Assert.Equal(professionsBefore.Where(static t => t != "Pilot"), professionsAfter.Skip(1));
        Assert.Equal(tagsBefore.Count, tagsAfter.Count);
        Assert.Equal(tagsBefore.Where(static t => t != "tag10"), tagsAfter.Skip(1));
    }

    /// <summary>The dialog has no confirm button: no dialog actions and no button named OK, Select, Confirm, Choose, Save or Done.</summary>
    [Fact]
    public async Task Pick_NeedsNoConfirmButton()
    {
        await using Harness harness = await Harness.OpenAsync();

        Assert.Empty(harness.Cut.FindAll(".mud-dialog-actions button"));
        string[] confirmWords = ["OK", "Select", "Confirm", "Choose", "Save", "Done"];
        Assert.DoesNotContain(
            harness.Cut.FindAll(".mud-dialog button").Select(static b => b.TextContent.Trim()),
            text => confirmWords.Contains(text, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>With no match the chips and Clear all stay, and Clear all brings the grid back.</summary>
    [Fact]
    public async Task NoMatch_KeepsClearAll()
    {
        await using Harness harness = await Harness.OpenAsync();
        await harness.TypeAsync("zzzz");

        Assert.Equal("Clear all", harness.Cut.Find(Clear).TextContent.Trim());
        Assert.Equal(AlphabeticalProfessions, harness.ChipTexts(Professions));
        Assert.Equal(40, harness.ChipTexts(Tags).Count);

        await harness.ClickAsync(Clear);

        harness.Cut.WaitForAssertion(() => Assert.Equal("48 of 120", harness.CountText), TimeSpan.FromSeconds(5));
        Assert.Empty(harness.Cut.FindAll(".avatar-library-empty"));
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly MudBunitContext ctx;

        private Harness(MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, IDialogReference reference)
        {
            this.ctx = ctx;
            this.Cut = cut;
            this.Reference = reference;
        }

        /// <summary>The rendered dialog provider fragment.</summary>
        public IRenderedComponent<ContainerFragment> Cut { get; }

        /// <summary>The open dialog's reference.</summary>
        public IDialogReference Reference { get; }

        /// <summary>The thumbnail buttons currently in the grid.</summary>
        public IReadOnlyList<IElement> Thumbs => [.. this.Cut.FindAll(Thumb)];

        /// <summary>The trimmed text of the count line.</summary>
        public string CountText => this.Cut.Find(Count).TextContent.Trim();

        /// <summary>Opens the dialog through <see cref="IDialogService"/> with the shared options and waits for the first content.</summary>
        public static async Task<Harness> OpenAsync(FakeAvatarLibraryLoader? loader = null)
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<IAvatarLibraryLoader>(loader ?? new FakeAvatarLibraryLoader());
            IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
            IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();

            IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<Agency.Huddle.App.Components.Shared.AvatarLibraryDialog>(null, new DialogParameters(),Agency.Huddle.App.Components.Shared.AvatarLibraryDialog.Options));
            cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 0 || cut.FindAll(".avatar-library-error").Count > 0), TimeSpan.FromSeconds(5));
            return new Harness(ctx, cut, reference);
        }

        /// <summary>The chip buttons of one cloud, in display order.</summary>
        public IReadOnlyList<IElement> Chips(string cloud) => [.. this.Cut.FindAll(cloud + " button.avatar-library-chip")];

        /// <summary>The chip labels of one cloud, in display order.</summary>
        public IReadOnlyList<string> ChipTexts(string cloud) =>
            [.. this.Chips(cloud).Select(static c => c.QuerySelector(".avatar-library-chip-text")?.TextContent.Trim() ?? string.Empty)];

        /// <summary>The skin switch button with the given label.</summary>
        public IElement SkinButton(string label) => this.Cut.FindAll(Skin).First(b => b.TextContent.Trim() == label);

        /// <summary>Clicks the element matching <paramref name="selector"/> and waits for the handler to finish.</summary>
        public Task ClickAsync(string selector) => this.Cut.InvokeAsync(() => this.Cut.Find(selector).ClickAsync());

        /// <summary>Presses the chip labelled <paramref name="text"/> in <paramref name="cloud"/>.</summary>
        public Task PressChipAsync(string cloud, string text) =>
            this.Cut.InvokeAsync(() => this.Chips(cloud).First(c => c.QuerySelector(".avatar-library-chip-text")?.TextContent.Trim() == text).ClickAsync());

        /// <summary>Types into the text box and waits (up to five seconds) until the debounced filter has been applied to the count line.</summary>
        public async Task TypeAsync(string text)
        {
            string before = this.CountText;
            await this.Cut.InvokeAsync(() => this.Cut.Find(SearchInput).InputAsync(text));
            this.Cut.WaitForAssertion(() => Assert.NotEqual(before, this.CountText), TimeSpan.FromSeconds(5));
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() => await this.ctx.DisposeAsync();
    }
}
