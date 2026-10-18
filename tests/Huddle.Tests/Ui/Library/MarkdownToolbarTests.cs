using Agency.Huddle.App.Components.Library;
using Bunit;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <see cref="MarkdownToolbar"/>'s markup, the half <c>library-editor.js</c> (<c>attachToolbar</c>) relies on:
/// the <c>data-md-toolbar</c> marker it finds the row by, a <c>data-md-command</c> on every button, the WAI-ARIA
/// toolbar role and the roving tabindex. The click behaviour itself runs in the browser and is pinned by the
/// Node tests of the pure command logic (<see cref="MarkdownCommandsScriptTests"/>).
/// </summary>
public sealed class MarkdownToolbarTests
{
    /// <summary>The row is a labelled horizontal toolbar that the editor script can find by its <c>data-md-toolbar</c> marker.</summary>
    [Fact]
    public async Task Render_IsALabelledToolbarTheEditorCanFind()
    {
        await using MudBunitContext context = new();

        IRenderedComponent<MarkdownToolbar> cut = context.Render<MarkdownToolbar>();

        AngleSharp.Dom.IElement row = cut.Find("[data-md-toolbar]");
        Assert.Equal("toolbar", row.GetAttribute("role"));
        Assert.Equal("Markdown formatting", row.GetAttribute("aria-label"));
        Assert.Equal("horizontal", row.GetAttribute("aria-orientation"));
    }

    /// <summary>Every command the script understands has exactly one button carrying its name in <c>data-md-command</c>, each with an <c>aria-label</c>.</summary>
    [Fact]
    public async Task Render_EveryCommandHasOneLabelledButton()
    {
        await using MudBunitContext context = new();

        IRenderedComponent<MarkdownToolbar> cut = context.Render<MarkdownToolbar>();

        string[] commands = [.. cut.FindAll("[data-md-command]").Select(static b => b.GetAttribute("data-md-command") ?? "")];
        Assert.Equal(
            [
                MarkdownToolbar.Commands.Bold,
                MarkdownToolbar.Commands.Italic,
                MarkdownToolbar.Commands.Heading,
                MarkdownToolbar.Commands.Code,
                MarkdownToolbar.Commands.BulletList,
                MarkdownToolbar.Commands.TaskList,
                MarkdownToolbar.Commands.Wikilink,
            ],
            commands);
        Assert.All(cut.FindAll("[data-md-command]"), static b => Assert.False(string.IsNullOrWhiteSpace(b.GetAttribute("aria-label"))));
    }

    /// <summary>The row is one tab stop: only the first button is tabbable and every other is <c>tabindex="-1"</c>, so Tab passes through once and the arrow keys move within.</summary>
    [Fact]
    public async Task Render_HasOneTabStop()
    {
        await using MudBunitContext context = new();

        IRenderedComponent<MarkdownToolbar> cut = context.Render<MarkdownToolbar>();

        string?[] tabIndexes = [.. cut.FindAll("[data-md-command]").Select(static b => b.GetAttribute("tabindex"))];
        Assert.Equal("0", tabIndexes[0]);
        Assert.All(tabIndexes[1..], static t => Assert.Equal("-1", t));
    }

    /// <summary>The Bold, Italic and Code tooltips name their shortcut, as the editor's keymap binds them.</summary>
    [Fact]
    public async Task Render_TooltipsNameTheShortcuts()
    {
        await using MudBunitContext context = new();

        IRenderedComponent<MarkdownToolbar> cut = context.Render<MarkdownToolbar>();

        string[] tooltips = [.. cut.FindComponents<MudBlazor.MudTooltip>().Select(static t => t.Instance.Text ?? "")];
        Assert.Equal(["Bold (Ctrl+B)", "Italic (Ctrl+I)", "Heading", "Code (Ctrl+E)", "Bulleted list", "Task list", "Link to a note"], tooltips);
    }
}
