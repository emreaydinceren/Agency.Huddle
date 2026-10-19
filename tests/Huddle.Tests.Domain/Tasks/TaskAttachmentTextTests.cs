using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>The description line that attaches a Library file to a Task (TaskAttachments spec 6.4).</summary>
public sealed class TaskAttachmentTextTests
{
    private const string Path = @"D:\Huddle\Teams\Growth\plan.md";

    /// <summary>An empty description becomes just the path line.</summary>
    [Fact]
    public void AppendPathLine_EmptyDescription_IsTheLine()
    {
        Assert.Equal($"`{Path}`", TaskAttachmentText.AppendPathLine(string.Empty, Path));
    }

    /// <summary>The first path line after prose is separated from it by a blank line, whether or not the prose ends in a newline.</summary>
    [Theory]
    [InlineData("Write the plan")]
    [InlineData("Write the plan\n")]
    public void AppendPathLine_AfterProse_AddsBlankLine(string description)
    {
        Assert.Equal($"Write the plan\n\n`{Path}`", TaskAttachmentText.AppendPathLine(description, Path));
    }

    /// <summary>A second attachment follows the first directly, so a run of attachments is one block.</summary>
    [Fact]
    public void AppendPathLine_AfterPathLine_AddsNoBlankLine()
    {
        string once = TaskAttachmentText.AppendPathLine("Notes", Path);

        string twice = TaskAttachmentText.AppendPathLine(once, @"D:\Huddle\Uploads\Tasks\GROW-0002\brief.pdf");

        Assert.Equal($"Notes\n\n`{Path}`\n`D:\\Huddle\\Uploads\\Tasks\\GROW-0002\\brief.pdf`", twice);
    }

    /// <summary>A path already on a line of its own is not added again.</summary>
    [Fact]
    public void AppendPathLine_DuplicatePath_LeavesDescriptionUnchanged()
    {
        string once = TaskAttachmentText.AppendPathLine("Notes", Path);

        Assert.Equal(once, TaskAttachmentText.AppendPathLine(once, Path));
    }

    /// <summary>A path with spaces stays one code span that the Library's whole-span rule accepts.</summary>
    [Fact]
    public void AppendPathLine_PathWithSpaces_IsOneMatchableCodeSpan()
    {
        string line = TaskAttachmentText.AppendPathLine(string.Empty, @"D:\Huddle\Uploads\Tasks\GROW-0002\my brief (2).pdf");

        Assert.True(LibraryPathPatterns.TryMatchWhole(line.Trim('`'), out string? path));
        Assert.Equal(@"D:\Huddle\Uploads\Tasks\GROW-0002\my brief (2).pdf", path);
    }
}
