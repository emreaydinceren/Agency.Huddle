using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The smallest cast a manual test needs: two Teammates and the one-to-one Room each of them has, nothing else. It
/// replaces the built-in <c>echo</c> and <c>alpha</c> demo agents, which are off by default, with Teammates that run
/// on the free mock adapter and so go through the real Reply Gate, stream Drafts and cost nothing. It exists only
/// when someone builds it with <c>--scenario uat</c>; a normal install never has it.
/// </summary>
internal sealed class UatScenario : IScenario
{
    /// <summary>The Teammate that stands in for the old <c>echo</c> demo agent.</summary>
    internal const string Ada = "Ada";

    /// <summary>The Teammate that stands in for the old <c>alpha</c> demo agent.</summary>
    internal const string Bo = "Bo";

    private const string EmptyViewsJson = """
        {
          "version": 1,
          "views": []
        }
        """;

    private const string AvatarsJson = """
        {
          "You": { "label": "ME", "background": "#d46002" },
          "Ada": { "label": "AD", "background": "#2e7d6b" },
          "Bo": { "label": "BO", "background": "#3b6ea5" }
        }
        """;

    /// <inheritdoc/>
    public string Name => "uat";

    /// <inheritdoc/>
    public string Description => "Manual-test cast: Teammates Ada and Bo on the mock adapter, each with an empty one-to-one Room. Opt-in only.";

    /// <inheritdoc/>
    public SeedPlan Build(DateOnly today) => new()
    {
        Scenario = this.Name,
        HumanName = "You",
        Teammates =
        [
            new(
                Ada,
                "Test Teammate",
                "answers a manual test; echoes what it is asked",
                [],
                [],
                """
                You are Ada, a Teammate that exists only so a manual test has someone to talk to.
                Keep every reply short.
                """),
            new(
                Bo,
                "Test Teammate",
                "a second voice for tests that need two Teammates in one Room",
                [],
                [],
                """
                You are Bo, a second Teammate that exists only so a manual test has someone to talk to.
                Keep every reply short.
                """),
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
        AvatarsJson = AvatarsJson,
        Today = today,
    };
}
