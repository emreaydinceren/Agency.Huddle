namespace Agency.Huddle.App;

/// <summary>
/// The global Needs You level, bound from <c>Team:NeedsYouLevel</c> (ADR-0087). The value Settings saves in <c>team.db</c> wins over
/// these keys once it exists.
/// </summary>
public sealed class NeedsYouOptions
{
    /// <summary>The level as stored: <c>ask-me</c> (the default) or <c>run-inside-team</c>. An unknown value reads as <c>ask-me</c>.</summary>
    public string Level { get; set; } = "ask-me";

    /// <summary>The global spend cap in USD that travels with the level; <see langword="null"/> (the default) is no cap.</summary>
    public decimal? SpendCap { get; set; }
}
