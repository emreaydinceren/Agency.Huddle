namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The valid ranges and refusal texts of the Session settings (LR-R13, Spec §3.3). A pure function
/// per setting: a valid value yields <see langword="null"/>, an invalid one yields the refusal text
/// the Settings tab shows beside the field.
/// </summary>
internal static class SessionSettingsRules
{
    /// <summary>The smallest accepted Message cap.</summary>
    public const int MinMessages = 1;

    /// <summary>The largest accepted Message cap.</summary>
    public const int MaxMessages = 2000;

    /// <summary>The smallest accepted token budget.</summary>
    public const int MinTokens = 1000;

    /// <summary>The largest accepted token budget.</summary>
    public const int MaxTokens = 150_000;

    private const string MessagesRefusal = "Enter a number from 1 to 2,000.";
    private const string TokensRefusal = "Enter a number from 1,000 to 150,000.";

    /// <summary>
    /// Checks a Message cap against <see cref="MinMessages"/> and <see cref="MaxMessages"/> (LR-R13).
    /// </summary>
    /// <param name="value">The Message cap the user entered.</param>
    /// <returns><see langword="null"/> when the value is in range; otherwise the refusal text T6.</returns>
    public static string? CheckMessages(int value)
    {
        return value >= MinMessages && value <= MaxMessages ? null : MessagesRefusal;
    }

    /// <summary>
    /// Checks a token budget against <see cref="MinTokens"/> and <see cref="MaxTokens"/> (LR-R13).
    /// </summary>
    /// <param name="value">The token budget the user entered.</param>
    /// <returns><see langword="null"/> when the value is in range; otherwise the refusal text T7.</returns>
    public static string? CheckTokens(int value)
    {
        return value >= MinTokens && value <= MaxTokens ? null : TokensRefusal;
    }
}
