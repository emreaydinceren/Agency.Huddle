namespace Agency.Huddle.App.Themes.Skins;

/// <summary>One legibility requirement: <paramref name="Foreground"/> on <paramref name="Background"/> must reach <paramref name="Minimum"/>:1.</summary>
/// <param name="Foreground">A skin expression the <see cref="SkinColourResolver"/> evaluates, such as <c>palette.textPrimary</c> or <c>ref(focus)</c>.</param>
/// <param name="Background">The expression the foreground is read on.</param>
/// <param name="Minimum">The least acceptable contrast ratio.</param>
internal sealed record SkinContrastPair(string Foreground, string Background, double Minimum)
{
    /// <summary>The pair as a person reads it, such as <c>textPrimary on ground.page</c>.</summary>
    public string Name => Plain(this.Foreground) + " on " + Plain(this.Background);

    private static string Plain(string expression)
    {
        if (expression.StartsWith("palette.", StringComparison.Ordinal))
        {
            return expression["palette.".Length..];
        }

        if (expression.StartsWith("ref(", StringComparison.Ordinal) && expression.EndsWith(')'))
        {
            return expression[4..^1];
        }

        return expression;
    }
}
