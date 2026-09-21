// Source: theme-defaults/themes/hc_light.json, from Visual Studio Code 1.138.0.
// No include chain: hc_light.json declares only 5 colour keys and inherits from nothing - the
// thinnest source theme in this catalog.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Default High Contrast Light", uiTheme "hc-light".
// Extracted 2026-09-16.
// Values VS Code's theme JSON does not carry (rows marked "registry default" below) came from
// the registerColor defaults in microsoft/vscode at tag 1.138.0 (MIT), selected for the
// "hcLight" variant.
// This is not a faithful high-contrast port: VS Code's real HC experience draws borders
// throughout via contrastBorder, and MudBlazor's palette has no equivalent slot for that token.
// What follows is Light High Contrast's ordinary fill colours only, which lands as a strong-
// contrast ordinary theme rather than true high contrast.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Light High Contrast", Visual Studio Code's built-in high-contrast light theme, ported once
/// at authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance and for why this is not a faithful high-contrast port.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class LightHighContrastTheme
{
    /// <summary>
    /// Builds a fresh instance of Light High Contrast's native light palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying Light High Contrast's palette.</returns>
    private static PaletteLight Light() => new()
    {
        Background = "#ffffff", // editor.background (registry default)
        Surface = "#ffffff", // editorWidget.background (registry default)
        DrawerBackground = "#ffffff", // sideBar.background (registry default)
        BackgroundGray = "#ffffff", // panel.background (registry default, sideBarSectionHeader.background absent)
        TableHover = "#e7edf3", // list.hoverBackground (registry default, composited from #0f4a851a over DrawerBackground #ffffff)
        Divider = "#0f4a85", // panel.border (registry default)
        DividerLight = "#0f4a85", // sideBar.border (registry default)
        LinesInputs = "#0f4a85", // input.border (registry default)
        LinesDefault = "#006bbd", // focusBorder (registry default)
        TextPrimary = "#292929", // foreground (registry default)
        TextSecondary = "#696969", // descriptionForeground (registry default, composited from #292929b2 over Surface #ffffff)
        TextDisabled = "#7f7f7f", // disabledForeground (registry default)
        Primary = "#0f4a85", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#ffffff", // computed: black 2.34:1 vs white 8.98:1 against Primary #0f4a85
        Secondary = "#0f4a85", // button.background (registry default)
        Success = "#374e06", // charts.green (registry default)
        Error = "#b5200d", // errorForeground (registry default)
        Warning = "#895503", // charts.yellow (registry default)
        Info = "#0063d3", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.4)", // fixed for a light theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Light High Contrast's descriptor. It is a single-mode VS Code theme and so carries only its
    /// light palette; MudBlazor's dark slot keeps MudBlazor's own defaults and is never
    /// rendered, because selecting this theme also selects its mode.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "light-high-contrast",
        Label: "Light High Contrast",
        Theme: ThemeDefaults.CreateLight(Light()),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.HighContrast);
}
