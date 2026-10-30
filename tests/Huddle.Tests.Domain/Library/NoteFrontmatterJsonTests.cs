using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins front matter written as JSON (front matter properties spec D-9): the boundary, the properties it reads to, and its <c>tags</c>.</summary>
public sealed class NoteFrontmatterJsonTests
{
    private const string Yaml = """
        ---
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
        empty:
        ---

        # Body
        """;

    private const string Json = """
        {
          "title": "Project Plan",
          "url": "https://example.com/plan",
          "pages": 320,
          "reviewed": true,
          "created": "2024-01-15",
          "logged": "2024-01-15T09:00:00",
          "due": "next week",
          "owner": "[[Grace]]",
          "authors": ["Ada", "[[Grace]]"],
          "tags": ["recipe", "cooking"],
          "aliases": ["Plan", "Q1 plan"],
          "cssclasses": ["wide"],
          "empty": null
        }

        # Body
        """;

    private static readonly Dictionary<string, NotePropertyKind> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["created"] = NotePropertyKind.Date,
        ["logged"] = NotePropertyKind.DateTime,
    };

    private static IEnumerable<string> Project(string text) =>
        PropertyTyping.Apply(NoteProperties.Read(NoteFrontmatter.Locate(text)), Types).Select(static p => $"{p.Key}|{p.Kind}|{p.Shape}|{p.Value}|{string.Join(",", p.Items)}");

    /// <summary>FP-T8: the JSON form reads to the same properties as its YAML twin, when the types file names the two keys that YAML writes as unquoted dates.</summary>
    [Fact]
    public void Json_ReadsToTheSamePropertiesAsItsYamlTwin()
    {
        Assert.Equal(Project(Yaml), Project(Json));
    }

    /// <summary>FP-T8: the JSON object is the block, the form says so, and the body starts after its closing line.</summary>
    [Fact]
    public void Locate_Json_ReportsTheFormAndTheBody()
    {
        const string Text = "{\"a\": 1}\nbody";

        NoteFrontmatterBlock block = NoteFrontmatter.Locate(Text);

        Assert.Equal("{\"a\": 1}", block.Text);
        Assert.Equal(NoteFrontmatterForm.Json, block.Form);
        Assert.Equal("body", Text[block.BodyStart..]);
        Assert.Equal(("{\"a\": 1}", 9), NoteFrontmatter.Split(Text));
    }

    /// <summary>FP-T8: a byte order mark and a CRLF break before the body change nothing.</summary>
    [Fact]
    public void Locate_Json_AfterABom_WithCrLf()
    {
        const string Text = "﻿{\"a\": 1}\r\n\r\nbody";

        NoteFrontmatterBlock block = NoteFrontmatter.Locate(Text);

        Assert.Equal("{\"a\": 1}", block.Text);
        Assert.Equal("\r\nbody", Text[block.BodyStart..]);
    }

    /// <summary>FP-T8: a leading <c>{</c> that is not one closed JSON object, or that shares its closing line with other text, is body.</summary>
    [Theory]
    [InlineData("{ not json at all\nbody")]
    [InlineData("{\"a\": 1\nbody")]
    [InlineData("{\"a\": 1} and then text\nbody")]
    [InlineData("{\"a\": [1, 2}\nbody")]
    [InlineData("{\"a\": 1}}\nbody")]
    public void Locate_NotAClosedObject_IsBody(string text)
    {
        NoteFrontmatterBlock block = NoteFrontmatter.Locate(text);

        Assert.Null(block.Text);
        Assert.Equal(0, block.BodyStart);
    }

    /// <summary>FP-T8: the JSON members read by the same rules: arrays of scalars are lists, objects and arrays of objects are Unsupported, and numbers and booleans infer.</summary>
    [Fact]
    public void Json_Members_ReadByTheSameRules()
    {
        IReadOnlyList<NoteProperty> properties = NoteProperties.Read(NoteFrontmatter.Locate(
            "{\"list\": [\"a\", 2, true], \"map\": {\"x\": 1}, \"maps\": [{\"x\": 1}], \"n\": 1.50, \"b\": false, \"s\": \"true\", \"d\": \"2026-02-30\", \"dt\": \"2024-01-15\"}\n"));

        Assert.Equal(
            [NotePropertyKind.List, NotePropertyKind.Unsupported, NotePropertyKind.Unsupported, NotePropertyKind.Number, NotePropertyKind.Checkbox, NotePropertyKind.Text, NotePropertyKind.Text, NotePropertyKind.Text],
            properties.Select(static p => p.Kind));
        Assert.Equal(["a", "2", "true"], properties[0].Items);
        Assert.Equal("{\"x\": 1}", properties[1].Source);
        Assert.Equal("1.50", properties[3].Value);
    }

    /// <summary>FP-T8: a JSON <c>tags</c> member reaches the tag reader as a flow list does, and the note's tag pass.</summary>
    [Fact]
    public void Json_Tags_ReachTheTagReader()
    {
        NoteFrontmatterBlock block = NoteFrontmatter.Locate(Json);

        Assert.NotNull(block.Text);
        Assert.Equal(["recipe", "cooking"], NoteTagReader.Read(block.Text, block.Form));
        Assert.Equal(["recipe", "cooking"], NoteTags.Of(Json));
    }

    /// <summary>FP-T8: the JSON <c>tags</c> member follows the YAML rules: a value that is not a string, or not a tag, is skipped, and a repeated member keeps the last.</summary>
    [Theory]
    [InlineData("{\"tags\": [\"a\", 1, true, null, \"b c\", \"#d\"]}", "a|d")]
    [InlineData("{\"tags\": \"solo\"}", "solo")]
    [InlineData("{\"tags\": [\"a\"], \"tags\": [\"z\"]}", "z")]
    [InlineData("{\"tag\": [\"a\"]}", "")]
    [InlineData("{\"tags\": {\"a\": 1}}", "")]
    public void Json_Tags_FollowTheYamlRules(string block, string expected) =>
        Assert.Equal(expected, string.Join("|", NoteTagReader.Read(block, NoteFrontmatterForm.Json)));

    /// <summary>R2: a JSON string is a quoted value, so a date-shaped string is Text; under an explicit date type it is a Date, as a quoted YAML value is.</summary>
    [Fact]
    public void Json_DateShapedString_IsTextUnlessTheKeyIsTyped()
    {
        NoteFrontmatterBlock block = NoteFrontmatter.Locate("{\"d\": \"2024-01-15\", \"t\": \"2024-01-15T09:00\"}\n");
        NoteFrontmatterBlock yaml = NoteFrontmatter.Locate("---\nd: \"2024-01-15\"\nt: \"2024-01-15T09:00\"\n---\n");
        Dictionary<string, NotePropertyKind> typed = new(StringComparer.Ordinal) { ["d"] = NotePropertyKind.Date, ["t"] = NotePropertyKind.DateTime };

        Assert.Equal([NotePropertyKind.Text, NotePropertyKind.Text], NoteProperties.Read(block).Select(static p => p.Kind));
        Assert.Equal([NotePropertyKind.Date, NotePropertyKind.DateTime], PropertyTyping.Apply(NoteProperties.Read(block), typed).Select(static p => p.Kind));
        Assert.Equal(
            PropertyTyping.Apply(NoteProperties.Read(yaml), typed).Select(static p => p.Kind),
            PropertyTyping.Apply(NoteProperties.Read(block), typed).Select(static p => p.Kind));
    }

    /// <summary>Fix 7: a YAML block whose content is a bare JSON-looking object is read as YAML, so the tag index and the Properties block agree: neither finds anything.</summary>
    [Fact]
    public void YamlBlock_LookingLikeJson_IsReadAsYaml()
    {
        const string Text = "---\n{\"tags\": [\"j\"]}\n---\n\nbody";

        Assert.Empty(NoteTags.Of(Text));
        Assert.Empty(NoteProperties.Read(NoteFrontmatter.Locate(Text)));
    }
}