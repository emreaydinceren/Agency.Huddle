using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Exercises how <c>MainLayout.razor</c> applies <see cref="Agency.Huddle.App.Appearance.AppearanceStore"/>'s
/// resolved state to MudBlazor's <c>MudThemeProvider</c>, now that a <c>MudTheme</c> is the single
/// source of theming: there is no <c>themes/{id}.css</c> link and no inline override
/// <c>&lt;style&gt;</c> for <c>App.razor</c> to emit any more - the provider renders its own
/// <c>&lt;style class='mud-theme-provider'&gt;</c> block directly from the C# <see cref="MudBlazor.MudTheme"/>,
/// prerendered into the initial HTML response the same way the rest of the interactive circuit is.
/// Writes <see cref="TeamWebApplicationFactory.AppearanceJsonPath"/> before <c>CreateClient()</c> so
/// the store's constructor reads it on first resolve.
/// </summary>
public sealed class AppearanceRenderingTests
{
    // The exact CSS custom property MudThemeProvider.GenerateTheme emits for Palette.Background, in
    // MudColor's default (rgba) string form. Present in the response only when the "Huddle Light"
    // theme's palette actually rendered. (Primary can no longer tell the two Huddle themes apart:
    // the accents differ per theme but the neutral ground is the stable marker.) The constant keeps
    // its historical name.
    private const string LightPrimaryVariable = "--mud-palette-background: rgba(242,240,237,1);";

    // The counterpart for "Huddle Dark", present only when IsDarkMode resolved to true before the
    // response was written. Kept as the literal rendered rgba because that is what reaches the browser.
    private const string DarkPrimaryVariable = "--mud-palette-background: rgba(28,28,27,1);";

    // Huddle Light's Primary (#5b64c8), which the Custom Accent Light placeholder carries.
    private const string BrandPrimaryVariable = "--mud-palette-primary: rgba(91,100,200,1);";

    // Solarized Dark's own editor.background, #002b36. Asserted rather than its Primary because
    // Solarized Dark takes Primary from VS Code's textLink.foreground registry default, which other
    // imported dark themes share - a background is this theme's and no other's.
    private const string SolarizedDarkBackgroundVariable = "--mud-palette-background: rgba(0,43,54,1);";

    /// <summary>
    /// With no <c>appearance.json</c>, the shell links no <c>themes/</c> stylesheet (that mechanism
    /// is gone entirely) and still renders the "Huddle" theme's light palette through MudBlazor's own
    /// generated <c>&lt;style&gt;</c> block.
    /// </summary>
    [Fact]
    public async Task AppShell_WithNoAppearanceFile_RendersTheDefaultThemeInLightMode()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.DoesNotContain("themes/", html, StringComparison.Ordinal);
        Assert.Contains(LightPrimaryVariable, html, StringComparison.Ordinal);
        Assert.DoesNotContain(DarkPrimaryVariable, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Selecting a dark theme is the whole of selecting dark mode: the file names a theme and
    /// nothing else, and the dark palette renders, with no page reload involved - the provider lives
    /// in the render tree.
    /// </summary>
    [Fact]
    public async Task AppShell_WithADarkThemeSelected_RendersTheDarkPalette()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"huddle-dark\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains(DarkPrimaryVariable, html, StringComparison.Ordinal);
        Assert.DoesNotContain(LightPrimaryVariable, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The regression this whole change exists for. An <c>appearance.json</c> written before the
    /// light/dark control was folded into the theme can say <c>"theme": "solarized-dark"</c> and
    /// <c>"dark": "light"</c> at the same time. That used to render the light palette Solarized Dark
    /// borrowed - which was Huddle's own - so the application showed Huddle Light under Solarized
    /// Dark's name. Now the theme decides, the retired key is ignored, and Solarized Dark's real
    /// palette renders.
    /// </summary>
    [Fact]
    public async Task AppShell_WithARetiredLightPreferenceBesideADarkTheme_StillRendersThatThemesOwnPalette()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"solarized-dark\",\"dark\":\"light\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains(SolarizedDarkBackgroundVariable, html, StringComparison.Ordinal);
        Assert.DoesNotContain(LightPrimaryVariable, html, StringComparison.Ordinal);
    }

    /// <summary>An unknown theme id is a warning, never a failure: the page still renders, with the catalog's default theme applied instead of a 500 or a blank palette.</summary>
    [Fact]
    public async Task AppShell_WithAnUnknownThemeId_StillRendersTheDefaultTheme()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"nonsense\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains(LightPrimaryVariable, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Selecting "Custom Accent Light" with a chosen accent colour renders that colour, not the
    /// placeholder Huddle Light Primary - and it does so from a plain HTTP GET, with no browser
    /// JavaScript involved. Unlike the browser-detection approach this replaced, the accent colour is
    /// a Human-picked, server-persisted value known synchronously on the very first render.
    /// </summary>
    [Fact]
    public async Task AppShell_WithCustomAccentAndAColourChosen_RendersThatColourNotThePlaceholder()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"custom-accent\",\"accentColor\":\"#00b294\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("--mud-palette-primary:", html, StringComparison.Ordinal);
        Assert.DoesNotContain(BrandPrimaryVariable, html, StringComparison.Ordinal);
    }

    /// <summary>Selecting "Custom Accent Light" with no colour chosen yet renders the placeholder - plain Huddle Light's own Primary - rather than an error or a blank palette.</summary>
    [Fact]
    public async Task AppShell_WithCustomAccentAndNoColourChosen_RendersThePlaceholder()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"custom-accent\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains(BrandPrimaryVariable, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Selecting Nordic Light renders the Theme marker with its id and its style pack (ADR-0057), which is what every
    /// rule in <c>packs/nordic.css</c> is scoped to; the pack's stylesheet is linked.
    /// </summary>
    [Fact]
    public async Task AppShell_WithNordicSelected_MarksTheThemeAndItsPack()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"nordic-light\"}");
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/", ct);
        IHtmlDocument page = new HtmlParser().ParseDocument(html);

        IElement marker = Assert.Single(page.QuerySelectorAll("span.theme-marker"));
        Assert.Equal("nordic-light", marker.GetAttribute("data-huddle-theme"));
        Assert.Equal("nordic", marker.GetAttribute("data-theme-pack"));
        Assert.Single(page.QuerySelectorAll("link[rel='stylesheet'][href^='packs/nordic']"));
    }

    /// <summary>
    /// With the default Theme the marker names it and carries no pack, so the Nordic pack, though linked for every
    /// Theme, has nothing to match.
    /// </summary>
    [Fact]
    public async Task AppShell_WithTheDefaultTheme_MarksNoPack()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/", ct);
        IHtmlDocument page = new HtmlParser().ParseDocument(html);

        IElement marker = Assert.Single(page.QuerySelectorAll("span.theme-marker"));
        Assert.Equal("huddle", marker.GetAttribute("data-huddle-theme"));
        Assert.False(marker.HasAttribute("data-theme-pack"));
    }
}
