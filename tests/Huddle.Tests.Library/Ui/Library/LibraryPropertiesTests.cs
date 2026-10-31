using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>Pins the typed Properties block (front matter properties spec D-3, D-4, D-5, D-11): each type draws as the spec says, links resolve like body links, and property text is never trusted.</summary>
public sealed class LibraryPropertiesTests
{
    private const string Example = """
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

    private static readonly Dictionary<string, NotePropertyKind> ExampleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pages"] = NotePropertyKind.Number,
        ["due"] = NotePropertyKind.Date,
    };

    private static FakeLibraryNoteResolver Resolver() => new(resolveWikiLink: static (_, link) => link.Target switch
    {
        "Grace" => new LibraryReference("r", "Agents/Grace/Grace.md", Exists: true),
        "Twin" => new LibraryReference("r", "A/Twin.md", Exists: true, IsAmbiguous: true),
        _ => new LibraryReference("r", link.Target, Exists: false),
    });

    private static IRenderedComponent<LibraryProperties> Render(
        MudBunitContext ctx,
        LibraryFileServiceFixture fixture,
        string yaml,
        IReadOnlyDictionary<string, NotePropertyKind>? types = null,
        TimeProvider? clock = null)
    {
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A\n");
        ctx.Services.AddSingleton<ILibraryNoteResolver>(Resolver());
        ctx.Services.AddSingleton(clock ?? new ManualTimeProvider());
        NoteFrontmatterBlock block = NoteFrontmatter.Locate("---\n" + yaml + "\n---\n");
        IReadOnlyList<NoteProperty> properties = PropertyTyping.Apply(NoteProperties.Read(block), types ?? new Dictionary<string, NotePropertyKind>());
        IReadOnlyList<string> tags = block.Text is null ? [] : NoteTagReader.Read(block.Text, block.Form);
        return ctx.Render<LibraryProperties>(p => p
            .Add(c => c.Properties, properties)
            .Add(c => c.Path, fixture.Resolve(root, "a.md"))
            .Add(c => c.Tags, tags));
    }

    private static IElement Find(IElement scope, string selector)
    {
        IElement? found = scope.QuerySelector(selector);
        Assert.NotNull(found);
        return found;
    }

    private static IElement ValueOf(IRenderedComponent<LibraryProperties> cut, string key)
    {
        IElement row = cut.FindAll(".library-properties-row").Single(r => Find(r, ".library-properties-key").TextContent.Trim() == key);
        return Find(row, ".library-properties-value");
    }

    /// <summary>FP-T9: each type of the example draws as D-4 says, in file order.</summary>
    [Fact]
    public async Task Example_DrawsEachTypeAsSpecified()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, Example, ExampleTypes);

        Assert.Equal(
            ["title", "url", "pages", "reviewed", "created", "logged", "due", "owner", "authors", "tags", "aliases", "cssclasses", "budget", "notes", "empty"],
            cut.FindAll(".library-properties-key").Select(static k => k.TextContent.Trim()));
        Assert.Equal("Project Plan", ValueOf(cut, "title").TextContent.Trim());
        Assert.Equal("https://example.com/plan", Find(ValueOf(cut, "url"), "a").GetAttribute("href"));
        Assert.Equal("320", ValueOf(cut, "pages").TextContent.Trim());
        Assert.Equal("15 January 2024", ValueOf(cut, "created").TextContent.Trim());
        Assert.Equal("2024-01-15", Find(ValueOf(cut, "created"), "[title]").GetAttribute("title"));
        Assert.Equal("15 January 2024 09:00", ValueOf(cut, "logged").TextContent.Trim());
        Assert.Equal(["recipe", "cooking"], ValueOf(cut, "tags").QuerySelectorAll(".library-properties-chip").Select(static c => c.TextContent.Trim()));
        Assert.Equal(["Plan", "Q1 plan"], ValueOf(cut, "aliases").QuerySelectorAll(".library-properties-chip").Select(static c => c.TextContent.Trim()));
        Assert.Equal(["wide"], ValueOf(cut, "cssclasses").QuerySelectorAll(".library-properties-chip").Select(static c => c.TextContent.Trim()));
        Assert.Equal("  amount: 1200\n  currency: EUR", Find(ValueOf(cut, "budget"), "pre.library-properties-source").TextContent);
        Assert.Equal("First line.\nSecond line.", ValueOf(cut, "notes").TextContent.Trim());
        Assert.Equal(string.Empty, ValueOf(cut, "empty").TextContent.Trim());
    }

    /// <summary>FP-T9: a checkbox is a read-only icon named Yes or No, never an input.</summary>
    [Theory]
    [InlineData("true", "Yes")]
    [InlineData("false", "No")]
    public async Task Checkbox_IsAnIconNotAnInput(string value, string label)
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "done: " + value);

        IElement icon = Find(ValueOf(cut, "done"), "[aria-label]");
        Assert.Equal(label, icon.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("input"));
    }

    /// <summary>FP-T9: a Mismatch shows the value as written and a warning mark whose tooltip names the expected type.</summary>
    [Fact]
    public async Task Mismatch_ShowsValueAndWarning()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "due: next week", ExampleTypes);

        IElement value = ValueOf(cut, "due");
        Assert.Equal("next week", Find(value, ".library-properties-mismatch-value").TextContent.Trim());
        Assert.Equal("Type mismatch: expected Date", Find(value, ".library-properties-mismatch").GetAttribute("title"));
    }

    /// <summary>D-4: text is never Markdown, so asterisks show as typed; an unquoted wikilink shows as its source.</summary>
    [Fact]
    public async Task Text_IsNotMarkdown_AndAnUnquotedWikilinkIsSource()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "note: '**bold**'\nrelated: [[Grace]]");

        Assert.Equal("**bold**", ValueOf(cut, "note").TextContent.Trim());
        Assert.Empty(ValueOf(cut, "related").QuerySelectorAll("a"));
        Assert.Equal("[[Grace]]", Find(ValueOf(cut, "related"), "pre.library-properties-source").TextContent);
    }

    /// <summary>FP-T10: a quoted wikilink renders as the body's link, with the resolver's href; a missing target gets the missing class and title; a label is the alias.</summary>
    [Fact]
    public async Task Links_ResolveLikeBodyLinks()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "owner: \"[[Grace]]\"\nreviewer: \"[[Nobody]]\"\nboth: \"[[Grace|The boss]] and [[Nobody]]\"\nsame: \"[[Twin]]\"");

        IElement owner = Find(ValueOf(cut, "owner"), "a.library-ref");
        Assert.Equal("?library=r/Agents/Grace/Grace.md", owner.GetAttribute("href"));
        Assert.Equal("Grace", owner.TextContent);
        Assert.False(owner.ClassList.Contains("library-ref-missing"));

        IElement missing = Find(ValueOf(cut, "reviewer"), "a.library-ref.library-ref-missing");
        Assert.Equal("No note named \"Nobody\". Click to create it.", missing.GetAttribute("title"));

        IElement both = ValueOf(cut, "both");
        Assert.Equal(["The boss", "Nobody"], both.QuerySelectorAll("a.library-ref").Select(static a => a.TextContent));
        Assert.Equal("The boss and Nobody", both.TextContent.Trim());

        Assert.Equal("Several notes match \"Twin\"; showing the closest.", Find(ValueOf(cut, "same"), "a.library-ref").GetAttribute("title"));
    }

    /// <summary>FP-T10: a list item that is a link is a link chip; a plain item is an ordinary chip.</summary>
    [Fact]
    public async Task ListItem_Link_IsALinkChip()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "authors:\n  - Ada\n  - \"[[Grace]]\"");

        IReadOnlyList<IElement> chips = [.. ValueOf(cut, "authors").QuerySelectorAll(".library-properties-chip")];
        Assert.Equal(2, chips.Count);
        Assert.Null(chips[0].QuerySelector("a"));
        Assert.Equal("Grace", Find(chips[1], "a.library-ref").TextContent);
    }

    /// <summary>FP-T13: only http, https and mailto URLs are links; any other scheme is plain text with no href.</summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/x")]
    public async Task Url_OtherScheme_IsPlainText(string url)
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "link: \"" + url + "\"");

        IElement value = ValueOf(cut, "link");
        Assert.Empty(value.QuerySelectorAll("a"));
        Assert.Equal(url, value.TextContent.Trim());
        Assert.DoesNotContain("href=", value.InnerHtml, StringComparison.Ordinal);
    }

    /// <summary>D-4: an http, https or mailto URL is a link that opens outside the app.</summary>
    [Theory]
    [InlineData("https://example.com/a")]
    [InlineData("http://example.com/a")]
    [InlineData("mailto:ada@example.com")]
    public async Task Url_AllowedScheme_IsALink(string url)
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "link: \"" + url + "\"");

        IElement anchor = Find(ValueOf(cut, "link"), "a");
        Assert.Equal(url, anchor.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", anchor.GetAttribute("rel"));
    }

    /// <summary>FP-T13: a <c>cssclasses</c> value reaches no <c>class</c> attribute; it is only text in a chip.</summary>
    [Fact]
    public async Task CssClasses_NeverBecomeAClass()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "cssclasses:\n  - zz-evil\n  - another");

        Assert.DoesNotContain(cut.FindAll("[class]"), static e => e.ClassList.Any(static c => c.Contains("zz-evil", StringComparison.Ordinal) || c == "another"));
        Assert.Equal(["zz-evil", "another"], ValueOf(cut, "cssclasses").QuerySelectorAll(".library-properties-chip").Select(static c => c.TextContent.Trim()));
    }

    /// <summary>FP-T13: bidirectional control characters are removed from keys and values, and a long Text value is cut with an ellipsis.</summary>
    [Fact]
    public async Task Text_BidiRemoved_AndLongValueIsCut()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();
        string bidi = new([(char)0x202E, (char)0x200F, (char)0x2066]);
        string yaml = "k" + bidi + "ey: \"a" + bidi + "b\"\nlong: \"" + new string('x', 2500) + "\"";

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, yaml);

        Assert.Equal(["key", "long"], cut.FindAll(".library-properties-key").Select(static k => k.TextContent.Trim()));
        Assert.Equal("ab", ValueOf(cut, "key").TextContent.Trim());
        Assert.Equal(new string('x', 2000) + "…", ValueOf(cut, "long").TextContent.Trim());
    }

    /// <summary>FP-T14: a date and time with <c>Z</c> or an offset is converted to the injected clock's local zone; one without an offset shows as written.</summary>
    [Fact]
    public async Task DateTime_OffsetConverted_WallTimeUntouched()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();
        TimeZoneInfo plusTwo = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "test+2", "test+2");
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), plusTwo);

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "zulu: 2024-01-15T09:00:00Z\nshifted: 2024-01-15T09:00:00-03:00\nwall: 2024-01-15T09:00:00", clock: clock);

        Assert.Equal("15 January 2024 11:00", ValueOf(cut, "zulu").TextContent.Trim());
        Assert.Equal("15 January 2024 14:00", ValueOf(cut, "shifted").TextContent.Trim());
        Assert.Equal("15 January 2024 09:00", ValueOf(cut, "wall").TextContent.Trim());
        Assert.Equal(new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Unspecified).ToString("d MMMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture), ValueOf(cut, "wall").TextContent.Trim());
        Assert.Equal("2024-01-15T09:00:00Z", Find(ValueOf(cut, "zulu"), "[title]").GetAttribute("title"));
    }

    /// <summary>D-3: a date assigned the Date and time type reads as midnight.</summary>
    [Fact]
    public async Task DateAssignedDateTime_ReadsAsMidnight()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();
        Dictionary<string, NotePropertyKind> types = new(StringComparer.OrdinalIgnoreCase) { ["d"] = NotePropertyKind.DateTime };

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, "d: 2024-01-15", types);

        Assert.Equal("15 January 2024 00:00", ValueOf(cut, "d").TextContent.Trim());
    }

    /// <summary>Fix 3 (D-11): the source block of an unsupported value loses bidi controls and is cut at 2,000 characters like any other value.</summary>
    [Fact]
    public async Task Source_BidiRemoved_AndLongValueIsCut()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = new();
        string bidi = new([(char)0x202E, (char)0x2067]);
        string yaml = "nested:\n  a: \"x" + bidi + "y\"\n  pad: " + new string('z', 2500);

        IRenderedComponent<LibraryProperties> cut = Render(ctx, fixture, yaml);

        string source = Find(ValueOf(cut, "nested"), "pre.library-properties-source").TextContent;
        Assert.DoesNotContain((char)0x202E, source);
        Assert.DoesNotContain((char)0x2067, source);
        Assert.Equal(("  a: \"xy\"\n  pad: " + new string('z', 2500))[..2000] + "…", source);
    }
}
