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
    // The exact CSS custom property MudThemeProvider.GenerateTheme emits for Palette.Primary, in
    // MudColor's default (rgba) string form - see ThemeCatalogTests for the same value read back
    // through the C# API instead. Present in the response only when the "Huddle Light" theme's
    // palette actually rendered.
    private const string LightPrimaryVariable = "--mud-palette-primary: rgba(74,21,75,1);";

    // The counterpart for "Huddle Dark", present only when IsDarkMode resolved to true before the
    // response was written.
    // #c07bc3. The dark palette used to DARKEN the brand purple to #5e2b60, which measured 1.63:1
    // against the dark ground - so MudBlazor's Primary-tinted active nav link was effectively
    // unreadable. ThemeCatalogTests now pins the contrast ratio itself; this constant only has to
    // follow the value. Kept as the literal rendered rgba because that is what reaches the browser.
    private const string DarkPrimaryVariable = "--mud-palette-primary: rgba(192,123,195,1);";

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
}
