# Design system

How Huddle looks and the rules that keep it consistent: tokens, type, spacing,
components, layout containers, overlays and copy. Read it before you design a screen, add
a component or write a stylesheet for `src/Huddle.App`.
[UXDesignGuidelines.md](UXDesignGuidelines.md) is the hub that routes you here and to
every other UI resource.

**This page describes the system as it ships.** It was written down from the Themes, so the
code is the fact and this page is its description. What matters is enforced by tests and by the
convention scan (see [How the rules are enforced](#how-the-rules-are-enforced)); the rest is house
style that review keeps. Where the page and the code disagree on something no test pins, check the
code, then fix whichever is wrong in the same change. If a rule is wrong for your case, change it and
say why. Don't quietly make an exception.

[rules.md](rules.md) stays the binding list for behaviour the compiler can't check.

**What this page doesn't cover.** Component APIs, parameters and paths are in the
[catalog](UXDesignGuidelines.md#reusable-parts-catalog). Theme values and the contrast
tests are in [theme-authoring.md](theme-authoring.md). This page states the rules and links
to both.

## The one idea

**MudBlazor is the design system.** Huddle does not keep a token file, a component
library or a second stylesheet beside it. A *Theme* is a `MudTheme` object
([ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md)), MudBlazor turns it into
`--mud-*` CSS custom properties, and the `wwwroot/css/` files and every scoped stylesheet read only
those. Everything below follows from that.

| Layer | Owner | Where |
| --- | --- | --- |
| Palette, typography, corner radius, elevation | `MudTheme` built in C# | `src/Huddle.App/Themes/` |
| Components | MudBlazor 9.12.0, used with the house defaults below | `src/Huddle.App/Components/` |
| Compositions and Huddle-only widgets | Plain CSS over `--mud-*` variables | `wwwroot/css/*.css`, `*.razor.css` |
| Skin tokens | 130 `--hu-*` tokens registered once in `SkinTokens`, among them the four `--ink-*` rungs and the five `--hu-radius-*` steps; a skin sets only the ones it changes | `src/Huddle.App/Themes/Skins/` |
| Fallbacks | `--font-mono`, `--font-read` and each token's default, for first paint | `wwwroot/app-vars.css` |

[ADR-0009](../adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md) describes the
earlier design, a `theme.css` of 39 tokens. It is superseded; do not rebuild it.

**Consistency comes from one place per decision.** A colour comes from a palette role, a
status colour from a pure mapping function, an icon from an icon map, an avatar from
`AgentAvatar`, and a recurring shape from a shared component. If you are about to
write the same markup or declaration block a second time, extract it instead.

## Foundations

**Visual reference.** One figure shows the presence badges, the chip looks, the type
scale and a Board card in the former Huddle Light's values. The User Guide and theme-authoring.md link
the same drawing, so all three readers see one picture. Redraw it when one of those rules
changes.

![Huddle's presence badges, chip looks, type scale and a Board card](../images/visual-reference.svg)

### Colour

Colour comes from the palette of the active Theme. Use a semantic role, never a value.

**Theme vocabulary.** These terms say what a colour does on screen, so one rule covers every
Theme. Each has its palette name beside it, so the rest of this page may use either.

| Term | Means | Palette role |
| --- | --- | --- |
| Ground | The one surface everything sits on: page, panes, cards, dialogs | `Surface` |
| Ink | Body text, and the colour every neutral line and fill is mixed from | `TextPrimary` |
| Quiet ink | Supporting text: meta, labels, counts, placeholders | `TextSecondary` |
| Faint ink | Disabled controls only | `TextDisabled` |
| Accent | The Theme's one brand colour, as a fill, bar or border | `Primary` |
| Accent ink | Any text or icon in the accent: the Accent 70% toward Ink | The Accent text mix below |
| Signal | A status colour that carries a meaning (attention, error, done, working, review) | `Warning`, `Error`, `Success`, `Info`, `Tertiary` |
| Signal ink | Text or an icon in a signal: the Signal 70% toward Ink | `color-mix`; see [Chips](#chips) |
| Tint n% | Any colour mixed n% over Ground. "Accent tint 14%" is the selected row | `color-mix` |

**The ink ladder.** A *rung* is Ink mixed n% over Ground. Lines and quiet fills sit on fixed
rungs, so they keep their order and their proportions on any ground. A weak Theme gets weaker
lines, but the order holds, and order is what makes a screen read.

| Rung | Name | Property | Used for | Palette role it replaces |
| --- | --- | --- | --- | --- |
| Ink 4% | Strip | `--ink-strip` | The tab strip | None: it was already a mix |
| Ink 6% | Wash | `--ink-wash` | Quiet fills: tag chip, segmented track, preview block, a disabled control | `BackgroundGray` |
| Ink 18% | Line | `--ink-line` | The border of a card, tile, lane, row, panel or table row, and an inner divider in a card or dialog | `Divider`, `DividerLight`, `TableLines` |
| Ink 30% | Field | `--ink-field` | The boundary of an input | `LinesInputs` |
| Ink 30% | Chrome | `--mud-palette-lines-default` | Top bar, tab strip, tab rail, toolbar and pane hairlines | `LinesDefault`, unchanged |
| Ink 50-80% | Quiet ink | `--mud-palette-text-secondary` | Supporting text, at least 4.5:1 | `TextSecondary`, unchanged |

The four `--ink-*` properties are the skin tokens `ink.strip`, `ink.wash`, `ink.line` and `ink.field`,
each mixed from MudBlazor variables only. The layout writes them with the other skin tokens, and
`wwwroot/app-vars.css` repeats their defaults for first paint. A stylesheet reads them with `var()`:

```css
:root {
  --ink-strip: color-mix(in srgb, var(--mud-palette-text-primary) 4%, var(--mud-palette-surface));
  --ink-wash: color-mix(in srgb, var(--mud-palette-text-primary) 6%, var(--mud-palette-surface));
  --ink-line: color-mix(in srgb, var(--mud-palette-text-primary) 18%, var(--mud-palette-surface));
  --ink-field: color-mix(in srgb, var(--mud-palette-text-primary) 30%, var(--mud-palette-surface));
}
```

Chrome stays the palette's `LinesDefault`, which every Theme sets at the Chrome rung. The measured
ratio of each rung in each Theme is evidence, not a rule; it lives in
[theme-authoring.md](theme-authoring.md).

| Other term | Means |
| --- | --- |
| Base radius | `var(--mud-default-borderradius)`: each skin's `radius` (Lumen and Fluent 6px, Nordic and Cobalt Soft 8px). Every corner is one of five named steps derived from it; see [Shape](#shape) |
| Hue distance | The difference in hue angle between two colours. Two signals, or a signal and the Accent, are told apart only if they are far enough apart |
| Second cue | A glyph, shape, position or word that carries a meaning as well as its hue, so the meaning survives a Theme whose hues collide, and greyscale |
| Theme families | Four, each a light and dark pair: Lumen (the default), Fluent, Nordic and Cobalt Soft, each a JSON skin ([ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md)). The Human's own accent is a per-Theme customization, not a Theme. The Huddle pair, Custom Accent, the VS Code ports and the High Contrast group are gone |

| Role | MudBlazor variable | Use it for |
| --- | --- | --- |
| Surface | `--mud-palette-surface` | **The one shell surface**: the page, the sidebar, the top bar, the tab strip and every pane, plus cards, inputs and dialogs. Panes are separated by hairlines (see Lines), never by a darker ground with gaps around a card. This is the default; Nordic's style pack paints four grounds instead and makes Surface the white raised paper ([ADR-0057](../adr/0057-a-theme-may-carry-a-style-pack.md)) |
| Quiet fill | `--ink-wash`, the Wash rung | Tag chips, preview blocks and the segmented control's track. The tab strip is the Strip rung (`--ink-strip`). All are Ink mixed into Ground, so they follow every Theme. Not a board lane: a lane is Ground (see [Tasks](#tasks)) |
| Ground, Sidebar | `--mud-palette-background`, `--mud-palette-drawer-background` | Not painted by the shell. They stay in each palette because MudBlazor and the theme tests read them |
| Text | `--mud-palette-text-primary`, `-secondary`, `-disabled` | Body, supporting text, disabled controls. `text-disabled` paints disabled controls only |
| Accent | `--mud-palette-primary` | **Only** a fill, an indicator bar or a border: the selected row's tint, the active tab's bar, a primary button, the Human's bubble tint. Never a text action's colour and never an open group's chevron |
| Accent text | `color-mix(in srgb, var(--mud-palette-primary) 70%, var(--mud-palette-text-primary))` | Any text or icon in the accent: links, the active sidebar row, the active tab, text and outlined buttons, a selected chip, an accent icon. Raw `Primary` is never text. The mix stays above 4.5:1 on a light and a dark Theme alike |
| Status | `-success`, `-warning`, `-error`, `-info`, `-tertiary` | State, never decoration. Never a solid fill under text; see [Chips](#chips). `-secondary` is kept for fills and is not a signal |

**Lines.** Each kind of line has one rung and one width (UX-061, UX-062).

| Line | Rung | Rule | Examples |
| --- | --- | --- | --- |
| Line | Ink 18% | `1px solid var(--ink-line)`. Replaces `Divider`, `DividerLight` and MudBlazor's `TableLines`; the Global MudBlazor overrides draw table row lines on Line. Inside a card, spacing often does the job better than a rule | Cards, tiles, board lanes, rows, table rows, panels, floating panels, and a sub-section divider inside a card or dialog |
| Field | Ink 30% | `1px solid var(--ink-field)`. Always stronger than Line | Every input, long-text included |
| Chrome | Ink 30% | `1px solid var(--mud-palette-lines-default)` | Top bar, tabs, tab rail, toolbars, panes, splitter, drawer edge |

> **Lines are ranked:** Line < Field <= Chrome. Each sits on its ink rung (18%, 30%, 30%). Every
> input, long-text fields included, draws the Field line. No line is drawn in Ink, Quiet ink or
> Accent, except a focused field (Accent) and a drop target (dashed Accent).
> `SkinColourRulesTests` holds every Theme to the order, on resolved colours.

> **A wash never defines a shape on its own.** A washed shape (tag chip, reference chip, preview
> block) also carries a Line, so it keeps its outline where the wash can't (dark grounds,
> any Theme). Washes sit on the Wash rung (Ink 6%); an inner rule is a Line, or is replaced by
> spacing.

Line is the only border of a card, tile or row. Chrome is for chrome only, never a card, an
outlined card or a hover border. On the one surface, Chrome is what separates panes, so it must
be clearly visible.

**A toolbar ends in a hairline.** A toolbar that heads a pane or a document (its path, its
mode switch, its actions) is closed by a Chrome line along its bottom edge, so the controls
read as one band and the content as another. The band has 12px above and below its
controls; the line runs the full width of the pane, not just the toolbar's content, and the
content starts 24px below it. It is the same line as the top bar's and the tab strip's, so a
toolbar reads as chrome, never as a card. A toolbar under a page title takes `.toolbar-hairline`
(the Agents toolbar, the Tasks toolbar's last row); a toolbar that heads a pane, like the
Library document toolbar (`.library-document-toolbar`), runs its line the pane's full width. A
stack of toolbar rows has one line, under the last row.

**A tab rail ends in a Chrome line, like a toolbar, and its content starts 24px below.** An
outlined button's border is the Chrome line; its text is Ink, or Accent ink for `Color.Primary`.

**States.** Each interaction state has one treatment (UX-064 to UX-067).

| State | Treatment | Shape |
| --- | --- | --- |
| Hover | `background: var(--mud-palette-action-default-hover)`. Not `table-hover`, except inside a table | None of its own |
| Selected | Accent tint 14% (`color-mix(in srgb, var(--mud-palette-primary) 14%, var(--mud-palette-surface))`), with Accent ink and weight 500, so a selected row never looks like a hovered one | The selected-row shape, below |
| Selected and hovered | The same mix at 20%, on hover and (in the sidebar) on focus | The same |
| Set (a chosen filter, toggle or control) | Reads the `selected.*` tokens: the Selected tint with Accent ink, and an Accent border on a Filter chip. The fill is visibly different from the Surface it sits on in every Theme | None of its own |
| Search match | Attention tint 24%, Ink text, weight 600 | None |
| Accent-tinted surface (the Human's bubble) | 14% fill with a 28% border, over Ground. Never a solid accent fill | The bubble's own |
| Drop target | `1px dashed var(--mud-palette-primary)` plus the hover background | None |
| De-emphasised text | `color: var(--mud-palette-text-secondary)`. Don't use opacity, and don't use `text-disabled`, which is for disabled controls only | None |
| Disabled | Faint ink on Wash, set once in the Global MudBlazor overrides so it follows every Theme. Never MudBlazor's fixed black alpha | The control's own |
| Archived or gone | `opacity: 0.6` on the whole item, the one use of opacity for muting | None |

> **Selected shape:** the row is inset 4px from its container and rounded at the control radius
> step (`--hu-radius-control`). It ships as the global `.selected-shape` class and
> applies to sidebar rows, tree rows, section-nav rows, list pickers and the Activity bar (a 40px
> tile). A selected Library tree row keeps its 2px bar inside the shape. A selected Task List row
> (`task-row-selected`) and the `@` picker's active item wear the same treatment.

Always mix a tint over `surface`, never over `transparent`, so it reads the same on every
ground.

**Two grounds, or two states, are one colour or a visible step apart.** Surface, the `ground.*`
tokens, the Strip rung and the Wash rung are pairwise equal or at least a CIEDE2000 difference of 3
apart (ΔE 3); so are hover, the current row, a selected or set item and their hovered forms. The
current row's ink is the Accent ink, which a hovered row's Ink never is, and every Theme draws a
Board lane border. `SkinColourRulesTests` pins all of it on the resolved colours of the eight Themes.

**How to use colour in a component:**

- Write `Color="Color.Primary"`, not a hex. Write `var(--mud-palette-primary)` in CSS.
- Never write `Style="color: #..."` in a `.razor` file. Per-Agent avatar colours are
  user data and arrive as an inline `style` built in C#; a test still forbids a literal.
- Don't colour body text with a status colour. Status colours mark a chip, a dot, an icon
  or a short value such as a date. A whole card of orange text fails contrast and hides the
  state. An overdue date is a warning icon and the date in weight 600, at the date's own
  size, in the error colour mixed 70% toward body text, not
  plain red text (UX-043). Overdue adds the icon and the weight, never size.
- A notice's sentence is body text too. Only its icon and its leading bar carry the status
  colour; see [Notices and snackbars](#notices-and-snackbars).

**One hue, one meaning across subjects.** `Warning` (amber) means "needs attention": High
priority, an overdue date, "need you", Starting, a folder warning. A neutral or working state
is never `Warning`, so a Board card never shows amber for its column, its assignee and its
priority at once. Before you give a new state a colour, check that its hue does not already
mean something else in this table.

> **A meaning never rests on hue alone.** Every signal has a second cue (the state dot's shape,
> the priority glyph, a word), so it survives colliding hues and greyscale. **Hue distance:** in
> an authored Theme, signals are at least 30 degrees apart from each other and from the Accent.
> A customized accent cannot guarantee this against a picked colour; the second cues carry the meaning.

**Status colours carry the same meaning everywhere.** A status colour is chosen by a pure
mapping function, never at a call site (UX-V14).

| Subject | Mapping | Source |
| --- | --- | --- |
| Task state | Backlog default (its column dot is dashed), To Do default, In Progress info, Review tertiary, Done success, Cancelled/Duplicate/Rejected error | [`TaskColors.cs`](../../src/Huddle.App/Components/Tasks/TaskColors.cs), drawn by `TaskStatusChip` |
| Task priority | Low default, Medium default, High warning (attention), Urgent error. Each also has its glyph | `TaskColors.cs`, drawn by `TaskPriorityChip` |
| Task due date | Overdue is a warning icon plus the date in weight 600, in the error colour mixed 70% toward body text | `TaskDates.IsOverdue` decides |
| Presence and Agent health | Drawn as a [presence badge](#avatars-and-status): a glyph plus a tone. `TaskColors.For(PresenceState)` gives Awake Info, Asleep and Offline Default | `PresenceCue.From` |
| Needs You reason, search notice | `StatusColors.For(NeedsYouReason)`, `StatusColors.For(SearchNoticeKind)` | [`StatusColors.cs`](../../src/Huddle.App/Components/Shared/StatusColors.cs). The Composer's upload chip still colours itself inline (UX-V14) |

A status colour never stands alone: a chip has its text, a dot has an accessible name
(UX-V08), and every signal has the second cue above.

**Contrast floors.** Tests check 4.5:1; reviewers check 7:1 for body text. **Text ladder:** Ink
reaches 7:1 for body text; Quiet ink reaches 4.5:1 and sits between Ink 50% and Ink 80%; Faint
ink is for disabled controls only. Supporting text
(paths, secondary tree text, breadcrumb separators, line numbers), icons and link text reach
4.5:1. Use `text-secondary` for supporting text and icons. A status colour used as an icon
or glyph on its own, such as the tree's orphan warning, is pulled toward the body text the
way the accent is
(`color-mix(in srgb, var(--mud-palette-warning) 75%, var(--mud-palette-text-primary))`).
Check every one in a light and a dark Theme.

**Contrast is tested.** `ThemeCatalogTests` holds every Theme to six pairs, including
`Primary` against both Background and Surface at 4.5:1 (WCAG 1.4.3), and to the line and text
relationships under [How the rules are enforced](#how-the-rules-are-enforced). It also holds
every Theme to a `DrawerText` and `DrawerIcon` taken from its own text colours. The Human's
accent colour goes through `AccentPalette.ClampForContrast`, which darkens or lightens it until
it clears the floor, and its `PrimaryContrastText` is black or white, whichever reads better
(`ContrastColour.ReadableForeground`). The Lumen Themes set `ActionDefault` equal to
`TextSecondary`, so an icon button reads as supporting text and not as MudBlazor's 54% black
(`ThemeCatalogTests` pins it). A palette that fails these tests does not ship, and there is
no exception list. The tests cover palettes and the chips, not
other tinted states; see [Known gaps](#known-gaps).

**The shipped palettes.** The catalog holds four authored pairs mapped from the approved mockup
directions: Lumen (the default, Lumen Light and Lumen Dark), Fluent, Nordic and Cobalt Soft, eight
Themes in all. Each is a JSON skin under `src/Huddle.App/Themes/Skins/`, loaded by `SkinLoader`; its
palette, tokens and traits are explained in [theme-authoring.md](theme-authoring.md). Each Theme has
its own accent, because each is its own palette
([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). A text colour on a Primary fill is
never hard-coded; see the [lookup](UXDesignGuidelines.md#i-need-use-this). The Huddle pair, the
Custom Accent pair and the 17 palettes once converted from VS Code were removed
([ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md); ADR-0016 is superseded).
The picker has a Light theme list and a Dark theme list; the catalog holds the counts.

**Tokens and traits.** Colour decisions the palette cannot name (the ground of the rail, the
current item, a region line, a card shadow) are `--hu-*` tokens, 130 of them, registered once in
`SkinTokens` with defaults written over the palette. Structure that differs between Themes is one of
nine traits: message layout, send button, status look (`glyph` or `pill`), labels, composer, nav,
surface, voice (`plain`, or `book` for a reading face) and ids. A trait is a `data-hu-*` attribute on
`.theme-marker`. Its variants are written once in `wwwroot/css/`, in the file of the feature each
styles, under that file's `Traits:` banner after its base rules. **Within a stylesheet, a trait
value's rules sit in one marked block that names its Themes**, for example
`/* ---- surface=paper: Nordic ---- */`, so the Themes that use a value are found in one place. A default
value usually carries no rules: `nav=tint` and `labels=sentence` have none, and `surface=flat`, the
default surface, holds only Cobalt Soft's form-group label and blocked-card rules.

**One palette per Theme.** A Theme carries one palette and its own `ThemeMode`; the mode
is derived from the Theme and from nothing else
([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). The Human picks one light
and one dark Theme, and the top bar's `ThemeModeToggle` switches between them
([ADR-0054](../adr/0054-a-light-theme-and-a-dark-theme.md)).

### Typography

| Aspect | Value |
| --- | --- |
| UI face | By default Inter, then Helvetica Neue, then `sans-serif`; loaded from Google Fonts in `App.razor`. A skin sets its own faces: Nordic uses IBM Plex Sans, with Literata as its reading face (`--font-read`) and IBM Plex Mono for code |
| Monospace | `--font-mono`: `ui-monospace`, Cascadia Mono, Consolas, Courier New |
| Case | Sentence case everywhere. `Typo.button` is `text-transform: none`; tab rails must be too (UX-009). A Tab title is upright unless the Tab is a preview, which is italic: a preview Tab is weight 400 and the others 500 |
| Scale | Set in rendered px, once, in `ThemeDefaults.WithSharedTypography`. The page is not zoomed ([ADR-0056](../adr/0056-the-ui-renders-at-100-percent-with-a-px-type-scale.md)) |

Set up once in [`ThemeDefaults.cs`](../../src/Huddle.App/Themes/ThemeDefaults.cs). A
palette carries no fonts, so every Theme inherits this stack.

**The type scale, by role.** Pick the role, then use its `Typo` and tag. The scale stops
at `h5`; don't use `h1` to `h4` as a `Typo`. The Rendered column is what the Human sees,
so drift shows when you read this table (`TypeScaleTests` pins it). Line heights are
unitless and set with the sizes in `ThemeDefaults.WithSharedTypography`.

| Role | `Typo` | `HtmlTag` | Rendered | Line height | Weight | Examples |
| --- | --- | --- | --- | --- | --- | --- |
| Page title | `h5` | `h1` | 20px | 1.3 | 600 | a Room's name (`C2`, UX-038), "Settings", a Team's name. Every page shows one; see [A page](#a-page), step 3 |
| Dialog or drawer title | `h6` | MudBlazor's | 16px | 1.35 | 600 | The item's name, never its type: "Alan", not "Agent"; "SUPP-0005 · Build the public status page"; "Duplicate Platform" |
| Section heading inside a page | `subtitle2` | `h2` | 16px | 1.35 | 600 | Agents groups, Prompts groups, "Roles", "Roster" (UX-001) |
| Label: every small label (a group of fields, a search group heading) | | | 12px | 1.35 | 600 | The Task form's groups, the search group headings. Quiet ink, sentence case, 8px below it: the global `.group-label` class. `labels=upper` uppercases it, its only variant |
| Card title | `subtitle1` | | 14px | 1.4 | 600, from the `.card-title` class | An Agent tile's name, an agent card's "Nova is asking" (UX-031). The `subtitle1` role itself is weight 400, so a title wears `.card-title` |
| Prose, Transcript | `body1` | | 14px (the Library reader 16px at 1.65) | 1.55 | 400 | Intro paragraphs, "No tasks yet.", a message's body |
| Editable long text | `body1` | | 14px | 1.55 | 400 | Prompt fields, Agent instructions, a Skill's body. Monospace only when the content is code. Never the input's default weight or line height |
| Row text: rows, cells, controls | `body2` | | 13px | 1.4 | 400, 500 for controls | List rows, table cells, menu items, sidebar rows |
| Metadata, timestamps, helper lines, counts | `caption` | | 12px | 1.35 | 400 | "11:48", "3 Members + you" (UX-037) |
| Fine: a second line that only qualifies the first | | | 11px, `var(--font-size-fine)` | inherits | 400 | A 1:1 Room's Agent title under the name in the sidebar. MudBlazor has no role this small, so `app-vars.css` declares the size |
| Button | `button` | | 13px | 1.4 | 500 | A button's label, Small buttons included (see [Spacing, density and size](#spacing-density-and-size)) |

**Minimum size.** No text renders below 11px, with no exceptions. An avatar's letters are 40% of its
diameter, held to that floor: a 24px avatar shows 11px letters and a 32px one 13px. A text label of up
to six characters shows its first three characters below 40px, five at 40px and all six at 72px, and
shrinks to fit its circle but never below 11px; a single character fills more of the circle (55% of
the diameter). `TypeScaleTests` fails on a literal font size under 11px and holds the avatar labels to
the floor.

**Weights.** Three: 400 for text, 500 for controls, 600 for emphasis and headings. Never
700; `strong` and `b` are 600. Never italic. What a Human types in a field is text, so it
is 400, which is the weight of the `subtitle1` role MudBlazor draws every `.mud-input` in; a card title
reaches 600 through `.card-title`, so no field needs an override.

**Measure.** Prose never runs wider than 75 characters: `max-width: 75ch` on the text
block. That covers intro paragraphs (`.settings-intro`, `.build-team-intro`,
`.page-lead`, `.page-header-subtitle`), `.settings-stored-at`, helper text and empty states.
A wide pane leaves the measure alone. The Transcript is one column of 760px, centred; an
Agent's `.message-body` is at most 75ch and the Human's bubble `min(75%, 75ch + 32px)`.
An empty state's sentence is capped tighter, at 45ch.

**Hierarchy.** The steps are 20, 16, 14, 13, 12 and 11px, so a page reads as an outline: page
title, section heading, card title and body, rows, meta, and the fine line that qualifies a row.

- A heading is never smaller than the text it heads, and a section heading is one size
  step above the text in its section, not only heavier. A card title may be the same size
  as its body at weight 600.
- A drawer or dialog title is the item's name, not its type: "SUPP-0005 · Build the public
  status page", never "Task". See [Task form anatomy](#task-form-anatomy).
- **In a drawer or dialog, the title is the only 16px text.** Its groups wear the Label
  (12px/600 in Quiet ink, `.group-label`), which is a label and so the one heading allowed to
  be smaller than what it heads.
- A section heading inside a page is `subtitle2`, weight 600, in `TextPrimary`
  (`.group-heading`). Secondary-grey section headings belong to the sidebar.

**In stylesheets, font sizes come from the typography variables**:
`var(--mud-typography-body1-size)`, `-body2-size`, `-caption-size`, `-subtitle1-size`,
`-subtitle2-size` and `-button-size`, plus `var(--font-size-fine)` for the 11px rung. Don't write `0.8rem`, `0.85rem` or `14.5px` (UX-068, UX-V18). The
Library is the one place a size is derived rather than picked (see [The Library](#the-library)),
because long-form reading
needs a size the UI scale doesn't have: rendered Markdown (`.library-rendered`, Read mode
and Split's preview) is `body1` x 8/7 (16px) at a 1.65 line height, about 70 characters a
line, with headings stepping up from it (h1 x 1.5, h2 x 1.25, h3 x 1.125); the
raw editor is monospace at `body1` x 0.9375 (13px) with a 1.6 line height. A
hand-written `mud-typography-*` class on a `<span>` is a `MudText` with `HtmlTag="span"`
(UX-V31).

**The sidebar follows the scale too.** Rows are `body2` (13px). A section header is
`caption` (12px) at weight 600 in the secondary text colour. See [The sidebar](#the-sidebar).

### Spacing, density and size

**Density is the default, and it is set on every control, not left to MudBlazor's
default.** MudBlazor's default `Size` is `Medium`, so a `MudButton` with no `Size`
renders taller than the 28px row size a Small one has (UX-V01).

| Control | Required |
| --- | --- |
| `MudButton`, `MudIconButton`, `MudChip`, `MudToggleGroup` | `Size="Size.Small"` |
| `MudTextField`, `MudSelect`, `MudAutocomplete`, `MudNumericField`, `MudDatePicker` | `Variant="Variant.Outlined"`, `Margin="Margin.Dense"` |
| `MudToolBar` | `Dense="true"`, `Gutters="false"` so it lines up with the page title (UX-005) |
| `MudAlert` with one line | `Dense="true"` (UX-040) |
| `Size.Large` | Avatars on a profile card and an empty pane's icon only. No large buttons |

**A Small button is a row-sized control**: 28px high, in the Button role (13px, weight 500), for text,
outlined and filled alike. The Status and Tag chips are 24px and the Filter chip 28px; see [Chips](#chips).

> **One height per toolbar row: 28px.** Small buttons, the segmented control, filter chips, and a
> `SearchField` or select with `Class="toolbar-compact"` (28px box, 13px text) all match. A
> full-height dense input belongs in a form. An inline add row (a field and its **Add**) follows
> the same rule.

**Spacing in markup uses MudBlazor's utility classes**, on a 4px step:

| Place | Class |
| --- | --- |
| Page container, drawer content, dialog content | `pa-4` |
| Card, panel, tile | `pa-3` (12px), with no exception: an Agent tile is 12px like a Board card |
| Between sections | `mb-4` |
| Between related fields | `mb-2` |
| A stack | `MudStack` with `Spacing="2"` for fields and `Spacing="4"` for sections |

**There is one spacing scale: 4, 8, 12, 16 and 24px** (32px for the rare large gap), the
same steps as MudBlazor's utility classes (`pa-1` is 4px, `pa-4` 16px, `pa-6` 24px). In
a stylesheet, write spacing (`padding`, `margin`, `gap` and their sides) in px on that
scale; 1px and 2px hairline offsets are fine. A pane puts 16px between its edge and its
content and 24px above its first line. A pane that opens with a toolbar is the exception:
the toolbar is a band with 12px above and below its controls, closed by its hairline, and
the 24px goes below that line instead.

**A clearance is the one larger step.** When padding only keeps text out from under a
control overlaid on the box (the Composer's Send and attach buttons, a role card's actions),
size it to that control at 40, 48, 64 or 80px. It is not rhythm, so don't use those values
for ordinary gaps.

CV11 flags spacing in rem and any px spacing off these values. Heights, widths and offsets
such as `top` or `right` are not spacing and keep their own units (UX-073, UX-V18). A skin's own
spacing tokens (a key ending in `padding`, `margin` or `gap`) use the same 4, 8, 12, 16, 24 and 32px,
and 0 (`SkinSpacingTests`).

**The size ladder.** A control, row or mark is one of 20, 24, 28, 32, 40 or 44px (18px is the one
small mark below them). A row is 32px; there is no density switch and no other row height.

| Size | Used for |
| --- | --- |
| 24px | A Status or Tag chip; an avatar in a row or the sidebar; a Feed row's avatar |
| 28px | A Small button, an icon button, a toolbar row, a Filter chip; the dialog close button |
| 32px | Every row: the sidebar, a menu item, a picker row, the Task List, the Task edit page's properties; the page title's row |
| 40px | The Activity bar's tile |
| 50px | An avatar on a card or tile, a Feed card's avatar included, and a Message's avatar in the Transcript |
| 44px | The least a touch target gets: a Library tree row on a touch screen |

An icon is 14, 16 or 18px, and 32px in an empty state. The window switches layout at three widths only:
MudBlazor's 600px and 960px and the shell's 1099px, which its stylesheet derives from the two sidebars.
Anything inside a pane uses a container query instead (the Task List does). CV11 checks the sizes and
the window widths in every stylesheet: a width or height from 17px to 47px must be on the ladder, a
glyph on the icon sizes, and a window `@media` width one of the three.

The page is not zoomed, so a size in a stylesheet is the size on screen and a full-height
area is plain `100vh`. See
[ADR-0056](../adr/0056-the-ui-renders-at-100-percent-with-a-px-type-scale.md).

### Shape

Corners are five named steps, all derived from the skin's one base radius,
`var(--mud-default-borderradius)` (6px in Lumen and Fluent, 8px in Nordic and Cobalt Soft). A skin sets
the base and nothing else, and a stylesheet names a step, never a multiplier.

| Step | Value | Use it for |
| --- | --- | --- |
| `var(--hu-radius-inner)` | 0.5 x base: 3px or 4px | A small mark inside a card: a keycap, a mention, a code span |
| `var(--hu-radius-control)` | 0.75 x base: 4.5px or 6px | A row, a button, a segment, the Status and Tag chips, the selected-row shape and the segmented indicator |
| `var(--hu-radius-card)` | The base: 6px or 8px | Every bordered card, tile, field, lane, editor and preview block, whatever its size |
| `var(--hu-radius-panel)` | 1.5 x base: 9px or 12px | A panel, dialog or popover; the Feed card and the message bubble |
| `var(--hu-radius-full)` | 999px | Pills: count badges, pill buttons, the Filter chip |
| `50%` | Circle | Avatars and status dots, always |

**No literal radius.** CV11 refuses a `border-radius` that is not 0, 50% or one of the five steps, so a
card cannot stay at a literal 8px while a Theme with a different base rounds everything around it
(UX-060).

### Elevation and layering

**Prefer a hairline to a shadow.** A page, panel, card or tile is flat with a card border:
`MudPaper Outlined="true"` with `Elevation="0"`, never the default elevation-1 shadow
(UX-013). The drawer and the app bar are `Elevation="0"`.

| Layer | Elevation | `z-index` |
| --- | --- | --- |
| Page content, cards, tiles | 0 with a card border | none, or `1`/`2` for stacking inside one component |
| A floating panel anchored to a control (Invite, the `@` picker) | `var(--mud-elevation-4)` with a card border, through one shared `.floating-panel` class (UX-063) | `var(--mud-zindex-popover)` |
| Menus and popovers | MudBlazor's own | MudBlazor's own |
| Dialogs, drawers, snackbars, tooltips | MudBlazor's own | MudBlazor's own |
| The error banner `#blazor-error-ui` | | `var(--mud-zindex-snackbar)`, so the sidebar can't cover it (UX-V20) |

Write no literal `z-index` above `2`; use a `--mud-zindex-*` variable.

### Icons

Icons are MudBlazor's Material set. A Tab stores a symbolic name and
[`TabIcons.For`](../../src/Huddle.App/Tabs/TabIcons.cs) resolves it, so persisted state
never holds SVG. An icon MudBlazor lacks comes from
[`MaterialSymbols.cs`](../../src/Huddle.App/Tabs/MaterialSymbols.cs).

**One concept, one icon; one icon, one concept.** Take an entity's icon from `TabIcons`
and an action's icon from the table below, in the weight given. A new concept gets a new
row here before it gets an icon in the code (UX-083, UX-084, UX-R26).

| Entity | Icon |
| --- | --- |
| Room (chat) | `Filled.Chat` |
| Team | `Outlined.Groups` |
| Project | `Filled.LibraryBooks`, for nothing else |
| Agents, a Team's members | `Filled.Group` |
| Tasks | `Outlined.TaskAlt` |
| Library | `Filled.MenuBook` |
| A folder, including a Library root | `Outlined.Folder` |
| A note or document | `Filled.Description` |
| Settings | `Filled.Settings` |
| Skill, including the Settings Skills section | `Outlined.AutoAwesome` |
| Goldfish mode (a fresh-eyes Agent) | `MaterialSymbols.Fish`, for this concept only |

| Action | Icon | Not this |
| --- | --- | --- |
| Add, New | `Filled.Add` | `Outlined.Add`, or a literal "+ " in the label |
| Close a dialog, panel or Tab; remove an item from a set the Human is building (a chip, an upload) | `Filled.Close` | Dismissing a notice |
| Delete permanently | `Filled.Delete` | `PersonRemove` |
| Remove from a Team or a list, where the thing itself survives | `Filled.PersonRemove` for a member, otherwise `Filled.RemoveCircleOutline` | `Delete` |
| Dismiss a notice or a Needs You row | `Filled.Check` | `Close` |
| Edit, Rename | `Filled.Edit` | |
| Copy text, an id or a path | `Filled.ContentCopy` | `ContentPaste` |
| Copy a link | `Filled.Link` | |
| Duplicate an entity | `Filled.FileCopy` | `ContentCopy` |
| Archive | `Filled.Archive` | |
| Close a Task | `Filled.Lock`, the glyph of the Closed chip | `Delete` |
| Reopen a Task | `Filled.LockOpen` | |
| Move a Board card to a state | The state's glyph, `TaskColors.Icon(state)` | |
| More actions | `Filled.MoreVert` | A FAB or a chevron (UX-050b) |
| Search | `Filled.Search` | |

### Avatars and status

Component parameters are in the [catalog](UXDesignGuidelines.md#display).

- **`AgentAvatar` is the only place an avatar becomes pixels.** Never draw a raw
  `MudAvatar` for an Agent, and never compute a monogram elsewhere (UX-051). Keep its
  single `MudAvatar` root.
- **An avatar is a circle at one of four sizes (a Team's is a rounded square).** `Small` (24px) in rows, the sidebar, the
  `TeamAvatar` wraps `AgentAvatar` with `Shape="RoundedSquare"`, adding `.avatar-square`; the sizes and
  the picture's hairline are the same. An Agent or the Human is never a square, and a Team never a circle.
  Board and the Room header; `.avatar-transcript` (32px) in headers and a 1:1 Room's sidebar row, and 50px on a
  Message in the Transcript, where it fills the 50px `.message-gutter`; `Medium` (50px) on cards and tiles, the Agents tile and roster
  cards; `Large` (72px) on a `AgentProfileCard` and in the avatar editor. Pass the class and the
  `Size`; don't set a width or radius of your own.
- **An Agent's handle is written `@alias`**, wherever it is shown.
- **An avatar-colour swatch is a 32px circle** drawn with the avatar recipe for the current
  mode, and the swatches are a radio group with colour names.

**The look.** One recipe per Theme mode, drawn by one `shell.css` rule, so every monogram clears
4.5:1 without choosing black or white per colour.

| Mode | Fill | Ring (1px, inset) | Letters |
| --- | --- | --- | --- |
| Light | The Agent's colour at 40% over Surface | The fill mixed 20% toward `TextPrimary`, a few shades darker | That colour mixed 75% toward `TextPrimary`, weight 600 |
| Dark | The colour at 40% over Surface, a deep fill | The fill mixed 20% toward white, a few shades lighter | That colour mixed 75% toward white, weight 600 |

CSS can't read a Theme's mode, so `MainLayout.razor` renders a hidden
`<span class="theme-mode-dark">` when the Theme is a Dark one, and
`:root:has(.theme-mode-dark)` in `shell.css` switches the recipe.
`AvatarColours.MonogramStyle` emits only `--mud-palette-primary: #hex;` on the avatar, and the
rule turns that into the fill and the letters. An Agent with no colour uses the Theme's
accent through the same recipe. The contrast tests sweep and sample both modes.

| Part | Rule |
| --- | --- |
| Letters | One word gives its first two letters ("Helen" gives HE). Several words give the first and last initials. A one-letter name stays one letter. An empty name gives "?" |
| Letter size | 40% of the diameter, on the type scale: 11px, 13px, 20px and 28.8px on 24px, 32px, 50px and 72px. Never under 11px |
| Text label | Up to six characters, in place of the monogram. Under 40px the first three are drawn, at 40px five and at 72px all six; a long label shrinks to fit, a single character grows to 55% of the diameter |
| Image | A photo or illustration keeps the same circle on one neutral fill, never the Agent's colour: `TextPrimary` 10% over Surface in a Light Theme, 24% in a Dark one. Its 1px inset ring is the monogram's recipe, the fill mixed 20% toward `TextPrimary` (Light) or white (Dark), so mixed kinds sit together. The Agent's stored colour stays for the monogram. It shows from 32px up; at 24px (`Size.Small`) every avatar is the monogram, because an image at that size reads as a blur beside the letters. So a 1:1 Room's sidebar row shows the monogram |
| Stacked members | The Room header's stack is 24px avatars, each with a 2px Surface ring |
| Profile ring | On a `ProfileCard`, the avatar sits in a ring: 1px `TextSecondary`, 2px clear of the avatar. Dashed for a proposed Agent, solid (`Existing`) for one that already exists. Nowhere else, except the goldfish ring (Huddle.Goldfish §7.4) |
| Goldfish ring | An Agent in goldfish mode (a live possession) wears a 2px solid `Info` ring, 2px clear of the avatar and outside it (`.presence-anchor.goldfish`), and a fish badge in place of its Health badge: the fish in `Info` mixed 20% toward black. The badge alone, with no ring, on a `ProfileCard`. `aria-label` "{Name}, in goldfish mode". Drawn only through `PresenceAnchor` (`AgentName`, `BadgeOnly`) |

**The presence badge.** A badge is a circle at the avatar's bottom-right rim, ringed in 2px
of Surface, with a glyph as well as a colour. It is 10px, and 12px beside an avatar of 40px or
larger. It has `role="img"`, the state word as its `aria-label`, and the reason in its
tooltip. It sits in a `PresenceAnchor` around the avatar, because a `MudAvatar` clips its
children. Standalone, `StatusDot` draws the same 10px badge inline.

| Glyph and tone | Health (`PersonaState`) | Presence (`PresenceState`) |
| --- | --- | --- |
| Green circle, white check | Online | |
| Amber circle, white pulse | Starting | |
| Blue (`Info`) circle, white pulse | | Awake (a Turn is running). Working is not attention, so it is not amber |
| Red circle, white dash | Degraded | |
| Hollow grey ring | Offline | Asleep; Offline |
| Blue (`Info`) circle, white fish | In goldfish mode (replaces the Health badge) | |

A filled tone is the palette colour mixed 20% toward black, so the white glyph keeps at least
3:1 against it in every Theme (a test holds it). Both rings are the neutral grey. The
pulse glyph breathes: the badge fades between full and about half opacity over 1.8s, and is
static under `prefers-reduced-motion`. The glyph carries the state, so the badge also reads in
greyscale.

**Health versus Presence.** Health says whether the Adapter can act at all; Presence says
what an assignee is doing now. They stay two systems that share one glyph set through
`PresenceCue.From`, and each keeps its own word.

- **A status dot is `StatusDot`**: a 10px circle drawn through `PresenceBadge`, with an
  accessible name and a title from its own label function (UX-070, UX-V08). Don't copy its
  state-to-class switch.
- **The Agents tile carries no "Online" chip.** The badge on its avatar says it.

### Motion

| Motion | Rule |
| --- | --- |
| Hover or focus reveal, a small transform | `transition: opacity 120ms ease` (or the property that changes) |
| The segmented control's indicator | `300ms cubic-bezier(.4, 0, .2, 1)` |
| Anything longer, or anything that loops | Must have a `@media (prefers-reduced-motion: reduce)` branch that replaces it with a static cue. `.search-hit-flash` and the presence pulse show the pattern; the draft caret and ReconnectModal still lack it (UX-V06) |

Write durations in `ms`. Every hover reveal also has a `@media (hover: none)` branch
that shows the control, or it can't be reached on a touch screen (UX-075).

### Focus and accessibility

- **The focus ring is `2px solid var(--mud-palette-text-primary)`, offset 2px**, on
  `a:focus-visible` and `button:focus-visible`. MudBlazor resets the outline, so this
  rule restores it. It uses text colour and not the accent because a dark Theme's accent
  can fall below 3:1; text colour clears 3:1 in every Theme. Don't override it with the
  accent, a different width or a hover ring (UX-V07)
  ([base.css](../../src/Huddle.App/wwwroot/css/base.css) "Keyboard focus ring").
- **The Composer's focused border is the accent.** `.composer-input:focus-visible` draws a
  `Primary` border plus a 0.5px inset accent ring, which reads as 1.5px and moves no text.
  `Primary` clears 3:1 against Surface in every Theme (`SkinContrastTests`), where the Chrome line would
  not.
- **Every `MudAlert` has a role**: `role="alert"` for an interruption, `role="status"` for
  a consequence (UX-V05).
- **An icon-only control has an `aria-label` that names its target** ("Delete Grace",
  not "Delete") when it repeats per row. A tooltip alone is not enough (UX-V09).
- **State is never shown by colour alone.** Use text, an icon or `aria-pressed` with it
  (UX-V08).
- **An inert input uses `Disabled`, not `ReadOnly`.** MudBlazor 9.12.0 styles only the
  disabled state (UX-V04).
- **An avatar image has an empty `alt`** because the Agent's name is always rendered
  beside it.
- **A drag handle also works from the keyboard**: `tabindex="0"`, `aria-value*` and the
  arrow keys (UX-V10).
- **Use the MudBlazor control** rather than a raw `<button>`, a clickable `<div>` or a bare
  `<a>` (UX-V13).

## Layout containers

The shell and its regions are in
[`MainLayout.razor`](../../src/Huddle.App/Components/Layout/MainLayout.razor), and every
container's code (`S2`, `C7a`, `T3`) is in [wireframes.md](wireframes.md).

| Region | Component | Notes |
| --- | --- | --- |
| Top bar | `TopBar` (a `MudAppBar`) | Slots: `Search`, `Actions`. Shows the breadcrumb of the focused pane |
| Activity bar | `ActivityBar`, 52px, in the left `MudDrawer` | A top toolbar with one button per Sidebar view (Chats, Teams, Agents, Tasks, Library): the selected one is pressed, and clicking it again collapses the Sidebar. A bottom toolbar holds Archived and Settings |
| Sidebar | `div#sidebar-views` beside the Activity bar, 240px | Shows one view: Chats (Needs you, Rooms), Teams, Agents, Tasks or Library. Section headers are sentence case |
| Workspace | `MudMainContent.main-column` | A flex column, `overflow: hidden`, holding `TabHost` |
| Tab groups | `TabHost` then `TabGroupPane` | One or two panes side by side, each a `TabStrip` over a `.tab-body`. The focused pane owns the URL; the other dims its active tab |
| Page | `TabPageHost` mounts a `DynamicComponent` | Only each group's active Tab is mounted |
| Right sidebar | Panels that follow the focused page, 280px | See [The Right sidebar](#the-right-sidebar) |
| Inside a page | `MudTabs` (Team page, Settings), `MudSplitPanel` (Library) | `MudDrawer` is not resizable, so use `MudSplitPanel` for two resizable panes |

### The sidebar

The sidebar's content is about 677px tall in an 890px window, so it fits one screen, and at
900px every section header is visible.

- **Every row is one line, 32px high** (`--nav-row-height`). The label is a
  `span.sidebar-row-label` with the full name as its `title`, and it ellipsises. A row is
  `body2` (13px) in body colour, with a 24px avatar or an 18px icon (`--icon-size-row`) and a
  4px gap to the label. A section header is `caption` (12px) at weight 600 in
  `TextSecondary`, sentence case, 16px above and 4px below (UX-014). Footer entries are rows
  too, or icon-only buttons with an `aria-label` (UX-017).
- **Every row sits on two lines: an icon line and a text line.** Every row has a 24px leading
  slot, and every leading mark is centred in it: a section's icon, a row's 18px icon, a 24px
  avatar, a Team's chevron, the "+" of an action row, the arrow of "Show all". The slot starts
  4px inside the row's pill and every label starts 4px after it: a section title, a name, "Show
  all", "New room". These are VS Code's Explorer proportions scaled to the 32px row (its 16px
  twistie becomes the 24px slot, its few-px margins stay tight), so a label starts 32px inside
  its row's pill. A row with no mark keeps the slot empty. The grid is one set of tokens in
  `shell.css` "Left menu" (`--nav-inset`, `--nav-pad`, `--nav-slot`, `--nav-gap`, `--nav-step`,
  `--nav-end`), and every view of the Sidebar uses it, so switching views moves nothing sideways.
- **A section header and its rows share the lines.** The header heads its list; it is not a
  level above it.
- **Every view starts at one top.** Its first row, a section header, a destination (Agents,
  Library) or the Needs you card, is 16px below the view's top, and a section that follows
  another in the same view is 16px below it. Every row is exactly 32px, an avatar row too, so
  rows fall on a 32px rhythm.
- **Nesting moves a row in by exactly one step** (`--nav-step`, the slot and the gap): a
  child's mark sits under its parent's label, and its label starts one step further in. A row
  sets its level with `--nav-level`, never a padding of its own. A Team's Rooms and Library
  folders are level 1, and the rows inside them level 1.25; "Build a team" is a top-level action, in line with the Teams.
- **Trailing details end on one line**, `--nav-end` inside the row's right edge: a Team's
  member count, a Needs you time and count, a section's collapse arrow. A count gives way to
  the row's hover "..." while the row is hovered or focused; on touch, where the button always
  shows, the count ends just before it.
- **The selected pill is inset `--nav-inset` from the panel's sides on every row.** The Needs
  you card's sides sit on that inset, and its rows are pulled back by the card's offset, so they
  keep both lines.
- **A destination is a row, not a header.** "Agents" and "Library" are rows (13px body,
  18px icon in the leading slot); only a group with children wears the section-header style.
- **The Needs you card is outlined in Line.** Its row is 32px: the reason icon, the Room's
  name, and a trailing time, like every other sidebar row.
- **A group header is 32px like a row.** Sidebar action rows ("Show all", "New room") are 13px
  in Quiet ink.
- **The active row wears the Selected treatment and shape**: Accent ink, a 14% tint (20% on
  hover or focus) and the inset rounded shape (see [States](#colour)).
- **The Activity bar follows the focused page's area** unless the Human picked a view since.
  A view whose only content is one group opens that group.
- **Settings has no Sidebar view.** Opening it collapses the Sidebar, because its sections are
  the page's own left menu; a Room keeps whichever view is showing, and opens Chats from a
  collapsed Sidebar.
- **Meta is trailing, never wrapped.** A Team row ends in a `span.sidebar-row-meta` member
  count whose tooltip reads "3 members" or "No members".
- **Groups start closed; the group holding the current page opens.** It also opens on
  navigation, and never closes by itself. Teams persist their open set under
  `teamsNav:expanded`.
- **A list over 8 rows folds behind "Show all (N)"** and "Show fewer" (`SidebarRows`,
  `ShowAllRow`). The active row stays visible.
- **The Library has one home.** The sidebar has one Library link to `/library` and no
  tree, root rows or "Add folder"; the tree lives on the Library page.
- **The first section is "Rooms"**, the defined term in [language.md](language.md).
- **Rows at the same level share one indent**, set by the grid in `shell.css` "Left menu".
  Within a level, either every row has an icon or none does (UX-015).
- **A 1:1 Room shows the Agent's small avatar; a group Room shows the Room icon**
  (UX-016). That avatar is 32px (`.room-row-avatar.avatar-transcript`), so it draws the Agent's
  picture: the face is how the Human finds the Room. The Agent's title sits on its own line below
  the name at every sidebar width, in Quiet ink at the fine rung, 11px (`.room-row-title`); the row grows to fit.
  An Agent with no Persona, or a title that repeats the name, shows no title. The avatar overhangs
  its slot's start by 4px, so its row's text box does not clip, and each line truncates itself. It
  keeps 8px to its name, so a 1:1 Room's name starts 8px after a group Room's.
  Room rows stand 4px apart (`.room-nav-row + .room-nav-row`).
- **Sidebar action links** ("New room", "Build a team", "New view") take the
  `nav-action-link` class: secondary text colour (not the accent), the "+" in the leading slot
  and the label on the text line like any row. Don't set `IconColor` on them, and don't repeat
  those rules inline.
- **The sidebar is the shell's surface**, not a tinted panel; a 1px `lines-default` edge
  separates it from the workspace. Its text is `DrawerText`, which every Theme sets to
  `TextPrimary`.
- **Row actions are revealed on hover or focus** with a `120ms` opacity transition, and
  stay reachable by keyboard and on touch.

### The Right sidebar

280px, with 16px padding and a Chrome line on its edge. Its panels follow the focused page.

- **Panel header:** 13px/600 in Ink, a 12px Quiet-ink count and a chevron; 32px high, with an
  Line line between panels.
- **Rows:** 32px (28px in the Outline), one line, ellipsised, with the full text in `title`.
  A Task row is the ID in 12px Quiet ink, the title in Ink, and the state's dot. A member row
  is a 24px avatar with its presence badge, then the name.
- **Text:** Ink. Accent ink marks only the hovered or current row. The Outline marks the
  heading in view with the Selected treatment.
- **Empty:** `EmptyState` with the sentence "Nothing to show for this page.", the same one the
  User Guide uses.
- **With a drawer:** a right-hand drawer opens beside the sidebar, or closes it and restores it
  afterwards. It never covers the sidebar and its toggle.

### The tab strip, tab rail and breadcrumb

- **Tab strip.** A Tab title is 13px at weight 400, and 500 when active. A preview Tab is
  marked by `TextSecondary` colour and an italic title, never by weight; keeping it returns the
  body colour and upright text. Titles ellipsise, and the `title` attribute carries the full title. A Tab is a
  fixed 14rem, so its hover toolbar leaves the start of the title visible. The strip is the
  Strip rung (Ink 4% over Ground), and the active Tab is Ground with a 2px accent top edge, so
  it opens into the page below. Every unpinned Tab shows its close button, at 60% until hovered
  or active.
- **Tab rail** (Settings, the Team page). Labels are 13px at 500 in `TextSecondary`; the
  active label is in accent text over a 2px accent indicator. The icon is 18px
  (`--icon-size-row`) and takes its label's colour. Scroll arrows appear only when the tabs
  overflow the rail. The rail ends in a Chrome line and its content starts 24px below it.
- **Breadcrumb.** 13px, ancestors in `TextSecondary`, the current crumb in `TextPrimary`, and
  8px each side of the "›". It starts at the window's left edge, inside the top bar's 16px
  gutter, so the strip above the sidebar is not left empty and a long trail has room.

### The Room

The Room is the Transcript over the Composer in one column.

- **An Agent's message has no card.** It is plain 14px text beside a 50px avatar; the body is
  at most 75ch. The Human's bubble keeps its tint, at most `min(75%, 75ch + 32px)`, with a 12px
  radius. Both sit in the 760px centred column.
- **Names, times and gaps.** The sender is 13px at 600; the time is 12px `TextSecondary`, 8px
  after the name. There are 16px between senders and 4px between consecutive messages of one
  sender (`.grouped`).
- **Day separator.** 12px at 600 in `TextSecondary` on a hairline, 24px above and 16px below.
  It reads Today, Yesterday, or "Mon, Oct 5"; a message under it shows the time only.
- **A file in a message** is its name as a `.ref-link` (an attachment chip for uploads), with
  the relative path in the tooltip.
- **The Room header is a `PageHeader`** with a Chrome line, not a band of its own; see
  [A page](#a-page).
- **A Task link** in a message has a dotted underline, accent text and an 8px status dot.
- **References sit under their message**, in the same column: left-aligned under an Agent's
  body, right-aligned under the Human's bubble. Never detached beside it.
- **A notice sits in the column too.** An offline or error notice goes between the Transcript
  and the Composer at the same 760px width, so all three share one left edge; see
  [Notices and snackbars](#notices-and-snackbars).
- **The Composer shares the column.** Its box is the Transcript's 760px, centred, so it lines up
  with the messages ([catalog row](UXDesignGuidelines.md#actions-and-input)). Its placeholder is
  "Message {Room name}… (/invite @Agent)", or "Message… (/invite @Agent)" without a name.

### Tasks

- **A Board card** has 12px padding, a Line border, the card radius and 8px between
  cards. Its first row (`.task-card-header`) holds the ID (12px `TextSecondary`) and the
  priority [status chip](#chips). The title is 14px at 600 and runs to three lines. Tags are
  tag chips. The assignee is a 24px avatar with the name at 12px. The due date is `caption`
  (12px), like the ID, written as [Dates and times](#dates-and-times) says. A card shows no
  State chip and has no coloured edge.
- **A lane** is Ground with a Line border and the card radius, in every Theme. Its
  column header names the state; the lane itself carries no fill, so an empty lane is not a
  grey block in Light and a lane is not invisible in Dark. A column is 260-300px. Empty
  terminal columns collapse to a 40px rail with the name and count. A column is **empty
  terminal** when every state in it is terminal (Done, or the Won't do family) and it holds no
  Task in any lane. A rail opens while a card is dragged, so its ghost buckets can take a
  drop, and while its name is being renamed.
- **A swimlane label** is the page section heading (16px/600) with its count, under a Chrome
  line. In a swimlane an empty cell is blank; the empty line shows only when the whole column
  is empty.
- **A column header** is an 8px state dot in the colour of the column's first state (dashed for
  Backlog), the name at 13px/600 and the count at 12px `TextSecondary`.
- **An empty column** shows one centred 12px `TextSecondary` line: "Nothing done yet.", "Nothing
  in progress.", "Backlog is empty.", "Nothing to do yet.", "Nothing in review.", or "Nothing
  here yet." for a column of several states. The line ignores the pointer and hides while a
  card is dragged, so the column still takes the drop.
- **Overdue** is the warning icon and the date in one span; see [Colour](#colour). An overdue
  card shows its date even when the View hides `due_date`.
- **Header counts** are 13px, with the numbers at 600 in body colour; "need you" is the
  warning colour mixed 70% toward body text. A count wears its signal only above 0.
- **Hover and focus reveal** (see [Patterns worth copying](#patterns-worth-copying)) applies
  to Board cards and List rows.
- **The List** has 32px rows, 13px body and header cells at 12px/600
  `TextSecondary`. The ID column is 110px, Priority 120px (`task-list-priority-cell`), the row
  menu 40px, and Title takes the rest. A group row is Ground, with a 13px/600 label, a 24px
  avatar for an assignee group, and a 12px Quiet-ink count. No wash.
- **The Task form** is in [Task form anatomy](#task-form-anatomy).

### The Library

- **Tree rows** are 1.6em (about 21px at the 13px text), so their spacing follows the font, with 16px icons and a 16px indent (44px high on a touch screen).
- **The reader** is `body1` x 8/7 (16px) at 1.65 with a 46.29em measure (35em widened 15% twice),
  centred in the pane rather than set flush left. A note's Markdown h1 is
  24px at 600, the one size above 20px in the app, and only in the reader.
- **The document toolbar** ends in a hairline, as every toolbar does (see Lines). The tree
  header and the document toolbar are one band of the same height, with one Chrome line across
  the splitter.
- **The file path** is 12px `TextSecondary`, relative to its Library root ("Teams/Platform/
  Architecture.md"), middle-truncated by `LibraryPathDisplay.Shorten`. The absolute path on
  disk is in the tooltip only.
- **"Linked from N notes"** (`.library-backlinks`) is the width of the text column above it,
  its label 13px at 500. The Read, Edit and Split toggle is 13px.
- **A folder warning** is a 14px triangle in the warning colour mixed toward body text, with a
  tooltip. The empty state's "New note" matches the toolbar's Small text button.

### Settings, the Team page and Build a team

- **Settings.** `.settings-page` caps the content at 960px, left-aligned. Its sections sit in a
  section nav (see [A page](#a-page)). Each Theme row shows a swatch of its Ground, Ink and
  Accent. The intro is 14px `TextSecondary` at 75ch; "Overrides stored at" is 12px with the path in 12px `--font-mono`;
  `{{tokens}}` are tag chips in 12px mono.
- **The Team page.** "Roles" and "Roster" are `subtitle2` `h3`s with a 12px `TextSecondary`
  count ("Roster · 4 members", "Roles · N roles available"). Roles fill two rows of `.role-grid`
  220px cards that scroll sideways, and the roster does the same with 20rem cards in `.roster-grid`
  (the one place a card grid scrolls; the team types still wrap): the description is up to three lines at 13px, "Hiring pool" is meta text, Add is
  a Small outlined button at the bottom left and "Suggested" sits to its right. A roster card
  has a 50px avatar with the presence badge and no status dot beside the name, the name at
  14px/600, the role at 13px `TextSecondary`, "Member", "New" or "1 skill" as meta text, and
  its actions on one row (Edit, Message, and Remove in the menu).
- **The save bar** renders only when there are unsaved changes, and is
  `position: sticky; bottom: 0` with a hairline on top (`.build-team-savebar`).
- **Build a team.** `PageHeader` stacks its subtitle on its own 13px `TextSecondary` line. A
  stepper label is 13px at 500 and its detail 12px `TextSecondary`. `.team-type-grid` wraps.
  Every team type is one quiet grey tile with a `TextSecondary` icon; only
  `.team-type-card-selected` is accent-tinted. "You have one" is meta text.

### Agents

The grid is `repeat(auto-fill, minmax(240px, 1fr))`. The `<li>` is the bordered card and holds
the opening `<button class="agent-tile">`, so the Task link is not nested in a button. A
tile has a 50px avatar with the badge, the name at 14px/600, the role at 13px `TextSecondary`,
`@alias · State` as 12px meta text, and "Working on SUPP-0002" at 12px with the Task as a link
to `/tasks/item/{id}`. A tile has 12px padding. Its content is left-aligned on one edge, the "Working on" line
included. Group headings are the section heading (16px/600) with a 12px count
(`.agents-group-count`). The page's intro says what an Agent is to the Human; how a
Persona file works belongs in the User Guide.

### A page

1. **Route it.** Make it a routable component (`@page`); `TabPageRoutes` resolves a Tab's
   URL to it. Teach `TabKeys.Describe` the URL's key, title and icon name. A page does not
   draw its own breadcrumb, and a page with no ancestor shows none.
2. **Contain it.** Wrap it in a `MudContainer` with `Class="pa-4"` (UX-002). The one
   exception is a page that is nothing but split panes edge to edge, the Library page: it
   uses `pa-0`, and each pane pads itself (16px sides, 12px above its toolbar or tree header) so the
   splitter's hairline runs the full height.

   | Kind | `MaxWidth` | Pages |
   | --- | --- | --- |
   | Reading page: forms, settings, a directory | `MaxWidth.Large` with `.page-reading` | Settings, Agents, Build a team, the Team notice |
   | Wide page: boards, grids, split panes, tabs that hold those | `MaxWidth.False` | Tasks, Library, a Team or Project page |

   **Left alignment.** Every page's title starts at the same left edge. A reading page
   limits the width of its content, not its position, so `.page-reading` drops the
   container's auto margins and the title does not move when the Human switches Tabs.

3. **Title it.** Every page opens with a page header: the title as
   `MudText Typo="Typo.h5" HtmlTag="h1"` on the left and the page's actions on the right,
   each a `Size.Small` button at its natural width, never full width (UX-001, UX-004).
   A page's toolbar sits under the header, lined up with the title.

   - **The page title model.** The page name is the `h1`, and every page shows its `h1`: there
     is no `HideTitle`. The breadcrumb holds only the ancestors, so it never repeats the
     title: a Room shows "Rooms", a Team "Teams", Build a team "Teams", and a Task View
     "Tasks", with the View's display name (never its slug) as the `h1`. A page with no
     ancestor, such as Settings, shows no breadcrumb. The one exception is a Project page,
     whose trail is "Teams › {Team} › {Project}", the Project as the current crumb, so the bar
     names the Project the Teams sidebar has selected.
   - **The document title is the bare page name**: "Platform", "Grace", "My Tasks". There
     is no "— Huddle" suffix. `PageHeader` sets it; a page that has no `PageHeader` sets
     `<PageTitle>` itself.
   - **One header geometry.** The `h1` row is 32px high, 16px below the tab strip, on every
     page. The Room header is a `PageHeader` with a Chrome line, not a band of its own.
   - **The create action is in the header, at the right**, as a Small button: filled if it is
     the page's one primary action, otherwise outlined.
   - **An intro is `body1` (14px) in Quiet ink at 75ch**, through `PageHeader`'s `Subtitle` or
     `.page-lead`.
4. **Fill the pane.** The page's root is `h-100`, with `min-height: 0` on a flex child so
   inner regions scroll instead of the page growing past the window (`.room-column` is the
   model). Split panes fill the height too (UX-006).
5. **Scroll inside the page, not the window.** A horizontally scrolling region keeps its
   scrollbar and headers in view (UX-007).

Two patterns for sections. A **tab rail** holds up to five sections (the Team page). A
**section nav** holds six or more (Settings), with the sidebar's row geometry and the Selected
shape. Either way, give each section an icon from `TabIcons` or the entity table and a
sentence-case label (UX-008, UX-009). A control that acts on one section lives inside it, not
above the rail or the nav (UX-010).

## Overlays

| Need | Use | Notes |
| --- | --- | --- |
| A form or a short flow | `MudDialog` through `IDialogService` | Pass an id or a snapshot, not live objects: an open dialog's parameters are frozen |
| A destructive yes or no | The shared confirm (see [Confirming](#confirming)) | Not a custom dialog per site |
| A destructive yes or no on a list row | `InlineConfirm` | Swaps in for the row |
| A transient result | `ISnackbar` | Always with a `key` |
| A menu or picker | `MudMenu`, `MudPopover` | MudBlazor's own sizing; the page is not zoomed |

### Dialog anatomy

| Part | Rule |
| --- | --- |
| Options | `CloseButton = true`, `FullWidth = true` for a form, width per the Width row; `BackdropClick = false` when it holds typed input. Don't hand-build a close button in `TitleContent` (UX-022, UX-023) |
| Title | `Typo.h6`, sentence case, and the item's name: a verb phrase naming the item for an action ("Duplicate Platform", "Rename note"), the item itself for a view ("Alan's avatar", "Fields in Awaiting review"). When a caller passes a title, show it |
| Fields | `Variant.Outlined`, `Margin.Dense`. `AutoFocus` on the first field. In a single-field dialog, Enter submits (UX-024) |
| Validation | Live, through the feature's pure validator, shown with `Error` and `ErrorText` on the field. Disable the primary action while invalid |
| An error from the action | `MudAlert Severity.Error Dense role="alert"` above the actions. Not a raw `<p>` or a `MudText` (UX-025) |
| Actions | Right-aligned in this order: **Cancel** (`Variant.Text`), then **Back** if there is one, then the **primary action** (`Variant.Filled`, `Color.Primary`, or `Color.Error` when it destroys). All `Size.Small` (UX-020, UX-021) |
| Busy | The primary action disables and shows a busy label ("Saving…") or a spinner |
| Picker dialog | No actions row; `CloseButton = true` is the way out |
| Close button | 28px, an 18px icon, `ActionDefault`; restyled once in the Global MudBlazor overrides |
| Padding | Title `16px 24px 8px`; content `8px 24px`; actions `12px 24px 16px` |
| Width | 600px for forms, confirms and pickers under about 12 items (`HuddleDialogOptions.SmallForm` for a form, `ShortPicker` for a picker); 960px (`HuddleDialogOptions.Picker`) only for two-pane or tabular pickers |
| Disabled primary | Faint ink on Wash; never MudBlazor's fixed black alpha |
| Field labels | Above the field, never floated into its border: caption size at 600 in body text, the same at rest, focused and filled, with the border drawn whole (alignment D6, `base.css` "A field's label sits above its field"). A context that labels the field elsewhere keeps the control's label visually hidden |
| Required fields | "(optional)" on optional fields; never an asterisk |
| From a menu | The menu closes before the dialog opens: wrap the call in `MenuDialogs.AfterClosingAsync(menu, () => dialog call)` |
| Primary verb | Repeats the title's verb ("Add a role" gives **Add**) |
| Quotes | One style in copy: "x", never 'x'. `ConfirmCopy.Quote(name)` writes it and `ConfirmCopy.DeleteTitle(name)` writes `Delete "x"?` |
| Place of a new item | `NameDialog.Location` appends the place to the title: "New note in Platform" |
| List rows in a picker | 32px, 13px, an 18px icon in a 24px slot (`.picker-row`) |
| Depth | At most two dialogs deep. A picker opened from a dialog replaces its content, with **Back** |
| Empty optional field | A Quiet-ink placeholder ("None", "Add tag…"), not a blank outlined box |

### Task form anatomy

The Task form (`TaskDetail`, on the Task edit page `K`) has a header row, content in `pa-4` with
`MudStack Spacing="4"`, and its actions in the dialog order. There is no side drawer: the Task
detail (`T7`) moved to the Right sidebar on 2026-10-28, and `ResizableEndDrawer` was deleted.

- **Actions are in the edit page's header** (`K2a`): Cancel, then Save, the one filled button.
- **A draft opens with the title field focused**, at title size.

- **The title is the item's name** in `Typo.h6` (16px/600, up to three lines): the Task's
  name alone, or "New task" for a draft. The generic type word "Task" is only the fallback.
- **An eyebrow sits above the title.** It is a 12px `TextSecondary`
  line: the ID, then the Project or Team ("PLAT-0013 · Platform"). A draft has no eyebrow and
  keeps its in-body "ID: title" row after create.
- **A close `MudIconButton`** sits at the right of the header. Edit, copy and the menu are
  a right-aligned icon row just under the header, not in it.
- **The form is grouped.** Put the fields under a Label each (`.group-label`,
  12px/600 in Quiet ink): the Task detail uses Status and priority, People, Planning and Dates,
  with a 120px label column at 13px `TextSecondary`. The title stays the only 16px text.
- **A property is a label and a value.** `.task-detail-groups` is a size container. At 368px
  of container width or more (a 400px column less its padding), `.task-detail-prop` is two
  columns, label left and value right. Narrower, it stacks. This mirrors the Library
  Properties grid, but shares no class.
- **Every field has a visible label.** Team and Project are two rows, and Description has
  a label. The control's own floating label stays in the DOM, visually hidden.
- **Created, Updated and Closed are footer meta.** They leave the Dates group and sit at the
  bottom in `.task-detail-footer-meta` (12px `TextSecondary`), after the Timeline.

### In-place action rows

Cards in the Transcript (`C8`), notices with actions (`C9c`) and banners lay their actions
out **left-aligned, primary first**: the Filled primary, then Outlined secondaries, then
Text quiet actions. This differs from dialogs on purpose: a left-aligned row reads from
its first button, and a dialog's footer reads from the right (UX-032).

An Agent's card (`C8`) sits in the 760px Transcript column with a Line line. Its answer options
are one-line 32px rows.

### Menus

A menu is a `MudMenu` of `Dense` 32px, 13px items.

- Every item has an icon from the action table, or none does.
- Dividers group items. A destructive item comes last, after a divider, in Error ink.
- The activator has an `aria-label` naming its target and no hover tooltip.
  `MenuDialogs.ActivatorLabel(target)` writes it ("Actions for {target}").
- An item that opens a dialog ends in "…", and the menu closes before the dialog opens
  (`MenuDialogs.AfterClosingAsync`, so the menu is never left above the scrim).
- A destructive item wears the global `.menu-item-destructive` class: Error ink on the text
  and the icon.
- The Task View menu (Rename…, Fields…, Delete view…) and the sidebar View row menu (Rename…,
  Delete view…) carry no icons, because the action table has no row for Fields; that is the
  "none does" case, and Delete view… is last after a divider in Error ink. The Task drawer
  menu is Copy id, Copy link, Make a copy…, a divider, then Close task… (Reopen for a closed
  Task) in Error ink, every item with an icon.
- A destructive action is never a top-level button with a caret: it is ⋮ › **Remove from
  team**.

### Confirming

- **Anything that can't be undone asks first**, including removing a Library root
  (UX-026).
- **The confirm button repeats the verb**: "Delete", "Remove", "Reset". Never "Confirm",
  "OK" or "Yes, …" (UX-080).
- **The title is a question ending in "?"**: Delete "Notes"?. One sentence of body
  says what is lost.
- A destructive confirm button is `Color.Error`. All destructive confirms go through one
  shared confirm component (UX-R12); a list row uses `InlineConfirm` (UX-R13).
- **`InlineConfirm`'s button repeats the verb and follows Cancel.** In the sidebar the prompt
  is the verb alone ("Delete?"), on one line, in the row's indent (`InlineConfirm Narrow`).
- **Closing a Task asks first**, in the shared confirm titled "Close PLAT-0003?" with the
  button "Close task". There is no undo snackbar: closing wakes the assignee.

### Notices and snackbars

- **A notice is a `MudAlert`** with a role, `Dense` when it is one line, placed in the
  page's notice zone (`C9`, `T9`, `L3d`). Not a styled `<div>` (UX-056, UX-V13).
- **A notice's sentence is `TextPrimary`.** Only its icon and its 3px leading bar carry the
  status colour. MudBlazor derives a coloured text from the severity; the override lives in
  `base.css` "Global MudBlazor overrides", so every notice gets it.
- **A notice in a Room sits in the 760px column**, between the Transcript and the Composer,
  so all three share one left edge. It never spans the pane.
- **A snackbar passes a `key`** so repeats collapse (UX-V25), and carries an action when
  there is a way forward, such as "Open task" or "Move anyway" (UX-V26).

## Components

### House defaults

Start from the pattern in the file the table names; the full list is in
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md).

| Need | Component and defaults |
| --- | --- |
| Button | `MudButton Size="Size.Small"`. `Variant.Filled` for the one primary action per view, `Variant.Outlined` for secondary, `Variant.Text` for quiet and for Cancel |
| Icon button | `MudIconButton Size="Size.Small"`, with an `aria-label`, and a `MudTooltip` when it is not obvious |
| Text input | `MudTextField Variant="Variant.Outlined" Margin="Margin.Dense"` |
| Search field | The same, with a `Filled.Search` start adornment, `Clearable`, `Immediate`, one debounce constant, a placeholder ("Search members") and an `aria-label` (UX-055) |
| Choice | `MudSelect` and `MudAutocomplete` (Outlined, Dense), `MudRadioGroup`, `MudToggleGroup`; for a two- to five-way mode switch, `HuddleSegmentedControl` |
| Container | `MudPaper Outlined="true" Elevation="0"` for a panel or card; `MudStack` for layout |
| Status | `StatusChip` for a state in a word, `MudBadge` or `StatusDot` for presence; see [Chips](#chips) |
| Table | `MudSimpleTable` for short lists, with an empty row; `MudDataGrid` for sortable, groupable data |
| Disclosure | `MudExpansionPanels`, `MudCollapse` |

### Chips

**One component, three looks.** Every chip is `Chip` (`Components/Shared/Chip.razor`) with a `Look`:
Status, Tag or Filter. It is built on `MudChip` because several callers live in a `MudChipSet`, and a
raw `<MudChip` anywhere else fails CV13. `StatusChip`, `TaskFilterChip` and `ChipCloud` wrap it, so a
caller names the look it needs and never a class. A fourth job, a count or a piece of meta, is plain
caption text (`.chip-meta`) and no chip. A status colour is never a solid fill under text. Parameters
are in the [catalog](UXDesignGuidelines.md#display).

| Look | Draws | Height | Used for |
| --- | --- | --- | --- |
| Status or priority, tinted: `Look=Status`, `.status-chip-*` | The colour at 14% over Surface; text and icon the colour mixed 50% toward `TextPrimary` (a heavier mix than other Signal ink, because the chip text sits on its own tint); a leading icon | 24px | High, Urgent, In Progress; the Feed's Live chip; the Human gate and decision chips; the AI-reacting chip |
| Tag, quiet: `Look=Tag`, `.tag-chip` | Wash fill with a Line border, Ink text | 24px | `api`, `pricing`; a note's tags and Archived chip; the Composer's upload chip; a Task's blockers and tags |
| Filter: `Look=Filter`, `.filter-chip` | A dashed Field line and no fill while unset; once set, an Accent border, the Selected tint (`selected.background`, Accent tint 14%) and Accent ink at 500 (`.is-set`) | 28px | Assignee: Me; the search scopes; the Feed's categories; a pack toggle; every `ChipCloud` chip |
| Count or meta: `.chip-meta` | No chip: plain caption text | | "@jane", the Needs You count, "Next session", the Settings nav badge, the history pills |

**The Status and Tag chips share one shape**: 24px high, `0 8px` padding, the control radius
(`--hu-radius-control`), 12px text at weight 500 and a 14px leading icon.

> **Filter chip:** 28px high, `0 12px` padding, a pill (`--hu-radius-full`), 13px at 400. Unset: a
> dashed Field line, no fill. Set: an Accent border, the Selected tint, Accent ink at 500. `.is-set` is
> the only set class Huddle writes. `TaskFilterChip`, `ChipCloud` and the search scopes all draw it,
> through the `Chip` component.

**Text on a status fill reaches 4.5:1.** `StatusChipContrast` mirrors the tint in C#, and a
test holds every pair to the floor. The measured ratio for each status colour in each Huddle
Theme is in [theme-authoring.md](theme-authoring.md).

A Board card shows no State chip, because its column names it, and it carries no
per-state left edge. No palette has `*ContrastText` properties for status colours, because
no status colour is a fill under text.

### Component rules

The catalog of components, with paths and parameters, lives only in
[UXDesignGuidelines.md](UXDesignGuidelines.md#reusable-parts-catalog). These are the
rules that catalog's rows carry out.

- **Use `AgentAvatar` and `StatusDot`**; see [Avatars and status](#avatars-and-status).
- **A chip is the one `Chip`, with a Look.** `StatusChip` is its Status look; a Task's state and
  priority go through `TaskStatusChip` and `TaskPriorityChip`; `TaskFilterChip` and `ChipCloud` are its
  Filter look. A raw `<MudChip` anywhere else fails CV13.
  [Catalog row](UXDesignGuidelines.md#display).
- **A page starts with `PageHeader`**; a page never writes its own `h1` or a loose action
  row. See [A page](#a-page), step 3. [Catalog row](UXDesignGuidelines.md#display).
- **An empty list, table or pane uses `EmptyState`.** It is a centred stack: a 32px
  `TextSecondary` icon, a required `Title` (14px/600, `role="status"`), a 13px `TextSecondary`
  sentence that says what to do next, and an optional action. A full-pane one adds
  `empty-state-centered` and sits a third of the way down. In a table, put it in a `colspan`
  row. [Catalog row](UXDesignGuidelines.md#display).
- **A pill track with a sliding indicator is `HuddleSegmentedControl`**, up to five
  options, coloured from `background-gray` and `surface`.
- **A destructive yes or no is `ConfirmAsync`, or `InlineConfirm` on a list row.**
  Confirm takes focus, Escape cancels. See [Confirming](#confirming).
- **A one-field name dialog is `NameDialog`.**
  [Catalog row](UXDesignGuidelines.md#dialogs-o1).
- **Dialog options come from `HuddleDialogOptions`**, never built by hand.
- **A card an Agent puts in front of the Human derives from `AgentCardBase`** and is drawn
  with `AgentCard`. Once decided, the card goes and the decision appears as a posted
  Message (Spec §6.11). `QuestionCard` uses buttons with `aria-pressed` because it owns the
  selection; `ElicitationCard` uses radios and checkboxes (UX-030 to UX-035).
- **Selection in a chip cloud** reads as text and `aria-pressed`, never by colour alone.
  Use `ChipCloud` wherever chips filter a list.
- **A search box is `SearchField`**; an Agent row is `AgentLabel`; a Task filter is
  `TaskFilterChip`.
- **An inline edit of a title keeps the title's size and replaces it.** The field renders at
  the title's size and weight, with no floating label: a Room's rename is `.title-inline-edit` at h5
  size, labelled by `aria-label="Room name"`. It stays `Variant.Outlined` and
  `Margin.Dense` because CV2 requires them, so the outline is the only cue that it is
  editable. There is no shared inline-rename component.
- **An empty optional field shows a Quiet-ink placeholder** ("None", "Add tag…"), not a blank
  outlined box.
- **Helpers that remove per-site copies** are listed under
  [Helpers](UXDesignGuidelines.md#helpers). Use them; don't write another copy.

No further components are planned; the
[catalog](UXDesignGuidelines.md#components-to-build) says what to do when a shape turns
up twice.

### Patterns worth copying

- **Transcript.** An Agent's message is plain text beside an avatar gutter, with no card.
  The Human's message is a right-aligned `.message-bubble` tinted with the accent. Both sit
  in a 760px centred column, with the Composer in the same column. The body is `body1`, and
  the sender's name is set apart by weight 600, not by being smaller (UX-039). The numbers
  are in [The Room](#the-room).
- **Empty state.** `EmptyState`: an icon, a short title that says what is missing ("No pages
  are open"), and a sentence that says what to do next ("Choose one from the menu."). It is
  centred in its block with 32px above and below; a full-pane one sits a third of the way
  down (UX-077).
- **A list row with actions.** Actions sit in a `MoreVert` menu revealed on hover or
  focus. The same item has the same menu items and label everywhere it appears (UX-050a).

## Interface copy

Words follow [language.md](language.md). These rules decide the form.

- **Controls are sentence case**: buttons, menu items, tab labels, field labels, column
  headers and dialog titles. Only the first word is capitalised, even when a defined term
  follows: "New team", "Invite agent", "Add folder", "New note", "Consult when" (UX-078).
- **Prose capitalises a defined term**, as language.md does: notices, helper text and
  empty states write "This Task was deleted" and "No Agents in this Team yet".
- **One verb per meaning:**

  | Verb | Means | Not |
  | --- | --- | --- |
  | Cancel | Abandon this dialog or edit; nothing changes | Close, Dismiss |
  | Close | Put away a view; nothing is lost | Cancel |
  | Close task… | End a Task. Asks first, or offers **Undo** in the snackbar | Delete |
  | Delete | Destroy permanently | Remove |
  | Remove | Take out of a set; the thing survives | Delete |
  | Archive | Hide, recoverably | |
  | Dismiss | Hide a notice or request without acting on it | Skip, Close |
  | Decline | Refuse an Agent's proposal | Reject, Leave paused |
  | Invite | Bring an existing Agent into a Room | Add |
  | Add | Put something into a set | Create |
  | Create | The primary action of a "New …" dialog | OK |
  | Save | Write pending edits | Save to view |

- **One label per field and per action across the app**: "Team name", never "Team title"
  in one dialog and "Team name" in another (UX-079, UX-082).
- **Count with a plural helper**: "1 member", "2 members". Never "1 Members" (UX-037).
- **Show a display name, never a raw id or slug**: "Checks before merge", not
  "checks-before-merge" (UX-082).
- **Busy labels end in an ellipsis character**: "Saving…", "Sending…".
- **Copy lives in markup**, not in a CSS `content:` (UX-V23).
- **Don't say "AI"**; language.md lists it under Avoid (UX-082).

### Dates and times

Write a date the way a person says it. One helper formats every date on screen
(`HumanDate`), so a call site never picks a format string.

| Date | Shown as |
| --- | --- |
| Today, tomorrow, yesterday | "Today", "Tomorrow", "Yesterday" |
| Within the next six days | The weekday: "Fri" |
| Any other date this year | Month and day: "Oct 27" |
| A date in another year | With the year: "Oct 27, 2025" |
| A time | 24-hour, as the Transcript already shows: "01:49" |
| A day separator | "Today", "Yesterday", or "Mon, Oct 5" |
| A date in a table column | Month and day, never a weekday: "Oct 27" |
| A message under a day separator | The time only |

The full date is in the tooltip. Never show an ISO date (`2026-10-27`) in the UI, except in
a raw-Markdown view where it is the file's content, and in a date picker's input. A date picker
prefers `DateFormat="MMM d, yyyy"`.

## Writing the stylesheet

1. **Put a rule in `wwwroot/css/`** (the file of its feature) when it targets a MudBlazor component's internals
   (`.mud-drawer-content`, `.mud-nav-link-text`, `.mud-toggle-item`). Blazor's CSS
   isolation stamps its attribute only onto HTML elements written in your own markup, so a
   `::deep` rule under a component root never matches and fails silently. This holds for a
   Huddle component's own scoped file too (UX-V16).
2. **Put a rule in a `.razor.css`** only when it targets markup in that component.
3. **Colour and font:** `var(--mud-palette-*)`, `var(--mud-typography-*)`, the skin tokens
   (`--hu-*`) and the ink rungs only. A
   literal fails `AppCss_DeclaresNoColourLiteral` or `AppCss_DeclaresNoFontFamilyLiteral`.
   The one allowed exception is `#blazor-error-ui` in `MainLayout.razor.css`.
4. **Every `var()` names a MudBlazor variable, a registered skin token (`--hu-*`), one of the
   four ink rungs (`--ink-strip`, `--ink-wash`, `--ink-line`, `--ink-field`) or `--font-mono`.** An unresolved
   property resolves to nothing, not to a fallback, and a surface goes invisible
   (`AppCss_UsesOnlyMudBlazorOrSkinTokens` allows exactly those, `AppCss_ReadsOnlyKnownTokens`
   refuses an unregistered `--hu-*`, and
   `ScopedCss_UsesOnlyMudBlazorVariables` holds scoped files to the same list). That includes a
   feature-local custom property such as `--task-ref-dot` (UX-V15).
5. **Sizes, radii and layers use the tokens above**: spacing in px on the 4/8/12/16/24
   scale, widths and heights from the size ladder, typography variables, the five radius steps,
   `--mud-zindex-*`. No literal radius, no rem spacing, no literal font size, and nothing under 11px.
6. **Reuse before you write.** Search `wwwroot/css/` for an existing class before writing a
   declaration block. Truncation is `.truncate`; screen-reader-only text is
   `.visually-hidden`, one global class in `base.css`. Add a utility there the first time a
   second site needs it (UX-R21, UX-R22). The shared look-and-feel classes are global:
   `.card-title`, `.group-label`, `.filter-chip`, `.toolbar-compact` and `.selected-shape`.
7. **No `!important`**, except where it is the only way past an inline style MudBlazor
   writes, with a comment saying so (UX-V16, UX-V22).
8. **Global MudBlazor overrides** (`.mud-button-root`, `.mud-drawer`, `.mud-avatar img`)
   live in one "Global MudBlazor overrides" section near the top of `base.css`, never in a
   feature's section (UX-V17). The overrides set `.mud-link` and selected list items to Accent
   ink and the Selected treatment, because MudBlazor paints them in raw Accent; they also draw
   table row lines on Line, the Disabled state and the dialog close button.
9. **Every feature has a section header** in the form
   `/* ---- Room list: the hover/focus-revealed row actions menu ---- */`. The headers are
   the index. Comment the reason, not the property (UX-V21).
10. **Inline `style=` is for user data only**, such as an Agent's avatar colour. Layout
    and sizes go in a class (UX-V28).

## Adding a Theme or changing a palette

[theme-authoring.md](theme-authoring.md) walks through these steps in full, with the
palette table.

1. Copy the closest skin file under `src/Huddle.App/Themes/Skins/`, give it an id, a label, a
   `mode` and an `order`, and change its palette, tokens and traits. `ThemeCatalog.BuiltIn` is
   built from the files; its first entry (Lumen Light) is the default.
2. Run `SkinLoaderTests`, `SkinContrastTests`, `SkinColourRulesTests` and `ThemeCatalogTests`. Fix the colour (a darker
   or lighter `primary`); a shipped Theme carries no exception. Set `drawerText` and
   `drawerIcon` from the palette's own text colours.
3. Tune it in the Theme lab (`/settings/theme-lab`, Development) and export the JSON.
4. Update [manual-tests/appearance-theme.md](manual-tests/appearance-theme.md) if the
   picker changes.

## How the rules are enforced

| Rule | Enforced by |
| --- | --- |
| No colour or font-family literal in a stylesheet | `AppCss_DeclaresNoColourLiteral`, `AppCss_DeclaresNoFontFamilyLiteral` |
| Only MudBlazor variables, registered `--hu-*` skin tokens, the four `--ink-*` rungs and `--font-mono` in `var()`; every token read, every trait value styled | `AppCss_UsesOnlyMudBlazorOrSkinTokens`, `AppCss_ReadsOnlyKnownTokens`, `SkinTokens_EveryTokenIsRead`, `Traits_EveryValueHasCss`, `ScopedCss_UsesOnlyMudBlazorVariables` |
| Palette contrast and `DrawerText` | `ThemeCatalogTests`; text 4.5:1 on every ground and focus 3:1 in `SkinContrastTests` |
| For every Theme: Quiet ink between Ink 50% and Ink 80% and at least 4.5:1; for authored Themes, signal hue distance at least 30 degrees. There are no exceptions | `ThemeCatalogTests` |
| For every Theme, on resolved colours: Line < Field <= Chrome against Surface; two grounds, and two states, equal or at least ΔE 3 apart; a set fill visible against Surface; the current row's ink is the Accent ink; a lane border is drawn | `SkinColourRulesTests`, with the `SkinColours`, `DeltaE` and `SrgbColour` helpers in `Huddle.Tests.Common` |
| A skin's `padding`, `margin` and `gap` tokens on the 4px scale | `SkinSpacingTests` |
| Type roles, 11px minimum, only the weights 400, 500 and 600, input text at 400, avatar labels, 75ch and 760px | `TypeScaleTests` |
| How a date reads on screen | `HumanDateTests` |
| One hue per meaning: the state, priority and presence mappings | `TaskColorsTests` |
| Status chip contrast, and the one `Chip`'s looks | `ChipStyleTests`, which holds the chip pairs to 4.5:1; `ChipTests` |
| No colour literal in an inline `style` attribute | `RazorComponents_NoInlineColourLiteralInStyleAttribute` |
| `Size.Small` on buttons, icon buttons, chips, toggles and FABs (CV1) | `UiConventionTests` |
| `Variant.Outlined` and `Margin.Dense` on inputs (CV2) | `UiConventionTests` |
| A `role` on every `MudAlert` (CV3) | `UiConventionTests` |
| A `key` on every `Snackbar.Add` (CV4) | `UiConventionTests` |
| No `ReadOnly` on an input; use `Disabled` (CV5) | `UiConventionTests` |
| No `Typo.h1` to `Typo.h4` and no raw `<h1>` to `<h6>` (CV6) | `UiConventionTests` |
| No `= default!` or `= null!` (CV7) | `UiConventionTests` |
| No `Outlined.Add`, `ContentPaste` or `+ ` label (CV8) | `UiConventionTests` |
| No `style=` with a literal value (CV9) | `UiConventionTests` |
| No component-local `DispatchAsync` (CV10) | `UiConventionTests` |
| In a stylesheet: radius, font size, spacing over 2px, `z-index` over 2, `!important`, widths and heights from 17px to 47px, icon sizes and window breakpoints come from the scales (CV11) | `UiConventionTests` |
| No raw `<MudChip` outside `Chip.razor` (CV13) | `UiConventionTests` |
| Everything else on this page | Review, and the checklist below |

`UiConventionTests` (`tests/Huddle.Tests.Shell/Ui/Conventions`) scans `src/Huddle.App` for CV1 to
CV11 and CV13 and fails on any violation not listed in `Baseline/Exceptions.txt`. Every area baseline file is empty;
an exception names the rule, the file, a count and the reason, and only the manager adds
one. What the test can't see stays with review: copy, icon choice, layout and the checklist
below.

## Checklist for a new or changed screen

**Before you start**

- [ ] Find the nearest existing screen in [wireframes.md](wireframes.md) and reuse its
      containers and codes.

**Foundations**

- [ ] Every colour is a palette role or a `Color.*`; none is a literal. Status colours
      come from a mapping function.
- [ ] Every line, hover, selected and drop state uses the treatments under Colour.
- [ ] Every text size comes from the type roles and none renders under 11px; the page has
      one `h1` ([A page](#a-page)).
- [ ] Every spacing, size, radius and `z-index` value comes from the scales above.
- [ ] Every icon comes from the entity or action table.

**Components**

- [ ] Every `MudButton` and `MudIconButton` is `Size.Small`; every chip is the one `Chip`; every
      input is `Variant.Outlined` and `Margin.Dense`.
- [ ] Dialogs, drawers, confirms and action rows follow their anatomy and button order.
- [ ] Every avatar is a `AgentAvatar`; every chip is a Status, Tag or Filter `Chip`; every empty
      state says what to do next.
- [ ] No markup or CSS block was copied from another component; a shared one was used or
      built.

**Accessibility**

- [ ] Each icon-only control has an `aria-label` that names its target; each alert has a
      `role`.
- [ ] Keyboard focus is visible, nothing overrides the ring, and every control, including
      hover-revealed ones and drag handles, is reachable by keyboard and on touch.
- [ ] Any animation has a reduced-motion branch.

**Copy and verification**

- [ ] Labels are sentence case, verbs come from the verb table, counts are pluralised.
- [ ] It looks right in Lumen Light, Lumen Dark and one other dark Theme (Nordic Dark), run with
      `.\run.ps1 -Seed`.
- [ ] It fills its pane and scrolls inside it at a narrow width.
- [ ] The text census (`tools/text-census/`) shows no size under 11px and no contrast
      failure; see
      [After: check it in the running app](UXDesignGuidelines.md#working-on-a-ui-change).
- [ ] [Huddle.UserGuide.md](../Huddle.UserGuide.md) is updated if a Human sees the change,
      and [wireframes.md](wireframes.md) if a container changed shape.

## Known gaps

These are open questions, not decisions. Raise them before relying on them.

- **No global MudBlazor defaults.** MudBlazor 9.12.0 can't set `Size.Small` and
  `Variant.Outlined` once, so they are set per call site and kept by the convention test
  (CV1, CV2) rather than by a wrapper.
- **No narrow-width guidance for the shell.** The drawer is persistent at 240px with no
  collapse, and only the Task list has a narrow-width rule.
- **Contrast tests cover palettes and chips, not other component states.** A tinted
  background such as the Human's bubble or the selected row is not measured. The measured
  baseline was taken on the former Huddle Themes and is no longer kept; the text
  and focus floors that are held are in [theme-authoring.md › The guards](theme-authoring.md#the-guards).

## Keeping this document current

- **The page follows the code.** A rule a test enforces changes in the test, the code and this
  page in one commit; the page never gets ahead of what ships.
- **A component's parameters, path and anatomy go in the
  [catalog](UXDesignGuidelines.md#reusable-parts-catalog)**, not here. A component that sets
  a pattern gets a one-line rule here that links to its catalog row.
- **A Theme value goes in [theme-authoring.md](theme-authoring.md).** Don't write a measured
  Huddle ratio here: a rule on this page is a rung or a relationship.
- **A rendered size changes in `ThemeDefaults`, the Rendered column and `TypeScaleTests`
  together.**

## See also

- [UXDesignGuidelines.md](UXDesignGuidelines.md): the hub for every UI resource, by task.
- [wireframes.md](wireframes.md): the code and name of every UI container.
- [rules.md](rules.md): the binding behaviour rules, including the stylesheet and Avatar rows.
- [MudBlazorDesign.md](../../agents/MudBlazorDesign.md): choosing a component for a spec.
- [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md): the house
  patterns, and facts checked against the pinned package.
- [Huddle.Tabs-Specifications.md](../Huddle.Tabs-Specifications.md): the tab and pane model.
- [language.md](language.md): the words to use in interface copy.
