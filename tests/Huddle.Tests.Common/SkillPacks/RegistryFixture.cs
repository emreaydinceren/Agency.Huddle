using System.Text;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;
using CheckRunner = Agency.Huddle.SkillPacks.Checking.Checks;

namespace Agency.Huddle.Tests.SkillPacks;

/// <summary>Builds a Skill Sources tree in an <see cref="InMemoryFileReader"/>: SKILLS.md rows, pack and catalog files,
/// Source records and Skill folders, plus the <see cref="CheckContext"/> to resolve and check them with.</summary>
internal sealed class RegistryFixture
{
    private readonly List<string> packRows = [];
    private readonly List<string> catalogRows = [];

    /// <summary>The Skill Sources folder.</summary>
    public string Dir { get; } = InMemoryFileReader.At("SS");

    /// <summary>The <c>Skills/</c> folder of the copies.</summary>
    public string SkillsDir { get; } = InMemoryFileReader.At("Data", "Skills");

    /// <summary>A folder outside the Skill Sources folder, standing in for a clone.</summary>
    public string Clone { get; } = InMemoryFileReader.At("Clones", "repo");

    /// <summary>The files.</summary>
    public InMemoryFileReader Files { get; } = new();

    /// <summary>This computer's name.</summary>
    public string Computer { get; set; } = "DESK-01";

    /// <summary>The roles in the context.</summary>
    public List<RoleInfo> Roles { get; } = [new("developer", "Developer", "Writes code.", []), new("bookkeeper", "Bookkeeper", "Keeps the books.", [])];

    /// <summary>The installed Skills in the context.</summary>
    public List<InstalledSkill> Installed { get; } = [];

    /// <summary>The App Tool names in the context.</summary>
    public List<string> AppTools { get; } = ["propose_sprint", "propose_agents", "validate_agent"];

    /// <summary>The "now" the context carries.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Adds a Packs row with the given Local path cell.</summary>
    public RegistryFixture PackRow(string cell)
    {
        this.packRows.Add(cell);
        return this;
    }

    /// <summary>Adds a Catalogs row with the given Local path cell.</summary>
    public RegistryFixture CatalogRow(string cell)
    {
        this.catalogRows.Add(cell);
        return this;
    }

    /// <summary>Writes <c>packs/&lt;id&gt;.skillpack.json</c> and adds its row. <paramref name="skills"/> is the JSON list.</summary>
    public RegistryFixture Pack(string id, string skills, string source = "local/test", string appliesTo = """["developer"]""", string version = "1.0.0")
    {
        string json = $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "{{version}}", "title": "{{id}} title", "source": "{{source}}",
              "appliesTo": {{appliesTo}}, "consultWhenAdds": "when {{id}} applies", "skills": {{skills}} }
            """;
        this.Files.Add(Path.Combine(this.Dir, "packs", id + ".skillpack.json"), json);
        return this.PackRow($"packs/{id}.skillpack.json");
    }

    /// <summary>Writes <c>catalogs/&lt;id&gt;.skillcatalog.json</c> and adds its row. <paramref name="skills"/> is the JSON list.</summary>
    public RegistryFixture Catalog(string id, string skills, string source = "local/test")
    {
        string json = $$"""{ "schemaVersion": 1, "id": "{{id}}", "source": "{{source}}", "skills": {{skills}} }""";
        this.Files.Add(Path.Combine(this.Dir, "catalogs", id + ".skillcatalog.json"), json);
        return this.CatalogRow($"catalogs/{id}.skillcatalog.json");
    }

    /// <summary>Writes a valid <c>sources/&lt;id&gt;/source.json</c> and <c>NOTES.md</c>.</summary>
    public RegistryFixture Source(string id = "local/test", string status = "active", string? extra = null)
    {
        string folder = SourceId.FolderOf(this.Dir, id);
        string json = $$"""{ "schemaVersion": 1, "kind": "local", "fetchedAt": "2026-10-17T10:00:00+02:00", "fetchedBy": "Test", "signIn": "none", "status": "{{status}}"{{extra}} }""";
        this.Files.Add(Path.Combine(folder, "source.json"), json);
        this.Files.Add(Path.Combine(folder, "NOTES.md"), "---\nlayout: skills/<name>/\n---\nNotes.");
        return this;
    }

    /// <summary>Writes a valid Skill folder at <paramref name="folder"/> named after its last segment.</summary>
    public RegistryFixture SkillFolder(string folder, string? body = null, string? frontmatterExtra = null, string? name = null)
    {
        string skillName = name ?? Path.GetFileName(folder);
        this.Files.Add(Path.Combine(folder, "SKILL.md"), $"---\nname: {skillName}\ndescription: Does {skillName} things.\nlicense: MIT\n{frontmatterExtra}---\n{body ?? "Body."}\n");
        return this;
    }

    /// <summary>A Skill folder under the clone.</summary>
    public string CloneSkill(string name)
    {
        return Path.Combine(this.Clone, "skills", name);
    }

    /// <summary>Writes a copy in <c>Skills/</c> with a <c>.source.json</c> naming <paramref name="origin"/> and <paramref name="sha"/>.</summary>
    public RegistryFixture Copy(string name, string origin, string sha)
    {
        string folder = Path.Combine(this.SkillsDir, name);
        this.SkillFolder(folder);
        this.Files.Add(Path.Combine(folder, ".source.json"), $$"""{ "repo": "x", "path": "x", "commit": "x", "sha256": "{{sha}}", "publisher": "p", "license": "MIT", "importedAt": "2026-10-01T00:00:00Z", "origin": "{{origin}}" }""");
        this.Installed.Add(new InstalledSkill(name, origin, sha, BuiltIn: false));
        return this;
    }

    /// <summary>Writes SKILLS.md from the rows added so far.</summary>
    public RegistryFixture WriteSkillsMd()
    {
        StringBuilder text = new();
        text.Append("# Skills registry\n\n## Packs\n\n| Local path | Notes |\n| --- | --- |\n");
        foreach (string row in this.packRows)
        {
            text.Append("| ").Append(row).Append(" | |\n");
        }

        text.Append("\n## Catalogs\n\n| Local path | Notes |\n| --- | --- |\n");
        foreach (string row in this.catalogRows)
        {
            text.Append("| ").Append(row).Append(" | |\n");
        }

        this.Files.Add(Path.Combine(this.Dir, RegistryReader.FileName), text.ToString());
        return this;
    }

    /// <summary>The context the checks run with.</summary>
    public CheckContext Context(string huddleVersion = "2.4.0")
    {
        return new CheckContext(huddleVersion, 1, this.Computer, InMemoryFileReader.At("Data"), this.Dir, this.SkillsDir, this.Roles, this.Installed, this.AppTools, this.Now);
    }

    /// <summary>Writes SKILLS.md, reads and resolves it.</summary>
    public ResolvedRegistry Resolve(ResolveOptions? options = null)
    {
        this.WriteSkillsMd();
        Registry registry = RegistryReader.Load(this.Files, this.Dir, this.Computer);
        return RegistryResolver.Resolve(registry, this.Files, this.Context(), options);
    }

    /// <summary>Writes SKILLS.md, reads, resolves and checks it.</summary>
    public CheckResult Check(CheckOptions? options = null)
    {
        ResolvedRegistry resolved = this.Resolve();
        return CheckRunner.Run(resolved, this.Context(), options ?? new CheckOptions(), this.Files, TestContext.Current.CancellationToken);
    }
}
