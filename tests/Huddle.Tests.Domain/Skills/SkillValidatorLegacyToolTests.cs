using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>Pins <see cref="SkillValidator"/>'s upgrade of the tool names used before the Teammate-to-Agent rename.</summary>
public sealed class SkillValidatorLegacyToolTests
{
    private const string Body = "Use this when the Human wants a team built, and read the steps below.";

    /// <summary>A Skill that lists the old tool names still grants the renamed tools.</summary>
    [Fact]
    public void Validate_LegacyToolNames_AreGrantedAsTheRenamedTools()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = $"---\nname: old-skill\ndescription: {Body}\ntools: [validate_teammate, propose_teammates]\n---\nSteps.\n",
        };

        SkillValidation result = SkillValidator.Validate("old-skill", files);

        Assert.Equal(["validate_agent", "propose_agents"], result.Tools);
    }

    /// <summary>A Skill that already lists the new names is validated as written, with the same files object.</summary>
    [Fact]
    public void Validate_CurrentToolNames_AreGranted()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = $"---\nname: new-skill\ndescription: {Body}\ntools: [validate_agent]\n---\nSteps.\n",
        };

        SkillValidation result = SkillValidator.Validate("new-skill", files);

        Assert.Equal(["validate_agent"], result.Tools);
    }
}
