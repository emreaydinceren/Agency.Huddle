using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the built-in Household team type (TT-8, Scenario 5): its manifest and two roles, its one default process, the catalog order, and the rule that only the type folder names the type id.</summary>
public sealed class BuiltinHouseholdTypeTests
{
    /// <summary>Loads the shipped Household type through the catalog, so its roles are composed from their hiring-pool jobs.</summary>
    /// <returns>The type with the roles it lists itself.</returns>
    private static TeamTypeManifest LoadManifest() => BuiltinManifests.Load("household");

    /// <summary>Walks up from the test binary until the directory holding <c>Huddle.slnx</c> is found.</summary>
    /// <returns>The repository root directory.</returns>
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>The manifest's identity, workspace, coverage and order match the plan: Household, no workspace, dropdown position 20.</summary>
    [Fact]
    public void Manifest_Identity_MatchesPlan()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("household", manifest.Id);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("Household", manifest.DisplayName);
        Assert.Equal(20, manifest.Order);
        Assert.Equal(WorkspaceKind.None, manifest.Workspace.Kind);
        Assert.Equal("Home folder", manifest.Workspace.Label);
        Assert.False(manifest.Workspace.Required);
        Assert.Equal(["generate", "verify"], manifest.Coverage);
    }

    /// <summary>The manifest declares exactly the two Scenario 5 roles, the manager first.</summary>
    [Fact]
    public void Manifest_Roles_AreHouseholdManagerThenSchoolLiaison()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(["household-manager", "school-liaison"], manifest.Roles.Select(static r => r.Id).ToList());
    }

    /// <summary>The Household Manager is the one required manager: strongest model, covers verify, holds the built-in team-management skill, and has three suggested names.</summary>
    [Fact]
    public void Manifest_HouseholdManager_IsRequiredStrongestManagerCoveringVerify()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec manager = Assert.Single(manifest.Roles, static r => r.Manager);
        Assert.Equal("household-manager", manager.Id);
        Assert.True(manager.Required);
        Assert.Equal(1, manager.MaxPerTeam);
        Assert.Equal(ModelTier.Strongest, manager.Model.Tier);
        Assert.Equal(["verify"], manager.Covers);
        SkillRef skill = Assert.Single(manager.Skills);
        Assert.Equal(new SkillRef("team-management", "builtin", "team-management"), skill);
        Assert.Equal(["Nora", "Ben", "Maya"], manager.SuggestedNames);
    }

    /// <summary>The School Liaison is optional, at most one per Team, balanced, covers generate, needs the email read connector, and has two suggested names.</summary>
    [Fact]
    public void Manifest_SchoolLiaison_IsOptionalBalancedGeneratorNeedingEmailRead()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec liaison = Assert.Single(manifest.Roles, static r => string.Equals(r.Id, "school-liaison", StringComparison.Ordinal));
        Assert.False(liaison.Manager);
        Assert.False(liaison.Required);
        Assert.Equal(1, liaison.MaxPerTeam);
        Assert.Equal(ModelTier.Balanced, liaison.Model.Tier);
        Assert.Equal(["generate"], liaison.Covers);
        Assert.Equal(["email:read"], liaison.Requires);
        Assert.Equal(["Ellie", "Theo"], liaison.SuggestedNames);
    }

    /// <summary>The one process is household-basics and it is the default.</summary>
    [Fact]
    public void Manifest_Processes_HouseholdBasicsIsTheOneDefault()
    {
        TeamTypeManifest manifest = LoadManifest();

        ProcessRef process = Assert.Single(manifest.Processes);
        Assert.Equal("household-basics", process.Id);
        Assert.True(process.Default);
    }

    /// <summary>The catalog loads the shipped type with no problems: both roles name pool jobs that hold a persona, and the process is present.</summary>
    [Fact]
    public void Validate_ShippedFiles_ReportsNoProblems()
    {
        Assert.DoesNotContain(BuiltinManifests.Problems(), static problem => string.Equals(problem.TypeId, "household", StringComparison.Ordinal));
    }

    /// <summary>The catalog lists every built-in type with no problems, Development first (order 10) and Household second (order 20).</summary>
    [Fact]
    public void Catalog_Types_DevelopmentThenHouseholdThenTheRest()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);

        Assert.Equal(["development", "household"], catalog.Types.Take(2).Select(static t => t.Id).ToList());
        Assert.Empty(catalog.Problems);
    }

    /// <summary>The household-basics process parses, carries the PR-6 outline with no missing section, names the Household template, declares no gate, and runs its retrospective every 14 days.</summary>
    [Fact]
    public void Process_HouseholdBasics_ParsesWithOutlineAndFourteenDayRetro()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);

        string? text = catalog.ReadProcess("household", "household-basics");
        Assert.NotNull(text);
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.NotNull(document);
        Assert.Equal(new VersionedRef("household-basics", "1.0.0"), document.Template);
        Assert.Empty(document.MissingSections);
        Assert.Empty(document.UnknownGates);
        Assert.Empty(document.Gates);
        Assert.Equal(new RetroCadence(14, CadenceUnit.Days), document.Retro);
    }

    /// <summary>No source file under src/Huddle.App outside TeamTypes/Builtin names the type id <c>household</c>, so the type stays data only (Task 81).</summary>
    [Fact]
    public void Source_OutsideBuiltinTypes_NeverNamesTheHouseholdTypeId()
    {
        string root = FindRepositoryRoot();
        string sourceRoot = Path.Combine(root, "src", "Huddle.App");
        string builtinRoot = Path.Combine(sourceRoot, "TeamTypes", "Builtin");
        List<string> offenders = [];
        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs", StringComparison.Ordinal) && !file.EndsWith(".razor", StringComparison.Ordinal))
            {
                continue;
            }

            if (file.StartsWith(builtinRoot, StringComparison.Ordinal))
            {
                continue;
            }

            if (File.ReadAllText(file).Contains("household", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(root, file));
            }
        }

        Assert.Empty(offenders);
    }
}
