using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Css;

/// <summary>How <see cref="CssText"/> treats line endings when it reads a stylesheet.</summary>
internal enum CssNewlines
{
    /// <summary>The text exactly as the file holds it (CRLF or LF, depending on the checkout).</summary>
    AsIs,

    /// <summary>Every <c>\r\n</c> replaced by <c>\n</c>, so a test does not depend on how the checkout stored the file.</summary>
    Lf,
}

/// <summary>Which definition <see cref="CssText.RuleBody(string, string, RuleMatch)"/> returns when a selector is defined more than once.</summary>
internal enum RuleMatch
{
    /// <summary>The first definition.</summary>
    First,

    /// <summary>The last definition: the one that wins the cascade when a later rule overrides an earlier one.</summary>
    Last,

    /// <summary>Every definition, bodies joined by a newline.</summary>
    All,
}

/// <summary>Reading and slicing stylesheet source text for tests that assert on the application's stylesheets in <c>wwwroot/css</c>.</summary>
internal static partial class CssText
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private static readonly Lazy<string> AppCssLf = new(static () => ReadAppCss(CssNewlines.Lf));

    private static readonly Lazy<string> AppCssAsIs = new(static () => ReadAppCss(CssNewlines.AsIs));

    /// <summary>
    /// The full paths of the application's own stylesheets, in the order <see cref="AppStylesheets.All"/> links them
    /// (<c>src/Huddle.App/wwwroot/css/*.css</c>).
    /// </summary>
    /// <returns>The paths, in link order.</returns>
    internal static IReadOnlyList<string> AppCssPaths() =>
        [.. AppStylesheets.All.Select(static relative => RepoPaths.App(["wwwroot", .. relative.Split('/')]))];

    /// <summary>
    /// The text of the application's own stylesheets (<c>src/Huddle.App/wwwroot/css/*.css</c>) joined in
    /// <see cref="AppStylesheets.All"/> order, which is the order the page links them and so the order of the cascade.
    /// Read once per newline mode.
    /// </summary>
    /// <param name="newlines">Whether to normalise line endings to LF (the default) or keep the files' own.</param>
    /// <returns>The stylesheet source.</returns>
    internal static string AppCss(CssNewlines newlines = CssNewlines.Lf) =>
        newlines == CssNewlines.Lf ? AppCssLf.Value : AppCssAsIs.Value;

    /// <summary>Reads a stylesheet file, optionally normalising its line endings.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="newlines">Whether to normalise line endings to LF (the default) or keep the file's own.</param>
    /// <returns>The file text.</returns>
    internal static string Read(string path, CssNewlines newlines = CssNewlines.Lf)
    {
        string text = File.ReadAllText(path);
        return newlines == CssNewlines.Lf ? text.Replace("\r\n", "\n", StringComparison.Ordinal) : text;
    }

    /// <summary>
    /// The declaration block of the rule whose selector is exactly <paramref name="selector"/>, starting at the beginning of a line and
    /// followed by optional whitespace and <c>{</c>. A longer name that merely ends in the selector (<c>.mud-chip</c> for <c>.chip</c>) or a
    /// descendant selector (<c>.foo .chip</c>) does not match. Fails the test, naming the selector, when there is no such rule.
    /// </summary>
    /// <param name="css">The stylesheet source.</param>
    /// <param name="selector">The selector as written, such as <c>.chip</c>.</param>
    /// <param name="match">Which definition to return when the selector is defined more than once.</param>
    /// <returns>The text between the braces.</returns>
    internal static string RuleBody(string css, string selector, RuleMatch match = RuleMatch.First)
    {
        Regex pattern = new(@"(?:^|\n)" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, RegexTimeout);
        List<string> bodies = [.. pattern.Matches(css).Select(static found => found.Groups["body"].Value)];
        if (bodies.Count == 0)
        {
            Assert.Fail($"The stylesheet has no rule with the selector '{selector}'.");
        }

        return match switch
        {
            RuleMatch.Last => bodies[^1],
            RuleMatch.All => string.Join('\n', bodies),
            _ => bodies[0],
        };
    }

    /// <summary>
    /// The body of the first rule whose selector is <paramref name="selector"/> with any whitespace between its parts, found anywhere in
    /// a line but never as the tail of a longer name. Fails the test, naming the selector, when there is no such rule.
    /// </summary>
    /// <param name="css">The stylesheet source.</param>
    /// <param name="selector">The selector; runs of whitespace match any whitespace.</param>
    /// <param name="collapseWhitespace">Trim the body and collapse its whitespace runs to single spaces.</param>
    /// <returns>The text between the braces.</returns>
    internal static string RuleBodyLoose(string css, string selector, bool collapseWhitespace = true)
    {
        string[] tokens = selector.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Regex pattern = new(
            @"(?<![\w.-])" + string.Join(@"\s+", tokens.Select(static token => Regex.Escape(token))) + @"\s*\{(?<body>[^}]*)\}",
            RegexOptions.CultureInvariant,
            RegexTimeout);
        Match found = pattern.Match(css);
        if (!found.Success)
        {
            Assert.Fail($"The stylesheet has no rule with the selector '{selector}'.");
        }

        string body = found.Groups["body"].Value;
        return collapseWhitespace ? CollapseWhitespace(body) : body;
    }

    /// <summary>
    /// The body of the first rule whose comma-separated selector list contains <paramref name="selector"/> as one of its entries
    /// (<c>b</c> in <c>a, b {</c>). Fails the test, naming the selector, when there is no such rule.
    /// </summary>
    /// <param name="css">The stylesheet source.</param>
    /// <param name="selector">One selector from a list.</param>
    /// <returns>The text between the braces.</returns>
    internal static string RuleBodyInList(string css, string selector)
    {
        Regex pattern = new(
            @"(?:^|\n)(?:[^{}\n]*,\s*)?" + Regex.Escape(selector) + @"\s*(?:,[^{}]*)?\{(?<body>[^}]*)\}",
            RegexOptions.CultureInvariant,
            RegexTimeout);
        Match found = pattern.Match(css);
        if (!found.Success)
        {
            Assert.Fail($"The stylesheet has no rule listing the selector '{selector}'.");
        }

        return found.Groups["body"].Value;
    }

    /// <summary>The stylesheet with every terminated <c>/* ... */</c> comment removed; an unterminated <c>/*</c> is left in place.</summary>
    /// <param name="css">The stylesheet source.</param>
    /// <returns>The text without comments.</returns>
    internal static string StripComments(string css) => CommentPattern().Replace(css, string.Empty);

    /// <summary>
    /// The text from the section header <c>/* ---- {header}</c> up to the header <c>/* ---- {nextHeader}</c>, or to the end when
    /// <paramref name="nextHeader"/> is <see langword="null"/>. Fails the test, naming the header, when a header is missing or out of order.
    /// </summary>
    /// <param name="css">The stylesheet source.</param>
    /// <param name="header">The start of the section's header text after <c>/* ---- </c>.</param>
    /// <param name="nextHeader">The header that ends the section, or <see langword="null"/> for the end of the text.</param>
    /// <returns>The section text, comments included.</returns>
    internal static string Section(string css, string header, string? nextHeader = null)
    {
        int start = css.IndexOf($"/* ---- {header}", StringComparison.Ordinal);
        if (start < 0)
        {
            Assert.Fail($"Section '{header}' is missing.");
        }

        if (nextHeader is null)
        {
            return css[start..];
        }

        int end = css.IndexOf($"/* ---- {nextHeader}", start + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            Assert.Fail($"Section '{nextHeader}' is missing after '{header}'.");
        }

        return css[start..end];
    }

    /// <summary>The text trimmed, with every run of whitespace (newlines included) replaced by one space.</summary>
    /// <param name="text">The text to collapse.</param>
    /// <returns>The collapsed text.</returns>
    internal static string CollapseWhitespace(string text) =>
        Regex.Replace(text.Trim(), @"\s+", " ", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>How many times <paramref name="needle"/> occurs in <paramref name="text"/>, counting non-overlapping matches, ordinally.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="needle">The text to count; must not be empty.</param>
    /// <returns>The number of occurrences.</returns>
    internal static int CountOccurrences(string text, string needle)
    {
        ArgumentException.ThrowIfNullOrEmpty(needle);
        int count = 0;
        int index = text.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Reads every application stylesheet in link order and joins the text with nothing between files.</summary>
    /// <param name="newlines">Whether to normalise line endings to LF or keep the files' own.</param>
    /// <returns>The joined source.</returns>
    private static string ReadAppCss(CssNewlines newlines) =>
        string.Concat(AppCssPaths().Select(path => Read(path, newlines)));

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommentPattern();
}
