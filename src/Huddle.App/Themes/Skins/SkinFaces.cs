namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The font families a skin names, each as an ordered fallback list. An empty list means the skin does not set that
/// face and the application default applies.
/// </summary>
/// <param name="Ui">Sets <c>Typography.Default.FontFamily</c>; empty means the shared Inter stack.</param>
/// <param name="Read">The reading face; empty means <c>var(--mud-typography-body1-family)</c>.</param>
/// <param name="Mono">The code face; empty means the <c>app-vars.css</c> stack.</param>
internal sealed record SkinFaces(IReadOnlyList<string> Ui, IReadOnlyList<string> Read, IReadOnlyList<string> Mono)
{
    /// <summary>A skin that sets no face.</summary>
    public static SkinFaces None { get; } = new([], [], []);
}
