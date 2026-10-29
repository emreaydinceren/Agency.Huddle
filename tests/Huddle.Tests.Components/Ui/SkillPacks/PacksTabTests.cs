using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Components.Settings.Skills;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins the Packs tab's empty state, the Details dialog, the arrival snackbar and New staying until the Human leaves
/// (Skill Packs spec §6.9.6, §6.9.9, §6.9.10).</summary>
public sealed class PacksTabTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>With no Packs row the empty state shows its title, explanation, three steps and Add; with one it does not.</summary>
    [Fact]
    public async Task PackEmptyState_ShownOnlyWithoutPacks()
    {
        this.fixture.Host.SkillsMd([]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> empty = RenderTab(ctx);

        Assert.Equal("No Packs yet", empty.Find(".empty-state-title").TextContent.Trim());
        Assert.Equal("Huddle does not download Skills; your AI tool does. It signs in where it needs to, fetches the Skills wherever it likes, and adds a pointer to SKILLS.md. Huddle picks it up.", empty.Find(".empty-state-text").TextContent.Trim());
        Assert.Equal(3, empty.FindAll(".pack-empty-steps li").Count);
        Assert.NotEmpty(empty.FindAll(".pack-empty-add"));
        Assert.NotEmpty(empty.FindAll(".pack-bar-reload"));

        await this.fixture.SeedAsync();
        await using MudBunitContext second = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> full = RenderTab(second);
        Assert.Empty(full.FindAll(".empty-state-title"));
        Assert.Single(full.FindAll(".pack-card"));
    }

    /// <summary>Details shows the row's Last updated and Notes, the roles, the consult_when phrase, a row per Skill and the
    /// state sentence in text; a Skill with S008 shows Unread files and its sentence below the table.</summary>
    [Fact]
    public async Task PackDetailsDialog_RendersTheRowAndSkills()
    {
        this.fixture.Host.Source();
        string docx = this.fixture.Host.CloneSkill("docx");
        Directory.CreateDirectory(Path.Combine(docx, "scripts"));
        File.WriteAllText(Path.Combine(docx, "scripts", "run.py"), "print(1)");
        this.fixture.Host.Pack("documents", "docx", docx);
        PackCardTests.WriteSkillsMd(this.fixture, "packs/documents.skillpack.json", "2026-10-16");
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<PackCard>(0);
            builder.AddAttribute(1, nameof(PackCard.RowIndex), 0);
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find(".pack-card-title").ClickAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".pack-details-dialog")));
        Assert.Equal("Last updated: 2026-10-16", cut.Find(".pack-details-last-updated").TextContent.Trim());
        Assert.Equal("Notes: Picked by Test.", cut.Find(".pack-details-notes").TextContent.Trim());
        Assert.Equal("For: Developer", cut.Find(".pack-details-roles").TextContent.Trim());
        Assert.Equal("Consult when: \"when documents applies\"", cut.Find(".pack-details-consult").TextContent.Trim());
        Assert.Single(cut.FindAll(".pack-details-skill"));
        Assert.Equal(Agency.Huddle.App.Skills.Ui.SkillStateText.For(Agency.Huddle.SkillPacks.Resolving.ItemState.Available).Sentence, cut.Find(".pack-details-sentence").TextContent.Trim());
        Assert.Equal("Unread files: Agents read only the .md files directly in a Skill's folder, up to 64 KB each, so scripts/ and references/ are ignored. A Skill that depends on them may work less well in Huddle than in the tool it was written for.", cut.Find(".pack-details-warning-s008").TextContent.Trim());
    }

    /// <summary>A Pack becoming usable raises one success snackbar naming it; the first snapshot
    /// raises none.</summary>
    [Fact]
    public async Task SkillArrivalWatcher_PackBecomesReady_OneSnackbar()
    {
        this.fixture.Host.Source();
        this.fixture.Host.SkillsMd([]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        _ = ctx.Render<SkillArrivalWatcher>();
        Assert.Empty(snackbar.ShownSnackbars);

        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"));
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.fixture.Host.ReadAsync();

        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("documents title is ready", shown.Message);
        Assert.Equal(Severity.Success, shown.Severity);
    }

    /// <summary>Four arrivals in one snapshot raise one "ready" snackbar.</summary>
    [Fact]
    public async Task SkillArrivalWatcher_FourArrivals_OneSnackbar()
    {
        this.fixture.Host.Source();
        this.fixture.Host.SkillsMd([]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        _ = ctx.Render<SkillArrivalWatcher>();

        string catalog = this.fixture.Host.Catalog("four", ["a1", "a2", "a3", "a4"], name => this.fixture.Host.CloneSkill(name));
        this.fixture.Host.SkillsMd([], [catalog]);
        await this.fixture.Host.ReadAsync();

        Assert.Equal("4 Packs and Skills are ready", Assert.Single(snackbar.ShownSnackbars).Message);
    }

    /// <summary>A key in NewKeys shows New, and stays New while the tab is open; leaving the tab marks the listed keys seen.</summary>
    [Fact]
    public async Task SkillsTabSeen_NewStaysUntilTheHumanLeaves()
    {
        this.fixture.Host.Settings.SetSeen([]);
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<SkillsPanel> cut = ctx.Render<SkillsPanel>();

        Assert.NotNull(cut.Find("tr:has([data-skill='docx'])").QuerySelector(".skill-state-chip-new"));
        Assert.Contains("skill:docx", this.fixture.Host.Registry.Current.NewKeys);

        await cut.InvokeAsync(() => cut.FindAll(".mud-tab")[1].ClickAsync());

        Assert.DoesNotContain("skill:docx", this.fixture.Host.Registry.Current.NewKeys);
        Assert.Contains("skill:docx", this.fixture.Host.Settings.Seen ?? []);
    }

    /// <summary>Renders the Packs tab.</summary>
    private static IRenderedComponent<ContainerFragment> RenderTab(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<PacksTab>(0);
            builder.AddAttribute(1, nameof(PacksTab.Revision), 1L);
            builder.CloseComponent();
        });
    }
}
