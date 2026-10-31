using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins the inline <c>#tag</c> rules of Spec §6.17. Rules marked (V) there are Huddle's reading of Obsidian and are pinned here
/// so a check against a real vault changes one expectation.
/// </summary>
public sealed class NoteTagParserTests
{
    private static string Tags(string body) => string.Join("|", NoteTagParser.Parse(body));

    /// <summary>A <c>#</c> after line start, whitespace or punctuation starts a tag.</summary>
    [Theory]
    [InlineData("#a", "a")]
    [InlineData("(#a)", "a")]
    [InlineData("**#a**", "a")]
    [InlineData(",#a", "a")]
    [InlineData("[#a]", "a")]
    [InlineData("x\n#a", "a")]
    [InlineData("see #a here", "a")]
    public void Parse_StartRule_Allows(string body, string expected) => Assert.Equal(expected, Tags(body));

    /// <summary>A <c>#</c> after a letter, digit, <c>&amp;</c> or another <c>#</c> does not start a tag.</summary>
    [Theory]
    [InlineData("word#a")]
    [InlineData("a1#a")]
    [InlineData("&#x2014;")]
    [InlineData("##a")]
    [InlineData("see https://example.com/page#a")]
    [InlineData("https://example.com/#a")]
    [InlineData("\U0001D400#a")]
    [InlineData("हिंदी#a")]
    [InlineData("é#a")]
    public void Parse_StartRule_Rejects(string body) => Assert.Equal(string.Empty, Tags(body));

    /// <summary>The characters a tag may hold, and where it ends.</summary>
    [Theory]
    [InlineData("#a/b", "a/b")]
    [InlineData("#a-b_c", "a-b_c")]
    [InlineData("#हिंदी", "हिंदी")]
    [InlineData("#🚀", "🚀")]
    [InlineData("#a.", "a")]
    [InlineData("#a,", "a")]
    [InlineData("#c++", "c")]
    [InlineData("#a=b", "a")]
    [InlineData("#a。", "a")]
    [InlineData("#a‮b", "ab")]
    [InlineData("#a‍b", "a‍b")]
    [InlineData("#work­x", "workx")]
    [InlineData("#a⁠b", "ab")]
    [InlineData("#a\"><img src=x>", "a")]
    [InlineData("#b<script>alert(1)</script>", "b")]
    public void Parse_Characters(string body, string expected) => Assert.Equal(expected, Tags(body));

    /// <summary>A tag is at most 100 UTF-16 units, measured after a trailing slash is dropped.</summary>
    [Fact]
    public void Parse_Length_CapIsOneHundred()
    {
        string hundred = new('a', 100);

        Assert.Equal(hundred, Tags("#" + hundred));
        Assert.Equal(string.Empty, Tags("#" + hundred + "a"));
        Assert.Equal(hundred, Tags("#" + hundred + "/"));
        Assert.Equal(hundred, Tags("#" + hundred + "​"));
    }

    /// <summary>A tag needs a character that is neither a digit nor a slash; a numeric parent is still a valid parent.</summary>
    [Theory]
    [InlineData("#1984", "")]
    [InlineData("#y1984", "y1984")]
    [InlineData("#2026/a", "2026/a")]
    [InlineData("#2026/1", "")]
    [InlineData("#2026/", "")]
    public void Parse_Digits(string body, string expected) => Assert.Equal(expected, Tags(body));

    /// <summary>Slash handling: a trailing slash is dropped, a leading slash is no tag and a doubled slash ends it.</summary>
    [Theory]
    [InlineData("#work/", "work")]
    [InlineData("#/a", "")]
    [InlineData("#a//b", "a")]
    public void Parse_Slashes(string body, string expected) => Assert.Equal(expected, Tags(body));

    /// <summary>Text that is code, a link destination, a comment, an escape or a heading anchor is not scanned.</summary>
    [Theory]
    [InlineData("\\#a")]
    [InlineData("[[Note #a]]")]
    [InlineData("![[x#a]]")]
    [InlineData("`#a`")]
    [InlineData("```\n#a\n```")]
    [InlineData("para\n\n    #a")]
    [InlineData("%% #a %%")]
    [InlineData("<!-- #a -->")]
    [InlineData("[t](#a)")]
    [InlineData("<https://example.com/#a>")]
    [InlineData("# Title")]
    public void Parse_Exclusions(string body) => Assert.Equal(string.Empty, Tags(body));

    /// <summary>Text that Obsidian counts: a link label, a heading with a tag, a heading-less <c>#Title</c> and the accepted HTML false positive.</summary>
    [Theory]
    [InlineData("[#a label](x)", "a")]
    [InlineData("## Plan #a", "a")]
    [InlineData("#Title", "Title")]
    [InlineData("<a href=\"#x\">", "x")]
    [InlineData("x https://a.b/c #d", "d")]
    [InlineData("`%%` marker #a %% end", "a")]
    public void Parse_Counts(string body, string expected) => Assert.Equal(expected, Tags(body));

    /// <summary>A Task id, alone or as the first segment, is not a tag; look-alikes are.</summary>
    [Theory]
    [InlineData("#PLAT-0042", "")]
    [InlineData("#PLAT-0042/notes", "")]
    [InlineData("#PLAT-0042x", "PLAT-0042x")]
    [InlineData("#plat-0042", "plat-0042")]
    public void Parse_TaskIds(string body, string expected) => Assert.Equal(expected, Tags(body));

    /// <summary>The same tag twice stays twice here; de-duplication is the merge step's job.</summary>
    [Fact]
    public void Parse_Repeats_AreKept()
    {
        Assert.Equal("a|a", Tags("#a #a"));
    }
}
