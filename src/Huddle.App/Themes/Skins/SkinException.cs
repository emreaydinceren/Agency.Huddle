namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// A skin value or file that cannot be used: a bad expression, an unknown token, a reference cycle. The message names
/// the token, the value and the skin, because a skin is authored by hand and the author needs the line to fix.
/// </summary>
public sealed class SkinException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public SkinException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    /// <param name="message">What is wrong, naming the token, value and skin.</param>
    public SkinException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and the exception that caused it.</summary>
    /// <param name="message">What is wrong, naming the token, value and skin.</param>
    /// <param name="innerException">The underlying failure.</param>
    public SkinException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
