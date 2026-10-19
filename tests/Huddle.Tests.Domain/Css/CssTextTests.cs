namespace Agency.Huddle.Tests.Css;

/// <summary>Pins <see cref="CssText"/> over small inline stylesheets: rule lookup, line-start anchoring, comment stripping, sections and counting.</summary>
public sealed class CssTextTests
{
    private const string Chips = ".mud-chip {\n  color: red;\n}\n.foo .chip {\n  color: green;\n}\n.chip {\n  color: blue;\n}\n";

    private const string Twice = ".a {\n  x: 1;\n}\n.a {\n  x: 2;\n}\n";

    /// <summary>A selector is found at the start of a line and not as the tail of <c>.mud-chip</c> or of <c>.foo .chip</c>.</summary>
    [Fact]
    public void RuleBody_ChipSelector_SkipsSuffixAndDescendantMatches()
    {
        string body = CssText.RuleBody(Chips, ".chip");

        Assert.Contains("color: blue;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("red", body, StringComparison.Ordinal);
        Assert.DoesNotContain("green", body, StringComparison.Ordinal);
    }

    /// <summary>A rule on the very first line of the text (no preceding newline) is found.</summary>
    [Fact]
    public void RuleBody_RuleOnFirstLine_IsFound()
    {
        Assert.Contains("a: 1;", CssText.RuleBody(".x { a: 1; }", ".x"), StringComparison.Ordinal);
    }

    /// <summary>With a selector defined twice, First returns the earlier body, Last the later one, All both.</summary>
    [Fact]
    public void RuleBody_SelectorDefinedTwice_FirstLastAndAllDiffer()
    {
        string first = CssText.RuleBody(Twice, ".a", RuleMatch.First);
        string last = CssText.RuleBody(Twice, ".a", RuleMatch.Last);
        string all = CssText.RuleBody(Twice, ".a", RuleMatch.All);

        Assert.Contains("x: 1;", first, StringComparison.Ordinal);
        Assert.DoesNotContain("x: 2;", first, StringComparison.Ordinal);
        Assert.Contains("x: 2;", last, StringComparison.Ordinal);
        Assert.DoesNotContain("x: 1;", last, StringComparison.Ordinal);
        Assert.Contains("x: 1;", all, StringComparison.Ordinal);
        Assert.Contains("x: 2;", all, StringComparison.Ordinal);
    }

    /// <summary>The default match is First.</summary>
    [Fact]
    public void RuleBody_NoMatchArgument_UsesFirst()
    {
        Assert.Equal(CssText.RuleBody(Twice, ".a", RuleMatch.First), CssText.RuleBody(Twice, ".a"));
    }

    /// <summary>A selector that is absent fails the test with a message naming it.</summary>
    [Fact]
    public void RuleBody_MissingSelector_FailsNamingIt()
    {
        Exception thrown = Assert.ThrowsAny<Exception>(() => CssText.RuleBody(Twice, ".missing-one"));

        Assert.Contains(".missing-one", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A comma-list rule is found by any of its selectors, and a selector that only ends a longer name is not.</summary>
    [Fact]
    public void RuleBodyInList_SelectorInsideList_FindsTheRule()
    {
        const string css = ".a, .b {\n  x: 1;\n}\n.mud-c {\n  y: 2;\n}\n";

        Assert.Contains("x: 1;", CssText.RuleBodyInList(css, ".b"), StringComparison.Ordinal);
        Assert.Contains("x: 1;", CssText.RuleBodyInList(css, ".a"), StringComparison.Ordinal);
        Assert.ThrowsAny<Exception>(() => CssText.RuleBodyInList(css, ".c"));
    }

    /// <summary>A missing list selector fails naming it.</summary>
    [Fact]
    public void RuleBodyInList_MissingSelector_FailsNamingIt()
    {
        Exception thrown = Assert.ThrowsAny<Exception>(() => CssText.RuleBodyInList(Twice, ".nope"));

        Assert.Contains(".nope", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>Loose lookup matches a selector written with different whitespace, mid-line, and collapses the body's whitespace.</summary>
    [Fact]
    public void RuleBodyLoose_DifferentWhitespace_MatchesAndCollapses()
    {
        const string css = "/* c */ .a   .b {\n  x: 1;\n  y:   2;\n}\n";

        Assert.Equal("x: 1; y: 2;", CssText.RuleBodyLoose(css, ".a .b"));
        Assert.Contains("\n", CssText.RuleBodyLoose(css, ".a .b", collapseWhitespace: false), StringComparison.Ordinal);
    }

    /// <summary>Loose lookup does not match a selector that is the tail of a longer class name.</summary>
    [Fact]
    public void RuleBodyLoose_SuffixOfLongerName_IsNotMatched()
    {
        Assert.ThrowsAny<Exception>(() => CssText.RuleBodyLoose(".mud-chip { x: 1; }", ".chip"));
    }

    /// <summary>Terminated comments are removed, including multi-line ones.</summary>
    [Fact]
    public void StripComments_TerminatedComments_AreRemoved()
    {
        Assert.Equal("a  b c", CssText.StripComments("a /* one */ b /* two\nlines */c"));
    }

    /// <summary>An unterminated comment is left in place, as the regex <c>/\*.*?\*/</c> does.</summary>
    [Fact]
    public void StripComments_UnterminatedComment_IsKept()
    {
        Assert.Equal("a /* open", CssText.StripComments("a /* open"));
    }

    /// <summary>A section runs from its header to the next named header, or to the end when none is named.</summary>
    [Fact]
    public void Section_HeaderToNextHeaderOrEnd()
    {
        const string css = "/* ---- One */\n.a { }\n/* ---- Two */\n.b { }\n";

        string one = CssText.Section(css, "One", "Two");
        string two = CssText.Section(css, "Two");

        Assert.Contains(".a", one, StringComparison.Ordinal);
        Assert.DoesNotContain(".b", one, StringComparison.Ordinal);
        Assert.Contains(".b", two, StringComparison.Ordinal);
        Assert.DoesNotContain(".a", two, StringComparison.Ordinal);
    }

    /// <summary>A missing section header fails naming it.</summary>
    [Fact]
    public void Section_MissingHeader_FailsNamingIt()
    {
        Exception thrown = Assert.ThrowsAny<Exception>(() => CssText.Section("/* ---- One */", "Ghost"));

        Assert.Contains("Ghost", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>Runs of whitespace, newlines included, become a single space and the ends are trimmed.</summary>
    [Fact]
    public void CollapseWhitespace_RunsBecomeOneSpace()
    {
        Assert.Equal("a b c", CssText.CollapseWhitespace("  a \r\n  b\t\tc \n"));
    }

    /// <summary>Counting does not overlap matches: <c>aa</c> occurs twice in <c>aaaaa</c>.</summary>
    [Fact]
    public void CountOccurrences_OverlappingNeedle_CountsNonOverlapping()
    {
        Assert.Equal(2, CssText.CountOccurrences("aaaaa", "aa"));
        Assert.Equal(0, CssText.CountOccurrences("abc", "z"));
    }

    /// <summary>Reading a CRLF file gives LF text by default and the CRLF text unchanged with AsIs.</summary>
    [Fact]
    public void Read_CrlfFile_LfByDefaultAndCrlfWithAsIs()
    {
        string path = Path.Combine(Path.GetTempPath(), $"csstext-{Guid.NewGuid():N}.css");
        File.WriteAllText(path, ".a {\r\n  x: 1;\r\n}\r\n");
        try
        {
            string lf = CssText.Read(path);
            string asIs = CssText.Read(path, CssNewlines.AsIs);

            Assert.DoesNotContain("\r", lf, StringComparison.Ordinal);
            Assert.Contains("\r\n", asIs, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", CssText.RuleBody(lf, ".a"), StringComparison.Ordinal);
            Assert.Contains("\r\n", CssText.RuleBody(asIs, ".a"), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The app stylesheet is read once per mode and the LF form has no carriage returns.</summary>
    [Fact]
    public void AppCss_LfDefault_IsCachedAndHasNoCarriageReturn()
    {
        string lf = CssText.AppCss();

        Assert.NotEmpty(lf);
        Assert.DoesNotContain("\r", lf, StringComparison.Ordinal);
        Assert.Same(lf, CssText.AppCss(CssNewlines.Lf));
        Assert.Same(CssText.AppCss(CssNewlines.AsIs), CssText.AppCss(CssNewlines.AsIs));
    }

    /// <summary>Src and App resolve under the checkout root and the app folder.</summary>
    [Fact]
    public void RepoPaths_SrcAndApp_ResolveUnderTheRoot()
    {
        Assert.Equal(Path.Combine(RepoPaths.Root, "src", "Huddle.App", "wwwroot"), RepoPaths.App("wwwroot"));
        Assert.Equal(Path.Combine(RepoPaths.Root, "src", "Huddle.Acp"), RepoPaths.Src("Huddle.Acp"));
    }
}
