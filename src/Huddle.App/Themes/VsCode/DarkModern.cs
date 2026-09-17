// Source: theme-defaults/themes/dark_modern.json, from Visual Studio Code 1.138.0.
// Include chain: dark_modern.json -> dark_plus.json -> dark_vs.json, merged child-over-parent.
// dark_plus.json carries no `colors` object at all - it is a token-colour delta - so the
// effective palette below is dark_modern.json layered over dark_vs.json.
// The extension declares "license": "MIT", publisher vscode; the theme's contributes.themes
// id is "Dark Modern", uiTheme "vs-dark".
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
/// "Dark Modern", the default dark theme bundled with Visual Studio Code, ported once at
/// authoring time into a <see cref="MudTheme"/>. See the file header for the source theme's
/// provenance and include chain.
/// </summary>
/// <remarks>
/// This theme's <c>Primary</c> palette entry reads <c>textLink.foreground</c> rather than the
/// more obvious <c>button.background</c>. <c>--mud-palette-primary</c> paints the active nav
/// link's text, so it must be legible on its own against the page background;
/// <c>button.background</c> is a fill colour designed to carry <c>button.foreground</c> printed
/// on top of it, and is not picked to clear 4.5:1 against the editor ground by itself. Mapping
/// <c>Primary</c> to <c>button.background</c> failed the text-contrast floor in 16 of the 19 VS
/// Code themes converted for this catalog; <c>textLink.foreground</c> is chosen for every one of
/// them instead, and later theme files in this series rely on this note rather than repeating it.
/// </remarks>
internal static class DarkModernTheme
{
    /// <summary>
    /// Builds a fresh instance of Dark Modern's native dark palette on every call, so no
    /// <see cref="Palette"/> instance is shared - and mutated - across the themes in this
    /// catalog. Every property below carries the VS Code (or registry-default) key it was
    /// converted from as a trailing comment; since no importer ships, that comment is the only
    /// remaining record of the mapping.
    /// </summary>
    /// <returns>A new <see cref="PaletteDark"/> carrying Dark Modern's palette.</returns>
    private static PaletteDark Dark() => new()
    {
        Background = "#1f1f1f", // editor.background (theme)
        Surface = "#202020", // editorWidget.background (theme)
        DrawerBackground = "#181818", // sideBar.background (theme)
        BackgroundGray = "#181818", // sideBarSectionHeader.background (theme)
        TableHover = "#2a2d2e", // list.hoverBackground (registry default)
        Divider = "#2b2b2b", // panel.border (theme)
        DividerLight = "#2b2b2b", // sideBar.border (theme)
        LinesInputs = "#3c3c3c", // input.border (theme)
        LinesDefault = "#0078d4", // focusBorder (theme)
        TextPrimary = "#cccccc", // foreground (theme)
        TextSecondary = "#9d9d9d", // descriptionForeground (theme)
        TextDisabled = "#767676", // disabledForeground (registry default, composited from #cccccc80 over Surface #202020)
        Primary = "#4daafc", // textLink.foreground (theme) - see the type-level remarks for why not button.background
        PrimaryContrastText = "#000000", // computed: black 8.47:1 vs white 2.48:1 against Primary #4daafc
        Secondary = "#0078d4", // button.background (theme)
        Success = "#89d185", // charts.green (registry default)
        Error = "#f85149", // errorForeground (theme)
        Warning = "#cca700", // charts.yellow (registry default)
        Info = "#59a4f9", // charts.blue (registry default)
        OverlayDark = "rgba(0,0,0,0.6)", // fixed for a dark theme, matching HuddleTheme
        // PrimaryDarken, PrimaryLighten, ErrorLighten and WarningLighten are deliberately left
        // unset: MudBlazor computes them from the base colour, and only HuddleTheme carries
        // hand-picked overrides worth keeping.
    };

    /// <summary>
    /// Dark Modern's descriptor. The light palette is borrowed from <see cref="HuddleTheme.Light"/>
    /// rather than authored - Dark Modern is a single-mode VS Code theme, so selecting it and then
    /// forcing Light mode shows Huddle's own light palette, not a Dark Modern light variant.
    /// </summary>
    public static ThemeDescriptor Descriptor { get; } = new(
        Id: "dark-modern",
        Label: "Dark Modern",
        Theme: ThemeDefaults.Create(HuddleTheme.Light(), Dark()),
        Mode: ThemeMode.Dark);
}
