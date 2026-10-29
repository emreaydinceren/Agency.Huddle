using Agency.Huddle.App.References;
using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// Pins <see cref="ReferenceQueryParser"/> (spec §5.1, §5.2, §5.5, D-3, D-4): the picker's token, read with
/// the search box's prefixes, its <c>#</c> alias of <c>@task:</c>, its searchable rule and the invite command.
/// </summary>
public sealed class ReferenceQueryParserTests
{
    /// <summary>A <c>#</c> token is the Tasks filter, and it always searches, even when empty.</summary>
    [Fact]
    public void Hash_IsTasks()
    {
        SearchParse parse = ReferenceQueryParser.Parse("fix", '#', false);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, "fix", true), parse);
    }

    /// <summary>An <c>@</c> with no prefix is the All filter, and it searches when the phrase is empty.</summary>
    [Fact]
    public void At_NoPrefix_IsAllAndSearchableWhenEmpty()
    {
        SearchParse parse = ReferenceQueryParser.Parse(string.Empty, '@', false);

        Assert.Equal(new SearchParse(SearchFilter.All, string.Empty, true), parse);
    }

    /// <summary>Each prefix sets its own filter and leaves the phrase after it.</summary>
    /// <param name="token">The token after the <c>@</c>.</param>
    /// <param name="filter">The filter the prefix sets.</param>
    /// <param name="phrase">The phrase after the prefix.</param>
    [Theory]
    [InlineData("agent:nova", SearchFilter.Agents, "nova")]
    [InlineData("task:fix", SearchFilter.Tasks, "fix")]
    [InlineData("room:launch", SearchFilter.Rooms, "launch")]
    [InlineData("team:market", SearchFilter.Teams, "market")]
    [InlineData("lib:spec", SearchFilter.Library, "spec")]
    public void At_EachPrefix_SetsItsFilter(string token, SearchFilter filter, string phrase)
    {
        SearchParse parse = ReferenceQueryParser.Parse(token, '@', false);

        Assert.Equal(new SearchParse(filter, phrase, true), parse);
    }

    /// <summary>A Library phrase under three characters does not search, but the same phrase under All does.</summary>
    [Fact]
    public void At_LibraryUnderThree_NotSearchable()
    {
        SearchParse library = ReferenceQueryParser.Parse("lib:ab", '@', false);
        SearchParse all = ReferenceQueryParser.Parse("ab", '@', false);

        Assert.Equal(new SearchParse(SearchFilter.Library, "ab", false), library);
        Assert.Equal(new SearchParse(SearchFilter.All, "ab", true), all);
    }

    /// <summary>A <c>task:</c> prefix with nothing after it still searches, because Tasks browse when empty.</summary>
    [Fact]
    public void At_TasksEmpty_Searchable()
    {
        SearchParse parse = ReferenceQueryParser.Parse("task:", '@', false);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, string.Empty, true), parse);
    }

    /// <summary>After <c>/invite </c>, the picker lists Agents whatever the prefix typed.</summary>
    [Fact]
    public void InviteCommand_PinsAgents()
    {
        SearchParse parse = ReferenceQueryParser.Parse("task:nova", '@', true);

        Assert.Equal(new SearchParse(SearchFilter.Agents, "nova", true), parse);
    }

    /// <summary>The prefix is read whatever its case, as the search box reads it.</summary>
    [Fact]
    public void At_PrefixIgnoresCase()
    {
        SearchParse parse = ReferenceQueryParser.Parse("TASK:fix", '@', false);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, "fix", true), parse);
    }
}
