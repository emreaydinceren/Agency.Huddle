using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Agency.Huddle.Tests.Ui;
using MudBlazor;
using Severity = Agency.Huddle.SkillPacks.Checking.Severity;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.SkillPacks.EndToEnd;

/// <summary>The end-to-end flow of the Skill Packs spec (§13, task P32): a fake AI tool follows <c>InstallSkillPack.md</c>, and
/// the real watcher, registry, copier and updater react on a real disk.</summary>
public sealed class SkillPacksEndToEndTests
{
    /// <summary>The Pack every test adds, and its first version.</summary>
    private const string PackId = "documents";

    /// <summary>The Teammate who chooses the Pack.</summary>
    private const string Holder = "Jane";

    /// <summary>The Pack once it is resolved, or null.</summary>
    private static PackEntry? Pack(SkillPacksWorld world) => world.Registry.Current.Resolved.FindPack(PackId);

    /// <summary>The Pack's display state, or null while it is not resolved.</summary>
    private static ItemState? StateOf(SkillPacksWorld world) => Pack(world) is { } pack ? world.Registry.DisplayState(pack) : null;

    /// <summary>The Teammate's identity read from their file.</summary>
    private static PersonaIdentity IdentityOf(SkillPacksWorld world)
    {
        Persona persona = world.Host.Personas.Get(Holder) ?? throw new InvalidOperationException($"{Holder} is missing");
        Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out string error), error);
        return identity;
    }

    /// <summary>The AI tool adds the Pack, and the registry has read it as usable.</summary>
    private static async Task<FakeAiTool> AddPackAsync(SkillPacksWorld world, params string[] skills)
    {
        FakeAiTool tool = world.AiTool();
        (int exit, string output) = tool.AddPack(PackId, "1.0.0", skills);
        Assert.True(exit == 0, output);
        await world.SettleUntilAsync(() => world.Registry.Current.Checked.StateOf($"pack:{PackId}") == ItemCheckState.Usable, "the Pack is usable");
        return tool;
    }

    /// <summary>What the Teammate card does when the Pack chip is set and the card saved: copy every Skill, then write the
    /// Teammate's <c>skills</c>, <c>_packs</c> and <c>consult_when</c>.</summary>
    private static async Task ChoosePackAsync(SkillPacksWorld world)
    {
        PackEntry pack = Pack(world) ?? throw new InvalidOperationException("the Pack is not resolved");
        IReadOnlyList<SkillEntry> entries = [.. pack.SkillNames.Select(name => world.Registry.Current.Resolved.FindSkill(name) ?? throw new InvalidOperationException(name))];

        CopyResult copied = await world.Copier.CopyAsync(entries, TestContext.Current.CancellationToken);

        Assert.True(copied.Ok);
        await world.SettleUntilAsync(() => pack.SkillNames.All(name => world.Host.Skills.Get(name) is not null), "the copies in the Skill store");
        Persona persona = world.Host.Personas.Get(Holder) ?? throw new InvalidOperationException($"{Holder} is missing");
        _ = world.Host.Personas.Update(Holder, PackMembership.Add(persona.Text, PackContents.From(pack)), persona.Model, persona.Effort, persona.WorkMode);
    }

    /// <summary>U1: the AI tool reads the install file, writes a Pack where it says and validates with no errors; without a
    /// reload the Pack shows New and one "ready" snackbar names it.</summary>
    [Fact]
    public async Task U1_AiToolAddsPack_PackIsNewAndSnackbarShown()
    {
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync();
        await using MudBunitContext ctx = world.Ui.NewContext();
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        _ = ctx.Render<SkillArrivalWatcher>();
        FakeAiTool tool = world.AiTool();

        (int exit, string output) = tool.AddPack(PackId, "1.0.0", ["docx", "pdf"]);

        Assert.True(exit == 0, output);
        Assert.Equal(world.Host.Dir, tool.SkillSourcesDir);
        await world.SettleUntilAsync(() => snackbar.ShownSnackbars.Any(), "the ready snackbar");
        Assert.Equal(ItemState.New, StateOf(world));
        Assert.Equal("Documents is ready", Assert.Single(snackbar.ShownSnackbars).Message);
    }

    /// <summary>U2: choosing the Pack copies both Skills with their provenance before the Teammate's file changes, then the
    /// file lists the Skills, the Pack and its phrase, and the registry counts the Teammate as a holder.</summary>
    [Fact]
    public async Task U2_ChoosePack_CopiesThenChangesTeammate()
    {
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync();
        _ = await AddPackAsync(world, "docx", "pdf");
        world.Host.Teammate(Holder, [], []);
        PackEntry pack = Pack(world) ?? throw new InvalidOperationException("the Pack is not resolved");
        IReadOnlyList<SkillEntry> entries = [.. pack.SkillNames.Select(name => world.Registry.Current.Resolved.FindSkill(name) ?? throw new InvalidOperationException(name))];

        CopyResult copied = await world.Copier.CopyAsync(entries, TestContext.Current.CancellationToken);
        PersonaIdentity before = IdentityOf(world);
        Persona persona = world.Host.Personas.Get(Holder) ?? throw new InvalidOperationException($"{Holder} is missing");
        _ = world.Host.Personas.Update(Holder, PackMembership.Add(persona.Text, PackContents.From(pack)), persona.Model, persona.Effort, persona.WorkMode);

        Assert.True(copied.Ok);
        Assert.Empty(before.Skills ?? []);
        foreach (string name in new[] { "docx", "pdf" })
        {
            Assert.True(SkillProvenance.TryRead(Path.Combine(world.Host.Settings.SkillsDir, name), out SkillProvenance? provenance));
            Assert.Equal("local/test", provenance?.Source);
        }

        PersonaIdentity after = IdentityOf(world);
        Assert.Equal(["docx", "pdf"], after.Skills);
        Assert.Equal([new VersionedRef(PackId, "1.0.0")], after.Packs);
        Assert.Contains("consult_when: 'office documents'", File.ReadAllLines(world.Host.Personas.PathFor(Holder)));
        await world.SettleUntilAsync(() => world.Registry.HolderNames(pack).Count == 1, "the holder count");
        Assert.Equal([Holder], world.Registry.HolderNames(pack));
    }

    /// <summary>U3: a Skill file that changes in the clone marks the Pack Update available and changes nothing else: the copy
    /// and the Teammate's file stay as they were.</summary>
    [Fact]
    public async Task U3_PointerChanges_ShowsUpdateAvailable_NothingElseChanges()
    {
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync();
        FakeAiTool tool = await AddPackAsync(world, "docx");
        world.Host.Teammate(Holder, [], []);
        await ChoosePackAsync(world);
        string copy = Path.Combine(world.Host.Settings.SkillsDir, "docx", "SKILL.md");
        string copyBefore = File.ReadAllText(copy);
        string teammateBefore = File.ReadAllText(world.Host.Personas.PathFor(Holder));

        tool.ChangeSkill("docx", "A new paragraph.");

        await world.SettleUntilAsync(() => StateOf(world) == ItemState.UpdateAvailable, "Update available");
        Assert.Equal(copyBefore, File.ReadAllText(copy));
        Assert.Equal(teammateBefore, File.ReadAllText(world.Host.Personas.PathFor(Holder)));
    }

    /// <summary>U4: the AI tool updates the Pack (a changed Skill and a new one); Update copies the files first, then takes
    /// the ticked Teammate to the new list and version, and the Pack is back in use.</summary>
    [Fact]
    public async Task U4_ApplyUpdate_CopiesFilesThenMembership()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync();
        FakeAiTool tool = await AddPackAsync(world, "docx");
        world.Host.Teammate(Holder, [], []);
        await ChoosePackAsync(world);
        tool.ChangeSkill("docx", "A new paragraph.");
        (int exit, string output) = tool.UpdatePack(PackId, "2.0.0", ["docx", "pdf"]);
        Assert.True(exit == 0, output);
        await world.SettleUntilAsync(() => StateOf(world) == ItemState.UpdateAvailable && Pack(world)?.Version == "2.0.0", "the 2.0.0 update");
        Persona holder = world.Host.Personas.Get(Holder) ?? throw new InvalidOperationException($"{Holder} is missing");
        PackUpdatePlan plan = PackUpdatePlanner.Plan(PackId, world.Registry, world.Host.Skills, [holder], PhysicalFileReader.Instance);

        RemovalOutcome outcome = await world.Updater.ApplyAsync(plan, [Holder], ct);

        Assert.True(outcome.Ok, outcome.Message);
        Assert.Equal(["docx"], plan.FileChanges.Select(static change => change.Name));
        Assert.Equal(["pdf"], plan.Added.Select(static change => change.Name));
        Assert.Equal(File.ReadAllText(Path.Combine(world.Host.Clone, "skills", "docx", "SKILL.md")), File.ReadAllText(Path.Combine(world.Host.Settings.SkillsDir, "docx", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(world.Host.Settings.SkillsDir, "pdf", "SKILL.md")));
        PersonaIdentity after = IdentityOf(world);
        Assert.Equal(["docx", "pdf"], after.Skills);
        Assert.Equal([new VersionedRef(PackId, "2.0.0")], after.Packs);
        await world.SettleUntilAsync(() => StateOf(world) == ItemState.InUse, "In use after the update");
    }

    /// <summary>U5: on a first start the move writes six catalogs holding 263 Skills and the two Packs from the skills
    /// repository, before the first read; nothing it wrote shows New.</summary>
    [Fact]
    public async Task U5_FirstStart_MoveWritesSixCatalogs263Skills_NothingNew()
    {
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync(runMove: true);

        ResolvedRegistry resolved = world.Registry.Current.Resolved;
        Assert.Equal(6, resolved.Catalogs.Count);
        Assert.Equal(263, resolved.Catalogs.Sum(static catalog => catalog.SkillNames.Count));
        Assert.Equal(["code-review-and-quality", "msbuild-authoring"], resolved.Packs.Select(static pack => pack.Id).Order(StringComparer.Ordinal));
        Assert.Empty(world.Registry.Current.NewKeys);
        Assert.True(File.Exists(Path.Combine(world.Host.Dir, "SKILLS.md")));
    }

    /// <summary>U9: a pack file caught half-written is not reported Broken while it is inside its grace; once the AI tool
    /// finishes it, the Pack is usable, and no published picture ever showed it Broken.</summary>
    [Fact]
    public async Task U9_HalfWrittenPackFile_NoBrokenFlash()
    {
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync(quietFiles: false);
        FakeAiTool tool = world.AiTool();
        // Changed is raised from the registry's own thread, which may still publish while the asserts enumerate: a List throws "Collection was modified" there.
        System.Collections.Concurrent.ConcurrentQueue<RegistrySnapshot> published = [];
        world.Registry.Changed += change => published.Enqueue(change.Snapshot);
        _ = world.Host.CloneSkill("docx");
        string packFile = RegistryHost.Write(tool.SkillSourcesDir, $"packs/{PackId}.skillpack.json", "{ \"schemaVersion\": 1, \"id\": \"documents\", \"vers");
        File.WriteAllText(tool.RegistryPath, $"# Skills registry\n\n## Packs\n\n| Local path |\n| --- |\n| packs/{PackId}.skillpack.json |\n\n## Catalogs\n\n| Local path |\n| --- |\n");
        long start = world.Registry.Current.Version;
        await world.SettleUntilAsync(() => world.Registry.Current.Version > start, "a read of the half-written file");

        (int exit, string output) = tool.AddPack(PackId, "1.0.0", ["docx"]);

        Assert.True(exit == 0, output);
        await world.SettleUntilAsync(() => world.Registry.Current.Checked.StateOf($"pack:{PackId}") == ItemCheckState.Usable, "the finished Pack");
        Assert.DoesNotContain(published, snapshot => snapshot.Checked.States.Values.Contains(ItemCheckState.Broken));
        Assert.DoesNotContain(published, snapshot => snapshot.Checked.Findings.Any(finding => finding.Severity == Severity.Error && string.Equals(finding.File, packFile, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>U13: a withdrawn Source makes the Pack Withdrawn, so a new hire needs setup and cannot take it, while the
    /// Teammate who holds it keeps the Skills, their copies and their file.</summary>
    [Fact]
    public async Task U13_WithdrawnSource_BlocksNewHire_HoldersKeepWorking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync();
        FakeAiTool tool = await AddPackAsync(world, "docx", "pdf");
        world.Host.Teammate(Holder, [], []);
        await ChoosePackAsync(world);
        string teammateBefore = File.ReadAllText(world.Host.Personas.PathFor(Holder));

        tool.Withdraw("Licence changed.");

        await world.SettleUntilAsync(() => StateOf(world) == ItemState.Withdrawn, "Withdrawn");
        SkillSetupItem blocked = Assert.Single(SkillSetupCheck.For([], [new VersionedRef(PackId, "1.0.0")], world.Registry));
        Assert.Equal(SkillAvailability.Withdrawn, blocked.Problem);
        Assert.Equal("Licence changed.", blocked.Detail);
        Assert.Empty(await world.Copier.EnsureAvailableAsync(["docx", "pdf"], ct));
        Assert.True(File.Exists(Path.Combine(world.Host.Settings.SkillsDir, "docx", "SKILL.md")));
        Assert.Equal(teammateBefore, File.ReadAllText(world.Host.Personas.PathFor(Holder)));
    }
}
