namespace Agency.Huddle.App.TeamTypes;

/// <summary>The outcome of saving a user candidate.</summary>
/// <param name="Success">Whether the candidate file and manifest were written.</param>
/// <param name="Error">Why nothing was saved; <see langword="null"/> on success.</param>
internal sealed record WriteResult(bool Success, string? Error)
{
    /// <summary>The result of a save that wrote its files.</summary>
    public static WriteResult Saved { get; } = new(true, null);

    /// <summary>Creates the result of a refused or failed save.</summary>
    /// <param name="error">The reason shown to the user.</param>
    /// <returns>A failed result.</returns>
    public static WriteResult Refused(string error)
    {
        return new(false, error);
    }
}
