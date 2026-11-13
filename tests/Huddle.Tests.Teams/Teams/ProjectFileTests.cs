using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Exercises <see cref="ProjectFile"/> parsing and composing (ADR-0083).</summary>
public sealed class ProjectFileTests
{
    /// <summary>A file with a label, a background, another key and notes reads them all and composes back to the same text.</summary>
    [Fact]
    public void FullFile_RoundTrips()
    {
        const string Text = "---\navatarLabel: '🚀'\navatarBackground: '#336699'\nowner: 'Ada'\n---\nNotes\n";

        ProjectFile file = ProjectFile.Parse(Text);

        Assert.Equal(new Avatar("🚀", null, "#336699"), file.Avatar);
        Assert.Equal(["owner: 'Ada'"], file.UnknownLines);
        Assert.Equal("Notes\n", file.Notes);
        Assert.Equal(Text, file.Compose());
    }

    /// <summary>A file with no frontmatter is all notes and no avatar, and a rewrite puts the avatar in front of the text without losing it.</summary>
    [Fact]
    public void NoFrontmatter_IsAllNotes_AndARewriteKeepsThem()
    {
        ProjectFile file = ProjectFile.Parse("Just notes\nsecond line\n");

        Assert.Equal(Avatar.None, file.Avatar);
        Assert.Equal("---\navatarLabel: 'LA'\n---\nJust notes\nsecond line\n", (file with { Avatar = new Avatar("LA", null, null) }).Compose());
    }

    /// <summary>A frontmatter that never closes is not frontmatter: its lines stay notes and no avatar is read from them.</summary>
    [Fact]
    public void UnclosedFence_IsAllNotes()
    {
        ProjectFile file = ProjectFile.Parse("---\navatarLabel: 'X'\n");

        Assert.Equal(Avatar.None, file.Avatar);
        Assert.Equal("---\navatarLabel: 'X'\n", file.Notes);
    }

    /// <summary>CRLF input reads the same as LF, and the composed text is LF-only.</summary>
    [Fact]
    public void CrlfInput_ReadsAsLf_AndComposesLf()
    {
        ProjectFile file = ProjectFile.Parse("---\r\navatarLabel: 'AB'\r\n---\r\nNotes\r\n");

        Assert.Equal("AB", file.Avatar.Label);
        Assert.Equal("---\navatarLabel: 'AB'\n---\nNotes\n", file.Compose());
    }

    /// <summary>A label holding a single quote is written doubled, as YAML single-quoted scalars require, and reads back unchanged.</summary>
    [Fact]
    public void LabelWithAQuote_RoundTrips()
    {
        string text = (ProjectFile.Empty with { Avatar = new Avatar("O'K", null, null) }).Compose();

        Assert.Equal("---\navatarLabel: 'O''K'\n---\n", text);
        Assert.Equal("O'K", ProjectFile.Parse(text).Avatar.Label);
    }

    /// <summary>A Project's avatar never has a picture: an <c>avatarImage</c> line is not read as the image, and a background that is not a colour is dropped.</summary>
    [Fact]
    public void ImageLine_AndBadColour_AreNotRead()
    {
        ProjectFile file = ProjectFile.Parse("---\navatarImage: 'face.png'\navatarBackground: 'not a colour'\n---\n");

        Assert.Equal(Avatar.None, file.Avatar);
        Assert.Equal(["avatarImage: 'face.png'"], file.UnknownLines);
    }
}
