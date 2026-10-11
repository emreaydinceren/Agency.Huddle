using System.Text.Json;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Exercises <c>team-type.json</c> parsing against the Spec's field list and its defaults (TT-6).</summary>
public sealed class TeamTypeManifestTests
{
    /// <summary>A manifest that uses every field of the Spec's tables parses into the matching record values.</summary>
    [Fact]
    public void Parse_FullSpecManifest_MapsEveryField()
    {
        TeamTypeManifest manifest = Parse(FullManifest);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("development", manifest.Id);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("Development Team", manifest.DisplayName);
        Assert.Equal(1, manifest.Order);
        Assert.Equal(WorkspaceKind.Repo, manifest.Workspace.Kind);
        Assert.Equal("Repository", manifest.Workspace.Label);
        Assert.True(manifest.Workspace.Required);
        Assert.Equal("generate,challenge,verify", string.Join(",", manifest.Coverage));
        Assert.Equal("default", manifest.Processes[0].Id);
        Assert.Equal("processes/default.md", manifest.Processes[0].File);
        Assert.True(manifest.Processes[0].Default);
        Assert.Equal("manager", manifest.RetroRole);
    }

    /// <summary>Every role field of the Spec's role table parses, including the nested model and skill objects.</summary>
    [Fact]
    public void Parse_FullSpecRole_MapsEveryField()
    {
        TeamTypeManifest manifest = Parse(FullManifest);
        RoleSpec role = manifest.Roles[0];

        Assert.Equal("manager", role.Id);
        Assert.Equal("Development Manager", role.Title);
        Assert.Equal("candidates/manager.md", role.Candidate);
        Assert.True(role.Manager);
        Assert.True(role.Required);
        Assert.Equal(2, role.MaxPerTeam);
        Assert.Equal("generate", string.Join(",", role.Covers));
        Assert.Equal(ModelTier.Strongest, role.Model.Tier);
        Assert.Equal("reviewer", string.Join(",", role.Model.DifferentVendorFrom));
        Assert.Equal(2, role.Skills.Count);
        Assert.Equal("plan", role.Skills[0].Name);
        Assert.Equal("builtin", role.Skills[0].Source);
        Assert.Null(role.Skills[0].Path);
        Assert.Equal("owner/repo", role.Skills[1].Source);
        Assert.Equal("skills/tdd", role.Skills[1].Path);
        Assert.Equal("msbuild-authoring", string.Join(",", role.Packs));
        Assert.Equal("git:push", string.Join(",", role.Requires));
        Assert.Equal("Ada", string.Join(",", role.SuggestedNames));
    }

    /// <summary>A minimal manifest and role take the documented defaults for every omitted field.</summary>
    [Fact]
    public void Parse_OmittedFields_TakeDocumentedDefaults()
    {
        TeamTypeManifest manifest = Parse(MinimalManifest);
        RoleSpec role = manifest.Roles[0];

        Assert.Empty(manifest.Coverage);
        Assert.Empty(manifest.Processes);
        Assert.Null(manifest.Description);
        Assert.Null(role.Job);
        Assert.False(role.Pooled);
        Assert.Null(manifest.RetroRole);
        Assert.False(manifest.Workspace.Required);
        Assert.False(role.Manager);
        Assert.False(role.Required);
        Assert.Equal(1, role.MaxPerTeam);
        Assert.Empty(role.Covers);
        Assert.Equal(ModelTier.Balanced, role.Model.Tier);
        Assert.Empty(role.Model.DifferentVendorFrom);
        Assert.Empty(role.Skills);
        Assert.Empty(role.Packs);
        Assert.Empty(role.Requires);
        Assert.Empty(role.SuggestedNames);
    }

    /// <summary>A process without <c>default</c> is not the default.</summary>
    [Fact]
    public void Parse_ProcessWithoutDefault_IsNotTheDefault()
    {
        const string json = """
            {
              "schemaVersion": 1, "id": "x", "version": "1.0.0", "displayName": "X", "order": 1,
              "workspace": { "kind": "none", "label": "None" },
              "processes": [ { "id": "d", "file": "processes/d.md" } ],
              "roles": [ { "id": "r", "title": "R", "candidate": "c.md" } ]
            }
            """;

        TeamTypeManifest manifest = Parse(json);

        Assert.False(manifest.Processes[0].Default);
    }

    /// <summary>A role that refers to a hiring-pool job carries only its id, its job and its type-specific settings; the catalog fills the rest from the job.</summary>
    [Fact]
    public void Parse_JobReferenceRole_KeepsTheJobAndLeavesTitleAndCandidateEmpty()
    {
        const string json = """
            {
              "schemaVersion": 1, "id": "x", "version": "1.0.0", "order": 1,
              "workspace": { "kind": "none", "label": "None" },
              "roles": [ { "id": "r", "job": "bookkeeper", "required": true, "maxPerTeam": 2, "covers": [ "generate" ] } ]
            }
            """;

        TeamTypeManifest manifest = Parse(json);
        RoleSpec role = manifest.Roles[0];

        Assert.Equal("bookkeeper", role.Job);
        Assert.Null(role.Title);
        Assert.Null(role.Candidate);
        Assert.Null(manifest.DisplayName);
        Assert.True(role.Required);
        Assert.Equal(2, role.MaxPerTeam);
        Assert.False(role.Pooled);
    }

    /// <summary>A model tier written in each lower-case form maps to its enum value.</summary>
    [Theory]
    [InlineData("strongest", "Strongest")]
    [InlineData("balanced", "Balanced")]
    [InlineData("fast", "Fast")]
    public void Parse_ModelTier_MapsLowerCaseName(string tier, string expected)
    {
        TeamTypeManifest manifest = Parse(ManifestWithRole($$"""
            "model": { "tier": "{{tier}}" }
            """));

        Assert.Equal(expected, manifest.Roles[0].Model.Tier.ToString());
    }

    /// <summary>An unknown model tier is rejected with a JSON error rather than silently defaulted.</summary>
    [Fact]
    public void Parse_UnknownModelTier_ThrowsJsonException()
    {
        string json = ManifestWithRole("\"model\": { \"tier\": \"legendary\" }");

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TeamTypeManifest>(json, TeamTypeJson.Options));
    }

    /// <summary>The options accept comments and trailing commas, so hand-edited manifests load.</summary>
    [Fact]
    public void Parse_CommentsAndTrailingCommas_Parse()
    {
        TeamTypeManifest manifest = Parse(CommentedManifest);

        Assert.Equal("commented", manifest.Id);
        Assert.Equal(2, manifest.Coverage.Count);
    }

    /// <summary>A manifest that omits <c>retroRole</c> leaves it null, so no Retro agent is hired for that type.</summary>
    [Fact]
    public void Parse_RetroRoleAbsent_IsNull()
    {
        TeamTypeManifest manifest = Parse(MinimalManifest);

        Assert.Null(manifest.RetroRole);
    }

    /// <summary>Parses a manifest with the shared options, failing the test when the result is null.</summary>
    /// <param name="json">The manifest JSON.</param>
    /// <returns>The parsed manifest.</returns>
    private static TeamTypeManifest Parse(string json)
    {
        TeamTypeManifest? manifest = JsonSerializer.Deserialize<TeamTypeManifest>(json, TeamTypeJson.Options);
        Assert.NotNull(manifest);
        return manifest;
    }

    /// <summary>Builds a one-role manifest whose role carries the given extra JSON members.</summary>
    /// <param name="roleFields">JSON members appended to the role object, each ending with a comma or none.</param>
    /// <returns>The manifest JSON.</returns>
    private static string ManifestWithRole(string roleFields) => $$"""
        {
          "schemaVersion": 1, "id": "x", "version": "1.0.0", "displayName": "X", "order": 1,
          "workspace": { "kind": "none", "label": "None" },
          "roles": [ { "id": "r", "title": "R", "candidate": "c.md", {{roleFields}} } ]
        }
        """;

    /// <summary>A manifest that uses every field the Spec lists.</summary>
    private const string FullManifest = """
        {
          "schemaVersion": 1,
          "id": "development",
          "version": "1.0.0",
          "displayName": "Development Team",
          "order": 1,
          "workspace": { "kind": "repo", "label": "Repository", "required": true },
          "coverage": [ "generate", "challenge", "verify" ],
          "processes": [ { "id": "default", "file": "processes/default.md", "default": true } ],
          "roles": [
            {
              "id": "manager",
              "title": "Development Manager",
              "candidate": "candidates/manager.md",
              "manager": true,
              "required": true,
              "maxPerTeam": 2,
              "covers": [ "generate" ],
              "model": { "tier": "strongest", "differentVendorFrom": [ "reviewer" ] },
              "skills": [
                { "name": "plan", "source": "builtin", "path": null },
                { "name": "tdd", "source": "owner/repo", "path": "skills/tdd" }
              ],
              "packs": [ "msbuild-authoring" ],
              "requires": [ "git:push" ],
              "suggestedNames": [ "Ada" ]
            }
          ],
          "retroRole": "manager"
        }
        """;

    /// <summary>The smallest manifest that still has one role, so every omitted field is exercised.</summary>
    private const string MinimalManifest = """
        {
          "schemaVersion": 1,
          "id": "x",
          "version": "1.0.0",
          "displayName": "X",
          "order": 2,
          "workspace": { "kind": "none", "label": "None" },
          "roles": [ { "id": "r", "title": "R", "candidate": "c.md" } ]
        }
        """;

    /// <summary>A manifest with a line comment and trailing commas, which the options must accept.</summary>
    private const string CommentedManifest = """
        {
          // hand-edited manifest
          "schemaVersion": 1,
          "id": "commented",
          "version": "1.0.0",
          "displayName": "Commented",
          "order": 3,
          "workspace": { "kind": "folder", "label": "Folder", },
          "coverage": [ "generate", "verify", ],
          "roles": [ { "id": "r", "title": "R", "candidate": "c.md", }, ],
        }
        """;
}
