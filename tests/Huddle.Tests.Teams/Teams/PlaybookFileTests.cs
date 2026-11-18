using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Exercises <see cref="PlaybookFile"/> parsing and composing (Team Playbooks spec §5.1).</summary>
public sealed class PlaybookFileTests
{
    /// <summary>A file with <c>basedOn</c> and a body reads both and composes back to the same text.</summary>
    [Fact]
    public void FullFile_RoundTrips()
    {
        const string Text = "---\nbasedOn: 'sha256:3f9a0c1e7b2d4a65'\n---\n## How to work on a Task\nDo one step.\n";

        PlaybookFile file = PlaybookFile.Parse(Text);

        Assert.Equal("sha256:3f9a0c1e7b2d4a65", file.BasedOn);
        Assert.Equal("## How to work on a Task\nDo one step.\n", file.Body);
        Assert.Empty(file.UnknownLines);
        Assert.Equal(Text, file.Compose());
    }

    /// <summary>A file made by hand with no frontmatter has no base (Unknown base) and is all body; it composes back unchanged.</summary>
    [Fact]
    public void NoFrontmatter_IsUnknownBase_AndAllBody()
    {
        const string Text = "## Our way\nShip on Fridays.\n";

        PlaybookFile file = PlaybookFile.Parse(Text);

        Assert.Null(file.BasedOn);
        Assert.Equal(Text, file.Body);
        Assert.Equal(Text, file.Compose());
    }

    /// <summary>Frontmatter lines the type does not know are kept verbatim, in order, and written back.</summary>
    [Fact]
    public void UnknownLines_AreKeptVerbatim()
    {
        const string Text = "---\nowner: 'Ada'\nbasedOn: 'sha256:3f9a0c1e7b2d4a65'\nreviewed: 2026-11-01\n---\nBody\n";

        PlaybookFile file = PlaybookFile.Parse(Text);

        Assert.Equal(["owner: 'Ada'", "reviewed: 2026-11-01"], file.UnknownLines);
        string composed = file.Compose();
        Assert.Contains("owner: 'Ada'\n", composed, StringComparison.Ordinal); // contains-ok: unknown lines are kept verbatim, wherever they sit in the front matter
        Assert.Contains("reviewed: 2026-11-01\n", composed, StringComparison.Ordinal); // contains-ok: unknown lines are kept verbatim, wherever they sit in the front matter
        PlaybookFile again = PlaybookFile.Parse(composed);
        Assert.Equal(file.BasedOn, again.BasedOn);
        Assert.Equal(file.Body, again.Body);
        Assert.Equal(file.UnknownLines, again.UnknownLines);
    }

    /// <summary>The frontmatter never reaches the body, whatever the line endings.</summary>
    [Fact]
    public void Frontmatter_NeverInTheBody_WithCrLf()
    {
        const string Text = "---\r\nbasedOn: 'sha256:3f9a0c1e7b2d4a65'\r\nowner: 'Ada'\r\n---\r\nBody line\r\n";

        PlaybookFile file = PlaybookFile.Parse(Text);

        Assert.Equal("Body line\n", file.Body);
        Assert.DoesNotContain("basedOn", file.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("owner", file.Body, StringComparison.Ordinal);
    }

    /// <summary>The leading underscore of both file names keeps the catalog from reading them as Projects (spec §5.1).</summary>
    [Fact]
    public void FileNames_AreReservedProjectNames()
    {
        Assert.True(TeamNames.IsReservedProjectName("_member-playbook.md"));
        Assert.True(TeamNames.IsReservedProjectName("_manager-playbook.md"));
    }

    /// <summary>Frontmatter with no <c>basedOn</c> is Unknown base too, and an unclosed block is all body: <c>Parse</c> never fails.</summary>
    [Fact]
    public void MissingBaseOrUnclosedBlock_NeverFails()
    {
        PlaybookFile noBase = PlaybookFile.Parse("---\nowner: 'Ada'\n---\nBody\n");
        PlaybookFile unclosed = PlaybookFile.Parse("---\nbasedOn: 'sha256:3f9a0c1e7b2d4a65'\nBody\n");
        PlaybookFile empty = PlaybookFile.Parse(string.Empty);

        Assert.Null(noBase.BasedOn);
        Assert.Equal("Body\n", noBase.Body);
        Assert.Null(unclosed.BasedOn);
        Assert.Equal("---\nbasedOn: 'sha256:3f9a0c1e7b2d4a65'\nBody\n", unclosed.Body);
        Assert.Null(empty.BasedOn);
        Assert.Equal(string.Empty, empty.Body);
    }
}
