using System.Globalization;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The words a context meter shows, so the Members panel and the transcript say the same thing about
/// one <see cref="ContextFillReading"/>.
/// </summary>
internal static class ContextFillText
{
    /// <summary>The meter's text: the whole percent, "&lt;1%" for a tiny non-zero fill, or an em dash when unknown.</summary>
    /// <param name="reading">The reading, or <see langword="null"/> when none was reported.</param>
    public static string Label(ContextFillReading? reading) => reading?.Percent switch
    {
        null => "—",
        0 when reading.Used > 0 => "<1%",
        { } percent => string.Create(CultureInfo.InvariantCulture, $"{percent}%"),
    };

    /// <summary>The meter's tooltip: the token counts and whether the reading is this Room's or shared.</summary>
    /// <param name="reading">The reading, or <see langword="null"/> when none was reported.</param>
    /// <param name="agentName">The Agent's Name, used for a shared-mode reading.</param>
    public static string Tooltip(ContextFillReading? reading, string agentName)
    {
        if (reading?.Percent is null)
        {
            return "No context usage reported yet";
        }

        string used = reading.Used.ToString("N0", CultureInfo.InvariantCulture);
        string size = reading.Size.ToString("N0", CultureInfo.InvariantCulture);
        string text = reading.Shared
            ? $"{used} of {size} tokens used, shared across all of {agentName}'s Rooms"
            : $"{used} of {size} tokens used in this Room";
        return string.IsNullOrWhiteSpace(reading.Model)
            ? text
            : text + string.Create(CultureInfo.InvariantCulture, $" Answered by {reading.Model}.");
    }
}
