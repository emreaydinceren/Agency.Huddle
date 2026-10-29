using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Agents;

/// <summary>
/// A fake team type catalog, skill pack store and hire context shared by the tests that propose
/// Candidates by role. The type <c>web</c> has a required manager role <c>lead</c> and a
/// <c>dev</c> role with one skill and one pack.
/// </summary>
internal static class RoleFixtures
{
    /// <summary>The Team name the role tests hire into.</summary>
    public const string TeamName = "Web Team";

    private static readonly string Manifest = Factories.TeamTypeJson.Manifest("web", "Web", order: 1, version: "1.2.0", rolesJson: """
        [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2,
              "skills": [{ "name": "test-driven-development", "source": "builtin" }], "packs": ["msbuild-authoring"] }
        ]
        """);

    /// <summary>Builds the fake team type catalog.</summary>
    /// <param name="dir">The data directory the catalog's options point at.</param>
    /// <returns>A catalog holding the <c>web</c> type.</returns>
    public static TeamTypeCatalog Types(TempDataDir dir)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code changes\n---\nYou build things.",
            ["web/processes/flow.md"] = "# Flow",
        };
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
    }

    /// <summary>A registry stand-in holding the <c>msbuild-authoring</c> Pack, every Skill present.</summary>
    /// <param name="dir">The data directory; kept so callers read as before.</param>
    public static Agency.Huddle.Tests.Teams.FakeSkillAvailability Packs(TempDataDir dir)
    {
        _ = dir;
        return new Agency.Huddle.Tests.Teams.FakeSkillAvailability().WithPack("msbuild-authoring", "1.0.0", "MSBuild", ["target-authoring"], ".csproj changes");
    }

    /// <summary>A hire context in which one adapter answered and nothing else is connected.</summary>
    public sealed class Context : IHireContext
    {
        /// <inheritdoc />
        public IReadOnlyList<AdapterAvailability> Answered { get; } = [new AdapterAvailability("claude-agent-acp", ["sonnet"])];

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; } =
            new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal)
            {
                ["claude-agent-acp"] = new Dictionary<ModelTier, string> { [ModelTier.Balanced] = "sonnet" },
            };

        /// <inheritdoc />
        public IReadOnlyCollection<string> AvoidAdapters { get; } = [];

        /// <inheritdoc />
        public IConnectorStatus Connectors { get; } = new EmptyConnectorStatus();
    }
}
