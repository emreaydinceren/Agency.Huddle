using System.Globalization;

namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>Words shared by the confirms (design-system "Confirming"): the one quoting style, so the shared confirm and <c>InlineConfirm</c> read alike.</summary>
internal static class ConfirmCopy
{
    /// <summary>Wraps a name in double quotes, the only quote style in copy: <c>Delete "Notes"?</c>, never <c>'Notes'</c>.</summary>
    /// <param name="name">The item's name.</param>
    /// <returns>The name in double quotes.</returns>
    public static string Quote(string name) => string.Create(CultureInfo.InvariantCulture, $"\"{name}\"");

    /// <summary>The title of a confirm that deletes <paramref name="name"/>: <c>Delete "Notes"?</c>.</summary>
    /// <param name="name">The item's name.</param>
    /// <returns>A question ending in "?".</returns>
    public static string DeleteTitle(string name) => string.Create(CultureInfo.InvariantCulture, $"Delete {Quote(name)}?");
}
