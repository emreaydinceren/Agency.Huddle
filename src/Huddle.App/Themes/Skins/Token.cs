namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// One named design decision a skin can set: a skin token. Registered once in <see cref="SkinTokens"/> with a default
/// per mode, written in the skin expression grammar, and rendered as the CSS custom property <see cref="CssName"/>.
/// </summary>
/// <param name="Key">The dotted key a skin file uses, such as <c>ground.page</c>.</param>
/// <param name="LightDefault">The value for a light skin that does not set this token, as a skin expression.</param>
/// <param name="DarkDefault">The value for a dark skin that does not set this token, as a skin expression.</param>
/// <param name="Description">What the token paints, for the schema and the Theme lab.</param>
internal sealed record Token(string Key, string LightDefault, string DarkDefault, string Description)
{
    /// <summary>
    /// The CSS custom property: <c>--hu-</c> and the key with each dot turned into a hyphen. The ink ladder
    /// (<c>ink.*</c>) drops the prefix and is <c>--ink-*</c>, the one name every stylesheet reads it by.
    /// </summary>
    public string CssName => CssNameOf(this.Key);

    /// <summary>The CSS custom property a token key renders as.</summary>
    /// <param name="key">The dotted token key, such as <c>ink.wash</c>.</param>
    /// <returns>The property name with its leading <c>--</c>.</returns>
    public static string CssNameOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return (key.StartsWith("ink.", StringComparison.Ordinal) ? "--" : "--hu-") + key.Replace('.', '-');
    }
}
