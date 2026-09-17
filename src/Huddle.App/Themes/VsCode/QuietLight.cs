// Source: theme-quietlight/themes/quietlight-color-theme.json, from Visual Studio Code 1.138.0.
// The extension declares "license": "MIT", publisher vscode, with no per-theme LICENSE file
// and no further third-party attribution anywhere in the VS Code tree for this theme.
// The theme's contributes.themes id is "Quiet Light", uiTheme "vs".
// Extracted 2026-09-16.
// Values VS Code's theme JSON does not carry (rows marked "registry default" below) came from
// the registerColor defaults in microsoft/vscode at tag 1.138.0 (MIT), selected for the "light"
// variant.
// This theme's JSON carries no `foreground` key at all, so TextPrimary below falls through to
// the registry `foreground` default rather than a theme-specific value - that is expected, not
// a conversion bug.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Quiet Light", one of the colour themes bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance.
/// </summary>
/// <remarks>
/// See <see cref="DarkModernTheme"/>'s remarks for why this theme's <c>Primary</c> reads
/// <c>textLink.foreground</c> rather than <c>button.background</c>.
/// </remarks>
internal static class QuietLightTheme
{
    /// <summary>
    /// Builds a fresh instance of Quiet Light's native light palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying Quiet Light's palette.</returns>
    private static PaletteLight Light() => new()
    {
        Background = "#f5f5f5", // editor.background (theme)
        Surface = "#f3f3f3", // editorWidget.background (registry default)
        DrawerBackground = "#f2f2f2", // sideBar.background (theme)
        BackgroundGray = "#ede8ef", // sideBarSectionHeader.background (theme)
        TableHover = "#e0e0e0", // list.hoverBackground (theme)
        Divider = "#cccccc", // panel.border (registry default, composited from #80808059 over Background #f5f5f5)
        DividerLight = "#f3f3f3", // tab.border (registry default, sideBar.border absent)
        LinesInputs = "#cecece", // dropdown.border (registry default, input.border absent)
        LinesDefault = "#9769dc", // focusBorder (theme)
        TextPrimary = "#616161", // foreground (registry default; the theme carries no `foreground` key)
        TextSecondary = "#717171", // descriptionForeground (registry default)
        TextDisabled = "#aaaaaa", // disabledForeground (registry default, composited from #61616180 over Surface #f3f3f3)
        Primary = "#006ab1", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#ffffff", // computed: black 3.70:1 vs white 5.68:1 against Primary #006ab1
        Secondary = "#705697", // button.background (theme)
        Success = "#388a34", // charts.green (registry default)
        Error = "#f1897f", // errorForeground (theme)
        Warning = "#bf8803", // charts.yellow (registry default)
        Info = "#0063d3", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.4)", // fixed for a light theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Quiet Light's descriptor. The dark palette is borrowed from <see cref="HuddleTheme.Dark"/>
    /// rather than authored - Quiet Light is a single-mode VS Code theme, so selecting it and then
    /// forcing Dark mode shows Huddle's own dark palette, not a Quiet Light dark variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "quiet-light",
        Label: "Quiet Light",
        Theme: ThemeDefaults.Create(Light(), HuddleTheme.Dark()),
        Mode: ThemeMode.Light);
}
