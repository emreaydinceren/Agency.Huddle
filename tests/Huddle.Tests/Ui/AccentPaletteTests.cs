using MudBlazor.Utilities;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="AccentPalette.ClampForContrast(MudColor, MudColor, MudColor, bool)"/>: an accent
/// colour that already clears WCAG's 4.5:1 text floor against both a background and a surface comes
/// back unchanged, one that does not gets shifted toward black or white until it does, and an accent
/// that can never clear the floor without losing its colour falls back to pure black or white - the
/// same floor <see cref="ThemeCatalogTests"/> holds every hand-tuned built-in theme to.
/// </summary>
public sealed class AccentPaletteTests
{
    private static readonly MudColor White = new("#ffffff");
    private static readonly MudColor NearWhiteSurface = new("#f2f0ed");
    private static readonly MudColor Black = new("#1c1c1b");
    private static readonly MudColor NearBlackSurface = new("#262625");

    /// <summary>An accent that already reads against a light background and surface is returned unchanged - clamping never fires when it is not needed.</summary>
    [Fact]
    public void ClampForContrast_AlreadySafeAgainstLightBackground_ReturnsAccentUnchanged()
    {
        MudColor accent = new("#4a154b");

        var clamped = AccentPalette.ClampForContrast(accent, White, NearWhiteSurface, towardBlack: true);

        Assert.Equal(accent, clamped);
    }

    /// <summary>An accent too pale to read on a light background is darkened until it clears 4.5:1 against both Background and Surface.</summary>
    [Fact]
    public void ClampForContrast_TooPaleForLightBackground_DarkensUntilItClearsTheFloor()
    {
        MudColor accent = new("#ffeb3b");

        var clamped = AccentPalette.ClampForContrast(accent, White, NearWhiteSurface, towardBlack: true);

        Assert.True(ContrastColour.Ratio(clamped, White) >= 4.5);
        Assert.True(ContrastColour.Ratio(clamped, NearWhiteSurface) >= 4.5);
    }

    /// <summary>An accent too dark to read on a dark background is lightened until it clears 4.5:1 against both Background and Surface.</summary>
    [Fact]
    public void ClampForContrast_TooDarkForDarkBackground_LightensUntilItClearsTheFloor()
    {
        MudColor accent = new("#1a0033");

        var clamped = AccentPalette.ClampForContrast(accent, Black, NearBlackSurface, towardBlack: false);

        Assert.True(ContrastColour.Ratio(clamped, Black) >= 4.5);
        Assert.True(ContrastColour.Ratio(clamped, NearBlackSurface) >= 4.5);
    }

    /// <summary>Pure white, darkened toward black for a light background, still reaches the floor rather than looping forever - proof the fallback in <see cref="ContrastColour"/>'s own remarks is reachable and correct.</summary>
    [Fact]
    public void ClampForContrast_PureWhiteTowardBlack_StillClearsTheFloor()
    {
        var clamped = AccentPalette.ClampForContrast(White, White, NearWhiteSurface, towardBlack: true);

        Assert.True(ContrastColour.Ratio(clamped, White) >= 4.5);
        Assert.True(ContrastColour.Ratio(clamped, NearWhiteSurface) >= 4.5);
    }

    /// <summary>Pure black, lightened toward white for a dark background, still reaches the floor rather than looping forever.</summary>
    [Fact]
    public void ClampForContrast_PureBlackTowardWhite_StillClearsTheFloor()
    {
        var clamped = AccentPalette.ClampForContrast(Black, Black, NearBlackSurface, towardBlack: false);

        Assert.True(ContrastColour.Ratio(clamped, Black) >= 4.5);
        Assert.True(ContrastColour.Ratio(clamped, NearBlackSurface) >= 4.5);
    }
}
