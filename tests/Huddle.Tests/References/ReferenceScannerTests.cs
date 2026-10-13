using System.Globalization;
using Agency.Huddle.App.References;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins <see cref="ReferenceScanner.Scan"/>: which references a Message's text names, in order, and how many (spec §8.2 step 1, D-19).</summary>
public sealed class ReferenceScannerTests
{
    /// <summary>A stand-alone Task id in plain text is a Task reference.</summary>
    [Fact]
    public void Scan_TaskIdInText()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("Fixed GROW-0002 today.");

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.Task, "GROW-0002") }, Keys(found));
    }

    /// <summary>A Task id inside a code span is not a reference (Tasks spec §13.13.2 step 2).</summary>
    [Fact]
    public void Scan_TaskIdInCodeSpan_IsIgnored()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("see `GROW-0002`");

        Assert.Empty(found);
    }

    /// <summary>A lower-case id is not a Task reference.</summary>
    [Fact]
    public void Scan_LowerCaseId_IsIgnored()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("see grow-0002");

        Assert.Empty(found);
    }

    /// <summary>A Task id that is the text of a link is left to the link, not counted as a Task reference.</summary>
    [Fact]
    public void Scan_TaskIdInsideALink_IsIgnored()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("[GROW-0002](https://example.com)");

        Assert.Empty(found);
    }

    /// <summary>A Room link and a Team link are references, in the order they are written.</summary>
    [Fact]
    public void Scan_RoomAndTeamLinks()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("[Findings](/rooms/abc) and [Growth Ops](/teams/Growth%20Ops)");

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.Room, "abc"), (ReferenceKind.Team, "Growth Ops") }, Keys(found));
    }

    /// <summary>A link whose address is not one of the two admitted shapes is not a reference.</summary>
    [Fact]
    public void Scan_RefusedShapes_AreIgnored()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("[a](/rooms/x/y) [b](/rooms/a?x=1) [c](/teams/a%2Fb) [d](/teams/new)");

        Assert.Empty(found);
    }

    /// <summary>An image is not a reference, even when its address looks like a Room.</summary>
    [Fact]
    public void Scan_ImageLink_IsIgnored()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("![pic](/rooms/abc)");

        Assert.Empty(found);
    }

    /// <summary>A Library path with spaces in a code span is one Library file reference.</summary>
    [Fact]
    public void Scan_LibraryCodeSpanWithSpaces()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(@"Read `D:\Data\my plan.md` now");

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.LibraryFile, @"D:\Data\my plan.md") }, Keys(found));
    }

    /// <summary>A bare Windows path in text is a Library file reference, without the full stop that ends the sentence.</summary>
    [Fact]
    public void Scan_BarePath()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(@"Open D:\Data\plan.md.");

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.LibraryFile, @"D:\Data\plan.md") }, Keys(found));
    }

    /// <summary>A repeated reference is kept once, at its first place in the text.</summary>
    [Fact]
    public void Scan_Duplicates_KeptOnceInFirstSeenOrder()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("GROW-0002 GROW-0002 [a](/rooms/abc) [b](/rooms/abc)");

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.Task, "GROW-0002"), (ReferenceKind.Room, "abc") }, Keys(found));
    }

    /// <summary>Eleven Task ids are all kept: the cap of ten applies to what resolves, after the lookups (finding 2).</summary>
    [Fact]
    public void Scan_ElevenReferences_KeepsAllEleven()
    {
        string text = "GROW-0001 GROW-0002 GROW-0003 GROW-0004 GROW-0005 GROW-0006 GROW-0007 GROW-0008 GROW-0009 GROW-0010 GROW-0011";

        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(text);

        Assert.Equal(11, found.Count);
        Assert.Equal("GROW-0011", found[10].Key);
    }

    /// <summary>The scan stops at the hard ceiling on candidates, so a pasted wall of ids is bounded.</summary>
    [Fact]
    public void Scan_SixtyIds_StopsAtTheCeiling()
    {
        string text = string.Join(' ', Enumerable.Range(1, 60).Select(index => "GROW-" + index.ToString("D4", CultureInfo.InvariantCulture)));

        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(text);

        Assert.Equal(ReferenceLimits.MaxCandidates, found.Count);
    }

    /// <summary>An upload's path lines, sent as Composer.SendAsync builds them, are Library file references.</summary>
    [Fact]
    public void Scan_UploadPathLines_AreLibraryCandidates()
    {
        string text = "please check\n\n`D:\\Uploads\\a.md`\n`D:\\Uploads\\b.md`";

        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(text);

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.LibraryFile, @"D:\Uploads\a.md"), (ReferenceKind.LibraryFile, @"D:\Uploads\b.md") }, Keys(found));
    }

    /// <summary>A rooted POSIX path with spaces in a code span is a Library file reference.</summary>
    [Fact]
    public void Scan_RootedPosixPathInCodeSpan()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan("`/data/Uploads/room1/a b.png`");

        Assert.Equal(new (ReferenceKind Kind, string Key)[] { (ReferenceKind.LibraryFile, "/data/Uploads/room1/a b.png") }, Keys(found));
    }

    /// <summary>Ten uploads and a Task id keep the Task: the Library files have their own cap (D-19).</summary>
    [Fact]
    public void Scan_TenUploadsAndATaskId_KeepsTheTaskId()
    {
        IEnumerable<string> paths = Enumerable.Range(0, 10).Select(index => @"D:\Uploads\file" + index.ToString(CultureInfo.InvariantCulture) + ".md");
        string text = "see GROW-0002\n\n" + string.Join('\n', paths.Select(path => "`" + path + "`"));

        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(text);

        (ReferenceKind Kind, string Key)[] expected = [(ReferenceKind.Task, "GROW-0002"), .. paths.Select(path => (ReferenceKind.LibraryFile, path))];
        Assert.Equal(expected, Keys(found));
    }

    /// <summary>Empty text has no references.</summary>
    [Fact]
    public void Scan_EmptyText_ReturnsEmpty()
    {
        IReadOnlyList<ReferenceCandidate> found = ReferenceScanner.Scan(string.Empty);

        Assert.Empty(found);
    }

    /// <summary>Projects candidates to their kind and key, so a test compares what was found without the positions.</summary>
    /// <param name="candidates">The scanner's candidates.</param>
    /// <returns>The kind and key of each candidate, in order.</returns>
    private static (ReferenceKind Kind, string Key)[] Keys(IReadOnlyList<ReferenceCandidate> candidates) =>
        candidates.Select(candidate => (candidate.Kind, candidate.Key)).ToArray();
}
