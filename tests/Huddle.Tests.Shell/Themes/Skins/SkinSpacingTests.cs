using System.Globalization;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// D6: a length a skin writes for spacing (a token whose last key segment is <c>padding</c>, <c>margin</c> or <c>gap</c>) is on the
/// 4px scale, so a Theme cannot slip a 14px or 10px padding past the stylesheet rule that holds every other spacing to the scale.
/// </summary>
public sealed partial class SkinSpacingTests
{
    private static readonly HashSet<double> Scale = [0, 4, 8, 12, 16, 24, 32];

    private static readonly string[] SpacingSegments = ["padding", "margin", "gap"];

    /// <summary>Every spacing length in every built-in skin, in both modes, is on the 4px scale.</summary>
    [Fact]
    public void EverySkin_WritesSpacingOnTheScale()
    {
        IReadOnlyList<Skin> skins = SkinLoader.LoadAll();

        Assert.NotEmpty(skins);
        foreach (Skin skin in skins)
        {
            Assert.Empty(OffScale(skin.Tokens));
        }
    }

    /// <summary>Every spacing default in the token registry, in both modes, is on the 4px scale.</summary>
    [Fact]
    public void Registry_DefaultsAreOnTheScale()
    {
        Dictionary<string, string> light = SkinTokens.All.ToDictionary(static token => token.Key, static token => token.LightDefault, StringComparer.Ordinal);
        Dictionary<string, string> dark = SkinTokens.All.ToDictionary(static token => token.Key, static token => token.DarkDefault, StringComparer.Ordinal);

        Assert.Empty(OffScale(light));
        Assert.Empty(OffScale(dark));
    }

    /// <summary>The check can fail: the off-scale paddings the skins used to write (14px, 10px) are each reported, and a length on the scale is not.</summary>
    [Fact]
    public void OffScale_ReportsALengthThatIsNotOnTheScale()
    {
        Dictionary<string, string> tokens = new(StringComparer.Ordinal)
        {
            ["gate.padding"] = "12px 14px",
            ["form.group.padding"] = "10px 14px 14px",
            ["list.row.gap"] = "8px 16px",
            ["gate.radius"] = "9px",
        };

        IReadOnlyList<string> found = OffScale(tokens);

        Assert.Equal(["form.group.padding: 10px", "form.group.padding: 14px", "form.group.padding: 14px", "gate.padding: 14px"], found);
    }

    /// <summary>The off-scale lengths in the spacing tokens of <paramref name="tokens"/>, each as <c>key: length</c>; a radius, blur or shadow is not spacing.</summary>
    /// <param name="tokens">Token key to skin expression.</param>
    private static List<string> OffScale(IReadOnlyDictionary<string, string> tokens)
    {
        List<string> found = [];
        foreach (KeyValuePair<string, string> pair in tokens.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            string last = pair.Key[(pair.Key.LastIndexOf('.') + 1)..];
            if (!SpacingSegments.Contains(last, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (Match length in Length().Matches(pair.Value))
            {
                double size = double.Parse(length.Groups["n"].Value, CultureInfo.InvariantCulture);
                if (!Scale.Contains(size) || !string.Equals(length.Groups["unit"].Value, "px", StringComparison.Ordinal))
                {
                    found.Add(pair.Key + ": " + length.Value);
                }
            }
        }

        return found;
    }

    [GeneratedRegex(@"(?<![\w.#-])(?<n>\d*\.?\d+)(?<unit>px|rem|em)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Length();
}
