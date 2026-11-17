using System.Text;
using Agency.Huddle.App.Helpers.MemoryTidy;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="MemoryTidyJournal"/> (Memory Tidy spec §6.12 and §8.1, "The journal"): an entry is on disk when <c>Append</c> returns,
/// every entry kind round-trips, and a torn last line is ignored on read.
/// </summary>
public sealed class MemoryTidyJournalTests : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }

    /// <summary>The entry is readable through a second handle while the journal's own object is still alive, the moment <c>Append</c> returns.</summary>
    [Fact]
    public void Append_PutsTheEntryOnDisk_BeforeItReturns()
    {
        MemoryTidyJournal journal = new(this.dir.Path);

        journal.Append(new MemoryTidyEntry.FolderJudged("agent:Nova"));

        string text;
        using (FileStream second = new(Path.Combine(this.dir.Path, "pass.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (StreamReader reader = new(second, Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Contains("agent:Nova", text, StringComparison.Ordinal);
        Assert.Equal(new MemoryTidyEntry.FolderJudged("agent:Nova"), Assert.Single(MemoryTidyJournal.Read(this.dir.Path)));
    }

    /// <summary>Every kind of entry is written as one line and read back as the same kind with the same values.</summary>
    [Fact]
    public void Read_ReturnsEveryEntryKind_AsWritten()
    {
        MemoryTidyJournal journal = new(this.dir.Path);
        DateTimeOffset at = new(2026, 11, 22, 3, 0, 0, TimeSpan.Zero);
        MemoryTidyEntry.StepFile[] files = [new("pricing.md", "v1"), new("price-list.md", "v2")];
        MemoryTidyEntry[] written =
        [
            new MemoryTidyEntry.PassStarted("2026-11-22", at, MemoryTidyStart.Overdue),
            new MemoryTidyEntry.LinesDropped("Nova", 3, "Agents/Nova/reported.md"),
            new MemoryTidyEntry.FolderJudged("agent:Nova"),
            new MemoryTidyEntry.FolderNotJudged("huddle", "failed twice"),
            new MemoryTidyEntry.StepStarted(1, "agent:Nova", "merge", files, "Same fact twice"),
            new MemoryTidyEntry.StepApplied(1, "agent:Nova", ["pricing.md"], ["price-list.md"], ["Agents/Nova/pricing.md"], ["3", "2026-01-02"], ["old.md"], true, "kept the description: it is fine as it is"),
            new MemoryTidyEntry.StepRefused(2, "agent:Nova", "rewrite", ["contacts.md"], "Tighten", "A-4: a number was lost"),
            new MemoryTidyEntry.StepSkipped(3, "agent:Nova", "notes.md"),
            new MemoryTidyEntry.StepFaulted(4, "agent:Nova", "other.md"),
            new MemoryTidyEntry.StepSettled(5, "agent:Nova", false, ["a.md"], ["a.md"]),
            new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.ThreeFailures),
            new MemoryTidyEntry.PassFinished(true),
        ];

        foreach (MemoryTidyEntry entry in written)
        {
            journal.Append(entry);
        }

        IReadOnlyList<MemoryTidyEntry> read = MemoryTidyJournal.Read(this.dir.Path);
        Assert.Equal(written.Length, read.Count);
        Assert.Equal(written.Select(static entry => entry.GetType()), read.Select(static entry => entry.GetType()));
        Assert.Equal(new MemoryTidyEntry.PassStarted("2026-11-22", at, MemoryTidyStart.Overdue), read[0]);
        Assert.Equal(new MemoryTidyEntry.LinesDropped("Nova", 3, "Agents/Nova/reported.md"), read[1]);
        Assert.Equal(new MemoryTidyEntry.FolderNotJudged("huddle", "failed twice"), read[3]);
        MemoryTidyEntry.StepStarted started = Assert.IsType<MemoryTidyEntry.StepStarted>(read[4]);
        Assert.Equal((1, "agent:Nova", "merge", "Same fact twice"), (started.Step, started.FolderKey, started.Kind, started.Reason));
        Assert.Equal(files, started.Files);
        MemoryTidyEntry.StepApplied applied = Assert.IsType<MemoryTidyEntry.StepApplied>(read[5]);
        Assert.Equal(["pricing.md"], applied.Written);
        Assert.Equal(["price-list.md"], applied.Removed);
        Assert.Equal(["Agents/Nova/pricing.md"], applied.Copies);
        Assert.Equal(["3", "2026-01-02"], applied.RemovedTokens);
        Assert.Equal(["old.md"], applied.LeftBehind);
        Assert.True(applied.CountsAsDrop);
        Assert.Equal("kept the description: it is fine as it is", applied.Note);
        MemoryTidyEntry.StepRefused refused = Assert.IsType<MemoryTidyEntry.StepRefused>(read[6]);
        Assert.Equal((2, "agent:Nova", "rewrite", "Tighten", "A-4: a number was lost"), (refused.Step, refused.FolderKey, refused.Kind, refused.Reason, refused.Refusal));
        Assert.Equal(["contacts.md"], refused.Files);
        Assert.Equal(new MemoryTidyEntry.StepSkipped(3, "agent:Nova", "notes.md"), read[7]);
        Assert.Equal(new MemoryTidyEntry.StepFaulted(4, "agent:Nova", "other.md"), read[8]);
        MemoryTidyEntry.StepSettled settled = Assert.IsType<MemoryTidyEntry.StepSettled>(read[9]);
        Assert.False(settled.NotDone);
        Assert.Equal(["a.md"], settled.ChangedFiles);
        Assert.Equal(["a.md"], settled.CopiedFiles);
        Assert.Equal(new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.ThreeFailures), read[10]);
        Assert.Equal(new MemoryTidyEntry.PassFinished(true), read[11]);
    }

    /// <summary>A step's start entry holds the kind, the files, their versions and the reason, and the file has one line per entry.</summary>
    [Fact]
    public void Append_WritesOneLinePerEntry_AndEnumsByName()
    {
        MemoryTidyJournal journal = new(this.dir.Path);

        journal.Append(new MemoryTidyEntry.PassStarted("2026-11-22", new DateTimeOffset(2026, 11, 22, 3, 0, 0, TimeSpan.Zero), MemoryTidyStart.TidyNow));
        journal.Append(new MemoryTidyEntry.StepStarted(1, "huddle", "rewrite", [new("a.md", "v9")], "Tighten"));

        string[] lines = File.ReadAllText(Path.Combine(this.dir.Path, "pass.jsonl")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"TidyNow\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("rewrite", lines[1], StringComparison.Ordinal);
        Assert.Contains("v9", lines[1], StringComparison.Ordinal);
    }

    /// <summary>A last line cut off by a killed process is ignored, and an entry appended after it is not glued to it.</summary>
    [Fact]
    public void Read_IgnoresATornLastLine_AndAppendAfterItIsKept()
    {
        MemoryTidyJournal journal = new(this.dir.Path);
        journal.Append(new MemoryTidyEntry.FolderJudged("agent:Nova"));
        string file = Path.Combine(this.dir.Path, "pass.jsonl");
        File.AppendAllText(file, "{\"entry\":\"step-started\",\"step\":1,\"fol");

        IReadOnlyList<MemoryTidyEntry> torn = MemoryTidyJournal.Read(this.dir.Path);
        journal.Append(new MemoryTidyEntry.PassFinished(false));
        IReadOnlyList<MemoryTidyEntry> after = MemoryTidyJournal.Read(this.dir.Path);

        Assert.Equal(new MemoryTidyEntry.FolderJudged("agent:Nova"), Assert.Single(torn));
        Assert.Equal(2, after.Count);
        Assert.Equal(new MemoryTidyEntry.PassFinished(false), after[1]);
    }

    /// <summary>A folder with no journal reads as no entries.</summary>
    [Fact]
    public void Read_WithNoJournal_ReturnsNothing()
    {
        Assert.Empty(MemoryTidyJournal.Read(this.dir.Path));
    }
}
