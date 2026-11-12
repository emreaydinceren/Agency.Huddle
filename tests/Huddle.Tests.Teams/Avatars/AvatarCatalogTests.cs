using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins the tag chip pool of <c>AvatarCatalog</c>: its exclusions, its ordering, and that it does not depend
/// on any query.
/// </summary>
public sealed class AvatarCatalogTests
{
    /// <summary>Builds a fixture avatar with the given file stem and tags.</summary>
    private static LibraryAvatar Av(string name, params string[] tags) =>
        new(name + ".png", "fair", "woman", tags);

    /// <summary>The skin-, man, woman and bg- tags never reach the pool.</summary>
    [Fact]
    public void TagPool_ExcludesSkinGenderAndBackground()
    {
        AvatarCatalog catalog = new([Av("A", "skin-fair", "man", "woman", "bg-pink", "glasses")]);

        string[] pool = [.. catalog.Tags.Select(t => t.Tag)];

        Assert.Equal(["glasses"], pool);
    }

    /// <summary>A blank or white-space tag never becomes an empty chip in the pool.</summary>
    [Fact]
    public void TagPool_ExcludesBlankTags()
    {
        AvatarCatalog catalog = new([Av("A", "", "  ", "hat")]);

        string[] pool = [.. catalog.Tags.Select(t => t.Tag)];

        Assert.Equal(["hat"], pool);
    }

    /// <summary>A tag that merely resembles an excluded one stays in the pool.</summary>
    [Fact]
    public void TagPool_KeepsLookalikeTags()
    {
        AvatarCatalog catalog = new([Av("A", "skinny", "woman-hat", "background", "pilot-hat")]);

        string[] pool = [.. catalog.Tags.Select(t => t.Tag).Order(StringComparer.Ordinal)];

        Assert.Equal(["background", "pilot-hat", "skinny", "woman-hat"], pool);
    }

    /// <summary>A tag that used to be a profession slug is now an ordinary tag and is counted like any other.</summary>
    [Theory]
    [InlineData("chef")]
    [InlineData("chief-of-staff")]
    [InlineData("dj")]
    public void TagPool_FormerProfessionSlug_IsOrdinaryTag(string slug)
    {
        AvatarCatalog catalog = new([Av("A", slug, "glasses"), Av("B", slug)]);

        TagCount[] pool = [.. catalog.Tags];

        Assert.Equal([new TagCount(slug, 2), new TagCount("glasses", 1)], pool);
    }

    /// <summary>Tags are ordered by how many avatars carry them, then alphabetically.</summary>
    [Fact]
    public void TagPool_OrdersByFrequencyThenName()
    {
        AvatarCatalog catalog = new(
        [
            Av("A", "zebra", "apple", "hat"),
            Av("B", "hat", "zebra"),
            Av("C", "hat", "banana"),
            Av("D", "hat"),
        ]);

        string[] pool = [.. catalog.Tags.Select(t => $"{t.Tag}={t.Count}")];

        Assert.Equal(["hat=4","zebra=2", "apple=1", "banana=1"], pool);
    }

    /// <summary>An avatar listing a tag twice, or in another case, counts once and the first casing wins.</summary>
    [Fact]
    public void TagPool_DuplicateAndCaseVariants_CountOncePerAvatar()
    {
        AvatarCatalog catalog = new([Av("A", "Hat", "hat", "HAT"), Av("B", "hAt")]);

        TagCount only = Assert.Single(catalog.Tags);

        Assert.Equal(new TagCount("Hat", 2), only);
    }

    /// <summary>Searching never changes the pool.</summary>
    [Fact]
    public void TagPool_IsUnchangedByAnyQuery()
    {
        AvatarCatalog catalog = new([Av("A", "hat", "beard"), Av("B", "hat")]);
        string[] tagsBefore = [.. catalog.Tags.Select(t => $"{t.Tag}={t.Count}")];

        _ = catalog.Search(new AvatarLibraryQuery(
            SkinGroup.Dark,
            "zzz",
            new HashSet<string>(["beard"], StringComparer.OrdinalIgnoreCase)));

        string[] tagsAfter = [.. catalog.Tags.Select(t => $"{t.Tag}={t.Count}")];
        Assert.Equal(tagsBefore, tagsAfter);
        Assert.Equal(2, catalog.Count);
    }

    /// <summary>The catalog copies its input, so changing the source afterwards does not change it.</summary>
    [Fact]
    public void Constructor_CopiesInput()
    {
        List<LibraryAvatar> source = [Av("A", "hat")];
        AvatarCatalog catalog = new(source);

        source.Clear();

        Assert.Single(catalog.All);
    }
}
