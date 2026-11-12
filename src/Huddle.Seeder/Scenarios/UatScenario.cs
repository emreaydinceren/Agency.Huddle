using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The smallest cast a manual test needs: two Agents and the one-to-one Room each of them has, nothing else. It
/// replaces the built-in <c>echo</c> and <c>alpha</c> demo agents, which are off by default, with Agents that run
/// on the free mock adapter and so go through the real Reply Gate, stream Drafts and cost nothing. It exists only
/// when someone builds it with <c>--scenario uat</c>; a normal install never has it.
/// Seeder spec §8 (SEED-UAT-1 to -3).
/// </summary>
internal sealed class UatScenario : IScenario
{
    /// <summary>The Agent that stands in for the old <c>echo</c> demo agent.</summary>
    internal const string Ada = "Ada";

    /// <summary>The Agent that stands in for the old <c>alpha</c> demo agent.</summary>
    internal const string Bo = "Bo";

    private const string EmptyViewsJson = """
        {
          "version": 1,
          "views": []
        }
        """;

    /// <inheritdoc/>
    public string Name => "uat";

    /// <inheritdoc/>
    public string Description => "Manual-test cast: Agents Ada and Bo on the mock adapter, each with an empty one-to-one Room. Opt-in only.";

    /// <inheritdoc/>
    public SeedPlan Build(DateOnly today) => new()
    {
        Scenario = this.Name,
        HumanName = "You",
        Agents =
        [
            new(
                Ada,
                "Test Agent",
                "answers a manual test; echoes what it is asked",
                [],
                [],
                """
                You are Ada, an Agent that exists only so a manual test has someone to talk to.
                Keep every reply short.
                """)
            {
                Gender = SeedGender.Woman,
                AvatarFile = "pixel-avatar_fair_f_21.png",
            },
            new(
                Bo,
                "Test Agent",
                "a second voice for tests that need two Agents in one Room",
                [],
                [],
                """
                You are Bo, a second Agent that exists only so a manual test has someone to talk to.
                Keep every reply short.
                """)
            {
                Gender = SeedGender.Man,
                AvatarFile = "pixel-avatar_tan_m_17.png",
            },
        ],
        Skills = [],
        Teams = [],
        Tasks = [],
        Files = [],
        Rooms =
        [
            new("dm-ada", Ada, [Ada], false, []),
            new("dm-bo", Bo, [Bo], false, []),
        ],
        ViewsJson = EmptyViewsJson,
        Today = today,
    };
}
