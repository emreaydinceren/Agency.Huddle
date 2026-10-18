using System.Text;

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// A Human's overrides of one Theme: palette colours and skin tokens, keyed exactly as a skin file keys them. Immutable.
/// Validate one with <see cref="SkinLoader.Sanitize"/> before trusting it, and merge it over its skin with
/// <see cref="SkinLoader.Customize"/>.
/// </summary>
/// <param name="Palette">Camel-case palette key (such as <c>primary</c>) to colour text.</param>
/// <param name="Tokens">Dotted token key (such as <c>ground.rail</c>) to skin expression.</param>
internal sealed record SkinCustomization(IReadOnlyDictionary<string, string> Palette, IReadOnlyDictionary<string, string> Tokens)
{
    /// <summary>A customization that overrides nothing.</summary>
    public static SkinCustomization Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Whether nothing is overridden.</summary>
    public bool IsEmpty => this.Palette.Count == 0 && this.Tokens.Count == 0;

    /// <summary>
    /// A canonical text for this content: equal content gives equal text whatever order the entries were added in. It keys
    /// the cache of built Themes, so a Theme is rebuilt only when what it is built from changes.
    /// </summary>
    public string CacheKey
    {
        get
        {
            StringBuilder key = new();
            foreach (KeyValuePair<string, string> entry in this.Palette.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                _ = key.Append("p|").Append(entry.Key).Append('=').Append(entry.Value).Append('\n');
            }

            foreach (KeyValuePair<string, string> entry in this.Tokens.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                _ = key.Append("t|").Append(entry.Key).Append('=').Append(entry.Value).Append('\n');
            }

            return key.ToString();
        }
    }
}
