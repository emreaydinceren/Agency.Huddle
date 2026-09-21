// Source: theme-defaults/themes/hc_black.json, from Visual Studio Code 1.138.0.
// No include chain: hc_black.json declares only 13 colour keys and inherits from nothing.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Default High Contrast", uiTheme "hc-black".
// Extracted 2026-09-16.
// Values VS Code's theme JSON does not carry (rows marked "registry default" below) came from
// the registerColor defaults in microsoft/vscode at tag 1.138.0 (MIT), selected for the
// "hcDark" variant.
// This is not a faithful high-contrast port: VS Code's real HC experience draws borders
// throughout via contrastBorder, and MudBlazor's palette has no equivalent slot for that token.
// What follows is Dark High Contrast's ordinary fill colours only, which lands as a strong-
// contrast ordinary theme rather than true high contrast.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Dark High Contrast", Visual Studio Code's built-in high-contrast dark theme, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance and for why this is not a faithful high-contrast port.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class DarkHighContrastTheme
{
    /// <summary>
    /// Builds a fresh instance of Dark High Contrast's native dark palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Dark High Contrast's palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#000000", // editor.background (theme)
        Surface = "#0c141f", // editorWidget.background (registry default)
        DrawerBackground = "#000000", // sideBar.background (registry default)
        BackgroundGray = "#000000", // panel.background (registry default, sideBarSectionHeader.background absent)
        TableHover = "#1a1a1a", // list.hoverBackground (registry default, composited from #ffffff1a over DrawerBackground #000000)
        Divider = "#6fc3df", // panel.border (registry default)
        DividerLight = "#6fc3df", // sideBar.border (registry default)
        LinesInputs = "#6fc3df", // input.border (registry default)
        LinesDefault = "#f38518", // focusBorder (registry default)
        TextPrimary = "#ffffff", // foreground (registry default)
        TextSecondary = "#b6b8bc", // descriptionForeground (registry default, composited from #ffffffb2 over Surface #0c141f)
        TextDisabled = "#a5a5a5", // disabledForeground (registry default)
        Primary = "#21a6ff", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 7.96:1 vs white 2.64:1 against Primary #21a6ff
        Secondary = "#000000", // button.background (registry default)
        Success = "#89d185", // charts.green (registry default)
        Error = "#f48771", // errorForeground (registry default)
        Warning = "#ffd370", // charts.yellow (registry default)
        Info = "#59a4f9", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.6)", // fixed for a dark theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Dark High Contrast's descriptor. It is a single-mode VS Code theme and so carries only its
    /// dark palette; MudBlazor's light slot keeps MudBlazor's own defaults and is never
    /// rendered, because selecting this theme also selects its mode.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "dark-high-contrast",
        Label: "Dark High Contrast",
        Theme: ThemeDefaults.CreateDark(Dark()),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.HighContrast);
}
