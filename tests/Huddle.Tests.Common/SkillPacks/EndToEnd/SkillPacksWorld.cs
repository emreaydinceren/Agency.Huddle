using Agency.Huddle.App;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.SkillSources.Move;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.Tests.SkillPacks.Move;
using Agency.Huddle.Tests.SkillPacks.Prompts;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Agency.Huddle.Tests.Ui.SkillPacks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.SkillPacks.EndToEnd;

/// <summary>One Huddle's Skill Packs pieces, all real, over a temporary data folder (Skill Packs spec §13, task P32): the
/// registry, the watcher with its real file-system watchers, the move, the install file writer, the copier and the updater.
/// Only the clock is fake, so the watcher's quiet period and the half-written grace are driven by <see cref="SettleUntilAsync"/>.</summary>
internal sealed class SkillPacksWorld : IAsyncDisposable
{
    private readonly RegistryWatcher watcher;

    /// <summary>Builds the pieces; nothing runs until <see cref="StartAsync"/>.</summary>
    private SkillPacksWorld(bool runMove, bool quietFiles)
    {
        this.Host.Clock.SetUtcNow(quietFiles ? DateTimeOffset.UtcNow.AddHours(1) : DateTimeOffset.UtcNow);
        ISkillSourcesMove move = runMove ? this.NewMove() : new NoSkillSourcesMove();
        this.watcher = new RegistryWatcher(this.Host.Registry, move, this.Host.Settings, NullLogger<RegistryWatcher>.Instance, this.Host.Clock);
        this.Install = new InstallFileWriter(this.Host.Roles, this.Host.Skills, this.Host.Registry, new RecordingPromptSource(), this.Host.Settings, NullLogger<InstallFileWriter>.Instance, this.Host.Clock);
        this.Copier = new SkillCopier(this.Host.Registry, this.Host.Settings, this.Host.Log, NullLogger<SkillCopier>.Instance, this.Host.Clock);
        this.Updater = new PackUpdater(this.Host.Registry, this.Copier, this.Host.Personas, this.Host.Log);
    }

    /// <summary>The bUnit side, for the components that react to the registry; its host holds every store.</summary>
    public SkillsUiFixture Ui { get; } = new();

    /// <summary>The stores, the registry and the fake clock.</summary>
    public RegistryHost Host => this.Ui.Host;

    /// <summary>The registry under test.</summary>
    public SkillRegistry Registry => this.Host.Registry;

    /// <summary>Writes <c>InstallSkillPack.md</c> and <c>install-context.json</c>.</summary>
    public InstallFileWriter Install { get; }

    /// <summary>The copier the Agent card and the hire path use.</summary>
    public SkillCopier Copier { get; }

    /// <summary>The updater the update card uses.</summary>
    public PackUpdater Updater { get; }

    /// <summary>Starts a world: the watcher (which runs the move when asked, then reads), then the install file writer.</summary>
    /// <param name="runMove">Run the real move, with the fixture skills repository as <c>Team:SkillsRepositoryDir</c>.</param>
    /// <param name="quietFiles">Put the clock an hour ahead so every file is past its grace; false keeps it at the real time.</param>
    public static async Task<SkillPacksWorld> StartAsync(bool runMove = false, bool quietFiles = true)
    {
        SkillPacksWorld world = new(runMove, quietFiles);
        if (!runMove)
        {
            // The move writes SKILLS.md on a first start, so a running Huddle always has one; an empty one stands in for it.
            File.WriteAllText(Path.Combine(world.Host.Dir, "SKILLS.md"), FakeAiTool.SkillsMdText([]));
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        await world.watcher.StartAsync(ct);
        await world.Install.StartAsync(ct);
        return world;
    }

    /// <summary>An AI tool that follows this Huddle's <c>InstallSkillPack.md</c>.</summary>
    public FakeAiTool AiTool()
    {
        return new FakeAiTool(this.Install.InstallFilePath, this.Host);
    }

    /// <summary>Lets the real file-system events arrive, then moves the fake clock past the quiet period, until
    /// <paramref name="done"/> holds or ten seconds of real time pass.</summary>
    public async Task SettleUntilAsync(Func<bool> done, string what)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        for (int attempt = 0; attempt < 100 && !done(); attempt++)
        {
            await Task.Delay(50, ct);
            this.Host.Clock.Advance(RegistryWatcher.Debounce);
            await Task.Delay(50, ct);
        }

        Assert.True(done(), $"Timed out waiting for: {what}");
    }

    /// <summary>The real move over this data folder, pointed at the fixture skills repository.</summary>
    private SkillSourcesMove NewMove()
    {
        IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = this.Host.DataDir, ComputerName = "DESK-01", SkillsRepositoryDir = MoveFixture.FixtureRepository });
        return new SkillSourcesMove(options, this.Host.Settings, new PhysicalMoveFiles(), new MoveReportStore(this.Host.Settings), this.Host.Log, NullLogger<SkillSourcesMove>.Instance, this.Host.Clock);
    }

    /// <summary>Stops the watcher and the writer, then deletes the folder.</summary>
    public async ValueTask DisposeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.Install.StopAsync(ct);
        await this.watcher.StopAsync(ct);
        this.Install.Dispose();
        this.watcher.Dispose();
        this.Ui.Dispose();
    }
}

/// <summary>A test double of the Human's AI tool (Skill Packs spec §13). It reads <c>InstallSkillPack.md</c>'s front matter
/// for the folder, the registry and the validate command, writes the Skill folders, the Source, the pack file and the
/// SKILLS.md row exactly where the file says, and runs <c>huddle-packs validate</c> through <c>Program.Run</c>.</summary>
internal sealed class FakeAiTool
{
    private readonly RegistryHost host;
    private readonly List<string> packRows = [];

    /// <summary>Reads the install file once, as a real AI tool would before it starts.</summary>
    public FakeAiTool(string installFile, RegistryHost host)
    {
        this.host = host;
        Dictionary<string, string> front = FrontMatter(File.ReadAllText(installFile));
        this.SkillSourcesDir = front["skillSourcesDir"];
        this.RegistryPath = front["registry"];
        this.DataDir = front["dataDir"];
        this.ValidateCommand = front["validate"];
    }

    /// <summary>The Skill Sources folder the install file names.</summary>
    public string SkillSourcesDir { get; }

    /// <summary>The SKILLS.md the install file names.</summary>
    public string RegistryPath { get; }

    /// <summary>The data folder the install file names.</summary>
    public string DataDir { get; }

    /// <summary>The validate command the install file gives.</summary>
    public string ValidateCommand { get; }

    /// <summary>Fetches the Skills into its clone, records the Source <c>local/test</c>, writes the pack file and adds its
    /// row to SKILLS.md, then validates. Returns validate's exit code and output.</summary>
    public (int Exit, string Output) AddPack(string id, string version, IReadOnlyList<string> skills)
    {
        foreach (string skill in skills)
        {
            if (!Directory.Exists(Path.Combine(this.host.Clone, "skills", skill)))
            {
                _ = this.host.CloneSkill(skill);
            }
        }

        this.WriteSource("active", null);
        this.WritePack(id, version, skills);
        string row = $"packs/{id}.skillpack.json";
        if (!this.packRows.Contains(row))
        {
            this.packRows.Add(row);
        }

        this.WriteSkillsMd();
        return this.Validate();
    }

    /// <summary>Rewrites the pack file at a new version and validates; the SKILLS.md row stays.</summary>
    public (int Exit, string Output) UpdatePack(string id, string version, IReadOnlyList<string> skills)
    {
        foreach (string skill in skills.Where(skill => !Directory.Exists(Path.Combine(this.host.Clone, "skills", skill))))
        {
            _ = this.host.CloneSkill(skill);
        }

        this.WritePack(id, version, skills);
        return this.Validate();
    }

    /// <summary>Marks the Source withdrawn, as an AI tool does when its repository is pulled.</summary>
    public void Withdraw(string reason)
    {
        this.WriteSource("withdrawn", reason);
    }

    /// <summary>Changes one Skill's <c>SKILL.md</c> in the clone, as a <c>git pull</c> would.</summary>
    public void ChangeSkill(string skill, string addedLine)
    {
        File.AppendAllText(Path.Combine(this.host.Clone, "skills", skill, "SKILL.md"), addedLine + "\n");
    }

    /// <summary>Runs <c>huddle-packs validate --data-dir</c> in-process on the real disk.</summary>
    public (int Exit, string Output) Validate(params string[] extra)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = Agency.Huddle.Packs.Program.Run(["validate", "--data-dir", this.DataDir, .. extra], output, error, static _ => null, this.DataDir, AppContext.BaseDirectory, PhysicalFileReader.Instance);
        return (exit, output.ToString() + error.ToString());
    }

    /// <summary>Reads the <c>key: value</c> lines between the two <c>---</c> lines, unquoting single-quoted YAML scalars.</summary>
    private static Dictionary<string, string> FrontMatter(string document)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        string[] lines = document.ReplaceLineEndings("\n").Split('\n');
        for (int i = 1; i < lines.Length && lines[i] != "---"; i++)
        {
            int colon = lines[i].IndexOf(':', StringComparison.Ordinal);
            string value = lines[i][(colon + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                value = value[1..^1].Replace("''", "'", StringComparison.Ordinal);
            }

            values[lines[i][..colon]] = value;
        }

        return values;
    }

    /// <summary>Writes <c>sources/local/test/source.json</c> and its notes.</summary>
    private void WriteSource(string status, string? reason)
    {
        string because = reason is null ? string.Empty : $", \"statusReason\": \"{reason}\"";
        RegistryHost.Write(this.SkillSourcesDir, "sources/local/test/source.json", $$"""{ "schemaVersion": 1, "kind": "local", "fetchedAt": "2026-10-17T10:00:00+02:00", "fetchedBy": "Fake AI tool", "signIn": "none", "status": "{{status}}"{{because}} }""");
        RegistryHost.Write(this.SkillSourcesDir, "sources/local/test/NOTES.md", "---\nlayout: skills/<name>/\n---\nFetched by the fake AI tool.");
    }

    /// <summary>Writes <c>packs/&lt;id&gt;.skillpack.json</c> pointing at the clone.</summary>
    private void WritePack(string id, string version, IReadOnlyList<string> skills)
    {
        IEnumerable<string> entries = skills.Select(skill =>
            $$"""{ "name": "{{skill}}", "path": "{{Path.Combine(this.host.Clone, "skills", skill).Replace("\\", "\\\\", StringComparison.Ordinal)}}", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/{{skill}}" }""");
        RegistryHost.Write(this.SkillSourcesDir, $"packs/{id}.skillpack.json", $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "{{version}}", "title": "Documents", "source": "local/test", "appliesTo": ["dev"], "consultWhenAdds": "office documents", "skills": [{{string.Join(", ", entries)}}] }
            """);
    }

    /// <summary>The text of a SKILLS.md with these Packs rows and no catalogs.</summary>
    public static string SkillsMdText(IReadOnlyList<string> packRows)
    {
        string rows = string.Concat(packRows.Select(static row => $"| {row} |\n"));
        return $"# Skills registry\n\n## Packs\n\n| Local path |\n| --- |\n{rows}\n## Catalogs\n\n| Local path |\n| --- |\n";
    }

    /// <summary>Writes SKILLS.md at the path the install file names, with every Packs row so far.</summary>
    private void WriteSkillsMd()
    {
        File.WriteAllText(this.RegistryPath, SkillsMdText(this.packRows));
    }
}
