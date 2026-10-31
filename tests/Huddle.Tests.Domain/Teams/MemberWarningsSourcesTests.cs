using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="MemberWarnings.ForSources"/> (Skill Packs spec §6.10.7): withdrawn, deprecated, and a Pack no
/// SKILLS.md row provides any more, in that order.</summary>
public sealed class MemberWarningsSourcesTests
{
    /// <summary>A withdrawn Pack and Skill come first, then a deprecated Pack, each with the Source's reason.</summary>
    [Fact]
    public void ForSources_ListsWithdrawnThenDeprecated()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("pulled", "1.0.0", "Pulled", [])
            .WithPack("old", "1.0.0", "Old", [])
            .WithPackProblem("pulled", SkillAvailability.Withdrawn, "Licence changed.")
            .WithSkill("gone-skill", SkillAvailability.Withdrawn, "Unsafe");

        IReadOnlyList<string> warnings = MemberWarnings.ForSources(
            "Jane", ["gone-skill", "fine"], [new VersionedRef("old", "1.0.0"), new VersionedRef("pulled", "1.0.0")], skills,
            static id => id == "old" ? "Use the new one" : null);

        Assert.Equal(
            [
                "Pack \"pulled\" was withdrawn by its Source: Licence changed.",
                "Skill \"gone-skill\" was withdrawn by its Source: Unsafe.",
                "Pack \"old\" is deprecated: Use the new one.",
            ],
            warnings);
    }

    /// <summary>A Pack the Agent records that no row provides any more says so, and what to do.</summary>
    [Fact]
    public void ForSources_PackNotInRegistry_Warns()
    {
        IReadOnlyList<string> warnings = MemberWarnings.ForSources("Jane", [], [new VersionedRef("vanished", "1.0.0")], new FakeSkillAvailability(), static _ => null);

        Assert.Equal(["Pack \"vanished\" is no longer in SKILLS.md. Jane keeps its Skills; remove the Pack here, or ask your AI tool to add it back."], warnings);
    }

    /// <summary>Nothing wrong gives no warning.</summary>
    [Fact]
    public void ForSources_AllFine_Empty()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability().WithPack("fine", "1.0.0", "Fine", ["a"]);

        Assert.Empty(MemberWarnings.ForSources("Jane", ["a"], [new VersionedRef("fine", "1.0.0")], skills, static _ => null));
    }
}
