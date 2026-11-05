using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins that <see cref="HireService.CheckDrafts"/> refuses the Human's own Name before anything is written (issue 351).</summary>
public sealed class HireServiceHumanNameTests
{
    private static DraftHire Draft(string name) =>
        new(name, "Developer", "You build things.", null, Avatar.None with { Background = "#3e9fe0" }, null, null);

    /// <summary>A draft named like the Human is refused with wording for the Human, and nothing is written.</summary>
    [Theory]
    [InlineData("Sam")]
    [InlineData("sam")]
    public void CheckDrafts_NameIsTheHumans_RefusesBeforeWriting(string name)
    {
        using TempDataDir dir = new();
        using PersonaStore personas = new(
            new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()),
            NullLogger<PersonaStore>.Instance, new ConfiguredHumanIdentity("Sam"));
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
        AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        HireLimitHarness harness = new(dir.Options());
        HireService service = new(personas, types, new FakeSkillAvailability(), avatars, harness.Guard, new FakeSkillCopier(), new FakeStartGate(), NullLogger<HireService>.Instance);

        string? refusal = service.CheckDrafts("Garden", [Draft(name)]);

        Assert.Equal($"'{name}' is your own name. Choose another for this Agent.", refusal);
        Assert.Empty(personas.Entries);
    }
}
