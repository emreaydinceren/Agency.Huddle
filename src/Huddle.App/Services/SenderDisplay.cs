using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The name to show for a Message's sender. A Transcript line keeps the <c>senderName</c> it was posted
/// with and is never rewritten, so after the Human renames themselves their older Messages still carry
/// the old Name; matching the fixed <see cref="KnownIds.Human"/> sender id shows the current Name on
/// every one of them instead.
/// </summary>
internal static class SenderDisplay
{
    /// <summary>The Human's current Name for a Message the Human sent; otherwise the stored sender name.</summary>
    /// <param name="message">The Message.</param>
    /// <param name="currentHumanName">The Human's current Name, or <see langword="null"/> when unknown, which keeps the stored name.</param>
    public static string NameOf(ChatMessage message, string? currentHumanName)
    {
        ArgumentNullException.ThrowIfNull(message);
        return currentHumanName is not null && string.Equals(message.SenderId, KnownIds.Human, StringComparison.Ordinal)
            ? currentHumanName
            : message.SenderName;
    }

    /// <summary><paramref name="message"/> with its sender name replaced by <see cref="NameOf"/>, or the same instance when nothing changes.</summary>
    /// <param name="message">The Message.</param>
    /// <param name="currentHumanName">The Human's current Name, or <see langword="null"/> when unknown.</param>
    public static ChatMessage WithCurrentName(ChatMessage message, string? currentHumanName)
    {
        var name = NameOf(message, currentHumanName);
        return string.Equals(name, message.SenderName, StringComparison.Ordinal) ? message : message with { SenderName = name };
    }
}
