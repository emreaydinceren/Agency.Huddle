using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins the two chip pools of <c>AvatarCatalog</c>: the distinct professions and the tag pool after its
/// exclusions, its ordering, and that neither depends on any query.
/// </summary>
public sealed class AvatarCatalogTests
{
    /// <summary>Builds a fixture avatar of the given profession and tags.</summary>
    private static LibraryAvatar Av(string profession, params string[] tags) =>
        new(profession + ".png", profession, "fair", "woman", tags);

    /// <summary>Professions are listed once each, alphabetically ignoring case, with the first casing seen.</summary>
    [Fact]
    public void ProfessionPool_IsDistinctAndAlphabetical()
    {
        AvatarCatalog catalog = new(
        [
            Av("pilot"),
            Av("Chef"),
            Av("Pilot"),
            Av("DJ"),
            Av("chef"),
        ]);

        string[] professions = [.. catalog.Professions];
        Assert.Equal(["Chef", "DJ", "pilot"], professions);
    }

    /// <summary>The profession slug, skin-, man, woman and bg- tags never reach the pool.</summary>
    [Theory]
    [InlineData("Chief of Staff", "chief-of-staff")]
    [InlineData("Blue-Collar Worker", "blue-collar-worker")]
    [InlineData("DJ", "dj")]
    public void TagPool_ExcludesProfessionSkinGenderAndBackground(string profession, string slug)
    {
        AvatarCatalog catalog = new([Av(profession, slug, "skin-fair", "man", "woman", "bg-pink", "glasses")]);

        string[] pool = [.. catalog.Tags.Select(t => t.Tag)];

        Assert.Equal(["glasses"], pool);
    }

    /// <summary>A tag that merely resembles an excluded one stays in the pool.</summary>
    [Fact]
    public void TagPool_KeepsLookalikeTags()
    {
        AvatarCatalog catalog = new([Av("Pilot", "pilot", "skinny", "woman-hat", "background", "pilot-hat")]);

        string[] pool = [.. catalog.Tags.Select(t => t.Tag).Order(StringComparer.Ordinal)];

        Assert.Equal(["background", "pilot-hat", "skinny", "woman-hat"], pool);
    }

    /// <summary>A profession slug is excluded only for that avatar's own profession, not for others.</summary>
    [Fact]
    public void TagPool_OtherAvatarsProfessionSlug_StaysInPool()
    {
        AvatarCatalog catalog = new([Av("Pilot", "pilot", "chef"), Av("Chef", "chef")]);

        string[] pool = [.. catalog.Tags.Select(t => t.Tag)];

        Assert.Equal(["chef"], pool);
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

    /// <summary>Searching never changes the pools.</summary>
    [Fact]
    public void TagPool_IsUnchangedByAnyQuery()
    {
        AvatarCatalog catalog = new([Av("A", "hat", "beard"), Av("B", "hat")]);
        string[] tagsBefore = [.. catalog.Tags.Select(t => $"{t.Tag}={t.Count}")];
        string[] professionsBefore = [.. catalog.Professions];

        _ = catalog.Search(new AvatarLibraryQuery(
            SkinGroup.Dark,
            "zzz",
            new HashSet<string>(["A"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["beard"], StringComparer.OrdinalIgnoreCase)));

        string[] tagsAfter = [.. catalog.Tags.Select(t => $"{t.Tag}={t.Count}")];
        Assert.Equal(tagsBefore, tagsAfter);
        string[] professionsAfter = [.. catalog.Professions];
        Assert.Equal(professionsBefore, professionsAfter);
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
