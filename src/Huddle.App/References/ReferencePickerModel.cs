using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.References;

/// <summary>Turns the picker's state into its groups, rows and lines, with no renderer (spec §5.3, §5.7, §11; D-6, D-22, D-23, D-25, D-27).</summary>
internal static class ReferencePickerModel
{
    /// <summary>The Tasks a single filter shows (the <c>#</c> picker's cap).</summary>
    private const int SingleTaskCap = 8;

    /// <summary>The rows each other group shows under one filter.</summary>
    private const int SingleCap = 20;

    /// <summary>The rows each group shows under All.</summary>
    private const int AllCap = 5;

    /// <summary>The fewest characters a Library search needs (spec §5.7).</summary>
    private const int LibraryMinChars = 3;

    /// <summary>The footer line of spec §5.7.</summary>
    private const string FooterText = "Narrow with teammate: task: room: team: lib:";

    /// <summary>The note on a Teammate who is not a Member of the Room (D-22).</summary>
    private const string NotMemberNote = "Not in this Room";

    /// <summary>The prefix of the note on a Team Room (D-27).</summary>
    private const string TeamRoomNotePrefix = "Team Room · ";

    /// <summary>The line for a Library search with too few characters (spec §5.7).</summary>
    private const string LibraryTypeText = "Type 3 characters to search the Library.";

    /// <summary>The groups in their order: the active side first, in the order of spec §5.3.</summary>
    private static readonly SearchSlot[] ActiveSlots =
    [
        new(SearchFilter.Teammates, SearchScope.Active, "Teammates"),
        new(SearchFilter.Tasks, SearchScope.Active, "Tasks"),
        new(SearchFilter.Rooms, SearchScope.Active, "Rooms"),
        new(SearchFilter.Teams, SearchScope.Active, "Teams"),
        new(SearchFilter.Library, SearchScope.Active, "Library"),
    ];

    /// <summary>The second pass: the archived side of the groups that have one, after every active group.</summary>
    private static readonly SearchSlot[] ArchivedSlots =
    [
        new(SearchFilter.Tasks, SearchScope.Archived, "Closed Tasks"),
        new(SearchFilter.Rooms, SearchScope.Archived, "Archived Rooms"),
    ];

    /// <summary>Builds the picker view for one state.</summary>
    /// <param name="input">The trigger, token, filter, group states and Members.</param>
    /// <returns>The groups, rows and lines to draw.</returns>
    public static ReferencePickerView Build(ReferencePickerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        HashSet<string> members = new(input.MemberNames, StringComparer.OrdinalIgnoreCase);
        List<(string Heading, List<Draft> Drafts)> groups = [];
        foreach (SearchSlot slot in SelectedSlots(input))
        {
            if (!input.Groups.TryGetValue((slot.Group, slot.Scope), out SearchGroupState? state) || state is not SearchGroupDone done)
            {
                continue;
            }

            List<Draft> drafts = DraftsFor(slot, done.Result, input, members);
            if (drafts.Count > 0)
            {
                groups.Add((slot.Heading, drafts));
            }
        }

        int total = groups.Sum(group => group.Drafts.Count);
        List<ReferencePickerGroup> built = [];
        int index = 0;
        foreach ((string heading, List<Draft> drafts) in groups)
        {
            List<ReferencePickerRow> rows = [];
            foreach (Draft draft in drafts)
            {
                string id = string.Concat(input.ListboxId, "-", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                index++;
                rows.Add(new ReferencePickerRow(id, draft.Hit, draft.InsertText, draft.Dimmed, draft.Note, AccessibleNameFor(draft)));
            }

            built.Add(new ReferencePickerGroup(heading, rows));
        }

        string? emptyText = total == 0 ? EmptyTextFor(input) : null;
        string liveText = total switch
        {
            0 => "No results",
            1 => "1 result",
            _ => string.Concat(total.ToString(System.Globalization.CultureInfo.InvariantCulture), " results"),
        };
        int highlight = total == 0 ? 0 : Math.Clamp(input.Highlight, 0, total - 1);

        // The footer names the prefixes, so it shows on a bare `@` only (spec §5.3); the `#` picker is Tasks only (spec §5.5).
        string footer = input.Trigger == '#' || input.Token.Length > 0 ? string.Empty : FooterText;
        return new ReferencePickerView(built, emptyText, footer, liveText, highlight);
    }

    /// <summary>The groups the picker searches for this state, in order: one filter, All, or the Teammates alone under <c>/invite </c>.</summary>
    /// <param name="input">The input.</param>
    /// <returns>The slots, active side first.</returns>
    private static List<SearchSlot> SelectedSlots(ReferencePickerInput input)
    {
        List<SearchSlot> slots = [];
        foreach (SearchSlot slot in ActiveSlots)
        {
            if (IsSelected(input, slot.Group))
            {
                slots.Add(slot);
            }
        }

        foreach (SearchSlot slot in ArchivedSlots)
        {
            if (IsSelected(input, slot.Group))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>Whether a group is searched for this state.</summary>
    /// <param name="input">The input.</param>
    /// <param name="group">The group.</param>
    /// <returns>True when the group shows.</returns>
    private static bool IsSelected(ReferencePickerInput input, SearchFilter group)
    {
        if (input.InviteCommand)
        {
            return group == SearchFilter.Teammates;
        }

        return input.Filter == SearchFilter.All || input.Filter == group;
    }

    /// <summary>The rows of one finished group, cut to its cap and ordered as spec §5.3 says.</summary>
    /// <param name="slot">The group.</param>
    /// <param name="result">The provider's batch.</param>
    /// <param name="input">The input, for the filter, the Members and the Library roots.</param>
    /// <param name="members">The Members, compared ignoring case.</param>
    /// <returns>The drafts, which have no id yet.</returns>
    private static List<Draft> DraftsFor(SearchSlot slot, SearchResult result, ReferencePickerInput input, HashSet<string> members)
    {
        IEnumerable<SearchHit> hits = result.Hits;
        if (slot.Group == SearchFilter.Teammates)
        {
            // A Member comes first unless the box is /invite, where the people it can add come first (D-22, D-23).
            hits = hits.OrderBy(hit => hit is TeammateHit teammate && members.Contains(teammate.Name) != input.InviteCommand ? 0 : 1);
        }

        return hits.Take(CapFor(input.Filter, slot.Group)).Select(hit => DraftFor(hit, input, members)).ToList();
    }

    /// <summary>The cap of a group under the filter: 5 under All, 8 for Tasks and 20 for the others under one filter.</summary>
    /// <param name="filter">The filter the box is set to.</param>
    /// <param name="group">The group.</param>
    /// <returns>The most rows the group shows.</returns>
    private static int CapFor(SearchFilter filter, SearchFilter group)
    {
        if (filter == SearchFilter.All)
        {
            return AllCap;
        }

        return group == SearchFilter.Tasks ? SingleTaskCap : SingleCap;
    }

    /// <summary>The row of one hit: the text it inserts, whether it is dimmed, and its note.</summary>
    /// <param name="hit">The hit.</param>
    /// <param name="input">The input, for the Library roots.</param>
    /// <param name="members">The Members, compared ignoring case.</param>
    /// <returns>The draft.</returns>
    private static Draft DraftFor(SearchHit hit, ReferencePickerInput input, HashSet<string> members)
    {
        switch (hit)
        {
            case TeammateHit teammate:
                bool isMember = members.Contains(teammate.Name);
                return new Draft(hit, string.Concat("@", teammate.Name), !isMember, isMember ? null : NotMemberNote);
            case RoomHit room:
                return new Draft(hit, InsertFor(hit), false, RoomNote(room));
            case LibraryHit library:
                return new Draft(hit, LibraryInsertFor(library, input.LibraryRootPaths), false, null);
            default:
                return new Draft(hit, InsertFor(hit), false, null);
        }
    }

    /// <summary>The text a pick of a non-Library hit writes (spec §6).</summary>
    /// <param name="hit">The hit.</param>
    /// <returns>The text.</returns>
    private static string InsertFor(SearchHit hit)
    {
        return ReferenceText.InsertFor(hit) ?? throw new InvalidOperationException(nameof(hit));
    }

    /// <summary>The note of a Room: none for a root Room, its Team for a scoped Room, and the Team Room's note for a Team Room (D-27).</summary>
    /// <param name="room">The Room hit.</param>
    /// <returns>The note, or null.</returns>
    private static string? RoomNote(RoomHit room)
    {
        if (room.Team is null)
        {
            return null;
        }

        return room.IsTeamRoom ? string.Concat(TeamRoomNotePrefix, room.Team) : room.Team;
    }

    /// <summary>The text a Library file inserts: its full path, joined from its root's path, in a code span (spec §6).</summary>
    /// <param name="library">The Library hit.</param>
    /// <param name="rootPaths">The full path of each root, keyed by root id.</param>
    /// <returns>The code span.</returns>
    private static string LibraryInsertFor(LibraryHit library, IReadOnlyDictionary<string, string> rootPaths)
    {
        string[] segments = library.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string path;
        if (rootPaths.TryGetValue(library.RootId, out string? rootPath))
        {
            List<string> parts = [rootPath];
            parts.AddRange(segments);
            path = Path.Combine(parts.ToArray());
        }
        else
        {
            path = string.Join('/', segments);
        }

        return string.Concat("`", path, "`");
    }

    /// <summary>The line for an empty picker: the Library's three characters, the task line for <c>#</c>, or the Teammate line for <c>@</c> (spec §5.7).</summary>
    /// <param name="input">The input.</param>
    /// <returns>The line.</returns>
    private static string EmptyTextFor(ReferencePickerInput input)
    {
        // The three characters count the phrase after the filter prefix, not the raw token that still carries `lib:`.
        if (input.Filter == SearchFilter.Library
            && ReferenceQueryParser.Parse(input.Token, input.Trigger, input.InviteCommand).Phrase.Length < LibraryMinChars)
        {
            return LibraryTypeText;
        }

        if (input.Trigger == '#')
        {
            return string.Concat("No task matches '#", input.Token, "'");
        }

        return string.Concat("Nothing matches '@", input.Token, "'.");
    }

    /// <summary>The accessible name of a row: its card's text, then its note after a comma (spec §11).</summary>
    /// <param name="draft">The row's draft.</param>
    /// <returns>The name.</returns>
    private static string AccessibleNameFor(Draft draft)
    {
        return draft.Note is null ? draft.Hit.Title : string.Concat(draft.Hit.Title, ", ", draft.Note);
    }

    /// <summary>A row before its id is set.</summary>
    /// <param name="Hit">The hit.</param>
    /// <param name="InsertText">The text a pick writes.</param>
    /// <param name="Dimmed">Whether the row is dimmed.</param>
    /// <param name="Note">The note, or null.</param>
    private sealed record Draft(SearchHit Hit, string InsertText, bool Dimmed, string? Note);

    /// <summary>One drawable group: its filter, its side and its heading.</summary>
    /// <param name="Group">The filter the group belongs to.</param>
    /// <param name="Scope">Active or Archived.</param>
    /// <param name="Heading">The heading text, as spec §5.7 names it.</param>
    private sealed record SearchSlot(SearchFilter Group, SearchScope Scope, string Heading);
}
