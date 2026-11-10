namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The two typography values a skin may change. Role sizes, weights and line heights stay with
/// <see cref="ThemeDefaults"/>, so the type scale is the same in every skin.
/// </summary>
/// <param name="LetterSpacing">Written to every text role (Default, Body1, Body2, Caption, Subtitle1, Subtitle2, Button);<see langword="null"/> keeps the default.</param>
/// <param name="LineHeight">Written to the Default role only; <see langword="null"/> keeps the default.</param>
internal sealed record SkinTypography(string? LetterSpacing, string? LineHeight)
{
    /// <summary>A skin that changes no typography.</summary>
    public static SkinTypography None { get; } = new(null, null);
}
