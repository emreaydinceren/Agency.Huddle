using System.Globalization;
using System.Text;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The single normalise step every tag candidate passes through, inline or frontmatter (Spec §6.17), so the two sources
/// cannot apply different rules. Rules marked (V) in the spec are Huddle's reading of Obsidian and live here, in one place.
/// </summary>
internal static class NoteTagText
{
    /// <summary>The longest tag, in UTF-16 code units, after invisible characters are removed and a trailing slash dropped.</summary>
    internal const int MaxLength = 100;

    /// <summary>Whether <paramref name="rune"/> may appear inside a tag: a letter of any script, a mark, a digit, a symbol or emoji, a format character
    /// (ZWJ and friends), or one of <c>_ - /</c>. Whitespace, controls, other ASCII punctuation and Unicode punctuation end a tag.</summary>
    /// <param name="rune">The character to test.</param>
    internal static bool IsTagRune(Rune rune)
    {
        if (rune.IsAscii)
        {
            char c = (char)rune.Value;
            return char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '/';
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber
            or UnicodeCategory.MathSymbol or UnicodeCategory.CurrencySymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol
            or UnicodeCategory.Format;
    }

    /// <summary>Whether every character of <paramref name="text"/> may appear inside a tag (the frontmatter rule: a value is skipped, not truncated).</summary>
    /// <param name="text">A frontmatter value.</param>
    internal static bool IsAllTagRunes(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (!IsTagRune(rune))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Normalises <paramref name="candidate"/> (the text after the <c>#</c>): removes invisible characters, drops a trailing <c>/</c> and
    /// everything after a <c>//</c>, then rejects a leading <c>/</c>, an all-digit tag, a tag over <see cref="MaxLength"/> and a Task id.</summary>
    /// <param name="candidate">The raw tag text, without the leading <c>#</c>.</param>
    /// <param name="tag">The normalised tag when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the candidate is a tag.</returns>
    internal static bool TryNormalise(string candidate, out string tag)
    {
        tag = string.Empty;
        string text = RemoveInvisible(candidate);
        int doubled = text.IndexOf("//", StringComparison.Ordinal);
        if (doubled >= 0)
        {
            text = text[..doubled];
        }

        text = text.TrimEnd('/');
        if (text.Length == 0 || text[0] == '/' || text.Length > MaxLength || !HasNonDigitNonSlash(text) || IsTaskId(text))
        {
            return false;
        }

        tag = text;
        return true;
    }

    private static string RemoveInvisible(string text)
    {
        StringBuilder builder = new(text.Length);
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (!IsInvisible(rune))
            {
                _ = builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }

    /// <summary>(V) Every format character is removed (bidi controls, zero-width space, BOM, soft hyphen, word joiner) except the joiners that
    /// emoji and script sequences need (ZWJ, ZWNJ) and the emoji tag characters, so two tags that look identical are one tag.</summary>
    private static bool IsInvisible(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format
        && rune.Value is not (0x200C or 0x200D or (>= 0xE0020 and <= 0xE007F));

    private static bool HasNonDigitNonSlash(string text)
    {
        foreach (char c in text)
        {
            if (c != '/' && !char.IsDigit(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A tag whose whole text or first segment is a valid, upper-case Task id is the id, not a tag (D-32).</summary>
    private static bool IsTaskId(string text)
    {
        int slash = text.IndexOf('/', StringComparison.Ordinal);
        string first = slash < 0 ? text : text[..slash];
        return string.Equals(first, first.ToUpperInvariant(), StringComparison.Ordinal) && TaskId.TryParse(first, out _);
    }
}
