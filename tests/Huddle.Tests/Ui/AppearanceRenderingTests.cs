using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Exercises the three-layer <c>&lt;head&gt;</c> <c>App.razor</c> builds on top of
/// <see cref="Agency.Huddle.App.Appearance.AppearanceStore"/>: base stylesheets, then the selected
/// theme's stylesheet, then the inline override <c>&lt;style&gt;</c> - in that source order, because
/// all three target plain <c>:root</c> and later wins per token. Writes
/// <see cref="TeamWebApplicationFactory.AppearanceJsonPath"/> before <c>CreateClient()</c> so the
/// store's constructor reads it on first resolve.
/// </summary>
public sealed class AppearanceRenderingTests
{
    /// <summary>With no <c>appearance.json</c>, the shell links no theme stylesheet and emits no override <c>&lt;style&gt;</c> - the built-in pair applies through the OS's preference alone.</summary>
    [Fact]
    public async Task AppShell_WithNoAppearanceFile_LinksNoThemeAndEmitsNoOverrideStyle()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.DoesNotContain("themes/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<style", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A selected theme is linked after the base <c>theme.css</c> stylesheet. Nothing else can see
    /// this ordering, and getting it wrong silently disables the theme.
    /// </summary>
    [Fact]
    public async Task AppShell_WithAThemeSelected_LinksItAfterTheBaseStylesheet()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"huddle-dark\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        // The base link is @Assets["theme.css"], which .NET's static-asset fingerprinting rewrites to
        // something like "theme.ce2n94aiaf.css" - "theme." matches only that href, never
        // "themes/huddle-dark.css" (the "s" right after "theme" rules that one out).
        var baseThemeIndex = html.IndexOf("theme.", StringComparison.Ordinal);
        var selectedThemeIndex = html.IndexOf("themes/huddle-dark.css", StringComparison.Ordinal);

        Assert.True(baseThemeIndex >= 0, "Expected the base theme.css stylesheet link to be present.");
        Assert.True(selectedThemeIndex >= 0, "Expected a themes/huddle-dark.css stylesheet link to be present.");
        Assert.True(baseThemeIndex < selectedThemeIndex, "Expected the base stylesheet to be linked before the selected theme.");
    }

    /// <summary>Valid overrides are emitted in a <c>&lt;style&gt;</c> block after both stylesheet links.</summary>
    [Fact]
    public async Task AppShell_WithOverrides_EmitsThemInAStyleBlockAfterBothLinks()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(
            factory.AppearanceJsonPath,
            "{\"theme\":\"huddle-dark\",\"overrides\":{\"--font-chat\":\"Georgia, serif\"}}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        var baseThemeIndex = html.IndexOf("theme.", StringComparison.Ordinal);
        var selectedThemeIndex = html.IndexOf("themes/huddle-dark.css", StringComparison.Ordinal);
        var styleIndex = html.IndexOf("<style", StringComparison.Ordinal);

        Assert.True(baseThemeIndex >= 0 && baseThemeIndex < selectedThemeIndex, "Base stylesheet must come before the theme link.");
        Assert.True(selectedThemeIndex < styleIndex, "The theme link must come before the override <style> block.");
        Assert.Contains("--font-chat: Georgia, serif;", html, StringComparison.Ordinal);
    }

    /// <summary>A rejected override value never reaches the document: the page renders normally, with no trace of the hostile text and no second <c>&lt;style&gt;</c> element.</summary>
    [Fact]
    public async Task AppShell_ARejectedOverrideValueNeverReachesTheDocument()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(
            factory.AppearanceJsonPath,
            "{\"overrides\":{\"--surface-base\":\"red; } :root{\"}}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.DoesNotContain("} :root{", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<style", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every built-in theme's stylesheet is actually served at its plain, unfingerprinted route -
    /// the route <c>App.razor</c>'s hand-built <c>href="themes/{id}.css"</c> depends on, since it
    /// bypasses <c>@Assets</c> entirely (D12a).
    /// </summary>
    [Fact]
    public async Task AppShell_ServesEveryBuiltInThemeStylesheet()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            using var response = await client.GetAsync($"/themes/{theme.Id}.css", ct);
            Assert.True(
                response.IsSuccessStatusCode,
                $"GET /themes/{theme.Id}.css returned {(int)response.StatusCode}.");
        }
    }
}
