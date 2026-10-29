using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Agents;

/// <summary>Tests for <see cref="RoleHirer"/>: a role Candidate is hired from its role, and one it cannot place is refused.</summary>
public sealed class RoleHirerTests
{
    /// <summary>A Candidate whose role is not known, or that names no Team, is refused in plain words and nothing is written.</summary>
    [Fact]
    public void Hire_UnknownRoleOrNoTeam_RefusedWithoutWriting()
    {
        using TempDataDir dir = new();
        TeamOptions options = new();
        PersonaStore personas = new(new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        FakeSkillCopier installer = new();
        HireLimitHarness harness = new(Microsoft.Extensions.Options.Options.Create(options));
        TeamTypeCatalog types = RoleFixtures.Types(dir);
        FakeSkillAvailability packs = RoleFixtures.Packs(dir);
        HireService hires = new(personas, types, packs, new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance), harness.Guard, installer, new FakeStartGate(), NullLogger<HireService>.Instance);
        RoleHirer hirer = new(hires, types, packs, new RoleFixtures.Context());

        HireResult unknown = hirer.Hire(new Candidate("Jane", "jane", "Dev", "Body.", [RoleFixtures.TeamName], null, "web/ghost"));
        HireResult noTeam = hirer.Hire(new Candidate("Kai", "kai", "Dev", "Body.", [], null, "web/dev"));

        Assert.False(unknown.Ok);
        Assert.Equal("Jane does not name a known role and Team.", unknown.Error);
        Assert.False(noTeam.Ok);
        Assert.Equal("Kai does not name a known role and Team.", noTeam.Error);
        Assert.Null(personas.Get("Jane"));
    }
}
