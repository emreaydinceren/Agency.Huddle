using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="WikiLinkParser"/>: the wikilink syntaxes it recognises, the code
/// constructs it skips, and the exact source span (<c>Line</c>/<c>Start</c>/<c>Length</c>) it
/// records for each match (Spec §6.5 <i>Parsing</i>, corrections-B5 D8 item 4).
/// </summary>
public sealed class WikiLinkParserTests
{
    /// <summary>
    /// The 1-based line of <paramref name="index"/> in <paramref name="text"/>, counting a
    /// lone <c>\r</c> (not followed by <c>\n</c>) as a line break in addition to <c>\n</c> - an
    /// independent count from <see cref="WikiLinkParser"/>'s own raw-offset line tracking.
    /// </summary>
    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                line++;
            }
            else if (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'))
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>Builds the expected <see cref="WikiLink"/> for a single, unique <paramref name="token"/> occurrence in <paramref name="markdown"/>.</summary>
    private static WikiLink Link(string markdown, string token, string target, string? heading, string? alias, bool embed)
    {
        int start = markdown.IndexOf(token, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Token '{token}' not found in fixture.");
        return new WikiLink(target, heading, alias, embed, LineOf(markdown, start), start, token.Length);
    }

    /// <summary>Every scenario the plan and corrections-B5 D8 item 4 name.</summary>
    public static TheoryData<string, string, WikiLink[]> Cases()
    {
        TheoryData<string, string, WikiLink[]> data = new();

        const string plain = "See [[plan]].";
        data.Add("PlainLink", plain, [Link(plain, "[[plan]]", "plan", null, null, false)]);

        const string alias = "[[plan|the plan]]";
        data.Add("Alias", alias, [Link(alias, alias, "plan", null, "the plan", false)]);

        const string heading = "[[plan#Dates]]";
        data.Add("Heading", heading, [Link(heading, heading, "plan", "Dates", null, false)]);

        const string headingAlias = "[[plan#Dates|when]]";
        data.Add("HeadingAlias", headingAlias, [Link(headingAlias, headingAlias, "plan", "Dates", "when", false)]);

        const string sameNoteHeading = "[[#Dates]]";
        data.Add("SameNoteHeading", sameNoteHeading, [Link(sameNoteHeading, sameNoteHeading, string.Empty, "Dates", null, false)]);

        const string embedRow = "![[diagram.png]]";
        data.Add("Embed", embedRow, [Link(embedRow, embedRow, "diagram.png", null, null, true)]);

        const string slashTarget = "[[Launch Q4/plan]]";
        data.Add("SlashTarget", slashTarget, [Link(slashTarget, slashTarget, "Launch Q4/plan", null, null, false)]);

        const string codeSpan = "`[[not a link]]` and [[y]].";
        data.Add("CodeSpanNotLink", codeSpan, [Link(codeSpan, "[[y]]", "y", null, null, false)]);

        const string fenced = "```\n[[x]]\n```\n[[y]]";
        data.Add("FencedBlockNotLink", fenced, [Link(fenced, "[[y]]", "y", null, null, false)]);

        const string twoOnLine3 = "para1\npara2\n[[a]] and [[b]]";
        data.Add("TwoLinksSameLine", twoOnLine3,
        [
            Link(twoOnLine3, "[[a]]", "a", null, null, false),
            Link(twoOnLine3, "[[b]]", "b", null, null, false),
        ]);

        const string emptyBrackets = "[[]] and [[ ]] and [[y]]";
        data.Add("EmptyOrWhitespaceTarget", emptyBrackets, [Link(emptyBrackets, "[[y]]", "y", null, null, false)]);

        const string tablePipeEscape = "| H |\n| --- |\n| [[a\\|b]] |";
        data.Add("TableCellEscapedPipe", tablePipeEscape, [Link(tablePipeEscape, "[[a\\|b]]", "a", null, "b", false)]);

        const string escapedOpen = "\\[[x]] and [[y]]";
        data.Add("EscapedOpeningBracketsNotLink", escapedOpen, [Link(escapedOpen, "[[y]]", "y", null, null, false)]);

        const string escapedClose = "[[x\\]]\n[[y]]";
        data.Add("EscapedClosingBracketNotLink", escapedClose, [Link(escapedClose, "[[y]]", "y", null, null, false)]);

        const string indentedCodeInList = "- item\n\n      [[x]]\n\n[[y]]";
        data.Add("IndentedCodeBlockInListItemSkipped", indentedCodeInList, [Link(indentedCodeInList, "[[y]]", "y", null, null, false)]);

        const string headingWithHash = "[[a#h1#h2]]";
        data.Add("HeadingContainsHash", headingWithHash, [Link(headingWithHash, headingWithHash, "a", "h1#h2", null, false)]);

        const string blockRef = "[[a#^block]]";
        data.Add("BlockReferenceHeading", blockRef, [Link(blockRef, blockRef, "a", "^block", null, false)]);

        const string aliasWithHash = "[[a|b#c]]";
        data.Add("AliasContainsHash", aliasWithHash, [Link(aliasWithHash, aliasWithHash, "a", null, "b#c", false)]);

        const string besideReferenceDefinition = "[x]: https://e\n[[x]]";
        data.Add("StillLinkBesideReferenceDefinition", besideReferenceDefinition, [Link(besideReferenceDefinition, "[[x]]", "x", null, null, false)]);

        const string loneCr = "line1\r[[x]]";
        data.Add("LoneCrLineBreak", loneCr, [Link(loneCr, "[[x]]", "x", null, null, false)]);

        const string mdExtensionKept = "[[plan.md]]";
        data.Add("MdExtensionKeptInTarget", mdExtensionKept, [Link(mdExtensionKept, mdExtensionKept, "plan.md", null, null, false)]);

        return data;
    }

    /// <summary>Parses each scenario and asserts the whole set of <see cref="WikiLink"/> records, in order.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Parse_VariousInputs_ReturnsExpectedLinks(string scenario, string markdown, WikiLink[] expected)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(expected);

        IReadOnlyList<WikiLink> actual = WikiLinkParser.Parse(markdown);

        Assert.True(expected.SequenceEqual(actual), scenario);
        Assert.Equal(expected, actual);
        foreach (WikiLink link in actual)
        {
            string token = markdown.Substring(link.Start, link.Length);
            // contains-ok: the haystack is raw Markdown source text, not rendered markup.
            Assert.StartsWith(link.IsEmbed ? "![[" : "[[", token, StringComparison.Ordinal);
            // contains-ok: the haystack is raw Markdown source text, not rendered markup.
            Assert.EndsWith("]]", token, StringComparison.Ordinal);
        }
    }
}
