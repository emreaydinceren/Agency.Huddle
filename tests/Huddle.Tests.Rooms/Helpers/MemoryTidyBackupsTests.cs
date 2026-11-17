using Agency.Huddle.App;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="MemoryTidyBackups"/> (Memory Tidy spec §6.12 and §8.1, "Retention"): the pass folder's name, the subfolder of each memory folder, and the
/// guarded deletion of old pass folders.
/// </summary>
public sealed class MemoryTidyBackupsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new(supportsReparsePoints: true);
    private readonly ManualTimeProvider clock = new(Now, TimeZoneInfo.Utc);
    private readonly IOptions<TeamOptions> options;
    private readonly MemoryTidyBackups backups;

    /// <summary>Builds the backups over a fresh data folder and a fixed clock.</summary>
    public MemoryTidyBackupsTests()
    {
        this.options = this.dir.Options();
        this.backups = new MemoryTidyBackups(this.options, this.clock);
    }

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }

    /// <summary>The pass folder is named by the pass's local date, which is not the UTC date near midnight.</summary>
    [Fact]
    public void CreatePassFolder_IsNamedByTheLocalDate()
    {
        ManualTimeProvider late = new(new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.Zero), TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2"));
        MemoryTidyBackups local = new(this.options, late);

        (string id, string path) = local.CreatePassFolder();

        Assert.Equal("2026-11-23", id);
        Assert.Equal(Path.Combine(this.backups.RootPath, "2026-11-23"), path);
        Assert.True(Directory.Exists(path));
    }

    /// <summary>A second and a third pass on one day get <c>-2</c> and <c>-3</c>, and keep the first folder's files.</summary>
    [Fact]
    public void CreatePassFolder_OnATakenDay_AddsANumber()
    {
        (string first, string firstPath) = this.backups.CreatePassFolder();
        File.WriteAllText(Path.Combine(firstPath, "pass.jsonl"), "x");

        (string second, _) = this.backups.CreatePassFolder();
        (string third, _) = this.backups.CreatePassFolder();

        Assert.Equal(["2026-11-22", "2026-11-22-2", "2026-11-22-3"], [first, second, third]);
        Assert.Equal("x", File.ReadAllText(Path.Combine(firstPath, "pass.jsonl")));
    }

    /// <summary>The root follows <c>Team:Library:MemoryBackups:Dir</c>.</summary>
    [Fact]
    public void RootPath_FollowsTheConfiguredFolder()
    {
        MemoryTidyBackups custom = new(this.dir.Options(static o => o.Library.MemoryBackups.Dir = "Copies"), this.clock);

        Assert.Equal(Path.GetFullPath(Path.Combine(this.dir.Path, "Copies")), custom.RootPath);
    }

    /// <summary>An Agent's folder key maps to <c>Agents/&lt;Name&gt;</c>, Huddle-wide to <c>Huddle</c>, and anything else is refused.</summary>
    [Theory]
    [InlineData("agent:Nova", "Agents", "Nova")]
    [InlineData("huddle", "Huddle", "")]
    public void FolderFor_MapsKeyToSubfolder(string key, string first, string second)
    {
        string pass = Path.Combine(this.dir.Path, "pass");

        string folder = MemoryTidyBackups.FolderFor(pass, key);

        Assert.Equal(Path.Combine(pass, first, second), folder);
    }

    /// <summary>A key that is no memory folder, or whose name climbs out of the pass folder, is refused.</summary>
    [Theory]
    [InlineData("team:Alpha")]
    [InlineData("agent:")]
    [InlineData("agent:..")]
    [InlineData("agent:a/b")]
    [InlineData("agent:a\\b")]
    [InlineData("Huddle")]
    public void FolderFor_RefusesABadKey(string key)
    {
        _ = Assert.Throws<ArgumentException>(() => MemoryTidyBackups.FolderFor(this.dir.Path, key));
    }

    /// <summary>A pass folder older than the retention is deleted with its contents, and a newer one is kept; the day it turns old is the 91st.</summary>
    [Fact]
    public void DeleteOld_RemovesOldPassFolders_AndKeepsNewerOnes()
    {
        string old = this.MakePass("2026-08-01", daysSinceWritten: 113);
        string oldNumbered = this.MakePass("2026-08-01-2", daysSinceWritten: 113);
        string justOld = this.MakePass("2026-08-23", daysSinceWritten: 91);
        string justKept = this.MakePass("2026-08-24", daysSinceWritten: 90);
        string recent = this.MakePass("2026-11-20", daysSinceWritten: 2);
        File.WriteAllText(Path.Combine(old, "copy.md"), "x");

        int deleted = this.backups.DeleteOld();

        Assert.Equal(3, deleted);
        Assert.False(Directory.Exists(old));
        Assert.False(Directory.Exists(oldNumbered));
        Assert.False(Directory.Exists(justOld));
        Assert.True(Directory.Exists(justKept));
        Assert.True(Directory.Exists(recent));
    }

    /// <summary>A folder whose name is not a pass date is never deleted, however old its journal is, and neither is a plain file.</summary>
    [Theory]
    [InlineData("notes")]
    [InlineData("2026-08-01-x")]
    [InlineData("2026-08-01-")]
    [InlineData("2026-8-1")]
    [InlineData("2026-13-01")]
    [InlineData("2026-08-01b")]
    [InlineData("backup-2026-08-01")]
    public void DeleteOld_NeverDeletesAFolderWhoseNameIsNotAPassDate(string name)
    {
        string folder = this.MakePass(name, daysSinceWritten: 400);

        int deleted = this.backups.DeleteOld();

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>A pass-named file, and a pass-named folder inside a folder that is not a pass, are left alone: only direct children are candidates.</summary>
    [Fact]
    public void DeleteOld_NeverDeletesAFile_OrAFolderDeeperThanTheRoot()
    {
        string root = this.backups.RootPath;
        _ = Directory.CreateDirectory(root);
        string file = Path.Combine(root, "2026-01-01");
        File.WriteAllText(file, "not a folder");
        string deeper = Path.Combine(root, "archive", "2026-01-01");
        _ = Directory.CreateDirectory(deeper);
        File.WriteAllText(Path.Combine(deeper, "pass.jsonl"), "{}");
        File.SetLastWriteTimeUtc(Path.Combine(deeper, "pass.jsonl"), Now.UtcDateTime.AddDays(-400));

        int deleted = this.backups.DeleteOld();

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(file));
        Assert.True(Directory.Exists(deeper));
    }

    /// <summary>A folder old by its name but with a journal written recently (the clock was set far forward once) is kept.</summary>
    [Fact]
    public void DeleteOld_KeepsAFolderOldByNameButWithARecentJournal()
    {
        string folder = this.MakePass("2026-01-01", daysSinceWritten: 1);

        int deleted = this.backups.DeleteOld();

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>A folder old by its journal's write time but with a recent date in its name is kept.</summary>
    [Fact]
    public void DeleteOld_KeepsAFolderOldByJournalButWithARecentName()
    {
        string folder = this.MakePass("2026-11-20", daysSinceWritten: 400);

        int deleted = this.backups.DeleteOld();

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>A pass folder with no journal cannot be shown old by its journal, so it is kept.</summary>
    [Fact]
    public void DeleteOld_KeepsAFolderWithNoJournal()
    {
        string folder = Path.Combine(this.backups.RootPath, "2026-01-01");
        _ = Directory.CreateDirectory(folder);

        int deleted = this.backups.DeleteOld();

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>A pass whose journal has entries and no <c>PassFinished</c> is kept however old; the same pass once finished is deleted.</summary>
    [Fact]
    public void DeleteOld_KeepsAnUnfinishedPass_UntilItIsFinished()
    {
        string folder = this.MakePass("2026-01-01", daysSinceWritten: 300, finished: false);

        int whileOpen = this.backups.DeleteOld();
        bool keptWhileOpen = Directory.Exists(folder);
        new MemoryTidyJournal(folder).Append(new MemoryTidyEntry.PassFinished(false));
        File.SetLastWriteTimeUtc(Path.Combine(folder, "pass.jsonl"), Now.UtcDateTime.AddDays(-300));
        int afterFinish = this.backups.DeleteOld();

        Assert.Equal(0, whileOpen);
        Assert.True(keptWhileOpen);
        Assert.Equal(1, afterFinish);
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>A root that is a junction or link deletes nothing, though the folders behind it are old.</summary>
    [Fact]
    public void DeleteOld_WithTheRootALink_DeletesNothing()
    {
        string target = Path.Combine(this.dir.Path, "elsewhere");
        string old = Path.Combine(target, "2026-01-01");
        _ = Directory.CreateDirectory(old);
        new MemoryTidyJournal(old).Append(new MemoryTidyEntry.PassFinished(false));
        File.SetLastWriteTimeUtc(Path.Combine(old, "pass.jsonl"), Now.UtcDateTime.AddDays(-300));
        Assert.True(TestLinks.TryCreateLink(this.backups.RootPath, target));
        try
        {
            int deleted = this.backups.DeleteOld();

            Assert.Equal(0, deleted);
            Assert.True(Directory.Exists(old));
        }
        finally
        {
            TestLinks.RemoveLink(this.backups.RootPath);
        }
    }

    /// <summary>Zero or less keeps every backup, and a missing root is no error.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void DeleteOld_WithZeroOrLessKeepsEverything(int keepDays)
    {
        string folder = this.MakePass("2026-01-01", daysSinceWritten: 300);
        MemoryTidyBackups keeping = new(this.dir.Options(o => o.Memory.Tidy.KeepBackupDays = keepDays), this.clock);

        int deleted = keeping.DeleteOld();

        Assert.Equal(0, deleted);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>With no backup root yet, there is nothing to delete.</summary>
    [Fact]
    public void DeleteOld_WithNoRoot_DeletesNothing()
    {
        Assert.Equal(0, this.backups.DeleteOld());
    }

    /// <summary>The retention follows <c>KeepBackupDays</c>.</summary>
    [Fact]
    public void DeleteOld_FollowsKeepBackupDays()
    {
        string folder = this.MakePass("2026-11-01", daysSinceWritten: 21);
        MemoryTidyBackups shortKeep = new(this.dir.Options(static o => o.Memory.Tidy.KeepBackupDays = 7), this.clock);

        int deleted = shortKeep.DeleteOld();

        Assert.Equal(1, deleted);
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>Makes a pass folder with a journal whose write time is <paramref name="daysSinceWritten"/> days before now.</summary>
    /// <param name="name">The folder's name.</param>
    /// <param name="daysSinceWritten">How old the journal's write time is.</param>
    /// <param name="finished">Whether the journal ends with <c>PassFinished</c>.</param>
    private string MakePass(string name, int daysSinceWritten, bool finished = true)
    {
        string folder = Path.Combine(this.backups.RootPath, name);
        _ = Directory.CreateDirectory(folder);
        MemoryTidyJournal journal = new(folder);
        journal.Append(new MemoryTidyEntry.FolderJudged("huddle"));
        if (finished)
        {
            journal.Append(new MemoryTidyEntry.PassFinished(false));
        }

        File.SetLastWriteTimeUtc(Path.Combine(folder, "pass.jsonl"), Now.UtcDateTime.AddDays(-daysSinceWritten));
        return folder;
    }
}
