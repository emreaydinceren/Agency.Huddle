using Agency.Huddle.App.Acp;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Agency.Huddle.Tests.Teams;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins what the Teammate card does with the registry (Skill Packs spec §6.10.4): Pack chips by state, Find more
/// Packs, the Skills select and its Not on this computer yet list, and copying on Save.</summary>
public sealed class TeammateCardSkillPacksTests : IDisposable
{
    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2 }
          ] }
        """;

    private readonly RegistryHost host = new();

    /// <summary>Disposes the host.</summary>
    public void Dispose()
    {
        this.host.Dispose();
    }

    /// <summary>An available Pack is a toggle chip; a not-downloaded one is a line with Copy prompt and no chip.</summary>
    [Fact]
    public async Task PackChips_AvailableIsAChip_NotDownloadedIsALine()
    {
        this.host.Source();
        this.host.PackOf("documents", "1.0.0", ("docx", this.host.CloneSkill("docx")));
        this.host.PackOf("lost", "1.0.0", ("ghost", null));
        this.host.SkillsMd(["packs/documents.skillpack.json", "packs/lost.skillpack.json"]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev(), CancellationToken.None);
        await using MudBunitContext ctx = this.NewCardContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory);

        Assert.Equal(["documents title"], cut.FindAll(".teammate-card-pack").Select(static chip => chip.TextContent.Trim()));
        Assert.Equal("Pack \"lost title\" is not downloaded.", cut.Find(".teammate-card-pack-missing .mud-typography").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".teammate-card-pack-copy-prompt"));
        Assert.Equal("Find more Packs for Developer", cut.Find(".teammate-card-find-packs").TextContent.Trim());
    }

    /// <summary>A withdrawn Pack the Teammate holds is a red chip with the warning and Remove from this Teammate; one it does
    /// not hold is not offered.</summary>
    [Fact]
    public async Task PackChips_Withdrawn_OnlyForAHolder()
    {
        this.host.Source("withdrawn");
        this.host.PackOf("documents", "1.0.0", ("docx", this.host.CloneSkill("docx")));
        this.host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("skills: [docx]\n_packs: ['documents@1.0.0']\n"), CancellationToken.None);
        await factory.WriteDefinitionAsync("Other", Dev(name: "Other"), CancellationToken.None);
        await using MudBunitContext ctx = this.NewCardContext(factory);

        IRenderedComponent<ContainerFragment> holder = await OpenEditAsync(ctx, factory);

        Assert.Equal("documents title", holder.Find(".teammate-card-pack-withdrawn").TextContent.Trim());
        Assert.Equal("Pack \"documents title\" was withdrawn by its Source: Because. Dev keeps working.", holder.Find(".teammate-card-pack-withdrawn-text").TextContent.Trim());
        Assert.NotEmpty(holder.FindAll(".teammate-card-pack-remove"));

        await using MudBunitContext other = this.NewCardContext(factory);
        IRenderedComponent<ContainerFragment> notHolder = await OpenEditAsync(other, factory, "Other");
        Assert.Empty(notHolder.FindAll(".teammate-card-pack, .teammate-card-pack-withdrawn"));
    }

    /// <summary>A Pack the Teammate records that no row provides shows the warning.</summary>
    [Fact]
    public async Task PackNotInRegistry_ShowsWarning()
    {
        this.host.Source();
        this.host.SkillsMd([]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("_packs: ['vanished@1.0.0']\n"), CancellationToken.None);
        await using MudBunitContext ctx = this.NewCardContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory);

        Assert.Equal(
            ["Pack \"vanished\" is no longer in SKILLS.md. Dev keeps its Skills; remove the Pack here, or ask your AI tool to add it back."],
            cut.FindAll(".teammate-card-warning").Select(static alert => alert.TextContent.Trim()));
    }

    /// <summary>The Skills list offers a copyable registry Skill with its caption; a not-downloaded one is only in the
    /// collapsed list, with Copy prompt.</summary>
    [Fact]
    public async Task SkillsSelect_CopyableIsAnOption_NotDownloadedIsListedBelow()
    {
        this.host.Source();
        string catalog = this.host.Catalog("extra", ["copyme", "ghost"], name => name == "copyme" ? this.host.CloneSkill("copyme") : null);
        this.host.SkillsMd([], [catalog]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev(), CancellationToken.None);
        await using MudBunitContext ctx = this.NewCardContext(factory);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory);

        await cut.InvokeAsync(() => cut.FindAll(".mud-select .mud-select-input")[0].MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal(["Huddle copies it into Skills/ when you save."], cut.FindAll(".teammate-card-copy-caption").Select(static caption => caption.TextContent.Trim())));
        Assert.Equal("Not on this computer yet (1)", cut.Find(".teammate-card-not-here-toggle").TextContent.Trim());
        Assert.Equal("ghost", cut.Find(".teammate-card-not-here-row").GetAttribute("data-skill"));
    }

    /// <summary>Save copies a copyable Skill before the Teammate file is written; the file names it only afterwards.</summary>
    [Fact]
    public async Task Save_CopiesBeforeTheWrite()
    {
        this.host.Source();
        string catalog = this.host.Catalog("extra", ["copyme"], name => this.host.CloneSkill(name));
        this.host.SkillsMd([], [catalog]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("skills: [copyme]\n"), CancellationToken.None);
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        string before = personas.Get("Dev")?.Text ?? string.Empty;
        FakeSkillCopier copier = new();
        string? textWhileCopying = null;
        copier.OnEnsure = _ => textWhileCopying = personas.Get("Dev")?.Text;
        await using MudBunitContext ctx = this.NewCardContext(factory, copier);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory);

        await ClickButtonAsync(cut, "Save");

        Assert.Equal(["copyme"], Assert.Single(copier.Calls));
        Assert.Equal(before, textWhileCopying);
    }

    /// <summary>A Skill that cannot be copied shows the message and writes nothing.</summary>
    [Fact]
    public async Task Save_CopyFails_ShowsMessage_WritesNothing()
    {
        this.host.Source();
        this.host.SkillsMd([]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("skills: [ghost]\n"), CancellationToken.None);
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        FakeSkillCopier copier = new() { Missing = [FakeSkillCopier.NotDownloaded("ghost")] };
        await using MudBunitContext ctx = this.NewCardContext(factory, copier);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory);
        string before = personas.Get("Dev")?.Text ?? string.Empty;

        await ClickButtonAsync(cut, "Save");

        cut.WaitForAssertion(() => Assert.Equal("Skills not ready: ghost (not downloaded). Copy a prompt below, then save again.", cut.Find(".mud-alert-message").TextContent.Trim()));
        Assert.Equal(before, personas.Get("Dev")?.Text);
    }

    /// <summary>Cancel copies nothing.</summary>
    [Fact]
    public async Task Cancel_CopiesNothing()
    {
        this.host.Source();
        this.host.SkillsMd([]);
        await this.host.ReadAsync();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("skills: [ghost]\n"), CancellationToken.None);
        FakeSkillCopier copier = new();
        await using MudBunitContext ctx = this.NewCardContext(factory, copier);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory);

        await ClickButtonAsync(cut, "Cancel");

        Assert.Empty(copier.Calls);
    }

    /// <summary>A Developer persona text.</summary>
    private static string Dev(string extra = "", string name = "Dev") =>
        $"---\nname: {name}\ntitle: Developer\nalias: {name.ToLowerInvariant()}\nteams: [Web Team]\n_role: 'web/dev@1.2.0'\n{extra}---\nYou build.";

    /// <summary>Opens the View card of <paramref name="name"/> and switches it to Edit.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenEditAsync(MudBunitContext ctx, TeamWebApplicationFactory factory, string name = "Dev")
    {
        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, name);
        await ClickButtonAsync(cut, "Edit");
        return cut;
    }

    /// <summary>The card context with the web team type, this registry and its prompt services, and an optional copier.</summary>
    private MudBunitContext NewCardContext(TeamWebApplicationFactory factory, ISkillCopier? copier = null)
    {
        MudBunitContext ctx = NewContext(factory);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\n---\nYou lead.",
            ["web/candidates/dev.md"] = "---\ntitle: Developer\n---\nYou build.",
            ["web/processes/flow.md"] = "# Flow",
        };
        ctx.Services.AddSingleton(new TeamTypeCatalog(this.host.Options, NullLogger<TeamTypeCatalog>.Instance, builtins));
        ctx.Services.AddSingleton(this.host.Registry);
        ctx.Services.AddSingleton(new SkillPromptBuilder(new Agency.Huddle.Tests.SkillPacks.Prompts.RecordingPromptSource(), new InstallFileAt(Path.Combine(this.host.DataDir, "InstallSkillPack.md"))));
        ctx.Services.AddScoped<PathFormatChoice>();
        if (copier is not null)
        {
            ctx.Services.AddSingleton(copier);
        }

        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    /// <summary>An install file at a fixed path.</summary>
    /// <param name="path">Its full path.</param>
    private sealed class InstallFileAt(string path) : IInstallFileLocation
    {
        /// <inheritdoc/>
        public string InstallFilePath => path;
    }
}
