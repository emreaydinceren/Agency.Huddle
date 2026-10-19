using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The web Team Type catalog and <see cref="ProcessProvisioner"/> builder shared by the provisioner and process-step tests.</summary>
internal static class ProcessProvisionerFixtures
{
    internal const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [
            { "id": "other", "file": "processes/other.md" },
            { "id": "flow", "file": "processes/flow.md", "default": true }
          ],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true }
          ] }
        """;

    internal const string FlowText = "---\ntitle: 'Flow'\ntemplate: 'flow@1.0.0'\ngates: ['qa-evidence-before-done']\n---\n# Flow\n\n## When it applies\n";

    internal static ProcessProvisioner Build(TempDataDir dir, ITeamFolders folders) =>
        new(Types(dir), folders);

    internal static TeamTypeCatalog Types(TempDataDir dir)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/processes/flow.md"] = FlowText,
            ["web/processes/other.md"] = "---\ntitle: 'Other'\n---\n# Other\n",
        };
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
    }
}
