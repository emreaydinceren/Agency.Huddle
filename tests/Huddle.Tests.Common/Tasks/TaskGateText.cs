using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>The QA-evidence gate id, its refusal text and the gate-service builder, shared by the Task service gate tests, the board refusal tests and the tool tests.</summary>
internal static class TaskGateText
{
    internal const string Team = "Platform";
    internal const string Check = "dotnet test";
    internal const string QaDone ="Gate qa-evidence-before-done: an Evidence line from a member who verifies (for example QA Engineer) is needed before this Task can be Done.";
    internal const string QaGate ="'qa-evidence-before-done'";

    private static readonly string Manifest = Factories.TeamTypeJson.Manifest("web", "Web", order: 1, version: "1.2.0", rolesJson: """
        [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "qa", "title": "QA Engineer", "candidate": "candidates/qa.md", "covers": ["verify"] }
        ]
        """);

    /// <summary>A <c>_team.md</c> text enabling the given comma-joined, quoted gate ids (Spec 6.2: the settings live in the Team file).</summary>
    internal static string Process(string gates) =>
        $"---\nteamType: 'web@1.2.0'\ngates: [{gates}]\ncheckCommand: '{Check}'\n---\n";

    /// <summary>Builds a gate service for Team Platform whose <c>_team.md</c> is <paramref name="teamFileText"/>, or has no settings when null.</summary>
    internal static ProcessGateService BuildGates(TempDataDir dir, string? teamFileText, ITeamRenames? renames = null)
    {
        VersionedRef type = new("web", "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        TeamFile teamFile = teamFileText is not null && TeamFile.TryParse(teamFileText, out TeamFile? parsed)
            ? parsed
            : new TeamFile(type, null, null, null, string.Empty);
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = teamFile,
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/qa.md"] = "---\ntitle: QA Engineer\nconsult_when: verifying\n---\nYou verify.",
            ["web/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        return new ProcessGateService(catalog, folders, types, null, renames);
    }
}
