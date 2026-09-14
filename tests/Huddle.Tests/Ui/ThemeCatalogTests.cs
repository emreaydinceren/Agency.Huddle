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

        Assert.Equal("#5E2B60", huddle.Theme.PaletteDark.Primary.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
        Assert.Equal("#1B1B1F", huddle.Theme.PaletteDark.Background.ToString(MudColorOutputFormats.Hex), ignoreCase: true);
    }
}
