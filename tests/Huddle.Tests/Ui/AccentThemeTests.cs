using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="AccentTheme"/>: <see cref="AccentTheme.BuildTheme(string, bool)"/> replaces only
/// the Primary family with a contrast-clamped version of the given accent colour, leaves every other
/// palette value exactly as <see cref="HuddleTheme.Light"/>/<see cref="HuddleTheme.Dark"/> authored it,
/// and never hands back a shared instance a second circuit could mutate; and the two catalog
/// descriptors carry Huddle's own placeholder Primary until a colour is chosen - see both types' own
/// remarks for why.
/// </summary>
public sealed class AccentThemeTests
{
    /// <summary>An accent too pale to read on Huddle Light's near-white ground still yields a Primary that clears the same 4.5:1 floor <see cref="ThemeCatalogTests"/> holds every built-in theme to.</summary>
    [Fact]
    public void BuildTheme_Light_ClearsThePrimaryContrastFloor()
    {
        var theme = AccentTheme.BuildTheme("#ffeb3b", dark: false);

        Assert.True(ContrastColour.Ratio(theme.PaletteLight.Primary, theme.PaletteLight.Surface) >= 4.5);
        Assert.True(ContrastColour.Ratio(theme.PaletteLight.PrimaryContrastText, theme.PaletteLight.Primary) >= 4.5);
    }

    /// <summary>An accent too dark to read on Huddle Dark's near-black ground still yields a Primary that clears the same 4.5:1 floor.</summary>
    [Fact]
    public void BuildTheme_Dark_ClearsThePrimaryContrastFloor()
    {
        var theme = AccentTheme.BuildTheme("#1a0033", dark: true);

        Assert.True(ContrastColour.Ratio(theme.PaletteDark.Primary, theme.PaletteDark.Surface) >= 4.5);
        Assert.True(ContrastColour.Ratio(theme.PaletteDark.PrimaryContrastText, theme.PaletteDark.Primary) >= 4.5);
    }

    /// <summary>Only the Primary family changes: every other palette value matches <see cref="HuddleTheme.Light"/>'s own, unmodified.</summary>
    [Fact]
    public void BuildTheme_Light_LeavesEveryOtherPaletteValueAsHuddleLightAuthoredIt()
    {
        var expected = HuddleTheme.Light();

        var theme = AccentTheme.BuildTheme("#3a6fd8", dark: false);

        Assert.Equal(expected.Background, theme.PaletteLight.Background);
        Assert.Equal(expected.Surface, theme.PaletteLight.Surface);
        Assert.Equal(expected.TextPrimary, theme.PaletteLight.TextPrimary);
        Assert.Equal(expected.TextSecondary, theme.PaletteLight.TextSecondary);
        Assert.Equal(expected.Secondary, theme.PaletteLight.Secondary);
        Assert.Equal(expected.Success, theme.PaletteLight.Success);
        Assert.Equal(expected.Error, theme.PaletteLight.Error);
    }

    /// <summary>Two calls with the same input never hand back the same instance - a shared, mutated-in-place theme would leak one circuit's accent colour into another's render.</summary>
    [Fact]
    public void BuildTheme_CalledTwice_ReturnsDistinctInstances()
    {
        var first = AccentTheme.BuildTheme("#3a6fd8", dark: false);
        var second = AccentTheme.BuildTheme("#3a6fd8", dark: false);

        Assert.NotSame(first, second);
        Assert.NotSame(first.PaletteLight, second.PaletteLight);
    }

    /// <summary>Custom Accent Light's descriptor carries Huddle Light's own Primary as its placeholder, per the type's remarks, and the id <c>AppearanceStore.IsKnownTheme</c> and <c>appearance.json</c> use.</summary>
    [Fact]
    public void LightDescriptor_CarriesHuddleLightsPlaceholderPrimary()
    {
        Assert.Equal("custom-accent", AccentTheme.LightDescriptor.Id);
        Assert.Equal(ThemeMode.Light, AccentTheme.LightDescriptor.Mode);
        Assert.Equal(ThemeGroup.Light, AccentTheme.LightDescriptor.Group);
        Assert.Equal(HuddleTheme.Light().Primary, AccentTheme.LightDescriptor.Theme.PaletteLight.Primary);
        Assert.Equal(HuddleTheme.Light().Background, AccentTheme.LightDescriptor.Theme.PaletteLight.Background);
        Assert.Equal(HuddleTheme.Light().Surface, AccentTheme.LightDescriptor.Theme.PaletteLight.Surface);
        Assert.Equal(HuddleTheme.Light().DrawerBackground, AccentTheme.LightDescriptor.Theme.PaletteLight.DrawerBackground);
    }

    /// <summary>Custom Accent Dark's descriptor carries Huddle Dark's own Primary as its placeholder.</summary>
    [Fact]
    public void DarkDescriptor_CarriesHuddleDarksPlaceholderPrimary()
    {
        Assert.Equal("custom-accent-dark", AccentTheme.DarkDescriptor.Id);
        Assert.Equal(ThemeMode.Dark, AccentTheme.DarkDescriptor.Mode);
        Assert.Equal(ThemeGroup.Dark, AccentTheme.DarkDescriptor.Group);
        Assert.Equal(HuddleTheme.Dark().Primary, AccentTheme.DarkDescriptor.Theme.PaletteDark.Primary);
    }
}
