namespace Agency.Huddle.App.Themes.Skins;

/// <summary>The pairs every shipped skin must keep legible: the skin contrast tests assert them and the Theme lab shows them live.</summary>
internal static class SkinContrastPairs
{
    /// <summary>The least ratio for text.</summary>
    public const double TextMinimum = 4.5;

    /// <summary>The least ratio for the keyboard focus ring.</summary>
    public const double FocusMinimum = 3.0;

    private static readonly string[] GroundTokens =
        ["ground.page", "ground.sidebar", "ground.rail", "ground.raised", "ground.sunk"];

    /// <summary>Every pair: both text colours on every ground, the current row, the accent ink, the primary button, and the focus ring.</summary>
    public static IReadOnlyList<SkinContrastPair> All { get; } = Build();

    private static List<SkinContrastPair> Build()
    {
        List<SkinContrastPair> pairs = [];
        foreach (string ground in GroundTokens)
        {
            pairs.Add(new SkinContrastPair("palette.textPrimary", $"ref({ground})", TextMinimum));
            pairs.Add(new SkinContrastPair("palette.textSecondary", $"ref({ground})", TextMinimum));
        }

        pairs.Add(new SkinContrastPair("ref(current.foreground)", "ref(current.background)", TextMinimum));
        pairs.Add(new SkinContrastPair("ref(accent.ink)", "ref(selected.background)", TextMinimum));
        pairs.Add(new SkinContrastPair("ref(note.prose.ink)", "ref(ground.page)", TextMinimum));
        pairs.Add(new SkinContrastPair("ref(accent.ink)", "ref(tone.primary.tint)", TextMinimum));
        pairs.Add(new SkinContrastPair("ref(accent.ink)", "ref(tone.primary.soft)", TextMinimum));
        pairs.Add(new SkinContrastPair("palette.primaryContrastText", "palette.primary", TextMinimum));
        pairs.Add(new SkinContrastPair("ref(focus)", "ref(ground.page)", FocusMinimum));
        pairs.Add(new SkinContrastPair("ref(focus)", "ref(ground.raised)", FocusMinimum));
        return pairs;
    }
}
