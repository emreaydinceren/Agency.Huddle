using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.SkillPacks.Checking;
using CheckSeverity = Agency.Huddle.SkillPacks.Checking.Severity;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="PackCard"/> and the <see cref="PackView"/> behind it (Skill Packs spec §6.9.4, §6.9.5): the
/// fields, the roles line, the cause line per state, Copy fix prompt, and the ⋯ menu order.</summary>
public sealed class PackCardTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>Title, version, Source, Skill count, role, the updated date and the chip all render.</summary>
    [Fact]
    public async Task Card_RendersItsFields()
    {
        this.fixture.Host.Source();
        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"), version: "2.1.0");
        WriteSkillsMd(this.fixture, "packs/documents.skillpack.json", "2026-10-16");
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.Services.AddSingleton<TimeProvider>(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero)));

        IRenderedComponent<ContainerFragment> cut = Render(ctx, 0);

        Assert.Equal("documents title", cut.Find(".pack-card-title").TextContent.Trim());
        Assert.Equal("2.1.0", cut.Find(".pack-card-version").TextContent.Trim());
        Assert.Equal("local/test · 1 Skill · updated Yesterday", Normalise(cut.Find(".pack-card-meta").TextContent));
        Assert.Equal("For Developer", cut.Find(".pack-card-roles").TextContent.Trim());
        Assert.Equal("Available", cut.Find(".skill-state-chip-available .status-chip").TextContent.Trim());
        Assert.Empty(cut.FindAll(".pack-card-cause"));
    }

    /// <summary>The roles line shows three titles, then "+N".</summary>
    [Fact]
    public void RolesLine_PastThree_ShowsPlusN()
    {
        RegistryRow row = new(RowKind.Pack, 0, PathEntries.Empty, null, null, null, null, default);
        PackEntry pack = new(row, null, null, "p", "1", "P", "s", [], null, [], [], []);
        PackView view = new(pack, "P", ItemState.Available, [], null, ["A", "B", "C", "D", "E"], null, [], false);

        Assert.Equal("A, B, C +2", view.RolesLine);
        Assert.Equal("A, B", (view with { RoleTitles = ["A", "B"] }).RolesLine);
    }

    /// <summary>A Broken card shows the composed cause line naming the file, and Copy fix prompt copies the fix prompt.</summary>
    [Fact]
    public async Task BrokenCard_ShowsCopyFixPrompt()
    {
        this.fixture.Host.Source();
        string broken = Path.Combine(this.fixture.Host.Clone, "skills", "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "README.md"), "No SKILL.md.");
        this.fixture.Host.Pack("bad", "broken", broken);
        this.fixture.Host.SkillsMd(["packs/bad.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        PackView view = PackView.From(this.fixture.Host.Registry, this.fixture.Host.Registry.Current.Resolved.Packs[0], this.fixture.Host.Roles.List());
        Finding first = Assert.IsType<Finding>(view.Findings.FirstOrDefault(static finding => finding.Severity == CheckSeverity.Error));

        IRenderedComponent<ContainerFragment> cut = Render(ctx, 0);
        await cut.InvokeAsync(() => cut.Find(".pack-card-copy-fix").ClickAsync());

        Assert.Equal(ItemState.Broken, view.State);
        string file = Path.GetRelativePath(this.fixture.Host.Dir, first.File).Replace('\\', '/');
        Assert.Equal($"{file}: {SkillProblemText.Compose(first)}", cut.Find(".pack-card-cause").TextContent.Trim());
        JSRuntimeInvocation copy = Assert.Single(ctx.JSInterop.Invocations["huddleClipboard.copy"]);
        Assert.Contains(first.Code, Assert.IsType<string>(copy.Arguments[0]), StringComparison.Ordinal); // contains-ok: the prompt text is pinned by SkillPromptBuilderTests
    }

    /// <summary>A Not downloaded card names the Skill with no folder in text and offers Copy fix prompt.</summary>
    [Fact]
    public async Task NotDownloadedCard_NamesTheMissingSkill()
    {
        this.fixture.Host.Source();
        this.fixture.Host.Pack("lost", "ghost", null);
        this.fixture.Host.SkillsMd(["packs/lost.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx, 0);

        Assert.Equal("ghost has no folder on this computer.", cut.Find(".pack-card-cause").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".pack-card-copy-fix"));
    }

    /// <summary>The ⋯ menu lists Details, Copy fix prompt, Hide, Show in Library, then a divider and Remove; Hide hides the Pack.</summary>
    [Fact]
    public async Task Menu_Order_AndHideHides()
    {
        this.fixture.Host.Source();
        this.fixture.Host.Pack("lost", "ghost", null);
        this.fixture.Host.SkillsMd(["packs/lost.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, 0);

        await cut.InvokeAsync(() => cut.Find(".pack-card-menu button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));
        IReadOnlyList<string> items = [.. cut.FindAll(".mud-menu-item").Select(static item => item.TextContent.Trim())];
        Assert.Single(cut.FindAll(".pack-card-menu-remove"));
        await cut.InvokeAsync(() => cut.Find(".pack-card-menu-hide").ClickAsync());

        Assert.Equal(["Details", "Copy fix prompt", "Hide", "Show in Library", "Remove"], items);
        Assert.True(this.fixture.Host.Registry.IsHidden(this.fixture.Host.Registry.Current.Resolved.Packs[0]));
        Assert.Equal("lost title hidden", Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars).Message);
    }

    /// <summary>Remove with no holders asks with the confirm, and confirming deletes the row and the file.</summary>
    [Fact]
    public async Task Remove_NoHolders_ConfirmsThenRemoves()
    {
        this.fixture.Host.Source();
        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"));
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        string file = this.fixture.Host.Registry.Current.Resolved.Packs[0].FilePath ?? string.Empty;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, 0);

        await cut.InvokeAsync(() => cut.Find(".pack-card-menu button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".pack-card-menu-remove")));
        Task removing = cut.InvokeAsync(() => cut.Find(".pack-card-menu-remove").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal("Remove \"documents title\"?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal(RemovalText.PackMessage("packs\\documents.skillpack.json".Replace('\\', Path.DirectorySeparatorChar), null), cut.Find(".mud-dialog-content").TextContent.Trim());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[^1].ClickAsync());
        await removing;

        cut.WaitForAssertion(() => Assert.False(File.Exists(file)));
        Assert.Equal("documents title removed.", ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Last().Message);
    }

    /// <summary>Remove with holders opens the three-way dialog naming them; Cancel changes nothing.</summary>
    [Fact]
    public async Task Remove_WithHolders_OpensRemovePackDialog()
    {
        this.fixture.Host.Source();
        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"));
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json"]);
        this.fixture.Host.Teammate("Jane", ["docx"], [new Agency.Huddle.App.Teams.VersionedRef("documents", "1.0.0")]);
        await this.fixture.Host.ReadAsync();
        string file = this.fixture.Host.Registry.Current.Resolved.Packs[0].FilePath ?? string.Empty;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, 0);

        await cut.InvokeAsync(() => cut.Find(".pack-card-menu button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".pack-card-menu-remove")));
        Task removing = cut.InvokeAsync(() => cut.Find(".pack-card-menu-remove").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Jane has this Pack. Choose what happens to them.", cut.Find(".remove-pack-holders").TextContent.Trim()));
        Assert.Equal(["Cancel", "Keep their Skills", "Remove from Teammates too"], cut.FindAll(".remove-pack-dialog .mud-dialog-actions button, .mud-dialog-actions button").Select(static button => button.TextContent.Trim()).Distinct());
        await cut.InvokeAsync(() => cut.Find(".remove-pack-cancel").ClickAsync());
        await removing;
        Assert.True(File.Exists(file));
    }

    /// <summary>Writes SKILLS.md with one Packs row and a Last updated cell.</summary>
    internal static void WriteSkillsMd(SkillsUiFixture fixture, string pack, string lastUpdated)
    {
        Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost.Write(
            fixture.Host.Dir,
            "SKILLS.md",
            $"# Skills registry\n\n## Packs\n\n| Local path | Origin URL | Last updated | Notes |\n| --- | --- | --- | --- |\n| {pack} | https://github.com/o/r | {lastUpdated} | Picked by Test. |\n\n## Catalogs\n\n| Local path |\n| --- |\n");
    }

    /// <summary>Collapses runs of white space.</summary>
    internal static string Normalise(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Renders the card for a Packs row with its popovers.</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, int index)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<PackCard>(0);
            builder.AddAttribute(1, nameof(PackCard.RowIndex), index);
            builder.CloseComponent();
        });
    }
}
