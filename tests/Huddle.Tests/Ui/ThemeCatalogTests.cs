using MudBlazor;
using MudBlazor.Utilities;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="ThemeCatalog"/>'s shape now that MudBlazor's <see cref="MudTheme"/> is the single
/// source of theming: every descriptor has a unique, non-empty id, carries both a light and a dark
/// palette, and the one shipped "Huddle" theme carries the colours this application shipped in
/// <c>wwwroot/theme.css</c> before this migration.
/// </summary>
public sealed class ThemeCatalogTests
{
    /// <summary>Every descriptor's id is unique and non-empty - a blank or duplicate id would make the Appearance tab's dropdown either unusable or ambiguous.</summary>
    [Fact]
    public void BuiltIn_EveryIdIsUniqueAndNonEmpty()
    {
        var ids = ThemeCatalog.BuiltIn.Select(descriptor => descriptor.Id).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));

        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(ids.Count, distinct.Count);
    }

    /// <summary>
    /// Every descriptor's <see cref="MudTheme"/> has both <see cref="MudTheme.PaletteLight"/> and
    /// <see cref="MudTheme.PaletteDark"/> populated. MudBlazor defaults both to a non-null instance,
    /// so this is really asserting that nobody accidentally left a descriptor pointing at a bare
    /// <c>new MudTheme()</c> with none of this application's own colours set.
    /// </summary>
    [Fact]
    public void BuiltIn_EveryThemeHasBothPalettes()
    {
        foreach (var descriptor in ThemeCatalog.BuiltIn)
        {
            Assert.NotNull(descriptor.Theme.PaletteLight);
            Assert.NotNull(descriptor.Theme.PaletteDark);
        }
    }

    /// <summary>
    /// A spot-check that the shipped "Huddle" theme's <c>Primary</c> and <c>Background</c> carry the
    /// expected values in both palettes - the two colours most likely to visibly regress if a future
    /// edit swaps light and dark, or fat-fingers a hex value.
    /// </summary>
    [Fact]
    public void Huddle_PrimaryAndBackgroundCarryTheExpectedValues()
    {
        var huddle = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "huddle", StringComparison.Ordinal));

        Assert.Equal("#4A154B", huddle.Theme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
        Assert.Equal("#FFFFFF", huddle.Theme.PaletteLight.Background.ToString(MudColorOutputFormats.Hex), ignoreCase: true);

        Assert.Equal("#C07BC3", huddle.Theme.PaletteDark.Primary.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
        Assert.Equal("#1B1B1F", huddle.Theme.PaletteDark.Background.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
    }

    /// <summary>
    /// The dark palette's <c>Primary</c> tints the active <see cref="MudBlazor.MudNavLink"/> in the
    /// sidebar through <c>--mud-palette-primary</c>, so an insufficiently light dark-mode Primary makes
    /// the active Room unreadable - exactly the defect a prior <c>#5e2b60</c> shipped (1.63:1 against
    /// <c>Background</c>). This computes the WCAG 2.1 contrast ratio of <c>Primary</c> against
    /// <c>Surface</c> - the tighter of the two constraints, since <c>Surface</c> (<c>#2a2a31</c>) is
    /// lighter than <c>Background</c> (<c>#1b1b1f</c>) - and asserts it clears the 4.5:1 text minimum,
    /// the same reasoning <see cref="ThemeSourceTests"/> gives for checking source text instead of
    /// eyeballing it: exact, rather than a matter of taste, is what stops this regressing quietly.
    /// </summary>
    [Fact]
    public void Huddle_PaletteDarkPrimaryOnSurface_ClearsWcagAaContrastMinimum()
    {
        var huddle = ThemeCatalog.BuiltIn.Single(descriptor => string.Equals(descriptor.Id, "huddle", StringComparison.Ordinal));

        var ratio = ContrastRatio(huddle.Theme.PaletteDark.Primary, huddle.Theme.PaletteDark.Surface);

        Assert.True(ratio >= 4.5, $"Contrast ratio {ratio:F2}:1 falls short of the 4.5:1 WCAG AA text minimum.");
    }

    /// <summary>
    /// The WCAG 2.1 contrast ratio between two colours: <c>(L1 + 0.05) / (L2 + 0.05)</c>, where
    /// <c>L1</c> is the lighter colour's relative luminance and <c>L2</c> the darker's.
    /// </summary>
    /// <param name="first">One colour.</param>
    /// <param name="second">The other colour.</param>
    private static double ContrastRatio(MudColor first, MudColor second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// A colour's WCAG 2.1 relative luminance: each sRGB channel linearised, then combined as
    /// <c>0.2126R + 0.7152G + 0.0722B</c>.
    /// </summary>
    /// <param name="color">The colour to measure.</param>
    private static double RelativeLuminance(MudColor color) =>
        (0.2126 * LineariseChannel(color.R)) + (0.7152 * LineariseChannel(color.G)) + (0.0722 * LineariseChannel(color.B));

    /// <summary>Linearises one 0-255 sRGB channel per WCAG 2.1's <c>c &lt;= 0.03928 ? c/12.92 : ((c+0.055)/1.055)^2.4</c>.</summary>
    /// <param name="channel">The channel's 0-255 byte value.</param>
    private static double LineariseChannel(byte channel)
    {
        var normalised = channel / 255.0;
        return normalised <= 0.03928 ? normalised / 12.92 : Math.Pow((normalised + 0.055) / 1.055, 2.4);
    }
}
