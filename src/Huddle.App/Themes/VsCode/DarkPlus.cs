// Source: theme-defaults/themes/dark_plus.json, from Visual Studio Code 1.138.0.
// Include chain: dark_plus.json -> dark_vs.json, merged child-over-parent. dark_plus.json
// carries no `colors` object at all - it is a pure token-colour delta, shipped as a
// user-selectable theme in its own right - so every slot below actually resolves from
// dark_vs.json plus VS Code's own registry defaults; nothing in this file comes from
// dark_plus.json itself.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Dark+", uiTheme "vs-dark".
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
/// "Dark+", Visual Studio Code's original default dark theme, ported once at authoring time into
/// a <see cref="MudTheme"/>. See the file header for the source theme's provenance and include
/// chain - and the surprising fact that dark_plus.json itself carries no colours at all.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class DarkPlusTheme
{
    /// <summary>
    /// Builds a fresh instance of Dark+'s native dark palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Dark+'s palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#1e1e1e", // editor.background (theme)
        Surface = "#252526", // editorWidget.background (registry default)
        DrawerBackground = "#252526", // sideBar.background (registry default)
        BackgroundGray = "#1e1e1e", // sideBarSectionHeader.background (theme, composited from #00000000 over Background #1e1e1e)
        TableHover = "#2a2d2e", // list.hoverBackground (registry default)
        Divider = "#404040", // panel.border (registry default, composited from #80808059 over Background #1e1e1e)
        DividerLight = "#252526", // tab.border (registry default, sideBar.border absent)
        LinesInputs = "#3c3c3c", // dropdown.border (registry default, input.border absent)
        LinesDefault = "#007fd4", // focusBorder (registry default)
        TextPrimary = "#cccccc", // foreground (registry default)
        TextSecondary = "#9a9a9a", // descriptionForeground (registry default, composited from #ccccccb2 over Surface #252526)
        TextDisabled = "#797979", // disabledForeground (registry default, composited from #cccccc80 over Surface #252526)
        Primary = "#3794ff", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 6.84:1 vs white 3.07:1 against Primary #3794ff
        Secondary = "#0e639c", // button.background (registry default)
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
    /// Dark+'s descriptor. It is a single-mode VS Code theme and so carries only its
    /// dark palette; MudBlazor's light slot keeps MudBlazor's own defaults and is never
    /// rendered, because selecting this theme also selects its mode.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "dark-plus",
        Label: "Dark+",
        Theme: ThemeDefaults.CreateDark(Dark()),
        Mode: ThemeMode.Dark,
        Group: ThemeGroup.Dark);
}
