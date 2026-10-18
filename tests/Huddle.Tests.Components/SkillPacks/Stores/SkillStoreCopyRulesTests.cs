using System.Text.Json;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Skills;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.Tests.Ui;
using Bunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.SkillPacks.Stores;

/// <summary>Pins the two rules a copy in <c>Skills/</c> needs (§6.3, §8.6): folders starting with <c>.</c> are skipped,
/// and a copy's <c>tools:</c> line is ignored.</summary>
public sealed class SkillStoreCopyRulesTests
{
    /// <summary>Writes a Skill folder with a <c>tools:</c> line, with or without a <c>.source.json</c>.</summary>
    private static void WriteSkill(TempDataDir dir, string name, bool withSource)
    {
        string folder = Path.Combine(dir.Path, "Skills", name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILL.md"), $"---\nname: {name}\ndescription: A skill.\ntools: propose_sprint\n---\nBody\n");
        if (withSource)
        {
            new SkillProvenance("https://github.com/o/r", $"skills/{name}", "abc", SkillFolderHash.ComputeFolder(folder), "o", "MIT", DateTimeOffset.UnixEpoch).Write(folder);
        }
    }

    /// <summary>Folders whose names start with a dot (<c>.staging</c>, <c>.trash</c>) are never read as Skills.</summary>
    [Fact]
    public void ScanDisk_DotFolders_AreSkipped()
    {
        using TempDataDir dir = new();
        string staging = Path.Combine(dir.Path, "Skills", ".staging", "docx-1");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(dir.Path, "Skills", ".staging", "SKILL.md"), "---\nname: .staging\ndescription: x\n---\n");

        using SkillStore store = new(dir.Options(), NullLogger<SkillStore>.Instance);

        Assert.DoesNotContain(store.All, static skill => skill.Name.StartsWith('.'));
        Assert.DoesNotContain(store.Issues, static issue => issue.Skill.StartsWith('.'));
    }

    /// <summary>A copy with a <c>.source.json</c> grants no tool, whatever its <c>tools:</c> line says.</summary>
    [Fact]
    public void All_FolderWithSourceJson_IgnoresToolsLine()
    {
        using TempDataDir dir = new();
        WriteSkill(dir, "copied", withSource: true);

        using SkillStore store = new(dir.Options(), NullLogger<SkillStore>.Instance);

        Skill skill = Assert.Single(store.All, static skill => skill.Name == "copied");
        Assert.Empty(skill.Tools);
    }

    /// <summary>A Skill the Human wrote, without a <c>.source.json</c>, keeps its tools.</summary>
    [Fact]
    public void All_FolderWithoutSourceJson_IsYours()
    {
        using TempDataDir dir = new();
        WriteSkill(dir, "mine", withSource: false);

        using SkillStore store = new(dir.Options(), NullLogger<SkillStore>.Instance);

        Skill skill = Assert.Single(store.All, static skill => skill.Name == "mine");
        Assert.Equal(SkillSource.Yours, skill.Source);
        Assert.Contains("propose_sprint", skill.Tools);
    }
}

/// <summary>Pins the new <c>.source.json</c> (§7.2): backward compatible both ways.</summary>
public sealed class SkillProvenanceNewFieldsTests
{
    /// <summary>A new file carries the seven old fields and the four new ones, local time with its offset.</summary>
    [Fact]
    public void Provenance_NewFile_IsReadByOldReaderShape()
    {
        using TempDataDir dir = new();
        SkillProvenance written = new("https://github.com/anthropics/skills", "skills/docx", "dbd4588", "9f2c", "Anthropic", "Proprietary",
            new DateTimeOffset(2026, 10, 17, 16, 41, 7, TimeSpan.FromHours(2)), 1, "github.com/anthropics/skills", "https://github.com/anthropics/skills/tree/dbd4588/skills/docx", @"E:\Clones\a\skills\docx");

        written.Write(dir.Path);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.Path, SkillProvenance.FileName)));
        string[] names = [.. document.RootElement.EnumerateObject().Select(static property => property.Name)];
        Assert.Equal(["repo", "path", "commit", "sha256", "publisher", "license", "importedAt", "schemaVersion", "source", "origin", "copiedFrom"], names);
        Assert.Equal("2026-10-17T16:41:07+02:00", document.RootElement.GetProperty("importedAt").GetString());
        Assert.True(SkillProvenance.TryRead(dir.Path, out SkillProvenance? read));
        Assert.Equal("https://github.com/anthropics/skills/tree/dbd4588/skills/docx", read?.Origin);
    }

    /// <summary>An old file reads with the new fields null and a derived origin and Source.</summary>
    [Fact]
    public void Provenance_OldFile_ReadsWithNewFieldsNull()
    {
        using TempDataDir dir = new();
        new SkillProvenance("https://github.com/dotnet/skills", "skills/target-authoring", "a660de8", "h", "Microsoft", "MIT", DateTimeOffset.UnixEpoch).Write(dir.Path);

        Assert.True(SkillProvenance.TryRead(dir.Path, out SkillProvenance? read));

        Assert.NotNull(read);
        Assert.Null(read.SchemaVersion);
        Assert.Null(read.Origin);
        Assert.Equal("https://github.com/dotnet/skills/tree/a660de8/skills/target-authoring", read.EffectiveOrigin);
        Assert.Equal("github.com/dotnet/skills", read.EffectiveSource);
        Assert.DoesNotContain("origin", File.ReadAllText(Path.Combine(dir.Path, SkillProvenance.FileName)), StringComparison.Ordinal);
    }

    /// <summary>The provenance line shows the Source, and a Source removed chip when asked.</summary>
    [Fact]
    public async Task ProvenanceLine_SourceRemoved_ShowsChip()
    {
        await using MudBunitContext ctx = new();
        Skill skill = new("docx", "Docx.", [], ["SKILL.md"], SkillSource.Yours, null,
            new SkillProvenance("https://github.com/anthropics/skills", "skills/docx", "dbd4588", "h", "Anthropic", "MIT", DateTimeOffset.UnixEpoch, 1, "github.com/anthropics/skills"));

        IRenderedComponent<SkillProvenanceLine> kept = ctx.Render<SkillProvenanceLine>(p => p.Add(x => x.Skill, skill));
        IRenderedComponent<SkillProvenanceLine> removed = ctx.Render<SkillProvenanceLine>(p => p.Add(x => x.Skill, skill).Add(x => x.SourceRemoved, true));

        Assert.Equal("github.com/anthropics/skills", kept.Find(".skill-provenance-source").TextContent.Trim());
        Assert.Empty(kept.FindAll(".skill-provenance-source-removed"));
        Assert.Contains("Source removed", removed.Find(".skill-provenance-source-removed").TextContent, StringComparison.Ordinal);
    }
}
