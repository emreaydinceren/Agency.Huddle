using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Appearance;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Tests for the Settings → Library panel (Task 13.4.t, Spec §6.10): the built-in roots'
/// hide-not-remove switch, adding and removing a pinned root, the existing-folder refusal and
/// trust warning the panel itself enforces (<see cref="LibraryRootStore.Save"/> does not check
/// either), and the Reset confirm dialog that deletes <c>library-roots.json</c>.
/// </summary>
public sealed class LibraryPanelTests
{
    /// <summary>The Library tab follows Skills in the Settings tab rail (Task 13.4.i's deliverable).</summary>
    [Fact]
    public async Task Tab_IsNamedLibrary_AfterSkills()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PromptStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<SkillStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<LibraryRootStore>());

        var cut = ctx.Render<Settings>();

        List<string> tabLabels = [.. cut.FindAll(".mud-tab").Select(e => e.TextContent.Trim())];
        Assert.Equal(["Prompts", "Appearance", "Personas", "Skills", "Library"], tabLabels);
    }

    /// <summary>The roots table lists the two built-ins first, then any pinned root, in
    /// <see cref="LibraryRootStore.Roots"/>'s own order.</summary>
    [Fact]
    public async Task Lists_BuiltInsThenPinned()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);

        Assert.Equal(
            ["Teams", "Teammates", "Docs"],
            cut.FindAll(".library-panel-root-row td:first-child").Select(e => e.TextContent.Trim()));
    }

    /// <summary>A built-in row carries a hide switch and never a Remove button; a pinned row is the opposite.</summary>
    [Fact]
    public async Task BuiltIns_HaveHideSwitch_NoRemove()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);

        var teamsRow = cut.Find(".library-panel-root-row[data-root-id='teams']");
        Assert.NotEmpty(teamsRow.QuerySelectorAll(".library-panel-hide-switch"));
        Assert.Empty(teamsRow.QuerySelectorAll(".library-panel-remove-button"));

        var pinnedRow = cut.Find(".library-panel-root-row[data-root-id='docs']");
        Assert.NotEmpty(pinnedRow.QuerySelectorAll(".library-panel-remove-button"));
        Assert.Empty(pinnedRow.QuerySelectorAll(".library-panel-hide-switch"));
    }

    /// <summary>Adding an existing folder saves it as a new pinned root, which then shows in the table.</summary>
    [Fact]
    public async Task Add_ValidFolder_Saves()
    {
        using PanelFixture fixture = PanelFixture.Build();
        string newPath = Path.Combine(fixture.DataDir, "Notes");
        Directory.CreateDirectory(newPath);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-name-field input").Change("Notes"));
        await cut.InvokeAsync(() => cut.Find(".library-panel-path-field input").Change(newPath));
        await cut.InvokeAsync(() => cut.Find(".library-panel-add-button").Click());

        Assert.Equal(["Notes"], fixture.Store.Pinned.Select(p => p.Name));
        cut.WaitForAssertion(() => Assert.Equal(
            ["Teams", "Teammates", "Notes"],
            cut.FindAll(".library-panel-root-row td:first-child").Select(e => e.TextContent.Trim())));
    }

    /// <summary>Adding a path that does not exist on disk is refused with the settled text, and nothing is saved.</summary>
    [Fact]
    public async Task Add_MissingFolder_Refused()
    {
        using PanelFixture fixture = PanelFixture.Build();
        string missingPath = Path.Combine(fixture.DataDir, "DoesNotExist");
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-name-field input").Change("Ghost"));
        await cut.InvokeAsync(() => cut.Find(".library-panel-path-field input").Change(missingPath));
        await cut.InvokeAsync(() => cut.Find(".library-panel-add-button").Click());

        Assert.Equal("That folder doesn't exist.", cut.Find(".library-panel-add-error").TextContent.Trim());
        Assert.Empty(fixture.Store.Pinned);
        Assert.False(File.Exists(fixture.Store.FilePath));
    }

    /// <summary>The panel always shows the Spec's exact trust-warning text.</summary>
    [Fact]
    public async Task Add_ShowsTrustWarning()
    {
        using PanelFixture fixture = PanelFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);

        Assert.Equal(
            "Anything in this folder can be read and changed from Huddle.",
            cut.Find(".library-panel-trust-warning").TextContent.Trim());
    }

    /// <summary>Removing a pinned root saves the remaining list and the row disappears.</summary>
    [Fact]
    public async Task Remove_Pinned_Saves()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='docs'] .library-panel-remove-button").Click());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-panel-root-row[data-root-id='docs']")));
        Assert.Empty(fixture.Store.Pinned);
    }

    /// <summary>Reset asks the settled confirm text through <c>ShowMessageBoxAsync</c>; confirming deletes <c>library-roots.json</c>.</summary>
    [Fact]
    public async Task Reset_AsksThenDeletesFile()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();
        string filePath = fixture.Store.FilePath;
        Assert.True(File.Exists(filePath));

        var cut = ctx.RenderWithPopovers(RenderPanel);
        _ = cut.InvokeAsync(() => cut.Find(".library-panel-reset-button").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Reset pinned folders?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Pinned folders go back to the configured list.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(
            ["Cancel", "Reset"],
            cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Reset", StringComparison.Ordinal)).Click());
        cut.WaitForAssertion(() => Assert.False(File.Exists(filePath)), TimeSpan.FromSeconds(5));
    }

    /// <summary>Cancelling Reset leaves <c>library-roots.json</c> untouched on disk.</summary>
    [Fact]
    public async Task Reset_Cancel_LeavesFileUntouched()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();
        string filePath = fixture.Store.FilePath;
        byte[] before = File.ReadAllBytes(filePath);

        var cut = ctx.RenderWithPopovers(RenderPanel);
        _ = cut.InvokeAsync(() => cut.Find(".library-panel-reset-button").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));

        Assert.True(File.Exists(filePath));
        Assert.Equal(before, File.ReadAllBytes(filePath));
    }

    /// <summary>Hiding a built-in root persists (a fresh store instance over the same disk sees it hidden), and it stays in the full root list rather than being removed.</summary>
    [Fact]
    public async Task HideBuiltIn_Persists_NotRemoved()
    {
        using PanelFixture fixture = PanelFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='teams'] .library-panel-hide-switch input").Change(true));

        LibraryRootStore reloaded = new(fixture.Options, fixture.Paths, NullLogger<LibraryRootStore>.Instance);
        Assert.Equal(["teammates"], reloaded.VisibleRoots.Select(r => r.Id));
        Assert.Equal(["teams", "teammates"], reloaded.Roots.Select(r => r.Id));
    }

    /// <summary>The panel re-renders when <see cref="LibraryRootStore.RootsChanged"/> fires from outside it (e.g. another tab or window saving a pinned root).</summary>
    [Fact]
    public async Task Panel_RerendersOnRootsChanged()
    {
        using PanelFixture fixture = PanelFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        Assert.Empty(cut.FindAll(".library-panel-root-row[data-root-id='outside']"));

        string outsidePath = Path.Combine(fixture.DataDir, "Outside");
        Directory.CreateDirectory(outsidePath);
        fixture.Store.Save([new PinnedRootEntry("Outside", outsidePath)]);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-panel-root-row[data-root-id='outside']")));
    }

    private static void RenderPanel(RenderTreeBuilder builder)
    {
        builder.OpenComponent<LibraryPanel>(0);
        builder.CloseComponent();
    }

    private sealed class PanelFixture : IDisposable
    {
        private readonly TempDataDir dataDir;

        private PanelFixture(TempDataDir dataDir, IOptions<TeamOptions> options, TeammatePaths paths, LibraryRootStore store)
        {
            this.dataDir = dataDir;
            this.Options = options;
            this.Paths = paths;
            this.Store = store;
        }

        public IOptions<TeamOptions> Options { get; }

        public TeammatePaths Paths { get; }

        public LibraryRootStore Store { get; }

        public string DataDir => this.dataDir.Path;

        /// <summary>Builds a fixture, optionally saving one pinned root ("Docs" under <c>DataDir</c>) before the store is handed to the panel, so <c>library-roots.json</c> exists from the start.</summary>
        public static PanelFixture Build(string? pinnedName = null, string? pinnedPath = null, bool createPinned = false)
        {
            TempDataDir dataDir = new();
            IOptions<TeamOptions> options = dataDir.Options();
            TeammatePaths paths = new(options);
            LibraryRootStore store = new(options, paths, NullLogger<LibraryRootStore>.Instance);

            if (createPinned)
            {
                string path = pinnedPath ?? Path.Combine(dataDir.Path, "Docs");
                Directory.CreateDirectory(path);
                store.Save([new PinnedRootEntry(pinnedName ?? "Docs", path)]);
            }

            return new PanelFixture(dataDir, options, paths, store);
        }

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryRootStore"/> registered.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.Store);
            return ctx;
        }

        /// <summary>Disposes the underlying temp <c>DataDir</c>.</summary>
        public void Dispose() => this.dataDir.Dispose();
    }
}
