using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.References;

/// <summary>
/// Reads one picker token (spec §5.1, §5.2, §5.5, D-3, D-4): the text after an <c>@</c> or a <c>#</c>, with the
/// search box's prefixes. <c>#</c> is the alias of <c>@task:</c>. Pure string work, no I/O.
/// </summary>
internal static class ReferenceQueryParser
{
    /// <summary>Reads one picker token into the filter, phrase and searchable flag the Reference purpose searches with.</summary>
    /// <param name="token">The text after the trigger, without the trigger.</param>
    /// <param name="trigger">The trigger character: <c>'@'</c> or <c>'#'</c>.</param>
    /// <param name="inviteCommand">True after <c>/invite </c> at the start of the box; the picker then lists Agents only (D-23).</param>
    /// <returns>The filter, the trimmed phrase, and whether the picker searches.</returns>
    public static SearchParse Parse(string token, char trigger, bool inviteCommand)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (trigger == '#')
        {
            return new SearchParse(SearchFilter.Tasks, token.Trim(), true);
        }

        SearchParse read = SearchQueryParser.Parse(token, SearchFilter.All);
        SearchFilter filter = inviteCommand ? SearchFilter.Agents : read.Filter;

        // Every filter searches at any length except Library, which needs the search box's minimum (spec §5.1).
        bool searchable = filter != SearchFilter.Library || read.Phrase.Length >= SearchLimits.MinChars;
        return new SearchParse(filter, read.Phrase, searchable);
    }
}
