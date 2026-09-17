// Source: theme-defaults/themes/2026-dark.json, from Visual Studio Code 1.138.0.
// Include chain: 2026-dark.json -> dark_modern.json -> dark_plus.json -> dark_vs.json, merged
// child-over-parent. Dark 2026 is the richest theme in this catalog: every mapped slot below
// resolved at 2026-dark.json itself, so nothing here came from a registerColor default.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Dark 2026", uiTheme "vs-dark".
// Extracted 2026-09-16.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Dark 2026", the newest dark theme bundled with Visual Studio Code, ported once at authoring
/// time into a <see cref="MudTheme"/>. See the file header for the source theme's provenance and
/// include chain.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class Dark2026Theme
{
    /// <summary>
    /// Builds a fresh instance of Dark 2026's native dark palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Dark 2026's palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#121314", // editor.background (theme)
        Surface = "#202122", // editorWidget.background (theme)
        DrawerBackground = "#191a1b", // sideBar.background (theme)
        BackgroundGray = "#191a1b", // sideBarSectionHeader.background (theme)
        TableHover = "#2b2c2d", // list.hoverBackground (theme, composited from #ffffff14 over DrawerBackground #191a1b)
        Divider = "#2a2b2c", // panel.border (theme)
        DividerLight = "#2a2b2c", // sideBar.border (theme)
        LinesInputs = "#333536", // input.border (theme)
        LinesDefault = "#2d6e8a", // focusBorder (theme, composited from #3994bcb3 over Background #121314)
        TextPrimary = "#bfbfbf", // foreground (theme)
        TextSecondary = "#8c8c8c", // descriptionForeground (theme)
        TextDisabled = "#555555", // disabledForeground (theme)
        Primary = "#48a0c7", // textLink.foreground (theme) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 7.13:1 vs white 2.95:1 against Primary #48a0c7
        Secondary = "#297aa0", // button.background (theme)
        Success = "#86cf86", // charts.green (theme)
        Error = "#f48771", // errorForeground (theme)
        Warning = "#e0b97f", // charts.yellow (theme)
        Info = "#57a3f8", // charts.blue (theme)
        OverlayDark = "rgba(0,0,0,0.6)", // fixed for a dark theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Dark 2026's descriptor. The light palette is borrowed from <see cref="HuddleTheme.Light"/>
    /// rather than authored - Dark 2026 is a single-mode VS Code theme, so selecting it and then
    /// forcing Light mode shows Huddle's own light palette, not a Dark 2026 light variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "dark-2026",
        Label: "Dark 2026",
        Theme: ThemeDefaults.Create(HuddleTheme.Light(), Dark()),
        Mode: ThemeMode.Dark);
}
