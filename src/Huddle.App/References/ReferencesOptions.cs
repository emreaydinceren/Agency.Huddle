namespace Agency.Huddle.App.References;

/// <summary>Bound from <c>Team:References</c>, per spec §10 and D-20.</summary>
public sealed class ReferencesOptions
{
    /// <summary>Whether a Turn's prompt gets the Referenced items header. False leaves prompts byte-identical to today's.</summary>
    public bool ExpandInPrompt { get; set; } = true;

    /// <summary>The most characters of item lines in one header. Below 200 is raised to 200 (see <see cref="EffectiveMaxPromptChars"/>).</summary>
    public int MaxPromptChars { get; set; } = 2000;

    /// <summary><see cref="MaxPromptChars"/> raised to at least 200.</summary>
    public int EffectiveMaxPromptChars => Math.Max(200, this.MaxPromptChars);
}
