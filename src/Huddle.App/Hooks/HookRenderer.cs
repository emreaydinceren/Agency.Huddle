namespace Agency.Huddle.App.Hooks;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Pure substitution engine for the <c>{{name}}</c> placeholder syntax used throughout
/// <see cref="HookCatalog"/>. Scans a template exactly once, replacing every well-formed
/// placeholder with its supplied value and leaving everything else — including a placeholder with
/// no matching value — exactly as written.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <c>{{...}}</c> and not <c>&lt;...&gt;</c>.</b> The <c>getHelp.messages</c> hook's default
/// text literally sends a model the documentation line <c>"[Room: &lt;name&gt; (id: &lt;id&gt;)]"</c>.
/// An angle-bracket placeholder syntax would treat that line as a placeholder and silently eat it;
/// double braces do not collide with it, and a single-brace JSON example in a prompt (e.g.
/// <c>{"type": "object"}</c>) is likewise never touched, since only a double <c>{{</c> opens a token.
/// </para>
/// <para>
/// <b>Key style.</b> Dictionary keys, and the tokens <see cref="FindPlaceholders"/> returns, include
/// the surrounding braces, e.g. <c>"{{helpTool}}"</c> rather than <c>"helpTool"</c>. This matches how
/// <see cref="HookDefinition.Placeholders"/> and <see cref="HookDefinition.RequiredPlaceholders"/> are
/// themselves stored, so a caller can build a values dictionary straight from those lists, and
/// <see cref="IHookSource.Render(string, IReadOnlyDictionary{string, string})"/> can pass its
/// <c>values</c> argument straight through to this type, with no stripping or re-adding of delimiters
/// anywhere in between.
/// </para>
/// <para>
/// <b>Single pass.</b> <see cref="Render"/> walks the template once, left to right, copying each
/// literal run and each substituted value into one output buffer as it goes; it never re-scans the
/// buffer it is building. Concretely, the placeholder matches are found against the original template
/// up front, and the output is assembled from slices of that same original string plus the looked-up
/// values — so a value that itself contains <c>{{...}}</c> text, including another placeholder's own
/// token, is copied into the output unchanged rather than expanded again. A naive loop that calls
/// <see cref="string.Replace(string, string)"/> once per entry in the values dictionary would instead
/// re-scan text it had just inserted, letting one substitution accidentally trigger another; that
/// shortcut is deliberately not taken here.
/// </para>
/// <para>
/// <b>Malformed and empty placeholders.</b> A <c>{{...}}</c> span is only recognized as a placeholder
/// when its inner content is non-empty and contains no whitespace. <c>{{}}</c>, <c>{{ }}</c>, and
/// <c>{{ nope here }}</c> are therefore left verbatim, braces included — the conservative choice: a
/// malformed token is far more likely to be prompt text an author wrote on purpose, or a typo best left
/// visible for someone to notice, than a name this type should try to guess at and expand.
/// </para>
/// </remarks>
internal static class HookRenderer
{
    /// <summary>
    /// Matches one well-formed placeholder token: two literal opening braces, one or more
    /// non-whitespace characters (matched lazily, so the token ends at the nearest closing pair), and
    /// two literal closing braces. A span whose inner content is empty or contains whitespace never
    /// matches this pattern and is therefore never treated as a placeholder.
    /// </summary>
    private static readonly Regex PlaceholderPattern =
        new(@"\{\{(\S+?)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Replaces every well-formed <c>{{name}}</c> token in <paramref name="template"/> that has a
    /// matching entry in <paramref name="values"/>, in a single left-to-right pass over the template.
    /// </summary>
    /// <param name="template">
    /// The text to render. Despite the non-nullable annotation, a caller passing <see langword="null"/>
    /// (or an empty string) gets back <see cref="string.Empty"/> rather than an exception: rejecting a
    /// missing or invalid template is the configuration store's responsibility, not the renderer's.
    /// </param>
    /// <param name="values">
    /// The substitution values, keyed by token including its braces, e.g. <c>"{{helpTool}}"</c>.
    /// </param>
    /// <returns>
    /// <paramref name="template"/> with every placeholder token that has a matching key in
    /// <paramref name="values"/> replaced by that value. A placeholder with no matching key, and any
    /// malformed <c>{{...}}</c> span, is left in the output exactly as it appeared in the template.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    internal static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var output = new StringBuilder(template.Length);
        var position = 0;

        foreach (Match match in PlaceholderPattern.Matches(template))
        {
            output.Append(template, position, match.Index - position);
            output.Append(values.TryGetValue(match.Value, out var value) ? value : match.Value);

            position = match.Index + match.Length;
        }

        output.Append(template, position, template.Length - position);

        return output.ToString();
    }

    /// <summary>
    /// Finds every distinct, well-formed <c>{{name}}</c> token in <paramref name="template"/>, for the
    /// hook validator (checking a template only uses declared placeholders) and the settings UI
    /// (rendering an input for each one).
    /// </summary>
    /// <param name="template">
    /// The text to scan. Despite the non-nullable annotation, <see langword="null"/> or an empty string
    /// yields an empty list rather than an exception.
    /// </param>
    /// <returns>
    /// Each distinct token, braces included, in the order it first appears in
    /// <paramref name="template"/>. A malformed span such as <c>{{ }}</c> is not a placeholder and is
    /// never included.
    /// </returns>
    internal static IReadOnlyList<string> FindPlaceholders(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokens = new List<string>();

        foreach (Match match in PlaceholderPattern.Matches(template))
        {
            if (seen.Add(match.Value))
            {
                tokens.Add(match.Value);
            }
        }

        return tokens;
    }
}
