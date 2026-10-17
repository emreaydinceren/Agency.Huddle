namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The registry of every skin token: the single list <c>app.css</c> may read as <c>var(--hu-*)</c> and a skin file may
/// set. A skin sets only the tokens it changes; every other token takes the default registered here for its mode.
/// Defaults are expressions over palette variables, so they are the same in both modes until a later phase gives dark
/// its own; each reproduces the value the stylesheet writes today at the sites the token replaces.
/// </summary>
internal static class SkinTokens
{
    /// <summary>Every registered token, in registration order.</summary>
    public static IReadOnlyList<Token> All { get; } = Build();

    /// <summary>Whether <paramref name="key"/> names a registered token.</summary>
    /// <param name="key">A dotted token key, such as <c>ground.page</c>.</param>
    /// <returns><see langword="true"/> when the registry has a token with exactly that key.</returns>
    public static bool Contains(string key) =>
        All.Any(token => string.Equals(token.Key, key, StringComparison.Ordinal));

    private static List<Token> Build()
    {
        List<Token> tokens = [];

        void Add(string key, string value, string description) => tokens.Add(new Token(key, value, value, description));

        static string Ink(int percent)
        {
            string recipe = $"mix(palette.textPrimary {percent}%, palette.surface)";
            return recipe;
        }

        static string Soft(string hue)
        {
            string recipe = $"mix(palette.{hue} 14%, palette.surface)";
            return recipe;
        }

        static string InkOf(string hue)
        {
            string recipe = $"mix(palette.{hue} 50%, palette.textPrimary)";
            return recipe;
        }

        // Ink ladder, grounds, lines.
        Add("ink.strip", Ink(4), "Faintest tint: the tab strip's wash.");
        Add("ink.wash", Ink(6), "A washed shape's fill.");
        Add("ink.rule", Ink(10), "An inner rule inside a card or list.");
        Add("ink.edge", Ink(18), "A card, tile or washed shape's outline.");
        Add("ink.field", Ink(30), "An input's line.");
        Add("ground.page", "palette.surface", "The page ground.");
        Add("ground.sidebar", "palette.surface", "The left drawer and the tree header.");
        Add("ground.rail", "palette.surface", "The activity bar.");
        Add("ground.raised", "palette.surface", "Cards, the composer and panels.");
        Add("ground.sunk", "palette.surface", "Lanes, wells and the search box.");
        Add("line.region", "palette.linesDefault", "The hairline between regions.");
        Add("line.control", "palette.linesDefault", "A button or control border.");

        // State.
        Add("accent.ink", "mix(palette.primary 70%, palette.textPrimary)", "The accent used as text.");
        Add("current.background", "mix(palette.primary 14%, palette.surface)", "The current nav row, rail item or settings nav row.");
        Add("current.background.hover", "mix(palette.primary 20%, palette.surface)", "The current row while hovered or focused.");
        Add("current.foreground", "ref(accent.ink)", "The current row's text.");
        Add("current.icon", "ref(accent.ink)", "The current row's icon.");
        Add("current.weight", "500", "The current row's font weight.");
        Add("current.shadow", "none", "The current row's lifted-tile shadow.");
        Add("selected.background", "mix(palette.primary 14%, palette.surface)", "A chosen item in a set.");
        Add("selected.background.hover", "mix(palette.primary 20%, palette.surface)", "A chosen item while hovered.");
        Add("selected.foreground", "ref(accent.ink)", "A chosen item's text.");
        Add("selected.task.background", "ref(selected.background)", "The selected Task row and card.");
        Add("selected.task.bar", "palette.primary", "The selected Task's inset bar.");
        Add("tree.current.bar", "palette.primary", "The Library tree's 2px current bar.");
        Add("hover", "palette.actionDefaultHover", "A hovered row, tile or button.");
        Add("focus", "palette.textPrimary", "The keyboard focus ring.");
        Add("needs", "palette.warning", "The hue for 'needs you'.");
        Add("text.subtle", "mix(palette.textSecondary 50%, palette.textPrimary)", "Quiet text: rows, tags and quotes.");

        // Tones.
        Add("tone.neutral.soft", Soft("textSecondary"), "Neutral status fill.");
        Add("tone.neutral.ink", InkOf("textSecondary"), "Neutral status text.");
        Add("tone.primary.soft", Soft("primary"), "Primary status fill.");
        Add("tone.primary.ink", InkOf("primary"), "Primary status text.");
        Add("tone.primary.tint", "mix(palette.primary 8%, palette.surface)", "A faint primary tint.");
        Add("tone.info.soft", Soft("info"), "Info status fill.");
        Add("tone.info.ink", InkOf("info"), "Info status text.");
        Add("tone.warning.soft", Soft("warning"), "Warning status fill.");
        Add("tone.warning.ink", InkOf("warning"), "Warning status text.");
        Add("tone.warning.tint", "mix(palette.warning 8%, palette.surface)", "A faint warning tint.");
        Add("tone.warning.line", "mix(palette.warning 55%, palette.surface)", "A warning outline.");
        Add("tone.success.soft", Soft("success"), "Success status fill.");
        Add("tone.success.ink", InkOf("success"), "Success status text.");
        Add("tone.error.soft", Soft("error"), "Error status fill.");
        Add("tone.error.ink", InkOf("error"), "Error status text.");
        Add("tone.tertiary.soft", Soft("tertiary"), "Tertiary status fill.");
        Add("tone.tertiary.ink", InkOf("tertiary"), "Tertiary status text.");

        // Surfaces, controls, transcript.
        Add("shadow.card", "none", "A card at rest.");
        Add("shadow.card.hover", "none", "A card while hovered.");
        Add("shadow.panel", "mud(elevation-4)", "Anchored panels such as popovers.");
        Add("field.background", "transparent", "An outlined input's fill.");
        Add("field.focus.line", "palette.primary", "A focused input's border.");
        Add("field.focus.shadow", "none", "A focused input's halo.");
        Add("scrim", "palette.overlayDark", "The dialog scrim.");
        Add("tabs.strip.background", Ink(4), "The tab strip's background.");
        Add("tabs.item.hover", Ink(7), "A hovered tab.");
        Add("message.line-height", "mud(typography-body1-lineheight)", "The transcript body's line height.");
        Add("send.fg", "palette.primary", "The Composer's Send icon.");
        Add("send.bg", "transparent", "The Composer's Send button ground.");
        Add("ring", "palette.surface", "The cut-out ring around a presence badge on an avatar.");

        return tokens;
    }
}
