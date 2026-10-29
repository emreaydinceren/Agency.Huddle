using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Resolving;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins the cause line <see cref="PackView"/> gives each state (Skill Packs spec §6.9.4) against real files, and
/// <see cref="SkillNotes"/>.</summary>
public sealed class PackViewTests : IDisposable
{
    private readonly RegistryHost host = new();

    /// <summary>Disposes the host.</summary>
    public void Dispose()
    {
        this.host.Dispose();
    }

    /// <summary>A Pack whose two Skills have no folder names both: "2 of 2 Skills have no folder here: a and b."</summary>
    [Fact]
    public async Task NotDownloaded_Several_NamesThem()
    {
        this.host.Source();
        this.WritePack("pair", ("alpha", null), ("beta", null));
        this.host.SkillsMd(["packs/pair.skillpack.json"]);
        await this.host.ReadAsync();

        PackView view = this.View();

        Assert.Equal(ItemState.NotDownloaded, view.State);
        Assert.Equal("2 of 2 Skills have no folder here: alpha and beta.", view.Cause);
    }

    /// <summary>An Agent holding an older version of the Pack, with no Skill file changed, gets the version line.</summary>
    [Fact]
    public async Task UpdateAvailable_VersionOnly_SaysTheVersionDiffers()
    {
        this.host.Source();
        this.host.Pack("documents", "docx", this.host.CloneSkill("docx"));
        this.host.SkillsMd(["packs/documents.skillpack.json"]);
        this.host.Agent("Jane", ["docx"], [new VersionedRef("documents", "0.9.0")]);
        await this.host.ReadAsync();

        PackView view = this.View();

        Assert.Equal(ItemState.UpdateAvailable, view.State);
        Assert.Equal("The version Agents hold differs from this Pack file's.", view.Cause);
    }

    /// <summary>Copied Skills whose files changed at the pointer are named: one "has", two "have".</summary>
    [Fact]
    public async Task UpdateAvailable_FilesChanged_NamesTheSkills()
    {
        this.host.Source();
        string alpha = this.host.CloneSkill("alpha");
        string beta = this.host.CloneSkill("beta");
        this.WritePack("pair", ("alpha", alpha), ("beta", beta));
        this.host.SkillsMd(["packs/pair.skillpack.json"]);
        await this.host.ReadAsync();
        SkillCopier copier = new(this.host.Registry, this.host.Settings, this.host.Log, NullLogger<SkillCopier>.Instance, this.host.Clock);
        Assert.True((await copier.CopyAsync([.. this.host.Registry.Current.Resolved.Skills], TestContext.Current.CancellationToken)).Ok);

        File.WriteAllText(Path.Combine(alpha, "extra.md"), "More.");
        await this.host.ReadAsync();
        Assert.Equal("alpha has new files.", this.View().Cause);

        File.WriteAllText(Path.Combine(beta, "extra.md"), "More.");
        await this.host.ReadAsync();
        Assert.Equal("alpha and beta have new files.", this.View().Cause);
    }

    /// <summary>A deprecated Source's reason comes first, then where it came from and when it was seen.</summary>
    [Fact]
    public async Task Deprecated_GivesTheReasonAndWhenSeen()
    {
        this.host.Source("deprecated");
        this.host.Pack("documents", "docx", this.host.CloneSkill("docx"));
        this.host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.host.ReadAsync();

        PackView view = this.View();
        string seen = Agency.Huddle.App.Tasks.HumanDate.Full(DateOnly.FromDateTime(this.host.Registry.Current.Resolved.ReadAt.LocalDateTime));

        Assert.Equal(ItemState.Deprecated, view.State);
        Assert.Equal($"Because. (from the Source's source.json, seen {seen})", view.Cause);
    }

    /// <summary>The notes reader gives the first lines, and the fixed text when the file is locked.</summary>
    [Fact]
    public void SkillNotes_FirstLines_OrUnreadable()
    {
        string path = RegistryHost.Write(this.host.Dir, "sources/local/test/NOTES.md", "one\ntwo\nthree\n");

        string first = SkillNotes.FirstLines(path, 2);
        string locked;
        using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            locked = SkillNotes.FirstLines(path, 2);
        }

        Assert.Equal("one\ntwo", first);
        Assert.Equal("The notes could not be read.", locked);
    }

    /// <summary>The first Pack's view.</summary>
    private PackView View()
    {
        return PackView.From(this.host.Registry, this.host.Registry.Current.Resolved.Packs[0], this.host.Roles.List());
    }

    /// <summary>Writes a Pack with several Skills, each with a folder or an empty path.</summary>
    private void WritePack(string id, params (string Name, string? Folder)[] skills)
    {
        IEnumerable<string> entries = skills.Select(static skill =>
            $$"""{ "name": "{{skill.Name}}", "path": "{{(skill.Folder ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal)}}", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/{{skill.Name}}" }""");
        RegistryHost.Write(this.host.Dir, $"packs/{id}.skillpack.json", $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "1.0.0", "title": "{{id}} title", "source": "local/test", "appliesTo": ["dev"], "skills": [{{string.Join(",", entries)}}] }
            """);
    }
}
