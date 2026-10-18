using System.Diagnostics;
using System.Text.Json;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Resolving;
using Agency.Huddle.Tests.SkillPacks.RegistryService;

namespace Agency.Huddle.Tests.SkillPacks.Removal;

/// <summary>Pins removing a Pack (Skill Packs spec §6.12.2): the confirm's two messages, the row and the file, Keep and Remove
/// too, failures that stop safely, <c>hidden.json</c>, the log line, and Remove from Teammates.</summary>
public sealed class RemovePackTests : IDisposable
{
    private readonly RemovalFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The confirm says the file is deleted when it is inside the folder, and left where it is when outside.</summary>
    [Fact]
    public void PackMessage_TwoVariants()
    {
        Assert.Equal(
            "This removes its row from SKILLS.md and deletes packs\\document-skills.skillpack.json from the Skill Sources folder. No Teammate has this Pack. Your AI tool's clone is not touched.",
            RemovalText.PackMessage("packs\\document-skills.skillpack.json", null));
        Assert.Equal(
            "This removes its row from SKILLS.md. The file E:\\Repos\\skills\\packs\\x.skillpack.json is outside the Skill Sources folder, so Huddle leaves it where it is. No Teammate has this Pack. Your AI tool's clone is not touched.",
            RemovalText.PackMessage(null, "E:\\Repos\\skills\\packs\\x.skillpack.json"));
        Assert.Equal("Remove \"Document skills\"?", RemovalText.PackTitle("Document skills"));
    }

    /// <summary>Removing deletes the row's lines and the file inside the folder; a Pack file outside the folder stays.</summary>
    [Fact]
    public async Task Remove_DeletesRowAndInsideFile_LeavesOutsideFile()
    {
        string outside = RegistryHost.Write(this.fixture.Host.Clone, "packs/far.skillpack.json", """
            { "schemaVersion": 1, "id": "far", "version": "1.0.0", "title": "Far", "source": "local/test", "appliesTo": ["dev"], "skills": [{ "name": "docx", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/docx" }] }
            """);
        await this.fixture.SeedAsync(extraPacks: [outside]);
        string inside = this.fixture.Pack("documents").FilePath ?? string.Empty;

        RemovalOutcome first = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.None);
        RemovalOutcome second = this.fixture.Remover.RemovePack(this.fixture.Pack("far"), HolderChoice.None);

        Assert.Equal(new RemovalOutcome(true, "documents title removed."), first);
        Assert.True(second.Ok);
        Assert.False(File.Exists(inside));
        Assert.True(File.Exists(outside));
        string md = File.ReadAllText(this.fixture.SkillsMd);
        Assert.DoesNotContain("documents.skillpack.json", md, StringComparison.Ordinal); // contains-ok: the row's own text must be gone from the file
        Assert.DoesNotContain("far.skillpack.json", md, StringComparison.Ordinal); // contains-ok: as above
    }

    /// <summary>Keep their Skills drops only <c>_packs</c>; <c>skills:</c> and <c>consult_when:</c> stay byte for byte.</summary>
    [Fact]
    public async Task Keep_DropsOnlyPacks()
    {
        await this.fixture.SeedAsync();
        string before = await this.fixture.HolderAsync("Jane", "documents", "own-skill");

        RemovalOutcome outcome = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.Keep);

        string after = this.fixture.TextOf("Jane");
        Assert.True(outcome.Ok);
        Assert.Null(RemovalFixture.Line(after, "_packs"));
        Assert.Equal(RemovalFixture.Line(before, "skills"), RemovalFixture.Line(after, "skills"));
        Assert.Equal(RemovalFixture.Line(before, "consult_when"), RemovalFixture.Line(after, "consult_when"));
    }

    /// <summary>Remove from Teammates too takes off the Skills nobody else gave and the phrase, and keeps the Teammate's own.</summary>
    [Fact]
    public async Task RemoveToo_TakesOffPackSkillsAndPhrase()
    {
        await this.fixture.SeedAsync();
        await this.fixture.HolderAsync("Jane", "documents", "own-skill");

        RemovalOutcome outcome = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.RemoveToo);

        string after = this.fixture.TextOf("Jane");
        Assert.True(outcome.Ok);
        Assert.Equal("skills: ['own-skill']", RemovalFixture.Line(after, "skills"));
        Assert.Null(RemovalFixture.Line(after, "consult_when"));
        Assert.Null(RemovalFixture.Line(after, "_packs"));
    }

    /// <summary>A Teammate write that fails stops before SKILLS.md is touched.</summary>
    [Fact]
    public async Task TeammateWriteFails_StopsBeforeSkillsMd()
    {
        await this.fixture.SeedAsync();
        await this.fixture.HolderAsync("Jane", "documents");
        string file = Path.Combine(this.fixture.Host.DataDir, "Teammates", "Jane", "Jane.md");
        string md = File.ReadAllText(this.fixture.SkillsMd);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            RemovalOutcome outcome = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.Keep);

            Assert.False(outcome.Ok);
            Assert.StartsWith("Could not change Jane:", outcome.Message, StringComparison.Ordinal); // contains-ok: the middle is the operating system's own message
            Assert.EndsWith(" The Pack and its row are still there.", outcome.Message, StringComparison.Ordinal); // contains-ok: as above
            Assert.Equal(md, File.ReadAllText(this.fixture.SkillsMd));
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    /// <summary>A pack file that cannot be deleted after the row is gone says so and leaves SKILLS.md edited.</summary>
    [Fact]
    public async Task FileDeleteFails_SaysSo_RowStaysRemoved()
    {
        await this.fixture.SeedAsync();
        string file = this.fixture.Pack("documents").FilePath ?? string.Empty;
        RemovalOutcome outcome;
        using (FileStream locked = new(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            outcome = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.None);
        }

        Assert.Equal(new RemovalOutcome(false, $"Removed the row, but could not delete {file}. Delete it yourself."), outcome);
        Assert.DoesNotContain("documents.skillpack.json", File.ReadAllText(this.fixture.SkillsMd), StringComparison.Ordinal); // contains-ok: the row's text must be gone
        Assert.True(File.Exists(file));
    }

    /// <summary>The Pack's <c>hidden.json</c> entry goes and other entries stay; a malformed file is left as it is.</summary>
    [Fact]
    public async Task HiddenEntry_Removed_OthersAndMalformedUntouched()
    {
        await this.fixture.SeedAsync();
        this.fixture.Host.Hidden.Hide(HiddenKind.Pack, "documents");
        this.fixture.Host.Hidden.Hide(HiddenKind.Pack, "other");
        this.fixture.Host.Hidden.Hide(HiddenKind.Skill, "qa-kit");

        _ = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.None);

        HiddenSet set = this.fixture.Host.Hidden.Read();
        Assert.Equal(["other"], set.Packs);
        Assert.Equal(["qa-kit"], set.Skills);

        using RemovalFixture second = new();
        await second.SeedAsync();
        File.WriteAllText(second.Host.Hidden.FilePath, "{ not json");
        _ = second.Remover.RemovePack(second.Pack("documents"), HolderChoice.None);
        Assert.Equal("{ not json", File.ReadAllText(second.Host.Hidden.FilePath));
    }

    /// <summary>The log line is event <c>removed</c>, subject <c>pack:&lt;id&gt;</c>, with row, fileDeleted, teammates and holders.</summary>
    [Fact]
    public async Task LogLine_HasTheData()
    {
        await this.fixture.SeedAsync();
        await this.fixture.HolderAsync("Jane", "documents");

        _ = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.Keep);

        JsonElement line = this.fixture.LastLog();
        Assert.Equal("removed", line.GetProperty("event").GetString());
        Assert.Equal("pack:documents", line.GetProperty("subject").GetString());
        JsonElement data = line.GetProperty("data");
        Assert.Equal("packs/documents.skillpack.json", data.GetProperty("row").GetString());
        Assert.True(data.GetProperty("fileDeleted").GetBoolean());
        Assert.Equal("kept", data.GetProperty("teammates").GetString());
        Assert.Equal(["Jane"], data.GetProperty("holders").EnumerateArray().Select(static item => item.GetString()));
    }

    /// <summary>With no Source folder, no log line is written and no folder is created.</summary>
    [Fact]
    public async Task NoSourceFolder_NoLineNoFolder()
    {
        await this.fixture.SeedAsync(source: false);

        RemovalOutcome outcome = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.None);

        Assert.True(outcome.Ok);
        Assert.False(Directory.Exists(Path.Combine(this.fixture.Host.Dir, "sources")));
    }

    /// <summary>Remove from Teammates (a withdrawn Pack) takes it off its holders and leaves the row and the file.</summary>
    [Fact]
    public async Task Unhold_LeavesRowAndFile()
    {
        await this.fixture.SeedAsync();
        await this.fixture.HolderAsync("Jane", "documents");
        string file = this.fixture.Pack("documents").FilePath ?? string.Empty;
        string md = File.ReadAllText(this.fixture.SkillsMd);

        RemovalOutcome outcome = this.fixture.Remover.UnholdPack(this.fixture.Pack("documents"));

        Assert.Equal(new RemovalOutcome(true, "documents title was taken off 1 Teammate."), outcome);
        Assert.Null(RemovalFixture.Line(this.fixture.TextOf("Jane"), "_packs"));
        Assert.Equal(md, File.ReadAllText(this.fixture.SkillsMd));
        Assert.True(File.Exists(file));
        Assert.Equal("unheld", this.fixture.LastLog().GetProperty("event").GetString());
        Assert.Equal("Remove from 2 Teammates?", RemovalText.UnholdTitle(2));
        Assert.Equal("Jane and Omar will lose the Skills no one else gave them, and the Pack's \"consult when\" phrase. They restart. The Pack's row and file stay.", RemovalText.UnholdMessage(["Jane", "Omar"]));
    }

    /// <summary>Nothing outside the Skill Sources folder (and Skills/.staging or .trash) is deleted by a removal, even Remove too.</summary>
    [Fact]
    public async Task Remove_DeletesNothingOutsideTheFolder()
    {
        await this.fixture.SeedAsync();
        await this.fixture.HolderAsync("Jane", "documents");
        IReadOnlySet<string> before = this.fixture.PathsOutsideTheFolder();

        _ = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), HolderChoice.RemoveToo);
        _ = this.fixture.Remover.RemoveSource("local/test");

        Assert.Subset(new HashSet<string>(this.fixture.PathsOutsideTheFolder(), StringComparer.OrdinalIgnoreCase), new HashSet<string>(before, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>Pins removing a catalog (Skill Packs spec §6.12.3).</summary>
public sealed class RemoveCatalogTests : IDisposable
{
    private readonly RemovalFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The message names the Packs that use the catalog; the row and the file inside the folder go; hidden.json is unchanged.</summary>
    [Fact]
    public async Task Remove_NamesPacks_DeletesRowAndFile_HiddenUnchanged()
    {
        string catalog = this.fixture.Host.Catalog("extra", ["alpha", "beta"]);
        await this.fixture.SeedAsync(catalogs: [catalog]);
        this.fixture.Host.Hidden.Hide(HiddenKind.Skill, "alpha");
        string hiddenBefore = File.ReadAllText(this.fixture.Host.Hidden.FilePath);
        ResolvedCatalog entry = this.fixture.Host.Registry.Current.Resolved.FindCatalog("extra") ?? throw new InvalidOperationException("extra");

        RemovalOutcome outcome = this.fixture.Remover.RemoveCatalog(entry);

        Assert.Equal(new RemovalOutcome(true, "Catalog extra title removed."), outcome);
        Assert.False(File.Exists(entry.FilePath));
        Assert.DoesNotContain("extra.skillcatalog.json", File.ReadAllText(this.fixture.SkillsMd), StringComparison.Ordinal); // contains-ok: the row's text must be gone
        Assert.Equal(hiddenBefore, File.ReadAllText(this.fixture.Host.Hidden.FilePath));
        Assert.Equal(
            "This removes its row from SKILLS.md and deletes catalogs\\dotnet-skills.skillcatalog.json from the Skill Sources folder. Its 106 Skills stop being listed. Teammates keep their copies in Skills/. These Packs name Skills in it and will show Broken until your AI tool fixes them: MSBuild Authoring.",
            RemovalText.CatalogMessage("catalogs\\dotnet-skills.skillcatalog.json", null, 106, ["MSBuild Authoring"]));
        Assert.Equal("Remove catalog \"dotnet-skills\"?", RemovalText.CatalogTitle("dotnet-skills"));
    }
}

/// <summary>Pins removing a Source (Skill Packs spec §6.12.5).</summary>
public sealed class RemoveSourceTests : IDisposable
{
    private readonly RemovalFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The rows naming the Source and their in-folder files go, then the Source folder; the clone is untouched.</summary>
    [Fact]
    public async Task Remove_RowsFilesThenFolder_CloneUntouched()
    {
        await this.fixture.SeedAsync();
        string file = this.fixture.Pack("documents").FilePath ?? string.Empty;
        string clone = Path.Combine(this.fixture.Host.Clone, "skills", "docx", "SKILL.md");

        RemovalOutcome outcome = this.fixture.Remover.RemoveSource("local/test");

        Assert.Equal(new RemovalOutcome(true, "Source removed. 1 row and their files are gone; the clone and Teammates' copies are untouched."), outcome);
        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(Path.Combine(this.fixture.Host.Dir, "sources", "local")));
        Assert.True(File.Exists(clone));
        Assert.DoesNotContain("documents.skillpack.json", File.ReadAllText(this.fixture.SkillsMd), StringComparison.Ordinal); // contains-ok: the row's text must be gone
    }

    /// <summary>A reparse point inside the Source folder is unlinked and its target kept.</summary>
    [Fact]
    public async Task RemoveSource_ReparsePointInside_IsUnlinkedTargetKept()
    {
        await this.fixture.SeedAsync();
        string target = Path.Combine(this.fixture.Host.Clone, "keep");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "precious.txt"), "keep me");
        string link = Path.Combine(this.fixture.Host.Dir, "sources", "local", "test", "link");
        CreateLink(link, target);

        RemovalOutcome outcome = this.fixture.Remover.RemoveSource("local/test");

        Assert.True(outcome.Ok);
        Assert.False(Directory.Exists(link));
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(target, "precious.txt")));
    }

    /// <summary>A Source id that resolves outside <c>sources/</c> is refused and nothing is deleted.</summary>
    [Fact]
    public async Task RemoveSource_IdResolvesOutside_Refused()
    {
        await this.fixture.SeedAsync();
        string md = File.ReadAllText(this.fixture.SkillsMd);

        RemovalOutcome outcome = this.fixture.Remover.RemoveSource("..\\..");

        Assert.Equal(new RemovalOutcome(false, SkillSourcesRemover.OutsideSourcesRefusal), outcome);
        Assert.Equal("That Source is not in the Skill Sources folder, so Huddle did not touch it.", outcome.Message);
        Assert.Equal(md, File.ReadAllText(this.fixture.SkillsMd));
        Assert.True(Directory.Exists(Path.Combine(this.fixture.Host.Dir, "sources", "local", "test")));
    }

    /// <summary>A failure in step 1 (SKILLS.md cannot be written) keeps the notes folder.</summary>
    [Fact]
    public async Task StepOneFails_KeepsTheNotes()
    {
        await this.fixture.SeedAsync();
        File.SetAttributes(this.fixture.SkillsMd, FileAttributes.ReadOnly);
        try
        {
            RemovalOutcome outcome = this.fixture.Remover.RemoveSource("local/test");

            Assert.Equal(new RemovalOutcome(false, "SKILLS.md could not be changed, so the Source and its notes are still there. Try again."), outcome);
            Assert.True(File.Exists(Path.Combine(this.fixture.Host.Dir, "sources", "local", "test", "NOTES.md")));
        }
        finally
        {
            File.SetAttributes(this.fixture.SkillsMd, FileAttributes.Normal);
        }
    }

    /// <summary>A notes folder that cannot be deleted (a file in it is open) says so after the rows are gone.</summary>
    [Fact]
    public async Task NotesLocked_SaysTheRowsAreGone()
    {
        await this.fixture.SeedAsync();
        string notes = Path.Combine(this.fixture.Host.Dir, "sources", "local", "test", "NOTES.md");
        RemovalOutcome outcome;
        using (FileStream locked = new(notes, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            outcome = this.fixture.Remover.RemoveSource("local/test");
        }

        Assert.False(outcome.Ok);
        Assert.StartsWith("The rows are gone, but the Source's notes could not be deleted: ", outcome.Message, StringComparison.Ordinal); // contains-ok: the rest is the operating system's own message
        Assert.DoesNotContain("documents.skillpack.json", File.ReadAllText(this.fixture.SkillsMd), StringComparison.Ordinal); // contains-ok: the row's text must be gone
    }

    /// <summary>The confirm's message lists the notes, the rows and the clone, and leaves out what does not apply.</summary>
    [Fact]
    public void SourceMessage_Composes()
    {
        Assert.Equal(
            "This permanently deletes the notes kept for github.com/anthropics/skills: NOTES.md, source.json and log.jsonl. It also removes 2 rows from SKILLS.md (Document skills, anthropics-skills) and deletes their files from the Skill Sources folder. Teammates keep their copies in Skills/. Your AI tool's clone at E:\\Clones\\anthropics-skills is not touched. If you share this folder with another Huddle, it loses the notes too.",
            RemovalText.SourceMessage("github.com/anthropics/skills", ["Document skills", "anthropics-skills"], "E:\\Clones\\anthropics-skills"));
        Assert.Equal(
            "This permanently deletes the notes kept for local/x: NOTES.md, source.json and log.jsonl. Teammates keep their copies in Skills/. If you share this folder with another Huddle, it loses the notes too.",
            RemovalText.SourceMessage("local/x", [], null));
    }

    /// <summary>A junction on Windows, a symbolic link elsewhere.</summary>
    private static void CreateLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            ProcessStartInfo startInfo = new("cmd") { ArgumentList = { "/c", "mklink", "/J", link, target }, UseShellExecute = false, CreateNoWindow = true };
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("mklink did not start.");
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target);
        }
    }
}
