using System.Buffers;
using System.Text;

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// Writes the <c>:root{...}</c> block that declares every skin token, either the registry defaults (which
/// <c>app-vars.css</c> carries verbatim) or a skin's overrides merged over them. The output holds none of
/// <c>&lt; &gt; &amp; @</c>, so the layout can write it as raw markup inside a <c>style</c> element.
/// </summary>
internal static class SkinCss
{
    private static readonly SearchValues<char> UnsafeFaceCharacters = SearchValues.Create("<>&@;{}\\");

    /// <summary>Renders the registry defaults for <paramref name="mode"/> with <paramref name="overrides"/> applied.</summary>
    /// <param name="overrides">Token key to skin expression: the tokens the skin sets.</param>
    /// <param name="mode">Whether the light or dark default of each unset token is used.</param>
    /// <param name="skinId">The id of the skin, for error messages.</param>
    /// <param name="faces">The skin's font stacks, or <see langword="null"/> for none.</param>
    /// <returns>A <c>:root</c> block declaring every token, plus the font variables the faces set.</returns>
    /// <exception cref="SkinException">An override names no token, a value is invalid, a reference loops, or a face is unsafe.</exception>
    public static string Render(IReadOnlyDictionary<string, string> overrides, ThemeMode mode, string skinId, SkinFacesCss? faces)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(skinId);

        foreach (string key in overrides.Keys)
        {
            if (!SkinTokens.Contains(key))
            {
                throw new SkinException($"unknown token '{key}' in skin '{skinId}'");
            }
        }

        Dictionary<string, string> merged = new(StringComparer.Ordinal);
        foreach (Token token in SkinTokens.All)
        {
            string fallback = mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            merged[token.Key] = overrides.TryGetValue(token.Key, out string? value) ? value : fallback;
        }

        SkinExpression.Validate(merged, skinId);

        StringBuilder css = new(":root{");
        foreach (Token token in SkinTokens.All)
        {
            _ = css.Append(token.CssName).Append(':').Append(SkinExpression.ToCss(merged[token.Key], token.Key, skinId)).Append(';');
        }

        AppendFace(css, "--font-read", faces?.Read, skinId);
        AppendFace(css, "--font-mono", faces?.Mono, skinId);
        return css.Append('}').ToString();
    }

    private static void AppendFace(StringBuilder css, string name, string? face, string skinId)
    {
        if (face is null)
        {
            return;
        }

        if (face.AsSpan().IndexOfAny(UnsafeFaceCharacters) >= 0 || string.IsNullOrWhiteSpace(face))
        {
            throw new SkinException($"unsafe font stack '{face}' for {name} in skin '{skinId}'");
        }

        _ = css.Append(name).Append(':').Append(face).Append(';');
    }
}
