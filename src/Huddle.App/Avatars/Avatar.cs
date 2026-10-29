using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// What an Agent or the Human shows for themselves in the chat surface: an optional image, an
/// optional text label standing in for it, and an optional background colour. Public because a Razor
/// <c>[Parameter]</c> may not be of an <c>internal</c> type (<c>CS0053</c>), and a later task binds one.
/// </summary>
/// <remarks>
/// <para>
/// Rendering picks between the three fields in one fixed order - <see cref="Image"/>, then
/// <see cref="Label"/>, then initials derived from whichever name the caller already has - and stops at
/// the first one present. All three fields <see langword="null"/> is exactly today's rendering:
/// initials are not a stored kind of avatar, they are what rendering falls back to when nothing has
/// been chosen. That is the same "absent is normal" shape <c>prompts.json</c> and <c>appearance.json</c>
/// already use for their own per-installation overrides - an absent file, or an absent field, means the
/// default applies, not that a value called "default" was written down.
/// </para>
/// <para>
/// <see cref="Background"/> sits outside that three-way choice entirely; it is orthogonal to it. A
/// transparent PNG in <see cref="Image"/> still wants a background to sit on, and initials - the
/// fallback every avatar reaches when neither an image nor a label was chosen - want one most of all,
/// since there is no image to supply one.
/// </para>
/// </remarks>
/// <param name="Label">The text standing in for an image, or <see langword="null"/> to fall through to initials.</param>
/// <param name="Image">The avatar image's source, or <see langword="null"/> when there is none.</param>
/// <param name="Background">The background colour behind <see cref="Image"/> or the fallback initials, or <see langword="null"/> for the surface's own default.</param>
public sealed record Avatar(string? Label, string? Image, string? Background)
{
    /// <summary>
    /// The longest a <see cref="Label"/> may be, counted in Unicode text elements rather than in
    /// <see cref="string.Length"/> - see <see cref="TrimLabel(string?)"/> for why the distinction
    /// matters. Named rather than written as a literal because the editor's own character counter
    /// has to agree with the guard that enforces it, and two copies of <c>6</c> would eventually not.
    /// Six is the most that stays legible inside the 32px circle at its smallest scaled font; the
    /// 24px row avatar cannot hold that many and shows only <see cref="SmallLabelTextElements"/>
    /// of them (see <c>AgentAvatar</c>).
    /// </summary>
    internal const int MaxLabelTextElements = 6;

    /// <summary>How many text elements of a label the 24px row avatar draws, which has no room for more at the 10px font floor.</summary>
    internal const int SmallLabelTextElements = 3;

    /// <summary>The avatar of an Agent or the Human who has not customised one - today's rendering exactly, with all three fields unset.</summary>
    public static Avatar None { get; } = new(Label: null, Image: null, Background: null);

    /// <summary>
    /// Whether <paramref name="name"/> is a bare file name that can be joined onto the image directory: no directory separator
    /// of either platform, no drive colon and no <c>..</c>. Checked on both separators whatever the host, because a hand-edited
    /// file written on one platform is read on another, and the name becomes a path to read and to delete.
    /// </summary>
    /// <param name="name">The stored image name to check.</param>
    /// <returns><see langword="true"/> when the name cannot leave the image directory.</returns>
    internal static bool IsBareImageName(string name) =>
        name.Length > 0
        && name.IndexOfAny(['/', '\\', ':']) < 0
        && !name.Contains("..", StringComparison.Ordinal);

    /// <summary>Whether every field is unset, meaning nothing overrides the default rendering.</summary>
    public bool IsDefault => this.Label is null && this.Image is null && this.Background is null;

    /// <summary>
    /// Strips control characters from <paramref name="raw"/> and takes at most its first <see cref="MaxLabelTextElements"/> text
    /// elements, returning <see langword="null"/> when the result is empty or whitespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A label is measured in Unicode <b>text elements</b> - UAX #29 extended grapheme clusters, read
    /// with <see cref="StringInfo.GetTextElementEnumerator(string)"/> - never in <see cref="string.Length"/>
    /// UTF-16 code units and never in <see cref="System.Text.Rune"/> count. <see cref="string.Length"/>
    /// does not account for surrogate pairs at all, and a <see cref="System.Text.Rune"/> fixes only
    /// that: it still counts a skin-tone modifier or a zero-width-joiner sequence as several units when
    /// a reader sees one glyph. Only a text element matches what a person means by "characters
    /// of a label":
    /// </para>
    /// <list type="table">
    /// <listheader><term>Input</term><description><see cref="string.Length"/> / Rune count / text elements</description></listheader>
    /// <item><term>grinning face U+1F600</term><description>2 / 1 / 1</description></item>
    /// <item><term>thumbs up + skin tone U+1F44D U+1F3FD</term><description>4 / 2 / 1</description></item>
    /// <item><term>woman technologist U+1F469 U+200D U+1F4BB (ZWJ)</term><description>5 / 3 / 1</description></item>
    /// <item><term>rainbow flag U+1F3F3 U+FE0F U+200D U+1F308</term><description>6 / 4 / 1</description></item>
    /// </list>
    /// </remarks>
    /// <param name="raw">The label text to trim, or <see langword="null"/>.</param>
    /// <returns>The trimmed label, or <see langword="null"/> when <paramref name="raw"/> is <see langword="null"/>, empty, whitespace, or reduces to nothing after stripping control characters.</returns>
    internal static string? TrimLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var stripped = string.Concat(raw.Where(character => !char.IsControl(character)));
        if (string.IsNullOrWhiteSpace(stripped))
        {
            return null;
        }

        var trimmed = FirstTextElements(stripped, MaxLabelTextElements);
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    /// <summary>The first <paramref name="count"/> text elements of <paramref name="text"/>, or all of it when it has fewer.</summary>
    /// <param name="text">The text to cut.</param>
    /// <param name="count">How many text elements to keep.</param>
    internal static string FirstTextElements(string text, int count)
    {
        StringBuilder builder = new();
        var elements = StringInfo.GetTextElementEnumerator(text);
        var taken = 0;
        while (taken < count && elements.MoveNext())
        {
            builder.Append((string)elements.Current);
            taken++;
        }

        return builder.ToString();
    }
}
