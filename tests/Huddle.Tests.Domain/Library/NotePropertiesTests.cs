using Agency.Huddle.App.Library;
using Agency.Huddle.Seeder.Scenarios;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the note property reader: D-2 step 3 (inference from the value), D-6 (a nested value is Unsupported) and D-10 (a malformed entry spoils only itself).</summary>
public sealed class NotePropertiesTests
{
    private static IReadOnlyList<NoteProperty> Read(string yaml) =>
        NoteProperties.Read(NoteFrontmatter.Locate("---\n" + yaml + "\n---\n"));

    private static NoteProperty One(string yaml) => Assert.Single(Read(yaml));

    /// <summary>FP-T2: each row of D-2's inference table, with its boundaries, infers the stated type.</summary>
    [Theory]
    [InlineData("k:", NotePropertyKind.Text)]
    [InlineData("k: ~", NotePropertyKind.Text)]
    [InlineData("k: null", NotePropertyKind.Text)]
    [InlineData("k: Null", NotePropertyKind.Text)]
    [InlineData("k: true", NotePropertyKind.Checkbox)]
    [InlineData("k: False", NotePropertyKind.Checkbox)]
    [InlineData("k: TRUE", NotePropertyKind.Checkbox)]
    [InlineData("k: yes", NotePropertyKind.Text)]
    [InlineData("k: no", NotePropertyKind.Text)]
    [InlineData("k: on", NotePropertyKind.Text)]
    [InlineData("k: off", NotePropertyKind.Text)]
    [InlineData("k: 320", NotePropertyKind.Number)]
    [InlineData("k: -4", NotePropertyKind.Number)]
    [InlineData("k: 1.5", NotePropertyKind.Number)]
    [InlineData("k: 1e3", NotePropertyKind.Number)]
    [InlineData("k: 1.50", NotePropertyKind.Number)]
    [InlineData("k: 12abc", NotePropertyKind.Text)]
    [InlineData("k: 2024-01-15", NotePropertyKind.Date)]
    [InlineData("k: 2026-02-30", NotePropertyKind.Text)]
    [InlineData("k: 2024-01-15T09:00", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15T09:00:00", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15T09:00:00.250", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15T09:00:00Z", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15T09:00:00+02:00", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15 09:00", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15T25:00", NotePropertyKind.Text)]
    [InlineData("k: \"true\"", NotePropertyKind.Text)]
    [InlineData("k: \"320\"", NotePropertyKind.Text)]
    [InlineData("k: \"2024-01-15\"", NotePropertyKind.Text)]
    [InlineData("k: 'true'", NotePropertyKind.Text)]
    [InlineData("k: [a, b]", NotePropertyKind.List)]
    [InlineData("k:\n  - a\n  - b", NotePropertyKind.List)]
    [InlineData("k:\n- a\n- b", NotePropertyKind.List)]
    [InlineData("k: {a: 1}", NotePropertyKind.Unsupported)]
    [InlineData("k:\n  a: 1", NotePropertyKind.Unsupported)]
    [InlineData("k:\n  - a: 1", NotePropertyKind.Unsupported)]
    [InlineData("k:\n  - [a, b]", NotePropertyKind.Unsupported)]
    [InlineData("k: |\n  true", NotePropertyKind.Text)]
    [InlineData("k: >\n  true", NotePropertyKind.Text)]
    [InlineData("k: just words", NotePropertyKind.Text)]
    public void Read_InferenceTable(string yaml, NotePropertyKind expected) =>
        Assert.Equal(expected, One(yaml).Kind);

    /// <summary>FP-T2: a number is kept exactly as written, never reformatted.</summary>
    [Fact]
    public void Read_Number_KeepsItsText()
    {
        Assert.Equal("1.50", One("k: 1.50").Value);
    }

    /// <summary>FP-T2: a quoted value loses its quotes and keeps its text.</summary>
    [Theory]
    [InlineData("k: \"a b\"", "a b")]
    [InlineData("k: 'it''s'", "it's")]
    [InlineData("k: \"say \\\"hi\\\"\"", "say \"hi\"")]
    public void Read_Quoted_ValueWithoutQuotes(string yaml, string expected) =>
        Assert.Equal(expected, One(yaml).Value);

    /// <summary>FP-T2: a literal block scalar keeps its line breaks and a folded one joins its lines, as YAML does.</summary>
    [Fact]
    public void Read_BlockScalars_KeepOrFoldLines()
    {
        Assert.Equal("First line.\nSecond line.", One("k: |\n  First line.\n  Second line.").Value);
        Assert.Equal("First line. Second line.", One("k: >\n  First line.\n  Second line.").Value);
    }

    /// <summary>FP-T2: list items lose their quotes; an empty value is an empty Text property.</summary>
    [Fact]
    public void Read_ListItemsAndEmpty_Shapes()
    {
        NoteProperty list = One("k: [Plan, \"Q1 plan\"]");
        Assert.Equal(["Plan", "Q1 plan"], list.Items);

        NoteProperty empty = One("k:");
        Assert.Equal(NotePropertyShape.Empty, empty.Shape);
        Assert.Equal(string.Empty, empty.Value);
    }

    /// <summary>FP-T6: a map keeps its indented lines as the entry's source and adds no rows of its own.</summary>
    [Fact]
    public void Read_Map_IsUnsupportedWithSourceAndNoExtraRows()
    {
        IReadOnlyList<NoteProperty> properties = Read("budget:\n  amount: 1200\n  currency: EUR\nafter: x");

        Assert.Equal(["budget", "after"], properties.Select(static p => p.Key));
        Assert.Equal(NotePropertyKind.Unsupported, properties[0].Kind);
        Assert.Equal("  amount: 1200\n  currency: EUR", properties[0].Source);
    }

    /// <summary>FP-T6: a flow map, a list of maps, a list of lists and an unquoted wikilink are each Unsupported with their source.</summary>
    [Theory]
    [InlineData("k: {a: 1}", "{a: 1}")]
    [InlineData("k:\n  - a: 1\n  - b: 2", "  - a: 1\n  - b: 2")]
    [InlineData("k:\n  - [a]", "  - [a]")]
    [InlineData("k: [[A]]", "[[A]]")]
    [InlineData("k:\n  - [[A]]", "  - [[A]]")]
    public void Read_NestedValue_IsUnsupportedWithItsSource(string yaml, string source)
    {
        NoteProperty property = One(yaml);

        Assert.Equal(NotePropertyKind.Unsupported, property.Kind);
        Assert.Equal(source, property.Source);
    }

    /// <summary>FP-T6: a quoted wikilink is an ordinary string, not nested.</summary>
    [Fact]
    public void Read_QuotedWikilink_IsText()
    {
        NoteProperty property = One("k: \"[[Grace]]\"");

        Assert.Equal(NotePropertyKind.Text, property.Kind);
        Assert.Equal("[[Grace]]", property.Value);
    }

    /// <summary>FP-T7: an unclosed quote makes that entry Unsupported and leaves its neighbours typed.</summary>
    [Fact]
    public void Read_UnclosedQuote_SpoilsOnlyItsEntry()
    {
        IReadOnlyList<NoteProperty> properties = Read("a: 1\nbroken: \"unclosed\nc: true");

        Assert.Equal(
            [NotePropertyKind.Number, NotePropertyKind.Unsupported, NotePropertyKind.Checkbox],
            properties.Select(static p => p.Kind));
        Assert.Equal("\"unclosed", properties[1].Source);
    }

    /// <summary>FP-T7: an unclosed flow list and a tab in an entry's indentation each spoil only that entry.</summary>
    [Theory]
    [InlineData("bad: [a, b")]
    [InlineData("bad:\n\t- a")]
    public void Read_OtherMalformedEntries_AreUnsupported(string entry)
    {
        IReadOnlyList<NoteProperty> properties = Read("a: 1\n" + entry + "\nz: 2");

        Assert.Equal(
            [NotePropertyKind.Number, NotePropertyKind.Unsupported, NotePropertyKind.Number],
            properties.Select(static p => p.Kind));
    }

    /// <summary>FP-T7: a repeated key keeps the last entry, at the last one's place.</summary>
    [Fact]
    public void Read_RepeatedKey_KeepsTheLastAtItsPlace()
    {
        IReadOnlyList<NoteProperty> properties = Read("status: draft\nother: 1\nstatus: final");

        Assert.Equal(["other", "status"], properties.Select(static p => p.Key));
        Assert.Equal("final", properties[1].Value);
    }

    /// <summary>FP-T1: the example (the seeded <c>AllTypes.md</c>) with its types file reads to the table in the spec, key by key.</summary>
    [Fact]
    public void Example_WithItsTypesFile_ReadsToTheSpecTable()
    {
        IReadOnlyDictionary<string, NotePropertyKind> types = PropertyTypes.Parse(SoftwareCoLibrary.PropertyTypesJson);

        IReadOnlyList<NoteProperty> properties = PropertyTyping.Apply(NoteProperties.Read(NoteFrontmatter.Locate(SoftwareCoLibrary.AllTypesNote)), types);

        Assert.Equal(
            [
                ("title", NotePropertyKind.Text, "Project Plan"),
                ("url", NotePropertyKind.Text, "https://example.com/plan"),
                ("pages", NotePropertyKind.Number, "320"),
                ("reviewed", NotePropertyKind.Checkbox, "true"),
                ("created", NotePropertyKind.Date, "2024-01-15"),
                ("logged", NotePropertyKind.DateTime, "2024-01-15T09:00:00"),
                ("due", NotePropertyKind.Mismatch, "next week"),
                ("owner", NotePropertyKind.Text, "[[Architecture]]"),
                ("authors", NotePropertyKind.List, string.Empty),
                ("tags", NotePropertyKind.Tags, string.Empty),
                ("aliases", NotePropertyKind.Aliases, string.Empty),
                ("cssclasses", NotePropertyKind.List, string.Empty),
                ("budget", NotePropertyKind.Unsupported, string.Empty),
                ("notes", NotePropertyKind.Text, "First line.\nSecond line."),
                ("empty", NotePropertyKind.Text, string.Empty),
            ],
            properties.Select(static p => (p.Key, p.Kind, p.Value)));
        Assert.Equal(NotePropertyKind.Date, properties[6].Expected);
        Assert.Equal(["Ada", "[[Architecture]]"], properties[8].Items);
        Assert.Equal(["recipe", "cooking"], properties[9].Items);
        Assert.Equal(["Plan", "Q1 plan"], properties[10].Items);
        Assert.Equal(["wide"], properties[11].Items);
        Assert.Equal("  amount: 1200\n  currency: EUR", properties[12].Source);
        Assert.Equal(NotePropertyShape.Empty, properties[14].Shape);
    }

    /// <summary>The example note, read before any types file: every key in file order with the type its value implies.</summary>
    [Fact]
    public void Read_TheExample_InferredKinds()
    {
        const string Yaml = """
            title: Project Plan
            url: https://example.com/plan
            pages: 320
            reviewed: true
            created: 2024-01-15
            logged: 2024-01-15T09:00:00
            due: next week
            owner: "[[Grace]]"
            authors:
              - Ada
              - "[[Grace]]"
            tags:
              - recipe
              - cooking
            aliases: [Plan, Q1 plan]
            cssclasses:
              - wide
            budget:
              amount: 1200
              currency: EUR
            notes: |
              First line.
              Second line.
            empty:
            """;

        IReadOnlyList<NoteProperty> properties = Read(Yaml);

        Assert.Equal(
            ["title", "url", "pages", "reviewed", "created", "logged", "due", "owner", "authors", "tags", "aliases", "cssclasses", "budget", "notes", "empty"],
            properties.Select(static p => p.Key));
        Assert.Equal(
            [
                NotePropertyKind.Text, NotePropertyKind.Text, NotePropertyKind.Number, NotePropertyKind.Checkbox,
                NotePropertyKind.Date, NotePropertyKind.DateTime, NotePropertyKind.Text, NotePropertyKind.Text,
                NotePropertyKind.List, NotePropertyKind.List, NotePropertyKind.List, NotePropertyKind.List,
                NotePropertyKind.Unsupported, NotePropertyKind.Text, NotePropertyKind.Text,
            ],
            properties.Select(static p => p.Kind));
        Assert.Equal("First line.\nSecond line.", properties[13].Value);
    }
}
