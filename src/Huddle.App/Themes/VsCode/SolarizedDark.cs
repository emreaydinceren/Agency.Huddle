// Source: theme-solarized-dark/themes/solarized-dark-color-theme.json, from Visual Studio Code
// 1.138.0.
// The extension declares "license": "MIT", publisher vscode, with no per-theme LICENSE file
// and no further third-party attribution anywhere in the VS Code tree for this theme.
// The theme's contributes.themes id is "Solarized Dark", uiTheme "vs-dark".
// Extracted 2026-09-16.
// Values VS Code's theme JSON does not carry (rows marked "registry default" below) came from
// the registerColor defaults in microsoft/vscode at tag 1.138.0 (MIT), selected for the "dark"
// variant.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Solarized Dark", one of the colour themes bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance.
/// </summary>
/// <remarks>
/// See <see cref="DarkModernTheme"/>'s remarks for why this theme's <c>Primary</c> reads
/// <c>textLink.foreground</c> rather than <c>button.background</c>.
/// </remarks>
internal static class SolarizedDarkTheme
{
    /// <summary>
    /// Builds a fresh instance of Solarized Dark's native dark palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Solarized Dark's palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#002b36", // editor.background (theme)
        Surface = "#00212b", // editorWidget.background (theme)
        DrawerBackground = "#00212b", // sideBar.background (theme)
        BackgroundGray = "#1a3c45", // sideBarSectionHeader.background (registry default, composited from #80808033 over Background #002b36)
        TableHover = "#003846", // list.hoverBackground (theme, composited from #004454aa over DrawerBackground #00212b)
        Divider = "#2b2b4a", // panel.border (theme)
        DividerLight = "#003847", // tab.border (theme, sideBar.border absent)
        LinesInputs = "#197271", // dropdown.border (theme, input.border absent; composited from #2aa19899 over Background #002b36)
        LinesDefault = "#197271", // focusBorder (theme, composited from #2aa19899 over Background #002b36)
        TextPrimary = "#cccccc", // foreground (registry default)
        TextSecondary = "#8f999c", // descriptionForeground (registry default, composited from #ccccccb2 over Surface #00212b)
        TextDisabled = "#66777c", // disabledForeground (registry default, composited from #cccccc80 over Surface #00212b)
        Primary = "#3794ff", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 6.84:1 vs white 3.07:1 against Primary #3794ff
        Secondary = "#197271", // button.background (theme, composited from #2aa19899 over Background #002b36)
        Success = "#89d185", // charts.green (registry default)
        Error = "#ffeaea", // errorForeground (theme)
        Warning = "#cca700", // charts.yellow (registry default)
        Info = "#59a4f9", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.6)", // fixed for a dark theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Solarized Dark's descriptor. It is a single-mode VS Code theme and so carries only its
    /// dark palette; MudBlazor's light slot keeps MudBlazor's own defaults and is never
    /// rendered, because selecting this theme also selects its mode.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "solarized-dark",
        Label: "Solarized Dark",
        Theme: ThemeDefaults.CreateDark(Dark()),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);
}
