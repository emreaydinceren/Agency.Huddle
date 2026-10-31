using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>
/// The text a reference is written as: its label, its address, the text a picker inserts and the parse of an
/// address back to its key (spec §6, D-9, D-10, D-26). Pure: no store and no clock.
/// </summary>
internal static class ReferenceText
{
    /// <summary>The path every Room address starts with (D-10).</summary>
    private const string RoomPrefix = "/rooms/";

    /// <summary>The path every Team address starts with (D-10).</summary>
    private const string TeamPrefix = "/teams/";

    /// <summary>The full-width commercial at that replaces every @ in a label (D-9 rule 2).</summary>
    private const char FullwidthAt = '＠';

    /// <summary>The mark that shows a label was cut (D-9 rule 4).</summary>
    private const string Ellipsis = "…";

    /// <summary>The characters a label backslash-escapes (D-9 rule 3).</summary>
    private static readonly char[] MarkdownSpecials = ['\\', '[', ']', '`', '*', '_', '<', '>', '&', '!', '~'];

    /// <summary>The characters that end an address's one segment: a Room id or a Team name never contains them.</summary>
    private static readonly char[] RefusedInAddress = ['/', '?', '#'];

    /// <summary>The label for a Name, built by the rules of spec §6 (D-9).</summary>
    /// <param name="name">The Name at the moment of the pick.</param>
    /// <param name="kind">The kind that gives the empty-label fallback.</param>
    /// <returns>The escaped label, never empty.</returns>
    public static string LabelFor(string name, ReferenceKind kind) => ReferenceText.Escape(ReferenceText.PlainLabelFor(name, kind));

    /// <summary>The label without its escaping (D-9 rules 1, 2, 4 and 5): what a chip draws and a snapshot stores.</summary>
    /// <param name="name">The Name at the moment of the pick.</param>
    /// <param name="kind">The kind that gives the empty-label fallback.</param>
    /// <returns>The plain label, never empty.</returns>
    public static string PlainLabelFor(string name, ReferenceKind kind)
    {
        string collapsed = string.Join(' ', name.Replace('@', ReferenceText.FullwidthAt).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length == 0)
        {
            return kind == ReferenceKind.Team ? "Team" : "Room";
        }

        return ReferenceText.CutToTextElements(collapsed, ReferenceLimits.MaxLabelChars);
    }

    /// <summary>The plain label for a Room, which says its Scope (D-26).</summary>
    /// <param name="roomName">The Room's Name.</param>
    /// <param name="team">The Room's Team, or null for a root Room.</param>
    /// <param name="isTeamRoom">Whether the Room is its Team's Team Room.</param>
    /// <returns>The unescaped label.</returns>
    public static string RoomPlainLabelFor(string roomName, string? team, bool isTeamRoom)
    {
        if (team is null)
        {
            return ReferenceText.PlainLabelFor(roomName, ReferenceKind.Room);
        }

        return isTeamRoom
            ? ReferenceText.PlainLabelFor(team + " Team Room", ReferenceKind.Room)
            : ReferenceText.PlainLabelFor(roomName + " (" + team + ")", ReferenceKind.Room);
    }

    /// <summary>The label for a Room, which says its Scope (D-26).</summary>
    /// <param name="roomName">The Room's Name.</param>
    /// <param name="team">The Room's Team, or null for a root Room.</param>
    /// <param name="isTeamRoom">Whether the Room is its Team's Team Room.</param>
    /// <returns>The escaped label.</returns>
    public static string RoomLabelFor(string roomName, string? team, bool isTeamRoom)
    {
        if (team is null)
        {
            return ReferenceText.LabelFor(roomName, ReferenceKind.Room);
        }

        return isTeamRoom
            ? ReferenceText.LabelFor(team + " Team Room", ReferenceKind.Room)
            : ReferenceText.LabelFor(roomName + " (" + team + ")", ReferenceKind.Room);
    }

    /// <summary>The address of a Room, which is the same whatever its Scope (D-10).</summary>
    /// <param name="roomId">The Room's id.</param>
    /// <returns>The site-relative address.</returns>
    public static string RoomHref(string roomId) => string.Concat(ReferenceText.RoomPrefix, roomId);

    /// <summary>The address of a Team, which is its escaped Name (D-10).</summary>
    /// <param name="name">The Team's Name.</param>
    /// <returns>The site-relative address.</returns>
    public static string TeamHref(string name) => string.Concat(ReferenceText.TeamPrefix, Uri.EscapeDataString(name));

    /// <summary>Reads a Room address back to its id: one valid id and nothing after it (D-10).</summary>
    /// <param name="url">The link's address.</param>
    /// <param name="roomId">The Room id, or null when the address is refused.</param>
    /// <returns>True when the address is a Room address.</returns>
    public static bool TryParseRoomHref(string url, [NotNullWhen(true)] out string? roomId)
    {
        roomId = null;
        if (!url.StartsWith(ReferenceText.RoomPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = url[ReferenceText.RoomPrefix.Length..];
        if (remainder.IndexOfAny(ReferenceText.RefusedInAddress) >= 0 || !NameRules.IsValidId(remainder))
        {
            return false;
        }

        roomId = remainder;
        return true;
    }

    /// <summary>Reads a Team address back to its Name: one segment that decodes to a legal Team Name, not new (D-10).</summary>
    /// <param name="url">The link's address.</param>
    /// <param name="teamName">The decoded Team Name, or null when the address is refused.</param>
    /// <returns>True when the address is a Team address.</returns>
    public static bool TryParseTeamHref(string url, [NotNullWhen(true)] out string? teamName)
    {
        teamName = null;
        if (!url.StartsWith(ReferenceText.TeamPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = url[ReferenceText.TeamPrefix.Length..];
        if (remainder.Length == 0 || remainder.IndexOfAny(ReferenceText.RefusedInAddress) >= 0 || !ReferenceText.HasWellFormedEscapes(remainder))
        {
            return false;
        }

        string decoded = Uri.UnescapeDataString(remainder);
        if (string.Equals(decoded, "new", StringComparison.OrdinalIgnoreCase) || TeamNames.ValidateTeamName(decoded, []) is not null)
        {
            return false;
        }

        teamName = decoded;
        return true;
    }

    /// <summary>The text a picker inserts for a hit (spec §6). A Library file is inserted by its path, not here.</summary>
    /// <param name="hit">The picked hit.</param>
    /// <returns>The text, or null for a kind the picker does not insert by this rule (a Library file).</returns>
    public static string? InsertFor(SearchHit hit) => hit switch
    {
        AgentHit agent => string.Concat("@", agent.Name),
        TaskHit task => task.Id.ToString(),
        RoomHit room => string.Concat("[", ReferenceText.RoomLabelFor(room.RoomName, room.Team, room.IsTeamRoom), "](", ReferenceText.RoomHref(room.RoomId), ")"),
        TeamHit team => string.Concat("[", ReferenceText.LabelFor(team.Name, ReferenceKind.Team), "](", ReferenceText.TeamHref(team.Name), ")"),
        _ => null,
    };

    /// <summary>
    /// Cuts text to a number of text elements, adding an ellipsis when it was cut. An emoji on the boundary is
    /// never split.
    /// </summary>
    /// <param name="text">The text to cut.</param>
    /// <param name="maxElements">The most text elements kept before the ellipsis.</param>
    /// <returns>The text, cut when it was longer.</returns>
    private static string CutToTextElements(string text, int maxElements)
    {
        StringInfo info = new(text);
        if (info.LengthInTextElements <= maxElements)
        {
            return text;
        }

        return string.Concat(info.SubstringByTextElements(0, maxElements), ReferenceText.Ellipsis);
    }

    /// <summary>Backslash-escapes each Markdown special in text.</summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped text.</returns>
    private static string Escape(string text)
    {
        StringBuilder builder = new(text.Length + 8);
        foreach (char character in text)
        {
            if (Array.IndexOf(ReferenceText.MarkdownSpecials, character) >= 0)
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether every % in an address starts a two-digit hexadecimal escape. <see cref="Uri.UnescapeDataString(string)"/>
    /// leaves a bad escape in place rather than failing, so the check is made here.
    /// </summary>
    /// <param name="text">The address segment.</param>
    /// <returns>True when every escape is well formed.</returns>
    private static bool HasWellFormedEscapes(string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] != '%')
            {
                index++;
                continue;
            }

            if (index + 2 >= text.Length || !Uri.IsHexDigit(text[index + 1]) || !Uri.IsHexDigit(text[index + 2]))
            {
                return false;
            }

            index += 3;
        }

        return true;
    }
}
