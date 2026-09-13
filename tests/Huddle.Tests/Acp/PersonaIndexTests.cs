namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;

/// <summary>
/// Exercises <see cref="PersonaIndex"/> directly against in-memory (path, text) pairs - no
/// filesystem, no <see cref="PersonaStore"/>. This is deliberately the bulk of this phase's
/// coverage: the index is where identity, collisions and ordering are actually decided, and testing
/// it here means <see cref="PersonaStore"/>'s own tests only need to prove it is wired in correctly.
/// </summary>
public sealed class PersonaIndexTests
{
    [Fact]
    public void Build_OneValidFile_BecomesOneEntry()
    {
        var index = PersonaIndex.Build([("jarvis.md", PersonaText("Jarvis"))]);

        var entry = Assert.Single(index.Entries);
        Assert.Equal("Jarvis", entry.Name);
        Assert.Equal("Jarvis", entry.Title);
        Assert.Equal("Jarvis", entry.Alias);
        Assert.Equal("jarvis.md", entry.Path);
        Assert.Empty(index.Rejected);
    }

    [Theory]
    [InlineData("---\nTitle: Chief of Staff\nAlias: coo\n---\nbody")]
    [InlineData("---\nName: coo\nAlias: coo\n---\nbody")]
    [InlineData("---\nName: coo\nTitle: Chief of Staff\n---\nbody")]
    public void Build_FileMissingARequiredIdentityField_IsRejected(string text)
    {
        var index = PersonaIndex.Build([("coo.md", text)]);

        Assert.Empty(index.Entries);
        var rejection = Assert.Single(index.Rejected);
        Assert.Equal("coo.md", rejection.Path);
        Assert.False(string.IsNullOrWhiteSpace(rejection.Reason));
    }

    [Fact]
    public void Build_FileWithNoFrontmatterAtAll_IsRejectedWithAReason()
    {
        var index = PersonaIndex.Build([("plain.md", "Just plain prose, no frontmatter at all.")]);

        Assert.Empty(index.Entries);
        var rejection = Assert.Single(index.Rejected);
        Assert.Equal("plain.md", rejection.Path);
        Assert.False(string.IsNullOrWhiteSpace(rejection.Reason));
    }

    /// <summary>Lowercase frontmatter keys, as real files on disk write them, read identically to the capitalised spec form.</summary>
    [Fact]
    public void Build_LowercaseFrontmatterKeys_ParseIdenticallyToCapitalisedKeys()
    {
        var text = "---\nname: coo\ntitle: Chief of Staff\nalias: coo\n---\nbody";

        var index = PersonaIndex.Build([("coo.md", text)]);

        var entry = Assert.Single(index.Entries);
        Assert.Equal("coo", entry.Name);
        Assert.Equal("Chief of Staff", entry.Title);
    }

    [Fact]
    public void Build_TwoFilesWithTheSameName_AreBothRejected()
    {
        var files = new List<(string Path, string Text)>
        {
            ("a.md", PersonaText("Jarvis", alias: "jar")),
            ("b.md", PersonaText("Jarvis", alias: "jv")),
        };

        var index = PersonaIndex.Build(files);

        Assert.Empty(index.Entries);
        Assert.Equal(2, index.Rejected.Count);
        Assert.All(index.Rejected, rejection => Assert.Contains("Jarvis", rejection.Reason, StringComparison.Ordinal));
        Assert.Contains(index.Rejected, rejection => rejection.Path == "a.md" && rejection.Reason.Contains("b.md", StringComparison.Ordinal));
        Assert.Contains(index.Rejected, rejection => rejection.Path == "b.md" && rejection.Reason.Contains("a.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_TwoFilesWithTheSameAlias_AreBothRejected()
    {
        var files = new List<(string Path, string Text)>
        {
            ("a.md", PersonaText("Jarvis", alias: "jar")),
            ("b.md", PersonaText("Nova", alias: "jar")),
        };

        var index = PersonaIndex.Build(files);

        Assert.Empty(index.Entries);
        Assert.Equal(2, index.Rejected.Count);
        Assert.All(index.Rejected, rejection => Assert.Contains("jar", rejection.Reason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_OneFilesAliasEqualsADifferentFilesName_BothAreRejected()
    {
        var files = new List<(string Path, string Text)>
        {
            ("jarvis.md", PersonaText("Jarvis", alias: "jar")),
            ("nova.md", PersonaText("Nova", alias: "Jarvis")),
        };

        var index = PersonaIndex.Build(files);

        Assert.Empty(index.Entries);
        Assert.Equal(2, index.Rejected.Count);
        Assert.Contains(index.Rejected, rejection => rejection.Path == "jarvis.md");
        Assert.Contains(index.Rejected, rejection => rejection.Path == "nova.md");
    }

    /// <summary>An Alias equal to its OWN Name is a plausible authoring choice, not a typo, and must not be flagged.</summary>
    [Fact]
    public void Build_AliasEqualToOwnName_IsNotACollision()
    {
        var index = PersonaIndex.Build([("jarvis.md", PersonaText("Jarvis", alias: "Jarvis"))]);

        var entry = Assert.Single(index.Entries);
        Assert.Equal("Jarvis", entry.Name);
        Assert.Equal("Jarvis", entry.Alias);
        Assert.Empty(index.Rejected);
    }

    /// <summary>Closes the live bug docs/agencyteam/known-limits.md records: "Jarvis" and "jarvis" sharing one SQLite row.</summary>
    [Fact]
    public void Build_NamesDifferingOnlyByCase_AreTreatedAsACollision()
    {
        var files = new List<(string Path, string Text)>
        {
            ("a.md", PersonaText("Jarvis", alias: "jar1")),
            ("b.md", PersonaText("jarvis", alias: "jar2")),
        };

        var index = PersonaIndex.Build(files);

        Assert.Empty(index.Entries);
        Assert.Equal(2, index.Rejected.Count);
    }

    [Fact]
    public void Build_OrdersEntriesByNameOrdinalRegardlessOfInputOrder()
    {
        var files = new List<(string Path, string Text)>
        {
            ("z.md", PersonaText("Zed")),
            ("a.md", PersonaText("Amy")),
            ("m.md", PersonaText("Mona")),
        };

        var index = PersonaIndex.Build(files);

        Assert.Equal(["Amy", "Mona", "Zed"], index.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void Build_TeamsAreDistinctAndSortedAcrossAllEntries()
    {
        var files = new List<(string Path, string Text)>
        {
            ("a.md", PersonaText("Amy", teams: "Household, Business")),

            // "business" duplicates Amy's "Business" case-insensitively, and must collapse to one.
            ("b.md", PersonaText("Bob", teams: "business, Finance")),
        };

        var index = PersonaIndex.Build(files);

        Assert.Equal(["Business", "Finance", "Household"], index.Teams);
    }

    /// <summary>Files are processed in path order before collisions are decided, so the error text a caller sees never depends on the order it happened to enumerate them in.</summary>
    [Fact]
    public void Build_RejectionReasons_AreStableRegardlessOfInputOrder()
    {
        var forward = PersonaIndex.Build([("a.md", PersonaText("Jarvis", alias: "j1")), ("b.md", PersonaText("Jarvis", alias: "j2"))]);
        var reversed = PersonaIndex.Build([("b.md", PersonaText("Jarvis", alias: "j2")), ("a.md", PersonaText("Jarvis", alias: "j1"))]);

        Assert.Equal(
            forward.Rejected.Select(rejection => (rejection.Path, rejection.Reason)),
            reversed.Rejected.Select(rejection => (rejection.Path, rejection.Reason)));
    }

    [Fact]
    public void ByName_IsCaseInsensitive()
    {
        var index = PersonaIndex.Build([("jarvis.md", PersonaText("Jarvis"))]);

        Assert.NotNull(index.ByName("jarvis"));
        Assert.NotNull(index.ByName("JARVIS"));
        Assert.Null(index.ByName("nova"));
    }

    /// <summary>
    /// A hand-built index (bypassing <see cref="PersonaIndex.Build"/>'s collision rules, which would
    /// never let this state arise on its own) isolates <see cref="PersonaIndex.ByNameOrAlias"/>'s
    /// precedence: a Name match must win even when a different entry's Alias also matches the query.
    /// </summary>
    [Fact]
    public void ByNameOrAlias_PrefersAMatchingNameOverAnotherEntrysAlias()
    {
        var jarvis = new PersonaEntry("Jarvis", "Chief of Staff", "jar", [], "jarvis.md", "text");
        var impostor = new PersonaEntry("Someone", "Someone Else", "Jarvis", [], "someone.md", "text");
        var index = new PersonaIndex([jarvis, impostor], [], []);

        var resolved = index.ByNameOrAlias("Jarvis");

        Assert.Same(jarvis, resolved);
    }

    [Fact]
    public void ByNameOrAlias_FallsBackToAlias_WhenNoEntryHasThatName()
    {
        var index = PersonaIndex.Build([("jarvis.md", PersonaText("Jarvis", alias: "jar"))]);

        var resolved = index.ByNameOrAlias("jar");

        Assert.NotNull(resolved);
        Assert.Equal("Jarvis", resolved.Name);
    }

    /// <summary>Minimal valid Persona frontmatter for <paramref name="name"/>, with <see cref="PersonaIdentity.Title"/> and <see cref="PersonaIdentity.Alias"/> defaulting to <paramref name="name"/> too.</summary>
    private static string PersonaText(string name, string? title = null, string? alias = null, string? teams = null)
    {
        var teamsLine = teams is null ? string.Empty : $"Teams: {teams}\n";
        return $"---\nName: {name}\nTitle: {title ?? name}\nAlias: {alias ?? name}\n{teamsLine}---\nbody";
    }
}
