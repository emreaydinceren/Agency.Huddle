using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.Library;

/// <summary>One Library entry that matched a search, by name or by contents (Global Search spec §8.3).</summary>
/// <param name="Entry">The matching file or folder.</param>
/// <param name="Tier">How well it matched: a name tier, or <see cref="MatchTier.ContentMatch"/> when its text matched.</param>
/// <param name="Snippet">The first matching line, cut to a fixed width, for a content hit; otherwise <see langword="null"/>.</param>
internal sealed record LibraryTextHit(LibraryEntry Entry, MatchTier Tier, string? Snippet);
