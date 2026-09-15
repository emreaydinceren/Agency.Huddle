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
    // through the C# API instead. Present in the response only when the "Huddle" theme's light
    // palette actually rendered.
    private const string LightPrimaryVariable = "--mud-palette-primary: rgba(74,21,75,1);";

    // The dark-palette counterpart of LightPrimaryVariable, present only when IsDarkMode resolved
    // to true before the response was written.
    private const string DarkPrimaryVariable = "--mud-palette-primary: rgba(94,43,96,1);";

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

    /// <summary>A stored <c>"dark"</c> preference is read back and rendered as the dark palette, with no page reload involved - the provider lives in the render tree.</summary>
    [Fact]
    public async Task AppShell_WithDarkPreferenceStored_RendersTheDarkPalette()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        File.WriteAllText(factory.AppearanceJsonPath, "{\"theme\":\"huddle\",\"dark\":\"dark\"}");
        using HttpClient client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains(DarkPrimaryVariable, html, StringComparison.Ordinal);
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
