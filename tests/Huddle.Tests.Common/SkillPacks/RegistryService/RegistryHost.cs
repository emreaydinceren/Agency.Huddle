using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.SkillPacks.RegistryService;

/// <summary>A real <see cref="SkillRegistry"/> over a temporary data folder, with a fake clock and helpers that write a Skill
/// Sources tree to disk. Also the <c>SnapshotBuilder</c> the UI tests build against (§A.6).</summary>
internal sealed class RegistryHost : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <summary>Builds every store the registry reads.</summary>
    public RegistryHost(string computer = "DESK-01")
    {
        this.Options = Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = this.dir.Path, ComputerName = computer });
        this.Settings = new SkillSourcesSettings(this.Options, NullLogger<SkillSourcesSettings>.Instance);
        this.Skills = new SkillStore(this.Options, NullLogger<SkillStore>.Instance);
        this.Personas = this.Options.CreatePersonaStore();
        this.Hidden = new HiddenStore(this.Settings);
        this.Log = new SourceLog(this.Settings, this.Clock);
        this.Types = RoleFixtures.Types(this.dir);
        this.Roles = new RoleLister(new HiringPoolCatalog(this.Options, NullLogger<HiringPoolCatalog>.Instance), new CustomRoleStore(this.Options), this.Types);
        this.Registry = new SkillRegistry(this.Settings, this.Skills, this.Personas, this.Hidden, this.Log, this.Roles, NullLogger<SkillRegistry>.Instance, this.Clock);
        this.Remover = new SkillSourcesRemover(this.Registry, this.Settings, new SkillsMdWriter(this.Settings), this.Hidden, this.Log, this.Personas, this.Types, NullLogger<SkillSourcesRemover>.Instance);
        Directory.CreateDirectory(this.Settings.Dir);
    }

    /// <summary>The team types the roles come from.</summary>
    public Agency.Huddle.App.TeamTypes.TeamTypeCatalog Types { get; }

    /// <summary>The remove and hide flows over these files.</summary>
    public SkillSourcesRemover Remover { get; }

    /// <summary>The fake clock.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The options.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The settings.</summary>
    public SkillSourcesSettings Settings { get; }

    /// <summary>The Skill store.</summary>
    public SkillStore Skills { get; }

    /// <summary>The Agent store.</summary>
    public PersonaStore Personas { get; }

    /// <summary>The hidden items.</summary>
    public HiddenStore Hidden { get; }

    /// <summary>The Source log.</summary>
    public SourceLog Log { get; }

    /// <summary>The roles.</summary>
    public RoleLister Roles { get; }

    /// <summary>The registry under test.</summary>
    public SkillRegistry Registry { get; }

    /// <summary>The data folder.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The Skill Sources folder.</summary>
    public string Dir => this.Settings.Dir;

    /// <summary>A folder outside the Skill Sources folder, standing in for a clone.</summary>
    public string Clone => Path.Combine(this.dir.Path, "Clones", "repo");

    /// <summary>Writes a file under <paramref name="root"/>, creating its folder.</summary>
    public static string Write(string root, string relative, string text)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Writes a valid Skill folder in the clone and returns its path.</summary>
    public string CloneSkill(string name, string? extra = null)
    {
        string folder = Path.Combine(this.Clone, "skills", name);
        Write(folder, "SKILL.md", $"---\nname: {name}\ndescription: Does {name} things.\nlicense: MIT\n{extra}---\nBody.\n");
        return folder;
    }

    /// <summary>Writes a valid Source record and notes for <c>local/test</c>.</summary>
    public void Source(string status = "active")
    {
        Write(this.Dir, "sources/local/test/source.json", $$"""{ "schemaVersion": 1, "kind": "local", "fetchedAt": "2026-10-17T10:00:00+02:00", "fetchedBy": "Test", "signIn": "none", "status": "{{status}}", "statusReason": "Because." }""");
        Write(this.Dir, "sources/local/test/NOTES.md", "---\nlayout: skills/<name>/\n---\nNotes.");
    }

    /// <summary>Writes a pack file with one Skill pointing at <paramref name="folder"/> (or with an empty path).</summary>
    public void Pack(string id, string skill, string? folder, string version = "1.0.0")
    {
        string path = folder is null ? string.Empty : folder.Replace("\\", "\\\\", StringComparison.Ordinal);
        Write(this.Dir, $"packs/{id}.skillpack.json", $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "{{version}}", "title": "{{id}} title", "source": "local/test", "appliesTo": ["dev"],
              "consultWhenAdds": "when {{id}} applies", "skills": [{ "name": "{{skill}}", "path": "{{path}}", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/{{skill}}" }] }
            """);
    }

    /// <summary>Writes a catalog file listing <paramref name="names"/>, each with no path (so Not downloaded) unless
    /// <paramref name="folderOf"/> gives one, and returns its row text for <see cref="SkillsMd"/>.</summary>
    public string Catalog(string id, IReadOnlyList<string> names, Func<string, string?>? folderOf = null)
    {
        IEnumerable<string> entries = names.Select(name =>
        {
            string path = (folderOf?.Invoke(name) ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal);
            return $$"""{ "name": "{{name}}", "description": "Does {{name}} things.", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/{{name}}", "publisher": "Pub", "license": "MIT", "path": "{{path}}" }""";
        });
        Write(this.Dir, $"catalogs/{id}.skillcatalog.json", $$"""{ "schemaVersion": 1, "id": "{{id}}", "title": "{{id}} title", "source": "local/test", "skills": [{{string.Join(",\n", entries)}}] }""");
        return $"catalogs/{id}.skillcatalog.json";
    }

    /// <summary>Reads everything with the clock far enough ahead that every file is quiet.</summary>
    public Task ReadAsync()
    {
        this.Clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(1));
        return this.Registry.ReloadAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Writes a pack file with several Skills, each with a folder or an empty path, and a phrase.</summary>
    public void PackOf(string id, string version, params (string Name, string? Folder)[] skills)
    {
        IEnumerable<string> entries = skills.Select(static skill =>
            $$"""{ "name": "{{skill.Name}}", "path": "{{(skill.Folder ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal)}}", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/{{skill.Name}}" }""");
        Write(this.Dir, $"packs/{id}.skillpack.json", $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "{{version}}", "title": "{{id}} title", "source": "local/test", "appliesTo": ["dev"], "consultWhenAdds": "when {{id}} applies", "skills": [{{string.Join(",", entries)}}] }
            """);
    }

    /// <summary>Writes SKILLS.md with the given Packs and Catalogs rows.</summary>
    public void SkillsMd(string[] packs, string[]? catalogs = null)
    {
        string Rows(string[] rows) => string.Concat(rows.Select(static row => $"| {row} |\n"));
        Write(this.Dir, "SKILLS.md", $"# Skills registry\n\n## Packs\n\n| Local path |\n| --- |\n{Rows(packs)}\n## Catalogs\n\n| Local path |\n| --- |\n{Rows(catalogs ?? [])}");
    }

    /// <summary>Adds an Agent whose <c>_packs</c> and <c>skills:</c> name the given ids.</summary>
    public void Agent(string name, IReadOnlyList<string> skills, IReadOnlyList<Agency.Huddle.App.Teams.VersionedRef> packs)
    {
        this.Personas.Add(new PersonaIdentity(name, "Developer", name.ToUpperInvariant(), ["Web"], Skills: skills, Packs: packs), "You build.");
    }

    /// <summary>Disposes the stores and the folder.</summary>
    public void Dispose()
    {
        this.Registry.Dispose();
        this.Personas.Dispose();
        this.Skills.Dispose();
        this.dir.Dispose();
    }
}
