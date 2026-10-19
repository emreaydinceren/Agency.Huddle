namespace Agency.Huddle.App.SkillSources;

/// <summary>One folder a watcher covers.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Recursive">Whether sub-folders count.</param>
internal sealed record WatchRoot(string Path, bool Recursive);
