using System.Text.Json;
using Agency.Huddle.App.SkillSources.Move;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;
using CheckRunner = Agency.Huddle.SkillPacks.Checking.Checks;

namespace Agency.Huddle.Tests.SkillPacks.Move;

/// <summary>Pins when the move runs (§6.13, "Idempotence and sharing").</summary>
public sealed class MoveDetectionTests
{
    /// <summary>A first start writes SKILLS.md and returns a report; a second start returns null and changes nothing.</summary>
    [Fact]
    public async Task RunAsync_FirstStartThenSecond_WritesOnceThenSkips()
    {
        using MoveFixture fixture = new();

        MoveReport? first = await fixture.RunAsync();
        SortedDictionary<string, string> tree = MoveFixture.Tree(fixture.Dir);
        MoveReport? second = await fixture.RunAsync();

        Assert.NotNull(first);
        Assert.False(first.Failed);
        Assert.True(File.Exists(Path.Combine(fixture.Dir, "SKILLS.md")));
        Assert.Null(second);
        Assert.Equal(tree, MoveFixture.Tree(fixture.Dir));
    }

    /// <summary>When SKILLS.md exists the move reads nothing, not even the skills repository folder, and writes nothing.</summary>
    [Fact]
    public async Task RunAsync_SkillsMdExists_TouchesNothing()
    {
        using MoveFixture fixture = new();
        Directory.CreateDirectory(fixture.Dir);
        File.WriteAllText(Path.Combine(fixture.Dir, "SKILLS.md"), "mine");
        fixture.WithRepository(Path.Combine(fixture.DataDir, "no-such-folder"));

        MoveReport? report = await fixture.RunAsync();

        Assert.Null(report);
        Assert.Equal(["SKILLS.md"], MoveFixture.Tree(fixture.Dir).Keys);
        Assert.False(File.Exists(Path.Combine(fixture.DataDir, MoveReportStore.FileName)));
    }

    /// <summary>Deleting SKILLS.md by hand makes the move run again, keeping the files that exist.</summary>
    [Fact]
    public async Task RunAsync_SkillsMdDeletedByHand_RunsAgain()
    {
        using MoveFixture fixture = new();
        await fixture.RunAsync();
        string catalog = Path.Combine(fixture.Dir, "catalogs", "dotnet-skills.skillcatalog.json");
        File.WriteAllText(catalog, "{ \"edited\": true }");
        File.Delete(Path.Combine(fixture.Dir, "SKILLS.md"));

        MoveReport? report = await fixture.RunAsync();

        Assert.NotNull(report);
        Assert.Equal("{ \"edited\": true }", File.ReadAllText(catalog));
        Assert.True(File.Exists(Path.Combine(fixture.Dir, "SKILLS.md")));
        Assert.Contains("kept-existing", File.ReadAllText(Path.Combine(fixture.Dir, "sources", "github.com", "dotnet", "skills", "log.jsonl")), StringComparison.Ordinal);
    }
}

/// <summary>Pins crash safety (§6.13): stage, place, SKILLS.md last, and stale staging cleaned.</summary>
public sealed class MoveCrashSafetyTests
{
    /// <summary>Placement that throws on its Nth call.</summary>
    private sealed class FailingMoveFiles(int failAt) : IMoveFiles
    {
        private int calls;

        /// <summary>Every placement, in order.</summary>
        public List<(string To, bool Overwrite)> Placed { get; } = [];

        /// <inheritdoc/>
        public void Place(string from, string to, bool overwrite)
        {
            if (++this.calls == failAt)
            {
                throw new IOException("Simulated crash.");
            }

            this.Placed.Add((to, overwrite));
            new PhysicalMoveFiles().Place(from, to, overwrite);
        }
    }

    /// <summary>A crash at any placement leaves no SKILLS.md; a rerun gives the same tree as an uninterrupted run.</summary>
    [Fact]
    public async Task RunAsync_CrashAtEachPlacement_NoSkillsMdAndRerunMatches()
    {
        using MoveFixture clean = new();
        await clean.RunAsync();
        SortedDictionary<string, string> expected = MoveFixture.Tree(clean.Dir, static path => path.EndsWith("log.jsonl", StringComparison.Ordinal));

        foreach (int failAt in new[] { 1, 3, 8 })
        {
            using MoveFixture crashed = new();
            MoveReport? report = await crashed.RunAsync(new FailingMoveFiles(failAt));

            Assert.True(report?.Failed);
            Assert.False(File.Exists(Path.Combine(crashed.Dir, "SKILLS.md")));
            await crashed.RunAsync();
            Assert.Equal(expected.Keys, MoveFixture.Tree(crashed.Dir, static path => path.EndsWith("log.jsonl", StringComparison.Ordinal)).Keys);
        }
    }

    /// <summary>SKILLS.md is placed last, without overwrite.</summary>
    [Fact]
    public async Task RunAsync_SkillsMdPlacedLastWithoutOverwrite()
    {
        using MoveFixture fixture = new();
        FailingMoveFiles files = new(int.MaxValue);

        await fixture.RunAsync(files);

        (string to, bool overwrite) = files.Placed[^1];
        Assert.EndsWith("SKILLS.md", to, StringComparison.Ordinal);
        Assert.False(overwrite);
    }

    /// <summary>A <c>.move-*</c> folder older than ten minutes is deleted; a young one is left.</summary>
    [Fact]
    public async Task RunAsync_StaleStagingCleaned_YoungKept()
    {
        using MoveFixture fixture = new();
        string old = Directory.CreateDirectory(Path.Combine(fixture.Dir, ".move-aaaaaaaa")).FullName;
        string young = Directory.CreateDirectory(Path.Combine(fixture.Dir, ".move-bbbbbbbb")).FullName;
        Directory.SetLastWriteTimeUtc(old, fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-20));
        Directory.SetLastWriteTimeUtc(young, fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1));

        await fixture.RunAsync();

        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(young));
    }

    /// <summary>When another Huddle placed SKILLS.md first, this move discards its staging and reports nothing.</summary>
    [Fact]
    public async Task RunAsync_OtherHuddleWinsRace_DiscardsStaging()
    {
        using MoveFixture fixture = new();
        RacingMoveFiles files = new(Path.Combine(fixture.Dir, "SKILLS.md"));

        MoveReport? report = await fixture.RunAsync(files);

        Assert.Null(report);
        Assert.Equal("theirs", File.ReadAllText(Path.Combine(fixture.Dir, "SKILLS.md")));
        Assert.Empty(Directory.GetDirectories(fixture.Dir, ".move-*"));
    }

    /// <summary>Placement where another Huddle writes SKILLS.md just before this one commits.</summary>
    private sealed class RacingMoveFiles(string skillsMd) : IMoveFiles
    {
        /// <inheritdoc/>
        public void Place(string from, string to, bool overwrite)
        {
            if (string.Equals(to, skillsMd, StringComparison.Ordinal))
            {
                File.WriteAllText(skillsMd, "theirs");
            }

            new PhysicalMoveFiles().Place(from, to, overwrite);
        }
    }
}

/// <summary>Pins the split of the old catalog (§6.13, Steps 1 and 2).</summary>
public sealed class CatalogSplitTests
{
    /// <summary>Six catalogs with 106/61/37/25/20/14 Skills: 263, with the two duplicates omitted and logged.</summary>
    [Fact]
    public async Task Split_SixCatalogs_263Skills()
    {
        using MoveFixture fixture = new();

        MoveReport? report = await fixture.RunAsync();

        Dictionary<string, int> counts = [];
        foreach (string file in Directory.GetFiles(Path.Combine(fixture.Dir, "catalogs")))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            counts[document.RootElement.GetProperty("id").GetString() ?? string.Empty] = document.RootElement.GetProperty("skills").GetArrayLength();
        }

        Assert.Equal(new Dictionary<string, int>
        {
            ["dotnet-skills"] = 106,
            ["garrytan-gstack"] = 61,
            ["mattpocock-skills"] = 37,
            ["addyosmani-agent-skills"] = 25,
            ["anthropics-skills"] = 20,
            ["obra-superpowers"] = 14,
        }, counts);
        Assert.Equal(263, report?.CatalogSkills);
        Assert.Equal(2, report?.OmittedDuplicates);
        Assert.Contains("duplicate-omitted", File.ReadAllText(Path.Combine(fixture.Dir, "sources", "github.com", "mattpocock", "skills", "log.jsonl")), StringComparison.Ordinal);
        Assert.Contains("retro (kept in garrytan-gstack)", File.ReadAllText(Path.Combine(fixture.Dir, "sources", "github.com", "mattpocock", "skills", "NOTES.md")), StringComparison.Ordinal);
    }

    /// <summary>An entry keeps its fields, gets its origin from the old link and an empty path; its Source has no commit.</summary>
    [Fact]
    public async Task Split_FieldMap_AndSourceRecord()
    {
        using MoveFixture fixture = new();

        await fixture.RunAsync();

        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Dir, "catalogs", "dotnet-skills.skillcatalog.json")));
        JsonElement entry = catalog.RootElement.GetProperty("skills").EnumerateArray().Single(static item => item.GetProperty("name").GetString() == "target-authoring");
        Assert.Equal("github.com/dotnet/skills", catalog.RootElement.GetProperty("source").GetString());
        Assert.Equal("https://github.com/dotnet/skills/tree/a660de83c2f9cad071a5d568f7d77041ccd07059/plugins/dotnet-msbuild/skills/target-authoring", entry.GetProperty("origin").GetString());
        Assert.Equal(string.Empty, entry.GetProperty("path").GetString());
        Assert.Equal("MSBuild Authoring", entry.GetProperty("subCategory").GetString());
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Dir, "sources", "github.com", "dotnet", "skills", "source.json")));
        Assert.False(source.RootElement.TryGetProperty("commit", out _));
        Assert.Equal("Huddle catalog", source.RootElement.GetProperty("fetchedBy").GetString());
        Assert.StartsWith("---\nlayout: huddle-catalog\n", File.ReadAllText(Path.Combine(fixture.Dir, "sources", "github.com", "dotnet", "skills", "NOTES.md")), StringComparison.Ordinal);
    }

    /// <summary>The Human's <c>SkillsCatalog.json</c>, when it parses, replaces the embedded catalog entirely.</summary>
    [Fact]
    public async Task Split_UserOverride_ReplacesEmbedded()
    {
        using MoveFixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.DataDir, "SkillsCatalog.json"), """
            [{ "id": "a/b/c", "name": "only-one", "description": "d", "publisher": "p", "repo": "https://github.com/a/b", "path": "c", "link": "https://github.com/a/b/tree/0123456789abcdef0123456789abcdef01234567/c", "commit": "0123456789abcdef0123456789abcdef01234567", "license": "MIT" }]
            """);

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(1, report?.Catalogs);
        Assert.Equal(["a-b.skillcatalog.json"], Directory.GetFiles(Path.Combine(fixture.Dir, "catalogs")).Select(Path.GetFileName));
    }
}

/// <summary>Pins the rows for the two Packs Huddle used to ship (§6.13, Step 3).</summary>
public sealed class ShippedPackRowsTests
{
    /// <summary>With no skills repository there is no row for either Pack and no <c>sources/local/skills</c>.</summary>
    [Fact]
    public async Task NoRepository_NoRows()
    {
        using MoveFixture fixture = new();

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(0, report?.ShippedPackRows);
        Assert.False(Directory.Exists(Path.Combine(fixture.Dir, "sources", "local", "skills")));
    }

    /// <summary>A repository that does not exist is a problem, not a failure.</summary>
    [Fact]
    public async Task MissingRepository_Problem()
    {
        using MoveFixture fixture = new();
        fixture.WithRepository(Path.Combine(fixture.DataDir, "absent"));

        MoveReport? report = await fixture.RunAsync();

        Assert.False(report?.Failed);
        Assert.Contains(report?.Problems ?? [], static problem => problem.Message.EndsWith("does not exist.", StringComparison.Ordinal));
    }

    /// <summary>With the fixture repository: one row per Pack with a DESK-01 entry, and the notes copied byte for byte.</summary>
    [Fact]
    public async Task FixtureRepository_TwoRowsAndNotes()
    {
        using MoveFixture fixture = new();
        fixture.WithRepository(MoveFixture.FixtureRepository);

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(2, report?.ShippedPackRows);
        Registry registry = RegistryReader.Read(File.ReadAllText(Path.Combine(fixture.Dir, "SKILLS.md")), fixture.Dir, "DESK-01");
        Assert.Equal([Path.Combine(MoveFixture.FixtureRepository, "packs", "msbuild-authoring.skillpack.json"), Path.Combine(MoveFixture.FixtureRepository, "packs", "code-review-and-quality.skillpack.json")], registry.Packs.Select(static row => row.LocalPath.Resolve("DESK-01")));
        Assert.Null(registry.Packs[0].LocalPath.Resolve("LAPTOP"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(MoveFixture.FixtureRepository, "NOTES.md")), File.ReadAllBytes(Path.Combine(fixture.Dir, "sources", "local", "skills", "NOTES.md")));
    }
}

/// <summary>Pins the Human's own Packs (§6.13, Step 3).</summary>
public sealed class UserPacksMoveTests
{
    /// <summary>An old Pack becomes a new one in <c>local/my-packs</c>, version kept, a catalog Skill named by catalog; a Pack
    /// with a built-in Skill is skipped; the old folder is untouched.</summary>
    [Fact]
    public async Task UserPack_MovedAndMapped()
    {
        using MoveFixture fixture = new();
        const string mine = """{ "id": "my-pack", "version": "2.1.0", "title": "Mine", "appliesTo": ["developer"], "skills": [{ "name": "target-authoring", "source": "dotnet/skills", "path": "plugins/dotnet-msbuild/skills/target-authoring" }, { "name": "elsewhere", "source": "x/y", "path": "z" }] }""";
        fixture.UserPack("my-pack.json", mine);
        fixture.UserPack("bad.json", """{ "id": "bad", "version": "1", "title": "Bad", "skills": [{ "name": "team-building", "source": "builtin" }] }""");

        MoveReport? report = await fixture.RunAsync();

        using JsonDocument pack = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Dir, "packs", "my-pack.skillpack.json")));
        Assert.Equal("local/my-packs", pack.RootElement.GetProperty("source").GetString());
        Assert.Equal("2.1.0", pack.RootElement.GetProperty("version").GetString());
        JsonElement[] skills = [.. pack.RootElement.GetProperty("skills").EnumerateArray()];
        Assert.Equal("dotnet-skills", skills[0].GetProperty("catalog").GetString());
        Assert.Equal("https://github.com/x/y/tree/HEAD/z", skills[1].GetProperty("origin").GetString());
        Assert.Equal(1, report?.UserPacksMoved);
        Assert.Contains(report?.Problems ?? [], static problem => problem.Message == "bad.json: Skill 'team-building' is built in and cannot be in a Pack.");
        Assert.Equal(mine, File.ReadAllText(Path.Combine(fixture.DataDir, "SkillPacks", "my-pack.json")));
    }

    /// <summary>A user Pack with a shipped id gets the only row; the shipped one gets none.</summary>
    [Fact]
    public async Task UserPackWithShippedId_OnlyRow()
    {
        using MoveFixture fixture = new();
        fixture.WithRepository(MoveFixture.FixtureRepository);
        fixture.UserPack("msbuild-authoring.json", """{ "id": "msbuild-authoring", "version": "9.0.0", "title": "Mine", "skills": [] }""");

        MoveReport? report = await fixture.RunAsync();

        Registry registry = RegistryReader.Read(File.ReadAllText(Path.Combine(fixture.Dir, "SKILLS.md")), fixture.Dir, "DESK-01");
        Assert.Equal(1, report?.ShippedPackRows);
        Assert.Contains(registry.Packs, static row => row.LocalPath.FirstText == "packs/msbuild-authoring.skillpack.json");
        Assert.DoesNotContain(registry.Packs, static row => row.LocalPath.FirstText.EndsWith(@"msbuild-authoring.skillpack.json", StringComparison.Ordinal) && row.LocalPath.FirstText.Contains("DESK-01", StringComparison.Ordinal));
    }
}

/// <summary>Pins the Human's imported Skills (§6.13, Step 4).</summary>
public sealed class ImportedSkillsMoveTests
{
    /// <summary>An imported Skill is copied to <c>fetched/</c> without <c>.source.json</c> or <c>.git</c>, and its catalog entry points there.</summary>
    [Fact]
    public async Task Imported_CopiedAndPointed()
    {
        using MoveFixture fixture = new();
        fixture.Imported("target-authoring", "https://github.com/dotnet/skills", "plugins/dotnet-msbuild/skills/target-authoring", "a660de83c2f9cad071a5d568f7d77041ccd07059");

        MoveReport? report = await fixture.RunAsync();

        string fetched = Path.Combine(fixture.Dir, "fetched", "target-authoring");
        Assert.True(File.Exists(Path.Combine(fetched, "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(fetched, ".source.json")));
        Assert.False(Directory.Exists(Path.Combine(fetched, ".git")));
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Dir, "catalogs", "dotnet-skills.skillcatalog.json")));
        JsonElement entry = catalog.RootElement.GetProperty("skills").EnumerateArray().Single(static item => item.GetProperty("name").GetString() == "target-authoring");
        Assert.Equal("../fetched/target-authoring", entry.GetProperty("path").GetString());
        Assert.Equal(1, report?.SkillsPointedAt);
    }

    /// <summary>A Skill imported from a repository outside the catalog gets its own catalog, Source and row.</summary>
    [Fact]
    public async Task ImportedOutsideCatalog_OwnCatalog()
    {
        using MoveFixture fixture = new();
        fixture.Imported("my-skill", "https://github.com/someone/stuff", "skills/my-skill", "0123456789abcdef0123456789abcdef01234567");

        await fixture.RunAsync();

        Assert.True(File.Exists(Path.Combine(fixture.Dir, "catalogs", "someone-stuff.skillcatalog.json")));
        Assert.True(File.Exists(Path.Combine(fixture.Dir, "sources", "github.com", "someone", "stuff", "source.json")));
        Assert.Contains("| catalogs/someone-stuff.skillcatalog.json | https://github.com/someone/stuff | 2026-10-17 | Imported by Huddle; 1 Skill, 1 downloaded |", File.ReadAllText(Path.Combine(fixture.Dir, "SKILLS.md")), StringComparison.Ordinal);
    }

    /// <summary>A locally edited Skill stays in place: no copy, empty path, a skill-left line.</summary>
    [Fact]
    public async Task ModifiedSkill_LeftInPlace()
    {
        using MoveFixture fixture = new();
        string folder = fixture.Imported("target-authoring", "https://github.com/dotnet/skills", "plugins/dotnet-msbuild/skills/target-authoring", "a660de83c2f9cad071a5d568f7d77041ccd07059");
        File.AppendAllText(Path.Combine(folder, "SKILL.md"), "edited");

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(1, report?.SkillsLeftInPlace);
        Assert.False(Directory.Exists(Path.Combine(fixture.Dir, "fetched", "target-authoring")));
        Assert.Contains("skill-left", File.ReadAllText(Path.Combine(fixture.Dir, "sources", "github.com", "dotnet", "skills", "log.jsonl")), StringComparison.Ordinal);
    }
}

/// <summary>Pins what the move must never do (§6.13): Skills/ and the old inputs are byte-identical afterwards.</summary>
public sealed class MoveBoundaryTests
{
    /// <summary><c>Skills/</c> and <c>SkillPacks/</c> are unchanged by the move.</summary>
    [Fact]
    public async Task Move_LeavesSkillsAndOldInputsAlone()
    {
        using MoveFixture fixture = new();
        fixture.Imported("target-authoring", "https://github.com/dotnet/skills", "plugins/dotnet-msbuild/skills/target-authoring", "a660de83c2f9cad071a5d568f7d77041ccd07059");
        fixture.UserPack("my-pack.json", """{ "id": "my-pack", "version": "1", "title": "Mine", "skills": [] }""");
        SortedDictionary<string, string> skills = MoveFixture.Tree(fixture.SkillsDir);
        SortedDictionary<string, string> packs = MoveFixture.Tree(Path.Combine(fixture.DataDir, "SkillPacks"));

        await fixture.RunAsync();

        Assert.Equal(skills, MoveFixture.Tree(fixture.SkillsDir));
        Assert.Equal(packs, MoveFixture.Tree(Path.Combine(fixture.DataDir, "SkillPacks")));
    }

    /// <summary>The move's types take no <see cref="HttpClient"/>.</summary>
    [Fact]
    public void Move_TakesNoHttpClient()
    {
        Type[] types = [.. typeof(SkillSourcesMove).Assembly.GetTypes().Where(static type => type.Namespace == typeof(SkillSourcesMove).Namespace)];

        Assert.NotEmpty(types);
        Assert.DoesNotContain(types.SelectMany(static type => type.GetConstructors()).SelectMany(static constructor => constructor.GetParameters()), static parameter => parameter.ParameterType == typeof(HttpClient));
    }
}

/// <summary>Pins that the move's output passes the checks (§6.13, "MoveOutputChecksTests").</summary>
public sealed class MoveOutputChecksTests
{
    /// <summary>The full fixture's output has no error but the expected S010 (its stand-in folder is not the pinned one), no O002 and no D003, and SKILLS.md reads back.</summary>
    [Fact]
    public async Task Move_FullFixture_ProducesNoErrorFindings()
    {
        using MoveFixture fixture = new();
        fixture.WithRepository(MoveFixture.FixtureRepository);
        fixture.Imported("target-authoring", "https://github.com/dotnet/skills", "plugins/dotnet-msbuild/skills/target-authoring", "a660de83c2f9cad071a5d568f7d77041ccd07059", "Microsoft dotnet");
        fixture.UserPack("my-pack.json", """{ "id": "my-pack", "version": "1", "title": "Mine", "appliesTo": ["developer"], "consultWhenAdds": "x", "skills": [{ "name": "target-authoring", "source": "dotnet/skills", "path": "plugins/dotnet-msbuild/skills/target-authoring" }] }""");
        await fixture.RunAsync();
        CheckContext context = new("1.0.0", 1, "DESK-01", fixture.DataDir, fixture.Dir, fixture.SkillsDir,
            [new RoleInfo("developer", "Developer", "d", []), new RoleInfo("release-engineer", "Release engineer", "d", []), new RoleInfo("reviewer", "Reviewer", "d", [])],
            [new InstalledSkill("target-authoring", "https://github.com/dotnet/skills/tree/a660de83c2f9cad071a5d568f7d77041ccd07059/plugins/dotnet-msbuild/skills/target-authoring", "x", false)],
            ["propose_sprint"],
            fixture.Clock.GetLocalNow());

        Registry registry = RegistryReader.Load(Agency.Huddle.SkillPacks.PhysicalFileReader.Instance, fixture.Dir, "DESK-01");
        ResolvedRegistry resolved = RegistryResolver.Resolve(registry, Agency.Huddle.SkillPacks.PhysicalFileReader.Instance, context, new ResolveOptions(IgnoreGrace: true));
        CheckResult result = CheckRunner.Run(resolved, context, new CheckOptions(), Agency.Huddle.SkillPacks.PhysicalFileReader.Instance, TestContext.Current.CancellationToken);

        Assert.Empty(registry.Findings);

        // The fixture's folder is stand-in text, not the real Skill's bytes, and the shipped catalog pins the real
        // folder hash, so the one expected error is S010 for exactly this Skill (the pinned check is live).
        Assert.Single(result.Findings, static finding => finding.Severity == Severity.Error && finding.Code == "S010" && finding.Message.Contains("\"target-authoring\"", StringComparison.Ordinal));
        string[] errors = [.. result.Findings.Where(static finding => finding.Severity == Severity.Error && finding.Code != "S010").Select(static finding => $"{finding.Code} {Path.GetFileName(finding.File)}: {finding.Message}")];
        Assert.True(errors.Length == 0, string.Join("\n", errors));
        Assert.DoesNotContain(result.Findings, static finding => finding.Code is "O002" or "D003");
        Assert.Equal(9, registry.Packs.Count + registry.Catalogs.Count);
    }
}

/// <summary>Pins the move's report (§6.13): the three snackbar texts and that the store gives it out once.</summary>
public sealed class MoveReportTests
{
    /// <summary>The snackbar text follows problems, ignored tools and failure.</summary>
    [Fact]
    public void SnackbarText_ThreeCases()
    {
        MoveReport clean = new(DateTimeOffset.UnixEpoch, false, 6, 263, 2, 2, 0, 1, 0, [], []);

        Assert.Equal("Packs and Skills moved to SKILLS.md", clean.SnackbarText());
        Assert.Equal("Packs and Skills moved to SKILLS.md · 1 could not be moved", (clean with { Problems = [new MoveProblem("a", "b")] }).SnackbarText());
        Assert.Equal("Packs and Skills moved to SKILLS.md · 2 Skills no longer grant tools", (clean with { SkillsWithIgnoredTools = ["x", "y"] }).SnackbarText());
        Assert.StartsWith("Huddle could not move", MoveReport.Failure(DateTimeOffset.UnixEpoch, []).SnackbarText(), StringComparison.Ordinal);
    }

    /// <summary>The store returns the report once, then null, across a new instance.</summary>
    [Fact]
    public async Task ReportStore_TakeUnshown_ReturnsOnceThenNull()
    {
        using MoveFixture fixture = new();
        fixture.UserPack("my-pack.json", MyPackJson);
        await fixture.RunAsync();

        MoveReport? first = new MoveReportStore(fixture.Settings()).TakeUnshown();
        MoveReport? second = new MoveReportStore(fixture.Settings()).TakeUnshown();

        Assert.NotNull(first);
        Assert.Equal(263, first.CatalogSkills);
        Assert.Null(second);
    }

    /// <summary>A blank data folder moves nothing of the Human's, so its report is saved already shown and no snackbar follows.</summary>
    [Fact]
    public async Task BlankFolder_ReportSavedAsShown()
    {
        using MoveFixture fixture = new();

        MoveReport? report = await fixture.RunAsync();

        Assert.NotNull(report);
        Assert.False(report.Failed);
        Assert.Equal(263, report.CatalogSkills);
        Assert.True(File.Exists(Path.Combine(fixture.DataDir, MoveReportStore.FileName)));
        using JsonDocument stored = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.DataDir, MoveReportStore.FileName)));
        Assert.True(stored.RootElement.GetProperty("shown").GetBoolean());
        Assert.Null(new MoveReportStore(fixture.Settings()).TakeUnshown());
    }

    /// <summary>A user Pack that moved leaves the report unshown.</summary>
    [Fact]
    public async Task UserPackMoved_ReportUnshown()
    {
        using MoveFixture fixture = new();
        fixture.UserPack("my-pack.json", MyPackJson);

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(1, report?.UserPacksMoved);
        Assert.NotNull(new MoveReportStore(fixture.Settings()).TakeUnshown());
    }

    /// <summary>The Human's own <c>SkillsCatalog.json</c> leaves the report unshown although every counter but the catalog's stays zero.</summary>
    [Fact]
    public async Task UserCatalog_ReportUnshown()
    {
        using MoveFixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.DataDir, "SkillsCatalog.json"), """
            [{ "id": "a/b/c", "name": "only-one", "description": "d", "publisher": "p", "repo": "https://github.com/a/b", "path": "c", "link": "https://github.com/a/b/tree/0123456789abcdef0123456789abcdef01234567/c", "commit": "0123456789abcdef0123456789abcdef01234567", "license": "MIT" }]
            """);

        MoveReport? report = await fixture.RunAsync();

        Assert.NotNull(report);
        Assert.Equal(1, report.CatalogSkills);
        Assert.Equal(0, report.ShippedPackRows + report.UserPacksMoved + report.SkillsPointedAt + report.SkillsLeftInPlace);
        Assert.Empty(report.Problems);
        Assert.NotNull(new MoveReportStore(fixture.Settings()).TakeUnshown());
    }

    /// <summary>An imported Skill that was pointed at leaves the report unshown.</summary>
    [Fact]
    public async Task ImportedSkill_ReportUnshown()
    {
        using MoveFixture fixture = new();
        fixture.Imported("target-authoring", "https://github.com/dotnet/skills", "plugins/dotnet-msbuild/skills/target-authoring", "a660de83c2f9cad071a5d568f7d77041ccd07059");

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(1, report?.SkillsPointedAt);
        Assert.NotNull(new MoveReportStore(fixture.Settings()).TakeUnshown());
    }

    /// <summary>Shipped Pack rows leave the report unshown.</summary>
    [Fact]
    public async Task ShippedPackRows_ReportUnshown()
    {
        using MoveFixture fixture = new();
        fixture.WithRepository(fixture.CopyOfFixtureRepository());

        MoveReport? report = await fixture.RunAsync();

        Assert.Equal(2, report?.ShippedPackRows);
        Assert.NotNull(new MoveReportStore(fixture.Settings()).TakeUnshown());
    }

    /// <summary>A problem alone, with nothing moved, still leaves the report unshown so the Human reads it.</summary>
    [Fact]
    public async Task ProblemOnly_ReportUnshown()
    {
        using MoveFixture fixture = new();
        string absent = Path.Combine(fixture.DataDir, "absent");
        fixture.WithRepository(absent);

        MoveReport? report = await fixture.RunAsync();

        Assert.NotNull(report);
        Assert.Equal(0, report.ShippedPackRows + report.UserPacksMoved + report.SkillsPointedAt + report.SkillsLeftInPlace);
        MoveProblem problem = Assert.Single(report.Problems);
        Assert.Equal($"Team:SkillsRepositoryDir '{absent}' does not exist.", problem.Message);
        Assert.NotNull(new MoveReportStore(fixture.Settings()).TakeUnshown());
    }

    private const string MyPackJson = """{ "id": "my-pack", "version": "1", "title": "Mine", "skills": [] }""";
}

/// <summary>Pins the SKILLS.md the move writes for the spec's fixture, byte for byte against a reviewed golden file.</summary>
public sealed class SkillsMdGoldenTests
{
    /// <summary>The golden file's path in the source tree, so a missing golden can be reseeded there.</summary>
    private static string GoldenPath([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        return Path.Combine(Path.GetDirectoryName(here) ?? ".", "Golden", "SKILLS.expected.md");
    }

    /// <summary>The fixture of §6.13 gives the golden SKILLS.md, with <c>{{skillsRepo}}</c> standing for the fixture's repository.</summary>
    [Fact]
    public async Task SkillsMd_ForFixture_MatchesGolden()
    {
        using MoveFixture fixture = new();
        string repository = fixture.CopyOfFixtureRepository();
        fixture.WithRepository(repository);
        fixture.Imported("target-authoring", "https://github.com/dotnet/skills", "plugins/dotnet-msbuild/skills/target-authoring", "a660de83c2f9cad071a5d568f7d77041ccd07059", "Microsoft dotnet");
        fixture.Imported("code-review-and-quality", "https://github.com/addyosmani/agent-skills", "skills/code-review-and-quality", "1401c8b8030e023baeebb31781a6653fe8e93026", "Addy Osmani");
        fixture.UserPack("my-pack.json", """{ "id": "my-pack", "version": "1", "title": "Mine", "skills": [] }""");

        await fixture.RunAsync();

        string actual = File.ReadAllText(Path.Combine(fixture.Dir, "SKILLS.md"))
            .Replace(repository, "{{skillsRepo}}", StringComparison.Ordinal)
            .Replace($"{{{{skillsRepo}}}}{Path.DirectorySeparatorChar}packs{Path.DirectorySeparatorChar}", "{{skillsRepo}}\\packs\\", StringComparison.Ordinal);
        string golden = GoldenPath();
        if (!File.Exists(golden))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(golden) ?? ".");
            File.WriteAllText(golden, actual);
        }

        Assert.Equal(File.ReadAllText(golden).ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));
    }
}
