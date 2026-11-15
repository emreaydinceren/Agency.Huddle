using System.Globalization;

namespace Agency.Huddle.App.Teams;

/// <summary>The wire names, display names and spend-cap text of the Needs You level (ADR-0088), shared by <c>_team.md</c>, <c>_project.md</c>, <c>team.db</c> and the configuration.</summary>
internal static class NeedsYouLevelText
{
    /// <summary>The wire name of <see cref="NeedsYouLevel.AskMe"/>.</summary>
    public const string AskMeWire = "ask-me";

    /// <summary>The wire name of <see cref="NeedsYouLevel.RunInsideTheTeam"/>.</summary>
    public const string RunInsideTheTeamWire = "run-inside-team";

    /// <summary>The frontmatter key that holds a scope's level in <c>_team.md</c> and <c>_project.md</c>.</summary>
    public const string LevelKey = "needsYouLevel";

    /// <summary>The frontmatter key that holds a scope's spend cap in <c>_team.md</c> and <c>_project.md</c>.</summary>
    public const string SpendCapKey = "spendCap";

    /// <summary>What <c>propose_retro_changes</c> answers, and what an Accept logs, when a retro change would touch the level or the cap.</summary>
    public const string RetroRefusal = "Only the Human sets the Needs You level and its spend cap; propose_retro_changes cannot change them.";

    /// <summary>Whether the text names either frontmatter key, ignoring case.</summary>
    /// <param name="text">Text a tool call or a retro change carries.</param>
    /// <returns><see langword="true"/> when it names <c>needsYouLevel</c> or <c>spendCap</c>.</returns>
    public static bool MentionsKey(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains(LevelKey, StringComparison.OrdinalIgnoreCase) || text.Contains(SpendCapKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The name a level is stored under.</summary>
    /// <param name="level">The level.</param>
    /// <returns><c>ask-me</c> or <c>run-inside-team</c>.</returns>
    public static string ToWire(this NeedsYouLevel level) => level switch
    {
        NeedsYouLevel.AskMe => AskMeWire,
        NeedsYouLevel.RunInsideTheTeam => RunInsideTheTeamWire,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>The name the Human reads.</summary>
    /// <param name="level">The level.</param>
    /// <returns><c>Ask me</c> or <c>Run inside the Team</c>.</returns>
    public static string ToDisplay(this NeedsYouLevel level) => level switch
    {
        NeedsYouLevel.AskMe => "Ask me",
        NeedsYouLevel.RunInsideTheTeam => "Run inside the Team",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>The scope's name as a sentence uses it: "proceeded under project level Run inside the Team".</summary>
    /// <param name="scope">The scope.</param>
    /// <returns><c>global</c>, <c>Team</c> or <c>project</c>.</returns>
    public static string ToDisplay(this NeedsYouScope scope) => scope switch
    {
        NeedsYouScope.Global => "global",
        NeedsYouScope.Team => "Team",
        NeedsYouScope.Project => "project",
        _ => throw new ArgumentOutOfRangeException(nameof(scope)),
    };

    /// <summary>Reads a wire name, ignoring case and surrounding spaces.</summary>
    /// <param name="text">The stored text, or <see langword="null"/>.</param>
    /// <param name="level">The level when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> for blank or unknown text, which reads as unset.</returns>
    public static bool TryParse(string? text, out NeedsYouLevel level)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        if (string.Equals(trimmed, AskMeWire, StringComparison.OrdinalIgnoreCase))
        {
            level = NeedsYouLevel.AskMe;
            return true;
        }

        if (string.Equals(trimmed, RunInsideTheTeamWire, StringComparison.OrdinalIgnoreCase))
        {
            level = NeedsYouLevel.RunInsideTheTeam;
            return true;
        }

        level = default;
        return false;
    }

    /// <summary>Reads a stored level, <see langword="null"/> when it is blank or unknown.</summary>
    /// <param name="text">The stored text, or <see langword="null"/>.</param>
    /// <returns>The level, or <see langword="null"/> for unset.</returns>
    public static NeedsYouLevel? ParseOrNull(string? text) => TryParse(text, out NeedsYouLevel level) ? level : null;

    /// <summary>Reads a stored spend cap: a plain invariant-culture decimal above zero.</summary>
    /// <param name="text">The stored text, or <see langword="null"/>.</param>
    /// <returns>The cap in USD, or <see langword="null"/> for blank, malformed, zero or negative text.</returns>
    public static decimal? ParseCapOrNull(string? text) =>
        decimal.TryParse(text?.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal cap) && cap > 0 ? cap : null;

    /// <summary>The text a cap is stored under.</summary>
    /// <param name="cap">The cap in USD.</param>
    /// <returns>The invariant-culture text.</returns>
    public static string FormatCap(decimal cap) => cap.ToString(CultureInfo.InvariantCulture);

    /// <summary>A cap as it may be stored: above zero, else none.</summary>
    /// <param name="cap">The cap, or <see langword="null"/>.</param>
    /// <returns>The cap when it is above zero, else <see langword="null"/>.</returns>
    public static decimal? Usable(decimal? cap) => cap > 0 ? cap : null;
}
