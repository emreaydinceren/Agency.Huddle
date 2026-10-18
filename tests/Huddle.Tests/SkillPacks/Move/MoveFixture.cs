using Agency.Huddle.App;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.SkillSources.Move;
using Agency.Huddle.App.Skills;
using Agency.Huddle.SkillPacks.Folders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.SkillPacks.Move;

/// <summary>A temporary data folder for the move (§6.13, "Tests"): the embedded catalog, a fake <c>Skills/</c>, optional user
/// Packs, an optional skills repository, a clock at 2026-10-17 16:25 +02:00 and the computer DESK-01.</summary>
internal sealed class MoveFixture : IDisposable
{
    private readonly TempDataDir dir = new();
    private string repositoryDir = string.Empty;

    /// <summary>Creates the folders.</summary>
    public MoveFixture()
    {
        this.Clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "+02:00", "+02:00"));
        Directory.CreateDirectory(this.SkillsDir);
    }

    /// <summary>The fake clock, at 2026-10-17 16:25 +02:00.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 14, 25, 0, TimeSpan.Zero));

    /// <summary>The data folder.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The Skill Sources folder.</summary>
    public string Dir => Path.Combine(this.dir.Path, "SkillSources");

    /// <summary>The copies' folder.</summary>
    public string SkillsDir => Path.Combine(this.dir.Path, "Skills");

    /// <summary>The fixture copy of the skills repository, in the test output.</summary>
    public static string FixtureRepository => Path.Combine(AppContext.BaseDirectory, "SkillPacks", "Fixtures", "skills-repo");

    /// <summary>Points <c>Team:SkillsRepositoryDir</c> at a folder.</summary>
    public MoveFixture WithRepository(string folder)
    {
        this.repositoryDir = folder;
        return this;
    }

    /// <summary>Writes an imported Skill into <c>Skills/</c> with a valid record whose hash matches.</summary>
    public string Imported(string name, string repo, string path, string commit, string publisher = "Pub", string license = "MIT", string? extraFrontmatter = null)
    {
        string folder = Path.Combine(this.SkillsDir, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILL.md"), $"---\nname: {name}\ndescription: Imported {name}.\n{extraFrontmatter}---\nBody.\n");
        Directory.CreateDirectory(Path.Combine(folder, ".git"));
        File.WriteAllText(Path.Combine(folder, ".git", "HEAD"), "ref");
        new SkillProvenance(repo, path, commit, SkillFolderHash.ComputeFolder(folder), publisher, license, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)).Write(folder);
        return folder;
    }

    /// <summary>Writes one of the Human's old Pack files.</summary>
    public void UserPack(string file, string json)
    {
        string folder = Path.Combine(this.dir.Path, "SkillPacks");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, file), json);
    }

    /// <summary>The settings the move runs with.</summary>
    public SkillSourcesSettings Settings()
    {
        return new SkillSourcesSettings(this.Options(), NullLogger<SkillSourcesSettings>.Instance);
    }

    /// <summary>The options the move runs with.</summary>
    public IOptions<TeamOptions> Options()
    {
        return Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = this.dir.Path, ComputerName = "DESK-01", SkillsRepositoryDir = this.repositoryDir });
    }

    /// <summary>A move over this folder, with an optional placement seam.</summary>
    public SkillSourcesMove Move(IMoveFiles? files = null)
    {
        SkillSourcesSettings settings = this.Settings();
        return new SkillSourcesMove(this.Options(), settings, files ?? new PhysicalMoveFiles(), new MoveReportStore(settings), new SourceLog(settings, this.Clock), NullLogger<SkillSourcesMove>.Instance, this.Clock);
    }

    /// <summary>Runs the move.</summary>
    public Task<MoveReport?> RunAsync(IMoveFiles? files = null)
    {
        return this.Move(files).RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Every file under a folder with its text, relative paths with <c>/</c>, for comparing trees.</summary>
    public static SortedDictionary<string, string> Tree(string folder, Func<string, bool>? skip = null)
    {
        SortedDictionary<string, string> tree = new(StringComparer.Ordinal);
        if (!Directory.Exists(folder))
        {
            return tree;
        }

        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (skip?.Invoke(relative) != true)
            {
                tree[relative] = File.ReadAllText(file);
            }
        }

        return tree;
    }

    /// <summary>Deletes the folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }
}
