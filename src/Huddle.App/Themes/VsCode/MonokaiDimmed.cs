// Source: theme-monokai-dimmed/themes/dimmed-monokai-color-theme.json, from Visual Studio Code
// 1.138.0.
// The extension declares "license": "MIT", publisher vscode, with no per-theme LICENSE file
// and no further third-party attribution anywhere in the VS Code tree for this theme.
// The theme's contributes.themes id is "Monokai Dimmed", uiTheme "vs-dark".
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
/// "Monokai Dimmed", one of the colour themes bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance.
/// </summary>
/// <remarks>
/// See <see cref="DarkModernTheme"/>'s remarks for why this theme's <c>Primary</c> reads
/// <c>textLink.foreground</c> rather than <c>button.background</c>.
/// </remarks>
internal static class MonokaiDimmedTheme
{
    /// <summary>
    /// Builds a fresh instance of Monokai Dimmed's native dark palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Monokai Dimmed's palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#1e1e1e", // editor.background (theme)
        Surface = "#252526", // editorWidget.background (registry default)
        DrawerBackground = "#272727", // sideBar.background (theme)
        BackgroundGray = "#505050", // sideBarSectionHeader.background (theme)
        TableHover = "#444444", // list.hoverBackground (theme)
        Divider = "#404040", // panel.border (registry default, composited from #80808059 over Background #1e1e1e)
        DividerLight = "#303030", // tab.border (theme, sideBar.border absent)
        LinesInputs = "#3c3c3c", // dropdown.border (registry default, input.border absent)
        LinesDefault = "#3655b5", // focusBorder (theme)
        TextPrimary = "#cccccc", // foreground (registry default)
        TextSecondary = "#9a9a9a", // descriptionForeground (registry default, composited from #ccccccb2 over Surface #252526)
        TextDisabled = "#797979", // disabledForeground (registry default, composited from #cccccc80 over Surface #252526)
        Primary = "#3794ff", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 6.84:1 vs white 3.07:1 against Primary #3794ff
        Secondary = "#565656", // button.background (theme)
        Success = "#89d185", // charts.green (registry default)
        Error = "#f48771", // errorForeground (registry default)
        Warning = "#cca700", // charts.yellow (registry default)
        Info = "#59a4f9", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.6)", // fixed for a dark theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Monokai Dimmed's descriptor. The light palette is borrowed from
    /// <see cref="HuddleTheme.Light"/> rather than authored - Monokai Dimmed is a single-mode
    /// VS Code theme, so selecting it and then forcing Light mode shows Huddle's own light
    /// palette, not a Monokai Dimmed light variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "monokai-dimmed",
        Label: "Monokai Dimmed",
        Theme: ThemeDefaults.Create(HuddleTheme.Light(), Dark()),
        Mode: ThemeMode.Dark);
}
