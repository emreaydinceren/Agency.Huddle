using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.Tests.Agents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.SkillPacks.Install;

/// <summary>Pins the shared <see cref="InstallContext"/> (§6.7, §7.2): what Huddle writes is exactly what huddle-packs reads.</summary>
public sealed class ContextFileTests
{
    /// <summary>A context with a role, a built-in and a copied Skill.</summary>
    internal static InstallContext Sample()
    {
        return new InstallContext(
            1,
            "2.4.0",
            "DESK-01",
            InMemoryFileReader.At("Data"),
            InMemoryFileReader.At("Data", "SkillSources"),
            InMemoryFileReader.At("Data", "Skills"),
            [new RoleInfo("developer", "Developer", "Writes code.", ["msbuild-authoring"]), new RoleInfo("bookkeeper", "Bookkeeper", "Keeps the books.", [])],
            [new InstalledSkill("read-skill", null, null, true), new InstalledSkill("docx", "https://github.com/o/r/tree/abc/docx", "9f2c", false)],
            ["propose_sprint", "propose_agents"],
            new DateTimeOffset(2026, 10, 17, 16, 25, 0, TimeSpan.FromHours(2)));
    }

    /// <summary>Writing then reading gives the same context, and the same JSON again.</summary>
    [Fact]
    public void RoundTrip_WriteThenRead_Equal()
    {
        InstallContext written = Sample();

        InstallContextReadResult read = InstallContext.Parse(written.ToJson());

        Assert.NotNull(read.Value);
        Assert.Equal(written.ToJson(), read.Value.ToJson());
        Assert.Contains("\"written\": \"2026-10-17T16:25:00+02:00\"", written.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("\r", written.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>A role without <c>suggestedPacks</c> reads with an empty list.</summary>
    [Fact]
    public void OptionalSuggestedPacks_Absent_Reads()
    {
        string json = Sample().ToJson().Replace("\"suggestedPacks\": [\n        \"msbuild-authoring\"\n      ]", "\"x\": 1", StringComparison.Ordinal);

        InstallContextReadResult read = InstallContext.Parse(json);

        Assert.NotNull(read.Value);
        Assert.Empty(read.Value.Roles[0].SuggestedPacks);
    }

    /// <summary>A missing required field is named in the reason.</summary>
    [Fact]
    public void MissingRequiredField_ReportsWhichOne()
    {
        string json = Sample().ToJson().Replace("\"skillsDir\"", "\"other\"", StringComparison.Ordinal);

        InstallContextReadResult read = InstallContext.Parse(json);

        Assert.Null(read.Value);
        Assert.Equal("missing skillsDir", read.Reason);
    }

    /// <summary>A context written for a newer schema is refused with its version.</summary>
    [Fact]
    public void NewerSchemaVersion_Refused()
    {
        string json = Sample().ToJson().Replace("\"schemaVersion\": 1", "\"schemaVersion\": 3", StringComparison.Ordinal);

        InstallContextReadResult read = InstallContext.Parse(json);

        Assert.Null(read.Value);
        Assert.Equal(3, read.NewerSchemaVersion);
    }

    /// <summary><see cref="CheckContext.From"/> copies every field one to one.</summary>
    [Fact]
    public void CheckContextFrom_CopiesEveryField()
    {
        InstallContext source = Sample();

        CheckContext context = CheckContext.From(source);

        Assert.Equal(source.Computer, context.ComputerName);
        Assert.Equal(source.SkillsDir, context.SkillsDir);
        Assert.Same(source.Roles, context.Roles);
        Assert.Same(source.AppTools, context.AppToolNames);
        Assert.Equal(source.Written, context.Written);
    }
}

/// <summary>Pins <see cref="RoleLister"/>: the union of custom roles, pool jobs and team-type roles, sorted and de-duplicated.</summary>
public sealed class RoleListerTests
{
    /// <summary>Roles come out ordered by id, each id once, a custom role winning over a pool job.</summary>
    [Fact]
    public void RoleLister_SortedByIdOrdinal_AndDeduplicated()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance);
        CustomRoleStore custom = new(dir.Options());
        CustomRole mine = custom.Add("Auditor", "Checks the books.", "Audit.", Agency.Huddle.App.TeamTypes.ModelTier.Balanced);
        RoleLister lister = new(pool, custom, RoleFixtures.Types(dir));

        IReadOnlyList<RoleInfo> roles = lister.List();

        Assert.Equal(roles.Select(static role => role.Id).Order(StringComparer.Ordinal), roles.Select(static role => role.Id));
        Assert.Equal(roles.Count, roles.Select(static role => role.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(roles, role => role.Id == mine.Id && role.Description == "Checks the books.");
    }

    /// <summary>The list holds custom roles, pool jobs (with their suggested Packs) and team-type roles.</summary>
    [Fact]
    public void Write_Roles_UnionsCustomPoolAndTeamTypes()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance);
        CustomRoleStore custom = new(dir.Options());
        CustomRole mine = custom.Add("Auditor", "Checks the books.", "Audit.", Agency.Huddle.App.TeamTypes.ModelTier.Balanced);
        RoleLister lister = new(pool, custom, RoleFixtures.Types(dir));

        IReadOnlyList<RoleInfo> roles = lister.List();

        PoolJob job = pool.Jobs[0];
        Assert.Contains(roles, role => role.Id == mine.Id);
        Assert.Contains(roles, role => role.Id == job.Id && role.SuggestedPacks.SequenceEqual(job.Packs));
        Assert.Contains(roles, static role => role.Id == "lead" && role.Title == "Lead");
        Assert.Contains(roles, static role => role.Id == "dev");
    }
}
