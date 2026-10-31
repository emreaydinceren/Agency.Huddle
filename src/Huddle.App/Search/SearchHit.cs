namespace Agency.Huddle.App.Search;

/// <summary>One result. The card is chosen by the subtype, never by a kind enum (spec §7.1).</summary>
/// <param name="Title">The card's main text.</param>
/// <param name="TeamName">The Team the hit belongs to, used only for "open Team first" (spec §7.4).</param>
/// <param name="IsArchived">Whether the hit is an Archived Room or a Closed Task.</param>
/// <param name="Tier">How well the hit matched.</param>
public abstract record SearchHit(string Title, string? TeamName, bool IsArchived, MatchTier Tier);
