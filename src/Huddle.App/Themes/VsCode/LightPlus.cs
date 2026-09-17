// Source: theme-defaults/themes/light_plus.json, from Visual Studio Code 1.138.0.
// Include chain: light_plus.json -> light_vs.json, merged child-over-parent. light_plus.json
// carries no `colors` object at all - it is a pure token-colour delta, shipped as a
// user-selectable theme in its own right - so every slot below actually resolves from
// light_vs.json plus VS Code's own registry defaults; nothing in this file comes from
// light_plus.json itself.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Light+", uiTheme "vs".
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
/// "Light+", Visual Studio Code's original default light theme, ported once at authoring time
/// into a <see cref="MudTheme"/>. See the file header for the source theme's provenance and
/// include chain - and the surprising fact that light_plus.json itself carries no colours at
/// all.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class LightPlusTheme
{
    /// <summary>
    /// Builds a fresh instance of Light+'s native light palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying Light+'s palette.</returns>
    private static PaletteLight Light() => new()
    {
        Background = "#ffffff", // editor.background (theme)
        Surface = "#f3f3f3", // editorWidget.background (registry default)
        DrawerBackground = "#f3f3f3", // sideBar.background (registry default)
        BackgroundGray = "#ffffff", // sideBarSectionHeader.background (theme, composited from #00000000 over Background #ffffff)
        TableHover = "#e8e8e8", // list.hoverBackground (theme)
        Divider = "#d3d3d3", // panel.border (registry default, composited from #80808059 over Background #ffffff)
        DividerLight = "#f3f3f3", // tab.border (registry default, sideBar.border absent)
        LinesInputs = "#cecece", // dropdown.border (registry default, input.border absent)
        LinesDefault = "#0090f1", // focusBorder (registry default)
        TextPrimary = "#616161", // foreground (registry default)
        TextSecondary = "#717171", // descriptionForeground (registry default)
        TextDisabled = "#aaaaaa", // disabledForeground (registry default, composited from #61616180 over Surface #f3f3f3)
        Primary = "#006ab1", // textLink.foreground (registry default) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#ffffff", // computed: black 3.70:1 vs white 5.68:1 against Primary #006ab1
        Secondary = "#007acc", // button.background (registry default)
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
    /// Light+'s descriptor. The dark palette is borrowed from <see cref="HuddleTheme.Dark"/>
    /// rather than authored - Light+ is a single-mode VS Code theme, so selecting it and then
    /// forcing Dark mode shows Huddle's own dark palette, not a Light+ dark variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "light-plus",
        Label: "Light+",
        Theme: ThemeDefaults.Create(Light(), HuddleTheme.Dark()),
        Mode: ThemeMode.Light);
}
