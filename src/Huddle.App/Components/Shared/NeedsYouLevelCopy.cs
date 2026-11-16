using System.Globalization;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>Every word of the Needs You level controls (ADR-0088) in one place, so the three places that set a level, and the cap question, say the same thing.</summary>
internal static class NeedsYouLevelCopy
{
    /// <summary>What Ask me means, under the choice.</summary>
    public const string AskMeNote = "Ask me: everything that waits for your Accept keeps waiting.";

    /// <summary>What Run inside the Team means, under the choice. It names what still waits, so it promises no more than the code does.</summary>
    public const string RunInsideNote =
        "Run inside the Team: a local merge into main of a Task's own branch once its Proof is complete, a Sprint and new Backlog Tasks go ahead without you. "
        + "Every push, every Task with an Action (send, pay, publish, sign, delete) and every connector call still wait for you.";

    /// <summary>The cap field's explanation.</summary>
    public const string CapNote = "When the Team has spent this much running inside the Team, it asks you before going on.";

    /// <summary>The refusal shown under a cap that is not a number above zero.</summary>
    public const string CapInvalid = "Enter an amount above 0, such as 5 or 2.50, or leave it empty for no cap.";

    /// <summary>The label of the Yes button of the cap question, for a cap.</summary>
    /// <param name="cap">The cap in USD.</param>
    public static string GrantLabel(decimal cap) => $"Let it spend another {Money(cap)} USD";

    /// <summary>The label of the No button of the cap question.</summary>
    public const string DeclineLabel = "No, ask me for each step";

    /// <summary>An amount of USD as the Human reads it: no trailing zeros, two decimals at most.</summary>
    /// <param name="amount">The amount.</param>
    public static string Money(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The cap question's text.</summary>
    /// <param name="scopeName">The scope as <c>NeedsYouCaps</c> names it: Huddle, a Team, or Team / Project.</param>
    /// <param name="spent">What the scope spent running inside the Team, in USD.</param>
    /// <param name="cap">The cap in USD.</param>
    public static string AskText(string scopeName, decimal spent, decimal cap) =>
        $"{scopeName} has spent {Money(spent)} USD running inside the Team. Let it spend another {Money(cap)} USD? Until you answer it asks you for each step.";

    /// <summary>The line that says what level is in force for a scope and where it comes from.</summary>
    /// <param name="effective">The level in force.</param>
    /// <param name="own">Whether the scope shown is the scope that supplied the level.</param>
    /// <param name="scopeLabel">What the scope shown is called: "the Team" or "the project", for the case where the cap is reached on it.</param>
    public static string InForce(EffectiveLevel effective, bool own, string scopeLabel)
    {
        ArgumentNullException.ThrowIfNull(effective);
        if (effective.CapReached)
        {
            string where = effective.Scope == NeedsYouScope.Global ? "Settings" : effective.ScopeName ?? scopeLabel;
            return $"Cap reached: asking you. {where} ran inside the Team up to its cap, so it asks you as Ask me does until you answer or change the cap.";
        }

        string name = effective.Level.ToDisplay();
        if (own)
        {
            return $"In force: {name}.";
        }

        return effective.Scope switch
        {
            NeedsYouScope.Team => $"Inherits {name} from the Team.",
            _ => $"Inherits {name} from Settings.",
        };
    }
}
