namespace Agency.Huddle.App.Search;

/// <summary>Which category the dropdown shows: All, or one filter. A chip and a prefix both set it.</summary>
public enum SearchFilter
{
    /// <summary>Every category, each capped at the group cap.</summary>
    All,

    /// <summary>Teams only.</summary>
    Teams,

    /// <summary>Agents only.</summary>
    Agents,

    /// <summary>Library files and folders only.</summary>
    Library,

    /// <summary>Tasks only.</summary>
    Tasks,

    /// <summary>Rooms only.</summary>
    Rooms,
}
