namespace Agency.Huddle.App.Search;

/// <summary>The kinds of notice a group can show under its cards.</summary>
public enum SearchNoticeKind
{
    /// <summary>The provider threw: <c>Couldn't search &lt;Group&gt;.</c></summary>
    Failed,
    /// <summary>The provider cut the search short: <c>Results may be incomplete.</c></summary>
    Truncated,
    /// <summary>One filter, no hit: <c>Nothing in &lt;Group&gt; matches '&lt;term&gt;'.</c></summary>
    NoMatch,
}
