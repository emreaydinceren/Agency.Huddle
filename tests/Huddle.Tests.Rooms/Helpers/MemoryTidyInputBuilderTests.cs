using System.Text;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="MemoryTidyInputBuilder"/>, <see cref="MemoryTidyDays"/> and the offset rule (Memory Tidy spec §6.5, §6.8 and §8.1, "MemoryTidyInputBuilder"
/// and "Days and the offset").
/// </summary>
public sealed class MemoryTidyInputBuilderTests : IDisposable
{
    private const string Key = "agent:Nova";
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly DateTime Noon = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDataDir dir = new();
    private readonly string folderPath;
    private readonly TeamOptions teamOptions;
    private readonly MemoryTidyStateStore store;
    private readonly ManualTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    private readonly MemoryTidyInputBuilder builder;

    /// <summary>Builds the builder over a fresh data folder with an empty memory folder.</summary>
    public MemoryTidyInputBuilderTests()
    {
        IOptions<TeamOptions> options = this.dir.Options();
        this.teamOptions = options.Value;
        this.store = new MemoryTidyStateStore(options);
        this.folderPath = Path.Combine(this.dir.Path, "memory");
        _ = Directory.CreateDirectory(this.folderPath);
        this.builder = new MemoryTidyInputBuilder(this.store, options, this.clock);
    }

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }

    /// <summary><c>MEMORY.md</c>, <c>reported.md</c> in an own folder, a subfolder's files and other extensions are not topic files.</summary>
    [Fact]
    public void Build_OwnFolder_LeavesOutIndexReportedSubfoldersAndOtherFiles()
    {
        this.Write("MEMORY.md", "index");
        this.Write("reported.md", "told");
        this.Write("Reported.MD", "told too");
        this.Write("topic.md", "a topic");
        this.Write("note.txt", "not markdown");
        this.Write(Path.Combine("archive", "old.md"), "archived");

        MemoryTidyBuild build = this.Build();

        Assert.Equal(["topic.md"], build.Input.Files.Select(static file => file.File));
        Assert.Single(build.Input.Index);
        Assert.StartsWith("topic.md — ", build.Input.Index[0], StringComparison.Ordinal);
    }

    /// <summary>A <c>reported.md</c> in a Huddle-wide folder is an ordinary topic file, and only that key locks descriptions.</summary>
    [Fact]
    public void Build_HuddleFolder_TreatsReportedMdAsATopicFile_AndLocksDescriptions()
    {
        this.Write("reported.md", "a topic here");
        this.Write("MEMORY.md", "index");

        MemoryTidyBuild huddle = this.Build("huddle");
        MemoryTidyBuild own = this.Build();

        Assert.Equal(["reported.md"], huddle.Input.Files.Select(static file => file.File));
        Assert.True(huddle.Input.Limits.DescriptionsLocked);
        Assert.Empty(own.Input.Files);
        Assert.False(own.Input.Limits.DescriptionsLocked);
    }

    /// <summary>A <c>tidy: keep</c> file (key and value in any case) is in the index marked kept and never in full.</summary>
    [Fact]
    public void Build_TidyKeep_IsInTheIndexAsKeptAndNeverInFull()
    {
        this.Write("kept.md", "---\ndescription: Pricing rules\nTidy: KEEP\n---\nbody");
        this.Write("plain.md", "---\ndescription: Other\ntidy: no\n---\nbody");

        MemoryTidyBuild build = this.Build();

        Assert.Equal(["plain.md"], build.Input.Files.Select(static file => file.File));
        Assert.Equal("kept.md — Pricing rules — written 2026-11-10 — kept", build.Input.Index[0]);
        Assert.Equal("plain.md — Other — written 2026-11-10", build.Input.Index[1]);
        Assert.Equal(1, build.EligibleCount);
        Assert.True(build.AllFitted);
    }

    /// <summary>More than 200 topic files give 200 index lines and a last line counting the rest.</summary>
    [Fact]
    public void Build_MoreThanTwoHundredFiles_CapsTheIndexLines()
    {
        for (int i = 0; i < 201; i++)
        {
            this.Write($"f{i:D3}.md", "x");
        }

        MemoryTidyBuild build = this.Build();

        Assert.Equal(201, build.Input.Index.Count);
        Assert.Equal("[…1 more files not shown]", build.Input.Index[^1]);
    }

    /// <summary>The index stops at 12,000 characters and says how many files it left out.</summary>
    [Fact]
    public void Build_LongDescriptions_CapTheIndexCharacters()
    {
        string description = string.Concat(Enumerable.Repeat("word ", 40));
        for (int i = 0; i < 80; i++)
        {
            this.Write($"f{i:D3}.md", $"---\ndescription: {description}\n---\nbody");
        }

        MemoryTidyBuild build = this.Build();

        IReadOnlyList<string> shown = [.. build.Input.Index.Take(build.Input.Index.Count - 1)];
        Assert.InRange(shown.Sum(static line => line.Length + 1), 1, 12_000);
        Assert.True(shown.Count < 80);
        Assert.Equal($"[…{80 - shown.Count} more files not shown]", build.Input.Index[^1]);
    }

    /// <summary>A stored row whose version equals the file's present version gives the day, whatever the file time says.</summary>
    [Fact]
    public void Build_StoredRowWithMatchingVersion_GivesTheDay()
    {
        string path = this.Write("a.md", "same");
        File.SetLastWriteTimeUtc(path, Noon);
        string version = MemoryVersion.Of(File.ReadAllBytes(path));
        this.store.PutFile(new MemoryTidyFileRow(Key, "a.md", version, new DateOnly(2026, 1, 5)));

        MemoryTidyBuild build = this.Build();

        Assert.Equal(new DateOnly(2026, 1, 5), build.Input.Files[0].WrittenDay);
        Assert.Contains("written 2026-01-05", build.Input.Index[0], StringComparison.Ordinal);
    }

    /// <summary>A row whose version differs, or no row, gives the local day of the file's time, and the row is written.</summary>
    [Fact]
    public void Build_VersionDiffersOrNoRow_UsesTheLocalFileDay_AndWritesTheRow()
    {
        ManualTimeProvider east = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.CreateCustomTimeZone("plus3", TimeSpan.FromHours(3), "plus3", "plus3"));
        MemoryTidyInputBuilder local = new(this.store, Options.Create(this.teamOptions), east);
        string changed = this.Write("changed.md", "now different");
        string fresh = this.Write("fresh.md", "new");
        File.SetLastWriteTimeUtc(changed, new DateTime(2026, 11, 10, 22, 30, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(fresh, new DateTime(2026, 11, 11, 1, 0, 0, DateTimeKind.Utc));
        this.store.PutFile(new MemoryTidyFileRow(Key, "changed.md", "stale-version", new DateOnly(2020, 1, 1)));

        MemoryTidyBuild build = local.Build(this.Folder(), Key, "the Agent Nova", Today, Context())
            ?? throw new InvalidOperationException("The folder could not be read.");

        Assert.Equal(new DateOnly(2026, 11, 11), build.Input.Files.Single(static file => file.File == "changed.md").WrittenDay);
        Assert.Equal(new DateOnly(2026, 11, 11), build.Input.Files.Single(static file => file.File == "fresh.md").WrittenDay);
        IReadOnlyList<MemoryTidyFileRow> rows = this.store.GetFiles(Key);
        Assert.Equal(new DateOnly(2026, 11, 11), rows.Single(static row => row.File == "changed.md").WrittenDay);
        Assert.Equal(MemoryVersion.Of(File.ReadAllBytes(changed)), rows.Single(static row => row.File == "changed.md").Version);
        Assert.Equal(2, rows.Count);
    }

    /// <summary>Rows of files no longer in the folder are deleted when the input is built.</summary>
    [Fact]
    public void Build_RowOfAFileThatIsGone_IsDeleted()
    {
        _ = this.Write("here.md", "x");
        this.store.PutFile(new MemoryTidyFileRow(Key, "gone.md", "v", Today));
        this.store.PutFile(new MemoryTidyFileRow("huddle", "gone.md", "v", Today));

        _ = this.Build();

        Assert.Equal(["here.md"], this.store.GetFiles(Key).Select(static row => row.File));
        Assert.Single(this.store.GetFiles("huddle"));
    }

    /// <summary>Files in full are ordered by written day, then name, and start at the stored offset.</summary>
    [Fact]
    public void Build_OrdersByDayThenName_AndStartsAtTheOffset()
    {
        this.WriteSized("b.md", 100, Noon);
        this.WriteSized("a.md", 100, Noon);
        this.WriteSized("z.md", 100, Noon.AddDays(-5));
        this.teamOptions.Memory.Tidy.MaxInputChars = 250;

        MemoryTidyBuild first = this.Build();
        this.SetOffset(2);
        MemoryTidyBuild third = this.Build();

        Assert.Equal(["z.md", "a.md"], first.Input.Files.Select(static file => file.File));
        Assert.Equal(["b.md", "z.md"], third.Input.Files.Select(static file => file.File));
    }

    /// <summary>The list wraps from its end to its start, and a file that does not fit is skipped and the next one tried.</summary>
    [Fact]
    public void Build_WrapsRound_AndSkipsAFileThatDoesNotFit()
    {
        this.WriteSized("a.md", 100, Noon);
        this.WriteSized("b.md", 300, Noon);
        this.WriteSized("c.md", 100, Noon);
        this.WriteSized("d.md", 100, Noon);
        this.WriteSized("e.md", 100, Noon);
        this.teamOptions.Memory.Tidy.MaxInputChars = 250;

        MemoryTidyBuild fromStart = this.Build();
        this.SetOffset(4);
        MemoryTidyBuild wrapped = this.Build();

        Assert.Equal(["a.md", "c.md"], fromStart.Input.Files.Select(static file => file.File));
        Assert.Equal(["e.md", "a.md"], wrapped.Input.Files.Select(static file => file.File));
        Assert.Equal(2, wrapped.Shown);
        Assert.False(wrapped.AllFitted);
        Assert.Equal(5, wrapped.EligibleCount);
    }

    /// <summary>A file with a byte-order mark is shown without it, and keeps the version of its bytes.</summary>
    [Fact]
    public void Build_FileWithByteOrderMark_IsShownWithoutIt()
    {
        string path = Path.Combine(this.folderPath, "bom.md");
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("---\ndescription: Hello\n---\nbody")];
        File.WriteAllBytes(path, bytes);

        MemoryTidyBuild build = this.Build();

        Assert.StartsWith("---", build.Input.Files[0].Text, StringComparison.Ordinal);
        Assert.Equal(MemoryVersion.Of(bytes), build.Input.Files[0].Version);
        Assert.StartsWith("bom.md — Hello", build.Input.Index[0], StringComparison.Ordinal);
    }

    /// <summary>A folder that cannot be read (a file where the folder should be) gives no input, not an exception.</summary>
    [Fact]
    public void Build_UnreadableFolder_GivesNoInput()
    {
        string notAFolder = Path.Combine(this.dir.Path, "file-not-folder");
        File.WriteAllText(notAFolder, "x");
        MemoryFolder folder = new(notAFolder, "me", null);

        MemoryTidyBuild? build = this.builder.Build(folder, Key, "the Agent Nova", Today, Context());
        MemoryTidyBuild? missing = this.builder.Build(new MemoryFolder(Path.Combine(this.dir.Path, "nowhere"), "me", null), Key, "the Agent Nova", Today, Context());

        Assert.Null(build);
        Assert.Null(missing);
    }

    /// <summary>When every file fitted the offset to store is zero; otherwise it moves on by the number shown and wraps.</summary>
    [Theory]
    [InlineData(3, 2, 6, false, 5)]
    [InlineData(4, 2, 6, false, 0)]
    [InlineData(5, 2, 6, false, 1)]
    [InlineData(4, 6, 6, true, 0)]
    [InlineData(7, 0, 0, true, 0)]
    public void NextOffset_ZeroWhenAllFitted_OtherwiseMovesOnAndWraps(int offset, int shown, int count, bool allFitted, int expected)
    {
        Assert.Equal(expected, MemoryTidyInputBuilder.NextOffset(offset, shown, count, allFitted));
    }

    /// <summary>A folder of three calls' size is shown whole across three passes although no file changed.</summary>
    [Fact]
    public void Build_AFolderOfThreeCallsSize_IsShownWholeAcrossThreePasses()
    {
        foreach (string name in new[] { "a", "b", "c", "d", "e", "f" })
        {
            this.WriteSized($"{name}.md", 100, Noon);
        }

        this.teamOptions.Memory.Tidy.MaxInputChars = 250;
        List<string> seen = [];
        for (int pass = 0; pass < 3; pass++)
        {
            MemoryTidyBuild build = this.Build();
            seen.AddRange(build.Input.Files.Select(static file => file.File));
            this.SetOffset(MemoryTidyInputBuilder.NextOffset(build.Offset, build.Shown, build.EligibleCount, build.AllFitted));
        }

        Assert.Equal(["a.md", "b.md", "c.md", "d.md", "e.md", "f.md"], seen);
        Assert.Equal(0, this.store.GetState(Key)?.Offset);
    }

    /// <summary>The fingerprint follows the key, the date, the index, the shown files and the limits, and never the context.</summary>
    [Fact]
    public void Fingerprint_FollowsTheInput_ButNotTheContext()
    {
        this.Write("a.md", "one");
        MemoryTidyInput input = this.Build().Input;
        MemoryTidyInput otherContext = input with { Context = Context("2030-01-01") };

        string baseline = MemoryTidyInputBuilder.Fingerprint(input);

        Assert.Equal(baseline, MemoryTidyInputBuilder.Fingerprint(otherContext));
        Assert.NotEqual(baseline, MemoryTidyInputBuilder.Fingerprint(input with { Today = Today.AddDays(1) }));
        Assert.NotEqual(baseline, MemoryTidyInputBuilder.Fingerprint(input with { FolderKey = "huddle" }));
        Assert.NotEqual(baseline, MemoryTidyInputBuilder.Fingerprint(input with { Limits = input.Limits with { MaxDrops = 9 } }));
        Assert.NotEqual(baseline, MemoryTidyInputBuilder.Fingerprint(input with { Files = [] }));
        Assert.NotEqual(baseline, MemoryTidyInputBuilder.Fingerprint(input with { Index = ["other"] }));
    }

    /// <summary>The folder fingerprint ignores the order given and changes with any name or version.</summary>
    [Fact]
    public void FolderFingerprint_IsOrderIndependent_AndFollowsNamesAndVersions()
    {
        string one = MemoryTidyInputBuilder.FolderFingerprint([("b.md", "v2"), ("a.md", "v1")]);

        Assert.Equal(one, MemoryTidyInputBuilder.FolderFingerprint([("a.md", "v1"), ("b.md", "v2")]));
        Assert.NotEqual(one, MemoryTidyInputBuilder.FolderFingerprint([("a.md", "v1"), ("b.md", "v3")]));
        Assert.NotEqual(one, MemoryTidyInputBuilder.FolderFingerprint([("a.md", "v1")]));
    }

    /// <summary>The build carries the folder fingerprint of every topic file, shown or not.</summary>
    [Fact]
    public void Build_FolderFingerprint_CoversEveryTopicFile()
    {
        string kept = this.Write("kept.md", "---\ntidy: keep\n---\nx");
        string plain = this.Write("p.md", "y");
        this.Write("MEMORY.md", "index");

        MemoryTidyBuild build = this.Build();

        Assert.Equal(
            MemoryTidyInputBuilder.FolderFingerprint([("kept.md", MemoryVersion.Of(File.ReadAllBytes(kept))), ("p.md", MemoryVersion.Of(File.ReadAllBytes(plain)))]),
            build.FolderFingerprint);
    }

    /// <summary>After a describe or a rewrite the file's row has the new version and keeps its day.</summary>
    [Fact]
    public void Days_Kept_KeepsTheDayAndTakesTheNewVersion()
    {
        this.store.PutFile(new MemoryTidyFileRow(Key, "a.md", "v1", new DateOnly(2026, 3, 1)));
        MemoryTidyDays days = new(this.store, Key);

        days.Kept("a.md", "v2");
        days.Kept("missing.md", "v9");

        Assert.Equal([new MemoryTidyFileRow(Key, "a.md", "v2", new DateOnly(2026, 3, 1))], this.store.GetFiles(Key));
    }

    /// <summary>A merge takes the latest day among the sources, so old content never looks newer than the fact that replaced it.</summary>
    [Fact]
    public void Days_Merged_TakesTheLatestDayOfTheSources()
    {
        this.store.PutFile(new MemoryTidyFileRow(Key, "a.md", "v1", new DateOnly(2026, 3, 1)));
        this.store.PutFile(new MemoryTidyFileRow(Key, "b.md", "v1", new DateOnly(2026, 6, 9)));
        this.store.PutFile(new MemoryTidyFileRow(Key, "c.md", "v1", new DateOnly(2026, 4, 2)));
        MemoryTidyDays days = new(this.store, Key);

        days.Merged("a.md", ["a.md", "b.md", "c.md"], "v7");

        MemoryTidyFileRow merged = this.store.GetFiles(Key).Single(static row => row.File == "a.md");
        Assert.Equal(new MemoryTidyFileRow(Key, "a.md", "v7", new DateOnly(2026, 6, 9)), merged);
    }

    /// <summary>A removed file loses its row, and no other folder's row of that name.</summary>
    [Fact]
    public void Days_Removed_DeletesTheRow()
    {
        this.store.PutFile(new MemoryTidyFileRow(Key, "a.md", "v1", Today));
        this.store.PutFile(new MemoryTidyFileRow("huddle", "a.md", "v1", Today));

        new MemoryTidyDays(this.store, Key).Removed("a.md");

        Assert.Empty(this.store.GetFiles(Key));
        Assert.Single(this.store.GetFiles("huddle"));
    }

    private static MemoryTidyPassContext Context(string passId = "2026-11-22") => new(passId, "backup", new NoJournal());

    private MemoryFolder Folder() => new(this.folderPath, "me", null);

    private MemoryTidyBuild Build(string key = Key) =>
        this.builder.Build(this.Folder(), key, key == "huddle" ? "every Agent (Huddle-wide Memory)" : "the Agent Nova", Today, Context())
        ?? throw new InvalidOperationException("The folder could not be read.");

    private void SetOffset(int offset) => this.store.PutState(new MemoryTidyState(Key, null, null, null, 0, null, offset));

    private string Write(string relative, string text)
    {
        string path = Path.Combine(this.folderPath, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.folderPath);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, Noon);
        return path;
    }

    private void WriteSized(string name, int length, DateTime written)
    {
        string path = this.Write(name, new string('x', length));
        File.SetLastWriteTimeUtc(path, written);
    }

    private sealed class NoJournal : IMemoryTidyJournal
    {
        public void Append(MemoryTidyEntry entry)
        {
        }
    }
}
