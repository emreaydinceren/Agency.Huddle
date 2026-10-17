namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// A skin file is invalid. The message names the file and the key at fault. Local to P1-B; the manager reconciles it with
/// P1-A's <c>SkinException</c> at merge.
/// </summary>
public sealed class SkinLoadException : Exception
{
    /// <summary>Creates the exception with a message naming the file and key.</summary>
    /// <param name="message">What is wrong, starting with the file name.</param>
    public SkinLoadException(string message)
        : base(message)
    {
    }
}
