using Agency.Huddle.App.Acp;
using Agency.Huddle.SkillPacks.Folders;

namespace Agency.Huddle.Tests.SkillPacks.Folders;

/// <summary>Pins the folder rules that moved out of the deleted downloader (§6.3, S003 and S004).</summary>
public sealed class SkillFolderRulesTests
{
    private static readonly string Folder = InMemoryFileReader.At("clone", "skills", "docx");

    /// <summary>201 files is a size violation; 200 is not.</summary>
    [Fact]
    public void TooManyFiles_Reported()
    {
        InMemoryFileReader files = new();
        for (int i = 0; i < 200; i++)
        {
            files.Add(Path.Combine(Folder, $"f{i}.md"), "x");
        }

        Assert.Empty(SkillFolderRules.Check(files, Folder));

        files.Add(Path.Combine(Folder, "one-more.md"), "x");

        FolderViolation violation = Assert.Single(SkillFolderRules.Check(files, Folder));
        Assert.Equal(FolderLimit.Size, violation.Limit);
        Assert.Contains("201", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>A folder marked as a reparse point is a name violation and is not walked into.</summary>
    [Fact]
    public void Junction_Reported()
    {
        string linked = Path.Combine(Folder, "linked");
        InMemoryFileReader files = new InMemoryFileReader()
            .Add(Path.Combine(Folder, "SKILL.md"), "x")
            .Add(Path.Combine(linked, "secret.md"), "x")
            .SetAttributes(linked, FileAttributes.Directory | FileAttributes.ReparsePoint);

        FolderViolation violation = Assert.Single(SkillFolderRules.Check(files, Folder));

        Assert.Equal(FolderLimit.Name, violation.Limit);
        Assert.Equal(linked, violation.Path);
        Assert.Contains("link", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>A reserved device name such as <c>CON.md</c> is a name violation.</summary>
    [Fact]
    public void ReservedDeviceName_Reported()
    {
        InMemoryFileReader files = new InMemoryFileReader().Add(Path.Combine(Folder, "CON.md"), "x");

        FolderViolation violation = Assert.Single(SkillFolderRules.Check(files, Folder));

        Assert.Equal(FolderLimit.Name, violation.Limit);
        Assert.False(SkillFolderRules.IsWindowsStorable("aux"));
        Assert.False(SkillFolderRules.IsWindowsStorable("trailing."));
        Assert.True(SkillFolderRules.IsWindowsStorable("console.md"));
    }

    /// <summary>Two names that differ only in case are a name violation.</summary>
    [Fact]
    public void CaseOnlyDuplicate_Reported()
    {
        InMemoryFileReader files = new InMemoryFileReader()
            .Add(Path.Combine(Folder, "Notes.md"), "x")
            .Add(Path.Combine(Folder, "notes.md"), "y");

        FolderViolation violation = Assert.Single(SkillFolderRules.Check(files, Folder));

        Assert.Equal(FolderLimit.Name, violation.Limit);
        Assert.Contains("case", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>A <c>.git</c> folder is skipped: its files neither count nor break a rule.</summary>
    [Fact]
    public void GitFolder_IsSkipped()
    {
        InMemoryFileReader files = new InMemoryFileReader().Add(Path.Combine(Folder, ".git", "CON"), "x");

        Assert.Empty(SkillFolderRules.Check(files, Folder));
    }
}

/// <summary>Pins that the library's <see cref="SkillFrontmatter"/> reads every SKILL.md shape the same way as the app's
/// <c>PersonaFrontmatter</c>, so moving the validator changed nothing.</summary>
public sealed class SkillValidatorParityTests
{
    /// <summary>SKILL.md texts covering plain, quoted, flow-list, block-list, folded and literal values, CRLF and no frontmatter.</summary>
    public static TheoryData<string> Texts { get; } = new(
        "---\nname: docx\ndescription: Edits Word files.\n---\nBody",
        "---\r\nname: 'doc''s'\r\ndescription: \"Quoted: yes\"\r\ntools: read_skill, propose_sprint\r\n---\r\n",
        "---\nname: a\ndescription: >\n  Folded line one\n  and two.\n\n  Second paragraph.\ntools: [read_skill, 'x, y']\n---\n",
        "---\nname: a\ndescription: |\n  Literal\n  lines\nlicense: MIT\n---\n",
        "---\nname: a\ntools:\n  - read_skill\n  - propose_sprint\ndescription: d\n---\n",
        "---\nname: a\ndescription: >-\n\nversion: 2\n---\n",
        "No frontmatter here.",
        "---\nname: never closed\n");

    /// <summary>The two parsers return the same name, description and tools for each text.</summary>
    [Theory]
    [MemberData(nameof(Texts))]
    public void SkillFrontmatter_Agrees_WithPersonaFrontmatter(string text)
    {
        IReadOnlyList<SkillFrontmatterField> library = SkillFrontmatter.Parse(text);
        (IReadOnlyList<PersonaFrontmatterField> app, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal(app.Select(static field => (field.Key, field.Value)), library.Select(static field => (field.Key, field.Value)));
        foreach (string key in new[] { "name", "description", "tools" })
        {
            Assert.Equal(app.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value, SkillFrontmatter.Get(library, key));
        }
    }
}
