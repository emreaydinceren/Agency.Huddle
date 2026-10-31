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
    /// <summary>The Library section follows Skills in the Settings section menu (Task 13.4.i's deliverable); Team Types (Task 30) sits between them.</summary>
    [Fact]
    public async Task Tab_IsNamedLibrary_AfterSkills()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PromptStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<SkillStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<LibraryRootStore>());
        ProfileTestServices.AddProfile(ctx.Services, factory);
        SkillsTestServices.AddFromFactory(ctx.Services, factory);

        var cut = ctx.Render<Settings>();

        List<string> tabLabels = [.. cut.FindAll(".settings-nav a.mud-nav-link").Select(e => e.TextContent.Trim())];
        Assert.Equal(["Profile", "Prompts", "Helpers", "Appearance", "Personas", "Skills", "Team types", "Library", "Remote access"], tabLabels);
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
            ["Teams", "Agents", "Uploads", "Skill Sources", "Docs"],
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

    /// <summary>The Skill Sources row says <i>Read only</i>, has the Hide switch and no Remove button; a writable built-in
    /// has no such caption (Skill Packs spec §6.11.2).</summary>
    [Fact]
    public async Task SkillSourcesRow_SaysReadOnly_HasHideSwitch_NoRemove()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);

        var row = cut.Find(".library-panel-root-row[data-root-id='skill-sources']");
        Assert.Equal("Read only", row.QuerySelector(".library-panel-read-only")?.TextContent.Trim());
        Assert.NotEmpty(row.QuerySelectorAll(".library-panel-hide-switch"));
        Assert.Empty(row.QuerySelectorAll(".library-panel-remove-button"));
        Assert.Empty(cut.Find(".library-panel-root-row[data-root-id='teams']").QuerySelectorAll(".library-panel-read-only"));
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
        await cut.InvokeAsync(() => cut.Find(".library-panel-name-field input").ChangeAsync("Notes"));
        await cut.InvokeAsync(() => cut.Find(".library-panel-path-field input").ChangeAsync(newPath));
        await cut.InvokeAsync(() => cut.Find(".library-panel-add-button").ClickAsync());

        Assert.Equal(["Notes"], fixture.Store.Pinned.Select(p => p.Name));
        cut.WaitForAssertion(() => Assert.Equal(
            ["Teams", "Agents", "Uploads", "Skill Sources", "Notes"],
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
        await cut.InvokeAsync(() => cut.Find(".library-panel-name-field input").ChangeAsync("Ghost"));
        await cut.InvokeAsync(() => cut.Find(".library-panel-path-field input").ChangeAsync(missingPath));
        await cut.InvokeAsync(() => cut.Find(".library-panel-add-button").ClickAsync());

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

    /// <summary>
    /// Removing a pinned root saves the remaining list and the row disappears, once the row's inline
    /// confirmation is answered (UX-026: Remove used to act on the first click). Changed from the
    /// one-click version for UX-026.
    /// </summary>
    [Fact]
    public async Task Remove_Pinned_Saves()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='docs'] .library-panel-remove-button").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='docs'] .inline-confirm-confirm").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".library-panel-root-row[data-root-id='docs']")));
        Assert.Empty(fixture.Store.Pinned);
    }

    /// <summary>The first click on a pinned root's Remove only asks: the row swaps to an inline Remove "Docs"? with Remove and Cancel, and nothing is saved (UX-026).</summary>
    [Fact]
    public async Task Remove_Pinned_AsksFirst_AndSavesNothing()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='docs'] .library-panel-remove-button").ClickAsync());

        var row = cut.Find(".library-panel-root-row[data-root-id='docs']");
        Assert.Equal("Remove \"Docs\"?", row.QuerySelector(".inline-confirm-prompt")?.TextContent.Trim());
        Assert.Equal("Remove", row.QuerySelector(".inline-confirm-confirm")?.TextContent.Trim());
        Assert.Equal("Cancel", row.QuerySelector(".inline-confirm-cancel")?.TextContent.Trim());
        Assert.Empty(row.QuerySelectorAll(".library-panel-remove-button"));
        Assert.Single(fixture.Store.Pinned);
    }

    /// <summary>Cancelling the inline confirmation keeps the root and brings the Remove button back (UX-026).</summary>
    [Fact]
    public async Task Remove_Pinned_Cancel_KeepsTheRoot()
    {
        using PanelFixture fixture = PanelFixture.Build(pinnedName: "Docs", pinnedPath: null, createPinned: true);
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='docs'] .library-panel-remove-button").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='docs'] .inline-confirm-cancel").ClickAsync());

        var row = cut.Find(".library-panel-root-row[data-root-id='docs']");
        Assert.NotEmpty(row.QuerySelectorAll(".library-panel-remove-button"));
        Assert.Empty(row.QuerySelectorAll(".inline-confirm"));
        Assert.Single(fixture.Store.Pinned);
    }

    /// <summary>The panel's action row (Reset pinned folders) comes before the table, as a Small button, not after it (UX-012).</summary>
    [Fact]
    public async Task ActionRow_ComesBeforeTheTable_AndIsSmall()
    {
        using PanelFixture fixture = PanelFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);

        var reset = cut.Find(".library-panel-reset-button");
        Assert.Contains("mud-button-outlined-size-small", reset.ClassList);
        Assert.True(cut.Markup.IndexOf("library-panel-reset-button", StringComparison.Ordinal) < cut.Markup.IndexOf("library-panel-roots-table", StringComparison.Ordinal));
    }

    /// <summary>The panel shows the shared trust-warning constant, the one <c>AddFolderDialog</c> adopts too (UX-R15).</summary>
    [Fact]
    public async Task Add_TrustWarning_IsTheSharedConstant()
    {
        using PanelFixture fixture = PanelFixture.Build();
        await using MudBunitContext ctx = fixture.NewContext();

        var cut = ctx.RenderWithPopovers(RenderPanel);

        Assert.Equal(LibraryRootStore.TrustWarning, cut.Find(".library-panel-trust-warning").TextContent.Trim());
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
        _ = cut.InvokeAsync(() => cut.Find(".library-panel-reset-button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Reset pinned folders?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Pinned folders go back to the configured list.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(
            ["Cancel", "Reset"],
            cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Reset", StringComparison.Ordinal)).ClickAsync());
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
        _ = cut.InvokeAsync(() => cut.Find(".library-panel-reset-button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());
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
        await cut.InvokeAsync(() => cut.Find(".library-panel-root-row[data-root-id='teams'] .library-panel-hide-switch input").ChangeAsync(true));

        LibraryRootStore reloaded = new(fixture.Options, fixture.Paths, NullLogger<LibraryRootStore>.Instance);
        Assert.Equal(["agents", "uploads", "skill-sources"], reloaded.VisibleRoots.Select(r => r.Id));
        Assert.Equal(["teams", "agents", "uploads", "skill-sources"], reloaded.Roots.Select(r => r.Id));
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

        private PanelFixture(TempDataDir dataDir, IOptions<TeamOptions> options, AgentPaths paths, LibraryRootStore store)
        {
            this.dataDir = dataDir;
            this.Options = options;
            this.Paths = paths;
            this.Store = store;
        }

        public IOptions<TeamOptions> Options { get; }

        public AgentPaths Paths { get; }

        public LibraryRootStore Store { get; }

        public string DataDir => this.dataDir.Path;

        /// <summary>Builds a fixture, optionally saving one pinned root ("Docs" under <c>DataDir</c>) before the store is handed to the panel, so <c>library-roots.json</c> exists from the start.</summary>
        public static PanelFixture Build(string? pinnedName = null, string? pinnedPath = null, bool createPinned = false)
        {
            TempDataDir dataDir = new();
            IOptions<TeamOptions> options = dataDir.Options();
            AgentPaths paths = new(options);
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
