namespace Agency.Huddle.App.SkillSources;

/// <summary>What to watch and what to poll.</summary>
/// <param name="Watched">At most <see cref="WatchRoots.MaxWatchers"/> roots with a <see cref="FileSystemWatcher"/>.</param>
/// <param name="Polled">Roots polled every 30 seconds: missing paths, network paths and roots past the cap.</param>
internal sealed record WatchPlan(IReadOnlyList<WatchRoot> Watched, IReadOnlyList<WatchRoot> Polled);
