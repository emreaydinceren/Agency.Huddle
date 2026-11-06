using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Agents;

/// <summary>
/// Pins <see cref="BuiltinAgentSeeder"/> against Spec §6.12 and §8.6, and use case U15: an
/// empty Persona library gets a Chief of Staff on startup, carrying the <c>chief-of-staff</c>
/// marker and the <c>team-building</c> Skill. Uses a real <see cref="PersonaStore"/> over a
/// <see cref="TempDataDir"/>, the same style <c>ProposalServiceTests</c> already uses for this
/// layer, rather than a mock.
/// </summary>
public sealed class BuiltinAgentSeederTests
{
    /// <summary>
    /// Spec §6.12's internal flow: with no Persona file on disk, <c>StartAsync</c> writes the
    /// default Chief of Staff, and the resulting entry carries the default Name and Alias, the
    /// <c>_builtin: chief-of-staff</c> marker, and the <c>team-building</c> Skill.
    /// </summary>
    [Fact]
    public async Task Start_EmptyLibrary_WritesChiefOfStaffWithMarkerAndSkill()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry? entry = personas.Entries.SingleOrDefault(e => string.Equals(e.Name, "Chief of Staff", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("cos", entry.Alias);
        Assert.Equal("chief-of-staff", entry.Builtin);
        Assert.Equal(["team-building", "team-rollup", "ingest", "wrap-up", "brain-check"], entry.Skills);
    }

    /// <summary>
    /// Spec §12 F-13: the marker is what the seeder looks for, never the Name. A Persona named
    /// "Alfred" that already carries <c>_builtin: chief-of-staff</c> satisfies the seeder, so
    /// <c>StartAsync</c> writes nothing and the library still holds exactly that one Persona.
    /// </summary>
    [Fact]
    public async Task Start_MarkerUnderOtherName_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Alfred", "Assistant", "alf", Teams: [], Builtin: BuiltinAgent.ChiefOfStaffMarker),
            "You are Alfred, and you already carry the Chief of Staff marker.");
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry entry = Assert.Single(personas.Entries);
        Assert.Equal("Alfred", entry.Name);
        Assert.False(File.Exists(Path.Combine(personas.TeamsDirectory, "Chief of Staff.md")));
    }

    /// <summary>
    /// Spec §12 F-14: a Persona already occupies the default Name "Chief of Staff" but carries no
    /// marker, so it does not satisfy the seeder's check. <c>StartAsync</c> must fall back to the
    /// next free (Name, Alias) pair from Spec §8.6's search and write "Chief of Staff 2" / "cos2"
    /// there, carrying the marker, rather than colliding with the file already on disk.
    /// </summary>
    [Fact]
    public async Task Start_NameTaken_WritesChiefOfStaff2WithAliasCos2()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Someone Else", "boss", Teams: []),
            "You hold the default Name, but not the marker.");
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry? entry = personas.Entries.SingleOrDefault(e => string.Equals(e.Name, "Chief of Staff 2", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("cos2", entry.Alias);
        Assert.Equal("chief-of-staff", entry.Builtin);
    }

    /// <summary>
    /// Issue 390: a blank install's Chief of Staff is created with the cheap model already stored,
    /// so its first greeting does not run on the account's most expensive default.
    /// </summary>
    [Fact]
    public async Task Start_EmptyLibrary_StoresTheCheapModelBeforeAnyTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Equal("haiku", personas.Get("Chief of Staff")?.Model);
    }

    /// <summary>Issue 390: an existing Chief of Staff with a stored model keeps it across a start.</summary>
    [Fact]
    public async Task Start_ExistingChiefWithModel_KeepsItsModel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Chief", "cos", Teams: [], Builtin: BuiltinAgent.ChiefOfStaffMarker),
            "You are the Chief.", model: "opus");
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Equal("opus", personas.Get("Chief of Staff")?.Model);
    }

    /// <summary>Issue 390: an existing Chief of Staff with no stored model is not given one, so an existing install sees no change.</summary>
    [Fact]
    public async Task Start_ExistingChiefWithoutModel_StaysOnAgentDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Chief", "cos", Teams: [], Builtin: BuiltinAgent.ChiefOfStaffMarker),
            "You are the Chief.");
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Null(personas.Get("Chief of Staff")?.Model);
    }

    /// <summary>Issue 390: a model the Human sets, or clears back to the Agent default, survives a restart.</summary>
    [Fact]
    public async Task Start_AfterHumanChangesModel_RestartKeepsTheChoice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string? chosen;
        string? cleared;
        using (PersonaStore first = NewStore(dataDir))
        {
            await new BuiltinAgentSeeder(first, NullLogger<BuiltinAgentSeeder>.Instance).StartAsync(ct);
            Persona chief = first.Get("Chief of Staff") ?? throw new InvalidOperationException("Chief missing.");
            _ = first.Update("Chief of Staff", chief.Text, "sonnet", null, null);
        }

        using (PersonaStore second = NewStore(dataDir))
        {
            await new BuiltinAgentSeeder(second, NullLogger<BuiltinAgentSeeder>.Instance).StartAsync(ct);
            chosen = second.Get("Chief of Staff")?.Model;
            Persona chief = second.Get("Chief of Staff") ?? throw new InvalidOperationException("Chief missing.");
            _ = second.Update("Chief of Staff", chief.Text, null, null, null);
        }

        using (PersonaStore third = NewStore(dataDir))
        {
            await new BuiltinAgentSeeder(third, NullLogger<BuiltinAgentSeeder>.Instance).StartAsync(ct);
            cleared = third.Get("Chief of Staff")?.Model;
        }

        Assert.Equal("sonnet", chosen);
        Assert.Null(cleared);
    }

    /// <summary>Builds a real store over the temp data folder.</summary>
    private static PersonaStore NewStore(TempDataDir dataDir) => new(
        new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);

    /// <summary>
    /// Issue 351: a Human named "Chief of Staff" owns the default Name, so the seeder moves on to
    /// the next free pair ("Chief of Staff 2" / "cos2") instead of failing and leaving no Chief of Staff.
    /// </summary>
    [Fact]
    public async Task Start_HumanIsNamedChiefOfStaff_WritesTheNextPair()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()),
            NullLogger<PersonaStore>.Instance, new ConfiguredHumanIdentity("chief of staff"));
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry entry = Assert.Single(personas.Entries);
        Assert.Equal("Chief of Staff 2", entry.Name);
        Assert.Equal("cos2", entry.Alias);
        Assert.Equal("chief-of-staff", entry.Builtin);
    }

    /// <summary>
    /// Spec §6.12 Constraints: the seeder never reverts an edit. A Chief of Staff file that
    /// already carries the marker, but whose Title and Body a Human has since edited away from
    /// the shipped default, is left byte-for-byte untouched by <c>StartAsync</c> - only an
    /// absent marker ever triggers a write.
    /// </summary>
    [Fact]
    public async Task Start_EditedChiefOfStaff_NotReverted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Edited Title", "cos", Teams: [], Builtin: BuiltinAgent.ChiefOfStaffMarker),
            "This Body was hand-edited and no longer matches the shipped default.");
        var path = personas.PathFor("Chief of Staff");
        var before = File.ReadAllText(path);
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Equal(before, File.ReadAllText(path));
    }

    /// <summary>
    /// Spec §12 F-15: two Personas under different Names both carry the marker - an ordinary
    /// situation the seeder is satisfied by (one marked entry is enough), not a case it
    /// reconciles down to one. <c>StartAsync</c> writes nothing and the library still holds
    /// exactly those two.
    /// </summary>
    [Fact]
    public async Task Start_TwoFilesWithMarker_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Chief of Staff", "cos", Teams: [], Builtin: BuiltinAgent.ChiefOfStaffMarker),
            "The original.");
        personas.Add(
            new PersonaIdentity("Backup Chief of Staff", "Chief of Staff", "backup-cos", Teams: [], Builtin: BuiltinAgent.ChiefOfStaffMarker),
            "A hand-made copy.");
        var countBefore = personas.Entries.Count;
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Equal(countBefore, personas.Entries.Count);
    }

    /// <summary>
    /// Spec §8.7: a failed write logs an Error and lets startup continue - the app is usable
    /// without the Chief of Staff. Sabotages the Teams directory itself (replaced with a plain
    /// file) so that <see cref="PersonaStore.Add"/>'s own <c>Directory.CreateDirectory</c> step
    /// fails for whichever (Name, Alias) pair Spec §8.6's free-name search settles on - a genuine
    /// write failure, not a naming collision the search would simply route around.
    /// </summary>
    [Fact]
    public async Task Start_WriteFails_LogsAndDoesNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        var teamsDir = personas.TeamsDirectory;
        Directory.Delete(teamsDir, recursive: true);
        File.WriteAllText(teamsDir, "A plain file occupying where the Teams directory should be.");
        RecordingLogger<BuiltinAgentSeeder> logger = new();
        BuiltinAgentSeeder seeder = new(personas, logger);

        var exception = await Record.ExceptionAsync(() => seeder.StartAsync(ct));

        Assert.Null(exception);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    /// <summary>
    /// Spec §6.15: the seeder writes under the ADR-0031 per-agent folder layout -
    /// <c>Agents/{Name}/{Name}.md</c> - not a flat <c>Agents/{Name}.md</c>, and it never
    /// creates the Work Dir sub-folder itself; only a session actually running under that
    /// Agent does.
    /// </summary>
    [Fact]
    public async Task Seed_WritesIntoAgentFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        AgentPaths paths = new(dataDir.Options());
        using PersonaStore personas = new(
            paths, new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.True(File.Exists(paths.DefinitionFile("Chief of Staff")));
        Assert.False(Directory.Exists(paths.WorkDir("Chief of Staff")));
    }

    /// <summary>
    /// Spec §6.15 / corrections-B2 #31: the free-name search checks the definition file through
    /// <see cref="AgentPaths.DefinitionFile"/> - a file left on disk at
    /// <c>Agents/Chief of Staff/Chief of Staff.md</c> that no loaded <see cref="PersonaEntry"/>
    /// knows about (never scanned - written straight to disk after the store already started)
    /// still makes the default (Name, Alias) pair taken, so <c>StartAsync</c> falls back to
    /// "Chief of Staff 2" / "cos2".
    /// </summary>
    [Fact]
    public async Task Seed_NameTaken_ChecksAgentFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        AgentPaths paths = new(dataDir.Options());
        using PersonaStore personas = new(
            paths, new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        Directory.CreateDirectory(paths.AgentFolder("Chief of Staff"));
        File.WriteAllText(paths.DefinitionFile("Chief of Staff"), "An orphan file the store never scanned.");
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry? entry = personas.Entries.SingleOrDefault(e => string.Equals(e.Name, "Chief of Staff 2", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("cos2", entry.Alias);
    }

    /// <summary>
    /// corrections-B2 #31: a candidate's folder that already holds any Markdown file - not
    /// necessarily one named exactly <c>{Name}.md</c> - counts as taken too, or two differently
    /// named Markdown files could both be rejected as "not the definition file" and collide in
    /// the same folder.
    /// </summary>
    [Fact]
    public async Task Seed_FolderHoldsOtherMarkdown_CandidateTaken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        AgentPaths paths = new(dataDir.Options());
        using PersonaStore personas = new(
            paths, new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        Directory.CreateDirectory(paths.AgentFolder("Chief of Staff"));
        File.WriteAllText(Path.Combine(paths.AgentFolder("Chief of Staff"), "notes.md"), "Some other Markdown file, not the definition file.");
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry? entry = personas.Entries.SingleOrDefault(e => string.Equals(e.Name, "Chief of Staff 2", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("cos2", entry.Alias);
    }
}
