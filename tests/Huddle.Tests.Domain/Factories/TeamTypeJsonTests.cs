using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.Factories;

/// <summary>Tests for <see cref="TeamTypeJson"/>: its output loads through the production <see cref="TeamTypeCatalog"/> with the documented defaults.</summary>
public sealed class TeamTypeJsonTests
{
    private const string Candidate = """
        ---
        title: Lead
        consult_when: planning
        ---
        You lead.
        """;

    /// <summary>A default manifest loads from a built-in dictionary with no problems and the common literal's shape.</summary>
    [Fact]
    public void Manifest_Defaults_LoadsThroughProductionCatalogWithCommonShape()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = TeamTypeJson.Manifest("web"),
            ["web/candidates/lead.md"] = Candidate,
            ["web/processes/flow.md"] = "# Flow",
        };

        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);

        Assert.Empty(catalog.Problems);
        TeamTypeManifest? manifest = catalog.Find("web");
        Assert.NotNull(manifest);
        Assert.Equal("web", manifest.DisplayName);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal(1, manifest.Order);
        Assert.Equal(WorkspaceKind.None, manifest.Workspace.Kind);
        ProcessRef process = Assert.Single(manifest.Processes);
        Assert.Equal("processes/flow.md", process.File);
        Assert.True(process.Default);
        RoleSpec role = Assert.Single(manifest.Roles);
        Assert.Equal("lead", role.Id);
        Assert.True(role.Manager);
        Assert.True(role.Required);
    }

    /// <summary>Name, order, version, roles and extra members override the defaults and still load.</summary>
    [Fact]
    public void Manifest_Overrides_AreReadBack()
    {
        using TempDataDir dir = new();
        string roles = """[{ "id": "dev", "title": "Developer", "candidate": "candidates/lead.md", "manager": true, "maxPerTeam": 2, "covers": ["spec"] }]""";
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = TeamTypeJson.Manifest("web", "Web", order: 7, version: "1.2.0", rolesJson: roles, extraJson: "\"coverage\": [\"spec\"]"),
            ["web/candidates/lead.md"] = Candidate,
            ["web/processes/flow.md"] = "# Flow",
        };

        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);

        Assert.Empty(catalog.Problems);
        TeamTypeManifest? manifest = catalog.Find("web");
        Assert.NotNull(manifest);
        Assert.Equal("Web", manifest.DisplayName);
        Assert.Equal(7, manifest.Order);
        Assert.Equal("1.2.0", manifest.Version);
        Assert.Equal(["spec"], manifest.Coverage);
        RoleSpec role = Assert.Single(manifest.Roles);
        Assert.Equal(2, role.MaxPerTeam);
    }

    /// <summary>A process file path with backslashes is escaped so the manifest stays valid JSON.</summary>
    [Fact]
    public void Manifest_ProcessFileWithBackslash_StaysValidJson()
    {
        string json = TeamTypeJson.Manifest("web", processFile: "processes\\flow.md");

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        string? file = document.RootElement.GetProperty("processes")[0].GetProperty("file").GetString();
        Assert.Equal("processes\\flow.md", file);
    }

    /// <summary><see cref="TeamTypeJson.WriteManifest"/> writes <c>team-type.json</c> into a user type folder that the catalog loads.</summary>
    [Fact]
    public void WriteManifest_UserFolder_IsLoadedByCatalog()
    {
        using TempDataDir dir = new();
        string typeFolder = Path.Combine(dir.Path, "TeamTypes", "mine");
        TeamTypeJson.WriteManifest(typeFolder, "mine", "Mine", order: 3);
        Directory.CreateDirectory(Path.Combine(typeFolder, "candidates"));
        Directory.CreateDirectory(Path.Combine(typeFolder, "processes"));
        File.WriteAllText(Path.Combine(typeFolder, "candidates", "lead.md"), Candidate);
        File.WriteAllText(Path.Combine(typeFolder, "processes", "flow.md"), "# Flow");

        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Empty(catalog.Problems);
        TeamTypeManifest? manifest = catalog.Find("mine");
        Assert.NotNull(manifest);
        Assert.Equal("Mine", manifest.DisplayName);
        Assert.Equal(3, manifest.Order);
    }
}
