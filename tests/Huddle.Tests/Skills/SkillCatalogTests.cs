using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §6.1 (<c>SkillCatalog</c>): the shipped Skills are compiled into the assembly as
/// embedded resources, so an install with an empty <c>{DataDir}</c> still has <c>team-building</c>,
/// and an upgrade delivers improved text without touching disk.
/// </summary>
public sealed class SkillCatalogTests
{
    /// <summary>The two grantable tools that the team-building Skill must list in its frontmatter (Spec §6.5).</summary>
    private static readonly string[] ExpectedTools = ["validate_teammate", "propose_teammates"];

    /// <summary>The catalog holds <c>team-building</c> with exactly its four shipped files, keyed ordinally.</summary>
    [Fact]
    public void All_ContainsTeamBuilding_WithItsFourFiles()
    {
        IReadOnlyDictionary<string, string> teamBuilding = SkillCatalog.All["team-building"];

        HashSet<string> expectedFiles = new(StringComparer.Ordinal)
        {
            "SKILL.md",
            "onboarding.md",
            "roles.md",
            "team-patterns.md",
        };

        Assert.Equal(expectedFiles, teamBuilding.Keys.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Every embedded file's text has already been normalised to <c>\n</c> line endings: no value
    /// contains a carriage return.
    /// </summary>
    [Fact]
    public void All_FileText_HasNoCarriageReturns()
    {
        Assert.NotEmpty(SkillCatalog.All);

        foreach (var files in SkillCatalog.All.Values)
        {
            foreach (var text in files.Values)
            {
                Assert.DoesNotContain('\r', text);
            }
        }
    }

    /// <summary>
    /// Enforces Spec §6.1 (Constraints): a shipped Skill that fails <see cref="SkillValidator.Validate"/>
    /// is a test failure here, never a runtime warning. Every Skill in <see cref="SkillCatalog.All"/>
    /// must come back with zero Issues at any severity — Info and Warning included, not just Error —
    /// and <c>team-building</c>'s two-entry <c>tools: [validate_teammate, propose_teammates]</c> flow
    /// list must survive into <see cref="SkillValidation.Tools"/> unchanged, in order.
    /// </summary>
    [Fact]
    public void All_EveryShippedSkill_PassesValidationWithNoErrorsOrWarnings()
    {
        foreach (var (skillName, files) in SkillCatalog.All)
        {
            SkillValidation validation = SkillValidator.Validate(skillName, files);

            Assert.True(
                validation.Issues.Count == 0,
                $"Shipped Skill '{skillName}' has validation issues:\n" +
                string.Join('\n', validation.Issues.Select(issue => $"  [{issue.Severity}] {issue.Message}")));
        }

        SkillValidation teamBuildingValidation = SkillValidator.Validate("team-building", SkillCatalog.All["team-building"]);
        Assert.Equal(ExpectedTools.ToList(), teamBuildingValidation.Tools);
    }

    /// <summary>
    /// The shipped <c>team-building</c> Skill honours its content contract (Spec §6.13). Checks the
    /// <c>tools</c> field, prohibited tool prefix, grantable tool names, file naming, description
    /// length, and greeting turn wording.
    /// </summary>
    [Fact]
    public void TeamBuilding_Tools_AreExactlyTheGrantableTwo()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> toolViolations = violations.Where(v => v.StartsWith("tools:", StringComparison.Ordinal)).ToList();
        Assert.Empty(toolViolations);
    }

    /// <summary>
    /// No file in the shipped <c>team-building</c> Skill contains the reserved MCP tool prefix
    /// (Spec §6.13 Constraints, §12 F-1).
    /// </summary>
    [Fact]
    public void TeamBuilding_NoFileContainsToolPrefix()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> prefixViolations = violations.Where(v => v.StartsWith("prefix:", StringComparison.Ordinal)).ToList();
        Assert.Empty(prefixViolations);
    }

    /// <summary>
    /// The shipped <c>team-building</c> SKILL.md names <c>propose_teammates</c>, never
    /// <c>create_teammate</c>, and no file mentions that obsolete tool name (Spec §6.13 Changes).
    /// </summary>
    [Fact]
    public void TeamBuilding_SkillMd_NamesProposeTeammates_NeverCreateTeammate()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> createTeammateViolations = violations.Where(v => v.StartsWith("create_teammate:", StringComparison.Ordinal)).ToList();
        Assert.Empty(createTeammateViolations);
    }

    /// <summary>
    /// Every supporting file in the shipped <c>team-building</c> Skill other than <c>SKILL.md</c> is
    /// named in <c>SKILL.md</c> (Spec §6.13, rule 4).
    /// </summary>
    [Fact]
    public void TeamBuilding_EverySupportingFile_IsNamedInSkillMd()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> unnamedViolations = violations.Where(v => v.StartsWith("unnamed-file:", StringComparison.Ordinal)).ToList();
        Assert.Empty(unnamedViolations);
    }

    /// <summary>
    /// The shipped <c>team-building</c> Skill's description is at most 300 characters (Spec §6.13,
    /// rule 5).
    /// </summary>
    [Fact]
    public void TeamBuilding_Description_AtMost300Chars()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> descriptionViolations = violations.Where(v => v.StartsWith("description:", StringComparison.Ordinal)).ToList();
        Assert.Empty(descriptionViolations);
    }

    /// <summary>
    /// The shipped <c>team-building</c> SKILL.md's step 0 contains the agreed greeting turn wording
    /// (Spec §6.13, rule 6).
    /// </summary>
    [Fact]
    public void TeamBuilding_SkillMdStep0_KeysOnGreetingTurn()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> step0Violations = violations.Where(v => v.StartsWith("step0:", StringComparison.Ordinal)).ToList();
        Assert.Empty(step0Violations);
    }

    /// <summary>
    /// RS §6.11 (Coordinators and Following, RS-T13): the shipped <c>team-patterns.md</c> teaches a
    /// coordinator following several work Rooms how to keep an overview - one memory file per piece
    /// of work, a work Room reporting back by Mentioning the coordinator (a worker cannot post into
    /// the coordinator's own Room with the Human: it is not a Member there), and reading the
    /// <c>create_room</c> seed as that Room's own first Message. Asserts on short, stable phrases
    /// per this task's own instruction, not whole sentences.
    /// </summary>
    [Fact]
    public void TeamBuilding_TeachesCoordinatorRoutes()
    {
        string teamPatterns = SkillCatalog.All["team-building"]["team-patterns.md"];

        Assert.Contains("memory folder", teamPatterns, StringComparison.Ordinal);
        Assert.Contains("Mention you", teamPatterns, StringComparison.Ordinal);
        Assert.Contains("seed", teamPatterns, StringComparison.Ordinal);
    }

    /// <summary>
    /// The content contract checks can fail when a Skill violates it. This test builds an in-memory
    /// copy of the shipped <c>team-building</c> files, mutates its <c>SKILL.md</c> frontmatter and
    /// body to violate the contract, and asserts the helper catches both violations.
    /// </summary>
    [Fact]
    public void ContentContract_PreSpecDraft_ReportsViolations()
    {
        // Create a mutable copy of team-building files.
        Dictionary<string, string> files = new(SkillCatalog.All["team-building"], StringComparer.Ordinal);

        // Mutate SKILL.md: change frontmatter tools to list create_teammate instead of validate_teammate.
        string skillMd = files["SKILL.md"];
        string mutatedSkillMd = skillMd.Replace(
            "tools: [validate_teammate, propose_teammates]",
            "tools: [validate_teammate, create_teammate]",
            StringComparison.Ordinal);

        // Also add a mention of create_teammate to the body so it fails the create_teammate check too.
        mutatedSkillMd = mutatedSkillMd.Replace(
            "Call `validate_teammate`",
            "Call `create_teammate` and `validate_teammate`",
            StringComparison.Ordinal);

        files["SKILL.md"] = mutatedSkillMd;

        // Check violations.
        List<string> violations = ContentContractViolations(files);

        // Assert both a tools: violation and a create_teammate: violation are reported.
        List<string> toolsViolations = violations.Where(v => v.StartsWith("tools:", StringComparison.Ordinal)).ToList();
        List<string> createTeammateViolations = violations.Where(v => v.StartsWith("create_teammate:", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(toolsViolations);
        Assert.NotEmpty(createTeammateViolations);
    }

    /// <summary>
    /// Validates a Skill's files against the content contract (Spec §6.13). Returns one human-readable
    /// violation string per violation found, each prefixed with a kind tag: <c>tools:</c>,
    /// <c>prefix:</c>, <c>create_teammate:</c>, <c>unnamed-file:</c>, <c>description:</c>, or
    /// <c>step0:</c>. An empty list means the Skill is compliant.
    /// </summary>
    /// <param name="files">Every file in the Skill, keyed by file name.</param>
    /// <returns>Violation strings, one per violation, each tagged with its kind.</returns>
    private static List<string> ContentContractViolations(IReadOnlyDictionary<string, string> files)
    {
        List<string> violations = [];

        // Rule 1: tools must be exactly [validate_teammate, propose_teammates].
        // We need to check the raw frontmatter because SkillValidator.Validate drops ungrantable tools.
        if (files.TryGetValue("SKILL.md", out string? skillMdText))
        {
            (IReadOnlyList<PersonaFrontmatterField> fields, _) = PersonaFrontmatter.Parse(skillMdText);
            string? rawTools = null;
            foreach (var field in fields)
            {
                if (string.Equals(field.Key, "tools", StringComparison.OrdinalIgnoreCase))
                {
                    rawTools = field.Value;
                    break;
                }
            }

            if (rawTools is not null)
            {
                // Parse the flow list and check it's exactly the two grantable tools.
                List<string> toolsInFrontmatter = [];
                foreach (var tool in rawTools.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    toolsInFrontmatter.Add(tool);
                }

                if (!toolsInFrontmatter.SequenceEqual(ExpectedTools, StringComparer.Ordinal))
                {
                    violations.Add($"tools: frontmatter lists {string.Join(", ", toolsInFrontmatter)}, expected {string.Join(", ", ExpectedTools)}");
                }
            }
            else
            {
                violations.Add("tools: frontmatter is missing the 'tools' field");
            }
        }

        // Rule 2: no file contains mcp__.
        foreach (var (fileName, text) in files)
        {
            if (text.Contains("mcp__", StringComparison.Ordinal))
            {
                violations.Add($"prefix: file '{fileName}' contains the reserved tool prefix 'mcp__'");
                break;
            }
        }

        // Rule 3: SKILL.md names propose_teammates and no file contains create_teammate.
        bool skillMdNamesPropose = false;
        bool anyFileNamesCreateTeammate = false;

        if (files.TryGetValue("SKILL.md", out skillMdText))
        {
            if (skillMdText.Contains("propose_teammates", StringComparison.Ordinal))
            {
                skillMdNamesPropose = true;
            }
        }

        foreach (var (_, text) in files)
        {
            if (text.Contains("create_teammate", StringComparison.Ordinal))
            {
                anyFileNamesCreateTeammate = true;
                break;
            }
        }

        if (!skillMdNamesPropose)
        {
            violations.Add("create_teammate: SKILL.md does not name 'propose_teammates'");
        }

        if (anyFileNamesCreateTeammate)
        {
            violations.Add("create_teammate: at least one file mentions the obsolete 'create_teammate' tool");
        }

        // Rule 4: every file other than SKILL.md is named in SKILL.md.
        if (files.TryGetValue("SKILL.md", out skillMdText))
        {
            foreach (var (fileName, _) in files)
            {
                if (!string.Equals(fileName, "SKILL.md", StringComparison.Ordinal))
                {
                    if (!skillMdText.Contains(fileName, StringComparison.Ordinal))
                    {
                        violations.Add($"unnamed-file: supporting file '{fileName}' is not mentioned in SKILL.md");
                    }
                }
            }
        }

        // Rule 5: description <= 300 characters.
        SkillValidation validation = SkillValidator.Validate("team-building", files);
        if (validation.Description is not null && validation.Description.Length > 300)
        {
            violations.Add($"description: {validation.Description.Length} characters exceeds the limit of 300");
        }

        // Rule 6: SKILL.md contains "greet" and "has not written" (case-insensitive).
        if (files.TryGetValue("SKILL.md", out skillMdText))
        {
            bool hasGreet = skillMdText.Contains("greet", StringComparison.OrdinalIgnoreCase);
            bool hasHasNotWritten = skillMdText.Contains("has not written", StringComparison.OrdinalIgnoreCase);

            if (!hasGreet)
            {
                violations.Add("step0: SKILL.md does not contain 'greet'");
            }

            if (!hasHasNotWritten)
            {
                violations.Add("step0: SKILL.md does not contain 'has not written'");
            }
        }

        return violations;
    }
}
