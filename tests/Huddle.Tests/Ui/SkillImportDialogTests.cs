using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Skills;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The skill import review dialog (SK-2, SK-3, SK-5, SK-6, Screen 3): step 1 searches the catalog or takes a
/// pasted GitHub tree URL, step 2 shows the flags first, then the provenance table, the differences and the
/// description, and installs only when the preview allows it, step 3 names the new folder. Cancel writes nothing.
/// </summary>
public sealed class SkillImportDialogTests
{
    private const string Catalog = """
        [
          { "id": "acme/skills/alpha", "name": "alpha", "description": "Alpha.", "publisher": "Acme",
            "repo": "https://github.com/acme/skills", "path": "skills/alpha",
            "link": "https://github.com/acme/skills/tree/0123456789abcdef0123456789abcdef01234567/skills/alpha",
            "commit": "0123456789abcdef0123456789abcdef01234567", "license": "MIT",
            "category": "Testing", "subCategory": "Sub", "requires": [] },
          { "id": "bolt/kit/beta", "name": "beta", "description": "Beta.", "publisher": "Bolt",
            "repo": "https://github.com/bolt/kit", "path": "beta",
            "link": "https://github.com/bolt/kit/tree/fedcba9876543210fedcba9876543210fedcba98/beta",
            "commit": "fedcba9876543210fedcba9876543210fedcba98", "license": "Apache-2.0",
            "category": "Writing", "subCategory": "Sub", "requires": [] },
          { "id": "bolt/kit/gamma", "name": "gamma", "description": "Gamma.", "publisher": "Bolt",
            "repo": "https://github.com/bolt/kit", "path": "gamma",
            "link": "https://github.com/bolt/kit/tree/fedcba9876543210fedcba9876543210fedcba98/gamma",
            "commit": "fedcba9876543210fedcba9876543210fedcba98", "license": "none",
            "category": "Writing", "subCategory": "Sub", "requires": [] }
        ]
        """;

    private const string TreeUrl = "https://github.com/acme/skills/tree/main/skills/alpha";

    /// <summary>Typing part of a name, a publisher or a category narrows the result list to the matching catalog entries, in catalog order.</summary>
    [Theory]
    [InlineData("alp", "Alpha")]
    [InlineData("bolt", "Beta,Gamma")]
    [InlineData("writ", "Beta,Gamma")]
    [InlineData("", "Alpha,Beta,Gamma")]
    public async Task Search_NarrowsTheResults(string typed, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, new FakeSkillImporter());

        await cut.InvokeAsync(() => cut.Find(".skill-import-search input").InputAsync(typed));

        string[] want = expected.Split(',', StringSplitOptions.RemoveEmptyEntries);
        cut.WaitForAssertion(() =>
        {
            string[] names = [.. cut.FindAll(".skill-import-result-name").Select(static e => e.TextContent.Trim())];
            Assert.Equal(want, names);
        });
    }

    /// <summary>Picking a result previews that catalog id, once, and moves to the review step.</summary>
    [Fact]
    public async Task PickingAResult_PreviewsThatCatalogEntry_AndShowsTheReview()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["bolt/kit/beta"] = FakeSkillImporter.MakePreview(name: "beta");
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await PickAsync(cut, 1);

        Assert.Equal(["bolt/kit/beta"], importer.CatalogCalls);
        Assert.Equal("Alpha does the alpha thing.", cut.Find(".skill-import-description").TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-import-result"));
    }

    /// <summary>A pasted GitHub tree URL is previewed as typed and moves to the review step.</summary>
    [Fact]
    public async Task PastedUrl_IsPreviewedAsTyped()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new() { ForUrl = FakeSkillImporter.MakePreview() };
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await cut.InvokeAsync(() => cut.Find(".skill-import-url input").InputAsync(TreeUrl));
        await cut.InvokeAsync(() => cut.Find(".skill-import-url-go").ClickAsync());

        Assert.Equal([TreeUrl], importer.UrlCalls);
        Assert.Equal("Acme", cut.Find(".skill-import-publisher").TextContent.Trim());
    }

    /// <summary>Text that is not a GitHub tree URL stays on step 1 and says so.</summary>
    [Fact]
    public async Task PastedUrl_NotATreeUrl_ShowsThePlainMessage()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, new FakeSkillImporter());

        await cut.InvokeAsync(() => cut.Find(".skill-import-url input").InputAsync("not a url"));
        await cut.InvokeAsync(() => cut.Find(".skill-import-url-go").ClickAsync());

        Assert.Equal("That is not a GitHub tree URL.", cut.Find(".skill-import-error").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".skill-import-result"));
    }

    /// <summary>A catalog entry the importer cannot preview stays on step 1 and says so.</summary>
    [Fact]
    public async Task PickingAResult_WithNoPreview_ShowsThePlainMessage()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, new FakeSkillImporter());

        await PickAsync(cut, 0);

        Assert.Equal("That skill is not in the catalog.", cut.Find(".skill-import-error").TextContent.Trim());
    }

    /// <summary>The flag alerts render above the provenance table, one per flag group, each listing file and line.</summary>
    [Fact]
    public async Task Review_RendersFlagGroupsAboveTheProvenanceTable()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview(flags:
        [
            new ReviewFlag(ReviewFlagKind.Script, "scripts/run.ps1", 1, "run.ps1"),
            new ReviewFlag(ReviewFlagKind.Script, "scripts/more.sh", 1, "more.sh"),
            new ReviewFlag(ReviewFlagKind.NetworkCall, "SKILL.md", 7, "curl https://example.com"),
        ]);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await PickAsync(cut, 0);

        string[] order = [.. cut.FindAll(".skill-import-flag, .skill-import-provenance").Select(static e => e.ClassList.Contains("skill-import-flag") ? "flag" : "table")];
        Assert.Equal(["flag", "flag", "table"], order);
        Assert.Equal(["Scripts", "Network calls"], cut.FindAll(".skill-import-flag-title").Select(static e => e.TextContent.Trim()).ToArray());
        Assert.Equal(["scripts/run.ps1:1", "scripts/more.sh:1", "SKILL.md:7"], cut.FindAll(".skill-import-flag-location").Select(static e => e.TextContent.Trim()).ToArray());
    }

    /// <summary>A skill with no license is flagged in words and the table says "No license" plainly.</summary>
    [Fact]
    public async Task Review_NoLicense_SaysSoPlainly()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview(
            license: "none",
            flags: [new ReviewFlag(ReviewFlagKind.NoLicense, ".", 1, "No license declared")]);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await PickAsync(cut, 0);

        Assert.Equal("No license", cut.Find(".skill-import-license").TextContent.Trim());
        Assert.Equal("No license", cut.Find(".skill-import-flag-title").TextContent.Trim());
    }

    /// <summary>The table names the publisher, the repository, a link to the pinned folder, the short commit and the license.</summary>
    [Fact]
    public async Task Review_ShowsTheProvenanceTable()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await PickAsync(cut, 0);

        Assert.Equal("Acme", cut.Find(".skill-import-publisher").TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-import-repo"));
        Assert.Equal("github.com/acme/skills", cut.Find(".skill-import-link a").TextContent.Trim());
        Assert.Equal("0123456", cut.Find(".skill-import-commit").TextContent.Trim());
        Assert.Equal("MIT", cut.Find(".skill-import-license").TextContent.Trim());
        Assert.Equal(
            "https://github.com/acme/skills/tree/0123456789abcdef0123456789abcdef01234567/skills/alpha",
            cut.Find(".skill-import-link a").GetAttribute("href"));
        Assert.Empty(cut.FindAll(".skill-import-flag"));
    }

    /// <summary>Each frontmatter difference is listed.</summary>
    [Fact]
    public async Task Review_ListsFrontmatterDifferences()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview(differences: ["name: the catalog says 'alpha' but the file says 'other'"]);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await PickAsync(cut, 0);

        Assert.Equal(["name: the catalog says 'alpha' but the file says 'other'"], cut.FindAll(".skill-import-difference").Select(static e => e.TextContent.Trim()).ToArray());
    }

    /// <summary>A preview that cannot be installed lists its problems (a download error included, in the downloader's own words) and disables Install.</summary>
    [Fact]
    public async Task Review_WithProblems_ShowsThemAndDisablesInstall()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview(problems: ["GitHub could not be reached."]);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);

        await PickAsync(cut, 0);

        Assert.Equal(["GitHub could not be reached."], cut.FindAll(".skill-import-problem").Select(static e => e.TextContent.Trim()).ToArray());
        Assert.True(cut.Find(".skill-import-install").HasAttribute("disabled"));
        Assert.Empty(importer.Installs);
    }

    /// <summary>Install on an installable preview calls the importer exactly once with that preview, then shows the folder name and a link to the skill.</summary>
    [Fact]
    public async Task Install_CallsTheImporterOnce_AndShowsSuccess()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        ImportPreview preview = FakeSkillImporter.MakePreview();
        importer.ByCatalogId["acme/skills/alpha"] = preview;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);
        await PickAsync(cut, 0);
        Assert.False(cut.Find(".skill-import-install").HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.Find(".skill-import-install").ClickAsync());

        Assert.Same(preview, Assert.Single(importer.Installs));
        Assert.Equal("Installed alpha.", cut.Find(".skill-import-success").TextContent.Trim());
        Assert.Equal("/settings/skills", cut.Find(".skill-import-skill-link").GetAttribute("href"));
        Assert.Empty(cut.FindAll(".skill-import-install"));
    }

    /// <summary>A failed install stays on the review step and shows the importer's message.</summary>
    [Fact]
    public async Task Install_Failure_ShowsTheImportersMessage()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new() { Outcome = new ImportOutcome(false, "The skill could not be written.", null) };
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);
        await PickAsync(cut, 0);

        await cut.InvokeAsync(() => cut.Find(".skill-import-install").ClickAsync());

        Assert.Equal("The skill could not be written.", cut.Find(".skill-import-error").TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-import-success"));
    }

    /// <summary>Cancel on the review step closes the dialog as cancelled and installs nothing.</summary>
    [Fact]
    public async Task Cancel_InstallsNothing_AndClosesCancelled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview();
        await using MudBunitContext ctx = new();
        (IRenderedComponent<ContainerFragment> cut, IDialogReference reference) = await OpenWithReferenceAsync(ctx, dir, importer);
        await PickAsync(cut, 0);

        await cut.InvokeAsync(() => cut.Find(".skill-import-cancel").ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.True(result.Canceled);
        Assert.Empty(importer.Installs);
    }

    /// <summary>Back returns from the review to the search without installing.</summary>
    [Fact]
    public async Task Back_ReturnsToTheSearch()
    {
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId["acme/skills/alpha"] = FakeSkillImporter.MakePreview();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, dir, importer);
        await PickAsync(cut, 0);

        await cut.InvokeAsync(() => cut.Find(".skill-import-back").ClickAsync());

        Assert.Equal(3, cut.FindAll(".skill-import-result").Count);
        Assert.Empty(importer.Installs);
    }

    /// <summary>The Skills panel's "Import skill" button opens the prompt dialog on Add (Skill Packs spec §6.9.3).</summary>
    [Fact]
    public async Task SkillsPanel_ImportButton_OpensTheDialog()
    {
        using TempDataDir dir = new();
        using Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = new();
        using SkillStore store = new(dir.Options(), NullLogger<SkillStore>.Instance);
        await using MudBunitContext ctx = new();
        SkillsTestServices.AddFromHost(ctx.Services, host);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton<ISkillImporter>(new FakeSkillImporter());
        ctx.Services.AddSingleton(new SkillsCatalogIndex(dir.Options(), NullLogger<SkillsCatalogIndex>.Instance));
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.SkillsPanel>(0);
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find(".skills-import-button").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Add with your AI tool", cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>Clicks the Pick button of the <paramref name="index"/>th result and waits for the review.</summary>
    private static async Task PickAsync(IRenderedComponent<ContainerFragment> cut, int index)
    {
        await cut.InvokeAsync(() => cut.FindAll(".skill-import-pick")[index].ClickAsync());
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(".skill-import-result").Count == 0 || cut.FindAll(".skill-import-error").Count > 0));
    }

    /// <summary>Opens the dialog and returns the rendered host.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenAsync(MudBunitContext ctx, TempDataDir dir, FakeSkillImporter importer)
    {
        (IRenderedComponent<ContainerFragment> cut, _) = await OpenWithReferenceAsync(ctx, dir, importer);
        return cut;
    }

    /// <summary>Opens the dialog through <see cref="IDialogService"/> the way the Skills panel does, over a catalog file in <paramref name="dir"/>.</summary>
    private static async Task<(IRenderedComponent<ContainerFragment> Cut, IDialogReference Reference)> OpenWithReferenceAsync(MudBunitContext ctx, TempDataDir dir, FakeSkillImporter importer)
    {
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.SkillsCatalogFile), Catalog);
        ctx.Services.AddSingleton<ISkillImporter>(importer);
        ctx.Services.AddSingleton(new SkillsCatalogIndex(dir.Options(), NullLogger<SkillsCatalogIndex>.Instance));
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();

        IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<Agency.Huddle.App.Components.Settings.SkillImportDialog>("Import skill", Agency.Huddle.App.Components.Settings.SkillImportDialog.Options));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        return (cut, reference);
    }
}
