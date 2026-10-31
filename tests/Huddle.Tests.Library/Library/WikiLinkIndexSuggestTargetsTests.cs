using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins <c>WikiLinkIndex.SuggestTargets</c>, the data behind the editor's <c>[[</c> completion.</summary>
public sealed class WikiLinkIndexSuggestTargetsTests
{
    /// <summary>Builds a <see cref="WikiLinkIndex"/> over <paramref name="fixture"/>'s current root store and resolver.</summary>
    private static WikiLinkIndex CreateIndex(LibraryFileServiceFixture fixture)
    {
        TeamOptions teamOptions = new() { Library = new LibraryOptions { MaxIndexedFiles = 5000 } };
        return new WikiLinkIndex(fixture.RootStore, fixture.Resolver, Options.Create(teamOptions));
    }

    /// <summary>Names starting with the query come before names that only contain it; the open note and non-Markdown files are left out.</summary>
    [Fact]
    public void SuggestTargets_RanksPrefixFirst_AndSkipsSelfAndNonMarkdown()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "open.md"), "x");
        File.WriteAllText(Path.Combine(root, "my-plan.md"), "x");
        File.WriteAllText(Path.Combine(root, "plan.md"), "x");
        File.WriteAllText(Path.Combine(root, "plan.png"), "x");
        File.WriteAllText(Path.Combine(root, "openplan.md"), "x");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = index.SuggestTargets(fixture.Resolve(root, "open.md"), "PLAN", 20);

        Assert.Equal(["plan", "my-plan", "openplan"], suggestions);
    }

    /// <summary>Two notes with the same name are offered in the shortest form that tells them apart, the form a rename writes.</summary>
    [Fact]
    public void SuggestTargets_DuplicateNames_UsesShortestUniquePath()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        Directory.CreateDirectory(Path.Combine(root, "a"));
        Directory.CreateDirectory(Path.Combine(root, "b"));
        File.WriteAllText(Path.Combine(root, "a", "notes.md"), "x");
        File.WriteAllText(Path.Combine(root, "b", "notes.md"), "x");
        File.WriteAllText(Path.Combine(root, "home.md"), "x");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = index.SuggestTargets(fixture.Resolve(root, "home.md"), "notes", 20);

        Assert.Equal(["a/notes", "b/notes"], suggestions);
    }

    /// <summary>With nothing typed, notes come nearest first as Spec §6.5 resolution measures it: the open note's folder, then the fewest folder steps away, with plain path order only breaking ties.</summary>
    [Fact]
    public void SuggestTargets_EmptyQuery_ListsNearestFirst()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        Directory.CreateDirectory(Path.Combine(root, "a", "b"));
        Directory.CreateDirectory(Path.Combine(root, "far", "x"));
        File.WriteAllText(Path.Combine(root, "a", "b", "open.md"), "x");
        File.WriteAllText(Path.Combine(root, "a", "b", "zeta.md"), "x");
        File.WriteAllText(Path.Combine(root, "a", "near.md"), "x");
        File.WriteAllText(Path.Combine(root, "far", "x", "alpha.md"), "x");
        File.WriteAllText(Path.Combine(root, "root.md"), "x");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = index.SuggestTargets(fixture.Resolve(root, "a/b/open.md"), "", 20);

        Assert.Equal(["zeta", "near", "root", "alpha"], suggestions);
    }

    /// <summary>Distance outranks the match kind: a nearer note that merely contains the query comes before a farther one that starts with it.</summary>
    [Fact]
    public void SuggestTargets_NearerContains_BeatsFartherStartsWith()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllText(Path.Combine(root, "open.md"), "x");
        File.WriteAllText(Path.Combine(root, "my-plan.md"), "x");
        File.WriteAllText(Path.Combine(root, "sub", "plan.md"), "x");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = index.SuggestTargets(fixture.Resolve(root, "open.md"), "plan", 20);

        Assert.Equal(["my-plan", "plan"], suggestions);
    }

    /// <summary>A lookup whose token is already cancelled does not run.</summary>
    [Fact]
    public async Task SuggestTargetsAsync_CancelledToken_Throws()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "open.md"), "x");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.SuggestTargetsAsync(fixture.Resolve(root, "open.md"), "", 20, cts.Token));
    }

    /// <summary>The async lookup answers the same list as the synchronous one.</summary>
    [Fact]
    public async Task SuggestTargetsAsync_ReturnsTheSameSuggestions()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "open.md"), "x");
        File.WriteAllText(Path.Combine(root, "plan.md"), "x");
        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = await index.SuggestTargetsAsync(fixture.Resolve(root, "open.md"), "pl", 20, TestContext.Current.CancellationToken);

        Assert.Equal(["plan"], suggestions);
    }

    /// <summary>The list is capped at <c>max</c>.</summary>
    [Fact]
    public void SuggestTargets_CapsAtMax()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "home.md"), "x");
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(root, $"n{i}.md"), "x");
        }

        fixture.Reload();
        WikiLinkIndex index = CreateIndex(fixture);

        IReadOnlyList<string> suggestions = index.SuggestTargets(fixture.Resolve(root, "home.md"), "", 3);

        Assert.Equal(3, suggestions.Count);
    }
}
