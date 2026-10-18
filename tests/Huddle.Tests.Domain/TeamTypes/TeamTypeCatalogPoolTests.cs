using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for how <see cref="TeamTypeCatalog"/> composes a type's roles from hiring-pool jobs: the fields each side owns, the pooled roles it adds, and the types it rejects.</summary>
public sealed class TeamTypeCatalogPoolTests
{
    private const string Manifest = """
        { "schemaVersion": 1, "id": "alpha", "version": "1.2.0", "order": 5,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "coverage": [ "make", "check" ],
          "processes": [ { "id": "flow", "file": "processes/flow.md", "default": true } ],
          "roles": [
            { "id": "lead", "job": "lead", "manager": true, "required": true, "maxPerTeam": 1, "covers": [ "check" ] },
            { "id": "maker", "job": "maker", "maxPerTeam": 3, "covers": [ "make" ], "model": { "differentVendorFrom": [ "lead" ] } }
          ] }
        """;

    private const string PoolJson = """
        { "schemaVersion": 1,
          "teamTypes": [ { "id": "alpha", "name": "Alpha Team", "description": "Does alpha work." } ],
          "jobs": [
            { "id": "lead", "title": "Team Lead", "model": { "tier": "strongest" }, "suggestedNames": [ "Lena" ],
              "skillRefs": [ { "name": "plan", "source": "builtin", "path": "plan" } ] },
            { "id": "maker", "title": "Maker", "model": { "tier": "fast" }, "requires": [ "email:read" ], "packs": [ "starter" ] },
            { "id": "tutor", "title": "Tutor", "suggestedNames": [ "Tess" ] }
          ] }
        """;

    private static Dictionary<string, string> Personas() => new(StringComparer.Ordinal)
    {
        ["personas/lead.md"] = "---\ntitle: 'Team Lead'\nconsult_when: 'A decision is needed'\n---\nYou lead.",
        ["personas/maker.md"] = "---\ntitle: 'Maker'\n---\nYou make things.",
        ["personas/tutor.md"] = "---\ntitle: 'Tutor'\n---\nYou teach.",
    };

    private static TeamTypeCatalog Create(TempDataDir dir, string manifest = Manifest, string pool = PoolJson)
    {
        Dictionary<string, string> poolFiles = Personas();
        poolFiles["hiringpool.json"] = pool;
        HiringPoolCatalog catalog = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, poolFiles);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["alpha/team-type.json"] = manifest,
            ["alpha/processes/flow.md"] = "# Flow",
        };
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins, catalog);
    }

    /// <summary>A role that names a job takes its title, persona, skills, tier, connectors, names and packs from the job, and its manager, limit, coverage and vendor rule from the type.</summary>
    [Fact]
    public void Find_JobRoles_AreComposedFromTheirJobs()
    {
        using TempDataDir dir = new();

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(Create(dir).Find("alpha"));

        RoleSpec lead = type.Roles.Single(static role => role.Id == "lead");
        Assert.Equal("Team Lead", lead.Title);
        Assert.Equal(ModelTier.Strongest, lead.Model.Tier);
        Assert.Equal(["Lena"], lead.SuggestedNames);
        Assert.Equal([new SkillRef("plan", "builtin", "plan")], lead.Skills);
        Assert.True(lead.Manager);
        Assert.True(lead.Required);
        Assert.Equal(["check"], lead.Covers);
        RoleSpec maker = type.Roles.Single(static role => role.Id == "maker");
        Assert.Equal(ModelTier.Fast, maker.Model.Tier);
        Assert.Equal(["lead"], maker.Model.DifferentVendorFrom);
        Assert.Equal(["email:read"], maker.Requires);
        Assert.Equal(["starter"], maker.Packs);
        Assert.Equal(3, maker.MaxPerTeam);
        Assert.False(lead.Pooled);
    }

    /// <summary>The type takes its display name and description from the hiring pool when the manifest gives none.</summary>
    [Fact]
    public void Find_ManifestWithoutDisplayName_TakesNameAndDescriptionFromThePool()
    {
        using TempDataDir dir = new();

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(Create(dir).Find("alpha"));

        Assert.Equal("Alpha Team", type.DisplayName);
        Assert.Equal("Does alpha work.", type.Description);
    }

    /// <summary>A pool job the type does not list is added as an optional role with default settings, marked pooled, so it can be hired.</summary>
    [Fact]
    public void Find_PoolJobNotListed_IsAppendedAsAPooledOptionalRole()
    {
        using TempDataDir dir = new();

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(Create(dir).Find("alpha"));

        RoleSpec tutor = type.Roles.Single(static role => role.Id == "tutor");
        Assert.True(tutor.Pooled);
        Assert.Equal("tutor", tutor.Job);
        Assert.False(tutor.Manager);
        Assert.False(tutor.Required);
        Assert.Equal(1, tutor.MaxPerTeam);
        Assert.Empty(tutor.Covers);
        Assert.Equal(["Tess"], tutor.SuggestedNames);
        Assert.DoesNotContain(type.Roles.Where(static role => role.Id is "lead" or "maker"), static role => role.Pooled);
    }

    /// <summary>A role that names a job the pool does not hold rejects the type and names the job in the problem.</summary>
    [Fact]
    public void Find_RoleNamesUnknownJob_RejectsTheType()
    {
        using TempDataDir dir = new();
        string manifest = Manifest.Replace("\"job\": \"maker\"", "\"job\": \"ghost\"", StringComparison.Ordinal);

        TeamTypeCatalog catalog = Create(dir, manifest);

        Assert.Null(catalog.Find("alpha"));
        TeamTypeFileProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("alpha", problem.TypeId);
        Assert.Equal("The role 'maker' names the hiring-pool job 'ghost', which the pool does not hold.", problem.Message);
    }

    /// <summary>A manifest with no display name, for a type the pool does not name, is rejected rather than shown without a name.</summary>
    [Fact]
    public void Find_NoDisplayNameAndNotNamedByThePool_RejectsTheType()
    {
        using TempDataDir dir = new();
        string pool = PoolJson.Replace("\"id\": \"alpha\"", "\"id\": \"other\"", StringComparison.Ordinal);

        TeamTypeCatalog catalog = Create(dir, pool: pool);

        Assert.Null(catalog.Find("alpha"));
        Assert.Equal("The manifest has no displayName and the hiring pool does not name the type.", Assert.Single(catalog.Problems).Message);
    }

    /// <summary>The persona of a role, listed or pooled, is read from its pool job.</summary>
    [Fact]
    public void ReadCandidate_JobRoleListedOrPooled_ReadsThePersonaFromThePool()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal("Team Lead", catalog.ReadCandidate("alpha", "lead")?.Title);
        Assert.Equal("A decision is needed", catalog.ReadCandidate("alpha", "lead")?.JobDescriptionFields.Single(static field => field.Key == "consult_when").Value);
        Assert.Equal("Tutor", catalog.ReadCandidate("alpha", "tutor")?.Title);
        Assert.Null(catalog.ReadCandidate("alpha", "nobody"));
    }

    /// <summary>When the hiring pool reloads, the catalog reloads its types, so a job added to the user pool shows up as a pooled role.</summary>
    [Fact]
    public void Reload_WhenThePoolChanges_RefreshesTheTypes()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> poolFiles = Personas();
        poolFiles["hiringpool.json"] = PoolJson;
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, poolFiles);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["alpha/team-type.json"] = Manifest,
            ["alpha/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins, pool);
        Assert.DoesNotContain(catalog.Find("alpha")?.Roles ?? [], static role => role.Id == "newbie");
        string userDir = Path.Combine(dir.Path, "HiringPool", "personas");
        Directory.CreateDirectory(userDir);
        File.WriteAllText(Path.Combine(dir.Path, "HiringPool", "hiringpool.json"), """{ "schemaVersion": 1, "jobs": [ { "id": "newbie", "title": "Newbie" } ] }""");
        File.WriteAllText(Path.Combine(userDir, "newbie.md"), "---\ntitle: 'Newbie'\n---\nYou are new.");

        pool.Reload();

        Assert.Contains(catalog.Find("alpha")?.Roles ?? [], static role => role.Id == "newbie" && role.Pooled);
    }

    /// <summary>
    /// A copy of a built-in type that Custom role saved before the pool existed names candidate files that no longer exist; each such
    /// role whose id is a pool job is composed from that job, and the user's own custom role keeps its candidate file.
    /// </summary>
    [Fact]
    public void Find_OldUserCopyWithMovedCandidates_LoadsFromThePool()
    {
        using TempDataDir dir = new();
        string typeDir = Path.Combine(dir.Path, "TeamTypes", "alpha");
        Directory.CreateDirectory(Path.Combine(typeDir, "candidates"));
        File.WriteAllText(Path.Combine(typeDir, "candidates", "extra.md"), "---\ntitle: 'Extra'\n---\nYou do extra.");
        File.WriteAllText(Path.Combine(typeDir, "team-type.json"), """
            { "schemaVersion": 1, "id": "alpha", "version": "1.2.0", "displayName": "Alpha", "order": 5,
              "workspace": { "kind": "none", "label": "None", "required": false },
              "coverage": [ "check" ],
              "processes": [ { "id": "flow", "file": "processes/flow.md", "default": true } ],
              "roles": [
                { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "covers": [ "check" ] },
                { "id": "extra", "title": "Extra", "candidate": "candidates/extra.md" } ] }
            """);

        TeamTypeCatalog catalog = Create(dir);

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(catalog.Find("alpha"));
        Assert.True(catalog.IsUserDefined("alpha"));
        RoleSpec lead = type.Roles.Single(static role => role.Id == "lead");
        Assert.Equal("lead", lead.Job);
        Assert.Equal("Team Lead", lead.Title);
        Assert.Equal("Team Lead", catalog.ReadCandidate("alpha", "lead")?.Title);
        RoleSpec extra = type.Roles.Single(static role => role.Id == "extra");
        Assert.Null(extra.Job);
        Assert.Equal("Extra", catalog.ReadCandidate("alpha", "extra")?.Title);
        Assert.Empty(catalog.Problems);
    }

    /// <summary>
    /// Saving a custom role against a type backed by a pool writes only the type's own roles and the new one, never the pooled extras,
    /// and after a reload the type lists the new role and still offers the pool jobs as pooled roles.
    /// </summary>
    [Fact]
    public void Save_CustomRoleOnAPoolBackedType_WritesNoPooledRolesAndRoundTrips()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = Create(dir);
        UserCandidateWriter writer = new(dir.Options(), catalog);

        WriteResult result = writer.Save("alpha", "scout", "Scout", "A lead is needed", "You scout.");

        Assert.True(result.Success);
        string saved = File.ReadAllText(Path.Combine(dir.Path, "TeamTypes", "alpha", "team-type.json"));
        TeamTypeManifest written = Assert.IsType<TeamTypeManifest>(System.Text.Json.JsonSerializer.Deserialize<TeamTypeManifest>(saved, TeamTypeJson.Options));
        Assert.Equal(["lead", "maker", "scout"], written.Roles.Select(static role => role.Id));
        catalog.Reload();
        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(catalog.Find("alpha"));
        Assert.Empty(catalog.Problems);
        Assert.Equal("Scout", type.Roles.Single(static role => role.Id == "scout").Title);
        Assert.Contains(type.Roles, static role => role.Id == "tutor" && role.Pooled);
        Assert.Equal("Team Lead", type.Roles.Single(static role => role.Id == "lead").Title);
    }

    /// <summary>A manifest with inline roles and no pool job keeps working: its roles are not pool roles and its candidate file is read from the type folder.</summary>
    [Fact]
    public void Find_InlineRoleWithCandidateFile_StaysLegacy()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> poolFiles = Personas();
        poolFiles["hiringpool.json"] = PoolJson;
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, poolFiles);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["old/team-type.json"] = """
                { "schemaVersion": 1, "id": "old", "version": "1.0.0", "displayName": "Old", "order": 1,
                  "workspace": { "kind": "none", "label": "None" },
                  "processes": [ { "id": "flow", "file": "processes/flow.md", "default": true } ],
                  "roles": [ { "id": "boss", "title": "Boss", "candidate": "candidates/boss.md", "manager": true, "required": true } ] }
                """,
            ["old/candidates/boss.md"] = "---\ntitle: 'Boss'\n---\nYou boss.",
            ["old/processes/flow.md"] = "# Flow",
        };

        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins, pool);

        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(catalog.Find("old"));
        RoleSpec boss = type.Roles.Single(static role => role.Id == "boss");
        Assert.Null(boss.Job);
        Assert.Equal("Boss", boss.Title);
        Assert.Equal("Boss", catalog.ReadCandidate("old", "boss")?.Title);
        Assert.Contains(type.Roles, static role => role.Pooled);
    }
}
