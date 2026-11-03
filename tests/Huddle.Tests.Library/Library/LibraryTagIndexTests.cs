using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the per-root tag aggregates of Spec §6.17: display casing, nesting, counts, availability and invalidation.</summary>
public sealed class LibraryTagIndexTests
{
    private static (WikiLinkIndex Links, LibraryTagIndex Tags) Create(LibraryFileServiceFixture fixture, int maxIndexedFiles = 5000, int maxEditableBytes = 2097152)
    {
        TeamOptions teamOptions = new() { Library = new LibraryOptions { MaxIndexedFiles = maxIndexedFiles, MaxEditableBytes = maxEditableBytes } };
        WikiLinkIndex links = new(fixture.RootStore, fixture.Resolver, Options.Create(teamOptions));
        return (links, new LibraryTagIndex(links));
    }

    private static string Seed(LibraryFileServiceFixture fixture)
    {
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#work/active #Inbox\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "---\ntags: [Work]\n---\n#inbox\n");
        File.WriteAllText(Path.Combine(root, "c.md"), "#2026/a\n");
        fixture.Reload();
        return root;
    }

    /// <summary>Tags and implied parents are sorted case-insensitively, the first spelling met wins, and a parent counts its subtree once per note.</summary>
    [Fact]
    public void AllTags_DisplayCasingSortAndCounts()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;

        IReadOnlyList<LibraryTag> all = tags.AllTags(fixture.Resolve(root, "a.md").Root.Id);

        Assert.Equal(
            ["2026:1", "2026/a:1", "Inbox:2", "work:2", "work/active:1"],
            all.Select(static t => $"{t.Text}:{t.NoteCount}"));
    }

    /// <summary>A note's own tags are in display casing, frontmatter first, with no implied parents.</summary>
    [Fact]
    public void TagsOf_FrontmatterFirstInDisplayCasing()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;

        Assert.Equal(["work", "Inbox"], tags.TagsOf(fixture.Resolve(root, "b.md")));
        Assert.Equal(["work/active", "Inbox"], tags.TagsOf(fixture.Resolve(root, "a.md")));
    }

    /// <summary>A query for a parent returns its children's notes; a query for a child does not return the parent's; the query tolerates <c>#</c>, a trailing slash and any casing.</summary>
    [Fact]
    public void NotesWith_NestingAndQueryForms()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;
        string rootId = fixture.Resolve(root, "a.md").Root.Id;

        Assert.Equal(["a.md", "b.md"], tags.NotesWith(rootId, "work"));
        Assert.Equal(["a.md"], tags.NotesWith(rootId, "#Work/ACTIVE/"));
        Assert.Equal(["c.md"], tags.NotesWith(rootId, "2026"));
        Assert.Empty(tags.NotesWith(rootId, "missing"));
    }

    /// <summary>Above <c>MaxIndexedFiles</c> the tags are unavailable and empty.</summary>
    [Fact]
    public void UnavailableRoot_IsEmpty()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture, maxIndexedFiles: 2);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;
        string rootId = fixture.Resolve(root, "a.md").Root.Id;

        Assert.False(tags.IsAvailable(rootId));
        Assert.Empty(tags.AllTags(rootId));
        Assert.Empty(tags.TagsOf(fixture.Resolve(root, "a.md")));
    }

    /// <summary>A note over <c>MaxEditableBytes</c> has no tags.</summary>
    [Fact]
    public void OversizedNote_HasNoTags()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "big.md"), "#a " + new string('x', 200));
        fixture.Reload();
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture, maxEditableBytes: 50);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;

        Assert.Empty(tags.AllTags(fixture.Resolve(root, "big.md").Root.Id));
    }

    /// <summary>The aggregate is dropped when the link index is invalidated, so a tag added on disk shows after the rebuild.</summary>
    [Fact]
    public void Invalidate_DropsTheAggregate()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;
        string rootId = fixture.Resolve(root, "a.md").Root.Id;
        Assert.Empty(tags.NotesWith(rootId, "new"));

        File.WriteAllText(Path.Combine(root, "d.md"), "#new\n");
        Assert.Empty(tags.NotesWith(rootId, "new"));
        links.Invalidate(rootId);

        Assert.Equal(["d.md"], tags.NotesWith(rootId, "new"));
    }

    /// <summary>A child is shown under its parent's first spelling, wherever the child's own text spelled the parent differently.</summary>
    [Fact]
    public void AllTags_ChildTakesParentSpelling()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "#Work/active\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "#work/Active/x\n");
        fixture.Reload();
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;

        Assert.Equal(["Work", "Work/active", "Work/active/x"], tags.AllTags(fixture.Resolve(root, "a.md").Root.Id).Select(static t => t.Text));
        Assert.Equal(["Work/active/x"], tags.TagsOf(fixture.Resolve(root, "b.md")));
    }

    /// <summary>A roots change drops the aggregate, so a file added on disk shows after the reload.</summary>
    [Fact]
    public void RootsChanged_DropsTheAggregate()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;
        string rootId = fixture.Resolve(root, "a.md").Root.Id;
        Assert.Empty(tags.NotesWith(rootId, "new"));

        File.WriteAllText(Path.Combine(root, "d.md"), "#new\n");
        fixture.RootStore.SetHidden("raw", true);

        Assert.Equal(["d.md"], tags.NotesWith(rootId, "new"));
    }

    /// <summary>Indexing writes nothing: every file keeps its exact bytes.</summary>
    [Fact]
    public void Indexing_LeavesFilesByteExact()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Seed(fixture);
        byte[] before = File.ReadAllBytes(Path.Combine(root, "b.md"));
        (WikiLinkIndex links, LibraryTagIndex tags) = Create(fixture);
        using WikiLinkIndex linkLifetime = links;
        using LibraryTagIndex tagLifetime = tags;

        _ = tags.AllTags(fixture.Resolve(root, "a.md").Root.Id);

        Assert.Equal(before, File.ReadAllBytes(Path.Combine(root, "b.md")));
    }
}
