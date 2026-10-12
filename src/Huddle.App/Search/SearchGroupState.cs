namespace Agency.Huddle.App.Search;

/// <summary>What one group shows once its provider has finished: a ranked batch, or a failure (spec §7.3).</summary>
public abstract record SearchGroupState;

/// <summary>A provider finished; <see cref="Result"/> is its batch.</summary>
/// <param name="Result">The provider's batch, before ranking.</param>
public sealed record SearchGroupDone(SearchResult Result) : SearchGroupState;

/// <summary>A provider threw; only its own group shows the failure (spec §7.3, D-20).</summary>
public sealed record SearchGroupFailed : SearchGroupState;

/// <summary>One group's update for one search. The bar drops an update whose <see cref="SearchId"/> is not the current search.</summary>
/// <param name="SearchId">The search this update answers.</param>
/// <param name="Group">The group the update fills.</param>
/// <param name="Scope">Active or Archived.</param>
/// <param name="State">The group's state.</param>
public sealed record SearchGroupUpdate(long SearchId, SearchFilter Group, SearchScope Scope, SearchGroupState State);
