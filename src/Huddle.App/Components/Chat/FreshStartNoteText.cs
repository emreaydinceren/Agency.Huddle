using System.Globalization;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Components.Chat;

/// <summary>The one-line note that says an Agent started a Room's session afresh (Spec LR-R21; texts T12 to T14).</summary>
internal static class FreshStartNoteText
{
    /// <summary>Builds the fresh-start note for an Agent. A failed read wins over the counts (T14); no Messages reread gives T13; otherwise the count and tokens are stated (T12, singular for one Message).</summary>
    /// <param name="agentName">The Agent's display name, placed at the start of the note.</param>
    /// <param name="note">The runner's note about the fresh start.</param>
    /// <returns>The note text, ready to show in the Room.</returns>
    internal static string Format(string agentName, FreshStartNote note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(note);

        if (note.ReadFailed)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{agentName} started afresh, as you asked. It could not reread the Room.");
        }

        if (note.MessageCount == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{agentName} started afresh, as you asked. There were no earlier Messages to reread.");
        }

        string tokens = note.EstimatedTokens.ToString("N0", CultureInfo.InvariantCulture);
        if (note.MessageCount == 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{agentName} started afresh, as you asked. It reread the last Message (about {tokens} tokens).");
        }

        string count = note.MessageCount.ToString("N0", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{agentName} started afresh, as you asked. It reread the last {count} Messages (about {tokens} tokens).");
    }
}
