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
        AddInkAndGroundTokens(tokens);
        AddStateTokens(tokens);
        AddToneTokens(tokens);
        AddSurfaceTokens(tokens);
        return tokens;
    }

    /// <summary>Registers one token whose default is <paramref name="value"/> in both modes.</summary>
    /// <param name="tokens">The registry being built.</param>
    /// <param name="key">The dotted token key.</param>
    /// <param name="value">The default expression.</param>
    /// <param name="description">What the token styles.</param>
    private static void Add(List<Token> tokens, string key, string value, string description) => tokens.Add(new Token(key, value, value, description));

    /// <summary>The expression mixing the primary text colour into the surface.</summary>
    /// <param name="percent">How much text colour, as a percentage.</param>
    /// <returns>The mix expression.</returns>
    private static string Ink(int percent)
    {
        string recipe = $"mix(palette.textPrimary {percent}%, palette.surface)";
        return recipe;
    }

    /// <summary>The expression mixing a palette hue into the surface.</summary>
    /// <param name="hue">The palette variable name.</param>
    /// <returns>The mix expression.</returns>
    private static string Soft(string hue)
    {
        string recipe = $"mix(palette.{hue} 14%, palette.surface)";
        return recipe;
    }

    /// <summary>The expression mixing a palette hue into the primary text colour.</summary>
    /// <param name="hue">The palette variable name.</param>
    /// <returns>The mix expression.</returns>
    private static string InkOf(string hue)
    {
        string recipe = $"mix(palette.{hue} 50%, palette.textPrimary)";
        return recipe;
    }

    /// <summary>Registers the ink ladder, grounds and lines.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddInkAndGroundTokens(List<Token> tokens)
    {
        Add(tokens, "ink.strip", Ink(4), "Faintest tint: the tab strip's wash.");
        Add(tokens, "ink.wash", Ink(6), "A washed shape's fill.");
        Add(tokens, "ink.rule", Ink(10), "An inner rule inside a card or list.");
        Add(tokens, "ink.edge", Ink(18), "A card, tile or washed shape's outline.");
        Add(tokens, "ink.field", Ink(30), "An input's line.");
        Add(tokens, "ground.page", "palette.surface", "The page ground.");
        Add(tokens, "ground.sidebar", "palette.surface", "The left drawer and the tree header.");
        Add(tokens, "ground.rail", "palette.surface", "The activity bar.");
        Add(tokens, "ground.raised", "palette.surface", "Cards, the composer and panels.");
        Add(tokens, "ground.sunk", "palette.surface", "Lanes, wells and the search box.");
        Add(tokens, "line.region", "palette.linesDefault", "The hairline between regions.");
        Add(tokens, "line.control", "palette.linesDefault", "A button or control border.");
    }

    /// <summary>Registers the current, selected, hover, focus and quiet-text tokens.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddStateTokens(List<Token> tokens)
    {
        Add(tokens, "accent.ink", "mix(palette.primary 70%, palette.textPrimary)", "The accent used as text.");
        Add(tokens, "current.background", "mix(palette.primary 14%, palette.surface)", "The current nav row, rail item or settings nav row.");
        Add(tokens, "current.background.hover", "mix(palette.primary 20%, palette.surface)", "The current row while hovered or focused.");
        Add(tokens, "current.foreground", "ref(accent.ink)", "The current row's text.");
        Add(tokens, "current.icon", "ref(accent.ink)", "The current row's icon.");
        Add(tokens, "current.weight", "500", "The current row's font weight.");
        Add(tokens, "current.shadow", "none", "The current row's lifted-tile shadow.");
        Add(tokens, "selected.background", "mix(palette.primary 14%, palette.surface)", "A chosen item in a set.");
        Add(tokens, "selected.background.hover", "mix(palette.primary 20%, palette.surface)", "A chosen item while hovered.");
        Add(tokens, "selected.foreground", "ref(accent.ink)", "A chosen item's text.");
        Add(tokens, "selected.task.background", "ref(selected.background)", "The selected Task row and card.");
        Add(tokens, "selected.task.bar", "palette.primary", "The selected Task's inset bar.");
        Add(tokens, "tree.current.bar", "palette.primary", "The Library tree's 2px current bar.");
        Add(tokens, "hover", "palette.actionDefaultHover", "A hovered row, tile or button.");
        Add(tokens, "focus", "palette.textPrimary", "The keyboard focus ring.");
        Add(tokens, "needs", "palette.warning", "The hue for 'needs you'.");
        Add(tokens, "text.subtle", "mix(palette.textSecondary 50%, palette.textPrimary)", "Quiet text: rows, tags and quotes.");
    }

    /// <summary>Registers the status tone tokens.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddToneTokens(List<Token> tokens)
    {
        Add(tokens, "tone.neutral.soft", Soft("textSecondary"), "Neutral status fill.");
        Add(tokens, "tone.neutral.ink", InkOf("textSecondary"), "Neutral status text.");
        Add(tokens, "tone.primary.soft", Soft("primary"), "Primary status fill.");
        Add(tokens, "tone.primary.ink", InkOf("primary"), "Primary status text.");
        Add(tokens, "tone.primary.tint", "mix(palette.primary 8%, palette.surface)", "A faint primary tint.");
        Add(tokens, "tone.info.soft", Soft("info"), "Info status fill.");
        Add(tokens, "tone.info.ink", InkOf("info"), "Info status text.");
        Add(tokens, "tone.warning.soft", Soft("warning"), "Warning status fill.");
        Add(tokens, "tone.warning.ink", InkOf("warning"), "Warning status text.");
        Add(tokens, "tone.warning.tint", "mix(palette.warning 8%, palette.surface)", "A faint warning tint.");
        Add(tokens, "tone.warning.line", "mix(palette.warning 55%, palette.surface)", "A warning outline.");
        Add(tokens, "tone.success.soft", Soft("success"), "Success status fill.");
        Add(tokens, "tone.success.ink", InkOf("success"), "Success status text.");
        Add(tokens, "tone.error.soft", Soft("error"), "Error status fill.");
        Add(tokens, "tone.error.ink", InkOf("error"), "Error status text.");
        Add(tokens, "tone.tertiary.soft", Soft("tertiary"), "Tertiary status fill.");
        Add(tokens, "tone.tertiary.ink", InkOf("tertiary"), "Tertiary status text.");
    }

    /// <summary>Registers the surface, control and transcript tokens.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddSurfaceTokens(List<Token> tokens)
    {
        Add(tokens, "shadow.card", "none", "A card at rest.");
        Add(tokens, "shadow.card.hover", "none", "A card while hovered.");
        Add(tokens, "shadow.panel", "mud(elevation-4)", "Anchored panels such as popovers.");
        Add(tokens, "field.background", "transparent", "An outlined input's fill.");
        Add(tokens, "field.focus.line", "palette.primary", "A focused input's border.");
        Add(tokens, "field.focus.shadow", "none", "A focused input's halo.");
        Add(tokens, "scrim", "palette.overlayDark", "The dialog scrim.");
        Add(tokens, "tabs.strip.background", Ink(4), "The tab strip's background.");
        Add(tokens, "tabs.item.hover", Ink(7), "A hovered tab.");
        Add(tokens, "message.line-height", "mud(typography-body1-lineheight)", "The transcript body's line height.");
        Add(tokens, "send.fg", "palette.primary", "The Composer's Send icon.");
        Add(tokens, "send.bg", "transparent", "The Composer's Send button ground.");
        Add(tokens, "ring", "palette.surface", "The cut-out ring around a presence badge on an avatar.");
    }
}
