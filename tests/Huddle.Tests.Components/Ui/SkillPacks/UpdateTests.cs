using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.SkillPacks.Resolving;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>A Pack <c>documents</c> (docx copied, and a second Skill <c>sheets</c>) held by Jane, with docx's files changed at
/// the pointer, for the update card's tests.</summary>
internal sealed class UpdateFixture : IDisposable
{
    /// <summary>The UI fixture.</summary>
    public SkillsUiFixture Ui { get; } = new();

    /// <summary>The registry host.</summary>
    public RegistryHost Host => this.Ui.Host;

    /// <summary>The pointer folder of docx.</summary>
    public string Docx { get; private set; } = string.Empty;

    /// <summary>Builds it: version 1.0.0 with docx copied and held, then version 1.1.0 adding sheets and changing docx.</summary>
    public async Task SeedAsync(bool addSheets = true, bool changeDocx = true, bool holder = true)
    {
        this.Host.Source();
        this.Docx = this.Host.CloneSkill("docx");
        string sheets = this.Host.CloneSkill("sheets");
        this.Host.PackOf("documents", "1.0.0", ("docx", this.Docx));
        this.Host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.Host.ReadAsync();
        SkillCopier copier = new(this.Host.Registry, this.Host.Settings, this.Host.Log, NullLogger<SkillCopier>.Instance, this.Host.Clock);
        Assert.True((await copier.CopyAsync([.. this.Host.Registry.Current.Resolved.Skills], Xunit.TestContext.Current.CancellationToken)).Ok);
        await Eventually.UntilAsync(() => this.Host.Skills.Get("docx") is not null, Xunit.TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        if (holder)
        {
            this.Host.Teammate("Jane", ["docx"], [new VersionedRef("documents", "1.0.0")]);
        }

        if (addSheets)
        {
            this.Host.PackOf("documents", "1.1.0", ("docx", this.Docx), ("sheets", sheets));
        }

        if (changeDocx)
        {
            File.WriteAllText(Path.Combine(this.Docx, "extra.md"), "More.");
            File.AppendAllText(Path.Combine(this.Docx, "SKILL.md"), "Changed.\n");
        }

        await this.Host.ReadAsync();
    }

    /// <summary>The plan for documents.</summary>
    public PackUpdatePlan Plan()
    {
        IReadOnlyList<Persona> teammates = [.. this.Host.Personas.Entries.Select(entry => this.Host.Personas.Get(entry.Name)).OfType<Persona>()];
        return PackUpdatePlanner.Plan("documents", this.Host.Registry, this.Host.Skills, teammates, PhysicalFileReader.Instance);
    }

    /// <summary>Disposes it.</summary>
    public void Dispose()
    {
        this.Ui.Dispose();
    }
}

/// <summary>Pins <see cref="PackUpdatePlanner"/> (Skill Packs spec §6.9.8, §8.4).</summary>
public sealed class PackUpdatePlannerTests : IDisposable
{
    private readonly UpdateFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>A changed Skill file is in FileChanges with its affected Teammates, counting the changed and added files, and
    /// is never a Teammate checkbox; an added Skill is in Added and on the holder's row.</summary>
    [Fact]
    public async Task Plan_FileChangeShared_AddedIsMembership()
    {
        await this.fixture.SeedAsync();

        PackUpdatePlan plan = this.fixture.Plan();

        SkillFileChange change = Assert.Single(plan.FileChanges);
        Assert.Equal("docx", change.Name);
        Assert.Equal(2, change.FilesChanged);
        Assert.Equal(["Jane"], change.AffectedTeammates);
        Assert.Equal("0123456", change.ToCommit);
        Assert.Equal(["sheets"], plan.Added.Select(static added => added.Name));
        TeammateChange jane = Assert.Single(plan.Teammates);
        Assert.Equal(["sheets"], jane.AddSkills);
        Assert.True(jane.VersionChanges);
        Assert.Equal("adds sheets; updates consult when; restarts", jane.Summary);
        Assert.Equal(("1.0.0", "1.1.0"), (plan.FromVersion, plan.ToVersion));
        Assert.Empty(plan.Removed);
    }

    /// <summary>A Teammate that already has every Skill, the phrase and the version is not listed.</summary>
    [Fact]
    public async Task Plan_TeammateUpToDate_NotListed()
    {
        await this.fixture.SeedAsync(addSheets: false, holder: false);
        this.fixture.Host.Teammate("Omar", ["docx"], []);
        Persona omar = this.fixture.Host.Personas.Get("Omar") ?? throw new InvalidOperationException("Omar");
        this.fixture.Host.Personas.Update("Omar", PackMembership.Add(omar.Text, PackContents.From(this.fixture.Host.Registry.FindPack("documents") ?? throw new InvalidOperationException("documents"))), null, null, null);
        await this.fixture.Host.ReadAsync();

        PackUpdatePlan plan = this.fixture.Plan();

        Assert.Empty(plan.Teammates);
        Assert.Single(plan.FileChanges);
    }

    /// <summary>A line-ending difference is not a changed file; content and files on one side only are.</summary>
    [Fact]
    public void FileHashes_IgnoreLineEndings_CountOneSidedFiles()
    {
        Agency.Huddle.Tests.SkillPacks.InMemoryFileReader files = new Agency.Huddle.Tests.SkillPacks.InMemoryFileReader()
            .Add(Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("a", "SKILL.md"), "one\r\ntwo\r\n")
            .Add(Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("a", "old.md"), "gone")
            .Add(Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("a", ".source.json"), "{}")
            .Add(Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("b", "SKILL.md"), "one\ntwo\n")
            .Add(Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("b", "new.md"), "new");

        int changed = SkillFolderHash.ChangedFiles(
            SkillFolderHash.FileHashes(files, Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("a")),
            SkillFolderHash.FileHashes(files, Agency.Huddle.Tests.SkillPacks.InMemoryFileReader.At("b")));

        Assert.Equal(2, changed);
    }
}

/// <summary>Pins <see cref="UpdateDialog"/> (Skill Packs spec §6.9.8): the two sections, the button's words, Not now, and that
/// Update copies before it rewrites any Teammate.</summary>
public sealed class UpdateDialogTests : IDisposable
{
    private readonly UpdateFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The Skill files section has no checkbox; the membership section has one per Teammate, checked; the button
    /// reads Update 1, then Update files with nobody ticked.</summary>
    [Fact]
    public async Task Sections_AndButtonWords()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.Ui.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx);

        Assert.Empty(cut.FindAll(".update-dialog-file input[type=checkbox]"));
        Assert.Equal("Used by Jane. Their copy changes as soon as you update; they do not restart.", cut.Find(".update-dialog-affected").TextContent.Trim());
        Assert.Single(cut.FindAll(".update-dialog-teammate input[type=checkbox]"));
        Assert.Equal("Update 1", cut.Find(".update-dialog-apply").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".update-dialog-teammate input").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = false }));

        Assert.Equal("Update files", cut.Find(".update-dialog-apply").TextContent.Trim());
    }

    /// <summary>A copy edited on this computer shows the warning, and the button reads Update and replace my edits.</summary>
    [Fact]
    public async Task ModifiedLocally_WarnsAndRenamesTheButton()
    {
        await this.fixture.SeedAsync();
        File.AppendAllText(Path.Combine(this.fixture.Host.Settings.SkillsDir, "docx", "SKILL.md"), "My edit.\n");
        await Eventually.UntilAsync(() => this.fixture.Host.Skills.Get("docx")?.ModifiedLocally == true, Xunit.TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        await using MudBunitContext ctx = this.fixture.Ui.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx);

        Assert.Equal("You edited docx on this computer. Updating replaces your edits.", cut.Find(".update-dialog-modified").TextContent.Trim());
        Assert.Equal("Update and replace my edits", cut.Find(".update-dialog-apply").TextContent.Trim());
    }

    /// <summary>A Pack whose Source deprecated it says so on top, with the Source's reason.</summary>
    [Fact]
    public async Task DeprecatedSource_SaysSoOnTop()
    {
        await this.fixture.SeedAsync();
        this.fixture.Host.Source("deprecated");
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.Ui.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx);

        Assert.Equal("local/test marks this Pack Deprecated: Because.", cut.Find(".update-dialog-status").TextContent.Trim());
    }

    /// <summary>An empty plan disables the button.</summary>
    [Fact]
    public async Task EmptyPlan_Disabled()
    {
        await this.fixture.SeedAsync(addSheets: false, changeDocx: false, holder: false);
        await using MudBunitContext ctx = this.fixture.Ui.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx);

        Assert.Equal("Nothing to update.", cut.Find(".update-dialog-empty").TextContent.Trim());
        Assert.True(cut.Find(".update-dialog-apply").HasAttribute("disabled"));
    }

    /// <summary>Not now writes nothing: Jane's file and the copy are as they were.</summary>
    [Fact]
    public async Task NotNow_WritesNothing()
    {
        await this.fixture.SeedAsync();
        string jane = this.fixture.Host.Personas.Get("Jane")?.Text ?? string.Empty;
        string copy = File.ReadAllText(Path.Combine(this.fixture.Host.Settings.SkillsDir, "docx", "SKILL.md"));
        await using MudBunitContext ctx = this.fixture.Ui.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx);

        await cut.InvokeAsync(() => cut.Find(".update-dialog-not-now").ClickAsync());

        Assert.Equal(jane, this.fixture.Host.Personas.Get("Jane")?.Text);
        Assert.Equal(copy, File.ReadAllText(Path.Combine(this.fixture.Host.Settings.SkillsDir, "docx", "SKILL.md")));
    }

    /// <summary>Update copies the files first, then changes the ticked Teammate: the copy has the new files and Jane holds
    /// sheets at the new version.</summary>
    [Fact]
    public async Task Update_CopiesThenChangesTeammates()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.Ui.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx);

        await cut.InvokeAsync(() => cut.Find(".update-dialog-apply").ClickAsync());

        Assert.True(File.Exists(Path.Combine(this.fixture.Host.Settings.SkillsDir, "docx", "extra.md")));
        Assert.True(File.Exists(Path.Combine(this.fixture.Host.Settings.SkillsDir, "sheets", "SKILL.md")));
        Assert.True(PersonaFrontmatter.TryReadIdentity(this.fixture.Host.Personas.Get("Jane")?.Text ?? string.Empty, out PersonaIdentity? identity, out _));
        Assert.Equal(["docx", "sheets"], identity.Skills);
        Assert.Equal([new VersionedRef("documents", "1.1.0")], identity.Packs);
        Assert.Equal("documents title updated for 1 Teammate.", ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Last().Message);
    }

    /// <summary>A copy failure stops the update and leaves every Teammate file untouched.</summary>
    [Fact]
    public async Task Update_CopyFails_NoTeammateChanged()
    {
        await this.fixture.SeedAsync();
        string jane = this.fixture.Host.Personas.Get("Jane")?.Text ?? string.Empty;
        PackUpdater updater = new(this.fixture.Host.Registry, new FailingCopier(), this.fixture.Host.Personas, this.fixture.Host.Log);

        RemovalOutcome outcome = await updater.ApplyAsync(this.fixture.Plan(), ["Jane"], Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(new RemovalOutcome(false, "docx could not be copied: The disk is full. No Teammate was changed."), outcome);
        Assert.Equal(jane, this.fixture.Host.Personas.Get("Jane")?.Text);
    }

    /// <summary>Opens the update card for documents.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenAsync(MudBunitContext ctx)
    {
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(() => SkillDialogs.OpenUpdateAsync(dialogs, "documents title", "documents"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".update-dialog-apply")));
        return cut;
    }

    /// <summary>A copier whose every copy fails.</summary>
    private sealed class FailingCopier : ISkillCopier
    {
        /// <inheritdoc/>
        public Task<CopyResult> CopyAsync(IReadOnlyList<SkillEntry> skills, CancellationToken ct)
        {
            return Task.FromResult(new CopyResult([], [.. skills.Select(static skill => new CopyFailure(skill.Name, CopyFailureKind.Io, "The disk is full."))]));
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<SkillSetupItem>> EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<SkillSetupItem>>([]);
        }
    }
}
