using System.Globalization;
using System.Reflection;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Guards the legibility of every shipped skin: the text pairs that must reach 4.5:1 and the keyboard focus ring that must
/// reach 3:1 against the two grounds it sits on. The token expressions are resolved to colours by <see cref="Resolver"/>.
/// </summary>
public sealed class SkinContrastTests
{
    private const double TextMinimum = 4.5;
    private const double FocusMinimum = 3.0;

    private static readonly string[] GroundTokens =
        ["ground.page", "ground.sidebar", "ground.rail", "ground.raised", "ground.sunk"];

    /// <summary>
    /// TextPrimary and TextSecondary read on every ground token, <c>current.foreground</c> on <c>current.background</c>,
    /// <c>accent.ink</c> on <c>selected.background</c> and PrimaryContrastText on Primary, all at 4.5:1 or better.
    /// </summary>
    [Fact]
    public void Skins_TextContrast()
    {
        List<string> failures = [];
        foreach (Skin skin in SkinLoader.LoadAll())
        {
            Resolver resolver = new(skin);
            MudColor textPrimary = resolver.Palette("textPrimary");
            MudColor textSecondary = resolver.Palette("textSecondary");
            foreach (string ground in GroundTokens)
            {
                MudColor background = resolver.Token(ground);
                Check(failures, skin, "textPrimary on " + ground, textPrimary, background, TextMinimum);
                Check(failures, skin, "textSecondary on " + ground, textSecondary, background, TextMinimum);
            }

            Check(failures, skin, "current.foreground on current.background", resolver.Token("current.foreground"), resolver.Token("current.background"), TextMinimum);
            Check(failures, skin, "accent.ink on selected.background", resolver.Token("accent.ink"), resolver.Token("selected.background"), TextMinimum);
            Check(failures, skin, "primaryContrastText on primary", resolver.Palette("primaryContrastText"), resolver.Palette("primary"), TextMinimum);
        }

        Assert.Empty(failures);
    }

    /// <summary>The <c>focus</c> ring reads at 3:1 or better on <c>ground.page</c> and on <c>ground.raised</c>.</summary>
    [Fact]
    public void Skins_FocusVisible()
    {
        List<string> failures = [];
        foreach (Skin skin in SkinLoader.LoadAll())
        {
            Resolver resolver = new(skin);
            MudColor focus = resolver.Token("focus");
            Check(failures, skin, "focus on ground.page", focus, resolver.Token("ground.page"), FocusMinimum);
            Check(failures, skin, "focus on ground.raised", focus, resolver.Token("ground.raised"), FocusMinimum);
        }

        Assert.Empty(failures);
    }

    /// <summary>The resolver itself: a nested mix, a ref chain and a palette override resolve to the expected colour.</summary>
    [Fact]
    public void Resolver_MixesInSrgbAndFollowsRefs()
    {
        Skin skin = SkinLoader.LoadAll().First(static candidate => string.Equals(candidate.Id, "huddle", StringComparison.Ordinal));
        Resolver resolver = new(skin);

        MudColor mixed = resolver.Evaluate("mix(#000000 25%, #ffffff)");
        Assert.Equal(191, mixed.R);
        Assert.Equal(191, mixed.G);
        Assert.Equal(191, mixed.B);

        Assert.Equal(resolver.Token("accent.ink").ToString(MudColorOutputFormats.Hex), resolver.Token("current.foreground").ToString(MudColorOutputFormats.Hex));
        Assert.Equal("#ffffff", resolver.Palette("surface").ToString(MudColorOutputFormats.Hex));
    }

    private static void Check(List<string> failures, Skin skin, string pair, MudColor foreground, MudColor background, double minimum)
    {
        double ratio = ContrastColour.Ratio(foreground, background);
        if (ratio < minimum)
        {
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"{skin.Id}: {pair} is {ratio:0.00}:1, needs {minimum:0.0}:1"));
        }
    }

    /// <summary>Resolves a skin's palette and token expressions (palette, ref, mix in sRGB, literals) to opaque colours.</summary>
    private sealed class Resolver
    {
        private const int MaxDepth = 32;

        private readonly Skin skin;
        private readonly Palette palette;
        private readonly Dictionary<string, string> tokens = new(StringComparer.Ordinal);

        public Resolver(Skin skin)
        {
            this.skin = skin;
            this.palette = skin.Mode == ThemeMode.Dark ? skin.Theme.PaletteDark : skin.Theme.PaletteLight;
            foreach (Token token in SkinTokens.All)
            {
                string fallback = skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
                this.tokens[token.Key] = skin.Tokens.TryGetValue(token.Key, out string? own) ? own : fallback;
            }
        }

        public MudColor Palette(string camelName)
        {
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

        public MudColor Token(string key) => this.Evaluate("ref(" + key + ")");

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
}
