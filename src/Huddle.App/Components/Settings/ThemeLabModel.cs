using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor.Utilities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// The Theme lab's state, free of rendering: a base skin and the palette and token values being edited over it. An edit
/// is validated exactly as a skin file's values are (the palette key set, the colour pattern, the token registry and
/// <see cref="SkinExpression"/>) and refused with a message when it fails, so the lab can never hold a value that would
/// not load. Everything the page shows is derived from here: the style block, the MudBlazor palette variables, the
/// contrast badges and the exported JSON.
/// </summary>
internal sealed partial class ThemeLabModel
{
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // The text goes to the clipboard, not into HTML, so keep + and ' readable.
    };

    private readonly Dictionary<string, string> palette = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> basePalette = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> baseTokens = new(StringComparer.Ordinal);

    /// <summary>Starts the lab on <paramref name="skin"/>, every value as that skin has it.</summary>
    /// <param name="skin">The skin to edit over.</param>
    public ThemeLabModel(Skin skin)
    {
        ArgumentNullException.ThrowIfNull(skin);

        this.Skin = skin;
        SkinColourResolver resolver = new(skin);
        foreach (string key in SkinLoader.PaletteKeys)
        {
            string value = FormatColour(resolver.Palette(key));
            this.palette[key] = value;
            this.basePalette[key] = value;
        }

        foreach (Token token in SkinTokens.All)
        {
            string fallback = skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            string value = skin.Tokens.TryGetValue(token.Key, out string? own) ? own : fallback;
            this.tokens[token.Key] = value;
            this.baseTokens[token.Key] = value;
        }
    }

    /// <summary>The skin being edited over.</summary>
    public Skin Skin { get; }

    /// <summary>Every palette key with its current colour text.</summary>
    public IReadOnlyDictionary<string, string> Palette => this.palette;

    /// <summary>Every token key with its current skin expression.</summary>
    public IReadOnlyDictionary<string, string> Tokens => this.tokens;

    /// <summary>The <c>:root</c> block the page puts in the <c>hu-skin</c> style element: every token, with the edits applied.</summary>
    public string StyleCss => SkinCss.Render(this.tokens, this.Skin.Mode, this.Skin.Id, FacesOf(this.Skin.Faces));

    /// <summary>The MudBlazor custom properties (such as <c>--mud-palette-text-primary</c>) to colour text, for every palette key.</summary>
    public IReadOnlyDictionary<string, string> PaletteVariables =>
        this.palette.ToDictionary(static pair => "--mud-palette-" + SkinLoader.Kebab(pair.Key), static pair => pair.Value, StringComparer.Ordinal);

    /// <summary>The token group a key is drawn under: its first dotted segment, such as <c>ground</c> for <c>ground.page</c>.</summary>
    /// <param name="tokenKey">A token key.</param>
    /// <returns>The group name.</returns>
    public static string GroupOf(string tokenKey)
    {
        ArgumentNullException.ThrowIfNull(tokenKey);
        int dot = tokenKey.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? tokenKey : tokenKey[..dot];
    }

    /// <summary>Whether the palette key's current value differs from the base skin's.</summary>
    /// <param name="key">A palette key.</param>
    /// <returns><see langword="true"/> when edited.</returns>
    public bool IsPaletteChanged(string key) =>
        this.palette.TryGetValue(key, out string? current) && !SameColour(current, this.basePalette[key]);

    /// <summary>Whether the token's current expression differs from the base skin's.</summary>
    /// <param name="key">A token key.</param>
    /// <returns><see langword="true"/> when edited.</returns>
    public bool IsTokenChanged(string key) =>
        this.tokens.TryGetValue(key, out string? current) && !string.Equals(current, this.baseTokens[key], StringComparison.Ordinal);

    /// <summary>Sets a palette colour.</summary>
    /// <param name="key">A palette key, such as <c>primary</c>.</param>
    /// <param name="value">A hex or <c>rgb()</c>/<c>rgba()</c> colour.</param>
    /// <returns><see langword="null"/> when accepted; otherwise why it was refused, with the value left as it was.</returns>
    public string? SetPalette(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (!this.palette.ContainsKey(key))
        {
            return $"'{key}' is not a palette key.";
        }

        string text = value.Trim();
        if (!ColourPattern().IsMatch(text))
        {
            return $"'{text}' is not a hex or rgb()/rgba() colour.";
        }

        this.palette[key] = text;
        return null;
    }

    /// <summary>Sets a token's expression.</summary>
    /// <param name="key">A token key, such as <c>ground.page</c>.</param>
    /// <param name="expression">A skin expression.</param>
    /// <returns><see langword="null"/> when accepted; otherwise the grammar's message, with the value left as it was.</returns>
    public string? SetToken(string key, string expression)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(expression);

        if (!this.tokens.TryGetValue(key, out string? before))
        {
            return $"'{key}' is not a token.";
        }

        string text = expression.Trim();
        if (text.Length == 0)
        {
            return "The expression is empty.";
        }

        this.tokens[key] = text;
        try
        {
            _ = SkinCss.Render(this.tokens, this.Skin.Mode, this.Skin.Id, null);
        }
        catch (SkinException exception)
        {
            this.tokens[key] = before;
            return exception.Message;
        }

        return null;
    }

    /// <summary>Measures every <see cref="SkinContrastPairs.All"/> pair on the current colours.</summary>
    /// <returns>One result per pair, in the pair list's order.</returns>
    public IReadOnlyList<ThemeLabContrast> Contrast()
    {
        SkinColourResolver resolver = new(this.Skin, this.palette, this.tokens);
        List<ThemeLabContrast> results = [];
        foreach (SkinContrastPair pair in SkinContrastPairs.All)
        {
            results.Add(new ThemeLabContrast(pair, Measure(resolver, pair)));
        }

        return results;
    }

    /// <summary>The edits as a skin file's <c>palette</c> and <c>tokens</c> objects, holding only the keys that differ from the base skin.</summary>
    /// <returns>Indented JSON; <c>{}</c> when nothing differs.</returns>
    public string ExportJson()
    {
        SortedDictionary<string, string> changedPalette = new(StringComparer.Ordinal);
        foreach (string key in this.palette.Keys.Where(this.IsPaletteChanged))
        {
            changedPalette[key] = this.palette[key];
        }

        SortedDictionary<string, string> changedTokens = new(StringComparer.Ordinal);
        foreach (string key in this.tokens.Keys.Where(this.IsTokenChanged))
        {
            changedTokens[key] = this.tokens[key];
        }

        Dictionary<string, object> export = new(StringComparer.Ordinal);
        if (changedPalette.Count > 0)
        {
            export["palette"] = changedPalette;
        }

        if (changedTokens.Count > 0)
        {
            export["tokens"] = changedTokens;
        }

        return JsonSerializer.Serialize(export, ExportOptions);
    }

    private static double? Measure(SkinColourResolver resolver, SkinContrastPair pair)
    {
        try
        {
            return ContrastColour.Ratio(resolver.Evaluate(pair.Foreground), resolver.Evaluate(pair.Background));
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string FormatColour(MudColor colour) =>
        colour.A == 255 ? colour.ToString(MudColorOutputFormats.Hex) : colour.ToString(MudColorOutputFormats.RGBA);

    private static bool SameColour(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
        || string.Equals(FormatColour(new MudColor(first)), FormatColour(new MudColor(second)), StringComparison.Ordinal);

    private static SkinFacesCss FacesOf(SkinFaces faces) => new(FontStack(faces.Read), FontStack(faces.Mono));

    private static string? FontStack(IReadOnlyList<string> families) =>
        families.Count == 0
            ? null
            : string.Join(", ", families.Select(static family => family.Contains(' ', StringComparison.Ordinal) ? $"\"{family}\"" : family));

    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|rgba?\([0-9 ,.%]+\))$", RegexOptions.CultureInvariant)]
    private static partial Regex ColourPattern();
}
