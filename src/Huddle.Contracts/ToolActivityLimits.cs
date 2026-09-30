namespace Agency.Huddle.Contracts;

/// <summary>
/// The size limits on the display-only members of <see cref="ToolActivity"/>, shared by the runner
/// that clips before sending and the server that clips again on receipt, so both use the same numbers.
/// </summary>
public static class ToolActivityLimits
{
    /// <summary>The most characters kept of each side of an <see cref="EditChange"/>.</summary>
    public const int MaxEditSideLength = 4096;

    /// <summary>The most characters a <see cref="ToolActivity.Path"/> may have; a longer one is dropped.</summary>
    public const int MaxPathLength = 1024;

    /// <summary>The most characters kept of a <see cref="ToolActivity.Title"/>.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>
    /// Cuts <paramref name="text"/> to at most <paramref name="maxLength"/> characters without
    /// splitting a surrogate pair, so the result is always valid text.
    /// </summary>
    /// <param name="text">The text to clip, or <see langword="null"/>.</param>
    /// <param name="maxLength">The most characters to keep.</param>
    /// <param name="clipped">Whether any text was cut.</param>
    /// <returns>The clipped text, or <see langword="null"/> when <paramref name="text"/> is.</returns>
    public static string? Clip(string? text, int maxLength, out bool clipped)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (text is null || text.Length <= maxLength)
        {
            clipped = false;
            return text;
        }

        int end = maxLength;
        if (end > 0 && char.IsHighSurrogate(text[end - 1]))
        {
            end--;
        }

        clipped = true;
        return text[..end];
    }
}
