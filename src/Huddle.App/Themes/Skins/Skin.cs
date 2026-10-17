using MudBlazor;

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// One loaded skin: a Theme's identity, the merged result of its file and every file it includes, and the
/// <see cref="MudTheme"/> built from it. Immutable.
/// </summary>
/// <param name="Id">The Theme id <c>appearance.json</c> stores, such as <c>lumen-light</c>.</param>
/// <param name="Label">The display name.</param>
/// <param name="Mode">Whether the skin fills the light or the dark palette slot.</param>
/// <param name="Order">The picker order within the catalogue; lower comes first.</param>
/// <param name="Radius">The default border radius as a CSS length, or <see langword="null"/> for MudBlazor's default.</param>
/// <param name="Faces">The font families the skin names.</param>
/// <param name="Typography">The typography overrides.</param>
/// <param name="Shadows">The literal shadow for each MudBlazor elevation index (1 to 25) the skin sets.</param>
/// <param name="Palette">The merged palette overrides: camelCase palette key to colour text.</param>
/// <param name="Tokens">The merged token overrides: dotted token key to an unresolved skin expression.</param>
/// <param name="Traits">The structural traits.</param>
/// <param name="Pack">The temporary style-pack name (phases 1 and 2), or <see langword="null"/>.</param>
/// <param name="Theme">The MudBlazor theme built from this skin.</param>
internal sealed record Skin(
    string Id,
    string Label,
    ThemeMode Mode,
    int Order,
    string? Radius,
    SkinFaces Faces,
    SkinTypography Typography,
    IReadOnlyDictionary<int, string> Shadows,
    IReadOnlyDictionary<string, string> Palette,
    IReadOnlyDictionary<string, string> Tokens,
    SkinTraits Traits,
    string? Pack,
    MudTheme Theme);
