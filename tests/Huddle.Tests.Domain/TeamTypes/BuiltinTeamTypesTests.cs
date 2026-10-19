using System.Text.Json;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests over every shipped team type and the shipped hiring pool: each type loads and is sound, each job fits the controlled tag and skill vocabulary, and each job has avatars that exist in the avatar library.</summary>
public sealed class BuiltinTeamTypesTests
{
    private static readonly string[] Tags =
    [
        "planning", "research", "writing", "editing", "verification", "analysis", "data", "code", "docs", "design", "finance", "tax",
        "budgeting", "accounting", "legal", "compliance", "health", "care", "home", "family", "education", "career", "sales", "marketing",
        "support", "community", "events", "travel", "email", "scheduling", "tracking", "triage", "forms", "purchasing", "security", "operations",
    ];

    private static readonly string[] Skills =
    [
        "web-research", "source-citing", "fact-checking", "outlining", "structural-editing", "copy-editing", "how-to-writing", "reply-drafting",
        "inbox-triage", "status-reporting", "meeting-prep", "minute-taking", "scope-definition", "deadline-tracking", "pipeline-tracking",
        "spreadsheet-modeling", "budget-modeling", "cost-estimation", "quote-comparison", "data-cleaning", "trend-analysis", "root-cause-analysis",
        "form-filling", "code-review", "repro-steps",
    ];

    private static readonly string[] NewTypeIds =
    [
        "small-business", "content", "research", "data", "product", "ops", "legal", "life-admin", "learning", "events", "job-search",
        "sales-outreach", "docs-team", "oss-maintainers", "grant-proposal", "tax-season", "support-desk", "founders-office", "home-project",
        "book-project", "caregiving", "community-org", "relocation", "home-lab",
    ];

    /// <summary>Every shipped type id, so a theory runs once per type.</summary>
    public static TheoryData<string> TypeIds
    {
        get
        {
            using TempDataDir dir = new();
            TheoryData<string> data = [];
            foreach (TeamTypeManifest type in new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance).Types)
            {
                data.Add(type.Id);
            }

            return data;
        }
    }

    private static HiringPoolCatalog Pool(TempDataDir dir) => new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance);

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

    /// <summary>The catalog ships the two original types first and all twenty-four new ones, with no problems and no type rejected.</summary>
    [Fact]
    public void Catalog_ShipsEveryPlannedType_WithNoProblems()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);

        Assert.Empty(catalog.Problems);
        Assert.Equal(["development", "household"], catalog.Types.Take(2).Select(static type => type.Id));
        Assert.Empty(NewTypeIds.Except(catalog.Types.Select(static type => type.Id), StringComparer.Ordinal));
        Assert.Equal(catalog.Types.Count, catalog.Types.Select(static type => type.Order).Distinct().Count());
    }

    /// <summary>Every shipped type has a name and a description from the pool, one manager, every coverage step covered, and no role without a job.</summary>
    /// <param name="typeId">The type id.</param>
    [Theory]
    [MemberData(nameof(TypeIds))]
    public void Type_IsSound(string typeId)
    {
        TeamTypeManifest type = BuiltinManifests.Load(typeId);

        Assert.False(string.IsNullOrWhiteSpace(type.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(type.Description));
        Assert.Single(type.Roles, static role => role.Manager);
        Assert.All(type.Roles, static role => Assert.NotNull(role.Job));
        Assert.All(type.Coverage, step => Assert.Contains(type.Roles, role => role.Covers.Contains(step, StringComparer.Ordinal)));
        Assert.Single(type.Processes, static process => process.Default);
    }

    /// <summary>Every job a type lists names that type among the types it suits, so the type cloud finds it.</summary>
    /// <param name="typeId">The type id.</param>
    [Theory]
    [MemberData(nameof(TypeIds))]
    public void Type_ListedJobs_NameTheTypeTheySuit(string typeId)
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = Pool(dir);

        TeamTypeManifest type = BuiltinManifests.Load(typeId);

        foreach (RoleSpec role in type.Roles)
        {
            PoolJob job = Assert.IsType<PoolJob>(pool.FindJob(role.Job ?? string.Empty));
            Assert.Contains(typeId, job.TeamTypes);
        }
    }

    /// <summary>The shipped pool loads with no problems, and every job has a title, a description and at least two tags.</summary>
    [Fact]
    public void Pool_Ships_WithNoProblemsAndDescribedJobs()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = Pool(dir);

        Assert.Empty(pool.Problems);
        Assert.True(pool.Jobs.Count >= 90, $"The pool holds only {pool.Jobs.Count} jobs.");
        Assert.All(pool.Jobs, static job =>
        {
            Assert.False(string.IsNullOrWhiteSpace(job.Description));
            Assert.True(job.Tags.Count >= 2, $"Job '{job.Id}' has fewer than two tags.");
            Assert.NotEmpty(job.TeamTypes);
        });
    }

    /// <summary>Every tag and every suggested skill name is in the controlled vocabulary, so the second cloud stays coherent.</summary>
    [Fact]
    public void Pool_TagsAndSkills_AreInTheControlledVocabulary()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = Pool(dir);

        Assert.Empty(pool.Jobs.SelectMany(static job => job.Tags).Except(Tags, StringComparer.Ordinal));
        Assert.Empty(pool.Jobs.SelectMany(static job => job.Skills).Except(Skills, StringComparer.Ordinal));
    }

    /// <summary>Every job names only team types the pool knows, so the type cloud has a chip for each.</summary>
    [Fact]
    public void Pool_JobTeamTypes_AreNamedByThePool()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = Pool(dir);
        HashSet<string> known = [.. pool.TeamTypes.Select(static type => type.Id)];

        Assert.All(pool.Jobs.SelectMany(static job => job.TeamTypes), id => Assert.Contains(id, known));
    }

    /// <summary>Every job suggests at least one avatar, and every suggested file is in the avatar library index.</summary>
    [Fact]
    public void Pool_Avatars_ExistInTheAvatarLibrary()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = Pool(dir);
        string index = Path.Combine(FindRepositoryRoot(), "src", "Huddle.App", "AvatarLibrary", "master.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(index));
        HashSet<string> files = [.. document.RootElement.GetProperty("avatars").EnumerateArray().Select(static item => item.GetProperty("file").GetString() ?? string.Empty)];

        Assert.All(pool.Jobs, job => Assert.NotEmpty(job.Avatars));
        Assert.All(pool.Jobs.SelectMany(static job => job.Avatars), file => Assert.Contains(file, files));
    }
}
