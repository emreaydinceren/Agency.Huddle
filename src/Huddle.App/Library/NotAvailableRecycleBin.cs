namespace Agency.Huddle.App.Library;

/// <summary>
/// Placeholder <see cref="IRecycleBin"/> registered until Task 6.8.i replaces it with the real
/// Windows implementation: every call refuses, so nothing is ever silently, permanently deleted in
/// the meantime.
/// </summary>
internal sealed class NotAvailableRecycleBin : IRecycleBin
{
    /// <summary>Always refuses: the recycle bin has no implementation yet.</summary>
    /// <param name="fullPath">The path this placeholder never sends anywhere.</param>
    /// <param name="error">Always a fixed refusal reason.</param>
    /// <returns><see langword="false"/>, always.</returns>
    public bool TrySend(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        error = "The recycle bin isn't available yet.";
        return false;
    }
}
