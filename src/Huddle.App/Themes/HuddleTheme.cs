using MudBlazor;

namespace Agency.Huddle.App.Themes;

/// <summary>
/// The "Huddle" theme: the palette this application shipped in <c>wwwroot/theme.css</c> before the
/// migration to MudBlazor theming, resolved into MudBlazor's light and dark <see cref="Palette"/>
/// pair rather than CSS <c>light-dark()</c> pairs.
/// </summary>
internal static class HuddleTheme
{
    /// <summary>
    /// Builds a fresh instance of the Huddle theme's light palette on every call. A fresh instance
    /// matters because other themes in this catalog borrow one of these two palettes for the mode
    /// they were not designed for, and a shared mutable <see cref="Palette"/> instance across themes
    /// would be a latent aliasing bug.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying today's light palette values.</returns>
    public static PaletteLight Light() => new()
    {
        Background = "#ffffff",
        Surface = "#ffffff",
        DrawerBackground = "#f5f5f5",
        BackgroundGray = "#f7f7f7",
        TableHover = "#eef3ff",
        Divider = "#d0d0d0",
        DividerLight = "#e3e3e3",
        LinesInputs = "#bbbbbb",
        LinesDefault = "#9fb8e8",
        TextPrimary = "#222222",
        TextSecondary = "#444444",
        TextDisabled = "#999999",
        Primary = "#4a154b",
        PrimaryDarken = "#611f69",
        PrimaryContrastText = "#ffffff",
        Secondary = "#3a6fd8",
        Success = "#2e9e44",
        Error = "#b32121",
        Warning = "#c77d00",
        WarningLighten = "#e0a83e",
        ErrorLighten = "#e3b6b6",
        Info = "#1d4f8f",
        OverlayDark = "rgba(0,0,0,0.4)",
    };

    /// <summary>
    /// Builds a fresh instance of the Huddle theme's dark palette on every call. A fresh instance
    /// matters because other themes in this catalog borrow one of these two palettes for the mode
    /// they were not designed for, and a shared mutable <see cref="Palette"/> instance across themes
    /// would be a latent aliasing bug.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying today's dark palette values.</returns>
    public static PaletteDark Dark() => new()
    {
        Background = "#1b1b1f",
        Surface = "#2a2a31",
        DrawerBackground = "#232328",
        BackgroundGray = "#17171b",
        TableHover = "#32323b",
        Divider = "#3a3a44",
        DividerLight = "#30303a",
        LinesInputs = "#4a4a56",
        LinesDefault = "#5a6f99",
        TextPrimary = "#e6e6ea",
        TextSecondary = "#c3c3cc",
        TextDisabled = "#6a6a74",
        // Lightened from the original #5e2b60, which measured only 1.63:1 against this
        // palette's Background (#1b1b1f) - see app.css's focus-ring comment for that
        // measurement - and 1.35:1 against Surface (#2a2a31), both well under the 4.5:1
        // MudBlazor needs for --mud-palette-primary to read on the active MudNavLink.
        // #c07bc3 keeps the same brand hue (~297.5 degrees, against the light palette's
        // ~298.9 and this palette's own prior ~297.7) and a close saturation, only lighter,
        // and was verified with the WCAG 2.1 relative-luminance formula:
        //   - vs Surface (#2a2a31):    4.656:1
        //   - vs Background (#1b1b1f): 5.610:1
        // Both clear the 4.5:1 text minimum.
        Primary = "#c07bc3",
        PrimaryDarken = "#7a3a7e",
        // White PrimaryContrastText only reaches 3.06:1 against the new, lighter Primary -
        // enough for large text but short of the 4.5:1 minimum the avatar monogram needs.
        // Black reaches 6.86:1, comfortably clearing it.
        PrimaryContrastText = "#000000",
        Secondary = "#7aa2f7",
        Success = "#3fbf5a",
        Error = "#e05a5a",
        Warning = "#e0a33e",
        WarningLighten = "#e8c46a",
        ErrorLighten = "#6b3535",
        Info = "#8ab4f8",
        OverlayDark = "rgba(0,0,0,0.6)",
    };

    /// <summary>The Huddle theme's descriptor, built from fresh <see cref="Light"/> and <see cref="Dark"/> palettes.</summary>
    public static ThemeDescriptor Descriptor { get; } = new(Id: "huddle", Label: "Huddle", Theme: ThemeDefaults.Create(Light(), Dark()), Mode: ThemeMode.Light);
}
