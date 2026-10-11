using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="HiringPoolCatalog"/>: loading the built-in pool, rejecting bad jobs, merging a user pool by id, and reading a persona.</summary>
public sealed class HiringPoolCatalogTests
{
    private const string Persona = """
        ---
        title: 'Bookkeeper'
        consult_when: 'A receipt arrives'
        ---
        You keep the books.
        """;

    private const string Pool = """
        { "schemaVersion": 1,
          "teamTypes": [ { "id": "alpha", "name": "Alpha Team", "description": "Does alpha work." } ],
          "jobs": [
            { "id": "bookkeeper", "title": "Bookkeeper", "description": "Keeps the books.", "tags": [ "accounting", "finance" ],
              "skills": [ "budget-modeling" ], "teamTypes": [ "alpha" ], "model": { "tier": "fast" }, "requires": [ "email:read" ],
              "suggestedNames": [ "Ann" ] },
            { "id": "ghost", "title": "Ghost", "persona": "personas/missing.md" }
          ] }
        """;

    private static Dictionary<string, string> Builtins() => new(StringComparer.Ordinal)
    {
        ["hiringpool.json"] = Pool,
        ["personas/bookkeeper.md"] = Persona,
    };

    private static HiringPoolCatalog Create(TempDataDir dir, Dictionary<string, string>? builtins = null) =>
        new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, builtins ?? Builtins());

    private static void WriteUser(TempDataDir dir, string relativePath, string text)
    {
        string path = Path.Combine(dir.Path, "HiringPool", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? dir.Path);
        File.WriteAllText(path, text);
    }

    /// <summary>A sound job loads with its tags, skills, model tier, connectors and names, and a persona path that defaults from its id.</summary>
    [Fact]
    public void Jobs_ValidJob_LoadsWithItsFieldsAndDefaultPersonaPath()
    {
        using TempDataDir dir = new();

        HiringPoolCatalog catalog = Create(dir);

        PoolJob job = Assert.IsType<PoolJob>(catalog.FindJob("bookkeeper"));
        Assert.Equal("Bookkeeper", job.Title);
        Assert.Equal(["accounting", "finance"], job.Tags);
        Assert.Equal(["budget-modeling"], job.Skills);
        Assert.Equal(["alpha"], job.TeamTypes);
        Assert.Equal(ModelTier.Fast, job.Model.Tier);
        Assert.Equal(["email:read"], job.Requires);
        Assert.Equal(["Ann"], job.SuggestedNames);
        Assert.Equal("personas/bookkeeper.md", job.Persona);
    }

    /// <summary>A job whose persona file is missing is left out and named in the problems, and the other jobs still load.</summary>
    [Fact]
    public void Jobs_MissingPersona_ExcludesTheJobAndReportsIt()
    {
        using TempDataDir dir = new();

        HiringPoolCatalog catalog = Create(dir);

        Assert.Null(catalog.FindJob("ghost"));
        Assert.Equal(["bookkeeper"], catalog.Jobs.Select(static job => job.Id));
        HiringPoolProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("ghost", problem.Id);
        Assert.Equal("The job has no persona file at 'personas/missing.md'.", problem.Message);
    }

    /// <summary>A job id with capitals or spaces is rejected with the rule, so it never becomes a role id.</summary>
    [Fact]
    public void Jobs_InvalidId_IsRejectedWithTheRule()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "Bad Id", "title": "Bad" } ] }""";

        HiringPoolCatalog catalog = Create(dir, builtins);

        Assert.Empty(catalog.Jobs);
        Assert.Equal("The job id must be lower-case letters, digits and hyphens, and start with a letter or digit.", Assert.Single(catalog.Problems).Message);
    }

    /// <summary>A job with a blank title is rejected with the reason.</summary>
    [Fact]
    public void Jobs_BlankTitle_IsRejectedWithTheReason()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "nameless", "title": "  " } ] }""";

        HiringPoolCatalog catalog = Create(dir, builtins);

        Assert.Empty(catalog.Jobs);
        Assert.Equal("The job has no title.", Assert.Single(catalog.Problems).Message);
    }

    /// <summary>A persona with no stance after its frontmatter is rejected, and the parser's own words are in the problem.</summary>
    [Fact]
    public void Jobs_PersonaWithoutStance_IsRejectedWithTheParserReason()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["personas/bookkeeper.md"] = "---\ntitle: 'Bookkeeper'\n---\n";

        HiringPoolCatalog catalog = Create(dir, builtins);

        Assert.Null(catalog.FindJob("bookkeeper"));
        Assert.Contains(catalog.Problems, static problem => problem.Id == "bookkeeper"
            && problem.Message == "The persona does not parse: The candidate has no stance: the text after the frontmatter is empty.");
    }

    /// <summary>A user pool file that cannot be read is reported and the shipped pool still loads.</summary>
    [Fact]
    public void Jobs_UserPoolFileLocked_ReportsItAndKeepsTheBuiltInPool()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "hiringpool.json", """{ "schemaVersion": 1, "jobs": [] }""");
        string path = Path.Combine(dir.Path, "HiringPool", "hiringpool.json");
        using FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.None);

        HiringPoolCatalog catalog = Create(dir);

        Assert.NotNull(catalog.FindJob("bookkeeper"));
        HiringPoolProblem problem = Assert.Single(catalog.Problems, static candidate => candidate.Message == "The user pool file could not be read.");
        Assert.Equal(path, problem.Source);
    }

    /// <summary>A persona path that climbs out of the pool folder is never resolved, so the job is rejected.</summary>
    [Fact]
    public void Jobs_PersonaPathWithParentSegment_IsRejected()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["hiringpool.json"] = """
            { "schemaVersion": 1, "jobs": [ { "id": "escape", "title": "Escape", "persona": "../secret.md" } ] }
            """;
        builtins["../secret.md"] = Persona;

        HiringPoolCatalog catalog = Create(dir, builtins);

        Assert.Empty(catalog.Jobs);
        Assert.Equal("The job has no persona file at '../secret.md'.", Assert.Single(catalog.Problems, static problem => problem.Id == "escape").Message);
    }

    /// <summary>A pool file that is not JSON loads nothing and reports one problem instead of throwing.</summary>
    [Fact]
    public void Jobs_PoolFileIsNotJson_LoadsNothingAndReportsAProblem()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["hiringpool.json"] = "{ not json";

        HiringPoolCatalog catalog = Create(dir, builtins);

        Assert.Empty(catalog.Jobs);
        Assert.Equal("The file is not valid hiring pool JSON.", Assert.Single(catalog.Problems).Message);
    }

    /// <summary>A pool file with another schema version is refused.</summary>
    [Fact]
    public void Jobs_UnsupportedSchemaVersion_IsRefused()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["hiringpool.json"] = """{ "schemaVersion": 2, "jobs": [] }""";

        HiringPoolCatalog catalog = Create(dir, builtins);

        Assert.Empty(catalog.Jobs);
        Assert.Equal("The hiring pool file needs schemaVersion 1.", Assert.Single(catalog.Problems).Message);
    }

    /// <summary>The pool names a team type with its display name and description.</summary>
    [Fact]
    public void FindTeamType_KnownId_ReturnsNameAndDescription()
    {
        using TempDataDir dir = new();

        HiringPoolCatalog catalog = Create(dir);

        PoolTeamType type = Assert.IsType<PoolTeamType>(catalog.FindTeamType("alpha"));
        Assert.Equal("Alpha Team", type.Name);
        Assert.Equal("Does alpha work.", type.Description);
        Assert.Null(catalog.FindTeamType("beta"));
    }

    /// <summary>A job's persona parses into the title and stance, and an unknown job has none.</summary>
    [Fact]
    public void ReadPersona_KnownJob_ReturnsTheParsedTemplate()
    {
        using TempDataDir dir = new();

        HiringPoolCatalog catalog = Create(dir);

        CandidateTemplate persona = Assert.IsType<CandidateTemplate>(catalog.ReadPersona("bookkeeper"));
        Assert.Equal("Bookkeeper", persona.Title);
        Assert.Equal("You keep the books.", persona.Stance);
        Assert.Null(catalog.ReadPersona("nobody"));
    }

    /// <summary>A user pool adds its jobs and replaces a shipped job of the same id, without hiding the rest of the shipped pool.</summary>
    [Fact]
    public void Jobs_UserPool_MergesByIdOnTopOfTheBuiltInPool()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "hiringpool.json", """
            { "schemaVersion": 1, "jobs": [
              { "id": "bookkeeper", "title": "Senior Bookkeeper", "persona": "personas/bookkeeper.md" },
              { "id": "tutor", "title": "Tutor" } ] }
            """);
        WriteUser(dir, "personas/tutor.md", Persona.Replace("Bookkeeper", "Tutor", StringComparison.Ordinal));

        HiringPoolCatalog catalog = Create(dir);

        Assert.Equal("Senior Bookkeeper", catalog.FindJob("bookkeeper")?.Title);
        Assert.Equal("Tutor", catalog.FindJob("tutor")?.Title);
        Assert.Equal("Tutor", catalog.ReadPersona("tutor")?.Title);
        Assert.NotNull(catalog.FindTeamType("alpha"));
    }

    /// <summary>Reloading rereads the user pool and raises <c>Changed</c>.</summary>
    [Fact]
    public void Reload_AfterUserPoolAppears_ShowsItsJobsAndRaisesChanged()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog catalog = Create(dir);
        int raised = 0;
        catalog.Changed += () => raised++;
        WriteUser(dir, "hiringpool.json", """{ "schemaVersion": 1, "jobs": [ { "id": "tutor", "title": "Tutor" } ] }""");
        WriteUser(dir, "personas/tutor.md", Persona);

        catalog.Reload();

        Assert.NotNull(catalog.FindJob("tutor"));
        Assert.Equal(1, raised);
    }
}
