// Source: theme-solarized-light/themes/solarized-light-color-theme.json, from Visual Studio
// Code 1.138.0.
// The extension declares "license": "MIT", publisher vscode, with no per-theme LICENSE file
// and no further third-party attribution anywhere in the VS Code tree for this theme.
// The theme's contributes.themes id is "Solarized Light", uiTheme "vs".
// Extracted 2026-09-16.
// Values VS Code's theme JSON does not carry (rows marked "registry default" below) came from
// the registerColor defaults in microsoft/vscode at tag 1.138.0 (MIT), selected for the "light"
// variant.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Solarized Light", one of the colour themes bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance.
/// </summary>
/// <remarks>
/// See <see cref="DarkModernTheme"/>'s remarks for why this theme's <c>Primary</c> reads
/// <c>textLink.foreground</c> rather than <c>button.background</c>.
/// </remarks>
internal static class SolarizedLightTheme
{
    /// <summary>
    /// Builds a fresh instance of Solarized Light's native light palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying Solarized Light's palette.</returns>
    private static PaletteLight Light() => new()
    {
        Background = "#fdf6e3", // editor.background (theme)
        Surface = "#eee8d5", // editorWidget.background (theme)
        DrawerBackground = "#eee8d5", // sideBar.background (theme)
        BackgroundGray = "#e4decf", // sideBarSectionHeader.background (registry default, composited from #80808033 over Background #fdf6e3)
        TableHover = "#eae0c0", // list.hoverBackground (theme, composited from #dfca8844 over DrawerBackground #eee8d5)
        Divider = "#ddd6c1", // panel.border (theme)
        DividerLight = "#ddd6c1", // tab.border (theme, sideBar.border absent)
        LinesInputs = "#d3af86", // dropdown.border (theme, input.border absent)
        LinesDefault = "#b49471", // focusBorder (theme)
        TextPrimary = "#616161", // foreground (registry default)
        TextSecondary = "#717171", // descriptionForeground (registry default)
        TextDisabled = "#a7a49b", // disabledForeground (registry default, composited from #61616180 over Surface #eee8d5)
        Primary = "#006ab1", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#ffffff", // computed: black 3.70:1 vs white 5.68:1 against Primary #006ab1
        Secondary = "#ac9d57", // button.background (theme)
        Success = "#388a34", // charts.green (registry default)
        Error = "#a1260d", // errorForeground (registry default)
        Warning = "#bf8803", // charts.yellow (registry default)
        Info = "#0063d3", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.4)", // fixed for a light theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Solarized Light's descriptor. The dark palette is borrowed from
    /// <see cref="HuddleTheme.Dark"/> rather than authored - Solarized Light is a single-mode
    /// VS Code theme, so selecting it and then forcing Dark mode shows Huddle's own dark
    /// palette, not a Solarized Light dark variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "solarized-light",
        Label: "Solarized Light",
        Theme: ThemeDefaults.Create(Light(), HuddleTheme.Dark()),
        Mode: ThemeMode.Light);
}
