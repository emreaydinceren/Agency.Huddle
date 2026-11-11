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
        AddFeedTokens(tokens);
        AddControlTokens(tokens);
        AddDetailTokens(tokens);
        AddEditTokens(tokens);
        AddTaskListTokens(tokens);
        AddEditorTokens(tokens);
        AddReadingTokens(tokens);
        AddShapeTokens(tokens);
        return tokens;
    }

    /// <summary>
    /// Registers the five named radius steps, derived from the Theme's one base radius (<c>--mud-default-borderradius</c>).
    /// A stylesheet names a step, never a multiplier or a length, so every corner follows the base; a Theme that wants
    /// rounder or squarer corners changes its base, not a step.
    /// </summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddShapeTokens(List<Token> tokens)
    {
        Add(tokens, "radius.inner", "calc(mud(default-borderradius) * 0.5)", "The corner of a small mark inside a card: a keycap, a mention, a code span.");
        Add(tokens, "radius.control", "calc(mud(default-borderradius) * 0.75)", "The corner of a row, a button, a segment or a field-sized control.");
        Add(tokens, "radius.card", "mud(default-borderradius)", "The corner of a card, a field, a tile or an editor: the Theme's base radius.");
        Add(tokens, "radius.panel", "calc(mud(default-borderradius) * 1.5)", "The corner of a panel, a dialog, a popover or the Human gate.");
        Add(tokens, "radius.full", "999px", "The corner of a pill. A circle uses 50%.");
    }

    /// <summary>
    /// Registers the Library note's reading tokens: the heading setting and the prose ink. The body size stays the one
    /// derived from the scale (design-system.md "Typography"); a Theme varies only how its headings and prose read.
    /// </summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddReadingTokens(List<Token> tokens)
    {
        Add(tokens, "note.prose.ink", "palette.textPrimary", "The text colour of a note's body prose; headings keep the primary text colour.");
        Add(tokens, "note.h1.size", "1.5em", "A note's h1, relative to its body size.");
        Add(tokens, "note.h1.tracking", "normal", "The letter-spacing of a note's h1.");
    }

    /// <summary>Registers the Feed Tab tokens. Each default is the Feed's look before Themes differed, so a skin lists only what it changes.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddFeedTokens(List<Token> tokens)
    {
        Add(tokens, "feed.summary.ink", "palette.textPrimary", "A Feed card's summary sentence.");
        Add(tokens, "feed.day.rule", "ref(ink.line)", "The rule after a Feed day heading.");
        Add(tokens, "feed.note.background", "transparent", "The Feed's read-only note fill.");
        Add(tokens, "feed.note.line", "transparent", "The Feed's read-only note outline.");
        Add(tokens, "feed.card.hover.shadow", "ref(shadow.card)", "A Feed card's shadow while hovered.");
        Add(tokens, "feed.card.open.shadow", "ref(shadow.card.hover)", "An open Feed card's shadow.");
        Add(tokens, "feed.card.hover.line", "ref(line.control)", "A Feed card's border while hovered.");
        Add(tokens, "feed.card.hover.fill", "ref(ground.raised)", "A Feed card's fill while hovered.");
        Add(tokens, "feed.card.needs.fill", "palette.surface", "A Feed card's fill when it is in the Needs you category.");
        Add(tokens, "feed.card.open.line","ref(line.control)", "An open Feed card's border.");
        Add(tokens, "feed.card.kindbar", "0px", "The width of the kind bar on a Feed card's left edge.");
        Add(tokens, "feed.card.kindbar.ink", "palette.primary", "The kind bar's colour for an item with no kind.");
        Add(tokens, "feed.detail.background", "transparent", "The expanded panel of a Feed card.");
        Add(tokens, "feed.unseen.background", "transparent", "The fill of an unseen Feed item.");
        Add(tokens, "feed.unseen.dot", "palette.primary", "The dot before an unseen item's age.");
        Add(tokens, "feed.unseen.weight", "400", "The summary weight of an unseen Feed item.");
        Add(tokens, "feed.unseen.bar", "palette.primary", "The accent bar of an unseen Feed item.");
    }

    /// <summary>Registers the segmented control and select tokens the Feed's filter bar reads.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddControlTokens(List<Token> tokens)
    {
        Add(tokens, "control.track", "ref(ink.wash)", "The track behind a segmented control.");
        Add(tokens, "control.ink", "ref(text.subtle)", "An unselected segmented-control label.");
        Add(tokens, "control.selected.ink", "palette.textPrimary", "The selected segmented-control label.");
        Add(tokens, "control.selected.line", "ref(line.region)", "The outline of the selected segment's pill.");
    }

    /// <summary>Registers the Task detail panel tokens (the read-only right drawer). Each default is the panel's look before Themes differed.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddDetailTokens(List<Token> tokens)
    {
        Add(tokens, "detail.background", "ref(ground.sidebar)", "The Task detail panel's ground.");
        Add(tokens, "detail.blur", "0px", "The blur behind the Task detail panel (a glass Theme sets it).");
        Add(tokens, "detail.edge", "transparent", "The start-edge line of the Task detail panel.");
        Add(tokens, "detail.decision.background", "transparent", "The band or card behind a Task's Decision card.");
        Add(tokens, "detail.decision.line", "transparent", "The top line of the Decision card's band.");
        Add(tokens, "detail.accent.from", "transparent", "The start colour of a 3px accent bar on a panel header or gate.");
        Add(tokens, "detail.accent.to", "transparent", "The end colour of that accent bar.");
        Add(tokens, "detail.head.line", "transparent", "The hairline under the Task detail header.");
        Add(tokens, "detail.row.line", "ref(ink.line)", "The hairline between property rows.");
        Add(tokens, "detail.section.line", "transparent", "The hairline above a Task detail section.");
    }

    /// <summary>Registers the Task edit page tokens: header, fields, field groups and the gate frame.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddEditTokens(List<Token> tokens)
    {
        Add(tokens, "edit.header.background", "ref(ground.raised)", "The Task edit page's sticky header.");
        Add(tokens, "edit.header.blur", "0px", "The blur behind the Task edit page's header.");
        Add(tokens, "field.line", "ref(ink.field)", "The border of a text or select field.");
        Add(tokens, "field.line.bottom", "ref(field.line)", "The bottom border of a field.");
        Add(tokens, "field.shadow", "none", "The resting shadow under a field.");
        Add(tokens, "form.group.background", "transparent", "The surface of a field group.");
        Add(tokens, "form.group.line", "transparent", "The border of a field group.");
        Add(tokens, "form.group.shadow", "none", "The shadow of a field group.");
        Add(tokens, "form.group.padding", "0px", "The padding inside a field group.");
        Add(tokens, "form.group.head.line", "transparent", "The rule under a field group's label.");
        Add(tokens, "gate.background", "transparent", "The surface of the Human gate and the Decision card on the edit page.");
        Add(tokens, "gate.line", "transparent", "Their border.");
        Add(tokens, "gate.halo", "none", "Their outer ring.");
        Add(tokens, "gate.padding", "0px", "Their inner padding.");
    }

    /// <summary>Registers the Tasks page tokens: the toolbar chips, the List rows and the Kanban board.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddTaskListTokens(List<Token> tokens)
    {
        Add(tokens, "toolbar.line", "transparent", "The rule under the Tasks toolbar row.");
        Add(tokens, "button.filled.shadow", "none", "The shadow of a filled button.");
        Add(tokens, "list.row.line", "ref(line.region)", "The line under a List row.");
        Add(tokens, "list.row.hover", "ref(hover)", "A List row's fill while hovered.");
        Add(tokens, "list.group.background", "ref(ground.page)", "A List group row's fill.");
        Add(tokens, "list.selected.bar", "transparent", "The 3px edge bar on the selected List row.");
        Add(tokens, "list.needs.background", "transparent", "The fill of a List row in Needs You.");
        Add(tokens, "list.needs.bar", "transparent", "The edge bar of a List row in Needs You.");
        Add(tokens, "list.blocked.bar", "transparent", "The edge bar of a blocked List row.");
        Add(tokens, "list.title.weight", "400", "The weight of a Task title in a List row.");
        Add(tokens, "board.background", "transparent", "The ground behind the Board's columns.");
        Add(tokens, "board.column.background", "ref(ground.sunk)", "A Board column's surface.");
        Add(tokens, "board.column.line", "ref(line.region)", "A Board column's border.");
        Add(tokens, "board.count.background", "transparent", "The pill behind a column count.");
        Add(tokens, "board.needs.background", "transparent", "The Needs You column's surface.");
        Add(tokens, "board.needs.line", "ref(board.column.line)", "The Needs You column's border.");
        Add(tokens, "board.needs.halo", "0 0 0 0 transparent", "The Needs You column's outer ring.");
        Add(tokens, "board.needs.bar", "transparent", "The Needs You column's top edge bar.");
        Add(tokens, "board.card.hover.line", "ref(line.control)", "A Board card's border while hovered.");
        Add(tokens, "board.card.hover.shadow", "ref(shadow.card.hover)", "A Board card's shadow while hovered.");
    }

    /// <summary>Registers the Description editor tokens: the card, its toolbar rule and the writing surface.</summary>
    /// <param name="tokens">The registry being built.</param>
    private static void AddEditorTokens(List<Token> tokens)
    {
        Add(tokens, "editor.shadow", "none", "The resting shadow of the Description editor's card.");
        Add(tokens, "editor.toolbar.line", "transparent", "The rule under the Description editor's toolbar, and the dividers in it.");
        Add(tokens, "editor.text.ink", "palette.textPrimary", "The text colour of the Description's writing surface.");
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
        Add(tokens, "ink.line", Ink(18), "A card, tile or washed shape's outline, and an inner rule inside a card or list.");
        Add(tokens, "ink.field", Ink(30), "An input's line.");
        Add(tokens, "ground.page", "palette.surface", "The page ground.");
        Add(tokens, "ground.sidebar", "palette.surface", "The left drawer and the tree header.");
        Add(tokens, "ground.rail", "palette.surface", "The activity bar.");
        Add(tokens, "ground.right", "ref(ground.page)", "The Right sidebar.");
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
        Add(tokens, "selected.background", "mix(palette.primary 14%, palette.surface)", "A chosen item in a set, and a filter or control once it is set.");
        Add(tokens, "selected.background.hover", "mix(palette.primary 20%, palette.surface)", "A chosen item while hovered.");
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
        Add(tokens, "tabs.item.hover", Ink(7), "A hovered tab.");
        Add(tokens, "message.line-height", "mud(typography-body1-lineheight)", "The transcript body's line height.");
        Add(tokens, "send.fg", "palette.primary", "The Composer's Send icon.");
        Add(tokens, "send.bg", "transparent", "The Composer's Send button ground.");
        Add(tokens, "ring", "palette.surface", "The cut-out ring around a presence badge on an avatar.");
    }
}
