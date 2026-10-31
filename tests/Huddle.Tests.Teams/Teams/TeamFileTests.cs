using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Exercises <see cref="TeamFile"/> parsing and composing (Spec §TeamFile).</summary>
public sealed class TeamFileTests
{
    private const string SpecExample = "---\n"
        + "teamType: 'development@1.0.0'\n"
        + "manager: 'Joe'\n"
        + "workspace: 'E:\\Repos\\WebApp'\n"
        + "owns: '/web, the React front end, the design system'\n"
        + "process: 'Process.md'\n"
        + "---\n";

    /// <summary>The Spec example parses to its fields and composes back to the same text.</summary>
    [Fact]
    public void SpecExample_RoundTrips()
    {
        bool ok = TeamFile.TryParse(SpecExample, out TeamFile? file);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Equal(new VersionedRef("development", "1.0.0"), file.TeamType);
        Assert.Equal("Joe", file.Manager);
        Assert.Equal("E:\\Repos\\WebApp", file.Workspace);
        Assert.Equal("/web, the React front end, the design system", file.Owns);
        Assert.Equal("Process.md", file.Process);
        Assert.Equal(string.Empty, file.Notes);
        Assert.Equal(SpecExample, file.Compose());
    }

    /// <summary>A Windows path with backslashes is not treated as an escape sequence.</summary>
    [Fact]
    public void Workspace_WindowsPath_KeepsBackslashes()
    {
        TeamFile file = new(new VersionedRef("development", "1.0.0"), null, "C:\\a\\b c\\d", null, "Process.md", string.Empty);

        bool ok = TeamFile.TryParse(file.Compose(), out TeamFile? parsed);

        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal("C:\\a\\b c\\d", parsed.Workspace);
        Assert.Null(parsed.Manager);
    }

    /// <summary>An apostrophe in a value is doubled on write and restored on read.</summary>
    [Fact]
    public void Owns_Apostrophe_IsDoubledAndRestored()
    {
        TeamFile file = new(new VersionedRef("development", "1.0.0"), "Joe", null, "the team's front end", "Process.md", string.Empty);

        string text = file.Compose();
        bool ok = TeamFile.TryParse(text, out TeamFile? parsed);

        Assert.Equal("---\nteamType: 'development@1.0.0'\nmanager: 'Joe'\nowns: 'the team''s front end'\nprocess: 'Process.md'\n---\n", text);
        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal("the team's front end", parsed.Owns);
    }

    /// <summary>A file without a teamType, or without frontmatter, does not parse.</summary>
    [Theory]
    [InlineData("---\nmanager: 'Joe'\n---\n")]
    [InlineData("---\nteamType: 'nonsense'\n---\n")]
    [InlineData("manager: 'Joe'\n")]
    [InlineData("")]
    public void TryParse_MissingTeamType_Fails(string text)
    {
        bool ok = TeamFile.TryParse(text, out TeamFile? file);

        Assert.False(ok);
        Assert.Null(file);
    }

    /// <summary>An unknown key survives a parse and compose, in its original position among the unknown keys.</summary>
    [Fact]
    public void UnknownKeys_SurviveRewrite()
    {
        string text = "---\nteamType: 'development@1.0.0'\nreviewer: 'Ann'\nmanager: 'Joe'\nlabel: 'x: y'\n---\nHand-written notes.\n";

        bool ok = TeamFile.TryParse(text, out TeamFile? file);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Equal("Hand-written notes.\n", file.Notes);
        Assert.Equal(
            "---\nteamType: 'development@1.0.0'\nmanager: 'Joe'\nprocess: 'Process.md'\nreviewer: 'Ann'\nlabel: 'x: y'\n---\nHand-written notes.\n",
            file.Compose());
    }

    /// <summary>CRLF text parses, and a missing process key defaults to Process.md.</summary>
    [Fact]
    public void TryParse_CrlfAndNoProcess_DefaultsProcess()
    {
        bool ok = TeamFile.TryParse("---\r\nteamType: development@1.0.0\r\n---\r\nBody\r\n", out TeamFile? file);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Equal("Process.md", file.Process);
        Assert.Equal("Body\n", file.Notes);
        Assert.DoesNotContain('\r', file.Compose());
    }

    /// <summary>The three avatar keys round-trip: composed after the process key, parsed back into the same <c>Avatar</c>.</summary>
    [Fact]
    public void Avatar_AllThreeKeys_RoundTrip()
    {
        TeamFile file = new(new VersionedRef("development", "1.0.0"), null, null, null, "Process.md", string.Empty)
        {
            Avatar = new Avatar("GR", "0123456789abcdef0123456789abcdef.png", "#1E90FF"),
        };

        string text = file.Compose();
        bool ok = TeamFile.TryParse(text, out TeamFile? parsed);

        Assert.Equal(
            "---\nteamType: 'development@1.0.0'\nprocess: 'Process.md'\navatarLabel: 'GR'\navatarImage: '0123456789abcdef0123456789abcdef.png'\navatarBackground: '#1E90FF'\n---\n",
            text);
        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal(file.Avatar, parsed.Avatar);
    }

    /// <summary>A Team with no avatar writes none of the three keys, so an unedited file stays as it was.</summary>
    [Fact]
    public void Avatar_None_WritesNoKeys()
    {
        TeamFile file = new(new VersionedRef("development", "1.0.0"), "Joe", null, null, "Process.md", string.Empty);

        string text = file.Compose();

        Assert.Equal("---\nteamType: 'development@1.0.0'\nmanager: 'Joe'\nprocess: 'Process.md'\n---\n", text);
        Assert.Equal(Avatar.None, file.Avatar);
    }

    /// <summary>A file written before Teams had avatars parses with <c>Avatar.None</c>.</summary>
    [Fact]
    public void Avatar_OldFileWithoutTheKeys_ParsesAsNone()
    {
        bool ok = TeamFile.TryParse(SpecExample, out TeamFile? file);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Equal(Avatar.None, file.Avatar);
    }

    /// <summary>Only the keys that are set are written, and a label alone round-trips without inventing an image or a colour.</summary>
    [Fact]
    public void Avatar_LabelOnly_WritesOnlyTheLabel()
    {
        TeamFile file = new(new VersionedRef("development", "1.0.0"), null, null, null, "Process.md", string.Empty)
        {
            Avatar = new Avatar("QA", null, null),
        };

        string text = file.Compose();
        bool ok = TeamFile.TryParse(text, out TeamFile? parsed);

        Assert.Equal("---\nteamType: 'development@1.0.0'\nprocess: 'Process.md'\navatarLabel: 'QA'\n---\n", text);
        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal(new Avatar("QA", null, null), parsed.Avatar);
    }

    /// <summary>A hand-edited file cannot smuggle a path into the image key or a non-colour into the background: both are dropped on read.</summary>
    [Fact]
    public void Avatar_HandEditedImagePathAndBadColour_AreDropped()
    {
        string text = "---\nteamType: 'development@1.0.0'\navatarImage: '..\\..\\secret.png'\navatarBackground: 'red; position: fixed'\navatarLabel: 'OK'\n---\n";

        bool ok = TeamFile.TryParse(text, out TeamFile? file);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Equal(new Avatar("OK", null, null), file.Avatar);
    }

    /// <summary>The avatar keys are known keys: they are read into <c>Avatar</c> and not kept a second time as unknown lines.</summary>
    [Fact]
    public void Avatar_Keys_AreNotUnknownLines()
    {
        bool ok = TeamFile.TryParse("---\nteamType: 'development@1.0.0'\navatarLabel: 'GR'\n---\n", out TeamFile? file);

        Assert.True(ok);
        Assert.NotNull(file);
        Assert.Empty(file.UnknownLines);
    }
}
