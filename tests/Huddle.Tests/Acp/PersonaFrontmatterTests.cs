namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;

/// <summary>Exercises <see cref="PersonaFrontmatter"/> against in-memory Persona text — no file I/O.</summary>
public sealed class PersonaFrontmatterTests
{
    /// <summary>No opening delimiter at all means the whole text is body and there are no fields.</summary>
    [Fact]
    public void Parse_NoFrontmatter_ReturnsNoFieldsAndWholeTextAsBody()
    {
        var (fields, body) = PersonaFrontmatter.Parse("You are a minimal persona.");

        Assert.Empty(fields);
        Assert.Equal("You are a minimal persona.", body);
    }

    /// <summary>An opening "---" that is never closed is not frontmatter — the whole text is body.</summary>
    [Fact]
    public void Parse_UnclosedFrontmatter_ReturnsNoFieldsAndWholeTextAsBody()
    {
        var text = "---\nname: 'Nova'\nYou are Nova.";

        var (fields, body) = PersonaFrontmatter.Parse(text);

        Assert.Empty(fields);
        Assert.Equal(text, body);
    }

    /// <summary>An empty frontmatter block (open and close with nothing between) parses with no fields.</summary>
    [Fact]
    public void Parse_EmptyFrontmatterBlock_ReturnsNoFieldsAndTheBody()
    {
        var (fields, body) = PersonaFrontmatter.Parse("---\n---\nYou are Nova.");

        Assert.Empty(fields);
        Assert.Equal("You are Nova.", body);
    }

    [Fact]
    public void Parse_WithFrontmatter_ReturnsBodyAfterTheClosingDelimiter()
    {
        var (_, body) = PersonaFrontmatter.Parse("---\nrole: 'Router'\n---\n\n# Nova\n\nYou are Nova.");

        Assert.Equal("# Nova\n\nYou are Nova.", body);
    }

    [Fact]
    public void ComposeJobDescription_NoFrontmatter_ReturnsEmptyString()
    {
        var description = PersonaFrontmatter.ComposeJobDescription("You are a minimal persona.");

        Assert.Equal(string.Empty, description);
    }

    /// <summary>Underscore-prefixed keys are reserved for future programmatic use and never shown.</summary>
    [Fact]
    public void ComposeJobDescription_OnlyUnderscorePrefixedFields_ReturnsEmptyString()
    {
        var text = "---\n_internal_id: '12345'\n_priority: '3'\n---\nYou are Nova.";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        Assert.Equal(string.Empty, description);
    }

    [Fact]
    public void ComposeJobDescription_ExcludesUnderscorePrefixedFieldsButKeepsOthersInFileOrder()
    {
        var text = "---\nrole: 'Router'\n_internal_id: '12345'\nsummary: 'Triages work'\n---\nYou are Nova.";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        Assert.Equal("Role: Router\nSummary: Triages work", description);
    }

    [Fact]
    public void Parse_SingleQuotedScalar_StripsQuotes()
    {
        var (fields, _) = PersonaFrontmatter.Parse("---\nname: 'Chief of Staff'\n---\nbody");

        Assert.Equal("Chief of Staff", Assert.Single(fields).Value);
    }

    [Fact]
    public void Parse_DoubleQuotedScalar_StripsQuotes()
    {
        var (fields, _) = PersonaFrontmatter.Parse("---\nstatus: \"Placeholder text\"\n---\nbody");

        Assert.Equal("Placeholder text", Assert.Single(fields).Value);
    }

    /// <summary>YAML's own escape for an embedded apostrophe inside a single-quoted scalar is "''".</summary>
    [Fact]
    public void Parse_EscapedApostropheInSingleQuotedScalar_Unescapes()
    {
        var (fields, _) = PersonaFrontmatter.Parse("---\nnote: 'You don''t know'\n---\nbody");

        Assert.Equal("You don't know", Assert.Single(fields).Value);
    }

    /// <summary>
    /// A quoted scalar containing commas (real persona data, e.g. a "role" field) must stay one
    /// value, not be misread as a list — Team's parser deliberately drops Agency's
    /// comma/space/bracket list-guessing for exactly this reason.
    /// </summary>
    [Fact]
    public void Parse_QuotedScalarContainingCommas_IsNotSplitIntoAList()
    {
        var (fields, _) = PersonaFrontmatter.Parse(
            "---\nrole: 'Router, triage, and cross-workstation continuity'\n---\nbody");

        var field = Assert.Single(fields);
        Assert.Equal("role", field.Key);
        Assert.Equal("Router, triage, and cross-workstation continuity", field.Value);
    }

    [Fact]
    public void Parse_FoldedBlockScalar_JoinsLinesWithSpacesIntoOneLine()
    {
        var text = "---\nsummary: >-\n  Owns the seams between workstations: triages\n  anything unrouted.\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal(
            "Owns the seams between workstations: triages anything unrouted.",
            Assert.Single(fields).Value);
    }

    /// <summary>A literal block scalar preserves its own line breaks, but the composed field value collapses them to spaces.</summary>
    [Fact]
    public void Parse_LiteralBlockScalar_CollapsesLineBreaksToSpaces()
    {
        var text = "---\naddress: |\n  Line one\n  Line two\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal("Line one Line two", Assert.Single(fields).Value);
    }

    /// <summary>
    /// Regression test: most real persona files (e.g. personas/Cielo.md) write "collaborates_with"
    /// as a bracketed YAML flow list, not a block list. Before this was recognized, the raw
    /// brackets and quotes leaked verbatim into the composed job description.
    /// </summary>
    [Fact]
    public void Parse_BracketedFlowList_ParsesEachQuoteStrippedItem()
    {
        var text = "---\ncollaborates_with: ['Finances', 'Email', 'Chief of Staff']\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal("Finances; Email; Chief of Staff", Assert.Single(fields).Value);
    }

    /// <summary>A comma inside a quoted flow-list item must not be treated as an item separator.</summary>
    [Fact]
    public void Parse_BracketedFlowListWithCommaInsideQuotedItem_DoesNotSplitInsideTheQuote()
    {
        var text = "---\nroles: ['Router, triage', 'Email']\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal("Router, triage; Email", Assert.Single(fields).Value);
    }

    /// <summary>An empty flow list carries no information, so it is dropped, matching an empty block-list key.</summary>
    [Fact]
    public void Parse_EmptyBracketedFlowList_IsDropped()
    {
        var (fields, _) = PersonaFrontmatter.Parse("---\ncollaborates_with: []\n---\nbody");

        Assert.Empty(fields);
    }

    /// <summary>
    /// Regression test: a block scalar left blank must not swallow the next top-level field. A
    /// frontmatter key is always at column 0, so a dedented (0-space) line can never be genuine
    /// block-scalar content, even as the block's first line.
    /// </summary>
    [Fact]
    public void Parse_EmptyBlockScalarImmediatelyFollowedByNextField_DoesNotSwallowIt()
    {
        var text = "---\nsummary: >-\n\nrole: 'Router'\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        var role = Assert.Single(fields, field => field.Key == "role");
        Assert.Equal("Router", role.Value);
    }

    [Fact]
    public void Parse_YamlBlockList_JoinsItemsWithSemicolon()
    {
        var text = "---\nconsult_when:\n  - 'First reason'\n  - 'Second reason'\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal("First reason; Second reason", Assert.Single(fields).Value);
    }

    [Fact]
    public void Parse_YamlBlockListWithEscapedQuotedItems_StripsAndUnescapesEachItem()
    {
        var text = "---\nconsult_when:\n  - 'You don''t know which teammate owns something'\n  - 'Plain reason'\n---\nbody";

        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.Equal(
            "You don't know which teammate owns something; Plain reason",
            Assert.Single(fields).Value);
    }

    [Fact]
    public void Parse_CrlfLineEndings_ParsedCorrectly()
    {
        var text = "---\r\nrole: 'Router'\r\n---\r\nYou are Nova.";

        var (fields, body) = PersonaFrontmatter.Parse(text);

        Assert.Equal("Router", Assert.Single(fields).Value);
        Assert.Equal("You are Nova.", body);
    }

    [Fact]
    public void ComposeJobDescription_TitleCasesMultiWordUnderscoreSeparatedKeys()
    {
        var text = "---\ndo_not_consult_for: 'Deep domain analysis'\n---\nbody";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        Assert.Equal("Do Not Consult For: Deep domain analysis", description);
    }

    /// <summary>
    /// End-to-end sanity check against the actual frontmatter shape used by
    /// personas/Chief of Staff.md, tracing every field this parser has to get right at once:
    /// quoted scalars, a folded block scalar, several block lists (one containing an escaped
    /// apostrophe), and a trailing plain scalar.
    /// </summary>
    [Fact]
    public void ComposeJobDescription_ChiefOfStaffFrontmatter_ComposesEveryFieldInOrder()
    {
        var text = "---\n"
            + "name: 'Chief of Staff'\n"
            + "mention: '@Chief of Staff'\n"
            + "role: 'Router, triage, and cross-workstation continuity'\n"
            + "summary: >-\n"
            + "  Owns the seams between workstations: triages anything unrouted, decides who handles what, holds the through-line across weeks, and produces the daily and weekly brief.\n"
            + "consult_when:\n"
            + "  - 'You don''t know which teammate owns something'\n"
            + "  - 'Your task spans two or more workstations and needs someone to hold the whole picture'\n"
            + "do_not_consult_for:\n"
            + "  - 'Deep domain analysis of any kind — they will route it straight back to you'\n"
            + "  - 'Anything already clearly inside one workstation'\n"
            + "delivers:\n"
            + "  - 'A routing decision'\n"
            + "owns:\n"
            + "  - 'Brain\\CLAUDE.md (Routing Map)'\n"
            + "collaborates_with: 'all'\n"
            + "---\n\n# Chief of Staff\n";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        var expected = string.Join(
            '\n',
            "Name: Chief of Staff",
            "Mention: @Chief of Staff",
            "Role: Router, triage, and cross-workstation continuity",
            "Summary: Owns the seams between workstations: triages anything unrouted, decides who handles what, holds the through-line across weeks, and produces the daily and weekly brief.",
            "Consult When: You don't know which teammate owns something; Your task spans two or more workstations and needs someone to hold the whole picture",
            "Do Not Consult For: Deep domain analysis of any kind — they will route it straight back to you; Anything already clearly inside one workstation",
            "Delivers: A routing decision",
            "Owns: Brain\\CLAUDE.md (Routing Map)",
            "Collaborates With: all");

        Assert.Equal(expected, description);
    }
}
