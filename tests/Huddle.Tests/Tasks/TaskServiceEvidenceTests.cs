using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskService.AddEvidence"/>: the append-only "## Evidence" section beside the Change log
/// (PR-7, Spec Evidence).
/// </summary>
public sealed class TaskServiceEvidenceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a structured Evidence line by Nova for the given command, stamped at <see cref="Noon"/>.</summary>
    private static EvidenceLine Line(string command, int? exitCode = 0) =>
        new(Noon, "Nova", command, exitCode, null, null, string.Empty);

    /// <summary>Creates a Task through the harness's real service and returns it.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string description = "")
    {
        TaskResult result = harness.Service.Create(
            new TaskDraft("Fix login", "Platform", null, Description: description),
            TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>The acting Agent used for Evidence.</summary>
    private static TaskActor Nova() => new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Reads the Task's file text, normalised to LF.</summary>
    private static string ReadText(TaskToolHarness harness, TaskId id)
    {
        string? text = harness.Store.ReadText(id);
        Assert.NotNull(text);
        return text.ReplaceLineEndings("\n");
    }

    /// <summary>The lines strictly between the Evidence heading and the next heading.</summary>
    private static string[] SectionLines(string text)
    {
        string[] lines = text.Split('\n');
        int start = Array.IndexOf(lines, "## Evidence");
        Assert.True(start >= 0);
        return [.. lines.Skip(start + 1).TakeWhile(static line => !line.StartsWith('#')).Where(static line => line.Length > 0)];
    }

    /// <summary>A Task with no Evidence section gets one, placed before the Change log.</summary>
    [Fact]
    public void AddEvidence_NoSection_CreatesSectionBeforeChangeLog()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness, "Body text.");
        EvidenceLine line = Line("dotnet test");

        TaskResult result = harness.Service.AddEvidence(task.Id, line, Nova());

        Assert.IsType<TaskResult.Saved>(result);
        string text = ReadText(harness, task.Id);
        Assert.True(text.IndexOf("## Evidence", StringComparison.Ordinal) < text.IndexOf("## Change log", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Body text.", StringComparison.Ordinal) < text.IndexOf("## Evidence", StringComparison.Ordinal));
        Assert.Equal(["2026-10-08T12:00Z | Nova | dotnet test | exit 0"], SectionLines(text));
    }

    /// <summary>A second line lands after the first, in the same section.</summary>
    [Fact]
    public void AddEvidence_SecondLine_IsAppendedAfterFirst()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);

        _ = harness.Service.AddEvidence(task.Id, Line("dotnet build"), Nova());
        _ = harness.Service.AddEvidence(task.Id, Line("dotnet test", 1), Nova());

        string text = ReadText(harness, task.Id);
        Assert.Equal(
            ["2026-10-08T12:00Z | Nova | dotnet build | exit 0", "2026-10-08T12:00Z | Nova | dotnet test | exit 1"],
            SectionLines(text));
        Assert.Equal(1, text.Split("## Evidence").Length - 1);
    }

    /// <summary>Lines already in the section, including a human note, stay byte-identical, and the text around them is kept.</summary>
    [Fact]
    public void AddEvidence_ExistingLines_AreKeptByteIdentical()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        string description = "Intro.\n\n## Evidence\nChecked by hand on staging.\n2026-10-07T09:30Z | Kai | npm test | exit 0\n\n## Notes\nAfter.";
        TaskItem task = CreateTask(harness, description);
        string before = ReadText(harness, task.Id);

        _ = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        string after = ReadText(harness, task.Id);
        Assert.Equal(
            ["Checked by hand on staging.", "2026-10-07T09:30Z | Kai | npm test | exit 0", "2026-10-08T12:00Z | Nova | dotnet test | exit 0"],
            SectionLines(after));
        string intro = before[..before.IndexOf("\n\n## Notes", StringComparison.Ordinal)];
        Assert.Equal(intro, after[..intro.Length]);
        int notes = after.IndexOf("## Notes", StringComparison.Ordinal);
        Assert.Equal("## Notes\nAfter.", after[notes..after.IndexOf("## Change log", StringComparison.Ordinal)].TrimEnd());
    }

    /// <summary>The Change log records who added Evidence and keeps its earlier entries.</summary>
    [Fact]
    public void AddEvidence_Saved_RecordsChangeLogEntry()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(2, saved.Task.ChangeLog.Count);
        Assert.Equal("created", saved.Task.ChangeLog[0].Summary);
        Assert.Equal("evidence added by Nova", saved.Task.ChangeLog[1].Summary);
        Assert.Equal("Nova", saved.Task.ChangeLog[1].Actor);
    }

    /// <summary>An unknown Task id is NotFound and writes nothing.</summary>
    [Fact]
    public void AddEvidence_UnknownTask_IsNotFound()
    {
        using TaskToolHarness harness = new();

        TaskResult result = harness.Service.AddEvidence(new TaskId("PLAT", 999), Line("dotnet test"), Nova());

        Assert.IsType<TaskResult.NotFound>(result);
    }
}
