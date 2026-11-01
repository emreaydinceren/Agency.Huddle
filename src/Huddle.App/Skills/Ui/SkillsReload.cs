using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.SkillSources;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>The one Reload routine behind both the Skills tab's and the Pack bar's Reload button: re-read everything, count
/// what is broken, and word the snackbar. Two copies of this once drifted apart, so they share it.</summary>
internal static class SkillsReload
{
    /// <summary>The snackbar key, so a second Reload replaces the first message rather than stacking.</summary>
    internal const string SnackbarKey = "skills-reload";

    /// <summary>The snackbar text for a Reload that found <paramref name="problems"/> problems.</summary>
    /// <param name="problems">What <see cref="RunAsync"/> returned.</param>
    /// <returns>"Reloaded. No problems." or "Reloaded. N problem(s) found.".</returns>
    internal static string Message(int problems)
    {
        return problems == 0 ? "Reloaded. No problems." : $"Reloaded. {Plural.Of(problems, "problem", "problems")} found.";
    }

    /// <summary>Re-reads everything now, then counts the problems the way the Settings menu badge does.</summary>
    /// <param name="registry">The registry to reload.</param>
    /// <param name="store">The Skill store whose issues are counted.</param>
    /// <param name="cancellationToken">Cancels the reload.</param>
    /// <returns>The <see cref="SkillProblemCount.Of"/> total after the reload.</returns>
    internal static async Task<int> RunAsync(SkillRegistry registry, SkillStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);
        await registry.ReloadAsync(cancellationToken).ConfigureAwait(false);
        return SkillProblemCount.Of(registry, store);
    }
}
