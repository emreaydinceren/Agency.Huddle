namespace Agency.Huddle.App.Library;

/// <summary>A result of a Library operation: either a value or an error message.</summary>
/// <typeparam name="T">The value type on success (must be a reference type).</typeparam>
/// <param name="Value">The value on success, or null on failure.</param>
/// <param name="Error">The error message on failure, or null on success.</param>
internal sealed record LibraryResult<T>(T? Value, string? Error) where T : class
{
    /// <summary>Whether the result succeeded (Error is null).</summary>
    public bool Succeeded => this.Error is null;

    /// <summary>Create a successful result.</summary>
    /// <param name="value">The result value.</param>
    public static LibraryResult<T> Ok(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LibraryResult<T>(value, null);
    }

    /// <summary>Create a failed result.</summary>
    /// <param name="error">The error message.</param>
    public static LibraryResult<T> Fail(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);
        return new LibraryResult<T>(null, error);
    }
}
