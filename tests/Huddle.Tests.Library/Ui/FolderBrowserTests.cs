using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins <see cref="FolderBrowser"/>, the listing behind the folder picker: sub-folders in name order, a parent, and a refusal instead of an exception for a path that is not a readable folder.</summary>
public sealed class FolderBrowserTests
{
    /// <summary>A folder lists its sub-folders ordered by name ignoring case, with full paths, its own full path and its parent.</summary>
    [Fact]
    public void List_Folder_ReturnsSubFoldersInNameOrderWithParent()
    {
        using TempDataDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "beta"));
        Directory.CreateDirectory(Path.Combine(dir.Path, "Alpha"));
        File.WriteAllText(Path.Combine(dir.Path, "file.txt"), "not a folder");

        FolderListing listing = FolderBrowser.List(dir.Path);

        Assert.Null(listing.Error);
        Assert.Equal(Path.GetFullPath(dir.Path), listing.Current);
        Assert.Equal(Directory.GetParent(dir.Path)?.FullName, listing.Parent);
        Assert.Equal(["Alpha", "beta"], listing.Folders.Select(folder => folder.Name));
        Assert.Equal([Path.Combine(dir.Path, "Alpha"), Path.Combine(dir.Path, "beta")], listing.Folders.Select(folder => folder.Path));
        Assert.True(listing.CanSelect);
        Assert.False(listing.Truncated);
    }

    /// <summary>A path that does not exist, and a path that is a file, are refused with the same words and cannot be selected.</summary>
    [Fact]
    public void List_MissingOrFile_IsRefused()
    {
        using TempDataDir dir = new();
        string file = Path.Combine(dir.Path, "file.txt");
        File.WriteAllText(file, "x");

        FolderListing missing = FolderBrowser.List(Path.Combine(dir.Path, "nowhere"));
        FolderListing notAFolder = FolderBrowser.List(file);

        Assert.Equal("That folder does not exist.", missing.Error);
        Assert.Equal("That folder does not exist.", notAFolder.Error);
        Assert.False(missing.CanSelect);
        Assert.Empty(missing.Folders);
    }

    /// <summary>A blank path is the top level: nothing is selectable there, and there is somewhere to go (the drives, or the root folder).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void List_Blank_IsTheTopLevel(string? path)
    {
        FolderListing listing = FolderBrowser.List(path);

        Assert.Null(listing.Error);
        Assert.NotEmpty(listing.Folders);
        if (OperatingSystem.IsWindows())
        {
            Assert.Null(listing.Current);
            Assert.False(listing.CanSelect);
        }
    }

    /// <summary>A folder with more sub-folders than the cap lists the cap and says it stopped.</summary>
    [Fact]
    public void List_MoreThanTheCap_IsTruncated()
    {
        using TempDataDir dir = new();
        for (int i = 0; i <= FolderBrowser.MaxFolders; i++)
        {
            Directory.CreateDirectory(Path.Combine(dir.Path, $"d{i:D4}"));
        }

        FolderListing listing = FolderBrowser.List(dir.Path);

        Assert.Equal(FolderBrowser.MaxFolders, listing.Folders.Count);
        Assert.True(listing.Truncated);
    }

    /// <summary>On Windows a drive root has no parent folder, so Up from it reaches the top level.</summary>
    [Fact]
    public void List_DriveRoot_HasNoParent()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Null(FolderBrowser.List("/").Parent);
            return;
        }

        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

        Assert.Null(FolderBrowser.List(root).Parent);
    }
}
