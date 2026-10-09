using System.Reflection;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the built-in Development team type manifest (TT-7, TT-3, PR-1): it parses, validates, has one manager, and its presets and suggested names are sound.</summary>
public sealed class BuiltinDevelopmentTypeTests
{
    private const string TypeFolder = "TeamTypes/Builtin/development/";

    private static readonly string[] FullPresetRoles =
    [
        "development-manager",
        "tech-lead",
        "developer",
        "reviewer",
        "security-engineer",
        "qa-engineer",
        "release-engineer",
    ];

    private static readonly string[] ExpectedRoles =
    [
        "development-manager",
        "tech-lead",
        "developer",
        "reviewer",
        "security-engineer",
        "qa-engineer",
        "release-engineer",
        "retro-agent",
    ];

    /// <summary>Reads the embedded manifest and parses it with the one set of team-type JSON options.</summary>
    /// <returns>The parsed manifest.</returns>
    private static TeamTypeManifest LoadManifest()
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(static n => n.Replace('\\', '/') == TypeFolder + "team-type.json");
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        TeamTypeManifest? manifest = System.Text.Json.JsonSerializer.Deserialize<TeamTypeManifest>(reader.ReadToEnd(), TeamTypeJson.Options);
        Assert.NotNull(manifest);
        return manifest;
    }

    /// <summary>Lists the embedded resource names of the Development type, relative to its folder, so validation sees the files that ship.</summary>
    /// <returns>The relative paths, such as <c>candidates/developer.md</c>.</returns>
    private static HashSet<string> EmbeddedTypeFiles()
    {
        HashSet<string> files = new(StringComparer.Ordinal);
        foreach (string name in typeof(TeamTypeCatalog).Assembly.GetManifestResourceNames())
        {
            string normalised = name.Replace('\\', '/');
            if (normalised.StartsWith(TypeFolder, StringComparison.Ordinal))
            {
                files.Add(normalised[TypeFolder.Length..]);
            }
        }

        return files;
    }

    /// <summary>The manifest's identity, workspace and order match the plan: Development Team, repository workspace, dropdown position 10.</summary>
    [Fact]
    public void Manifest_Identity_MatchesPlan()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("development", manifest.Id);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("Development Team", manifest.DisplayName);
        Assert.Equal(10, manifest.Order);
        Assert.Equal(WorkspaceKind.Repo, manifest.Workspace.Kind);
        Assert.True(manifest.Workspace.Required);
        Assert.Equal(["generate", "challenge", "verify"], manifest.Coverage);
    }

    /// <summary>The manifest declares the seven TT-7 roles in the plan's order, then the optional Retro agent (RT-1).</summary>
    [Fact]
    public void Manifest_Roles_AreTheSevenTT7RolesThenRetroAgent()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(ExpectedRoles, manifest.Roles.Select(static r => r.Id).ToList());
    }

    /// <summary>The manager role holds the built-in team-management skill (Task 62), so it is offered propose_sprint.</summary>
    [Fact]
    public void Manifest_Manager_HoldsTheBuiltinTeamManagementSkill()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec manager = Assert.Single(manifest.Roles, static r => r.Manager);

        SkillRef skill = Assert.Single(manager.Skills);
        Assert.Equal("team-management", skill.Name);
        Assert.Equal("builtin", skill.Source);
    }

    /// <summary>The QA role covers verify and holds the built-in team-management skill, so it is offered confirm_request.</summary>
    [Fact]
    public void Manifest_QaEngineer_HoldsTheBuiltinTeamManagementSkill()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec qa = Assert.Single(manifest.Roles, static r => r.Id == "qa-engineer");

        Assert.Equal(["verify"], qa.Covers);
        SkillRef skill = Assert.Single(qa.Skills);
        Assert.Equal(new SkillRef("team-management", "builtin", "team-management"), skill);
    }

    /// <summary>Exactly one role is the manager, and it is the Development Manager.</summary>
    [Fact]
    public void Manifest_Managers_ExactlyOneDevelopmentManager()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec manager = Assert.Single(manifest.Roles, static r => r.Manager);
        Assert.Equal("development-manager", manager.Id);
        Assert.Equal(ModelTier.Strongest, manager.Model.Tier);
    }

    /// <summary>The validator reports no problems over the shipped files: the candidates and the four process templates are all present.</summary>
    [Fact]
    public void Validate_ShippedFiles_ReportsNoProblems()
    {
        TeamTypeManifest manifest = LoadManifest();
        HashSet<string> files = EmbeddedTypeFiles();

        Assert.Empty(TeamTypeValidator.Validate(manifest, files.Contains));
    }

    /// <summary>The default process is lightweight-flow, and the four processes are declared in the plan's order.</summary>
    [Fact]
    public void Manifest_Processes_LightweightFlowIsDefault()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(["lightweight-flow", "spec-driven", "sprints", "maintenance"], manifest.Processes.Select(static p => p.Id).ToList());
        ProcessRef defaultProcess = Assert.Single(manifest.Processes, static p => p.Default);
        Assert.Equal("lightweight-flow", defaultProcess.Id);
    }

    /// <summary>Both presets name only real roles: Small has four roles and Full has all seven.</summary>
    [Fact]
    public void Manifest_Presets_NameRealRoles()
    {
        TeamTypeManifest manifest = LoadManifest();

        PresetSpec small = Assert.Single(manifest.Presets, static p => p.Id == "small");
        PresetSpec full = Assert.Single(manifest.Presets, static p => p.Id == "full");
        Assert.Equal("Small", small.Label);
        Assert.Equal(["development-manager", "developer", "reviewer", "qa-engineer"], small.Roles);
        Assert.Equal("Full", full.Label);
        Assert.Equal(FullPresetRoles, full.Roles);
        Assert.All(manifest.Presets.SelectMany(static p => p.Roles), role => Assert.Contains(role, ExpectedRoles));
    }

    /// <summary>Every role has at least four suggested names, and no name is used by more than one role.</summary>
    [Fact]
    public void Manifest_SuggestedNames_AreUniqueAcrossRoles()
    {
        TeamTypeManifest manifest = LoadManifest();

        List<string> names = manifest.Roles.SelectMany(static r => r.SuggestedNames).ToList();

        Assert.All(manifest.Roles, static role => Assert.True(role.SuggestedNames.Count >= 4, $"Role '{role.Id}' has fewer than four suggested names."));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Developer, reviewer and tech lead carry the Addy Osmani skills that Start Team imports (HR-9).</summary>
    [Fact]
    public void Manifest_ImportedSkills_OnDeveloperReviewerAndTechLead()
    {
        TeamTypeManifest manifest = LoadManifest();

        foreach (string roleId in new[] { "developer", "reviewer", "tech-lead" })
        {
            RoleSpec role = manifest.Roles.Single(r => string.Equals(r.Id, roleId, StringComparison.Ordinal));
            Assert.NotEmpty(role.Skills);
            Assert.All(role.Skills, static s => Assert.Equal(new SkillRef(s.Name, "addyosmani/agent-skills", $"skills/{s.Name}"), s));
        }
    }
}
