using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Tests.TeamTypes;

namespace Agency.Huddle.Tests.SkillPacks.Skills;

/// <summary>Pins SB-R23's first half (Spec §6.8): the three Second Brain Skills are installed in the catalog and granted to every Team manager and the Chief of Staff, and to no one else.</summary>
public sealed class SecondBrainSkillsTests
{
    private static readonly string[] SecondBrainSkills = ["ingest", "wrap-up", "brain-check"];

    /// <summary>Team types whose manager and members the grants are checked on: one per manager job family.</summary>
    private static readonly string[] TypeIds = ["development", "household", "book-project"];

    /// <summary>The catalog holds each of the three Skills, valid, with a description under 300 characters and one SKILL.md.</summary>
    [Theory]
    [InlineData("ingest")]
    [InlineData("wrap-up")]
    [InlineData("brain-check")]
    public void Catalog_ContainsSkill_Valid(string name)
    {
        Assert.Contains(name, SkillCatalog.All.Keys);
        IReadOnlyDictionary<string, string> files = SkillCatalog.All[name];

        SkillValidation validation = SkillValidator.Validate(name, files);

        Assert.Equal(["SKILL.md"], files.Keys.ToList());
        Assert.Empty(validation.Issues);
        Assert.Empty(validation.Tools);
    }

    /// <summary>A Team manager of every sampled type holds all three Skills.</summary>
    [Fact]
    public void TeamManager_HoldsTheThreeSkills()
    {
        foreach (string typeId in TypeIds)
        {
            RoleSpec manager = Assert.Single(BuiltinManifests.Load(typeId).Roles, static r => r.Manager);

            foreach (string skill in SecondBrainSkills)
            {
                Assert.Contains(new SkillRef(skill, "builtin", skill), manager.Skills);
            }
        }
    }

    /// <summary>A member who is not the manager holds none of the three Skills.</summary>
    [Fact]
    public void NonManagerMember_HoldsNoneOfTheThreeSkills()
    {
        foreach (string typeId in TypeIds)
        {
            foreach (RoleSpec member in BuiltinManifests.Load(typeId).Roles.Where(static r => !r.Manager))
            {
                Assert.DoesNotContain(member.Skills, s => SecondBrainSkills.Contains(s.Name, StringComparer.Ordinal));
            }
        }
    }

    /// <summary>The built-in Chief of Staff holds the three Skills.</summary>
    [Fact]
    public void ChiefOfStaff_HoldsTheThreeSkills()
    {
        bool parsed = PersonaFrontmatter.TryReadIdentity(BuiltinAgent.DefaultText, out PersonaIdentity? identity, out string error);

        Assert.True(parsed, error);
        Assert.NotNull(identity);
        Assert.Subset(identity.Skills?.ToHashSet(StringComparer.Ordinal) ?? [], SecondBrainSkills.ToHashSet(StringComparer.Ordinal));
    }
}
