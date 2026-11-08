using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins the words a Turn detail row shows for a tool call (#484): Huddle's tools in plain words, files by name, no backticks.</summary>
public sealed class ToolActivityTextTests
{
    /// <summary>Huddle's own tools read as what the Agent is doing, never as their model-facing id.</summary>
    [Theory]
    [InlineData("mcp__team__ask_human", "Asking you")]
    [InlineData("mcp__team__create_task", "Creating a Task")]
    [InlineData("mcp__team__some_new_tool", "Some new tool")]
    public void Row_TeamTool_ReadsAsPlainWords(string title, string expected)
    {
        Assert.Equal(expected, ToolActivityText.Row(title, null));
    }

    /// <summary>Markdown backticks are stripped and an absolute path is cut to its name, in either separator.</summary>
    [Theory]
    [InlineData("Find `C:\\seeds\\huddle\\paid\\data\\Teams\\Growth` `**/*`", "Find Growth **/*")]
    [InlineData("Read C:\\seeds\\huddle\\paid\\data\\Teams\\Growth\\Process.md", "Read Process.md")]
    [InlineData("Read /home/nova/work/notes.md", "Read notes.md")]
    [InlineData("Read notes.md", "Read notes.md")]
    [InlineData("Run `dotnet test`", "Run dotnet test")]
    public void Row_Title_HasNoBackticksOrAbsolutePaths(string title, string expected)
    {
        Assert.Equal(expected, ToolActivityText.Row(title, null));
    }

    /// <summary>With no title the row names the file, and with neither it falls back to a plain label.</summary>
    [Fact]
    public void Row_WithoutTitle_NamesTheFileOrFallsBack()
    {
        Assert.Equal("notes.md", ToolActivityText.Row(null, "E:\\work\\notes.md"));
        Assert.Equal("Tool call", ToolActivityText.Row("  ", null));
    }

    /// <summary>The tooltip keeps what the row hides: the full path, else the raw title; a row that says it all has none.</summary>
    [Fact]
    public void Tooltip_KeepsWhatTheRowHides()
    {
        Assert.Equal("E:\\work\\notes.md", ToolActivityText.Tooltip("Read notes.md", "E:\\work\\notes.md"));
        Assert.Equal("mcp__team__ask_human", ToolActivityText.Tooltip("mcp__team__ask_human", null));
        Assert.Null(ToolActivityText.Tooltip("Read notes.md", null));
    }
}
