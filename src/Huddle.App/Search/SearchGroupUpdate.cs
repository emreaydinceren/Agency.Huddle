namespace Agency.Huddle.App.Search;

/// <summary>One group's update for one search. The bar drops an update whose <see cref="SearchId"/> is not the current search.</summary>
/// <param name="SearchId">The search this update answers.</param>
/// <param name="Group">The group the update fills.</param>
/// <param name="Scope">Active or Archived.</param>
/// <param name="State">The group's state.</param>
public sealed record SearchGroupUpdate(long SearchId, SearchFilter Group, SearchScope Scope, SearchGroupState State);
