using System.Text.Json;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Resolving;
using Agency.Huddle.Tests.SkillPacks.RegistryService;

namespace Agency.Huddle.Tests.SkillPacks.Removal;

/// <summary>A registry for the remove and hide flows (Skill Packs spec §6.12): a Source, Packs inside and outside the folder,
/// a catalog, Agents that hold a Pack, and helpers to read the files back.</summary>
internal sealed class RemovalFixture : IDisposable
{
    /// <summary>The registry and its files.</summary>
    public RegistryHost Host { get; } = new();

    /// <summary>The flows under test.</summary>
    public SkillSourcesRemover Remover => this.Host.Remover;

    /// <summary>SKILLS.md's path.</summary>
    public string SkillsMd => Path.Combine(this.Host.Dir, "SKILLS.md");

    /// <summary>The test Source's log.</summary>
    public string LogFile => Path.Combine(this.Host.Dir, "sources", "local", "test", SourceLog.FileName);

    /// <summary>Writes the Source (unless told not to), the Pack <c>documents</c> (docx, inside the folder), and reads.</summary>
    public async Task SeedAsync(bool source = true, string[]? extraPacks = null, string[]? catalogs = null)
    {
        if (source)
        {
            this.Host.Source();
        }

        this.Host.Pack("documents", "docx", this.Host.CloneSkill("docx"));
        this.Host.SkillsMd(["packs/documents.skillpack.json", .. extraPacks ?? []], catalogs);
        await this.Host.ReadAsync();
    }

    /// <summary>The Pack with this id.</summary>
    public PackEntry Pack(string id) => this.Host.Registry.FindPack(id) ?? throw new InvalidOperationException($"No Pack {id}.");

    /// <summary>Adds an Agent that carries <paramref name="own"/> and has the Pack added by the card's own rule, then re-reads.</summary>
    public async Task<string> HolderAsync(string name, string packId, params string[] own)
    {
        this.Host.Agent(name, own, []);
        Persona persona = this.Host.Personas.Get(name) ?? throw new InvalidOperationException(name);
        string text = PackMembership.Add(persona.Text, PackContents.From(this.Pack(packId)));
        this.Host.Personas.Update(name, text, persona.Model, persona.Effort, persona.WorkMode);
        await this.Host.ReadAsync();
        return text;
    }

    /// <summary>An Agent's file text now.</summary>
    public string TextOf(string name) => this.Host.Personas.Get(name)?.Text ?? throw new InvalidOperationException(name);

    /// <summary>The frontmatter line that starts with <paramref name="key"/>, or null.</summary>
    public static string? Line(string text, string key) =>
        text.Split('\n').Select(static line => line.TrimEnd('\r')).FirstOrDefault(line => line.StartsWith(key + ":", StringComparison.Ordinal));

    /// <summary>The last line of the Source's log, parsed.</summary>
    public JsonElement LastLog()
    {
        string last = File.ReadAllLines(this.LogFile).Last(static line => line.Length > 0);
        using JsonDocument document = JsonDocument.Parse(last);
        return document.RootElement.Clone();
    }

    /// <summary>Every file path under the data folder outside the Skill Sources folder and Skills/.staging or .trash.</summary>
    public IReadOnlySet<string> PathsOutsideTheFolder()
    {
        string folder = Path.GetFullPath(this.Host.Dir) + Path.DirectorySeparatorChar;
        return new HashSet<string>(
            Directory.EnumerateFiles(this.Host.DataDir, "*", SearchOption.AllDirectories)
                .Where(file => !file.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                    && !file.Contains($"{Path.DirectorySeparatorChar}.staging{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}.trash{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Disposes the host.</summary>
    public void Dispose()
    {
        this.Host.Dispose();
    }
}
