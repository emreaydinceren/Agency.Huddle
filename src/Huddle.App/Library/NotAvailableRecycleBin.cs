namespace Agency.Huddle.App.Library;

/// <summary>
/// Placeholder <see cref="IRecycleBin"/> registered everywhere <see cref="WindowsRecycleBin"/> isn't
/// (Task 6.9.i): every call refuses with the same settled text (Spec §10 E-8), so nothing is ever
/// silently, permanently deleted where no recycle bin implementation exists.
/// </summary>
internal sealed class NotAvailableRecycleBin : IRecycleBin
{
    /// <summary>Always refuses: no recycle bin is available on this host.</summary>
    /// <param name="fullPath">The path this placeholder never sends anywhere.</param>
    /// <param name="error">Always <see cref="WindowsRecycleBin.UnavailableReason"/>.</param>
    /// <returns><see langword="false"/>, always.</returns>
    public bool TrySend(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        error = WindowsRecycleBin.UnavailableReason;
        return false;
    }
}
