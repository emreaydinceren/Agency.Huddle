using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchQueryParser"/>: the prefixes, the chip rewrite and the searchable rule (spec §6.2, §7.2; GS-T1).</summary>
public sealed class SearchQueryParserTests
{
    /// <summary>Each of the five prefixes sets its filter, ignores case, and is removed from the phrase.</summary>
    [Theory]
    [InlineData("team:nova", SearchFilter.Teams, "nova")]
    [InlineData("TEAMMATE:nova", SearchFilter.Teammates, "nova")]
    [InlineData("Lib:readme", SearchFilter.Library, "readme")]
    [InlineData("task:fix", SearchFilter.Tasks, "fix")]
    [InlineData("Room:kickoff", SearchFilter.Rooms, "kickoff")]
    public void Parse_EachPrefix_SetsItsFilter(string boxText, SearchFilter expectedFilter, string expectedPhrase)
    {
        SearchParse parse = SearchQueryParser.Parse(boxText, SearchFilter.All);

        Assert.Equal(new SearchParse(expectedFilter, expectedPhrase, true), parse);
    }

    /// <summary>The colon decides: <c>teammate:</c> sets Teammates and <c>team:</c> sets Teams.</summary>
    [Fact]
    public void Parse_TeamAndTeammate_AreKeptApart()
    {
        SearchParse teammate = SearchQueryParser.Parse("teammate:nova", SearchFilter.All);
        SearchParse team = SearchQueryParser.Parse("team:nova", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.Teammates, "nova", true), teammate);
        Assert.Equal(new SearchParse(SearchFilter.Teams, "nova", true), team);
    }

    /// <summary>A prefix that is not at the very start of the text is part of the phrase.</summary>
    [Fact]
    public void Parse_PrefixNotAtStart_IsPlainText()
    {
        SearchParse parse = SearchQueryParser.Parse(" team:nova", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.All, "team:nova", true), parse);
    }

    /// <summary>With no prefix, the chip's filter stands and the whole text is the phrase.</summary>
    [Fact]
    public void Parse_NoPrefix_ChipFilterStands()
    {
        SearchParse parse = SearchQueryParser.Parse("nova", SearchFilter.Teams);

        Assert.Equal(new SearchParse(SearchFilter.Teams, "nova", true), parse);
    }

    /// <summary>The phrase after the prefix is trimmed at both ends.</summary>
    [Fact]
    public void Parse_TrimsThePhrase()
    {
        SearchParse parse = SearchQueryParser.Parse("task:   login   ", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, "login", true), parse);
    }

    /// <summary>Two characters are not enough to search, even behind a prefix.</summary>
    [Fact]
    public void Parse_TaskFi_NotSearchable()
    {
        SearchParse parse = SearchQueryParser.Parse("task:fi", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, "fi", false), parse);
    }

    /// <summary>Three characters are enough to search.</summary>
    [Fact]
    public void Parse_TaskFix_Searchable()
    {
        SearchParse parse = SearchQueryParser.Parse("task:fix", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, "fix", true), parse);
    }

    /// <summary>An empty phrase under Teams or Teammates is a browse, whichever way the filter was set.</summary>
    [Theory]
    [InlineData("", SearchFilter.Teams, SearchFilter.Teams)]
    [InlineData("", SearchFilter.Teammates, SearchFilter.Teammates)]
    [InlineData("team:", SearchFilter.All, SearchFilter.Teams)]
    [InlineData("teammate:   ", SearchFilter.Rooms, SearchFilter.Teammates)]
    public void Parse_EmptyTeamsAndTeammates_Browse(string boxText, SearchFilter chip, SearchFilter expectedFilter)
    {
        SearchParse parse = SearchQueryParser.Parse(boxText, chip);

        Assert.Equal(new SearchParse(expectedFilter, string.Empty, true), parse);
    }

    /// <summary>An empty Tasks filter does not search: there is nothing to browse.</summary>
    [Fact]
    public void Parse_EmptyTasks_NotSearchable()
    {
        SearchParse parse = SearchQueryParser.Parse("", SearchFilter.Tasks);

        Assert.Equal(new SearchParse(SearchFilter.Tasks, string.Empty, false), parse);
    }

    /// <summary>An empty box under All, Library or Rooms is not searchable, only browsable under Teams and Teammates.</summary>
    [Theory]
    [InlineData(SearchFilter.All)]
    [InlineData(SearchFilter.Library)]
    [InlineData(SearchFilter.Rooms)]
    public void Parse_EmptyOtherFilters_NotSearchable(SearchFilter chip)
    {
        SearchParse parse = SearchQueryParser.Parse("", chip);

        Assert.Equal(new SearchParse(chip, string.Empty, false), parse);
    }

    /// <summary>A phrase starting with <c>&gt;</c> is reserved for V2 commands and never searches.</summary>
    [Fact]
    public void Parse_GreaterThan_NeverSearches()
    {
        SearchParse plain = SearchQueryParser.Parse(">explain the thing", SearchFilter.All);
        SearchParse prefixed = SearchQueryParser.Parse("task:>fixing", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.All, ">explain the thing", false), plain);
        Assert.Equal(new SearchParse(SearchFilter.Tasks, ">fixing", false), prefixed);
    }

    /// <summary>A question mark has no meaning in V1: three of them are an ordinary three-character phrase.</summary>
    [Fact]
    public void Parse_QuestionMarks_AreOrdinaryPhrase()
    {
        SearchParse parse = SearchQueryParser.Parse("???", SearchFilter.All);

        Assert.Equal(new SearchParse(SearchFilter.All, "???", true), parse);
    }

    /// <summary>Picking a chip replaces the prefix at the start and keeps the rest of the text.</summary>
    [Fact]
    public void ApplyChip_ReplacesPrefixKeepsText()
    {
        string text = SearchQueryParser.ApplyChip("task:login", SearchFilter.Rooms);

        Assert.Equal("room:login", text);
    }

    /// <summary>Picking All removes the prefix and keeps the phrase.</summary>
    [Fact]
    public void ApplyChip_All_RemovesPrefix()
    {
        string text = SearchQueryParser.ApplyChip("team:nova", SearchFilter.All);

        Assert.Equal("nova", text);
    }

    /// <summary>Picking All on text without a prefix leaves the text alone.</summary>
    [Fact]
    public void ApplyChip_AllWithoutPrefix_LeavesTextAlone()
    {
        string text = SearchQueryParser.ApplyChip("login", SearchFilter.All);

        Assert.Equal("login", text);
    }

    /// <summary>Picking a chip on text without a prefix adds one in front of the text.</summary>
    [Fact]
    public void ApplyChip_NoPrefix_AddsOne()
    {
        string text = SearchQueryParser.ApplyChip("login", SearchFilter.Teams);

        Assert.Equal("team:login", text);
    }

    /// <summary>Each filter maps to its prefix, and All maps to no prefix.</summary>
    [Theory]
    [InlineData(SearchFilter.All, "")]
    [InlineData(SearchFilter.Teams, "team:")]
    [InlineData(SearchFilter.Teammates, "teammate:")]
    [InlineData(SearchFilter.Library, "lib:")]
    [InlineData(SearchFilter.Tasks, "task:")]
    [InlineData(SearchFilter.Rooms, "room:")]
    public void PrefixFor_EachFilter_ReturnsItsPrefix(SearchFilter filter, string expected)
    {
        string prefix = SearchQueryParser.PrefixFor(filter);

        Assert.Equal(expected, prefix);
    }
}
