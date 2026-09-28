namespace Agency.Huddle.App.Library;

/// <summary>
/// Sends a file or folder to the OS recycle bin (Spec §6.4 <c>RecycleAsync</c>, §10 E-8).
/// Availability is per path (a network drive, or a non-Windows host, may refuse some paths and not
/// others), so there is no separate "is available" check: a caller always attempts
/// <see cref="TrySend"/> and reads its error from the same call (corrections-B4 item 29).
/// </summary>
internal interface IRecycleBin
{
    /// <summary>
    /// Attempts to send <paramref name="fullPath"/> to the recycle bin.
    /// </summary>
    /// <param name="fullPath">The absolute path of the file or folder to recycle.</param>
    /// <param name="error">The refusal reason, when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the item was sent to the recycle bin.</returns>
    bool TrySend(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error);
}
