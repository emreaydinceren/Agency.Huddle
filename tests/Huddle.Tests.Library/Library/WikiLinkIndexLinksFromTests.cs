using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins the right sidebar's Linked documents data (Spec <i>Linked documents</i>): the outgoing
/// <c>WikiLinkIndex.LinksFrom</c> lookup and the <c>WikiLinkIndex.Invalidated</c> change signal.
/// </summary>
public sealed class WikiLinkIndexLinksFromTests
{
    /// <summary>Builds a <see cref="WikiLinkIndex"/> over <paramref name="fixture"/>'s current root store and resolver.</summary>
    private static WikiLinkIndex CreateIndex(LibraryFileServiceFixture fixture, int maxIndexedFiles = 5000)
    {
        TeamOptions teamOptions = new() { Library = new LibraryOptions { MaxIndexedFiles = maxIndexedFiles } };
        return new WikiLinkIndex(fixture.RootStore, fixture.Resolver, Options.Create(teamOptions));
    }

    /// <summary>A note linking an existing note, a missing one and another existing note comes back in document
    /// order; the missing one is listed with a null <c>ResolvedPath</c>, not hidden.</summary>
    [Fact]
    public void LinksFrom_ListsResolvedAndUnresolvedInDocumentOrder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "b.md"), "# B\n");
        File.WriteAllText(Path.Combine(root, "c.md"), "# C\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "first [[b]]\nthen [[ghost]] and [[c]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryOutgoingLink> links = index.LinksFrom(fixture.Resolve(root, "a.md"));

        Assert.Equal(["b", "ghost", "c"], links.Select(static l => l.Link.Target));
        Assert.Equal(["b.md", null, "c.md"], links.Select(static l => l.ResolvedPath));
    }

    /// <summary>Two links to the same target (even on different lines) are listed once, keeping the first.</summary>
    [Fact]
    public void LinksFrom_SameTargetTwice_ListedOnce()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "b.md"), "# B\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "[[b]] and [[b|again]]\nlater [[b]] and [[ghost]] [[ghost]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryOutgoingLink> links = index.LinksFrom(fixture.Resolve(root, "a.md"));

        Assert.Equal(["b", "ghost"], links.Select(static l => l.Link.Target));
        Assert.Equal(1, links[0].Link.Line);
        Assert.Null(links[0].Link.Alias);
    }

    /// <summary>An embed <c>![[x]]</c> is not a linked document and is left out.</summary>
    [Fact]
    public void LinksFrom_Embed_IsLeftOut()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "b.md"), "# B\n");
        File.WriteAllText(Path.Combine(root, "c.md"), "# C\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "![[b]]\nsee [[c]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryOutgoingLink> links = index.LinksFrom(fixture.Resolve(root, "a.md"));

        LibraryOutgoingLink only = Assert.Single(links);
        Assert.Equal("c.md", only.ResolvedPath);
    }

    /// <summary>When the root has more files than <c>MaxIndexedFiles</c> the index is unavailable and the answer is empty.</summary>
    [Fact]
    public void LinksFrom_UnavailableRoot_IsEmpty()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "b.md"), "# B\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "see [[b]]\n");
        File.WriteAllText(Path.Combine(root, "c.md"), "# C\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture, maxIndexedFiles: 2);

        IReadOnlyList<LibraryOutgoingLink> links = index.LinksFrom(fixture.Resolve(root, "a.md"));

        Assert.False(index.IsAvailable(fixture.Resolve(root, "a.md").Root.Id));
        Assert.Empty(links);
    }

    /// <summary>A note the built index has never seen (created after the index was cached) has no links.</summary>
    [Fact]
    public void LinksFrom_UnknownNote_IsEmpty()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "b.md"), "# B\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "see [[b]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);
        Assert.Single(index.LinksFrom(fixture.Resolve(root, "a.md")));
        File.WriteAllText(Path.Combine(root, "late.md"), "see [[b]]\n");

        IReadOnlyList<LibraryOutgoingLink> links = index.LinksFrom(fixture.Resolve(root, "late.md"));

        Assert.Empty(links);
    }

    /// <summary><c>Invalidate</c> raises <c>Invalidated</c> once with the invalidated root's id.</summary>
    [Fact]
    public void Invalidate_RaisesInvalidatedWithTheRootId()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        WikiLinkIndex index = CreateIndex(fixture);
        string rootId = fixture.Resolve(root, string.Empty).Root.Id;
        List<string> raised = [];
        index.Invalidated += raised.Add;

        index.Invalidate(rootId);

        Assert.Equal([rootId], raised);
    }

    /// <summary>A roots change (here a reset of the store) raises <c>Invalidated</c> once for each configured root.</summary>
    [Fact]
    public void RootsChanged_RaisesInvalidatedPerRoot()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreatePinnedRoot("root");
        WikiLinkIndex index = CreateIndex(fixture);
        List<string> raised = [];
        index.Invalidated += raised.Add;

        fixture.RootStore.Reset();

        List<string> expected = [.. fixture.RootStore.Roots.Select(static r => r.Id)];
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Order(StringComparer.Ordinal), raised.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>Invalidated</c> is raised after the index's lock is released. The index is warmed, then the handler
    /// starts a probe (<c>IsAvailable</c>, which takes the lock) on another thread and spins up to five seconds
    /// for it to finish. Raised inside <c>lock (gate)</c>, the raising thread still holds the lock, the probe
    /// blocks and the spin times out (<c>false</c>); raised outside it, the probe completes at once.
    /// </summary>
    [Fact]
    public void Invalidated_IsRaisedOutsideTheLock()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);
        LibraryPath note = fixture.Resolve(root, "a.md");
        string rootId = note.Root.Id;
        _ = index.LinksFrom(note);
        bool finished = false;
        using ManualResetEventSlim probed = new();
        Thread probe = new(() =>
        {
            _ = index.IsAvailable(rootId);
            probed.Set();
        })
        {
            IsBackground = true,
        };
        index.Invalidated += _ =>
        {
            // A thread of its own and a blocking wait: through the thread pool, under load, the probe queued behind other work for longer than this budget.
            probe.Start();
            finished = probed.Wait(TestBudgets.Standard);
        };

        index.Invalidate(rootId);
        probe.Join();

        Assert.True(finished);
    }
}
