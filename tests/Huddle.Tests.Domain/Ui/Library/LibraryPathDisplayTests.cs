using Agency.Huddle.App.Components.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>Pins how the document toolbar shortens a long full system path (<see cref="LibraryPathDisplay"/>).</summary>
public sealed class LibraryPathDisplayTests
{
    /// <summary>A path of exactly the limit is shown whole.</summary>
    [Fact]
    public void Shorten_AtTheLimit_IsUnchanged()
    {
        string path = @"C:\Notes\" + new string('a', LibraryPathDisplay.MaxLength - 12) + ".md";

        Assert.Equal(LibraryPathDisplay.MaxLength, path.Length);
        Assert.Equal(path, LibraryPathDisplay.Shorten(path));
    }

    /// <summary>A longer path keeps its drive and first folder, then an ellipsis and the file name.</summary>
    [Fact]
    public void Shorten_LongWindowsPath_KeepsStartAndFileName()
    {
        const string Path = @"C:\Users\user\AppData\Local\Temp\seeds\data\Teams\Growth\Lifecycle Emails\Email-Ideas.md";

        Assert.Equal(@"C:\Users\…\Email-Ideas.md", LibraryPathDisplay.Shorten(Path));
    }

    /// <summary>A long forward-slash path is shortened the same way, with its own separator.</summary>
    [Fact]
    public void Shorten_LongUnixPath_UsesItsOwnSeparator()
    {
        const string Path = "/home/emre/huddle/data/Teams/Growth/Lifecycle Emails/Drafts/Email-Ideas.md";

        Assert.Equal("/home/…/Email-Ideas.md", LibraryPathDisplay.Shorten(Path));
    }

    /// <summary>A long path with no folder between its start and its file name has nothing to cut and is shown whole.</summary>
    [Fact]
    public void Shorten_LongButShallow_IsUnchanged()
    {
        string path = @"C:\Notes\" + new string('a', 80) + ".md";

        Assert.Equal(path, LibraryPathDisplay.Shorten(path));
    }
}
