using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>The words of a Room's delete confirmation by upload count (ChatUploads spec 6.9 and 7).</summary>
public sealed class RoomDeleteCopyTests
{
    /// <summary>No uploads: the plain question; one: the singular sentence; several: the counted plural sentence.</summary>
    [Theory]
    [InlineData(0, "Delete \"Nova\"?")]
    [InlineData(1, "Delete \"Nova\"? Its uploaded file goes to the Recycle Bin.")]
    [InlineData(3, "Delete \"Nova\"? Its 3 uploaded files go to the Recycle Bin.")]
    public void Prompt_ByCount_UsesTheMatchingWording(int count, string expected)
    {
        Assert.Equal(expected, RoomDeleteCopy.Prompt("Nova", count));
    }
}
