namespace Agency.Huddle.Tests.Acp.Sessions;

using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Pins the rules <see cref="TurnToolCalls"/> keeps for one Turn: which tool-call updates count,
/// what an Adapter command's outcome line is built from, and which paths File Changes treats as the
/// Agent's own.
/// </summary>
public sealed class TurnToolCallsTests
{
    private const string JsonObject = "{\"tokens\":12}";

    /// <summary>An update for a call this Turn never started is ignored, whatever it carries.</summary>
    [Fact]
    public void RecordResult_UpdateForUnknownCall_RecordsNothing()
    {
        TurnToolCalls calls = new();

        calls.RecordResult("earlier-turn", isStart: false, ToolCallStatus.Failed, TurnToolCallsTests.JsonObject);

        Assert.False(calls.ToolCallFailed);
        Assert.Null(calls.LastOutputJson);
        Assert.Empty(calls.ToolCallIds);
    }

    /// <summary>A start enrols the call, so a later update for it counts.</summary>
    [Fact]
    public void RecordResult_Start_EnrolsTheCall()
    {
        TurnToolCalls calls = new();

        calls.RecordResult("call-1", isStart: true, ToolCallStatus.Pending, null);
        calls.RecordResult("call-1", isStart: false, ToolCallStatus.Completed, TurnToolCallsTests.JsonObject);

        Assert.Contains("call-1", calls.ToolCallIds);
        Assert.Equal(TurnToolCallsTests.JsonObject, calls.LastOutputJson);
    }

    /// <summary>A Failed status sets the flag, on the start event and on an update of an enrolled call alike.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordResult_FailedStatus_SetsTheFlag(bool onStart)
    {
        TurnToolCalls calls = new();
        calls.RecordResult("call-1", isStart: true, onStart ? ToolCallStatus.Failed : ToolCallStatus.InProgress, null);

        if (!onStart)
        {
            calls.RecordResult("call-1", isStart: false, ToolCallStatus.Failed, null);
        }

        Assert.True(calls.ToolCallFailed);
    }

    /// <summary>Only an output that opens with a JSON object is kept, and a later plain output does not replace it.</summary>
    [Fact]
    public void RecordResult_Output_KeepsOnlyJsonObjects()
    {
        TurnToolCalls calls = new();
        calls.RecordResult("call-1", isStart: true, ToolCallStatus.InProgress, null);

        calls.RecordResult("call-1", isStart: false, ToolCallStatus.InProgress, "plain text");
        Assert.Null(calls.LastOutputJson);

        calls.RecordResult("call-1", isStart: false, ToolCallStatus.InProgress, "  " + TurnToolCallsTests.JsonObject);
        calls.RecordResult("call-1", isStart: false, ToolCallStatus.Completed, "[1,2]");

        Assert.Equal("  " + TurnToolCallsTests.JsonObject, calls.LastOutputJson);
    }

    /// <summary>An <see cref="ToolKind.Execute"/> call is not parsed for paths, even when its input holds one.</summary>
    [Fact]
    public void RecordTouched_Execute_RecordsNothing()
    {
        TurnToolCalls calls = new();

        calls.RecordTouched(ToolKind.Execute, TurnToolCallsTests.InputNaming("shell.txt"));

        Assert.Empty(calls.Touched);
    }

    /// <summary>An Edit, Delete or Move call adds the rooted path its input names.</summary>
    [Theory]
    [InlineData(ToolKind.Edit)]
    [InlineData(ToolKind.Delete)]
    [InlineData(ToolKind.Move)]
    public void RecordTouched_PathKind_AddsTheRootedPath(ToolKind kind)
    {
        TurnToolCalls calls = new();

        calls.RecordTouched(kind, TurnToolCallsTests.InputNaming("edited.txt"));

        Assert.Equal([TurnToolCallsTests.Rooted("edited.txt")], calls.Touched);
    }

    /// <summary>A path added directly, as the memory tool's file is, joins the same set.</summary>
    [Fact]
    public void AddTouched_AddsThePath()
    {
        TurnToolCalls calls = new();
        string path = TurnToolCallsTests.Rooted("memory.md");

        calls.AddTouched(path);

        Assert.Contains(path, calls.Touched);
    }

    private static string Rooted(string name) => Path.GetFullPath(Path.Combine(Path.GetTempPath(), name));

    private static string InputNaming(string name) => JsonSerializer.Serialize(new { file_path = TurnToolCallsTests.Rooted(name) });
}
