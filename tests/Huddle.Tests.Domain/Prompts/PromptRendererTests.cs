namespace Agency.Huddle.Tests.Prompts;

using Agency.Huddle.App.Prompts;

/// <summary>
/// Behavioral tests for <see cref="PromptRenderer"/>: substitution of well-formed placeholders,
/// verbatim preservation of everything else, and the single-pass guarantee that a substituted
/// value is never itself re-expanded.
/// </summary>
public sealed class PromptRendererTests
{
    /// <summary>A placeholder with a supplied value is replaced by that value.</summary>
    [Fact]
    public void Render_KnownPlaceholder_IsSubstituted()
    {
        var result = PromptRenderer.Render("Hello {{name}}", new Dictionary<string, string> { ["{{name}}"] = "Nova" });

        Assert.Equal("Hello Nova", result);
    }

    /// <summary>
    /// A placeholder with no matching entry in <c>values</c> is left in the output verbatim, braces
    /// included, so a typo'd or unconfigured placeholder stays visible in the prompt rather than
    /// silently erasing the surrounding text.
    /// </summary>
    [Fact]
    public void Render_UnknownPlaceholder_IsLeftVerbatim()
    {
        var result = PromptRenderer.Render("Hi {{nope}}", new Dictionary<string, string>());

        Assert.Equal("Hi {{nope}}", result);
    }

    /// <summary>
    /// The exact angle-bracket documentation line from <c>getHelp.messages</c> passes through
    /// untouched — the specific collision the <c>{{...}}</c> syntax was chosen to avoid, pinned here
    /// with the literal string so a future change cannot silently regress it.
    /// </summary>
    [Fact]
    public void Render_AngleBracketText_IsUnchanged()
    {
        const string template = "[Room: <name> (id: <id>)]";

        var result = PromptRenderer.Render(template, new Dictionary<string, string> { ["{{name}}"] = "General", ["{{id}}"] = "42" });

        Assert.Equal(template, result);
    }

    /// <summary>A single-brace JSON example in prompt text is not mistaken for a placeholder.</summary>
    [Fact]
    public void Render_SingleBraceJson_IsUnchanged()
    {
        const string template = "show {\"type\": \"object\"}";

        var result = PromptRenderer.Render(template, new Dictionary<string, string>());

        Assert.Equal(template, result);
    }

    /// <summary>Every occurrence of a repeated placeholder is replaced, not just the first.</summary>
    [Fact]
    public void Render_RepeatedPlaceholder_ReplacesEveryOccurrence()
    {
        var result = PromptRenderer.Render("{{a}} and {{a}}", new Dictionary<string, string> { ["{{a}}"] = "x" });

        Assert.Equal("x and x", result);
    }

    /// <summary>
    /// A substituted value that itself contains placeholder-shaped text is emitted as-is and never
    /// re-expanded, even when the "inner" placeholder also has a value supplied — the single-pass
    /// guarantee that keeps a user's own text from injecting a placeholder.
    /// </summary>
    [Fact]
    public void Render_SubstitutedValueContainingAToken_IsNotReExpanded()
    {
        var values = new Dictionary<string, string> { ["{{a}}"] = "{{b}}", ["{{b}}"] = "expanded" };

        var result = PromptRenderer.Render("{{a}}", values);

        Assert.Equal("{{b}}", result);
    }

    /// <summary>An empty template renders to an empty string.</summary>
    [Fact]
    public void Render_EmptyTemplate_ReturnsEmptyString()
    {
        var result = PromptRenderer.Render(string.Empty, new Dictionary<string, string>());

        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// A <see langword="null"/> template renders to an empty string rather than throwing: rejecting a
    /// missing template is the configuration store's job, not the renderer's.
    /// </summary>
    [Fact]
    public void Render_NullTemplate_ReturnsEmptyString()
    {
        var result = PromptRenderer.Render(null!, new Dictionary<string, string>());

        Assert.Equal(string.Empty, result);
    }

    /// <summary>A <see langword="null"/> values dictionary is a caller error and throws.</summary>
    [Fact]
    public void Render_NullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PromptRenderer.Render("{{a}}", null!));
    }

    /// <summary>A placeholder token containing only whitespace is malformed and left verbatim.</summary>
    [Fact]
    public void Render_WhitespaceOnlyPlaceholder_IsLeftVerbatim()
    {
        var result = PromptRenderer.Render("{{ }}", new Dictionary<string, string>());

        Assert.Equal("{{ }}", result);
    }

    /// <summary>An empty placeholder token is malformed and left verbatim.</summary>
    [Fact]
    public void Render_EmptyPlaceholder_IsLeftVerbatim()
    {
        var result = PromptRenderer.Render("{{}}", new Dictionary<string, string>());

        Assert.Equal("{{}}", result);
    }

    /// <summary>A placeholder name containing internal whitespace is malformed and left verbatim.</summary>
    [Fact]
    public void Render_PlaceholderNameWithInternalWhitespace_IsLeftVerbatim()
    {
        var result = PromptRenderer.Render("{{ nope here }}", new Dictionary<string, string>());

        Assert.Equal("{{ nope here }}", result);
    }

    /// <summary><see cref="PromptRenderer.FindPlaceholders"/> returns an empty list for text with no tokens.</summary>
    [Fact]
    public void FindPlaceholders_NoTokens_ReturnsEmptyList()
    {
        var tokens = PromptRenderer.FindPlaceholders("no tokens here");

        Assert.Empty(tokens);
    }

    /// <summary><see cref="PromptRenderer.FindPlaceholders"/> returns a null template as an empty list.</summary>
    [Fact]
    public void FindPlaceholders_NullTemplate_ReturnsEmptyList()
    {
        var tokens = PromptRenderer.FindPlaceholders(null!);

        Assert.Empty(tokens);
    }

    /// <summary><see cref="PromptRenderer.FindPlaceholders"/> finds a single token.</summary>
    [Fact]
    public void FindPlaceholders_OneToken_ReturnsThatToken()
    {
        var tokens = PromptRenderer.FindPlaceholders("Hello {{name}}");

        Assert.Equal(["{{name}}"], tokens);
    }

    /// <summary>
    /// <see cref="PromptRenderer.FindPlaceholders"/> finds several distinct tokens, and duplicate
    /// occurrences of the same token collapse to one entry.
    /// </summary>
    [Fact]
    public void FindPlaceholders_SeveralTokensWithDuplicates_ReturnsDistinctTokens()
    {
        var tokens = PromptRenderer.FindPlaceholders("{{a}} {{b}} {{a}} {{c}}");

        Assert.Equal(["{{a}}", "{{b}}", "{{c}}"], tokens);
    }

    /// <summary>
    /// <see cref="PromptRenderer.FindPlaceholders"/> returns distinct tokens in order of first
    /// appearance in the template, not, say, alphabetical order.
    /// </summary>
    [Fact]
    public void FindPlaceholders_PreservesOrderOfFirstAppearance()
    {
        var tokens = PromptRenderer.FindPlaceholders("{{z}} {{a}} {{z}} {{m}}");

        Assert.Equal(["{{z}}", "{{a}}", "{{m}}"], tokens);
    }
}
