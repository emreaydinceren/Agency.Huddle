using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §6.1 (<c>SkillCatalog</c>): the shipped Skills are compiled into the assembly as
/// embedded resources, so an install with an empty <c>{DataDir}</c> still has <c>team-building</c>,
/// and an upgrade delivers improved text without touching disk.
/// </summary>
public sealed class SkillCatalogTests
{
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
        List<string> expectedTools = ["validate_teammate", "propose_teammates"];
        Assert.Equal(expectedTools, teamBuildingValidation.Tools);
    }
}
