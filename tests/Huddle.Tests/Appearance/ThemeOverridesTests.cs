using Agency.Huddle.App.Appearance;

namespace Agency.Huddle.Tests.Appearance;

/// <summary>
/// Tests for <see cref="ThemeOverrides.Build"/>: pure, no store, no host. Proves the D18 allowlist
/// rejects everything that could break out of the inline <c>&lt;style&gt;</c> block
/// <c>App.razor</c> renders it into, while still accepting the quoted font names the allowlist
/// exists to permit.
/// </summary>
public sealed class ThemeOverridesTests
{
    /// <summary>No overrides at all produces an empty body and no problems.</summary>
    [Fact]
    public void Build_WithNoOverrides_ReturnsEmptyAndNoProblems()
    {
        Dictionary<string, string> overrides = new(StringComparer.Ordinal);

        var css = ThemeOverrides.Build(overrides, out var problems);

        Assert.Equal(string.Empty, css);
        Assert.Empty(problems);
    }

    /// <summary>Every accepted token lands in one <c>:root</c> block, ordered by token name rather than insertion order.</summary>
    [Fact]
    public void Build_WithValidOverrides_EmitsOneRootBlockSortedByToken()
    {
        Dictionary<string, string> overrides = new(StringComparer.Ordinal)
        {
            ["--font-chat"] = "Georgia, serif",
            ["--accent"] = "#336699",
        };

        var css = ThemeOverrides.Build(overrides, out var problems);

        Assert.Empty(problems);
        Assert.Equal(
            ":root {\n    --accent: #336699;\n    --font-chat: Georgia, serif;\n}",
            css);
    }

    /// <summary>A key that names no theme token is skipped and reported, contributing nothing to the output.</summary>
    [Fact]
    public void Build_WithAnUnknownKey_SkipsItAndReportsAProblem()
    {
        Dictionary<string, string> overrides = new(StringComparer.Ordinal)
        {
            ["--not-a-real-token"] = "red",
        };

        var css = ThemeOverrides.Build(overrides, out var problems);

        Assert.Equal(string.Empty, css);
        var problem = Assert.Single(problems);
        Assert.Contains("--not-a-real-token", problem, StringComparison.Ordinal);
    }

    /// <summary>Every shape of a hostile or malformed value is rejected and contributes nothing to the output.</summary>
    /// <param name="value">A value D18's allowlist must reject.</param>
    [Theory]
    [InlineData("red; }")]
    [InlineData("red } :root{")]
    [InlineData("</style><script>x</script>")]
    [InlineData("/* */ red")]
    [InlineData("url(http://example.invalid/x)")]
    [InlineData("red\nblue")]
    public void Build_WithARejectedValue_SkipsItAndReportsAProblem(string value)
    {
        Dictionary<string, string> overrides = new(StringComparer.Ordinal)
        {
            ["--surface-base"] = value,
        };

        var css = ThemeOverrides.Build(overrides, out var problems);

        Assert.Equal(string.Empty, css);
        Assert.Single(problems);
        Assert.DoesNotContain(value, css, StringComparison.Ordinal);
    }

    /// <summary>
    /// A quoted font name survives intact: the allowlist keeps quotes deliberately, because
    /// <c>"Segoe UI", sans-serif</c> is the single most likely thing anyone types into a font
    /// override. This is the test that stops someone "hardening" the allowlist into uselessness.
    /// </summary>
    [Fact]
    public void Build_WithAQuotedFontName_IsAccepted()
    {
        Dictionary<string, string> overrides = new(StringComparer.Ordinal)
        {
            ["--font-chat"] = "\"Segoe UI\", sans-serif",
        };

        var css = ThemeOverrides.Build(overrides, out var problems);

        Assert.Empty(problems);
        Assert.Equal(":root {\n    --font-chat: \"Segoe UI\", sans-serif;\n}", css);
    }
}
