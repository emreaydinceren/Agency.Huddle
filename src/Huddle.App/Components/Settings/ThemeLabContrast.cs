using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>One contrast badge: a <see cref="SkinContrastPair"/> measured on the lab's current colours.</summary>
/// <param name="Pair">The pair that was measured.</param>
/// <param name="Ratio">The contrast ratio, or <see langword="null"/> when an expression uses a term the resolver cannot evaluate.</param>
internal sealed record ThemeLabContrast(SkinContrastPair Pair, double? Ratio)
{
    /// <summary>Whether the pair reaches its minimum. An unmeasured pair does not pass.</summary>
    public bool Passes => this.Ratio is { } ratio && ratio >= this.Pair.Minimum;
}
