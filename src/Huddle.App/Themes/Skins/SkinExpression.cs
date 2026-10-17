using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The skin expression grammar. A value is a whitespace or comma separated sequence of terms (colour literals, lengths,
/// numbers, a few keywords, <c>palette.x</c>, <c>mud(x)</c>, <c>ref(key)</c>, <c>mix(A N%, B)</c>, <c>alpha(A N%)</c> and
/// <c>calc(...)</c>); <see cref="ToCss"/> rewrites each term to CSS and refuses everything else, so a skin file can never
/// put a stray declaration, a tag or a hand-written <c>var()</c> into the stylesheet. Every refusal is a
/// <see cref="SkinException"/> naming the token, the value and the skin.
/// </summary>
internal static partial class SkinExpression
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "none", "transparent", "currentColor", "auto", "normal", "inherit", "initial", "inset", "solid", "dashed", "antialiased",
    };

    /// <summary>Rewrites <paramref name="value"/> to CSS, checking every <c>ref()</c> against the token registry.</summary>
    /// <param name="value">A skin expression, such as <c>mix(palette.primary 14%, palette.surface)</c>.</param>
    /// <param name="tokenKey">The key of the token the value belongs to, for the error message.</param>
    /// <param name="skinId">The id of the skin (or <c>defaults</c>), for the error message.</param>
    /// <returns>The CSS value.</returns>
    /// <exception cref="SkinException">The value uses a term the grammar does not allow.</exception>
    public static string ToCss(string value, string tokenKey, string skinId)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(tokenKey);
        ArgumentNullException.ThrowIfNull(skinId);
        return Rewrite(value, tokenKey, skinId, SkinTokens.Contains, null);
    }

    /// <summary>
    /// Checks a whole token set: every value parses, every <c>ref()</c> names a key in <paramref name="tokens"/>, and no
    /// chain of references loops back on itself (CSS would silently make every token in a loop invalid).
    /// </summary>
    /// <param name="tokens">Token key to skin expression: the registry defaults with a skin's overrides applied.</param>
    /// <param name="skinId">The id of the skin (or <c>defaults</c>), for the error message.</param>
    /// <exception cref="SkinException">A value is invalid, or the references form a cycle such as <c>a → b → a</c>.</exception>
    public static void Validate(IReadOnlyDictionary<string, string> tokens, string skinId)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(skinId);

        Dictionary<string, List<string>> edges = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in tokens)
        {
            List<string> refs = [];
            _ = Rewrite(pair.Value, pair.Key, skinId, tokens.ContainsKey, refs);
            edges[pair.Key] = refs;
        }

        HashSet<string> done = new(StringComparer.Ordinal);
        foreach (string key in edges.Keys)
        {
            Visit(key, edges, done, [], skinId);
        }
    }

    private static void Visit(string key, Dictionary<string, List<string>> edges, HashSet<string> done, List<string> path, string skinId)
    {
        int at = path.IndexOf(key);
        if (at >= 0)
        {
            string cycle = string.Join(" → ", path.Skip(at).Append(key));
            throw new SkinException($"token reference cycle {cycle} in skin '{skinId}'");
        }

        if (!done.Add(key))
        {
            return;
        }

        path.Add(key);
        foreach (string next in edges[key])
        {
            Visit(next, edges, done, path, skinId);
        }

        path.RemoveAt(path.Count - 1);
    }

    private static string Rewrite(string value, string tokenKey, string skinId, Func<string, bool> isKnown, List<string>? refs)
    {
        Parser parser = new(value, tokenKey, skinId, isKnown, refs);
        return parser.Run();
    }

    /// <summary>Maps <c>palette.camelName</c> to the MudBlazor variable name, or <see langword="null"/> when it names none.</summary>
    private static string? PaletteVariable(string camel)
    {
        if (!PaletteNamePattern().IsMatch(camel))
        {
            return null;
        }

        const string contrast = "ContrastText";
        string name = char.ToUpperInvariant(camel[0]) + camel[1..];
        if (name.EndsWith(contrast, StringComparison.Ordinal))
        {
            name = name[..^contrast.Length] + "Text";
        }

        StringBuilder kebab = new("--mud-palette-");
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                _ = kebab.Append('-');
            }

            _ = kebab.Append(char.ToLowerInvariant(name[i]));
        }

        string variable = kebab.ToString();
        return MudVariables.All.Contains(variable) ? variable : null;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PaletteNamePattern();

    [GeneratedRegex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"^[+-]?(?:\d+\.?\d*|\.\d+)(?:px|em|rem|ch|s|%)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(?:\d+\.?\d*|\.\d+)%$", RegexOptions.CultureInvariant)]
    private static partial Regex PercentPattern();

    [GeneratedRegex(@"^[0-9.%,/\s+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex RgbBodyPattern();

    [GeneratedRegex(@"^[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex MudNamePattern();

    /// <summary>One parse of one value: a cursor over the text and the CSS written so far.</summary>
    private sealed class Parser
    {
        private readonly string text;
        private readonly string tokenKey;
        private readonly string skinId;
        private readonly Func<string, bool> isKnown;
        private readonly List<string>? refs;
        private readonly StringBuilder output = new();
        private int position;

        public Parser(string text, string tokenKey, string skinId, Func<string, bool> isKnown, List<string>? refs)
        {
            this.text = text;
            this.tokenKey = tokenKey;
            this.skinId = skinId;
            this.isKnown = isKnown;
            this.refs = refs;
        }

        private bool AtEnd => this.position >= this.text.Length;

        public string Run()
        {
            this.Sequence(calc: false);
            if (!this.AtEnd)
            {
                throw this.Forbidden(this.text[this.position].ToString());
            }

            return this.output.ToString();
        }

        private SkinException Error(string detail) =>
            new($"{detail} in token '{this.tokenKey}' of skin '{this.skinId}' (value '{this.text}')");

        private SkinException Forbidden(string term) => this.Error($"forbidden term '{term}'");

        private void SkipWhitespace()
        {
            while (!this.AtEnd && char.IsWhiteSpace(this.text[this.position]))
            {
                this.position++;
            }
        }

        /// <summary>Reads characters up to whitespace, a comma or a parenthesis.</summary>
        private string ReadWord()
        {
            int start = this.position;
            while (!this.AtEnd)
            {
                char c = this.text[this.position];
                if (char.IsWhiteSpace(c) || c is ',' or '(' or ')')
                {
                    break;
                }

                this.position++;
            }

            return this.text[start..this.position];
        }

        private void Sequence(bool calc)
        {
            bool first = true;
            string pending = " ";
            while (true)
            {
                this.SkipWhitespace();
                if (this.AtEnd)
                {
                    if (calc)
                    {
                        throw this.Error("missing ')'");
                    }

                    if (first)
                    {
                        throw this.Error("empty value");
                    }

                    return;
                }

                char c = this.text[this.position];
                if (c == ')')
                {
                    if (calc && !first)
                    {
                        return;
                    }

                    throw this.Forbidden(")");
                }

                if (!first)
                {
                    _ = this.output.Append(pending);
                }

                if (calc && this.IsCalcOperator())
                {
                    _ = this.output.Append(c);
                    this.position++;
                }
                else
                {
                    this.Term();
                }

                first = false;
                this.SkipWhitespace();
                pending = " ";
                if (!calc && !this.AtEnd && this.text[this.position] == ',')
                {
                    this.position++;
                    this.SkipWhitespace();
                    if (this.AtEnd || this.text[this.position] == ')')
                    {
                        throw this.Forbidden(",");
                    }

                    pending = ", ";
                }
            }
        }

        private bool IsCalcOperator()
        {
            char c = this.text[this.position];
            if (c is '+' or '*' or '/')
            {
                return true;
            }

            return c == '-' && this.position + 1 < this.text.Length && char.IsWhiteSpace(this.text[this.position + 1]);
        }

        private void Term()
        {
            string word = this.ReadWord();
            if (!this.AtEnd && this.text[this.position] == '(')
            {
                this.position++;
                this.Function(word);
                return;
            }

            if (word.Length == 0)
            {
                throw this.Forbidden(this.AtEnd ? string.Empty : this.text[this.position].ToString());
            }

            if (word.StartsWith("palette.", StringComparison.Ordinal))
            {
                string camel = word["palette.".Length..];
                string variable = PaletteVariable(camel)
                    ?? throw this.Error($"unknown palette colour '{camel}'");
                _ = this.output.Append("var(").Append(variable).Append(')');
                return;
            }

            bool literal = word[0] == '#'
                ? HexPattern().IsMatch(word)
                : NumberPattern().IsMatch(word) || Keywords.Contains(word);
            if (!literal)
            {
                throw this.Forbidden(word);
            }

            _ = this.output.Append(word);
        }

        private void Function(string name)
        {
            switch (name)
            {
                case "rgb":
                case "rgba":
                    this.RawFunction(name);
                    break;
                case "mud":
                    this.Mud();
                    break;
                case "ref":
                    this.Ref();
                    break;
                case "mix":
                    this.Mix();
                    break;
                case "alpha":
                    this.Alpha();
                    break;
                case "calc":
                    _ = this.output.Append("calc(");
                    this.Sequence(calc: true);
                    this.Expect(')');
                    _ = this.output.Append(')');
                    break;
                default:
                    throw this.Forbidden(name + "(");
            }
        }

        private string ReadArgument(string function)
        {
            int close = this.text.IndexOf(')', this.position);
            if (close < 0)
            {
                throw this.Error($"missing ')' after {function}(");
            }

            string argument = this.text[this.position..close].Trim();
            this.position = close + 1;
            return argument;
        }

        private void RawFunction(string name)
        {
            string body = this.ReadArgument(name);
            if (!RgbBodyPattern().IsMatch(body) || body.Length == 0)
            {
                throw this.Forbidden($"{name}({body})");
            }

            _ = this.output.Append(name).Append('(').Append(body).Append(')');
        }

        private void Mud()
        {
            string name = this.ReadArgument("mud");
            if (!MudNamePattern().IsMatch(name) || !MudVariables.All.Contains("--mud-" + name))
            {
                throw this.Error($"'{name}' is not a MudBlazor variable");
            }

            _ = this.output.Append("var(--mud-").Append(name).Append(')');
        }

        private void Ref()
        {
            string key = this.ReadArgument("ref");
            if (!this.isKnown(key))
            {
                throw this.Error($"unknown token '{key}'");
            }

            this.refs?.Add(key);
            _ = this.output.Append("var(--hu-").Append(key.Replace('.', '-')).Append(')');
        }

        private void Mix()
        {
            _ = this.output.Append("color-mix(in srgb, ");
            this.Term();
            this.Percent("mix");
            this.SkipWhitespace();
            this.Expect(',');
            _ = this.output.Append(", ");
            this.SkipWhitespace();
            this.Term();
            this.SkipWhitespace();
            this.Expect(')');
            _ = this.output.Append(')');
        }

        private void Alpha()
        {
            _ = this.output.Append("color-mix(in srgb, ");
            this.Term();
            this.Percent("alpha");
            this.SkipWhitespace();
            this.Expect(')');
            _ = this.output.Append(", transparent)");
        }

        private void Percent(string function)
        {
            this.SkipWhitespace();
            string word = this.ReadWord();
            if (!PercentPattern().IsMatch(word)
                || !double.TryParse(word.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)
                || percent is <= 0 or >= 100)
            {
                throw this.Error($"{function}() needs a percentage greater than 0 and less than 100 after the colour, found '{word}'");
            }

            _ = this.output.Append(' ').Append(word);
        }

        private void Expect(char expected)
        {
            this.SkipWhitespace();
            if (this.AtEnd || this.text[this.position] != expected)
            {
                throw this.Error($"expected '{expected}'");
            }

            this.position++;
        }
    }
}
