using System.Globalization;

namespace Agency.Huddle.App.Components.Settings.Helpers;

/// <summary>Every user-facing string of the Memory Tidy card section (Memory Tidy spec §6.16), in one place.</summary>
internal static class MemoryTidyCardCopy
{
    /// <summary>The Tidy now button.</summary>
    public const string TidyNow = "Tidy now";

    /// <summary>The Tidy now button's text, and the notice, while a pass is running.</summary>
    public const string PassRunning = "A pass is running";

    /// <summary>The link to the newest list.</summary>
    public const string OpenList = "Open the last list";

    /// <summary>The separator between the next and the last pass.</summary>
    private const string Separator = " · ";

    /// <summary>The next pass: <c>Next pass: Sunday 03:00</c>.</summary>
    /// <param name="next">The next run, in the Human's local time.</param>
    public static string NextPass(DateTimeOffset next) => string.Create(CultureInfo.InvariantCulture, $"Next pass: {next:dddd HH:mm}");

    /// <summary>The last pass: <c>Last pass: 22 Nov, tidied 4, dropped 2</c>.</summary>
    /// <param name="at">When the pass started, in the Human's local time.</param>
    /// <param name="tidied">Topic files tidied.</param>
    /// <param name="dropped">Topic files dropped.</param>
    public static string LastPass(DateTimeOffset at, int tidied, int dropped) =>
        string.Create(CultureInfo.InvariantCulture, $"Last pass: {at:d MMM}, tidied {tidied}, dropped {dropped}");

    /// <summary>Joins the parts that are known into the one line.</summary>
    /// <param name="parts">The next and last pass texts, each present only when known.</param>
    public static string Line(IEnumerable<string> parts) => string.Join(Separator, parts);
}
