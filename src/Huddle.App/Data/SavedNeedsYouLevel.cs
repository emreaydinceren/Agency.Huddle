using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Data;

/// <summary>The global level and cap Settings saved (<see cref="NeedsYouLevelStore"/>).</summary>
/// <param name="Level">The saved level.</param>
/// <param name="SpendCap">The saved cap in USD, or <see langword="null"/> for none.</param>
internal sealed record SavedNeedsYouLevel(NeedsYouLevel Level, decimal? SpendCap);
