using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins front matter links and aliases in the root index (front matter properties spec D-5 backlinks, D-7): a quoted property link is a backlink, an alias finds a note and completes as <c>[[Note|Alias]]</c>, and a bare alias stays a missing link.</summary>
public sealed class WikiLinkIndexAliasesTests
{
    private const string AllTypes = "---\ntitle: Project Plan\nowner: \"[[Grace]]\"\naliases: [Plan, Q1 plan]\n---\n\n# Project Plan\n";

    private static WikiLinkIndex CreateIndex(LibraryFileServiceFixture fixture) =>
        new(fixture.RootStore, fixture.Resolver, Options.Create(new TeamOptions()));

    private static string Root(LibraryFileServiceFixture fixture)
    {
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "AllTypes.md"), AllTypes);
        File.WriteAllText(Path.Combine(root, "Grace.md"), "# Grace\n");
        File.WriteAllText(Path.Combine(root, "open.md"), "# Open\n");
        fixture.Reload();
        return root;
    }

    /// <summary>FP-T11: a quoted property link appears in the target's backlinks, with the property's line as context.</summary>
    [Fact]
    public void Backlinks_QuotedPropertyLink_CountsAsALink()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Root(fixture);
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.Resolve(root, "Grace.md"));

        Assert.Equal([new LibraryBacklink("AllTypes.md", 3, "owner: \"[[Grace]]\"")], backlinks);
    }

    /// <summary>D-7: the aliases of a note are read from its front matter, as a list or as a single value.</summary>
    [Theory]
    [InlineData("---\naliases: [Plan, Q1 plan]\n---\n", "Plan|Q1 plan")]
    [InlineData("---\naliases:\n  - One\n  - \"Two\"\n---\n", "One|Two")]
    [InlineData("---\naliases: Solo\n---\n", "Solo")]
    [InlineData("---\naliases: [Dup, Dup, \" \"]\n---\n", "Dup")]
    [InlineData("---\ntitle: none\n---\n", "")]
    [InlineData("no front matter", "")]
    [InlineData("{\"aliases\": [\"Json one\", \"Json two\"]}\n\nbody", "Json one|Json two")]
    public void NoteAliases_ReadsTheAliasesKey(string text, string expected) =>
        Assert.Equal(expected, string.Join("|", NoteAliases.Of(text)));

    /// <summary>FP-T12: a note is found by any of its aliases, by a case-insensitive part of the name.</summary>
    [Fact]
    public void NotesByAlias_FindsTheNote()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Root(fixture);
        WikiLinkIndex index = CreateIndex(fixture);
        string rootId = fixture.Resolve(root, "Grace.md").Root.Id;

        IReadOnlyList<(string NotePath, string Alias)> found = index.NotesByAlias(rootId, "Q1", TestContext.Current.CancellationToken);

        Assert.Equal([("AllTypes.md", "Q1 plan")], found);
        Assert.Empty(index.NotesByAlias(rootId, "nothing", TestContext.Current.CancellationToken));
    }

    /// <summary>FP-T12: completion offers each alias as its own entry, which inserts <c>Note|Alias</c> so the link resolves by file name.</summary>
    [Fact]
    public void SuggestTargets_OffersEachAliasAsNoteBarAlias()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Root(fixture);
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = index.SuggestTargets(fixture.Resolve(root, "open.md"), "pla", 20);

        Assert.Equal(["AllTypes|Plan", "AllTypes|Q1 plan"], suggestions);
    }

    /// <summary>FP-T12: a link written through an alias resolves by the file name, and a bare alias stays a missing link.</summary>
    [Fact]
    public void Resolve_BareAlias_StaysMissing_AndNoteBarAliasResolves()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Root(fixture);
        WikiLinkIndex index = CreateIndex(fixture);
        LibraryPath from = fixture.Resolve(root, "open.md");

        WikiLinkResolution bare = index.Resolve(from, new WikiLink("Plan", null, null, false, 1, 0, 8));
        WikiLinkResolution piped = index.Resolve(from, new WikiLink("AllTypes", null, "Plan", false, 1, 0, 17));

        Assert.Null(bare.Path);
        Assert.Equal("AllTypes.md", piped.Path);
    }

    /// <summary>D-7: an invalidated index drops its aliases with its links, so a rewritten note is found by its new alias.</summary>
    [Fact]
    public void NotesByAlias_AfterInvalidate_ReadsTheNewAliases()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Root(fixture);
        WikiLinkIndex index = CreateIndex(fixture);
        string rootId = fixture.Resolve(root, "Grace.md").Root.Id;
        Assert.Empty(index.NotesByAlias(rootId, "Renamed", TestContext.Current.CancellationToken));

        File.WriteAllText(Path.Combine(root, "AllTypes.md"), "---\naliases: [Renamed]\n---\n");
        index.Invalidate(rootId);

        Assert.Equal([("AllTypes.md", "Renamed")], index.NotesByAlias(rootId, "Renamed", TestContext.Current.CancellationToken));
    }

    /// <summary>Fix 5a: asking for aliases of a root whose index is not built, with a cancelled token, stops before the disk walk.</summary>
    [Fact]
    public void NotesByAlias_Cancelled_ThrowsBeforeBuilding()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = Root(fixture);
        WikiLinkIndex index = CreateIndex(fixture);
        string rootId = fixture.Resolve(root, "Grace.md").Root.Id;
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() => index.NotesByAlias(rootId, "Plan", cancelled.Token));
    }
}