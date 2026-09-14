using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Shared parsing helpers over stylesheet <em>source text</em>, used by the CSS-tokenisation
/// suites in this project. It exists so that concurrent test files can each parse a stylesheet
/// without editing one another's code, and so every test agrees on what counts as a declared
/// token, a token reference, a colour literal, or a font-family literal.
/// </summary>
internal static partial class CssSource
{
    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> until it finds the directory that
    /// holds <c>Huddle.slnx</c>, then combines that directory with <paramref name="segments"/>.
    /// </summary>
    /// <param name="segments">Path segments relative to the repository root.</param>
    public static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        var path = directory.FullName;
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    /// <summary>
    /// Every custom property declared in <paramref name="cssPath"/> (a line beginning
    /// <c>--name:</c>, ignoring leading whitespace), in file order, with duplicates preserved so a
    /// caller can assert on uniqueness itself.
    /// </summary>
    /// <param name="cssPath">Path to the stylesheet to read.</param>
    public static IReadOnlyList<string> ReadDeclaredTokens(string cssPath)
    {
        List<string> tokens = [];
        foreach (var line in File.ReadLines(cssPath))
        {
            var match = DeclaredTokenPattern().Match(line.TrimStart());
            if (match.Success)
            {
                tokens.Add(match.Groups["name"].Value);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Every custom property referenced through <c>var(...)</c> in <paramref name="cssPath"/>, in
    /// file order, with duplicates preserved.
    /// </summary>
    /// <param name="cssPath">Path to the stylesheet to read.</param>
    public static IReadOnlyList<string> ReadReferencedTokens(string cssPath)
    {
        var text = File.ReadAllText(cssPath);
        return VarReferencePattern()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .ToList();
    }

    /// <summary>
    /// Every declaration <em>value</em> in <paramref name="cssPath"/> that contains a colour
    /// literal, formatted as <c>"line N: text"</c>. Only the text after a line's first <c>:</c> up
    /// to its terminating <c>;</c> is scanned, so an id selector such as
    /// <c>#components-reconnect-modal</c> is never mistaken for a hex colour, and a trailing
    /// comment (which always follows the <c>;</c> in this codebase) is never scanned either. A
    /// colour is a hex literal, an <c>rgb()</c>/<c>rgba()</c>/<c>hsl()</c>/<c>hsla()</c> call, or a
    /// named colour keyword; text inside <c>var(...)</c> is stripped first so a token name never
    /// counts as a literal.
    /// </summary>
    /// <param name="cssPath">Path to the stylesheet to read.</param>
    public static IReadOnlyList<string> FindColourLiterals(string cssPath)
    {
        List<string> hits = [];
        var lines = File.ReadAllLines(cssPath);
        for (var i = 0; i < lines.Length; i++)
        {
            var declarationValue = ExtractDeclarationValue(lines[i]);
            if (declarationValue is null)
            {
                continue;
            }

            var withoutVarCalls = VarCallPattern().Replace(declarationValue, string.Empty);
            if (ColourLiteralPattern().IsMatch(withoutVarCalls))
            {
                hits.Add($"line {i + 1}: {lines[i].Trim()}");
            }
        }

        return hits;
    }

    /// <summary>
    /// Every <c>font-family:</c> or <c>font:</c> declaration in <paramref name="cssPath"/> whose
    /// value is not a single <c>var(--name)</c> reference, formatted as <c>"line N: text"</c>. The
    /// typography equivalent of <see cref="FindColourLiterals"/>.
    /// </summary>
    /// <param name="cssPath">Path to the stylesheet to read.</param>
    public static IReadOnlyList<string> FindFontFamilyLiterals(string cssPath)
    {
        List<string> hits = [];
        var lines = File.ReadAllLines(cssPath);
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FontDeclarationPattern().Match(lines[i].Trim());
            if (!match.Success)
            {
                continue;
            }

            var value = match.Groups["value"].Value.Trim();
            if (!SingleVarReferencePattern().IsMatch(value))
            {
                hits.Add($"line {i + 1}: {lines[i].Trim()}");
            }
        }

        return hits;
    }

    /// <summary>
    /// Returns the text after a declaration line's first <c>:</c> up to (not including) its first
    /// <c>;</c>, or <see langword="null"/> if the line is not a single-line declaration (it has no
    /// <c>:</c>, no <c>;</c>, or the <c>;</c> precedes the <c>:</c>).
    /// </summary>
    /// <param name="line">One line of stylesheet source.</param>
    private static string? ExtractDeclarationValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        var semicolonIndex = line.IndexOf(';');
        if (colonIndex < 0 || semicolonIndex < 0 || semicolonIndex < colonIndex)
        {
            return null;
        }

        return line[(colonIndex + 1)..semicolonIndex];
    }

    [GeneratedRegex(@"^(?<name>--[a-zA-Z0-9-]+)\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex DeclaredTokenPattern();

    [GeneratedRegex(@"var\(\s*(?<name>--[a-zA-Z0-9-]+)\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex VarReferencePattern();

    [GeneratedRegex(@"var\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex VarCallPattern();

    [GeneratedRegex(
        @"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(|\b(?:white|black|red|green|blue|yellow|orange|purple|grey|gray|lightyellow)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ColourLiteralPattern();

    [GeneratedRegex(@"^(?<prop>font-family|font)\s*:\s*(?<value>[^;]+);", RegexOptions.CultureInvariant)]
    private static partial Regex FontDeclarationPattern();

    [GeneratedRegex(@"^var\(\s*--[a-zA-Z0-9-]+\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex SingleVarReferencePattern();
}
