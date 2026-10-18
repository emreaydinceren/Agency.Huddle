using System.Globalization;
using System.Reflection;
using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// Resolves a skin's palette and token expressions (palette terms, <c>ref()</c>, <c>mix()</c> in sRGB and colour
/// literals) to opaque colours, so a contrast ratio can be measured without a browser. The contrast tests and the Theme
/// lab share it. A term it cannot evaluate (<c>mud()</c>, <c>alpha()</c>, <c>calc()</c>, lengths) throws
/// <see cref="NotSupportedException"/> rather than guessing.
/// </summary>
internal sealed class SkinColourResolver
{
    private const int MaxDepth = 32;

    private static readonly IReadOnlyDictionary<string, string> NoOverrides = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly Skin skin;
    private readonly Palette palette;
    private readonly IReadOnlyDictionary<string, string> paletteOverrides;
    private readonly Dictionary<string, string> tokens = new(StringComparer.Ordinal);

    /// <summary>Resolves <paramref name="skin"/> exactly as the file defines it.</summary>
    /// <param name="skin">The loaded skin.</param>
    public SkinColourResolver(Skin skin)
        : this(skin, NoOverrides, NoOverrides)
    {
    }

    /// <summary>Resolves <paramref name="skin"/> with a palette and token set laid over it, as the Theme lab edits them.</summary>
    /// <param name="skin">The loaded skin.</param>
    /// <param name="paletteOverrides">Palette key (camelCase) to colour text; wins over the skin's own palette.</param>
    /// <param name="tokenOverrides">Token key to skin expression; wins over the skin's own tokens and the registry default.</param>
    public SkinColourResolver(Skin skin, IReadOnlyDictionary<string, string> paletteOverrides, IReadOnlyDictionary<string, string> tokenOverrides)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(paletteOverrides);
        ArgumentNullException.ThrowIfNull(tokenOverrides);

        this.skin = skin;
        this.paletteOverrides = paletteOverrides;
        this.palette = skin.Mode == ThemeMode.Dark ? skin.Theme.PaletteDark : skin.Theme.PaletteLight;
        foreach (Token token in SkinTokens.All)
        {
            string fallback = skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            this.tokens[token.Key] = skin.Tokens.TryGetValue(token.Key, out string? own) ? own : fallback;
        }

        foreach (KeyValuePair<string, string> pair in tokenOverrides)
        {
            this.tokens[pair.Key] = pair.Value;
        }
    }

    /// <summary>A palette colour of the skin's mode.</summary>
    /// <param name="camelName">The palette key, such as <c>textPrimary</c>.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="InvalidOperationException">The key names no palette colour.</exception>
    public MudColor Palette(string camelName)
    {
        ArgumentException.ThrowIfNullOrEmpty(camelName);

        if (this.paletteOverrides.TryGetValue(camelName, out string? overridden))
        {
            return new MudColor(overridden);
        }

        PropertyInfo property = typeof(Palette).GetProperty(char.ToUpperInvariant(camelName[0]) + camelName[1..])
            ?? throw new InvalidOperationException($"{this.skin.Id}: no palette colour '{camelName}'.");
        object? value = property.GetValue(this.palette);
        return value switch
        {
            MudColor colour => colour,
            string text => new MudColor(text),
            _ => throw new InvalidOperationException($"{this.skin.Id}: palette colour '{camelName}' has no value."),
        };
    }

    /// <summary>A token's colour, following its references.</summary>
    /// <param name="key">The dotted token key, such as <c>ground.page</c>.</param>
    /// <returns>The colour.</returns>
    public MudColor Token(string key) => this.Evaluate("ref(" + key + ")");

    /// <summary>Evaluates a skin expression to an opaque colour.</summary>
    /// <param name="expression">The expression, such as <c>mix(#000000 25%, #ffffff)</c>.</param>
    /// <returns>The colour.</returns>
    public MudColor Evaluate(string expression) => this.Evaluate(expression, 0);

    private MudColor Evaluate(string expression, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"{this.skin.Id}: expression '{expression}' nests too deeply (a reference loop?).");
        }

        string text = expression.Trim();
        if (text.StartsWith("ref(", StringComparison.Ordinal) && text.EndsWith(')'))
        {
            string key = text[4..^1].Trim();
            if (!this.tokens.TryGetValue(key, out string? target))
            {
                throw new InvalidOperationException($"{this.skin.Id}: unknown token '{key}'.");
            }

            return this.Evaluate(target, depth + 1);
        }

        if (text.StartsWith("mix(", StringComparison.Ordinal) && text.EndsWith(')'))
        {
            string body = text[4..^1];
            int comma = TopLevelComma(body);
            string first = body[..comma].Trim();
            int space = first.LastIndexOf(' ');
            string percentText = first[(space + 1)..];
            double share = double.Parse(percentText.TrimEnd('%'), CultureInfo.InvariantCulture) / 100.0;
            MudColor a = this.Evaluate(first[..space], depth + 1);
            MudColor b = this.Evaluate(body[(comma + 1)..], depth + 1);
            return new MudColor(
                Blend(a.R, b.R, share),
                Blend(a.G, b.G, share),
                Blend(a.B, b.B, share),
                (byte)255);
        }

        if (text.StartsWith("palette.", StringComparison.Ordinal))
        {
            return this.Palette(text["palette.".Length..]);
        }

        if (text.StartsWith('#'))
        {
            string hex = text.Length == 4 ? string.Concat(text[1], text[1], text[2], text[2], text[3], text[3]) : text[1..];
            return new MudColor("#" + hex);
        }

        throw new NotSupportedException($"{this.skin.Id}: the contrast resolver cannot evaluate '{text}'; extend it before using the term on a checked token.");
    }

    private static byte Blend(byte first, byte second, double share) =>
        (byte)Math.Round((first * share) + (second * (1 - share)), MidpointRounding.AwayFromZero);

    private static int TopLevelComma(string body)
    {
        int depth = 0;
        for (int index = 0; index < body.Length; index++)
        {
            switch (body[index])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return index;
                default:
                    break;
            }
        }

        throw new InvalidOperationException($"mix() needs two colours: '{body}'.");
    }
}
