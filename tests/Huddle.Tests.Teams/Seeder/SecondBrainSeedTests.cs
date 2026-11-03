using Agency.Huddle.App.Teams;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>Pins D-16: the <c>software-co</c> seed writes only the Second Brain layout (Spec §6.8, §7.1).</summary>
public sealed class SecondBrainSeedTests
{
    private static string[] Folders(string dataDir, string name) =>
        [.. Directory.EnumerateDirectories(dataDir, name, SearchOption.AllDirectories)];

    private static string[] Files(string dataDir, string name) =>
        [.. Directory.EnumerateFiles(dataDir, name, SearchOption.AllDirectories)];

    /// <summary>No Team, Project or Room folder in the seed is called <c>Uploads</c>; uploads live under <c>raw/</c>.</summary>
    [Fact]
    public async Task Seed_WritesNoUploadsFolderAnywhere()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Folders(seed.DataDir, "Uploads"));
    }

    /// <summary>Every seeded Room upload and Task attachment file sits below a <c>raw</c> folder.</summary>
    [Fact]
    public async Task Seed_PutsUploadsUnderRaw()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;

        Assert.NotEmpty(plan.Uploads);
        Assert.NotEmpty(plan.TaskUploads);
        foreach (SeedTaskUpload upload in plan.TaskUploads)
        {
            string path = Path.Combine(seed.DataDir, upload.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), path);
            Assert.Contains("raw", upload.RelativePath.Split('/'));
        }

        foreach (string file in plan.Uploads.SelectMany(u => Files(seed.DataDir, u.FileName)))
        {
            string relative = Path.GetRelativePath(seed.DataDir, file).Replace('\\', '/');
            Assert.Contains("raw", relative.Split('/'), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>The seed writes no <c>Instructions.md</c> anywhere: its text moved to <c>AGENTS.md</c>.</summary>
    [Fact]
    public async Task Seed_WritesNoInstructionsFile()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Files(seed.DataDir, "Instructions.md"));
    }

    /// <summary>The seed writes no <c>Process.md</c> (the process lives in <c>AGENTS.md</c> and the Team settings).</summary>
    [Fact]
    public async Task Seed_WritesNoProcessFile()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Files(seed.DataDir, "Process.md"));
    }

    /// <summary>Every Team folder and every Project folder has <c>raw/</c>, <c>memory/</c> and <c>outputs/</c>.</summary>
    [Fact]
    public async Task Seed_GivesEveryTeamAndProjectTheThreeFolders()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        string[] expected = [TeamNames.RawFolder, TeamNames.MemoryFolder, TeamNames.OutputsFolder];

        Assert.NotEmpty(plan.Teams);
        foreach (SeedTeam team in plan.Teams)
        {
            string teamFolder = Path.Combine(seed.DataDir, "Teams", team.Name);
            AssertHasFolders(teamFolder, expected);
            foreach (string project in team.Projects)
            {
                AssertHasFolders(Path.Combine(teamFolder, project), expected);
            }
        }
    }

    /// <summary>No seeded Team or Project is named like a reserved folder.</summary>
    [Fact]
    public async Task Seed_UsesNoReservedTeamOrProjectName()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;

        Assert.All(plan.Teams, t => Assert.False(TeamNames.IsReservedTeamName(t.Name), t.Name));
        Assert.All(plan.Teams.SelectMany(t => t.Projects), p => Assert.False(TeamNames.IsReservedProjectName(p), p));
    }

    private static void AssertHasFolders(string parent, string[] names)
    {
        foreach (string name in names)
        {
            string path = Path.Combine(parent, name);
            Assert.True(Directory.Exists(path), path);
        }
    }
}
