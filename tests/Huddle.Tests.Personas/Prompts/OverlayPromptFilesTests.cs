using System.Text.Json;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Prompts;

/// <summary>
/// Pins the overlay prompt file format (Spec §7.3, D-40, SB-R22): the parser against one embedded
/// fixture, and the shipped <c>Overlays/second-brain/prompts/*.md</c> files against the catalog.
/// </summary>
public sealed class OverlayPromptFilesTests
{
    private const string FixtureResource = "OverlayFixtures/test.md";
    private const string ShippedPrefix = "Overlays/second-brain/prompts/";

    /// <summary>Reads the fixture embedded in this test assembly.</summary>
    private static string ReadFixture()
    {
        using Stream stream = typeof(OverlayPromptFilesTests).Assembly.GetManifestResourceStream(FixtureResource)
            ?? throw new InvalidOperationException($"Fixture '{FixtureResource}' is not embedded.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The names of the shipped overlay prompt files embedded in the app assembly.</summary>
    private static List<string> ShippedResourceNames() =>
        [.. typeof(PromptCatalog).Assembly.GetManifestResourceNames()
            .Where(static name => name.Replace('\\', '/').StartsWith(ShippedPrefix, StringComparison.Ordinal)
                && name.EndsWith(".md", StringComparison.Ordinal))];

    /// <summary>The fixture's frontmatter scalars become the definition's identity, texts and timing.</summary>
    [Fact]
    public void Parse_Fixture_ReadsFrontmatterKeys()
    {
        PromptDefinition definition = OverlayPromptFiles.Parse("test.md", ReadFixture());

        Assert.Equal("fixture.sample", definition.Key); // fixture line 2: key: fixture.sample
        Assert.Equal("Sample label", definition.Label); // line 3
        Assert.Equal("Keep {{alpha}} in the text.", definition.HelperText); // line 4
        Assert.Equal(PromptTiming.Live, definition.Timing); // line 7: timing: Live
    }

    /// <summary>The fixture's two list keys parse into string lists in order, quotes removed.</summary>
    [Fact]
    public void Parse_Fixture_ReadsPlaceholderLists()
    {
        PromptDefinition definition = OverlayPromptFiles.Parse("test.md", ReadFixture());

        Assert.Equal(["{{alpha}}", "{{beta}}"], definition.Placeholders); // line 5
        Assert.Equal(["{{alpha}}"], definition.RequiredPlaceholders); // line 6
    }

    /// <summary>
    /// The body is everything after the closing fence, LF-normalised, trailing blank lines trimmed; the
    /// blank middle line stays.
    /// </summary>
    [Fact]
    public void Parse_Fixture_BodyIsLfNormalisedAndTrimmed()
    {
        PromptDefinition definition = OverlayPromptFiles.Parse("test.md", ReadFixture());

        // Fixture body: "First line uses {{alpha}}." / blank / "Third line mentions {{beta}}." / blank, then end of file.
        Assert.Equal("First line uses {{alpha}}.\n\nThird line mentions {{beta}}.", definition.Default);
        Assert.DoesNotContain('\r', definition.Default);
    }

    /// <summary>A CRLF file parses to the same definition as its LF twin, whatever the checkout's endings.</summary>
    [Fact]
    public void Parse_CrLfText_EqualsLfText()
    {
        string lf = ReadFixture().ReplaceLineEndings("\n");
        string crlf = lf.ReplaceLineEndings("\r\n");

        PromptDefinition fromLf = OverlayPromptFiles.Parse("test.md", lf);
        PromptDefinition fromCrLf = OverlayPromptFiles.Parse("test.md", crlf);

        Assert.Equal(fromLf.Default, fromCrLf.Default);
        Assert.Equal(fromLf.Key, fromCrLf.Key);
        Assert.Equal(fromLf.Placeholders, fromCrLf.Placeholders);
    }

    /// <summary><c>relied: true</c> sets a warning; <c>relied: false</c> leaves it null.</summary>
    [Fact]
    public void Parse_Relied_MapsToReliedWarning()
    {
        string relied = ReadFixture();
        string notRelied = relied.Replace("relied: true", "relied: false", StringComparison.Ordinal);

        Assert.False(string.IsNullOrWhiteSpace(OverlayPromptFiles.Parse("test.md", relied).ReliedWarning)); // fixture line 8: relied: true
        Assert.Null(OverlayPromptFiles.Parse("test.md", notRelied).ReliedWarning);
    }

    /// <summary><c>timing: NextSession</c> parses to <see cref="PromptTiming.NextSession"/>.</summary>
    [Fact]
    public void Parse_TimingNextSession_ParsesToNextSession()
    {
        string text = ReadFixture().Replace("timing: Live", "timing: NextSession", StringComparison.Ordinal);

        Assert.Equal(PromptTiming.NextSession, OverlayPromptFiles.Parse("test.md", text).Timing);
    }

    /// <summary>A file with no frontmatter fence is refused, naming the file.</summary>
    [Fact]
    public void Parse_NoFrontmatter_ThrowsNamingFile()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => OverlayPromptFiles.Parse("broken.md", "just a body"));

        Assert.Contains("broken.md", error.Message, StringComparison.Ordinal);
    }

    /// <summary>One definition comes back per embedded shipped prompt file.</summary>
    [Fact]
    public void Load_ReturnsOneDefinitionPerEmbeddedFile()
    {
        IReadOnlyList<PromptDefinition> loaded = OverlayPromptFiles.Load();

        Assert.Equal(ShippedResourceNames().Count, loaded.Count);
    }

    /// <summary>No two prompts in the catalog share a key.</summary>
    [Fact]
    public void Catalog_Keys_AreUnique()
    {
        List<string> duplicates = [.. PromptCatalog.All
            .GroupBy(static prompt => prompt.Key, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)];

        Assert.Empty(duplicates);
    }

    /// <summary>Every shipped overlay prompt keeps required placeholders inside its placeholders and its body.</summary>
    [Fact]
    public void Load_EveryDefinition_RequiredPlaceholdersAreDeclaredAndPresentInBody()
    {
        List<string> problems = [];
        foreach (PromptDefinition definition in OverlayPromptFiles.Load())
        {
            problems.AddRange(definition.RequiredPlaceholders
                .Where(required => !definition.Placeholders.Contains(required, StringComparer.Ordinal))
                .Select(required => $"{definition.Key}: {required} not in placeholders"));
            problems.AddRange(definition.RequiredPlaceholders
                .Where(required => !definition.Default.Contains(required, StringComparison.Ordinal))
                .Select(required => $"{definition.Key}: {required} not in body"));
        }

        Assert.Empty(problems);
    }

    /// <summary>Every shipped overlay key is in <see cref="PromptCatalog.All"/>.</summary>
    [Fact]
    public void Catalog_ContainsEveryOverlayKey()
    {
        List<string> missing = [.. OverlayPromptFiles.Load()
            .Select(static definition => definition.Key)
            .Where(key => !PromptCatalog.All.Any(prompt => string.Equals(prompt.Key, key, StringComparison.Ordinal)))];

        Assert.Empty(missing);
    }

    /// <summary>The default of <c>getHelp.rawChanged</c> is the heading line then the list placeholder, character for character.</summary>
    [Fact]
    public void RawChanged_DefaultText_IsHeadingThenFilesPlaceholder()
    {
        Assert.Equal("Raw files changed since filing\n{{files}}", PromptCatalog.Get("getHelp.rawChanged").Default.ReplaceLineEndings("\n"));
    }

    /// <summary>Every shipped overlay key is in <c>prompts.default.json</c> (regenerate it when a file is added).</summary>
    [Fact]
    public void DefaultsFile_ContainsEveryOverlayKey()
    {
        using JsonDocument defaults = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts.default.json")));

        List<string> missing = [.. OverlayPromptFiles.Load()
            .Select(static definition => definition.Key)
            .Where(key => !defaults.RootElement.TryGetProperty(key, out _))];

        Assert.Empty(missing);
    }
}
