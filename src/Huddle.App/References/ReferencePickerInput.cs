using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.References;

/// <summary>The state the picker is built from: the parsed trigger and token, the group states and the Room's Members (spec §5.3).</summary>
/// <param name="Trigger">The trigger character, <c>@</c> or <c>#</c>.</param>
/// <param name="Token">The text after the trigger.</param>
/// <param name="Filter">The filter the box is set to.</param>
/// <param name="Groups">The state of each group and side, keyed by filter and scope. A missing key is an update that has not arrived.</param>
/// <param name="MemberNames">The names of the Room's Members, compared ignoring case.</param>
/// <param name="LibraryRootPaths">The full path of each Library Root, keyed by root id.</param>
/// <param name="InviteCommand">Whether the box starts with <c>/invite </c>, which shows the Teammates group only.</param>
/// <param name="ListboxId">The element id of the list, which the row ids extend.</param>
/// <param name="Highlight">The index the highlight was last set to.</param>
internal sealed record ReferencePickerInput(
    char Trigger,
    string Token,
    SearchFilter Filter,
    IReadOnlyDictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> Groups,
    IReadOnlySet<string> MemberNames,
    IReadOnlyDictionary<string, string> LibraryRootPaths,
    bool InviteCommand,
    string ListboxId,
    int Highlight);
