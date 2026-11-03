using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Task 93 (11.2.t, SB-R12, Spec §6.3): <see cref="OutputsIndex"/> lists the Markdown documents under each Team's and Project's
/// <c>outputs/</c>, ordinal by path, Team by Team in the order given, capped across all of them. The Read when line is the file's frontmatter
/// <c>description</c> only (Spec §6.3 implementation notes, lines 388-390: "description from frontmatter only. A missing description lists the bare
/// relative path"). A path is relative to the Teams root, with <c>/</c> separators on every system.
/// </summary>
public sealed class OutputsIndexTests
{
    /// <summary>A description of 205 characters.</summary>
    private static readonly string LongDescription = new('d', 205);

    /// <summary>Every document the fixture holds is listed once: ordinal by path within A, then B, <c>y.md</c> by bare path, with the descriptions as written.</summary>
    [Fact]
    public void Build_TwoTeams_ListsOrdinalByPathWithinEachTeam()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A", "B"], 10);

        // Ordinal on the Teams-root-relative path: "A/Proj/..." sorts before "A/outputs/..." because 'P' (0x50) < 'o' (0x6F), and
        // "A/outputs/sub/y.md" before "A/outputs/z.md" because 's' < 'z'. B follows A because the Teams are taken in the order given.
        Assert.Equal(
            [
                ("A/Proj/outputs/x.md", "Read when X"),
                ("A/outputs/sub/y.md", null),
                ("A/outputs/z.md", "Read when Z"),
                ("B/outputs/w.md", "Read when W"),
            ],
            snapshot.Entries.Select(static entry => (entry.RelativePath, entry.Description)));
        Assert.Equal(0, snapshot.NotListed);
        Assert.Empty(snapshot.Overflows);
    }

    /// <summary>The order follows the Teams as given: B before A lists B's document first.</summary>
    [Fact]
    public void Build_TeamsGivenBFirst_ListsBFirst()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["B", "A"], 10);

        Assert.Equal("B/outputs/w.md", snapshot.Entries[0].RelativePath); // The first Team given is listed first.
        Assert.Equal(4, snapshot.Entries.Count);
    }

    /// <summary>A PDF is never listed, and neither is a document in a folder or of a name that starts with <c>_</c> or <c>.</c>.</summary>
    [Fact]
    public void Build_PdfAndUnderscoreAndDotNames_AreSkipped()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A", "B"], 10);

        string[] paths = [.. snapshot.Entries.Select(static entry => entry.RelativePath)];
        Assert.DoesNotContain(paths, static path => path.EndsWith(".pdf", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, static path => path.Contains("_drafts", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, static path => path.Contains(".hidden", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, static path => path.EndsWith("/_note.md", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, static path => path.EndsWith("/.secret.md", StringComparison.Ordinal));
    }

    /// <summary>A description over 200 characters is cut to 200 and ends with an ellipsis; one of exactly 200 is kept whole.</summary>
    [Fact]
    public void Build_LongDescription_ClipsAt200WithEllipsis()
    {
        using TempDataDir dir = new();
        Write(dir.Path, "A/outputs/long.md", $"---\ndescription: {LongDescription}\n---\nBody.\n");
        Write(dir.Path, "A/outputs/exact.md", $"---\ndescription: {new string('e', 200)}\n---\nBody.\n");

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A"], 10);

        Assert.Equal(new string('e', 200), snapshot.Entries.Single(static entry => entry.RelativePath.EndsWith("exact.md", StringComparison.Ordinal)).Description);
        Assert.Equal(new string('d', 200) + "…", snapshot.Entries.Single(static entry => entry.RelativePath.EndsWith("long.md", StringComparison.Ordinal)).Description); // 205 - 5 cut, plus the ellipsis.
    }

    /// <summary>A file with no description, or whose body only has a heading, is listed with a null description: the first body line is never used.</summary>
    [Fact]
    public void Build_NoFrontmatterDescription_GivesNullNotTheFirstBodyLine()
    {
        using TempDataDir dir = new();
        Write(dir.Path, "A/outputs/plain.md", "# A heading\n\nBody text.\n");
        Write(dir.Path, "A/outputs/empty-description.md", "---\ndescription:\n---\nBody.\n");

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A"], 10);

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.All(snapshot.Entries, static entry => Assert.Null(entry.Description));
    }

    /// <summary>With a cap of 3 the three documents of A are listed, B's is not, and B gets the overflow line naming <c>B/outputs</c> with a count of 1.</summary>
    [Fact]
    public void Build_CapOfThree_ListsAFirstAndReportsBOverflow()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A", "B"], 3);

        Assert.Equal(["A/Proj/outputs/x.md", "A/outputs/sub/y.md", "A/outputs/z.md"], snapshot.Entries.Select(static entry => entry.RelativePath));
        Assert.Equal(1, snapshot.NotListed); // Four documents - three listed.
        Assert.Equal([new OutputsOverflow("B/outputs", 1)], snapshot.Overflows); // The "1 more in B/outputs" line.
    }

    /// <summary>A cap that falls inside A leaves the rest of A's own folder to the overflow line and B's whole folder to its own.</summary>
    [Fact]
    public void Build_CapOfTwo_ReportsOverflowPerOutputsFolder()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A", "B"], 2);

        Assert.Equal(["A/Proj/outputs/x.md", "A/outputs/sub/y.md"], snapshot.Entries.Select(static entry => entry.RelativePath));
        Assert.Equal(2, snapshot.NotListed); // Four documents - two listed.
        Assert.Equal([new OutputsOverflow("A/outputs", 1), new OutputsOverflow("B/outputs", 1)], snapshot.Overflows); // z.md is left in A/outputs, w.md in B/outputs.
    }

    /// <summary>A cap of zero lists nothing and counts every document.</summary>
    [Fact]
    public void Build_CapOfZero_ListsNone()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A", "B"], 0);

        Assert.Empty(snapshot.Entries);
        Assert.Equal(4, snapshot.NotListed);
    }

    /// <summary>A Team with no <c>outputs/</c> folder, and a Team that does not exist, give an empty snapshot.</summary>
    [Fact]
    public void Build_NoOutputsFolderOrNoTeam_IsEmpty()
    {
        using TempDataDir dir = new();
        _ = Directory.CreateDirectory(Path.Combine(dir.Path, "A"));

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A", "Ghost"], 10);

        Assert.Empty(snapshot.Entries);
        Assert.Equal(0, snapshot.NotListed);
        Assert.Empty(snapshot.Overflows);
    }

    /// <summary>A Team named twice is listed once.</summary>
    [Fact]
    public void Build_TeamNamedTwice_ListsItOnce()
    {
        using TempDataDir dir = new();
        Fixture(dir.Path);

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["B", "B"], 10);

        Assert.Single(snapshot.Entries);
    }

    /// <summary>The Project folders' <c>memory</c> and <c>raw</c> are not Projects, so a Markdown file under <c>A/memory/outputs/</c> is not listed.</summary>
    [Fact]
    public void Build_OutputsUnderReservedFolder_IsNotListed()
    {
        using TempDataDir dir = new();
        Write(dir.Path, $"A/{TeamNames.MemoryFolder}/outputs/m.md", "---\ndescription: Not an output\n---\n");
        Write(dir.Path, $"A/{TeamNames.RawFolder}/outputs/r.md", "---\ndescription: Not an output\n---\n");

        OutputsSnapshot snapshot = OutputsIndex.Build(dir.Path, ["A"], 10);

        Assert.Empty(snapshot.Entries);
    }

    /// <summary>
    /// Writes the Spec fixture: A holds <c>outputs/z.md</c> (Read when Z), <c>outputs/sub/y.md</c> (no description), a PDF, a Project
    /// <c>Proj/outputs/x.md</c>, and skipped names; B holds <c>outputs/w.md</c>.
    /// </summary>
    /// <param name="root">The Teams root.</param>
    private static void Fixture(string root)
    {
        Write(root, "A/outputs/z.md", Described("Read when Z"));
        Write(root, "A/outputs/sub/y.md", "# Y\n\nNo description here.\n");
        Write(root, "A/outputs/report.pdf", "%PDF-1.4");
        Write(root, "A/Proj/outputs/x.md", Described("Read when X"));
        Write(root, "B/outputs/w.md", Described("Read when W"));
        Write(root, "A/outputs/_drafts/d.md", Described("Draft"));
        Write(root, "A/outputs/.hidden/h.md", Described("Hidden"));
        Write(root, "A/outputs/_note.md", Described("Underscore file"));
        Write(root, "A/outputs/.secret.md", Described("Dot file"));
    }

    /// <summary>Frontmatter holding a description, then a body.</summary>
    /// <param name="description">The description.</param>
    private static string Described(string description) => $"---\ndescription: {description}\n---\n# Title\n";

    /// <summary>Writes a file under the root, creating its folders.</summary>
    /// <param name="root">The Teams root.</param>
    /// <param name="relative">The <c>/</c>-separated path.</param>
    /// <param name="text">The file's text.</param>
    private static void Write(string root, string relative, string text)
    {
        string path = Path.Combine(root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllText(path, text);
    }
}
