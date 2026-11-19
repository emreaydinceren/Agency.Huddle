using System.Diagnostics;
using System.Text.Json;

namespace Agency.Huddle.Tests.Ui.PointerDragScript;

/// <summary>
/// Runs <c>pointer-drag.js</c>, the drag and arrow-key step that <c>resize.js</c> and <c>task-list-resize.js</c>
/// share, under <c>node</c> against a hand-made handle (<c>pointer-drag-harness.mjs</c>). Needs <c>node</c> on PATH,
/// like the other node-backed tests.
/// </summary>
public sealed class PointerDragJsTests
{
    /// <summary>Starts node on the harness for one scenario and parses what it printed.</summary>
    /// <param name="scenario">The harness scenario to run.</param>
    /// <returns>The harness's JSON output.</returns>
    private static JsonElement Run(string scenario)
    {
        string script = RepoPaths.Data("pointer-drag-harness.mjs");
        ProcessStartInfo info = new("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(script);
        info.ArgumentList.Add(scenario);
        info.ArgumentList.Add(CssSource.RepoPath());
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return JsonElement.Parse(output);
    }

    /// <summary>ArrowLeft and ArrowRight step by the caller's step, Shift by its large step, and every other key by nothing.</summary>
    [Fact]
    public void ArrowStep_SignsTheCallersStepsAndIgnoresOtherKeys()
    {
        int[] steps = [.. Run("arrow-step").GetProperty("steps").EnumerateArray().Select(static step => step.GetInt32())];

        Assert.Equal([-10, 10, -40, 40, 0, 0], steps);
    }

    /// <summary>A drag captures the pointer, reports each move from the start divided by the scale, and ends once on pointerup, leaving no listener behind.</summary>
    [Fact]
    public void StartDrag_PointerUp_ReportsScaledMovesAndEndsOnce()
    {
        JsonElement result = Run("drag-up").GetProperty("result");

        Assert.Equal([20.0, -10.0], result.GetProperty("moves").EnumerateArray().Select(static move => move.GetDouble()));
        Assert.Equal(1, result.GetProperty("ends").GetInt32());
        Assert.True(result.GetProperty("prevented").GetBoolean());
        Assert.Equal(["capture 7", "release 7"], result.GetProperty("log").EnumerateArray().Select(static line => line.GetString()));
        Assert.Equal(0, result.GetProperty("listenersLeft").GetInt32());
    }

    /// <summary>A pointercancel ends the drag the same way, and the scale is read again on every move rather than once.</summary>
    [Fact]
    public void StartDrag_PointerCancel_EndsOnceAndReadsTheScalePerMove()
    {
        JsonElement result = Run("drag-cancel").GetProperty("result");

        Assert.Equal([40.0, -5.0], result.GetProperty("moves").EnumerateArray().Select(static move => move.GetDouble()));
        Assert.Equal(1, result.GetProperty("ends").GetInt32());
        Assert.Equal(0, result.GetProperty("listenersLeft").GetInt32());
    }
}
