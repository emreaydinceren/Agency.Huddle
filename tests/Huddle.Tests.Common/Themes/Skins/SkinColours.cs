using System.Globalization;
using System.Reflection;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Resolves a shipped skin's tokens to the concrete sRGB colours a browser paints, without a browser, for tests that
/// compare colours (a difference floor between named tokens, the order of the line rungs). It follows what the
/// stylesheet does: a skin's own token or the registry default for its mode, <c>ref()</c> and <c>palette.*</c>
/// references, <c>mix()</c> as <c>color-mix(in srgb, ...)</c> (premultiplied, unrounded between steps), <c>alpha()</c> as a
/// mix with <c>transparent</c>, hex, <c>rgb()</c> and <c>rgba()</c> literals; a colour that is not opaque is then composited
/// over the skin's <c>palette.surface</c>. A token whose value is not a colour (a length, a weight, a shadow list, a
/// <c>mud()</c> variable, <c>none</c>) resolves to <see langword="null"/>. This is wider than the app's
/// <c>SkinColourResolver</c>, which stops at opaque colours and rounds every step to bytes.
/// </summary>
internal sealed class SkinColours
{
    private const int MaxDepth = 32;

    private static readonly Dictionary<string, PropertyInfo> PaletteProperties = typeof(Palette)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(static property => property.PropertyType == typeof(MudColor) || property.PropertyType == typeof(string))
        .ToDictionary(static property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..], StringComparer.Ordinal);

    private readonly Skin skin;
    private readonly Palette palette;
    private readonly Dictionary<string, string> expressions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rgba?> cache = new(StringComparer.Ordinal);

    /// <summary>Resolves the tokens of <paramref name="skin"/> as the file defines them.</summary>
    /// <param name="skin">A loaded skin.</param>
    public SkinColours(Skin skin)
    {
        ArgumentNullException.ThrowIfNull(skin);

        this.skin = skin;
        this.palette = skin.Mode == ThemeMode.Dark ? skin.Theme.PaletteDark : skin.Theme.PaletteLight;
        foreach (Token token in SkinTokens.All)
        {
            string fallback = skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            this.expressions[token.Key] = skin.Tokens.TryGetValue(token.Key, out string? own) ? own : fallback;
        }
    }

    /// <summary>One resolver for every built-in skin, which is every Theme in both modes (eight).</summary>
    public static IReadOnlyList<SkinColours> All { get; } = [.. SkinLoader.LoadAll().Select(static skin => new SkinColours(skin))];

    /// <summary>The skin's id, such as <c>lumen-light</c>.</summary>
    public string Id => this.skin.Id;

    /// <summary>The skin's <c>palette.surface</c>, which every translucent colour is composited over.</summary>
    public SrgbColour Surface => this.Flatten(this.PaletteColour("surface"));

    /// <summary>The key of every token whose value is a colour, in registry order.</summary>
    public IReadOnlyList<string> ColourKeys => [.. SkinTokens.All.Select(static token => token.Key).Where(key => this.Token(key) is not null)];

    /// <summary>The ink ladder's rungs: every <c>ink.*</c> token that is a colour, in registry order.</summary>
    public IReadOnlyList<string> InkRungs => [.. this.ColourKeys.Where(static key => key.StartsWith("ink.", StringComparison.Ordinal))];

    /// <summary>A token's colour over the surface, following its references.</summary>
    /// <param name="key">The dotted token key, such as <c>ink.rule</c>.</param>
    /// <returns>The colour, or <see langword="null"/> when the token's value is not a colour.</returns>
    /// <exception cref="InvalidOperationException">The key names no registered token.</exception>
    public SrgbColour? Token(string key)
    {
        Rgba? colour = this.TokenColour(key, 0);
        return colour is { } found ? this.Flatten(found) : null;
    }

    /// <summary>A palette colour of the skin's mode over the surface.</summary>
    /// <param name="camelName">The palette key, such as <c>textPrimary</c>.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="InvalidOperationException">The key names no palette colour.</exception>
    public SrgbColour Palette(string camelName) => this.Flatten(this.PaletteColour(camelName));

    /// <summary>Every colour token resolved over the surface.</summary>
    /// <returns>Token key to colour, for each token whose value is a colour.</returns>
    public IReadOnlyDictionary<string, SrgbColour> AllColours()
    {
        Dictionary<string, SrgbColour> result = new(StringComparer.Ordinal);
        foreach (string key in this.ColourKeys)
        {
            if (this.Token(key) is { } colour)
            {
                result[key] = colour;
            }
        }

        return result;
    }

    /// <summary>The CIEDE2000 difference between two tokens of this skin, each resolved over the surface.</summary>
    /// <param name="firstKey">One token key.</param>
    /// <param name="secondKey">The other token key.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="InvalidOperationException">Either token is not a colour.</exception>
    public double Difference(string firstKey, string secondKey) =>
        DeltaE.Ciede2000(this.Required(firstKey), this.Required(secondKey));

    private SrgbColour Required(string key) =>
        this.Token(key) ?? throw new InvalidOperationException($"{this.skin.Id}: token '{key}' is not a colour.");

    private SrgbColour Flatten(Rgba colour)
    {
        if (colour.A >= 1)
        {
            return new SrgbColour(colour.R, colour.G, colour.B);
        }

        MudColor surface = this.RawPalette("surface");
        return new SrgbColour(
            (colour.R * colour.A) + (surface.R * (1 - colour.A)),
            (colour.G * colour.A) + (surface.G * (1 - colour.A)),
            (colour.B * colour.A) + (surface.B * (1 - colour.A)));
    }

    private Rgba PaletteColour(string camelName)
    {
        MudColor colour = this.RawPalette(camelName);
        if (colour.A == 255)
        {
            return new Rgba(colour.R, colour.G, colour.B, 1);
        }

        // MudBlazor writes a translucent palette entry as rgba() text with the alpha rounded to two decimals; the browser sees that text, not the byte.
        return this.Evaluate(colour.ToString(MudColorOutputFormats.RGBA), 0) ?? new Rgba(colour.R, colour.G, colour.B, colour.A / 255.0);
    }

    private MudColor RawPalette(string camelName)
    {
        // A skin expression names the MudBlazor variable (palette.primaryText is --mud-palette-primary-text), whose C# property is PrimaryContrastText.
        string name = !PaletteProperties.ContainsKey(camelName) && camelName.EndsWith("Text", StringComparison.Ordinal)
            ? camelName[..^"Text".Length] + "ContrastText"
            : camelName;
        if (PaletteProperties.TryGetValue(name, out PropertyInfo? property))
        {
            // Most palette entries are a MudColor; a few (the primary's darken and lighten) are CSS colour text.
            switch (property.GetValue(this.palette))
            {
                case MudColor colour:
                    return colour;
                case string text when !string.IsNullOrWhiteSpace(text):
                    return new MudColor(text);
                default:
                    break;
            }
        }

        throw new InvalidOperationException($"{this.skin.Id}: no palette colour '{camelName}'.");
    }

    private Rgba? TokenColour(string key, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"{this.skin.Id}: token '{key}' nests too deeply (a reference loop?).");
        }

        if (!this.expressions.TryGetValue(key, out string? expression))
        {
            throw new InvalidOperationException($"{this.skin.Id}: unknown token '{key}'.");
        }

        if (!this.cache.TryGetValue(key, out Rgba? colour))
        {
            colour = this.Evaluate(expression, depth + 1);
            this.cache[key] = colour;
        }

        return colour;
    }

    private Rgba? Evaluate(string expression, int depth)
    {
        string text = expression.Trim();
        if (SingleCall(text, "ref", out string key))
        {
            return this.TokenColour(key.Trim(), depth);
        }

        if (SingleCall(text, "mix", out string mixBody))
        {
            return this.Mix(mixBody, transparentSecond: false, depth);
        }

        if (SingleCall(text, "alpha", out string alphaBody))
        {
            return this.Mix(alphaBody, transparentSecond: true, depth);
        }

        if (SingleCall(text, "rgba", out string rgbaBody) || SingleCall(text, "rgb", out rgbaBody))
        {
            return ParseFunctionalRgb(rgbaBody);
        }

        if (text.StartsWith("palette.", StringComparison.Ordinal) && !text.Contains(' '))
        {
            return this.PaletteColour(text["palette.".Length..]);
        }

        if (string.Equals(text, "transparent", StringComparison.Ordinal))
        {
            return new Rgba(0, 0, 0, 0);
        }

        return text.StartsWith('#') ? ParseHex(text) : null;
    }

    /// <summary>
    /// <c>color-mix(in srgb, A p%, B)</c> (or, for <c>alpha()</c>, with <c>transparent</c> as B): each colour is weighted by
    /// its share and its own alpha, then divided by the mixed alpha, which is what CSS specifies for a premultiplied space.
    /// </summary>
    private Rgba? Mix(string body, bool transparentSecond, int depth)
    {
        string first = body;
        string? second = null;
        if (!transparentSecond)
        {
            int comma = TopLevelComma(body);
            if (comma < 0)
            {
                return null;
            }

            first = body[..comma];
            second = body[(comma + 1)..];
        }

        first = first.Trim();
        int space = first.LastIndexOf(' ');
        if (space < 0 || !first.EndsWith('%')
            || !double.TryParse(first[(space + 1)..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
        {
            return null;
        }

        Rgba? a = this.Evaluate(first[..space], depth + 1);
        Rgba? b = transparentSecond ? new Rgba(0, 0, 0, 0) : this.Evaluate(second ?? string.Empty, depth + 1);
        if (a is not { } left || b is not { } right)
        {
            return null;
        }

        double share = percent / 100.0;
        double alpha = (left.A * share) + (right.A * (1 - share));
        if (alpha <= 0)
        {
            return new Rgba(0, 0, 0, 0);
        }

        return new Rgba(
            ((left.R * left.A * share) + (right.R * right.A * (1 - share))) / alpha,
            ((left.G * left.A * share) + (right.G * right.A * (1 - share))) / alpha,
            ((left.B * left.A * share) + (right.B * right.A * (1 - share))) / alpha,
            alpha);
    }

    /// <summary>True when the whole text is one call <c>name( ... )</c> whose closing parenthesis is the last character.</summary>
    private static bool SingleCall(string text, string name, out string body)
    {
        body = string.Empty;
        if (!text.StartsWith(name + "(", StringComparison.Ordinal) || !text.EndsWith(')'))
        {
            return false;
        }

        int depth = 0;
        for (int index = name.Length; index < text.Length; index++)
        {
            if (text[index] == '(')
            {
                depth++;
            }
            else if (text[index] == ')' && --depth == 0 && index != text.Length - 1)
            {
                return false;
            }
        }

        body = text[(name.Length + 1)..^1];
        return depth == 0;
    }

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

        return -1;
    }

    private static Rgba? ParseHex(string text)
    {
        string hex = text[1..];
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex.Select(static c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8) || !hex.All(char.IsAsciiHexDigit))
        {
            return null;
        }

        double alpha = hex.Length == 8 ? int.Parse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0 : 1;
        return new Rgba(
            int.Parse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            alpha);
    }

    /// <summary><c>rgb(r, g, b)</c>, <c>rgba(r, g, b, a)</c> and the space and slash forms; a percentage alpha is read as a share.</summary>
    private static Rgba? ParseFunctionalRgb(string body)
    {
        string[] parts = body.Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not (3 or 4))
        {
            return null;
        }

        double[] channels = new double[3];
        for (int index = 0; index < 3; index++)
        {
            bool isPercent = parts[index].EndsWith('%');
            if (!double.TryParse(parts[index].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return null;
            }

            channels[index] = isPercent ? value * 255 / 100 : value;
        }

        double alpha = 1;
        if (parts.Length == 4)
        {
            bool isPercent = parts[3].EndsWith('%');
            if (!double.TryParse(parts[3].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out alpha))
            {
                return null;
            }

            alpha = isPercent ? alpha / 100 : alpha;
        }

        return new Rgba(channels[0], channels[1], channels[2], Math.Clamp(alpha, 0, 1));
    }

    private readonly record struct Rgba(double R, double G, double B, double A);
}
