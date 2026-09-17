namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;

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
    /// apostrophe), and a trailing plain scalar. <c>name</c> is deliberately excluded from the
    /// expected output: it is now a structural identity field (see
    /// <see cref="PersonaFrontmatter.TryReadIdentity(string, out PersonaIdentity?, out string)"/>),
    /// and <c>list_agents</c> already prints it on the bullet line above this job description, so
    /// repeating it here would be a redundant "Name: Chief of Staff" line.
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

    /// <summary><see cref="PersonaFrontmatter.ComposeJobDescription"/> excludes only the <c>Name</c> key; <c>Title</c>, <c>Alias</c> and <c>Teams</c> still appear.</summary>
    [Fact]
    public void ComposeJobDescription_ExcludesNameButKeepsTitleAliasAndTeams()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business, Household\n---\nbody";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        Assert.Equal(
            "Title: Chief of Staff\nAlias: jar\nTeams: Business, Household",
            description);
    }

    /// <summary>The exclusion of <c>Name</c> is case-insensitive, matching the rest of key lookup in this class.</summary>
    [Fact]
    public void ComposeJobDescription_ExcludesLowercaseNameKeyToo()
    {
        var text = "---\nname: Jarvis\nrole: 'Router'\n---\nbody";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        Assert.Equal("Role: Router", description);
    }

    /// <summary>
    /// <c>adapter</c> is plumbing about which ACP agent runs a Teammate, not something any Agent
    /// can act on (Spec §12, E-6), so it never appears in the job description — while an
    /// unrelated custom key survives, proving only <c>adapter</c> was excluded.
    /// </summary>
    [Fact]
    public void ComposeJobDescription_AdapterField_IsExcludedButUnrelatedKeysSurvive()
    {
        var text = "---\nadapter: agency\nrole: 'Router'\n---\nbody";

        var description = PersonaFrontmatter.ComposeJobDescription(text);

        Assert.DoesNotContain("adapter", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Adapter", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role", description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A well-formed identity block yields all four fields, with <c>Teams</c> read from its comma form.</summary>
    [Fact]
    public void TryReadIdentity_WellFormedBlock_ReadsAllFourFields()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business, Household\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("Jarvis", identity.Name);
        Assert.Equal("Chief of Staff", identity.Title);
        Assert.Equal("jar", identity.Alias);
        Assert.Equal(["Business", "Household"], identity.Teams);
    }

    /// <summary>Lowercase keys, as written by existing files on disk, read identically to the capitalised spec form.</summary>
    [Fact]
    public void TryReadIdentity_LowercaseKeys_ReadsIdenticallyToCapitalisedKeys()
    {
        var text = "---\nname: Jarvis\ntitle: Chief of Staff\nalias: jar\nteams: Business, Household\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("Jarvis", identity.Name);
        Assert.Equal("Chief of Staff", identity.Title);
        Assert.Equal("jar", identity.Alias);
        Assert.Equal(["Business", "Household"], identity.Teams);
    }

    /// <summary>A missing <c>Name</c> field fails, naming <c>Name</c> in the error.</summary>
    [Fact]
    public void TryReadIdentity_NameMissing_FailsNamingName()
    {
        var text = "---\nTitle: Chief of Staff\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Name", error, StringComparison.Ordinal);
    }

    /// <summary>A missing <c>Title</c> field fails, naming <c>Title</c> in the error.</summary>
    [Fact]
    public void TryReadIdentity_TitleMissing_FailsNamingTitle()
    {
        var text = "---\nName: Jarvis\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Title", error, StringComparison.Ordinal);
    }

    /// <summary>A missing <c>Alias</c> field fails, naming <c>Alias</c> in the error.</summary>
    [Fact]
    public void TryReadIdentity_AliasMissing_FailsNamingAlias()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Alias", error, StringComparison.Ordinal);
    }

    /// <summary>A whitespace-only value for a required field is treated the same as a missing one.</summary>
    [Fact]
    public void TryReadIdentity_WhitespaceOnlyTitle_FailsAsMissing()
    {
        var text = "---\nName: Jarvis\nTitle: '   '\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Title", error, StringComparison.Ordinal);
    }

    /// <summary>A <c>Name</c> that fails <see cref="NameRules.IsValidAgentName(string?)"/> (a leading space here) fails, naming <c>Name</c> and the offending value.</summary>
    [Fact]
    public void TryReadIdentity_InvalidName_FailsNamingNameAndValue()
    {
        var text = "---\nName: ' Jarvis'\nTitle: Chief of Staff\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Name", error, StringComparison.Ordinal);
        Assert.Contains("Jarvis", error, StringComparison.Ordinal);
    }

    /// <summary>An <c>Alias</c> that fails <see cref="NameRules.IsValidAgentName(string?)"/> (a trailing space here) fails, naming <c>Alias</c> and the offending value.</summary>
    [Fact]
    public void TryReadIdentity_InvalidAlias_FailsNamingAliasAndValue()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: 'jar '\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Alias", error, StringComparison.Ordinal);
        Assert.Contains("jar", error, StringComparison.Ordinal);
    }

    /// <summary>The bracketed flow-list form of <c>Teams</c> yields the same list as the comma form.</summary>
    [Fact]
    public void TryReadIdentity_TeamsAsFlowList_YieldsSameListAsCommaForm()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: [Business, Household]\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal(["Business", "Household"], identity.Teams);
    }

    /// <summary>The block-list form of <c>Teams</c> yields the same list as the comma form.</summary>
    [Fact]
    public void TryReadIdentity_TeamsAsBlockList_YieldsSameListAsCommaForm()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams:\n  - Business\n  - Household\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal(["Business", "Household"], identity.Teams);
    }

    /// <summary>An absent <c>Teams</c> field yields an empty list, not null, and is still a success.</summary>
    [Fact]
    public void TryReadIdentity_TeamsAbsent_YieldsEmptyListAndStillSucceeds()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Empty(identity.Teams);
    }

    /// <summary>A team repeated (case-insensitively) in the same <c>Teams</c> field collapses to its first occurrence.</summary>
    [Fact]
    public void TryReadIdentity_TeamsWithCaseInsensitiveDuplicate_CollapsesToFirstOccurrence()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business, household, Business\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal(["Business", "household"], identity.Teams);
    }

    /// <summary>A key written twice fails, naming that key, rather than silently taking the last occurrence.</summary>
    [Fact]
    public void TryReadIdentity_DuplicateKey_Fails()
    {
        var text = "---\nName: Jarvis\nName: Zed\nTitle: Chief of Staff\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.False(succeeded);
        Assert.Null(identity);
        Assert.Contains("Name", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression guard: a quoted "role" scalar containing commas must stay one field with its
    /// commas intact when reading identity, exactly as it does under the generic <see cref="PersonaFrontmatter.Parse"/> —
    /// only the <c>Teams</c> key is ever split on commas.
    /// </summary>
    [Fact]
    public void TryReadIdentity_RoleFieldWithCommas_IsNotSplitAndIdentityStillReads()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\n"
            + "role: 'Router, triage, and cross-workstation continuity'\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);
        var (fields, _) = PersonaFrontmatter.Parse(text);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);

        var role = Assert.Single(fields, field => field.Key == "role");
        Assert.Equal("Router, triage, and cross-workstation continuity", role.Value);
    }

    /// <summary>A quoted <c>Name</c> value has its quotes stripped before validation, same as any other scalar.</summary>
    [Fact]
    public void TryReadIdentity_QuotedValues_StripsQuotes()
    {
        var text = "---\nName: 'Jarvis'\nTitle: 'Chief of Staff'\nAlias: 'jar'\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("Jarvis", identity.Name);
        Assert.Equal("Chief of Staff", identity.Title);
        Assert.Equal("jar", identity.Alias);
    }

    /// <summary>An <c>adapter:</c> field reads as <see cref="PersonaIdentity.Adapter"/>, per Spec §7.2.</summary>
    [Fact]
    public void TryReadIdentity_AdapterPresent_ReadsAsAdapter()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nadapter: agency\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("agency", identity.Adapter);
    }

    /// <summary>An absent <c>adapter:</c> field yields <see langword="null"/>, and the file is still a valid Persona.</summary>
    [Fact]
    public void TryReadIdentity_AdapterAbsent_YieldsNullAndStillSucceeds()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Null(identity.Adapter);
    }

    /// <summary>A blank <c>adapter:</c> value yields <see langword="null"/>, not an empty string.</summary>
    [Fact]
    public void TryReadIdentity_AdapterBlank_YieldsNullNotEmptyString()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nadapter: '   '\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Null(identity.Adapter);
    }

    /// <summary>The <c>adapter</c> key matches case-insensitively, same as the other structural keys.</summary>
    [Fact]
    public void TryReadIdentity_CapitalizedAdapterKey_ReadsIdenticallyToLowercaseKey()
    {
        var text = "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nAdapter: agency\n---\nbody";

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var identity, out var error);

        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("agency", identity.Adapter);
    }

    /// <summary><see cref="PersonaFrontmatter.Compose"/> writes lowercase, single-quoted keys, and the result loads straight back through <see cref="PersonaFrontmatter.TryReadIdentity"/>.</summary>
    [Fact]
    public void Compose_ProducesLowercaseSingleQuotedFrontmatter_ThatRoundTripsThroughTryReadIdentity()
    {
        var identity = new PersonaIdentity("coo", "Chief of Staff", "coo", []);

        var text = PersonaFrontmatter.Compose(identity, "You are the Chief of Staff.");

        Assert.Equal(
            "---\nname: 'coo'\ntitle: 'Chief of Staff'\nalias: 'coo'\n---\nYou are the Chief of Staff.",
            text);

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var roundTripped, out var error);
        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(roundTripped);
        Assert.Equal(identity.Name, roundTripped.Name);
        Assert.Equal(identity.Title, roundTripped.Title);
        Assert.Equal(identity.Alias, roundTripped.Alias);
        Assert.Empty(roundTripped.Teams);
    }

    /// <summary>YAML's own escape for an embedded apostrophe (<c>''</c>) round-trips through Compose and back through TryReadIdentity unescaped, exactly as a hand-written file would.</summary>
    [Fact]
    public void Compose_EscapesAnApostropheInATitle_AndTryReadIdentityUnescapesItAgain()
    {
        var identity = new PersonaIdentity("coo", "The Boss's Assistant", "coo", []);

        var text = PersonaFrontmatter.Compose(identity, "body");

        Assert.Contains("title: 'The Boss''s Assistant'", text, StringComparison.Ordinal);

        PersonaFrontmatter.TryReadIdentity(text, out var roundTripped, out var error);
        Assert.Equal(string.Empty, error);
        Assert.Equal("The Boss's Assistant", roundTripped!.Title);
    }

    /// <summary>Teams become a bracketed flow list, matching the style real Persona files use, and read back as the same ordered list.</summary>
    [Fact]
    public void Compose_WritesTeamsAsABracketedFlowList_ThatReadsBackTheSameList()
    {
        var identity = new PersonaIdentity("coo", "Chief of Staff", "coo", ["Business", "Household"]);

        var text = PersonaFrontmatter.Compose(identity, "body");

        Assert.Contains("teams: ['Business', 'Household']", text, StringComparison.Ordinal);

        PersonaFrontmatter.TryReadIdentity(text, out var roundTripped, out _);
        Assert.Equal(["Business", "Household"], roundTripped!.Teams);
    }

    /// <summary>An empty Teams list is omitted entirely rather than written as a blank field, since Teams is optional and nobody hand-writes "no Teams" as a field.</summary>
    [Fact]
    public void Compose_OmitsTheTeamsLineEntirely_WhenThereAreNoTeams()
    {
        var identity = new PersonaIdentity("coo", "Chief of Staff", "coo", []);

        var text = PersonaFrontmatter.Compose(identity, "body");

        Assert.DoesNotContain("teams:", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A non-null <see cref="PersonaIdentity.Adapter"/> is emitted as an <c>adapter:</c> line
    /// after the four identity keys, and round-trips back through <see cref="PersonaFrontmatter.TryReadIdentity"/> —
    /// closing the Create-time data-loss defect at Spec §12 E-5.
    /// </summary>
    [Fact]
    public void Compose_WithAdapter_EmitsAdapterLineThatRoundTripsThroughTryReadIdentity()
    {
        var identity = new PersonaIdentity("coo", "Chief of Staff", "coo", [], "agency");

        var text = PersonaFrontmatter.Compose(identity, "body");

        Assert.Contains("adapter: 'agency'", text, StringComparison.Ordinal);

        var succeeded = PersonaFrontmatter.TryReadIdentity(text, out var roundTripped, out var error);
        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(roundTripped);
        Assert.Equal("agency", roundTripped.Adapter);
    }

    /// <summary>A <see langword="null"/> <see cref="PersonaIdentity.Adapter"/> emits no <c>adapter:</c> line at all.</summary>
    [Fact]
    public void Compose_WithNullAdapter_EmitsNoAdapterLine()
    {
        var identity = new PersonaIdentity("coo", "Chief of Staff", "coo", []);

        var text = PersonaFrontmatter.Compose(identity, "body");

        Assert.DoesNotContain("adapter:", text, StringComparison.Ordinal);
    }

    /// <summary>Rewriting one field leaves every other field's value, order and single-quoted formatting untouched.</summary>
    [Fact]
    public void WriteScalarField_UnrelatedFields_KeepValuesOrderAndFormatting()
    {
        var text = "---\nname: 'Nova'\ntitle: 'Assistant'\nalias: 'nov'\n---\nYou are Nova.";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "Chief of Staff");

        Assert.Equal(
            "---\nname: 'Nova'\ntitle: 'Chief of Staff'\nalias: 'nov'\n---\nYou are Nova.",
            result);
    }

    /// <summary>The body after the closing delimiter, including its own blank lines, is untouched by a field rewrite.</summary>
    [Fact]
    public void WriteScalarField_MultiLineBody_IsLeftUntouched()
    {
        var text = "---\nname: 'Nova'\n---\n# Nova\n\nYou are Nova.";

        var result = PersonaFrontmatter.WriteScalarField(text, "name", "Nova II");

        Assert.Equal("---\nname: 'Nova II'\n---\n# Nova\n\nYou are Nova.", result);

        var (_, body) = PersonaFrontmatter.Parse(result);
        Assert.Equal("# Nova\n\nYou are Nova.", body);
    }

    /// <summary>A new value containing an apostrophe is written with YAML's doubled-quote escape and reads back unescaped through <see cref="PersonaFrontmatter.TryReadIdentity"/>.</summary>
    [Fact]
    public void WriteScalarField_ValueWithApostrophe_RoundTripsThroughTryReadIdentity()
    {
        var text = "---\nName: Jarvis\nTitle: 'Old Title'\nAlias: jar\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "Title", "The Boss's Assistant");

        Assert.Contains("Title: 'The Boss''s Assistant'", result, StringComparison.Ordinal);

        var succeeded = PersonaFrontmatter.TryReadIdentity(result, out var identity, out var error);
        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("The Boss's Assistant", identity.Title);
    }

    /// <summary>A new value containing a colon and a hash survives quoting and reads back exactly, since single quotes make both YAML-safe.</summary>
    [Fact]
    public void WriteScalarField_ValueWithColonAndHash_RoundTripsThroughTryReadIdentity()
    {
        var text = "---\nName: Jarvis\nTitle: 'Old Title'\nAlias: jar\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "Title", "Router, triage: cross-team #1 priority");

        Assert.Contains("Title: 'Router, triage: cross-team #1 priority'", result, StringComparison.Ordinal);

        var succeeded = PersonaFrontmatter.TryReadIdentity(result, out var identity, out var error);
        Assert.True(succeeded);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(identity);
        Assert.Equal("Router, triage: cross-team #1 priority", identity.Title);
    }

    /// <summary>A folded block scalar (<c>&gt;</c>) cannot be replaced by a one-line edit, so the whole input comes back unchanged.</summary>
    [Fact]
    public void WriteScalarField_FoldedBlockScalarValue_ReturnsInputUnchanged()
    {
        var text = "---\nname: 'Nova'\nsummary: >-\n  Owns the seams between workstations.\n  Triages anything unrouted.\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "summary", "New summary");

        Assert.Equal(text, result);
    }

    /// <summary>A literal block scalar (<c>|</c>) cannot be replaced by a one-line edit, so the whole input comes back unchanged.</summary>
    [Fact]
    public void WriteScalarField_LiteralBlockScalarValue_ReturnsInputUnchanged()
    {
        var text = "---\nname: 'Nova'\naddress: |\n  Line one\n  Line two\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "address", "New address");

        Assert.Equal(text, result);
    }

    /// <summary>A block list cannot be replaced by a one-line edit, so the whole input comes back unchanged.</summary>
    [Fact]
    public void WriteScalarField_BlockListValue_ReturnsInputUnchanged()
    {
        var text = "---\nname: 'Nova'\nconsult_when:\n  - 'First reason'\n  - 'Second reason'\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "consult_when", "New value");

        Assert.Equal(text, result);
    }

    /// <summary>An absent key is inserted as a new line immediately before the closing delimiter, leaving every existing line untouched.</summary>
    [Fact]
    public void WriteScalarField_AbsentKey_InsertsNewLineBeforeClosingDelimiter()
    {
        var text = "---\nname: 'Nova'\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "Chief of Staff");

        Assert.Equal("---\nname: 'Nova'\ntitle: 'Chief of Staff'\n---\nbody", result);

        var (fields, _) = PersonaFrontmatter.Parse(result);
        var titleField = Assert.Single(fields, field => string.Equals(field.Key, "title", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Chief of Staff", titleField.Value);
    }

    /// <summary>An absent key is still inserted correctly when the frontmatter block is otherwise empty.</summary>
    [Fact]
    public void WriteScalarField_AbsentKeyInEmptyFrontmatterBlock_InsertsNewLine()
    {
        var text = "---\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "Chief of Staff");

        Assert.Equal("---\ntitle: 'Chief of Staff'\n---\nbody", result);
    }

    /// <summary>Text with no frontmatter block at all is returned unchanged.</summary>
    [Fact]
    public void WriteScalarField_NoFrontmatter_ReturnsInputUnchanged()
    {
        var text = "You are a minimal persona.";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "Anything");

        Assert.Equal(text, result);
    }

    /// <summary>An unclosed opening delimiter is not a frontmatter block, so the whole input is returned unchanged.</summary>
    [Fact]
    public void WriteScalarField_UnclosedFrontmatter_ReturnsInputUnchanged()
    {
        var text = "---\nname: 'Nova'\nYou are Nova.";

        var result = PersonaFrontmatter.WriteScalarField(text, "name", "Nova II");

        Assert.Equal(text, result);
    }

    /// <summary>CRLF input keeps every line ending as CRLF, since a Persona file's line endings belong to the Human editing it.</summary>
    [Fact]
    public void WriteScalarField_CrlfInput_StaysCrlf()
    {
        var text = "---\r\nname: 'Nova'\r\ntitle: 'Assistant'\r\nalias: 'nov'\r\n---\r\nYou are Nova.";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "Chief of Staff");

        Assert.Equal(
            "---\r\nname: 'Nova'\r\ntitle: 'Chief of Staff'\r\nalias: 'nov'\r\n---\r\nYou are Nova.",
            result);
    }

    /// <summary>LF input keeps every line ending as LF, with no CRLF introduced.</summary>
    [Fact]
    public void WriteScalarField_LfInput_StaysLf()
    {
        var text = "---\nname: 'Nova'\ntitle: 'Assistant'\nalias: 'nov'\n---\nYou are Nova.";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "Chief of Staff");

        Assert.Equal(
            "---\nname: 'Nova'\ntitle: 'Chief of Staff'\nalias: 'nov'\n---\nYou are Nova.",
            result);
    }

    /// <summary>A capitalised <c>Title:</c> line in the file matches a lowercase <c>title</c> key argument, and the file's own key casing is preserved.</summary>
    [Fact]
    public void WriteScalarField_CapitalizedKeyInFile_MatchesLowercaseKeyArgument()
    {
        var text = "---\nName: Jarvis\nTitle: 'Old Title'\nAlias: jar\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "title", "New Title");

        Assert.Equal("---\nName: Jarvis\nTitle: 'New Title'\nAlias: jar\n---\nbody", result);
    }

    /// <summary>A lowercase <c>title:</c> line in the file matches a capitalised <c>Title</c> key argument, and the file's own key casing is preserved.</summary>
    [Fact]
    public void WriteScalarField_LowercaseKeyInFile_MatchesCapitalizedKeyArgument()
    {
        var text = "---\nname: 'Nova'\ntitle: 'Old Title'\nalias: 'nov'\n---\nbody";

        var result = PersonaFrontmatter.WriteScalarField(text, "Title", "New Title");

        Assert.Equal("---\nname: 'Nova'\ntitle: 'New Title'\nalias: 'nov'\n---\nbody", result);
    }
}
