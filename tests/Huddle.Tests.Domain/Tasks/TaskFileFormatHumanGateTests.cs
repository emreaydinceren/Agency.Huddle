using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for the <c>human_gate</c> and <c>action</c> frontmatter keys in <see cref="TaskFileFormat"/> (Human gate Spec §6.1).</summary>
public sealed class TaskFileFormatHumanGateTests
{
    private static readonly TaskLocation DefaultLocation = new("Platform", null, false);

    /// <summary>A minimal Task file with the given extra frontmatter lines placed after <c>sprint</c>-position, before the description.</summary>
    /// <param name="extraLines">The extra frontmatter lines.</param>
    private static string FileText(params string[] extraLines)
    {
        List<string> lines =
        [
            "---",
            "id: PLAT-0042",
            "title: 'Reply to Acme'",
            "status: In Progress",
            "priority: Urgent",
            "creator: Emre",
            .. extraLines,
            "---",
            "Body text.",
            "",
            "## Change log",
            "- 2026-09-24T11:20:00Z | Emre | created",
        ];
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Parses <paramref name="text"/>, asserting success.</summary>
    /// <param name="text">The Task file text.</param>
    private static TaskItem ParseOk(string text)
    {
        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result, error);
        Assert.NotNull(task);
        return task;
    }

    /// <summary><c>human_gate: true</c> sets <see cref="TaskItem.HumanGate"/> and gates the Task.</summary>
    [Fact]
    public void Parse_HumanGateTrue_SetsHumanGate()
    {
        TaskItem task = ParseOk(FileText("human_gate: true"));

        Assert.True(task.HumanGate);
        Assert.True(task.IsGated);
    }

    /// <summary><c>human_gate: false</c> reads as absent.</summary>
    [Fact]
    public void Parse_HumanGateFalse_ReadsAsAbsent()
    {
        TaskItem task = ParseOk(FileText("human_gate: false"));

        Assert.False(task.HumanGate);
        Assert.False(task.IsGated);
    }

    /// <summary>An <c>action</c> alone gates the Task: <see cref="TaskItem.IsGated"/> is true with no <c>human_gate</c>.</summary>
    [Fact]
    public void Parse_Action_SetsActionAndGates()
    {
        TaskItem task = ParseOk(FileText("action: 'Send reply to Acme about the renewal'"));

        Assert.Equal("Send reply to Acme about the renewal", task.Action);
        Assert.False(task.HumanGate);
        Assert.True(task.IsGated);
    }

    /// <summary>An <c>action</c> longer than 200 characters refuses the file, and the error names <c>action</c>.</summary>
    [Fact]
    public void Parse_ActionOver200Chars_IsRefused()
    {
        string text = FileText("action: " + new string('x', 201));

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("action", error, StringComparison.Ordinal);
    }

    /// <summary>An <c>action</c> of exactly 200 characters is accepted.</summary>
    [Fact]
    public void Parse_ActionOf200Chars_IsAccepted()
    {
        TaskItem task = ParseOk(FileText("action: " + new string('x', 200)));

        Assert.Equal(200, task.Action?.Length);
    }

    /// <summary>A Task with <see cref="TaskItem.HumanGate"/> false writes no <c>human_gate</c> line.</summary>
    [Fact]
    public void Compose_HumanGateFalse_WritesNoKey()
    {
        TaskItem task = TestTasks.Make() with { HumanGate = false };

        string composed = TaskFileFormat.Compose(task);

        Assert.DoesNotContain("human_gate", composed, StringComparison.Ordinal);
    }

    /// <summary>A gated Task writes <c>human_gate: true</c> after <c>sprint</c> and before the description.</summary>
    [Fact]
    public void Compose_HumanGateTrue_SitsAfterSprint()
    {
        TaskItem task = TestTasks.Make(sprint: new SprintId(2)) with { HumanGate = true };

        string[] lines = TaskFileFormat.Compose(task).Split('\n');

        int sprintIndex = Array.IndexOf(lines, "sprint: S2");
        Assert.True(sprintIndex >= 0);
        Assert.Equal("human_gate: true", lines[sprintIndex + 1]);
    }

    /// <summary>An action with quotes, an apostrophe and a colon survives compose then parse.</summary>
    [Fact]
    public void Compose_Action_RoundTrips()
    {
        const string action = "Send \"final\" reply: it's Acme's renewal";
        TaskItem task = TestTasks.Make() with { Action = action, HumanGate = true };

        string composed = TaskFileFormat.Compose(task);
        TaskItem parsed = ParseOk(composed);

        Assert.Equal(action, parsed.Action);
        Assert.True(parsed.HumanGate);
    }

    /// <summary>Neither <c>human_gate</c> nor <c>action</c> lands in <see cref="TaskItem.UnknownFields"/>; a truly unknown key still does.</summary>
    [Fact]
    public void HumanGateAndAction_AreNotUnknownFields()
    {
        TaskItem task = ParseOk(FileText("human_gate: true", "action: 'Do it'", "estimate: 3"));

        KeyValuePair<string, string> unknown = Assert.Single(task.UnknownFields);
        Assert.Equal("estimate", unknown.Key);
    }
}
