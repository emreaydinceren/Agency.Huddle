using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.App.Appearance;

/// <summary>
/// Validates a Human-supplied, token-keyed override map from <c>appearance.json</c> and renders it
/// into the inline CSS body <c>App.razor</c> emits as a <see cref="Microsoft.AspNetCore.Components.MarkupString"/>
/// after the selected theme's stylesheet. This class is pure — no file access, no logging, no
/// state — so every rejection rule lives in one place, testable without a store or a host.
/// </summary>
/// <remarks>
/// <see cref="Build"/> is the only thing standing between a typo in a hand-edited file and a
/// broken or blanked page: <c>App.razor</c> must render the finished CSS body as a
/// <see cref="Microsoft.AspNetCore.Components.MarkupString"/> rather than plain text, because Razor
/// HTML-encodes <c>@</c> expressions and CSS does not decode HTML entities — a value such as
/// <c>"Segoe UI"</c> would otherwise arrive as <c>&amp;quot;Segoe UI&amp;quot;</c> and be dropped by
/// every browser. That means nothing downstream of this method escapes what it returns, so the
/// allowlist below is the sole defence, not one layer among several.
/// </remarks>
internal static partial class ThemeOverrides
{
    /// <summary>
    /// The complete set of characters D18 allows in a value, once trimmed: letters, digits, space,
    /// and <c>,.-_#%()/'"</c>. It deliberately excludes <c>;</c>, <c>{</c>, <c>}</c>, <c>&lt;</c>,
    /// <c>&gt;</c>, <c>&amp;</c>, <c>@</c>, <c>:</c> and every
    /// backslash and control character — none of those can appear in a value that reaches the page
    /// as a <see cref="Microsoft.AspNetCore.Components.MarkupString"/>, because any one of them could
    /// terminate the CSS declaration early, close the surrounding <c>&lt;style&gt;</c> element, or
    /// open a comment. Quotes are kept in, even though they delimit a CSS string themselves, because
    /// <c>"Segoe UI", sans-serif</c> is the single most likely thing anyone types into a font
    /// override, and a quote alone cannot do anything unsafe once <c>&lt;</c>, <c>&gt;</c> and
    /// <c>&amp;</c> are gone — <c>ThemeOverridesTests.Build_WithAQuotedFontName_IsAccepted</c> exists
    /// specifically to stop a future edit from tightening this list into uselessness.
    /// </summary>
    [GeneratedRegex(@"\A[A-Za-z0-9 ,.\-_#%()/'""]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedValue();

    /// <summary>
    /// Validates <paramref name="overrides"/> against <see cref="ThemeTokens.All"/> and D18's
    /// allowlist, returning the finished inline CSS body for whatever survives.
    /// </summary>
    /// <param name="overrides">The raw, unvalidated token-to-value map read from <c>appearance.json</c>.</param>
    /// <param name="problems">
    /// One human-readable line per rejected entry, in no particular order, for the Appearance tab to
    /// display. Empty when every entry is accepted (or <paramref name="overrides"/> is empty).
    /// </param>
    /// <returns>
    /// The complete inline CSS body — <c>":root {\n    --x: y;\n}"</c>, one declaration per accepted
    /// token ordered by token name so the output is stable and diffable — or
    /// <see cref="string.Empty"/> when nothing valid survives. A rejected entry contributes nothing
    /// here; it is not this method's job to touch the file it came from, so the caller
    /// (<see cref="AppearanceStore"/>) leaves a rejected entry exactly as it found it.
    /// </returns>
    internal static string Build(IReadOnlyDictionary<string, string> overrides, out IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        List<string> problemList = [];
        SortedDictionary<string, string> accepted = new(StringComparer.Ordinal);

        foreach (var (token, rawValue) in overrides)
        {
            if (!ThemeTokens.All.Contains(token))
            {
                problemList.Add($"'{token}' is not a theme token; it was left in the file and ignored.");
                continue;
            }

            var value = rawValue.Trim();

            if (value.Length is 0 or > 200)
            {
                problemList.Add($"'{token}': value must be 1-200 characters once trimmed; it was left in the file and ignored.");
                continue;
            }

            if (!AllowedValue().IsMatch(value))
            {
                problemList.Add($"'{token}': value '{value}' contains a character that is not allowed; it was left in the file and ignored.");
                continue;
            }

            if (value.Contains("/*", StringComparison.Ordinal) ||
                value.Contains("*/", StringComparison.Ordinal) ||
                value.Contains("url(", StringComparison.OrdinalIgnoreCase))
            {
                problemList.Add($"'{token}': value '{value}' contains a disallowed sequence; it was left in the file and ignored.");
                continue;
            }

            accepted[token] = value;
        }

        problems = problemList;

        if (accepted.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder body = new();
        body.Append(":root {\n");
        foreach (var (token, value) in accepted)
        {
            body.Append("    ").Append(token).Append(": ").Append(value).Append(";\n");
        }

        body.Append('}');

        return body.ToString();
    }
}
