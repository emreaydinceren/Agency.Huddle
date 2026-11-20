using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder.Model;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Expands the seed tokens in a Message and builds the <see cref="MessageReference"/> list the way the app's send
/// path does (<c>ReferenceSnapshotter</c>, <c>ReferenceText</c>, <c>ReferencePickerModel.LibraryInsertFor</c>). The seeder cannot see
/// those internals, so each format is mirrored here and pinned by a test that compares it with the app's own function.
/// Seeder spec §5 (tokens); SEED-INV-9, SEED-INV-11.
/// </summary>
/// <remarks>
/// Tokens: <c>{{lib:Teams/Platform/Architecture.md}}</c> (a Library file under the data folder), <c>{{upload:name.png}}</c> (a file
/// attached in the Message's own Room), <c>{{room:key}}</c> and <c>{{team:Name}}</c>.
/// </remarks>
internal static partial class MessageReferences
{
    /// <summary>The longest label a reference carries, mirroring <c>ReferenceLimits.MaxLabelChars</c>.</summary>
    internal const int MaxLabelChars = 60;

    private const string Ellipsis = "…";
    private const char FullwidthAt = '＠';
    private const string MarkdownSpecials = "\\[]`*_<>&!~";

    [GeneratedRegex(@"\{\{(lib|upload|room|team):([^}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])([A-Z][A-Z0-9]{0,7}-[0-9]{1,9})(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    private static partial Regex TaskIdPattern();

    /// <summary>Expands the tokens of one Message and builds its references.</summary>
    /// <param name="text">The seed text, possibly holding tokens.</param>
    /// <param name="room">The Room the Message is in.</param>
    /// <param name="plan">The plan, for Tasks and for the Rooms a token names.</param>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="roomIds">Room key to Room id.</param>
    /// <returns>The final text and, when the Room keeps references, the list.</returns>
    internal static ExpandedMessage Expand(string text, SeedRoom room, SeedPlan plan, string dataDir, IReadOnlyDictionary<string, string> roomIds)
    {
        StringBuilder output = new();
        List<(int Position, int End, MessageReference Reference)> found = [];
        int cursor = 0;
        foreach (Match match in TokenPattern().Matches(text))
        {
            output.Append(text, cursor, match.Index - cursor);
            cursor = match.Index + match.Length;
            int start = output.Length;
            MessageReference reference = ExpandToken(match.Groups[1].Value, match.Groups[2].Value.Trim(), room, plan, dataDir, roomIds, output);
            found.Add((start, output.Length, reference));
        }

        output.Append(text, cursor, text.Length - cursor);
        string expanded = output.ToString();

        foreach (Match match in TaskIdPattern().Matches(expanded))
        {
            string id = match.Groups[1].Value;
            SeedTask? task = plan.Tasks.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
            if (task is null || found.Any(f => match.Index >= f.Position && match.Index < f.End))
            {
                continue;
            }

            found.Add((match.Index, match.Index + match.Length, TaskReference(task)));
        }

        if (!room.WithReferences)
        {
            return new ExpandedMessage(expanded, null);
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<MessageReference> references = [];
        foreach ((int _, int _, MessageReference reference) in found.OrderBy(f => f.Position))
        {
            string key = reference.Kind is ReferenceKind.Team or ReferenceKind.LibraryFile ? reference.Key.ToUpperInvariant() : reference.Key;
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
            if (seen.Add(reference.Kind.ToString() + "\u001F" + key))
#pragma warning restore HAA0102
            {
                references.Add(reference);
            }
        }

        return new ExpandedMessage(expanded, references.Count == 0 ? null : references);
    }

    /// <summary>The chip a Task reference stores: the id, the title at send time and the status in wire form.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>The reference.</returns>
    internal static MessageReference TaskReference(SeedTask task)
    {
        if (!TaskStates.TryParse(task.Status, out TaskState state))
        {
            throw new InvalidOperationException($"Task {task.Id} has the unknown status '{task.Status}'.");
        }

        return new MessageReference(ReferenceKind.Task, task.Id, task.Id, task.FormerTitle ?? task.Title, state.ToWire());
    }

    /// <summary>The absolute path of a Library file, built as <c>LibraryInsertFor</c> builds it: the root's path joined with the segments.</summary>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="relativePath">The path below the data folder, using <c>/</c>.</param>
    /// <returns>The absolute path.</returns>
    internal static string LibraryPath(string dataDir, string relativePath) =>
        Path.Combine([dataDir, .. relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>
    /// The body text of a Library file token: its file name as a link to the file's Library address (<c>?library=</c> root id, then the
    /// path below the root), so a message never prints the machine path and the link works on a copied data folder. The reference
    /// stored beside it keeps the absolute path, the only key the app resolves.
    /// </summary>
    /// <param name="relativePath">The path below the data folder, using <c>/</c>; its first segment is a built-in root's folder.</param>
    /// <returns>The Markdown link.</returns>
    internal static string LibraryLink(string relativePath)
    {
        string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string address = string.Join('/', segments.Select((segment, index) => Uri.EscapeDataString(index == 0 ? segment.ToLowerInvariant() : segment)));
        return "[" + Escape(segments[^1]) + "](?library=" + address + ")";
    }

    /// <summary>
    /// The folder a Room's attachments go to: <c>Raw/&lt;roomId&gt;</c> for a root Room, and
    /// <c>Teams/&lt;Team&gt;/raw/&lt;roomId&gt;</c> for a Room scoped to a Team (<c>ChatUploadService</c> saves into the last candidate).
    /// </summary>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="room">The Room.</param>
    /// <param name="roomId">The Room's id.</param>
    /// <returns>The absolute folder.</returns>
    internal static string UploadFolder(string dataDir, SeedRoom room, string roomId) =>
        room.Team is null
            ? Path.Combine(dataDir, "Raw", roomId)
            : Path.Combine(dataDir, "Teams", room.Team, TeamNames.RawFolder, roomId);

    /// <summary>The plain label of a Room, which says its Scope (<c>ReferenceText.RoomPlainLabelFor</c>).</summary>
    /// <param name="room">The Room.</param>
    /// <returns>The unescaped label.</returns>
    internal static string RoomPlainLabel(SeedRoom room)
    {
        if (room.Team is null)
        {
            return PlainLabel(room.Name, "Room");
        }

        return room.IsTeamRoom ? PlainLabel(room.Team + " Team Room", "Room") : PlainLabel(room.Name + " (" + room.Team + ")", "Room");
    }

    /// <summary>The plain label of a Team (<c>ReferenceText.PlainLabelFor</c> with the Team fallback).</summary>
    /// <param name="name">The Team name.</param>
    /// <returns>The unescaped label.</returns>
    internal static string TeamPlainLabel(string name) => PlainLabel(name, "Team");

    /// <summary>Backslash-escapes each Markdown special in a label (<c>ReferenceText.LabelFor</c>).</summary>
    /// <param name="plain">The plain label.</param>
    /// <returns>The escaped label.</returns>
    internal static string Escape(string plain)
    {
        StringBuilder builder = new(plain.Length + 8);
        foreach (char character in plain)
        {
            if (MarkdownSpecials.Contains(character, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static MessageReference ExpandToken(
        string kind,
        string value,
        SeedRoom room,
        SeedPlan plan,
        string dataDir,
        IReadOnlyDictionary<string, string> roomIds,
        StringBuilder output)
    {
        switch (kind)
        {
            case "lib":
            {
                string path = LibraryPath(dataDir, value);
                output.Append(LibraryLink(value));
                return new MessageReference(ReferenceKind.LibraryFile, path, Path.GetFileName(path));
            }

            case "upload":
            {
                string path = Path.Combine(UploadFolder(dataDir, room, roomIds[room.Key]), value);
                output.Append('`').Append(path).Append('`');
                return new MessageReference(ReferenceKind.LibraryFile, path, value);
            }

            case "room":
            {
                SeedRoom target = plan.Rooms.FirstOrDefault(r => string.Equals(r.Key, value, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"The token {{{{room:{value}}}}} names no Room.");
                string id = roomIds[target.Key];
                string plain = RoomPlainLabel(target);
                output.Append('[').Append(Escape(plain)).Append("](/rooms/").Append(id).Append(')');
                return new MessageReference(ReferenceKind.Room, id, plain);
            }

            default:
            {
                if (!plan.Teams.Any(t => string.Equals(t.Name, value, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException($"The token {{{{team:{value}}}}} names no Team.");
                }

                output.Append('[').Append(Escape(TeamPlainLabel(value))).Append("](/teams/").Append(Uri.EscapeDataString(value)).Append(')');
                return new MessageReference(ReferenceKind.Team, value, TeamPlainLabel(value));
            }
        }
    }

    private static string PlainLabel(string name, string fallback)
    {
        string collapsed = string.Join(' ', name.Replace('@', FullwidthAt).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length == 0)
        {
            return fallback;
        }

        StringInfo info = new(collapsed);
        return info.LengthInTextElements <= MaxLabelChars
            ? collapsed
            : string.Concat(info.SubstringByTextElements(0, MaxLabelChars), Ellipsis);
    }
}
