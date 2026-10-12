namespace Agency.Huddle.App.Search;

/// <summary>Which side of a category a call searches: the live items, or Archived Rooms and Closed Tasks.</summary>
public enum SearchScope
{
    /// <summary>The live items: active Rooms, open Tasks, every Team, Teammate and Library file.</summary>
    Active,

    /// <summary>The archived side: Archived Rooms and Closed Tasks.</summary>
    Archived,
}
