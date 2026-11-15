using System.Reflection;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="TaskService.AddEvidence"/> and <see cref="TaskService.AddComment"/>: each appends exactly one line to the
/// Task's append-only timeline and never rewrites an earlier one (Spec §7.7, ADR-0086).
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

    /// <summary>The entry lines under the timeline heading, in file order.</summary>
    private static string[] TimelineLines(string text)
    {
        string[] lines = text.Split('\n');
        int start = Array.IndexOf(lines, "## Timeline");
        Assert.True(start >= 0);
        return [.. lines.Skip(start + 1).Where(static line => line.StartsWith("- ", StringComparison.Ordinal))];
    }

    /// <summary>The number of event lines in a Task file.</summary>
    private static int EventLineCount(string text) =>
        TimelineLines(text).Count(static line => line.Contains(" | event | ", StringComparison.Ordinal));

    /// <summary>Adding Evidence appends one timeline line after the description and before nothing else: no Evidence section, no Change log.</summary>
    [Fact]
    public void AddEvidence_AppendsOneEvidenceLineToTheTimeline()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness, "Body text.");

        TaskResult result = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        Assert.IsType<TaskResult.Saved>(result);
        string text = ReadText(harness, task.Id);
        Assert.DoesNotContain("## Evidence", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Change log", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("Body text.", StringComparison.Ordinal) < text.IndexOf("## Timeline", StringComparison.Ordinal));
        Assert.Equal("- 2026-10-08T12:00:00Z | Nova | evidence | dotnet test | exit 0", TimelineLines(text)[^1]);
        Assert.Equal(2, TimelineLines(text).Length);
    }

    /// <summary>The entry is stamped by the service's clock, not by the time the line itself carries.</summary>
    [Fact]
    public void AddEvidence_LineTime_IsNotWritten()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        EvidenceLine old = new(new DateTimeOffset(2020, 1, 2, 3, 4, 0, TimeSpan.Zero), "Nova", "dotnet test", 0, null, null, string.Empty);

        _ = harness.Service.AddEvidence(task.Id, old, Nova());

        Assert.Equal("- 2026-10-08T12:00:00Z | Nova | evidence | dotnet test | exit 0", TimelineLines(ReadText(harness, task.Id))[^1]);
    }

    /// <summary>Adding Evidence writes no event line: the Task has the one event it was created with.</summary>
    [Fact]
    public void AddEvidence_WritesNoEventLine()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);

        _ = harness.Service.AddEvidence(task.Id, Line("dotnet build"), Nova());
        _ = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        string text = ReadText(harness, task.Id);
        Assert.Equal(1, EventLineCount(text));
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(["created"], stored.ChangeLog.Select(static entry => entry.Summary));
    }

    /// <summary>A second line lands after the first, and the Evidence reads back in order.</summary>
    [Fact]
    public void AddEvidence_SecondLine_IsAppendedAfterFirst()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);

        _ = harness.Service.AddEvidence(task.Id, Line("dotnet build"), Nova());
        _ = harness.Service.AddEvidence(task.Id, Line("dotnet test", 1), Nova());

        string[] lines = TimelineLines(ReadText(harness, task.Id));
        Assert.Equal(
            ["- 2026-10-08T12:00:00Z | Nova | evidence | dotnet build | exit 0", "- 2026-10-08T12:00:00Z | Nova | evidence | dotnet test | exit 1"],
            lines[1..]);
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(["dotnet build", "dotnet test"], stored.Evidence.Select(static line => line.Command));
    }

    /// <summary>Everything already in the file, a comment and a note among it, stays byte-identical: the new text is exactly one appended line.</summary>
    [Fact]
    public void AddEvidence_ExistingLines_AreKeptByteIdentical()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness, "Intro.\n\n## Notes\nAfter.");
        TaskActor kai = new(TaskActorKind.Agent, "Kai", "kai-id");
        _ = harness.Service.AddEvidence(task.Id, new EvidenceLine(Noon, "Kai", string.Empty, null, null, null, "Checked by hand on staging."), kai);
        _ = harness.Service.AddComment(task.Id, "A | tricky\ncomment", kai);
        _ = harness.Service.AddEvidence(task.Id, new EvidenceLine(Noon, "Kai", "npm test", 0, null, null, string.Empty), kai);
        string before = ReadText(harness, task.Id);

        _ = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        string after = ReadText(harness, task.Id);
        Assert.Equal(before + "- 2026-10-08T12:00:00Z | Nova | evidence | dotnet test | exit 0\n", after);
    }

    /// <summary>The change raised for Evidence carries the in-memory summary "evidence added by X", once, and the file does not.</summary>
    [Fact]
    public void AddEvidence_RaisesOneChangeWithTheSummary()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        TaskResult result = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        TaskChange change = Assert.Single(changes);
        Assert.Equal("evidence added by Nova", change.Entry.Summary);
        Assert.Equal("Nova", change.Entry.Actor);
        Assert.Equal(saved.Task.Version, change.After.Version);
        Assert.DoesNotContain("evidence added by", ReadText(harness, task.Id), StringComparison.Ordinal);
    }

    /// <summary>The Task keeps only its created event; the Evidence is an entry of the timeline authored by the actor.</summary>
    [Fact]
    public void AddEvidence_Saved_IsATimelineEntryNotAnEvent()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);

        TaskResult result = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(["created"], saved.Task.ChangeLog.Select(static entry => entry.Summary));
        TimelineEntry entry = Assert.Single(saved.Task.Comments);
        Assert.Equal(TimelineEntryKind.Evidence, entry.Kind);
        Assert.Equal("Nova", entry.Author);
        Assert.Equal(Noon, entry.At);
    }

    /// <summary>An unknown Task id is NotFound and writes nothing.</summary>
    [Fact]
    public void AddEvidence_UnknownTask_IsNotFound()
    {
        using TaskToolHarness harness = new();

        TaskResult result = harness.Service.AddEvidence(new TaskId("PLAT", 999), Line("dotnet test"), Nova());

        Assert.IsType<TaskResult.NotFound>(result);
    }

    /// <summary>A comment is one <c>comment</c> line with its text, and no event line.</summary>
    [Fact]
    public void AddComment_AppendsOneCommentLine_AndNoEventLine()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness, "Body.");
        string before = ReadText(harness, task.Id);

        TaskResult result = harness.Service.AddComment(task.Id, "  Looks right to me.  ", Nova());

        Assert.IsType<TaskResult.Saved>(result);
        string after = ReadText(harness, task.Id);
        Assert.Equal(before.TrimEnd('\n') + "\n- 2026-10-08T12:00:00Z | Nova | comment | Looks right to me.\n", after);
        Assert.Equal(1, EventLineCount(after));
    }

    /// <summary>A comment spanning lines and holding the field separator reads back exactly.</summary>
    [Fact]
    public void AddComment_MultiLineText_SurvivesAReRead()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        const string text = "First line.\n\nSecond | line with a \\ backslash.\n- a list item";

        _ = harness.Service.AddComment(task.Id, text, Nova());

        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        TimelineEntry entry = Assert.Single(stored.Comments);
        Assert.Equal(TimelineEntryKind.Comment, entry.Kind);
        Assert.Equal("Nova", entry.Author);
        Assert.Equal(text, entry.Text);
        Assert.Equal(text, Assert.Single(stored.Timeline, static candidate => candidate.Kind == TimelineEntryKind.Comment).Text);
    }

    /// <summary>Empty or blank text is refused with its message and nothing is written or raised.</summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  \n\t ")]
    public void AddComment_BlankText_IsRefusedAndWritesNothing(string text)
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        string before = ReadText(harness, task.Id);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        TaskResult result = harness.Service.AddComment(task.Id, text, Nova());

        Assert.Equal("A comment needs some text.", Assert.Single(Assert.IsType<TaskResult.Refused>(result).Problems));
        Assert.Equal(before, ReadText(harness, task.Id));
        Assert.Empty(changes);
    }

    /// <summary>4001 characters is refused with the count in the message; 4000 is saved.</summary>
    [Fact]
    public void AddComment_FourThousandAndOneCharacters_IsRefusedAndFourThousandIsSaved()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        string before = ReadText(harness, task.Id);

        TaskResult tooLong = harness.Service.AddComment(task.Id, new string('x', 4001), Nova());

        Assert.Equal("A comment can be at most 4000 characters; this one has 4001.", Assert.Single(Assert.IsType<TaskResult.Refused>(tooLong).Problems));
        Assert.Equal(before, ReadText(harness, task.Id));
        Assert.IsType<TaskResult.Saved>(harness.Service.AddComment(task.Id, new string('x', 4000), Nova()));
        TaskItem? stored = harness.Store.Get(task.Id);
        Assert.NotNull(stored);
        Assert.Equal(4000, Assert.Single(stored.Comments).Text.Length);
    }

    /// <summary>An unknown Task id is NotFound.</summary>
    [Fact]
    public void AddComment_UnknownTask_IsNotFound()
    {
        using TaskToolHarness harness = new();

        TaskResult result = harness.Service.AddComment(new TaskId("PLAT", 999), "hello", Nova());

        Assert.IsType<TaskResult.NotFound>(result);
    }

    /// <summary>A comment raises one change with the in-memory summary "comment added by X"; the file does not hold it.</summary>
    [Fact]
    public void AddComment_RaisesOneChangeWithTheSummary()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = harness.Service.AddComment(task.Id, "hello", Nova());

        TaskChange change = Assert.Single(changes);
        Assert.Equal("comment added by Nova", change.Entry.Summary);
        Assert.Equal("Nova", change.Entry.Actor);
        Assert.DoesNotContain("comment added by", ReadText(harness, task.Id), StringComparison.Ordinal);
    }

    /// <summary>A closed Task can still be commented on, as it can still be given Evidence.</summary>
    [Fact]
    public void AddComment_ClosedTask_IsSaved()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Close(task.Id, TaskActors.Human(harness.Options.Value)));

        TaskResult result = harness.Service.AddComment(task.Id, "Afterthought.", Nova());

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("Afterthought.", Assert.Single(saved.Task.Comments).Text);
    }

    /// <summary>A description write never changes the comments and Evidence already on the Task.</summary>
    [Fact]
    public void Update_Description_NeverChangesComments()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        TaskItem task = CreateTask(harness, "Old body.");
        _ = harness.Service.AddEvidence(task.Id, Line("dotnet test"), Nova());
        _ = harness.Service.AddComment(task.Id, "A remark.", Nova());
        TaskItem? before = harness.Store.Get(task.Id);
        Assert.NotNull(before);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Description = "New body." }, null, Nova());

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("New body.", saved.Task.Description);
        Assert.Equal(2, before.Comments.Count);
        Assert.Equal(before.Comments, saved.Task.Comments);
    }

    /// <summary>The service offers one way to add Evidence and one to add a comment, and none that edits or removes a timeline entry.</summary>
    [Fact]
    public void TaskService_PublicSurface_HasNoWayToEditOrRemoveATimelineEntry()
    {
        string[] names = [.. typeof(TaskService).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.IsPublic || method.IsAssembly)
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)];

        Assert.Equal(["AddComment"], names.Where(static name => name.Contains("Comment", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(["AddEvidence"], names.Where(static name => name.Contains("Evidence", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(names, static name => name.Contains("Timeline", StringComparison.OrdinalIgnoreCase) || name.Contains("Entry", StringComparison.OrdinalIgnoreCase));
    }
}
