namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The registry of every skin token: the single list <c>app.css</c> may read as <c>var(--hu-*)</c> and a skin file may
/// set. A skin sets only the tokens it changes; every other token takes the default registered here for its mode.
/// </summary>
internal static class SkinTokens
{
    /// <summary>Every registered token, in registration order.</summary>
    public static IReadOnlyList<Token> All { get; } = [];

    /// <summary>Whether <paramref name="key"/> names a registered token.</summary>
    /// <param name="key">A dotted token key, such as <c>ground.page</c>.</param>
    /// <returns><see langword="true"/> when the registry has a token with exactly that key.</returns>
    public static bool Contains(string key) =>
        All.Any(token => string.Equals(token.Key, key, StringComparison.Ordinal));
}
