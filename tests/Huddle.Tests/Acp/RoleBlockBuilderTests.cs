namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

/// <summary>Pins how <see cref="RoleBlockBuilder"/> turns a hired member and its Team into a <see cref="RoleBlockInput"/> (Task 29).</summary>
public sealed class RoleBlockBuilderTests
{
    private static readonly RoleRef DeveloperRole = new("development", "developer", "1");

    /// <summary>Composes a Persona file for a member with an optional role and consult_when line.</summary>
    /// <param name="name">The Name.</param>
    /// <param name="title">The Title.</param>
    /// <param name="role">The hired role, or <see langword="null"/>.</param>
    /// <param name="consultWhen">The consult_when line, or <see langword="null"/>.</param>
    /// <returns>The Persona.</returns>
    private static Persona Member(string name, string title, RoleRef? role, string? consultWhen = null)
    {
        PersonaIdentity identity = new(name, title, name.ToLowerInvariant(), ["Apollo"], Role: role, ConsultWhen: consultWhen);
        return new Persona(name, PersonaFrontmatter.Compose(identity, "Body."));
    }

    /// <summary>Reads the identity out of a member's file.</summary>
    /// <param name="persona">The member.</param>
    /// <returns>The identity.</returns>
    private static PersonaIdentity Identity(Persona persona)
    {
        Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
        Assert.NotNull(identity);
        return identity;
    }

    /// <summary>A Persona with no <c>_role</c> gets no block.</summary>
    [Fact]
    public void Build_NoRole_ReturnsNull()
    {
        Persona ada = Member("Ada", "Developer", role: null);

        RoleBlockInput? block = RoleBlockBuilder.Build(Identity(ada), [], new FakeTeamCatalog(), _ => null);

        Assert.Null(block);
    }

    /// <summary>A role with no Team line gets no block, because there is no Team to name.</summary>
    [Fact]
    public void Build_RoleWithoutTeam_ReturnsNull()
    {
        PersonaIdentity identity = new("Ada", "Developer", "ada", [], Role: DeveloperRole);

        RoleBlockInput? block = RoleBlockBuilder.Build(identity, [], new FakeTeamCatalog(), _ => null);

        Assert.Null(block);
    }

    /// <summary>The consult list holds the other members, the manager first, each with its consult_when line; the member itself is left out.</summary>
    [Fact]
    public void Build_ListsOtherMembersManagerFirst()
    {
        Persona ada = Member("Ada", "Developer", DeveloperRole, "Ask me about code.");
        Persona grace = Member("Grace", "Lead", new RoleRef("development", "lead", "1"), "Ask me about scope.");
        Persona linus = Member("Linus", "Reviewer", new RoleRef("development", "reviewer", "1"));
        Dictionary<string, Persona> all = new(StringComparer.OrdinalIgnoreCase) { ["Ada"] = ada, ["Grace"] = grace, ["Linus"] = linus };
        FakeTeamCatalog catalog = new()
        {
            Teams = [new TeamSummary("Apollo", [], ["Ada", "Linus", "Grace"], true) { Manager = "Grace" }],
        };

        RoleBlockInput? block = RoleBlockBuilder.Build(Identity(ada), [], catalog, name => all.GetValueOrDefault(name));

        Assert.NotNull(block);
        Assert.Equal("Ada", block.Name);
        Assert.Equal("Developer", block.Role);
        Assert.Equal("Apollo", block.Team);
        Assert.Equal(["Grace", "Linus"], block.Consult.Select(line => line.Name));
        Assert.Equal("Ask me about scope.", block.Consult[0].ConsultWhen);
        Assert.Equal("Lead", block.Consult[0].Role);
    }

    /// <summary>The skill lines carry each resolved skill's name and description.</summary>
    [Fact]
    public void Build_MapsSkillsToLines()
    {
        Persona ada = Member("Ada", "Developer", DeveloperRole);
        Skill skill = new("code-review", "Use when reviewing a change.", [], [], SkillSource.Default, null);

        RoleBlockInput? block = RoleBlockBuilder.Build(Identity(ada), [skill], new FakeTeamCatalog(), _ => null);

        Assert.NotNull(block);
        SkillLine line = Assert.Single(block.Skills);
        Assert.Equal(new SkillLine("code-review", "Use when reviewing a change."), line);
    }
}
