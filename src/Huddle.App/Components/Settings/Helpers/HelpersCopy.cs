using System.Globalization;

namespace Agency.Huddle.App.Components.Settings.Helpers;

/// <summary>Every user-facing string of Settings › Helpers and the Task panel's Helper line, in one place (Helpers Spec §7).</summary>
internal static class HelpersCopy
{
    /// <summary>The panel intro.</summary>
    public const string Intro = "Helpers are small background agents that make one judgement and let Huddle act on it. They don't chat, keep memory or use tools.";

    /// <summary>The notice shown, with every card read only, when Adapters are off.</summary>
    public const string AdaptersOff = "Helpers run on an Adapter, and Adapters are off in this installation.";

    /// <summary>The chip on a running Helper.</summary>
    public const string ChipOn = "On";

    /// <summary>The chip on a Helper the Human turned off.</summary>
    public const string ChipOff = "Off";

    /// <summary>The chip on a Helper that reached its daily cap.</summary>
    public const string ChipPaused = "Paused";

    /// <summary>The label of the on switch.</summary>
    public const string SwitchLabel = "On";

    /// <summary>The label of the daily cap field.</summary>
    public const string CapLabel = "Daily cap";

    /// <summary>The helper text under the daily cap field.</summary>
    public const string CapHelper = "Calls a day before it pauses and asks you.";

    /// <summary>The link to the Helper's prompt.</summary>
    public const string PromptLink = "Edit the prompt";

    /// <summary>The warning shown on a paused Helper.</summary>
    public const string Paused = "Paused — daily cap reached";

    /// <summary>The Save button.</summary>
    public const string Save = "Save";

    /// <summary>The Reset to defaults button.</summary>
    public const string Reset = "Reset to defaults";

    /// <summary>The Task panel line's lead, before the Helper's reason.</summary>
    public const string TaskPanelLead = "Turned on by the";

    /// <summary>Today's use: <c>12 of 50 calls today</c>.</summary>
    /// <param name="calls">Calls made today.</param>
    /// <param name="cap">The cap, with today's grant.</param>
    public static string Usage(int calls, int cap) => string.Create(CultureInfo.InvariantCulture, $"{calls} of {cap} calls today");

    /// <summary>The skipped count: <c>3 skipped while paused</c>.</summary>
    /// <param name="skipped">Inputs dropped today while paused.</param>
    public static string Skipped(int skipped) => string.Create(CultureInfo.InvariantCulture, $"{skipped} skipped while paused");

    /// <summary>The cost line: <c>0.02 USD</c>.</summary>
    /// <param name="cost">The summed cost.</param>
    /// <param name="currency">Its currency.</param>
    public static string Cost(decimal cost, string? currency) => string.Create(CultureInfo.InvariantCulture, $"{cost:0.####} {currency}").TrimEnd();

    /// <summary>The last run: <c>Last run 10:42 · Turned the gate on for RSH-0208</c>.</summary>
    /// <param name="time">The local time of day.</param>
    /// <param name="result">What it did.</param>
    public static string LastRun(string time, string result) => $"Last run {time} · {result}";

    /// <summary>The last error: <c>Last error 10:40 · Timed out after 60 s</c>.</summary>
    /// <param name="time">The local time of day.</param>
    /// <param name="message">What went wrong.</param>
    public static string LastError(string time, string message) => $"Last error {time} · {message}";

    /// <summary>The Continue button: <c>Continue (+10 today)</c>.</summary>
    /// <param name="grant">Calls the button adds for today.</param>
    public static string Continue(int grant) => string.Create(CultureInfo.InvariantCulture, $"Continue (+{grant} today)");

    /// <summary>
    /// The Task panel line shown when a Helper turned the gate on. When the Task names an Action the gate is locked on, so the
    /// line says to decide the Task instead of offering to turn it off (#452). A reason's own closing full stop is not doubled.
    /// </summary>
    /// <param name="helperName">The Helper's display name, such as <c>Needs Human</c>.</param>
    /// <param name="reason">Its reason.</param>
    /// <param name="namedTheStep">Whether the Task names an Action, which locks the gate on.</param>
    public static string TaskPanel(string helperName, string reason, bool namedTheStep = false)
    {
        string trimmed = reason.Trim().TrimEnd('.');
        string lead = $"{TaskPanelLead} {helperName} Helper{(trimmed.Length == 0 ? string.Empty : ": " + trimmed)}.";
        return namedTheStep
            ? lead + " It also named the step, which keeps the gate on: accept or decline the Task to decide."
            : lead + " If you turn it off, the Helper won't ask about this Task again.";
    }

    /// <summary>The address of the Prompts tab with a prompt highlighted.</summary>
    /// <param name="promptKey">The prompt's key.</param>
    public static string PromptHref(string promptKey) => $"/settings/prompts?highlight={Uri.EscapeDataString(promptKey)}";
}
