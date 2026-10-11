using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Exercises <see cref="TeamTypeValidator"/>: one valid manifest gives no problems and each rule gives its code (TT-4, TT-5).</summary>
public sealed class TeamTypeValidatorTests
{
    /// <summary>A well-formed manifest whose files all exist yields no problems.</summary>
    [Fact]
    public void Validate_ValidManifest_ReturnsNoProblems()
    {
        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(Valid(), AllFiles);

        Assert.Empty(problems);
    }

    /// <summary>A schema version other than 1 is reported.</summary>
    [Fact]
    public void Validate_SchemaVersionTwo_ReportsSchema()
    {
        Assert.Contains("schema", Codes(Valid() with { SchemaVersion = 2 }));
    }

    /// <summary>Ids that are empty, upper-case, start with a hyphen or hold spaces are reported.</summary>
    /// <param name="id">The invalid id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Dev")]
    [InlineData("-dev")]
    [InlineData("de v")]
    public void Validate_BadId_ReportsId(string id)
    {
        Assert.Contains("id", Codes(Valid() with { Id = id }));
    }

    /// <summary>A version that is not major.minor.patch is reported.</summary>
    /// <param name="version">The invalid version.</param>
    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.x")]
    public void Validate_BadVersion_ReportsVersion(string version)
    {
        Assert.Contains("version", Codes(Valid() with { Version = version }));
    }

    /// <summary>No manager role is reported as no-manager.</summary>
    [Fact]
    public void Validate_NoManager_ReportsNoManager()
    {
        TeamTypeManifest manifest = Valid() with { Roles = [Role("dev", covers: ["generate", "challenge", "verify"])] };

        Assert.Contains("no-manager", Codes(manifest));
    }

    /// <summary>Two manager roles are reported as many-managers.</summary>
    [Fact]
    public void Validate_TwoManagers_ReportsManyManagers()
    {
        TeamTypeManifest manifest = Valid() with
        {
            Roles = [Role("a", manager: true, covers: ["generate", "challenge", "verify"]), Role("b", manager: true)],
        };

        Assert.Contains("many-managers", Codes(manifest));
    }

    /// <summary>A repeated role id is reported with the id in the message.</summary>
    [Fact]
    public void Validate_DuplicateRoleId_ReportsDuplicateRole()
    {
        TeamTypeManifest manifest = Valid() with
        {
            Roles = [Lead(), Role("dev"), Role("dev")],
        };

        TeamTypeProblem problem = Assert.Single(Validate(manifest), static p => p.Code == "duplicate-role");
        Assert.Contains("dev", problem.Message);
    }

    /// <summary>A role whose candidate file is missing is reported, naming the role and the file.</summary>
    [Fact]
    public void Validate_MissingCandidateFile_ReportsCandidateMissing()
    {
        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(Valid(), static path => path != "candidates/dev.md");

        TeamTypeProblem problem = Assert.Single(problems);
        Assert.Equal("candidate-missing", problem.Code);
        Assert.Contains("dev", problem.Message);
        Assert.Contains("candidates/dev.md", problem.Message);
    }

    /// <summary>A coverage step that no role covers is reported with the step in the message.</summary>
    [Fact]
    public void Validate_UncoveredStep_ReportsCoverageUncovered()
    {
        TeamTypeManifest manifest = Valid() with { Coverage = ["generate", "challenge", "verify", "ship"] };

        TeamTypeProblem problem = Assert.Single(Validate(manifest));
        Assert.Equal("coverage-uncovered", problem.Code);
        Assert.Contains("ship", problem.Message);
    }

    /// <summary>A role that names a hiring-pool job the pool does not hold is reported with the job id, and its candidate file is not asked for.</summary>
    [Fact]
    public void Validate_RoleNamesUnknownJob_ReportsJobUnknown()
    {
        RoleSpec ghost = new("ghost", "Ghost", "personas/ghost.md", Job: "ghost");
        TeamTypeManifest manifest = Valid() with { Roles = [Lead(), ghost] };

        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(manifest, static _ => false, static job => job != "ghost");

        Assert.Equal(
            "The role 'ghost' names the hiring-pool job 'ghost', which the pool does not hold.",
            Assert.Single(problems, static problem => problem.Code == "job-unknown").Message);
        Assert.DoesNotContain(problems, static problem => problem.Code == "candidate-missing" && problem.Message.Contains("'ghost'", StringComparison.Ordinal));
    }

    /// <summary>A role that names a job the pool holds is valid even though the type folder has no candidate file for it.</summary>
    [Fact]
    public void Validate_RoleNamesKnownJob_NeedsNoCandidateFile()
    {
        RoleSpec dev = new("dev", "Dev", "personas/dev.md", Job: "dev");
        TeamTypeManifest manifest = Valid() with { Roles = [Lead(), dev] };

        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(manifest, static path => !path.StartsWith("candidates/", StringComparison.Ordinal), static _ => true);

        Assert.DoesNotContain(problems, static problem => problem.Code is "job-unknown" or "candidate-missing" && problem.Message.Contains("'dev'", StringComparison.Ordinal));
    }

    /// <summary>Processes with no default are reported.</summary>
    [Fact]
    public void Validate_NoDefaultProcess_ReportsProcessDefault()
    {
        TeamTypeManifest manifest = Valid() with { Processes = [new ProcessRef("p", "processes/p.md")] };

        Assert.Contains("process-default", Codes(manifest));
    }

    /// <summary>Two default processes are reported.</summary>
    [Fact]
    public void Validate_TwoDefaultProcesses_ReportsProcessDefault()
    {
        TeamTypeManifest manifest = Valid() with
        {
            Processes = [new ProcessRef("p", "processes/p.md", true), new ProcessRef("q", "processes/q.md", true)],
        };

        Assert.Contains("process-default", Codes(manifest));
    }

    /// <summary>A process file that does not exist is reported, naming the file.</summary>
    [Fact]
    public void Validate_MissingProcessFile_ReportsProcessMissing()
    {
        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(Valid(), static path => path != "processes/default.md");

        TeamTypeProblem problem = Assert.Single(problems);
        Assert.Equal("process-missing", problem.Code);
        Assert.Contains("default.md", problem.Message);
    }

    /// <summary>A differentVendorFrom entry that is not a role id is reported.</summary>
    [Fact]
    public void Validate_VendorUnknownRole_ReportsVendorUnknownRole()
    {
        RoleSpec dev = Role("dev") with { Model = new ModelSpec(ModelTier.Fast, ["ghost"]) };
        TeamTypeManifest manifest = Valid() with { Roles = [Lead(), dev] };

        TeamTypeProblem problem = Assert.Single(Validate(manifest));
        Assert.Equal("vendor-unknown-role", problem.Code);
        Assert.Contains("ghost", problem.Message);
    }

    /// <summary>A required role with a negative limit is reported.</summary>
    [Fact]
    public void Validate_RequiredWithNegativeLimit_ReportsLimit()
    {
        RoleSpec dev = Role("dev") with { Required = true, MaxPerTeam = -1 };
        TeamTypeManifest manifest = Valid() with { Roles = [Lead(), dev] };

        Assert.Contains("limit", Codes(manifest));
    }

    /// <summary>A required role with MaxPerTeam of zero (unlimited) is valid.</summary>
    [Fact]
    public void Validate_RequiredWithZeroLimit_IsValid()
    {
        RoleSpec dev = Role("dev") with { Required = true, MaxPerTeam = 0 };
        TeamTypeManifest manifest = Valid() with { Roles = [Lead(), dev] };

        Assert.Empty(Validate(manifest));
    }

    /// <summary>A retro role that is not a role id is reported.</summary>
    [Fact]
    public void Validate_RetroRoleUnknown_ReportsRetroRoleUnknown()
    {
        Assert.Contains("retro-role-unknown", Codes(Valid() with { RetroRole = "ghost" }));
    }

    /// <summary>A retro role that is a role id, or null, is valid.</summary>
    [Fact]
    public void Validate_RetroRoleKnownOrNull_IsValid()
    {
        Assert.Empty(Validate(Valid() with { RetroRole = "lead" }));
        Assert.Empty(Validate(Valid() with { RetroRole = null }));
    }

    /// <summary>Several faults in one manifest are all reported, not just the first.</summary>
    [Fact]
    public void Validate_SeveralFaults_ReportsAll()
    {
        List<string> codes = Codes(Valid() with { SchemaVersion = 3, Version = "x", RetroRole = "ghost" });

        Assert.Contains("schema", codes);
        Assert.Contains("version", codes);
        Assert.Contains("retro-role-unknown", codes);
    }

    /// <summary>Every rule states its reason in the exact words the Problems list shows.</summary>
    [Fact]
    public void Validate_EachRule_MessageText()
    {
        Assert.Equal("Unsupported schemaVersion 2; only 1 is understood.", MessageOf(Valid() with { SchemaVersion = 2 }, "schema"));
        Assert.Equal("The id 'Dev' must be lower-case letters, digits and hyphens, and start with a letter or digit.", MessageOf(Valid() with { Id = "Dev" }, "id"));
        Assert.Equal("The version '1.0' must be major.minor.patch.", MessageOf(Valid() with { Version = "1.0" }, "version"));
        Assert.Equal("The retroRole 'ghost' is not a role of this type.", MessageOf(Valid() with { RetroRole = "ghost" }, "retro-role-unknown"));
        Assert.Equal(
            "The type must declare exactly one manager role; none does.",
            MessageOf(Valid() with { Roles = [Role("dev", covers: ["generate", "challenge", "verify"])] }, "no-manager"));
        Assert.Equal(
            "The type must declare exactly one manager role; found 2 (a, b).",
            MessageOf(Valid() with { Roles = [Role("a", manager: true, covers: ["generate", "challenge", "verify"]), Role("b", manager: true)] }, "many-managers"));
        Assert.Equal("The role id 'dev' is used more than once.", MessageOf(Valid() with { Roles = [Lead(), Role("dev"), Role("dev")] }, "duplicate-role"));
        Assert.Equal(
            "The role 'dev' has no candidate file at 'candidates/dev.md'.",
            Assert.Single(TeamTypeValidator.Validate(Valid(), static path => path != "candidates/dev.md")).Message);
        Assert.Equal(
            "The role 'dev' is required but its maxPerTeam is -1.",
            MessageOf(Valid() with { Roles = [Lead(), Role("dev") with { Required = true, MaxPerTeam = -1 }] }, "limit"));
        Assert.Equal(
            "The role 'dev' asks for a different vendor from 'ghost', which is not a role of this type.",
            MessageOf(Valid() with { Roles = [Lead(), Role("dev") with { Model = new ModelSpec(ModelTier.Fast, ["ghost"]) }] }, "vendor-unknown-role"));
        Assert.Equal("No role covers the coverage step 'ship'.", MessageOf(Valid() with { Coverage = ["ship"] }, "coverage-uncovered"));
        Assert.Equal(
            "Exactly one process must be the default; 0 are.",
            MessageOf(Valid() with { Processes = [new ProcessRef("p", "processes/p.md")] }, "process-default"));
        Assert.Equal(
            "The process 'default' has no file at 'processes/default.md'.",
            Assert.Single(TeamTypeValidator.Validate(Valid(), static path => path != "processes/default.md")).Message);
    }

    /// <summary>A process file named without the processes folder is looked up under it.</summary>
    [Fact]
    public void Validate_ProcessFileWithoutFolder_LooksUnderProcesses()
    {
        TeamTypeManifest manifest = Valid() with { Processes = [new ProcessRef("default", "default.md", true)] };
        List<string> asked = [];

        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(manifest, path =>
        {
            asked.Add(path);
            return true;
        });

        Assert.Empty(problems);
        Assert.Contains("processes/default.md", asked);
    }

    private static string MessageOf(TeamTypeManifest manifest, string code) =>
        Assert.Single(Validate(manifest), p => p.Code == code).Message;

    private static bool AllFiles(string path) => true;

    private static IReadOnlyList<TeamTypeProblem> Validate(TeamTypeManifest manifest) => TeamTypeValidator.Validate(manifest, AllFiles);

    private static List<string> Codes(TeamTypeManifest manifest) => [.. Validate(manifest).Select(static p => p.Code)];

    private static RoleSpec Role(string id, bool manager = false, IReadOnlyList<string>? covers = null) =>
        new(id, id, $"candidates/{id}.md", Manager: manager, Covers: covers);

    private static RoleSpec Lead() => Role("lead", manager: true, covers: ["generate", "challenge", "verify"]);

    private static TeamTypeManifest Valid() => new(
        1,
        "development",
        "1.0.0",
        "Development Team",
        1,
        new WorkspaceSpec(WorkspaceKind.Repo, "Repository", true),
        Coverage: ["generate", "challenge", "verify"],
        Processes: [new ProcessRef("default", "processes/default.md", true)],
        Roles: [Lead(), Role("dev")],
        RetroRole: "lead");
}
