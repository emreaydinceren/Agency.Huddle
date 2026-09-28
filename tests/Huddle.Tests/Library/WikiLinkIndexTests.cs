using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins Spec §6.5 (wikilinks, backlinks, the index) and Spec §7 <c>MaxIndexedFiles</c>, over a
/// temp Library built by <see cref="LibraryFileServiceFixture"/>.
/// </summary>
public sealed class WikiLinkIndexTests
{
    /// <summary>Builds a <see cref="WikiLinkIndex"/> over <paramref name="fixture"/>'s current
    /// root store and resolver, with <paramref name="maxIndexedFiles"/> in place of the default.</summary>
    private static WikiLinkIndex CreateIndex(LibraryFileServiceFixture fixture, int maxIndexedFiles = 5000)
    {
        TeamOptions teamOptions = new() { Library = new LibraryOptions { MaxIndexedFiles = maxIndexedFiles } };
        return new WikiLinkIndex(fixture.RootStore, fixture.Resolver, Options.Create(teamOptions));
    }

    /// <summary>Plan row: <c>a.md</c> line 3 and <c>b.md</c> line 1 both link <c>[[plan]]</c>; both
    /// come back with their line number and the line's text.</summary>
    [Fact]
    public void Backlinks_ListsNotesAndLines()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "line one\nline two\nsee [[plan]] here\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "look at [[plan]]\nmore text\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.Resolve(root, "plan.md"));

        Assert.Equal(
            [
                new LibraryBacklink("a.md", 3, "see [[plan]] here"),
                new LibraryBacklink("b.md", 1, "look at [[plan]]"),
            ],
            backlinks);
    }

    /// <summary>Two links to the same target on one line are listed once for that line, not once
    /// per link (this run's pinned choice: a backlink row is per matching line, not per link token).</summary>
    [Fact]
    public void Backlinks_TwoLinksOnOneLine_ListedOncePerLine()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "[[plan]] and again [[plan]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.Resolve(root, "plan.md"));

        Assert.Equal([new LibraryBacklink("a.md", 1, "[[plan]] and again [[plan]]")], backlinks);
    }

    /// <summary>Spec §6.5: "Links across roots are not resolved" — a link in one pinned root never
    /// counts as a backlink of a note in another root, even with the identical relative path.</summary>
    [Fact]
    public void Backlinks_SkipsOtherRoots()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootA = fixture.CreatePinnedRoot("rootA");
        string rootB = fixture.CreatePinnedRoot("rootB");
        File.WriteAllText(Path.Combine(rootA, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(rootB, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(rootB, "a.md"), "see [[plan]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.Resolve(rootA, "plan.md"));

        Assert.Empty(backlinks);
    }

    /// <summary>Used by rename: every link across the root that resolves to a given path, note and
    /// link together, in note-path order.</summary>
    [Fact]
    public void LinksTo_ReturnsEveryLinkResolvingToPath()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "see [[plan]]\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "see [[plan|the plan]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<(string NotePath, WikiLink Link)> links = index.LinksTo(fixture.Resolve(root, "plan.md"));

        Assert.Equal(2, links.Count);
        Assert.Equal("a.md", links[0].NotePath);
        Assert.Equal("plan", links[0].Link.Target);
        Assert.Equal("b.md", links[1].NotePath);
        Assert.Equal("the plan", links[1].Link.Alias);
    }

    /// <summary>Plan row: a note created after construction but before the first query is still
    /// indexed — the index is built lazily, on first use, not eagerly at construction.</summary>
    [Fact]
    public void Build_IsLazy()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        File.WriteAllText(Path.Combine(root, "a.md"), "see [[plan]]\n");
        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.Resolve(root, "plan.md"));

        Assert.Equal([new LibraryBacklink("a.md", 1, "see [[plan]]")], backlinks);
    }

    /// <summary>Plan row: <c>Invalidate</c> drops the cached root so the next query re-walks disk.</summary>
    [Fact]
    public void Invalidate_Root_RebuildsOnNextQuery()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);
        LibraryPath planPath = fixture.Resolve(root, "plan.md");
        Assert.Empty(index.Backlinks(planPath));

        File.WriteAllText(Path.Combine(root, "a.md"), "see [[plan]]\n");
        string rootId = planPath.Root.Id;
        index.Invalidate(rootId);

        Assert.Equal([new LibraryBacklink("a.md", 1, "see [[plan]]")], index.Backlinks(planPath));
    }

    /// <summary>Plan row: above <c>MaxIndexedFiles</c>, the root's index is not built:
    /// <c>IsAvailable</c> is false and <c>Backlinks</c> comes back empty rather than throwing.</summary>
    [Fact]
    public void OverMaxIndexedFiles_NotBuilt()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        for (int i = 0; i < 3; i++)
        {
            File.WriteAllText(Path.Combine(root, $"extra{i}.md"), $"see [[plan]] {i}\n");
        }

        fixture.Reload();
        LibraryPath planPath = fixture.Resolve(root, "plan.md");
        WikiLinkIndex index = CreateIndex(fixture, maxIndexedFiles: 3);

        Assert.False(index.IsAvailable(planPath.Root.Id));
        Assert.Empty(index.Backlinks(planPath));
    }

    /// <summary>The boundary case for <see cref="OverMaxIndexedFiles_NotBuilt"/>: exactly
    /// <c>MaxIndexedFiles</c> files is still available.</summary>
    [Fact]
    public void AtMaxIndexedFiles_Available()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "see [[plan]]\n");
        fixture.Reload();
        LibraryPath planPath = fixture.Resolve(root, "plan.md");
        WikiLinkIndex index = CreateIndex(fixture, maxIndexedFiles: 2);

        Assert.True(index.IsAvailable(planPath.Root.Id));
        Assert.Equal([new LibraryBacklink("a.md", 1, "see [[plan]]")], index.Backlinks(planPath));
    }

    /// <summary>Plan row: a non-Markdown file is a resolvable link target (for embeds and
    /// backlinks) but is never itself parsed for outgoing links.</summary>
    [Fact]
    public void NonMarkdownFiles_AreResolvableTargets_ButNotParsed()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllBytes(Path.Combine(root, "diagram.png"), [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllText(Path.Combine(root, "a.md"), "![[diagram.png]]\n[[diagram]] would not resolve without the extension in the target text\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);
        LibraryPath diagramPath = fixture.Resolve(root, "diagram.png");

        IReadOnlyList<(string NotePath, WikiLink Link)> links = index.LinksTo(diagramPath);

        Assert.Single(links);
        Assert.Equal("a.md", links[0].NotePath);
        Assert.True(links[0].Link.IsEmbed);
        Assert.Equal("diagram.png", links[0].Link.Target);
    }

    /// <summary>Correction D8.5: <c>Backlinks</c> compares the caller's <see cref="LibraryPath"/>
    /// against the indexed note paths with <see cref="FolderSnapshot.PathComparer"/>, so a caller
    /// spelling that differs only in case from what's on disk still finds the note's backlinks.
    /// Skipped on a case-sensitive file system (Linux), where the two spellings name different files.</summary>
    [Fact]
    public void Backlinks_CallerPathDifferentCase_StillFinds()
    {
        if (!FolderSnapshot.PathComparer.Equals("A", "a"))
        {
            Assert.Skip("Case-sensitive file system: a differently-cased path names a different file.");
            return;
        }

        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("marketing");
        Directory.CreateDirectory(Path.Combine(root, "Marketing"));
        File.WriteAllText(Path.Combine(root, "Marketing", "PLAN.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(root, "a.md"), "see [[plan]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);
        LibraryPath differentlyCased = fixture.Resolve(root, "marketing/PLAN.md");

        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(differentlyCased);

        Assert.Equal([new LibraryBacklink("a.md", 1, "see [[plan]]")], backlinks);
    }

    /// <summary>A note inside a Team's <c>_tasks/</c> folder is hidden the same way
    /// <c>ListAsync</c> hides it (Spec §6.16); a link written there is not indexed, so it never
    /// shows up as a backlink.</summary>
    [Fact]
    public void Backlinks_ExcludesLinksUnderHiddenTasksFolder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Acme"));
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Acme", "_tasks"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Acme", "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Acme", "_tasks", "hidden.md"), "see [[plan]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.ResolveTeams("Acme/plan.md"));

        Assert.Empty(backlinks);
    }

    /// <summary>A directory junction that loops back into an already-visited folder is skipped by
    /// <c>TryResolveChild</c>, the same as <see cref="LibraryFileService.ListAsync"/>: the walk
    /// terminates and does not double-count the notes reached the other, non-looping way.</summary>
    [Fact]
    public void Walk_JunctionLoop_DoesNotHangOrDoubleCount()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        string sub = Path.Combine(root, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(sub, "a.md"), "see [[plan]]\n");
        string link = Path.Combine(sub, "loop");
        bool linked = TestLinks.TryCreateLink(link, root);
        if (!linked)
        {
            Assert.Skip("Could not create a directory link on this machine.");
            return;
        }

        try
        {
            fixture.Reload();
            WikiLinkIndex index = CreateIndex(fixture);

            IReadOnlyList<LibraryBacklink> backlinks = index.Backlinks(fixture.Resolve(root, "plan.md"));

            Assert.Equal([new LibraryBacklink("sub/a.md", 1, "see [[plan]]")], backlinks);
        }
        finally
        {
            TestLinks.RemoveLink(link);
        }
    }

    /// <summary>Plan row (Spec §6.5 rule 3): re-pinning a root at a different path raises
    /// <see cref="LibraryRootStore.RootsChanged"/>, which drops the cached index — the old root's
    /// notes are gone from the next query, not merged with the new path's.</summary>
    [Fact]
    public void RootsChanged_DropsCache_OldNotesGone()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string oldRoot = Path.Combine(fixture.DataDir, "old");
        string newRoot = Path.Combine(fixture.DataDir, "new");
        Directory.CreateDirectory(oldRoot);
        Directory.CreateDirectory(newRoot);
        File.WriteAllText(Path.Combine(oldRoot, "plan.md"), "# Plan\n");
        File.WriteAllText(Path.Combine(oldRoot, "a.md"), "see [[plan]]\n");
        File.WriteAllText(Path.Combine(newRoot, "plan.md"), "# Plan\n");
        fixture.CreatePinnedRoot("pinned");
        fixture.RootStore.Save([new PinnedRootEntry("pinned", oldRoot)]);
        WikiLinkIndex index = CreateIndex(fixture);
        LibraryPath oldPlan = fixture.Resolve(oldRoot, "plan.md");
        Assert.Equal([new LibraryBacklink("a.md", 1, "see [[plan]]")], index.Backlinks(oldPlan));

        fixture.RootStore.Save([new PinnedRootEntry("pinned", newRoot)]);
        LibraryPath newPlan = fixture.Resolve(newRoot, "plan.md");

        Assert.Empty(index.Backlinks(newPlan));
    }

    /// <summary>Spec §6.5 rule 3, applied to backlinks: when a bare-name link ties between two
    /// notes at equal distance, resolution picks the ordinal-first path, and only THAT note gets
    /// the backlink — not both candidates.</summary>
    [Fact]
    public void AmbiguousLink_CountsBacklinkOfResolvedNoteOnly()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        Directory.CreateDirectory(Path.Combine(root, "A"));
        Directory.CreateDirectory(Path.Combine(root, "B"));
        Directory.CreateDirectory(Path.Combine(root, "C"));
        File.WriteAllText(Path.Combine(root, "A", "plan.md"), "# Plan A\n");
        File.WriteAllText(Path.Combine(root, "B", "plan.md"), "# Plan B\n");
        File.WriteAllText(Path.Combine(root, "C", "link.md"), "see [[plan]]\n");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<LibraryBacklink> winnerBacklinks = index.Backlinks(fixture.Resolve(root, "A/plan.md"));
        IReadOnlyList<LibraryBacklink> loserBacklinks = index.Backlinks(fixture.Resolve(root, "B/plan.md"));

        Assert.Equal([new LibraryBacklink("C/link.md", 1, "see [[plan]]")], winnerBacklinks);
        Assert.Empty(loserBacklinks);
    }

    /// <summary>Fix card 8.4 item 1: an <see cref="WikiLinkIndex.Invalidate"/> that lands while a build is
    /// still walking disk (nothing cached yet to remove) must stop that build's stale result from being
    /// cached. The test-only <c>afterWalkForTests</c> hook fires deterministically right after the walk
    /// finishes but before the generation check, so the race is reproduced without a sleep: the query that
    /// triggers the race gets its own (correct-at-the-time) answer, but the FOLLOWING query must see the
    /// change made during the race, not the stale cached one.</summary>
    [Fact]
    public void Invalidate_DuringInFlightBuild_PreventsStaleCache()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "plan.md"), "# Plan\n");
        fixture.Reload();
        LibraryPath planPath = fixture.Resolve(root, "plan.md");
        string rootId = planPath.Root.Id;

        bool raceTriggered = false;
        TeamOptions teamOptions = new();
        List<WikiLinkIndex> holder = [];
        WikiLinkIndex index = new(fixture.RootStore, fixture.Resolver, Options.Create(teamOptions), afterWalkForTests: () =>
        {
            if (raceTriggered)
            {
                return;
            }

            raceTriggered = true;

            // Simulates a write landing, and its Invalidate call running, while the build above was
            // already in flight (so Invalidate found nothing cached to remove). Reached through
            // `holder` (populated right after construction, below) rather than the `index` local
            // itself, so this closure never reads a not-yet-assigned or possibly-null variable.
            File.WriteAllText(Path.Combine(root, "a.md"), "see [[plan]]\n");
            holder[0].Invalidate(rootId);
        });
        holder.Add(index);

        IReadOnlyList<LibraryBacklink> duringRace = index.Backlinks(planPath);
        IReadOnlyList<LibraryBacklink> afterRace = index.Backlinks(planPath);

        Assert.Empty(duringRace);
        Assert.Equal([new LibraryBacklink("a.md", 1, "see [[plan]]")], afterRace);
    }
}
