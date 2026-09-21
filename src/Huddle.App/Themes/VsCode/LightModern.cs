// Source: theme-defaults/themes/light_modern.json, from Visual Studio Code 1.138.0.
// Include chain: light_modern.json -> light_plus.json -> light_vs.json, merged child-over-parent.
// light_plus.json carries no `colors` object at all - it is a token-colour delta - so the
// effective palette below is light_modern.json layered over light_vs.json.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Light Modern", uiTheme "vs".
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
/// "Light Modern", the default light theme bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance and include chain.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than
/// <c>button.background</c>; see <see cref="DarkModernTheme"/>'s remarks for why.
/// </remarks>
internal static class LightModernTheme
{
    /// <summary>
    /// Builds a fresh instance of Light Modern's native light palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteLight"/> carrying Light Modern's palette.</returns>
    private static PaletteLight Light() => new()
    {
        Background = "#ffffff", // editor.background (theme)
        Surface = "#f8f8f8", // editorWidget.background (theme)
        DrawerBackground = "#f8f8f8", // sideBar.background (theme)
        BackgroundGray = "#f8f8f8", // sideBarSectionHeader.background (theme)
        TableHover = "#f2f2f2", // list.hoverBackground (theme)
        Divider = "#e5e5e5", // panel.border (theme)
        DividerLight = "#e5e5e5", // sideBar.border (theme)
        LinesInputs = "#cecece", // input.border (theme)
        LinesDefault = "#005fb8", // focusBorder (theme)
        TextPrimary = "#3b3b3b", // foreground (theme)
        TextSecondary = "#3b3b3b", // descriptionForeground (theme)
        TextDisabled = "#acacac", // disabledForeground (registry default, composited from #61616180 over Surface #f8f8f8)
        Primary = "#005fb8", // textLink.foreground (theme) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#ffffff", // computed: black 3.33:1 vs white 6.31:1 against Primary #005fb8
        Secondary = "#005fb8", // button.background (theme)
        Success = "#388a34", // charts.green (registry default)
        Error = "#f85149", // errorForeground (theme)
        Warning = "#bf8803", // charts.yellow (registry default)
        Info = "#0063d3", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.4)", // fixed for a light theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Light Modern's descriptor. It is a single-mode VS Code theme and so carries only its
    /// light palette; MudBlazor's dark slot keeps MudBlazor's own defaults and is never
    /// rendered, because selecting this theme also selects its mode.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "light-modern",
        Label: "Light Modern",
        Theme: ThemeDefaults.CreateLight(Light()),
        Mode: ThemeMode.Light,
        Group: ThemeGroup.Light);
}
