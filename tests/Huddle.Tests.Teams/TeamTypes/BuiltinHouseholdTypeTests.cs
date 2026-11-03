using Agency.Huddle.App.Acp;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the built-in Household team type (TT-8, Scenario 5): its manifest and roles for a whole family's home, its one default process, its hiring-pool entries, the catalog order, and the rule that only the type folder names the type id.</summary>
public sealed class BuiltinHouseholdTypeTests
{
    private static readonly string[] ProcessRules =
    [
        "one owner", "one Task per occurrence", "`action:`", "never its number", "allergies", "emergency contacts", "bin day", "check-in",
    ];

    /// <summary>The optional roles that draft household work: one per Team, none a manager, each covering generate.</summary>
    public static TheoryData<string> GeneratorRoleIds =>
    [
        "school-liaison", "meal-planner", "home-keeper", "health-admin", "schedule-keeper", "finance-tracker", "comparison-shopper",
    ];

    /// <summary>The two jobs added to the hiring pool for the Household type.</summary>
    public static TheoryData<string> NewJobIds => ["meal-planner", "home-keeper"];

    /// <summary>Loads the shipped Household type through the catalog, so its roles are composed from their hiring-pool jobs.</summary>
    /// <returns>The type with the roles it lists itself.</returns>
    private static TeamTypeManifest LoadManifest() => BuiltinManifests.Load("household");

    /// <summary>Reads the shipped household-basics process text through the catalog.</summary>
    /// <returns>The process text.</returns>
    private static string ReadProcess()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);
        string? text = catalog.ReadProcess("household", "household-basics");
        Assert.NotNull(text);
        return text;
    }

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
        Assert.Equal("1.1.0", manifest.Version);
        Assert.Equal("Household", manifest.DisplayName);
        Assert.Equal(20, manifest.Order);
        Assert.Equal(WorkspaceKind.None, manifest.Workspace.Kind);
        Assert.Equal("Home folder", manifest.Workspace.Label);
        Assert.False(manifest.Workspace.Required);
        Assert.Equal(["generate", "verify"], manifest.Coverage);
    }

    /// <summary>The manifest keeps the two Scenario 5 roles first, then adds meals, upkeep, family admin and the Retro agent, in that order.</summary>
    [Fact]
    public void Manifest_Roles_CoverTheWholeHomeInOrder()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(
            [
                "household-manager", "school-liaison", "meal-planner", "home-keeper", "health-admin", "schedule-keeper", "finance-tracker",
                "comparison-shopper", "retro-agent",
            ],
            manifest.Roles.Select(static r => r.Id).ToList());
    }

    /// <summary>Every drafting role is an optional hire, one per Team, that covers generate and leaves verify to the manager.</summary>
    /// <param name="roleId">The role id.</param>
    [Theory]
    [MemberData(nameof(GeneratorRoleIds))]
    public void Manifest_GeneratorRole_IsOptionalOnePerTeamCoveringGenerate(string roleId)
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec role = Assert.Single(manifest.Roles, r => string.Equals(r.Id, roleId, StringComparison.Ordinal));
        Assert.False(role.Manager);
        Assert.False(role.Required);
        Assert.Equal(1, role.MaxPerTeam);
        Assert.Equal(["generate"], role.Covers);
    }

    /// <summary>The Meal Planner and the Home Keeper carry their titles and need no connector, so their cards are ready on Build a team.</summary>
    [Fact]
    public void Manifest_MealPlannerAndHomeKeeper_HaveTitlesAndNeedNoConnector()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec meals = Assert.Single(manifest.Roles, static r => string.Equals(r.Id, "meal-planner", StringComparison.Ordinal));
        RoleSpec upkeep = Assert.Single(manifest.Roles, static r => string.Equals(r.Id, "home-keeper", StringComparison.Ordinal));
        Assert.Equal("Meal Planner", meals.Title);
        Assert.Equal("Home Keeper", upkeep.Title);
        Assert.Empty(meals.Requires);
        Assert.Empty(upkeep.Requires);
    }

    /// <summary>The Retro agent is the type's retro role, an optional hire on a different vendor from the Household Manager whose work it reviews.</summary>
    [Fact]
    public void Manifest_RetroAgent_IsOptionalRetroRoleOnAnotherVendor()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec retro = Assert.Single(manifest.Roles, static r => string.Equals(r.Id, "retro-agent", StringComparison.Ordinal));
        Assert.Equal("retro-agent", manifest.RetroRole);
        Assert.False(retro.Required);
        Assert.Equal(1, retro.MaxPerTeam);
        Assert.Equal(["household-manager"], retro.Model?.DifferentVendorFrom ?? []);
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
        // The manager also holds the three Second Brain Skills (Task 105), so pick team-management out of the list.
        SkillRef skill = Assert.Single(manager.Skills, static s => s.Name == "team-management");
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

    /// <summary>The household-basics process parses, carries the PR-6 outline with no missing section, names the Household template, declares no gate, wakes the manager weekly for the check-in, and runs its retrospective every 28 days.</summary>
    [Fact]
    public void Process_HouseholdBasics_ParsesWithWeeklyCheckInAndMonthlyRetro()
    {
        string text = ReadProcess();

        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.NotNull(document);
        Assert.Empty(document.MissingSections);
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        ProcessSettings settings = file.Process;
        Assert.Equal("household-basics@1.1.0", settings.Template);
        Assert.Empty(settings.Gates);
        Assert.Empty(settings.UnknownGates);
        Assert.Equal(new DigestCadence(7, CadenceUnit.Days), settings.DigestSchedule());
        Assert.Equal(new RetroCadence(28, CadenceUnit.Days), settings.RetroSchedule());
    }

    /// <summary>The process says who does what for every role the type declares, so no hire arrives without a duty.</summary>
    [Fact]
    public void Process_HouseholdBasics_NamesEveryRole()
    {
        string text = ReadProcess();

        Assert.Equal(string.Empty, string.Join(", ", LoadManifest().Roles.Select(static r => "`" + r.Id + "`").Where(id => !text.Contains(id, StringComparison.Ordinal))));
    }

    /// <summary>The process states the family rules: one owner per job, one Task per occurrence, the Action line on a spend, the privacy rule, and the onboarding notes a family needs.</summary>
    [Fact]
    public void Process_HouseholdBasics_StatesTheFamilyRules()
    {
        string text = ReadProcess();

        Assert.Equal(string.Empty, string.Join(", ", ProcessRules.Where(rule => !text.Contains(rule, StringComparison.Ordinal))));
    }

    /// <summary>The pool describes the Household type as a family's whole home, not school mail alone, and tells it apart from Life Admin.</summary>
    [Fact]
    public void Pool_HouseholdDescription_CoversTheHomeAndDiffersFromLifeAdmin()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance);

        Assert.Equal(
            "Runs a family's home together: school mail, meals, upkeep, appointments, bills and renewals, each with one owner and a weekly check-in. It drafts and tracks; a parent pays, signs and sends.",
            pool.FindTeamType("household")?.Description);
        Assert.Equal(
            "Keeps one person's own admin moving: trips, bills and spending, appointments and forms, and the inbox they arrive in. It drafts and tracks; you book, pay and send.",
            pool.FindTeamType("life-admin")?.Description);
    }

    /// <summary>The Household Manager reads as the family's manager and the Chief of Household as one person's, so Build a team offers two clear choices.</summary>
    [Fact]
    public void Pool_HouseholdManagerAndChiefOfHousehold_ReadAsDifferentJobs()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance);

        Assert.Equal(
            "Runs a family's shared home: gives each chore and school Task one owner, checks every draft against its source and leads the weekly check-in.",
            pool.FindJob("household-manager")?.Description);
        Assert.Equal(
            "Runs one person's own admin: trips, bills, appointments and the inbox. Sets the order of work and checks every draft before it reaches you.",
            pool.FindJob("chief-of-household")?.Description);
    }

    /// <summary>Each new job's persona parses, says when to consult it, and has a substantial stance.</summary>
    /// <param name="jobId">The job id.</param>
    [Theory]
    [MemberData(nameof(NewJobIds))]
    public void Pool_NewJobPersona_ParsesWithConsultWhenAndStance(string jobId)
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance);

        CandidateTemplate? template = pool.ReadPersona(jobId);
        Assert.NotNull(template);
        PersonaFrontmatterField consultWhen = Assert.Single(template.JobDescriptionFields, static f => f.Key == "consult_when");
        Assert.False(string.IsNullOrWhiteSpace(consultWhen.Value));
        Assert.True(template.Stance.Length >= 400, $"The {jobId} stance has {template.Stance.Length} characters.");
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
