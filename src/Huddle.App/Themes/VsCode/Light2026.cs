// Source: theme-defaults/themes/2026-light.json, from Visual Studio Code 1.138.0.
// Include chain: 2026-light.json -> light_modern.json -> light_plus.json -> light_vs.json,
// merged child-over-parent. Light 2026 is the richest theme in this catalog: every mapped slot
// below resolved at 2026-light.json itself, so nothing here came from a registerColor default.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Light 2026", uiTheme "vs".
// Extracted 2026-09-16.
// This file is generated once by hand from that source and is not read at run time - there is
// no importer in this application, no runtime JSON reading, no file provider. The C# below IS
// the artefact.
using MudBlazor;

namespace Agency.Huddle.App.Themes.VsCode;

/// <summary>
/// "Light 2026", the newest light theme bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance and include chain.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class Light2026Theme
{
    /// <summary>
    /// Builds a fresh instance of Light 2026's native light palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying Light 2026's palette.</returns>
    private static PaletteLight Light() => new()
    {
        Background = "#ffffff", // editor.background (theme)
        Surface = "#fafafd", // editorWidget.background (theme)
        DrawerBackground = "#fafafd", // sideBar.background (theme)
        BackgroundGray = "#fafafd", // sideBarSectionHeader.background (theme)
        TableHover = "#e6e6e9", // list.hoverBackground (theme, composited from #00000014 over DrawerBackground #fafafd)
        Divider = "#f0f1f2", // panel.border (theme)
        DividerLight = "#f0f1f2", // sideBar.border (theme)
        LinesInputs = "#efefef", // input.border (theme, composited from #d8d8d866 over Background #ffffff)
        LinesDefault = "#0069cc", // focusBorder (theme)
        TextPrimary = "#202020", // foreground (theme)
        TextSecondary = "#606060", // descriptionForeground (theme)
        TextDisabled = "#bbbbbb", // disabledForeground (theme)
        Primary = "#0069cc", // textLink.foreground (theme) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#ffffff", // computed: black 3.89:1 vs white 5.39:1 against Primary #0069cc
        Secondary = "#0069cc", // button.background (theme)
        Success = "#388a34", // charts.green (theme)
        Error = "#ad0707", // errorForeground (theme)
        Warning = "#667309", // charts.yellow (theme)
        Info = "#1a5cff", // charts.blue (theme)
        OverlayDark = "rgba(0,0,0,0.4)", // fixed for a light theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Light 2026's descriptor. The dark palette is borrowed from <see cref="HuddleTheme.Dark"/>
    /// rather than authored - Light 2026 is a single-mode VS Code theme, so selecting it and then
    /// forcing Dark mode shows Huddle's own dark palette, not a Light 2026 dark variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "light-2026",
        Label: "Light 2026",
        Theme: ThemeDefaults.Create(Light(), HuddleTheme.Dark()),
        Mode: ThemeMode.Light);
}
