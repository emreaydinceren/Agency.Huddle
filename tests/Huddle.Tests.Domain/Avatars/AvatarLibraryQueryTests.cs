using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins <c>AvatarCatalog.Search</c>: skin group, prefix text over hyphen-split tokens, professions OR,
/// tags AND, and that all four combine as AND while master order is preserved.
/// </summary>
public sealed class AvatarLibraryQueryTests
{
    /// <summary>Builds a fixture avatar whose file name doubles as its identity in assertions.</summary>
    private static LibraryAvatar Av(string file, string profession, string tone, params string[] tags) =>
        new(file, profession, tone, "woman", tags);

    /// <summary>Builds a query from loose parts, with case-insensitive sets as the real dialog uses.</summary>
    private static AvatarLibraryQuery Q(SkinGroup? skin = null, string text = "", string[]? professions = null, string[]? tags = null) =>
        new(
            skin,
            text,
            new HashSet<string>(professions ?? [], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(tags ?? [], StringComparer.OrdinalIgnoreCase));

    /// <summary>One avatar per documented tone plus an unknown one, in a fixed order.</summary>
    private static AvatarCatalog Tones() => new(
    [
        Av("fair.png", "Pilot", "fair"),
        Av("light.png", "Pilot", "light"),
        Av("tan.png", "Pilot", "tan"),
        Av("brown.png", "Pilot", "brown"),
        Av("dark.png", "Pilot", "dark"),
        Av("olive.png", "Pilot", "olive"),
    ]);

    /// <summary>Projects results to their file names.</summary>
    private static string[] Files(IReadOnlyList<LibraryAvatar> found) => [.. found.Select(a => a.File)];

    /// <summary>The Light group holds fair and light avatars.</summary>
    [Fact]
    public void Skin_Light_IncludesFairAndLight()
    {
        string[] files = Files(Tones().Search(Q(skin: SkinGroup.Light)));

        Assert.Equal(["fair.png", "light.png"], files);
    }

    /// <summary>The Tan group holds tan and brown avatars.</summary>
    [Fact]
    public void Skin_Tan_IncludesTanAndBrown()
    {
        string[] files = Files(Tones().Search(Q(skin: SkinGroup.Tan)));

        Assert.Equal(["tan.png", "brown.png"], files);
    }

    /// <summary>The Dark group holds only dark avatars.</summary>
    [Fact]
    public void Skin_Dark_IncludesOnlyDark()
    {
        string[] files = Files(Tones().Search(Q(skin: SkinGroup.Dark)));

        Assert.Equal(["dark.png"], files);
    }

    /// <summary>With no skin chosen an avatar of an unknown tone is still listed.</summary>
    [Fact]
    public void Skin_All_IncludesUnknownTone()
    {
        string[] files = Files(Tones().Search(Q()));

        Assert.Contains("olive.png", files);
        Assert.Equal(6, files.Length);
    }

    /// <summary>Any specific skin group excludes an avatar of an unknown tone.</summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Tan")]
    [InlineData("Dark")]
    public void Skin_Group_ExcludesUnknownTone(string group)
    {
        SkinGroup skin = Enum.Parse<SkinGroup>(group);

        string[] files = Files(Tones().Search(Q(skin: skin)));

        Assert.DoesNotContain("olive.png", files);
    }

    /// <summary>A word that starts a tag word finds the avatar; a word from the middle does not.</summary>
    [Theory]
    [InlineData("bun", 1)]
    [InlineData("hai", 1)]
    [InlineData("un", 0)]
    [InlineData("hair bunx", 0)]
    public void Text_PrefixMatchesTagWord(string text, int expected)
    {
        AvatarCatalog catalog = new([Av("a.png", "Pilot", "fair", "hair bun")]);

        IReadOnlyList<LibraryAvatar> found = catalog.Search(Q(text: text));

        Assert.Equal(expected, found.Count);
    }

    /// <summary>Tokens split on hyphens, so a word finds the part after the hyphen and the whole hyphenated tag still matches.</summary>
    [Theory]
    [InlineData("pink", 1)]
    [InlineData("bg", 1)]
    [InlineData("skin-fair", 1)]
    [InlineData("skin-dark", 0)]
    [InlineData("ink", 0)]
    public void Text_MatchesHyphenSplitToken(string text, int expected)
    {
        AvatarCatalog catalog = new(
        [
            Av("a.png", "Pilot", "fair", "bg-pink"),
            Av("b.png", "Chef", "fair", "skin-fair"),
        ]);

        IReadOnlyList<LibraryAvatar> found = catalog.Search(Q(text: text));

        Assert.Equal(expected, found.Count);
    }

    /// <summary>The profession counts as searchable text, and every word must match something.</summary>
    [Theory]
    [InlineData("chief", 1)]
    [InlineData("chief staff", 1)]
    [InlineData("chief glasses", 1)]
    [InlineData("chief hat", 0)]
    public void Text_MultipleWords_AreAnded(string text, int expected)
    {
        AvatarCatalog catalog = new([Av("a.png", "Chief of Staff", "fair", "glasses")]);

        IReadOnlyList<LibraryAvatar> found = catalog.Search(Q(text: text));

        Assert.Equal(expected, found.Count);
    }

    /// <summary>Case in the box or in the data never changes the result.</summary>
    [Theory]
    [InlineData("PINK")]
    [InlineData("pInK")]
    [InlineData("chef")]
    public void Text_IsCaseInsensitive(string text)
    {
        AvatarCatalog catalog = new([Av("a.png", "CHEF", "fair", "Bg-Pink")]);

        IReadOnlyList<LibraryAvatar> found = catalog.Search(Q(text: text));

        Assert.Single(found);
    }

    /// <summary>An empty or whitespace-only box passes every avatar.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - ")]
    public void Text_Empty_PassesEveryone(string text)
    {
        IReadOnlyList<LibraryAvatar> found = Tones().Search(Q(text: text));

        Assert.Equal(6, found.Count);
    }

    /// <summary>Several selected professions keep an avatar of any of them.</summary>
    [Fact]
    public void Professions_Several_AreOred()
    {
        AvatarCatalog catalog = new(
        [
            Av("pilot.png", "Pilot", "fair"),
            Av("chef.png", "Chef", "fair"),
            Av("dj.png", "DJ", "fair"),
        ]);

        string[] files = Files(catalog.Search(Q(professions: ["pilot", "CHEF"])));

        Assert.Equal(["pilot.png", "chef.png"], files);
    }

    /// <summary>Several selected tags keep only an avatar that carries all of them.</summary>
    [Fact]
    public void Tags_Several_AreAnded()
    {
        AvatarCatalog catalog = new(
        [
            Av("both.png", "Pilot", "fair", "hat", "beard"),
            Av("hat.png", "Pilot", "fair", "hat"),
            Av("beard.png", "Pilot", "fair", "beard"),
        ]);

        string[] files = Files(catalog.Search(Q(tags: ["HAT", "beard"])));

        Assert.Equal(["both.png"], files);
    }

    /// <summary>Skin, text, profession and tag filters all have to pass together.</summary>
    [Fact]
    public void AllFilters_Combine_AsAnd()
    {
        AvatarCatalog catalog = new(
        [
            Av("hit.png", "Pilot", "fair", "hat"),
            Av("wrongSkin.png", "Pilot", "dark", "hat"),
            Av("wrongProfession.png", "Chef", "fair", "hat"),
            Av("wrongTag.png", "Pilot", "fair", "beard"),
            Av("wrongText.png", "Pilot", "fair", "hat", "hair bun"),
        ]);

        AvatarLibraryQuery query = Q(SkinGroup.Light, "hat", ["Pilot"], ["hat"]);

        string[] files = Files(catalog.Search(query));

        Assert.Equal(["hit.png", "wrongText.png"], files);
    }

    /// <summary>Results keep the order the avatars were supplied in.</summary>
    [Fact]
    public void Search_PreservesMasterOrder()
    {
        AvatarCatalog catalog = new(
        [
            Av("z.png", "Pilot", "fair"),
            Av("a.png", "Pilot", "fair"),
            Av("m.png", "Pilot", "fair"),
        ]);

        string[] files = Files(catalog.Search(AvatarLibraryQuery.Empty));

        Assert.Equal(["z.png", "a.png", "m.png"], files);
    }

    /// <summary>A query nothing satisfies yields an empty list, not null.</summary>
    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        IReadOnlyList<LibraryAvatar> found = Tones().Search(Q(text: "zzz"));

        Assert.Empty(found);
    }

    /// <summary>An avatar with an unknown tone is listed under All even when text and tags select it.</summary>
    [Fact]
    public void Search_UnknownToneWithTextAndTag_FoundUnderAll()
    {
        AvatarCatalog catalog = new([Av("o.png", "Pilot", "olive", "hat")]);

        IReadOnlyList<LibraryAvatar> found = catalog.Search(Q(text: "pil", tags: ["hat"]));

        Assert.Single(found);
    }
}
