using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §8.1 (validating a Skill) against the Spec §7.2 <c>SKILL.md</c> frontmatter schema.
/// Every case builds the minimal <c>SKILL.md</c> text needed to isolate the one rule under test,
/// as an in-memory file-name → text map, and calls <see cref="SkillValidator.Validate"/> directly.
/// </summary>
public sealed class SkillValidatorTests
{
    /// <summary>A well-formed Skill yields no Errors and parses its Name, Description and Tools.</summary>
    [Fact]
    public void Validate_ValidSkill_HasNoErrorsAndParsesNameDescriptionTools()
    {
        string skillMd = """
            ---
            name: team-building
            description: Use when the Human wants to build a team.
            tools: [validate_teammate]
            ---
            Body text.
            """;
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.False(validation.HasErrors);
        Assert.Equal("team-building", validation.Name);
        Assert.Equal("Use when the Human wants to build a team.", validation.Description);
        List<string> expectedTools = ["validate_teammate"];
        Assert.Equal(expectedTools, validation.Tools);
    }

    /// <summary>A folder with no <c>SKILL.md</c> file is an Error.</summary>
    [Fact]
    public void Validate_NoSkillMdFile_IsError()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["roles.md"] = "Some content.",
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.True(validation.HasErrors);
        Assert.Contains(
            validation.Issues,
            issue => issue.Severity == SkillIssueSeverity.Error && issue.Message.Contains("SKILL.md", StringComparison.Ordinal));
    }

    /// <summary>A <c>name</c> that does not equal the folder name is an Error.</summary>
    [Fact]
    public void Validate_NameNotEqualToFolderName_IsError()
    {
        string skillMd = """
            ---
            name: other-skill
            description: Use when the Human wants to build a team.
            ---
            Body text.
            """;
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.True(validation.HasErrors);
    }

    /// <summary>A <c>name</c> that fails the <c>\A[a-z0-9]+(?:-[a-z0-9]+)*\z</c> regex is an Error, even when it equals the folder name.</summary>
    [Fact]
    public void Validate_NameFailsTheNameRegex_IsError()
    {
        string skillMd = """
            ---
            name: Team_Building
            description: Use when the Human wants to build a team.
            ---
            Body text.
            """;
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("Team_Building", files);

        Assert.True(validation.HasErrors);
    }

    /// <summary>A blank (whitespace-only) <c>description</c> is an Error.</summary>
    [Fact]
    public void Validate_BlankDescription_IsError()
    {
        string skillMd = """
            ---
            name: team-building
            description: "   "
            ---
            Body text.
            """;
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.True(validation.HasErrors);
    }

    /// <summary>A 301-character <c>description</c> is over the 300-char soft limit but under the 500-char hard limit: a Warning, not an Error.</summary>
    [Fact]
    public void Validate_Description301Characters_IsWarning()
    {
        string description = new('a', 301);
        string skillMd = "---\nname: team-building\ndescription: " + description + "\n---\nBody text.\n";
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.False(validation.HasErrors);
        Assert.Contains(validation.Issues, issue => issue.Severity == SkillIssueSeverity.Warning);
    }

    /// <summary>A 501-character <c>description</c> is over the 500-char hard limit: an Error.</summary>
    [Fact]
    public void Validate_Description501Characters_IsError()
    {
        string description = new('a', 501);
        string skillMd = "---\nname: team-building\ndescription: " + description + "\n---\nBody text.\n";
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.True(validation.HasErrors);
        Assert.Contains(validation.Issues, issue => issue.Severity == SkillIssueSeverity.Error);
    }

    /// <summary>A <c>tools</c> entry outside <c>SkillGrants.Grantable</c> is a Warning and is dropped from the returned <see cref="SkillValidation.Tools"/>.</summary>
    [Fact]
    public void Validate_UngrantableTool_IsWarningAndDroppedFromTools()
    {
        string skillMd = """
            ---
            name: team-building
            description: Use when the Human wants to build a team.
            tools: [post_message]
            ---
            Body text.
            """;
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.False(validation.HasErrors);
        Assert.Contains(validation.Issues, issue => issue.Severity == SkillIssueSeverity.Warning);
        Assert.Empty(validation.Tools);
    }

    /// <summary>An unknown frontmatter key is an Info, not a Warning or an Error.</summary>
    [Fact]
    public void Validate_UnknownKey_IsInfo()
    {
        string skillMd = """
            ---
            name: team-building
            description: Use when the Human wants to build a team.
            author: Ada
            ---
            Body text.
            """;
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.False(validation.HasErrors);
        Assert.Contains(validation.Issues, issue => issue.Severity == SkillIssueSeverity.Info);
    }

    /// <summary>A supporting file over 65,536 bytes is a Warning and is dropped from the returned <see cref="SkillValidation.Files"/>.</summary>
    [Fact]
    public void Validate_FileOver64KB_IsWarningAndDroppedFromFiles()
    {
        string skillMd = """
            ---
            name: team-building
            description: Use when the Human wants to build a team.
            ---
            Body text.
            """;
        string oversizedText = new('a', 70_000);
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = skillMd,
            ["big.md"] = oversizedText,
        };

        SkillValidation validation = SkillValidator.Validate("team-building", files);

        Assert.Contains(validation.Issues, issue => issue.Severity == SkillIssueSeverity.Warning);
        Assert.DoesNotContain("big.md", validation.Files);
        Assert.Contains("SKILL.md", validation.Files);
    }
}
