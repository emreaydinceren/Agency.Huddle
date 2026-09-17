// Source: theme-tomorrow-night-blue/themes/tomorrow-night-blue-color-theme.json, from Visual
// Studio Code 1.138.0.
// The extension declares "license": "MIT", publisher vscode, with no per-theme LICENSE file
// and no further third-party attribution anywhere in the VS Code tree for this theme.
// The theme's contributes.themes id is "Tomorrow Night Blue", uiTheme "vs-dark".
// Extracted 2026-09-16.
// Values VS Code's theme JSON does not carry (rows marked "registry default" below) came from
// the registerColor defaults in microsoft/vscode at tag 1.138.0 (MIT), selected for the "dark"
// variant.
// This theme's JSON carries no `button.background` key at all, so Secondary below falls
// through to the registry `button.background` default rather than a theme-specific value -
// that is expected, not a conversion bug.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Tomorrow Night Blue", one of the colour themes bundled with Visual Studio Code, ported once
/// at authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance.
/// </summary>
/// <remarks>
/// See <see cref="DarkModernTheme"/>'s remarks for why this theme's <c>Primary</c> reads
/// <c>textLink.foreground</c> rather than <c>button.background</c>.
/// </remarks>
internal static class TomorrowNightBlueTheme
{
    /// <summary>
    /// Builds a fresh instance of Tomorrow Night Blue's native dark palette on every call, so
    /// no <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Tomorrow Night Blue's palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#002451", // editor.background (theme)
        Surface = "#001c40", // editorWidget.background (theme)
        DrawerBackground = "#001c40", // sideBar.background (theme)
        BackgroundGray = "#1a365a", // sideBarSectionHeader.background (registry default, composited from #80808033 over Background #002451)
        TableHover = "#304764", // list.hoverBackground (theme, composited from #ffffff30 over DrawerBackground #001c40)
        Divider = "#2d4461", // panel.border (registry default, composited from #80808059 over Background #002451)
        DividerLight = "#252526", // tab.border (registry default, sideBar.border absent)
        LinesInputs = "#3c3c3c", // dropdown.border (registry default, input.border absent)
        LinesDefault = "#bbdaff", // focusBorder (theme)
        TextPrimary = "#cccccc", // foreground (registry default)
        TextSecondary = "#8f97a2", // descriptionForeground (registry default, composited from #ccccccb2 over Surface #001c40)
        TextDisabled = "#667486", // disabledForeground (registry default, composited from #cccccc80 over Surface #001c40)
        Primary = "#3794ff", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 6.84:1 vs white 3.07:1 against Primary #3794ff
        Secondary = "#0e639c", // button.background (registry default; the theme carries no `button.background` key)
        Success = "#89d185", // charts.green (registry default)
        Error = "#a92049", // errorForeground (theme)
        Warning = "#cca700", // charts.yellow (registry default)
        Info = "#59a4f9", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.6)", // fixed for a dark theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Tomorrow Night Blue's descriptor. The light palette is borrowed from
    /// <see cref="HuddleTheme.Light"/> rather than authored - Tomorrow Night Blue is a
    /// single-mode VS Code theme, so selecting it and then forcing Light mode shows Huddle's
    /// own light palette, not a Tomorrow Night Blue light variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "tomorrow-night-blue",
        Label: "Tomorrow Night Blue",
        Theme: ThemeDefaults.Create(HuddleTheme.Light(), Dark()),
        Mode: ThemeMode.Dark);
}
