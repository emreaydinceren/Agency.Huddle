namespace Agency.Huddle.App.Search;

/// <summary>A provider finished; <see cref="Result"/> is its batch.</summary>
/// <param name="Result">The provider's batch, before ranking.</param>
public sealed record SearchGroupDone(SearchResult Result) : SearchGroupState;
