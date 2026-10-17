using System.Diagnostics;
using System.Text.Json;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Runs <c>teamComposer.attach</c> and <c>teamComposer.send</c> from the real <c>app.js</c> under
/// <c>node</c> against a hand-made DOM (<c>composer-attach-harness.mjs</c>), so paste, drop and the
/// held send are tested as behaviour. Needs <c>node</c> on PATH, like the other node-backed tests.
/// </summary>
public sealed class ComposerAttachJsTests
{
    private static JsonElement Run(string scenario)
    {
        string script = CssSource.RepoPath("tests", "Huddle.Tests", "Ui", "Composer", "composer-attach-harness.mjs");
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
        return JsonDocument.Parse(output).RootElement.Clone();
    }

    /// <summary>A paste carrying files puts them in the hidden input, fires <c>change</c> and stops the browser default (spec 6.5).</summary>
    [Fact]
    public void Paste_WithFiles_AssignsInputAndDispatchesChange()
    {
        JsonElement result = Run("paste-files");

        Assert.True(result.GetProperty("prevented").GetBoolean());
        Assert.Equal(1, result.GetProperty("assigned").GetInt32());
        Assert.Equal("image.png", result.GetProperty("first").GetString());
        Assert.Equal(1, result.GetProperty("changes").GetInt32());
    }

    /// <summary>A paste with no files is left to the browser: no prevent, no change (spec 6.5).</summary>
    [Fact]
    public void Paste_WithoutFiles_IsUntouched()
    {
        JsonElement result = Run("paste-text");

        Assert.False(result.GetProperty("prevented").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("assigned").ValueKind);
        Assert.Equal(0, result.GetProperty("changes").GetInt32());
    }

    /// <summary>Dragging files over the box marks it dropping, leaving clears it, and a drop assigns the files, fires <c>change</c> and clears the mark (spec 6.5).</summary>
    [Fact]
    public void DragOverLeaveAndDrop_MarkTheBoxAndAttachTheFiles()
    {
        JsonElement result = Run("drag-drop");

        Assert.True(result.GetProperty("overPrevented").GetBoolean());
        Assert.True(result.GetProperty("droppingDuring").GetBoolean());
        Assert.False(result.GetProperty("droppingAfterLeave").GetBoolean());
        Assert.True(result.GetProperty("dropPrevented").GetBoolean());
        Assert.False(result.GetProperty("droppingAfterDrop").GetBoolean());
        Assert.Equal(1, result.GetProperty("assigned").GetInt32());
        Assert.Equal(1, result.GetProperty("changes").GetInt32());
    }

    /// <summary>Dragging text (no files) over the box does nothing: no hint, no prevent, no change.</summary>
    [Fact]
    public void DragOfText_IsIgnored()
    {
        JsonElement result = Run("drag-text");

        Assert.False(result.GetProperty("overPrevented").GetBoolean());
        Assert.False(result.GetProperty("dropping").GetBoolean());
        Assert.False(result.GetProperty("dropPrevented").GetBoolean());
        Assert.Equal(0, result.GetProperty("changes").GetInt32());
    }

    /// <summary>A dragleave into a child of the box keeps the drop hint, so it does not flicker.</summary>
    [Fact]
    public void DragLeave_IntoAChild_KeepsTheHint()
    {
        JsonElement result = Run("drag-inner-leave");

        Assert.True(result.GetProperty("dropping").GetBoolean());
    }

    /// <summary>
    /// With no file input (Library off) a dragged or dropped file is still cancelled so the browser does not
    /// navigate to it (spec U20), without a drop hint; text drags and pastes are left alone.
    /// </summary>
    [Theory]
    [InlineData("no-host")]
    [InlineData("no-input")]
    public void Attach_WithoutAFileInput_SwallowsFileDragsOnlyAndShowsNoHint(string scenario)
    {
        JsonElement result = Run(scenario);

        Assert.True(result.GetProperty("overPrevented").GetBoolean());
        Assert.True(result.GetProperty("dropPrevented").GetBoolean());
        Assert.False(result.GetProperty("dropping").GetBoolean());
        Assert.False(result.GetProperty("textOverPrevented").GetBoolean());
        Assert.False(result.GetProperty("textDropPrevented").GetBoolean());
        Assert.False(result.GetProperty("pastePrevented").GetBoolean());
        Assert.Equal(1, result.GetProperty("keydown").GetInt32());
    }

    /// <summary>The file input is looked up when the paste or drop happens, so an input the picker replaced after attach still receives the files.</summary>
    [Theory]
    [InlineData("paste-input-replaced")]
    [InlineData("drop-input-replaced")]
    public void PasteOrDrop_AfterTheInputWasReplaced_UsesTheCurrentInput(string scenario)
    {
        JsonElement result = Run(scenario);

        Assert.Equal(0, result.GetProperty("oldChanges").GetInt32());
        Assert.Equal(1, result.GetProperty("newChanges").GetInt32());
        Assert.Equal(1, result.GetProperty("newAssigned").GetInt32());
    }

    /// <summary>
    /// Attaching observes the composer box for resizes, and a change of the box's width refits the textarea, so an empty
    /// Composer does not stay as tall as it was when the window was narrow; <c>detach</c> disconnects the observer.
    /// </summary>
    [Fact]
    public void Attach_ObservesTheBoxWidth_RefitsOnResize_AndDetachDisconnects()
    {
        JsonElement result = Run("resize-observer");

        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("observesBox").GetBoolean());
        Assert.Equal("102px", result.GetProperty("heightAfterResize").GetString());
        Assert.True(result.GetProperty("disconnected").GetBoolean());
    }

    /// <summary>A resize that leaves the width unchanged (the textarea's own height change) does not refit, so the observer cannot feed itself.</summary>
    [Fact]
    public void ResizeObserver_SameWidth_DoesNotRefit()
    {
        JsonElement result = Run("resize-same-width");

        Assert.Equal("999px", result.GetProperty("heightAfterResize").GetString());
    }

    /// <summary>When SendAsync reports it held the message (uploads pending) the box keeps its text; when it posted, the box clears.</summary>
    [Theory]
    [InlineData("send-held", "hello")]
    [InlineData("send-posted", "")]
    public void Send_ClearsTheBoxOnlyWhenTheComposerPosted(string scenario, string expected)
    {
        JsonElement result = Run(scenario);

        Assert.Equal(expected, result.GetProperty("value").GetString());
    }
}
