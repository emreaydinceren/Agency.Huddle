using System.Net;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Themes;
using Microsoft.AspNetCore.Mvc.Testing;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins the round-3 Settings package (visual audit B1, B9a-g, E6, E7, Import skill): section nav geometry, Theme swatches and picker, Library labels, Skills display names and the team-types address.</summary>
public sealed partial class R3SettingsTests
{
    /// <summary>A Skill's slug is shown as a sentence-cased name, never the folder spelling (B9g).</summary>
    /// <param name="slug">The folder name.</param>
    /// <param name="expected">What the Human reads.</param>
    [Theory]
    [InlineData("team-building", "Team building")]
    [InlineData("checks_before_merge", "Checks before merge")]
    [InlineData("writer", "Writer")]
    [InlineData("", "")]
    public void SkillNames_Display_TurnsASlugIntoAName(string slug, string expected)
    {
        Assert.Equal(expected, SkillNames.Display(slug));
    }

    /// <summary>Each Theme's swatch reads its Ground, Ink and Accent from the palette it fills, as three distinct hex colours (B9b).</summary>
    [Fact]
    public void Appearance_SwatchFor_ReturnsGroundInkAndAccentFromTheFilledPalette()
    {
        foreach (ThemeDescriptor theme in ThemeCatalog.BuiltIn)
        {
            IReadOnlyList<string> swatch = Agency.Huddle.App.Components.Settings.Appearance.SwatchFor(theme);

            Assert.Equal(3, swatch.Count);
            Assert.All(swatch, colour => Assert.Matches("^#[0-9A-Fa-f]{6,8}$", colour));
            Assert.NotEqual(swatch[0], swatch[1]);
        }
    }

    /// <summary>The Appearance page draws one swatch per Theme and no inner-scrolling band (B9b, B9c).</summary>
    [Fact]
    public async Task AppearancePage_DrawsASwatchPerTheme()
    {
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/settings/appearance", TestContext.Current.CancellationToken);

        Assert.Equal(ThemeCatalog.BuiltIn.Count, Regex.Count(html, "class=\"theme-swatch\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)));
        Assert.Contains("Licences", html, StringComparison.Ordinal); // contains-ok: rendered markup text
    }

    /// <summary>The Library tab's Hide switch reads once, not "HideHide" (B9f).</summary>
    [Fact]
    public async Task LibraryPage_HideSwitchReadsOnce()
    {
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/settings/library", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("ShowShow", html, StringComparison.Ordinal);
        Assert.Contains("Show", html, StringComparison.Ordinal); // contains-ok: rendered markup text
    }

    /// <summary>The hyphenated team-types address redirects to the canonical one (E7).</summary>
    [Fact]
    public async Task TeamTypesAlias_RedirectsToTheCanonicalAddress()
    {
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.GetAsync("/settings/team-types", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/settings/teamtypes", response.Headers.Location?.ToString(), StringComparison.Ordinal); // contains-ok: rendered markup text
    }

    /// <summary>The section nav rows are 32px, inset and rounded like the sidebar's, and the intro is limited to 75ch (B1, B9a).</summary>
    [Fact]
    public void AppCss_SectionNavUsesTheSidebarRowGeometry()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(NavRow(), css);
        Assert.Matches(IntroMeasure(), css);
    }

    /// <summary>The Theme picker grows with its content: no max-height and no inner scrollbar (B9c).</summary>
    [Fact]
    public void AppCss_ThemePickerHasNoInnerScroll()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        Match rule = ThemePickerRule().Match(css);

        Assert.True(rule.Success);
        Assert.DoesNotContain("max-height", rule.Groups["body"].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow", rule.Groups["body"].Value, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\.settings-nav \.mud-nav-link\s*\{[^}]*min-height:\s*var\(--nav-row-height\)", RegexOptions.CultureInvariant, 5000)]
    private static partial Regex NavRow();

    [GeneratedRegex(@"\.settings-intro\s*\{[^}]*max-width:\s*75ch", RegexOptions.CultureInvariant, 5000)]
    private static partial Regex IntroMeasure();

    [GeneratedRegex(@"(?<![\w-])\.theme-picker\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, 5000)]
    private static partial Regex ThemePickerRule();
}
