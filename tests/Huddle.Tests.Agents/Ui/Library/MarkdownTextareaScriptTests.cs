using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Behaviour tests for the Task Description's Markdown toolbar (<c>markdown-textarea.js</c>, read view spec D-7). Its pure half,
/// <c>applyPlan(text, selection, plan)</c>, turns the Library's <c>planCommand</c> plan into new text, a new selection and the one
/// span of the old text that differs, so the DOM step can make a single <c>insertText</c> edit. <c>markdown-textarea.harness.mjs</c>
/// runs it under Node and these tests assert on the outcome. Needs <c>node</c> on PATH, as <see cref="MarkdownCommandsScriptTests"/> does;
/// the DOM half (<c>insertText</c>, the <c>input</c> and <c>change</c> events, Ctrl+Z) is checked by hand.
/// </summary>
public sealed class MarkdownTextareaScriptTests
{
    /// <summary>One harness run shared by every test in the class: the scenarios are independent.</summary>
    private static readonly Lazy<Task<JsonElement>> Results = new(() => RunHarnessAsync(CancellationToken.None));

    /// <summary>Bold wraps the selection in <c>**</c> and the returned selection is the same text, now inside the markers.</summary>
    [Fact]
    public async Task Bold_Selection_IsWrappedAndStaysSelected()
    {
        Assert.Equal("**hello** world", await TextAsync("boldWrap"));
        Assert.Equal("2,7", await SelectionAsync("boldWrap"));
    }

    /// <summary>Bold on a caret inserts an empty pair with the caret between the markers.</summary>
    [Fact]
    public async Task Bold_Caret_InsertsAnEmptyPairWithTheCaretInside()
    {
        Assert.Equal("a****b", await TextAsync("boldCaret"));
        Assert.Equal("3,3", await SelectionAsync("boldCaret"));
    }

    /// <summary>Bold on an already wrapped selection removes the markers and keeps the text selected.</summary>
    [Fact]
    public async Task Bold_AlreadyWrapped_TogglesOff()
    {
        Assert.Equal("hello world", await TextAsync("boldOff"));
        Assert.Equal("0,5", await SelectionAsync("boldOff"));
    }

    /// <summary>Bulleted list prefixes every selected line, and only those, and a second press takes the prefixes off.</summary>
    [Fact]
    public async Task BulletedList_SelectedLines_GetThePrefixAndToggleOff()
    {
        Assert.Equal("- a\n- b\nc", await TextAsync("bulletLines"));
        Assert.Equal("a\nb", await TextAsync("bulletOff"));
    }

    /// <summary>A task list prefixes the lines that lack it and leaves the one that has it.</summary>
    [Fact]
    public async Task TaskList_MixedLines_PrefixesOnlyTheOnesWithoutIt()
    {
        Assert.Equal("- [ ] a\n- [ ] b", await TextAsync("taskListMixed"));
    }

    /// <summary>A heading on a caret at the start of the second line prefixes that line and keeps the caret after the prefix's start.</summary>
    [Fact]
    public async Task Heading_CaretAtLineStart_PrefixesThatLine()
    {
        Assert.Equal("one\n## two", await TextAsync("headingCaretAtLineStart"));
        Assert.Equal("4,4", await SelectionAsync("headingCaretAtLineStart"));
    }

    /// <summary>Code wraps the selection in backticks.</summary>
    [Fact]
    public async Task Code_Selection_IsWrappedInBackticks()
    {
        Assert.Equal("`run` it", await TextAsync("codeWrap"));
    }

    /// <summary>
    /// The span the DOM step replaces is enough on its own: replacing it in the old text gives the new text, for every scenario, and it
    /// covers only the touched lines for a list command (so an untouched line keeps its own undo history).
    /// </summary>
    [Theory]
    [InlineData("boldWrap")]
    [InlineData("boldCaret")]
    [InlineData("boldOff")]
    [InlineData("bulletLines")]
    [InlineData("bulletOff")]
    [InlineData("headingCaretAtLineStart")]
    [InlineData("taskListMixed")]
    [InlineData("codeWrap")]
    public async Task Span_ReplacingItInTheOldText_GivesTheNewText(string scenario)
    {
        JsonElement result = (await Results.Value).GetProperty(scenario);

        Assert.Equal(result.GetProperty("text").GetString(), result.GetProperty("spanText").GetString());
    }

    /// <summary>A heading on the second line of two touches only that line: the span starts after the first line.</summary>
    [Fact]
    public async Task Span_HeadingOnSecondLine_StartsAtThatLine()
    {
        JsonElement span = (await Results.Value).GetProperty("headingCaretAtLineStart").GetProperty("span");

        Assert.Equal(4, span[0].GetInt32());
        Assert.Equal("## ", span[2].GetString());
    }

    /// <summary>The new text a scenario produced.</summary>
    /// <param name="scenario">The harness scenario's name.</param>
    private static async Task<string> TextAsync(string scenario) =>
        (await Results.Value).GetProperty(scenario).GetProperty("text").GetString() ?? string.Empty;

    /// <summary>The new selection a scenario produced, as "start,end".</summary>
    /// <param name="scenario">The harness scenario's name.</param>
    private static async Task<string> SelectionAsync(string scenario) =>
        string.Join(',', (await Results.Value).GetProperty(scenario).GetProperty("selection").EnumerateArray().Select(static value => value.GetInt32()));

    /// <summary>Runs the Node harness once and parses the JSON it prints.</summary>
    /// <param name="ct">Cancels the run.</param>
    private static async Task<JsonElement> RunHarnessAsync(CancellationToken ct)
    {
        ProcessStartInfo info = new("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(RepoPaths.Data("markdown-textarea.harness.mjs"));
        info.ArgumentList.Add(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "markdown-textarea.js"));
        info.ArgumentList.Add(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "markdown-commands.js"));
        info.ArgumentList.Add(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "markdown-toolbar.js"));
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
        Task<string> error = process.StandardError.ReadToEndAsync(ct);
        Task<string> output = process.StandardOutput.ReadToEndAsync(ct);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("The markdown textarea harness failed: " + await error);
        }

        using JsonDocument document = JsonDocument.Parse(await output);
        return document.RootElement.Clone();
    }
}
