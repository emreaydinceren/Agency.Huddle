# Decision record

Nineteen dated entries from 2026-09-11 onward, newest first, each recording what
changed and — more usefully — what was considered and rejected. Read it when you are
about to revisit a decision, or when an older Markdown file in this repo
disagrees with current vocabulary and you need the old-to-new mapping.

This is history, not instruction. Nothing here binds you the way [Rules](rules.md)
and [Traps](traps.md) do. Back to the hub: [AgencyTeam.md](../AgencyTeam.md).

**2026-09-25 — Tasks: a Markdown file per unit of work, wired to the same Reply Gate and Budget
chat runs on.**

Thirty-five decisions shaped it ([the Tasks spec](../Huddle.Tasks-Specifications.md) §17 holds
them in full; [ADR-0025](../adr/0025-in-tasks-a-team-is-a-folder-by-convention.md) and
[ADR-0026](../adr/0026-a-change-to-a-task-wakes-its-assignee.md) are the two that needed their
own record). What each one turned down:

- **A separate `Tasks/` root, with the Team as the folder rather than a frontmatter field**
  (D-1, D-2). Rejected: filing Tasks under `Teams/`, which is scanned recursively for Personas
  and would reject every Task as a bad one; and a Team field, which gives a Task an identity to
  protect that its filing already is.
- **One assignee; eight fixed states with four terminal ones; closing doesn't require a
  terminal state and nothing closes automatically** (D-3, D-4, D-5). Rejected: several assignees
  plus watchers; custom states, which static tool schemas can't validate; auto-closing Done,
  which would hide finished work from the Human.
- **Any change wakes the assignee, bounded by a per-Task wake budget** (D-6, D-14). Rejected:
  waking only on assignment; leaving the Room Budget alone to bound it.
- **Room choice walks origin, then {Human, creator, assignee}, then creates one** (D-7, D-12,
  D-13). Rejected: always the direct Room; always using an Archived Room, except as origin;
  inviting a third Agent's actor into the creator's Room.
- **The Change log lives in the file and derives created/updated/closed; Views live in
  `views.json`, with only the last-opened View in browser storage** (D-8, D-9). Rejected:
  frontmatter timestamps; `team.db` or browser storage for Views themselves.
- **The wake-up Message posts as the actor** — the Human for UI edits and outside edits, the
  Agent for tool calls — **so a Room's Budget stays honest** (D-11). Rejected: always posting as
  the Human, which would reset both Budgets and let an Agent loop bypass them.
- **Compare against the last version seen, never a self-write flag; edits made while Huddle was
  stopped are logged at startup but wake no one** (D-15, D-16). Rejected: an "ignore my own
  write" flag; waking at startup.
- **Per-Team id prefixes, stored in `team.db`, never reused; tags can't contain `,` or `;`**
  (D-17, D-18). Rejected: a global `TASK-n` counter; full YAML for tags.
- **Merging by field, with a conflict only when the same field overlaps** (D-19). Rejected: last
  write wins; rejecting any stale save outright.
- **A drag saves at once; the panel waits for Save; the panel and the large editor are one
  component** (D-20, D-21). Rejected: both immediate or both explicit; two separate designs.
- **The words are Close/Reopen, Change log and Make a copy** (D-22). Rejected: Archive, Activity,
  Duplicate.
- **`BitMarkdownEditor` moves to V2; tools are offered to every Persona; a rename rewrites names
  with no log entry or wake-up** (D-23, D-24, D-25). Rejected: adopting the editor in V1;
  granting tools through a Skill; logging a rename.
- **The List is a `MudDataGrid`; MudBlazor components come before custom CSS; `MudExitPrompt`
  plus a message box guard unsaved edits; deleting a View is confirmed through
  `ShowMessageBoxAsync`** (D-26 to D-29). Rejected: a hand-built grouped table; custom classes
  and animations; no guard; an inline Confirm/Cancel swap.
- **Blocked by and Tags are a closable chip set plus a single-value autocomplete** (D-30).
  Rejected: a multi-select autocomplete — 9.10's `MudAutocomplete` has no `MultiSelection`.
- **A Task is referenced by its plain id, which becomes a link only when it resolves and carries
  an upper-case prefix; `#` opens a picker and is then removed, leaving the plain id; links are
  plain `href`s to `/tasks/item/{id}`, and `IsSafe` allows exactly that shape** (D-31 to D-35).
  Rejected: copying a URL, a markdown link, or a title alongside the id; linking anything that
  parses as an id; keeping `#PLAT-0042` as a marker; opening a dialog over the chat; a general
  relative-URL allowance.

**2026-09-22 — Skills, and a built-in Chief of Staff who builds the team and speaks first.**

A new user met three placeholder Personas and an empty Room. Now the app ships a built-in
**Chief of Staff** that greets them unprompted and assembles a team with them, using the
first **Skill**, `team-building`: know-how an Agent reads on demand, the way `get_help` is
read on demand for tools. Nineteen decisions shaped it
([ADR-0021](../adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md) and the
[Skills design](../Huddle.Skills-Specifications.md) §14 hold them in full). What each one
turned down:

- **An Agent proposes and the Human's Approve creates** (D-1). Rejected: a
  `create_teammate` tool guarded only by prompt text, since a prompt is not a guard, and a
  Teammate created in a pending state, which moves the decision away from the
  conversation. The outcome is posted as a Message from the Human that Mentions the
  proposer, so the ordinary Reply Gate wakes it with no new Envelope.
- **`MaxTeammates` is 8 and counts every loaded Persona** (D-2). Rejected: 4, which any real
  library reaches at once, and counting only proposed Teammates, which misses the actual
  cost: processes.
- **The Chief of Staff is permanent, recognised by `_builtin: chief-of-staff`, and checked
  only at startup** (D-3, D-4). Rejected: recognition by Name, which duplicates it on
  rename, and recreation on deletion events, which races file moves and gets both files
  rejected. Its card offers Reset to default instead of Remove.
- **A Candidate carries only descriptive fields** (D-5). Rejected: letting an Agent set
  Model, Effort, Adapter or Skills, each of which spends money or grants tools.
- **One Proposal per Room; the same proposer replaces it** (D-6). Rejected: a queue, which
  asks the Human several questions at once.
- **Partial success with an honest report, no rollback** (D-7). Rejected: all-or-nothing,
  whose rollback deletes Teammates that are already starting.
- **A read-only Settings › Skills tab and a card picker** (D-8). Rejected for V1: a full
  editor like Prompts; the Human writes Markdown in their own editor.
- **Which tools a Skill can grant is a code-defined set** (D-9). Rejected: "any tool a Skill
  lists is gated", under which a hand-written Skill could take a default tool away from
  every other Agent.
- **Shipped Skills are embedded resources, overridden file by file** (D-10). Rejected: C#
  raw strings like `PromptCatalog`, and a generated file beside the binary.
- **`read_skill` and the Skill Index appear only for a Persona holding a Skill** (D-11,
  D-12). Rejected: offering `read_skill` to everyone, which would change every tool list
  and golden, and hiding Skills behind `get_help`, where a model would never know to look.
- **Outcome texts are interface copy, not Prompts** (D-13). Rejected: putting them in
  `PromptCatalog`, which would let a Prompt edit change what the Human appears to have said.
- **`PersonaStore` serialises its own writes** (D-14). Rejected: a lock only in
  `ProposalService`, which leaves the Teammate card racing. Building it showed the plan's
  lock was not enough on its own: the lock must also publish the new index before it is
  released, or a second writer validates against a stale snapshot.
- **`skills` never goes on the `Persona` record** (D-15). Rejected: adding it for
  convenience, since a list member breaks record equality and would restart every Teammate
  on every refresh. A test now fails if anyone tries.
- **The app greets a new Human unprompted, at first start** (D-16, D-17). Rejected: waiting
  for the Human to type first, and greeting on their first *view* of the Room, which needs
  a new Envelope and `ProtocolVersion` 4. An additive `RoomInfo.IsEmpty` needs no bump.
- **The Greeting is triggered by a Prompt, `turn.greeting`, never by a posted Message**
  (D-18). Rejected: posting a Message as the Human, which puts words in their mouth.
- **Onboarding is a fourth file of the Skill** (D-19). Rejected: folding it into
  `SKILL.md`, which every read would then pay for.

**Old-to-new mapping**: ADR-0021's `create_teammate` is `propose_teammates`; a proposed
Teammate is a **Candidate**, never a *draft*; the Chief of Staff's unprompted first Message
is a **Greeting**, never a *welcome*.

**2026-09-22 — A Hook is renamed to a Prompt.**

ADR-0007 named it deliberately and spent the word: *"if executable extension points are
ever wanted at these same sites, they will need a different name, because 'hook' will
already mean a piece of text."* That bet did not pay off — "Hook" reads as an executable
extension point to anyone who has met a git hook, a React hook, a webhook, or this very
toolchain's own `PreToolUse` hooks, which is exactly backwards for a named piece of static,
editable wording. Every `Hook`-prefixed identifier became its `Prompt`-prefixed equivalent,
one for one — `HookCatalog` → `PromptCatalog`, `HookStore` → `PromptStore`, `IHookSource` →
`IPromptSource`, and so on — along with `hooks.json` → `prompts.json`,
`hooks.default.json` → `prompts.default.json`, and the `/settings/hooks` route →
`/settings/prompts`. A full rename, including the persisted file names: no shipped install
exists yet to break, and `App_Data/` is gitignored, so no tracked data was at stake. Nothing
about the design changed — see [ADR-0020](../adr/0020-a-hook-is-a-prompt.md).

**Old-to-new mapping**, for any older Markdown file in this repo still saying the left side:
`Hook` → `Prompt`, `HookCatalog` → `PromptCatalog`, `HookDefinition` → `PromptDefinition`,
`HookIssue`/`HookIssueSeverity` → `PromptIssue`/`PromptIssueSeverity`, `HookRenderer` →
`PromptRenderer`, `HookStore` → `PromptStore`, `HookTiming` → `PromptTiming`,
`HookValidator` → `PromptValidator`, `IHookSource` → `IPromptSource`, `HooksPanel` →
`PromptsPanel`, `HookFieldFactory` → `PromptFieldFactory`, `HookFieldState`/`HookFieldGroup`
→ `PromptFieldState`/`PromptFieldGroup`, `hooks.json` → `prompts.json`,
`hooks.default.json` → `prompts.default.json`.

**2026-09-22 — A Teammate chooses its own Avatar, and it is not part of the Persona.**

An avatar was the initials of a Name on the Theme's `Primary` colour, in two places, with
nothing about it choosable — and `app.css` admitted the gap in a comment: *"The monogram
stands in for Slack's avatar photo."* It is now three optional fields — a label of up to
three characters, an uploaded image, and a background colour — rendered everywhere a
Teammate appears, the transcript included. The Human has one too. See
[ADR-0019](../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md).

**Rejected: `avatar:` in Persona frontmatter.** The obvious home, beside `adapter:`, and the
one that would have let an avatar travel with a copied `.md`. `PersonaSupervisor.NeedsRestart`
is whole-record value equality and `Persona.Text` is the entire file, so picking a background
colour would have stopped a live ACP session and thrown away everything that Agent remembered
— the exact trade [Rules](rules.md) already refuses for Hooks, in the same words. It would
also have put an image file name in a system prompt, and left `JobDescriptionExcludedKeys` one
forgotten line away from leaking `Avatar Color: #4a154b` into model-facing text forever.

**Rejected: an `AvatarKind` enum with a payload.** It needs a discriminator that can disagree
with what it describes, and the rule reconciling them is precedence written a second time. The
shape chosen has no discriminator at all: Image beats Label beats initials, and *all three
absent* is the default — so "initials" is never written down, and an installation that never
opens the new controls has no file at all.

**Rejected: base64 `data:` URIs instead of an endpoint.** Tempting, because it needs no
middleware, no provider, no cache story and no rename cascade. 512 KB of image is ~683 KB of
base64, and on Blazor Server that crosses the SignalR circuit — ten Teammates on `/teammates`
is roughly 6.8 MB on a page that currently paints in one small diff, and nothing is ever
cached.

**Rejected: naming an uploaded image after its Teammate.** It reads better in the folder and
makes a removal obvious. It also needs percent-encoding for a Name with spaces, and
[Known limits](known-limits.md) records that `CON`, `NUL` and `COM1` pass `NameRules` — `CON.png`
is a file Windows will not create. An opaque id means a rename touches no file at all, and
replacing an image yields a new URL, so cache-busting costs nothing.

**2026-09-21 — A Room can be archived or deleted, and archived state is a sibling table.**

A Room was permanent, and that was a stated position rather than an oversight: a Room and
its Transcript are chat facts that outlive the Teammate which created them. But *"removing
a Persona must not destroy a Room"* and *"a Human may never put a Room away"* are two
different claims, and only the first follows from that argument. The sidebar is the app's
primary navigation and it only ever grew — `manual-tests/common.md` had to tell testers to
append a digit to Names they had already used. Archive hides a Room reversibly; Delete
removes it and its Transcript for good. See
[ADR-0018](../adr/0018-a-room-can-be-archived-or-deleted.md).

**Rejected: an `archived` column on `rooms`.** The obvious shape, and silently wrong here —
all DDL is `CREATE TABLE IF NOT EXISTS`, so an existing `team.db` never gains a column and
never says so. `archived_rooms` is its own table, presence-means-archived, following
`PersonaModelStore` and `PersonaEffortStore`. The same trap is why Room auto-naming detects
a custom name by comparison rather than storing a flag. A migration test builds a
pre-feature database by hand and proves the claim, because otherwise the design's central
argument is untested.

**Rejected: freezing an archived Room.** Refusing delivery into it reads tidier, but it
would put a sidebar preference inside the delivery path, beside `ReplyGate` and
`AgentGateway.DeliverAsync`, which is the most load-bearing logic in the app. Archive is a
display filter and `RoomList`/`Chat` are the only code that knows it exists. Accepted cost:
an archived Room can accrue Messages nobody sees until it is unarchived.

**Rejected: a soft delete.** It would add a second hidden state beside archived, with no UI
to reach it, no purge story, and two meanings of "gone". Archive already *is* the reversible
option; making delete reversible too leaves the pair with no distinction and nothing that
reclaims disk.

**Rejected (against the recommendation): reusing an archived 1:1 Room.** Starting a chat
with a Teammate whose Room is archived now creates a fresh Room rather than unarchiving the
old one. Reuse would have preserved
[ADR-0003](../adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md)'s
one-two-Member-Room-per-Agent invariant exactly, and was the recommendation. The repo
owner's call was that archiving a conversation should mean it stays put. The cost is that
unarchiving afterwards yields two identically-named Rooms with the same two Members;
`FindRoomWithExactMembersAsync` gained an `ORDER BY` it never had so the winner is at least
deterministic.
**2026-09-16 — VS Code's bundled Themes are converted once, by hand, and the mapping is
a claim you test with contrast.**

The catalog had one Theme, so nothing proved its extension point worked. It now has
eighteen: `huddle` plus seventeen of the colour Themes Visual Studio Code bundles,
converted at authoring time into ordinary C#, one file per Theme. **No importer ships** —
this is not roadmap item 7, which keeps the harder half, an arbitrary Theme a Human
supplies. See [ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md).

**Rejected: mapping `Primary` to `button.background`.** It is the obvious choice and it
failed sixteen of nineteen Themes against a 4.5:1 text floor — Abyss 1.33:1, Dark High
Contrast 1.14:1. A failure rate that high indicts the mapping, not the Themes.
`--mud-palette-primary` paints the active nav link's *text*; `button.background` is a fill
picked to carry `button.foreground` on top of it. `textLink.foreground` is by definition
legible as text on the same ground, and clears 4.5:1 on every Theme. Total shortfalls fell
from 23 to 7. The lesson generalises: **a mapping is a claim about what a colour is for,
and contrast is how you test the claim.** Every value was extracted correctly both times.

**Rejected: a `LinesDefault` floor at WCAG 1.4.11's 3:1, labelled "focus ring".** It is not
the focus ring — `app.css` paints that with `--mud-palette-text-primary`, deliberately —
and `LinesDefault`'s one consumer is a hover border whose state the same rule also signals
by changing the background. The wrong pair failed `huddle` itself at 2.00:1 and was one edit
from being "resolved" by adding an eighth documented exception, which would have frozen a
false claim about the code into a justification comment. It is now a [Rule](rules.md).

**Rejected: shipping all nineteen bundled Themes.** `Dark (Visual Studio)` and `Light
(Visual Studio)` resolve byte-identically to `Dark+` and `Light+`; upstream they differ only
in `tokenColors`, which this application does not render. Two of them were dropped.

**Rejected: deriving the missing mode for a single-mode Theme.** An imported Theme fills
only its native palette and borrowed the other from `huddle`. Inverting a palette
algorithmically produces colours nobody designed and whose contrast nobody verified.

What it cost: three Themes sit just under the contrast floor and are documented rather than
corrected, the High Contrast pair is not truly high contrast, and a dark-only Theme in Light
mode showed Huddle's palette. What it bought: eighteen Themes, a mapping exercised against
nineteen real inputs, and a contrast guarantee that now covers every Theme instead of one.

> **Amended 2026-09-21 by [ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md).** The
> rejection above stands — nothing derives a missing palette. What changed is that there is no
> longer a missing palette to fill: a Theme carries **one**, borrowing is gone, and the cost
> recorded above as "a dark-only Theme in Light mode shows Huddle's palette" turned out to be a
> defect rather than a cost. Selecting a Theme now selects light or dark with it.

**2026-09-21 — a Theme is a palette, and the light/dark control that could contradict it is
gone.**

Two controls that can disagree will disagree. `{"theme": "solarized-dark", "dark": "light"}` was
two clicks away and rendered Huddle Light under Solarized Dark's name, because the light half of
Solarized Dark was Huddle's. The fix was not to validate the combination but to remove the second
control: `ThemeMode` now decides `IsDarkMode`, `ThemeDefaults` lost its two-palette builder so a
borrowed palette has nowhere to live, and `huddle` — the one Theme with two authored palettes —
became two Themes. The picker became a grouped `MudList`, since MudBlazor 9 has no
`MudSelectItemGroup` and nineteen Themes read better as a list than a dropdown.

What it cost: **the application no longer follows the device's light/dark setting at all.** There
is no System option. Doing that honestly needs a *pair* of authored Themes and a rule for
resolving between them, which is a different feature; six Themes already have a real counterpart,
so a later `Counterpart` field would be enough. What it bought: the defect is unrepresentable
rather than merely fixed, and the JavaScript ADR-0010 had to accept — with it the possible
first-paint flash — went away. See [ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md).

**2026-09-16 — following a Room is a Singleton, not a field on the runner, and that changed what
the Room view can know.**

[ADR-0005](../adr/0005-agent-topologies-are-emergent.md) and [Roadmap](roadmap.md) item 8 both
specified the follow set as a per-Persona `HashSet` shared between `PersonaRunner` and its App
Tools, "built through one `factory.CreateAsync` call". Building item 8 found that they are not
shared through that call: it returns only `(IAgentHost, IAgentSession)`, so the tools it constructs
never reach the runner. The choice was to widen `IAgentHostFactory` — whose own doc calls it
*"purely a test seam"*, and which a fake implements for the whole suite — or to put the state in a
Singleton the tool resolves from DI like any other dependency. The Singleton won: no seam moved,
and there is one source of truth rather than two.

Two things follow, and the second is the interesting one. Self-heal stopped being free — a field
on the runner died with the runner, so `PersonaRunner` now calls `ClearAgent` explicitly after its
handshake. And because a Singleton is in-process, **the Room view can read who is following**,
which falsified the headline argument of [ADR-0012](../adr/0012-a-room-says-why-it-stayed-quiet.md)
four days after it was accepted: that ADR rejected a per-message annotation chiefly because the
view would be *"structurally unable"* to hold `following`. It is not. The rejection survived on its
second argument — a per-message claim is a claim about what a runner *did*, which needs a
`ProtocolVersion` bump — and the ADR now carries an amendment saying so rather than a quiet edit.

The lesson worth keeping is narrower than "check your assumptions": **"never crosses the wire" and
"never knowable" are different claims, and the first does not imply the second in a single-process
application.** ADR-0012 conflated them, and [Rules](rules.md) has been narrowed to state the
wording rule on its own terms instead of resting it on that inference.

**2026-09-15 — the sample clients implement the Reply Gate, reversing a documented decision.**

`DemoAgentHost` and `tools/echo-bot.ps1` now call the same decision `ReplyGate.Decide` makes —
Budget first, then answer-everything at two Members or fewer, Mention-gated above that. Until now
both replied only when `mentioned` was true, and the manual tests recorded that as deliberate:
*"Neither demo agent implements the Reply Gate … That is legal client behaviour by design — the
server labels, the client decides."* That reading was defensible. [ADR-0004](../adr/0004-direct-rooms-reply-without-mention.md)
says the flag rides on the Envelope *"so a Bot **can** apply this rule itself"* — permission, not
obligation.

It was reversed because the cost landed on the person the sample clients exist to serve.
[Product observations](product-observations.md) records a tester spending roughly fifteen minutes
diagnosing a healthy delivery pipeline, because the free agent that should have demonstrated the
Room rule was quietly following a different one. `echo` and `alpha` are what a new install shows
first, and the rule they contradicted is stated in `get_help`, in the system prompt and in the
docs.

The tell was in the test suite rather than the code: **three separate `CRITICAL — DO NOT FILE
THIS` notes** existed to stop testers reporting the silence. A behaviour needing three warnings to
prevent good-faith bug reports is confusing, whatever its provenance.

**Rejected: reverting the change and filing it as a design question instead.** Cheaper by a day,
and it would have restored coherence immediately. But it preserves the fifteen-minute trap and
leaves the demo agents as a worked example that teaches the wrong rule to whoever copies
`tools/echo-bot.ps1`.

**Rejected: changing `DemoAgentHost` only, leaving `tools/echo-bot.ps1` mention-gated.** Smaller,
and defensible on the grounds that a sample external client is a different audience. Rejected
because several manual tests describe the two as a pair, so the split would have to be explained
everywhere they are mentioned, and because the sample bot is the more copied of the two.

What it cost: nine files of manual-test corrections, two tests renamed (ids kept — ids are
append-only), and `ROOMMESSAGING-20` restructured, because it used a plain `hello` in a two-Member
Room as the negative baseline for Mention word-boundary matching. That baseline stopped being
negative, which would have made every boundary assertion in the test pass vacuously. It now
invites a third Member to restore gating as the control.

What it bought, beyond the first impression: the Reply Gate's own decisions became observable in
the **free** tests. `reply-gate-budget.md` previously stated that answer-without-mention *"is ONLY
exercised by a real Persona, in the paid tests"*. In an area of 36 tests where 6 spend money, a
capability moved out of the paid lane. Catch-up buffering stays server-side and remains a paid
concern.

**2026-09-14 — a Theme is a MudBlazor `MudTheme`, and the hand-built Tokens are gone.**

MudBlazor was adopted as the component library, and keeping a second theming system
beside it was rejected as the worst of both: every colour decided twice, in two
vocabularies, kept in step by discipline. `theme.css`, `wwwroot/themes/`,
`ThemeOverrides` and `ThemeTokens` are deleted; every stylesheet reads `--mud-*`.
[ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md) is the decision in full.

**Rejected: keeping the Tokens as the authority and bridging MudBlazor onto them.** It
works — a higher-specificity block can repoint all 77 `--mud-palette-*` variables at the
39 Tokens — but eight of those variables have `-rgb` companions that CSS cannot derive
from a hex, so the colours would have had to be written twice, in two forms. That is the
drift ADR-0009's one-declaration-per-Token rule existed to prevent.

**Rejected: parking collapsed Tokens in unused palette slots.** Seven Tokens collapse
because 39 do not fit MudBlazor's palette one-to-one. `Skeleton` and `TableStriped` were
free and would have preserved the colours, at the cost of palette entries whose names
mean nothing like what they hold.

What it cost: JavaScript is back for the System preference, a flash of the wrong Theme is
possible on first paint, per-Token customisation is gone, and the selected-row colour
changed because MudBlazor computes `primary-hover` rather than exposing it. What it
bought: one vocabulary, no page reload on a Theme change, and roadmap item 7 reduced from
a CSS generator plus a file provider to a JSON-to-object mapping.

**2026-09-13 — a Theme is a stylesheet layered over the tokens, and the choice lives
in a file.**

Roadmap item 6 shipped: every colour and font left the stylesheets for 39 Tokens in
`wwwroot/theme.css`, and a Theme became a CSS file layered over that base.
[ADR-0009](../adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md) is the
decision in full, and [Language](language.md) now defines **Theme**, **Token** and
**Appearance**.

**The wire did not change.** `ProtocolVersion.Current` stays where ADR-0008 left it.
Nothing in this item crosses the pipe, touches `Huddle.Contracts` or reaches an Agent
at all — it is `<head>`, three stylesheets and one config file. Saying so because
almost every entry above this one moved something on the wire, and this one is the
shape that does not.

**Three layers, all targeting plain `:root`.** `theme.css`, then
`wwwroot/themes/<id>.css`, then a server-rendered inline `<style>` of the Human's
overrides — source order decides per token, independently, which is why an override
needs no `!important` and a Theme needs no knowledge of what else is loaded. Rejected:
a self-contained stylesheet per Theme, which resolves a token it forgot to *empty*
rather than to that mode's built-in value, blanking a surface. Rejected too: a fourth
generated file for the overrides, when an inline `<style>` has its cascade position
guaranteed by document order and needs nothing served or invalidated.

**The base layer turned out to be roadmap item 7's per-token fallback, obtained from
the cascade rather than from generator code.** The two built-in Themes are one `:root`
block apiece — `color-scheme` and nothing else — so all 39 Tokens fall through and resolve to
that mode's half of their `light-dark()`. Shipping a Theme that relies on the
fall-through entirely is the cheapest possible proof it works.

**The override key is the Token name**, `{"--font-chat": "Sans"}`. Rejected: a friendly
alias vocabulary, because a second naming layer is a second thing to keep in step — the
exact trap item 7's entry names. Friendliness belongs on the Appearance tab, as a
labelled control that writes the Token name for you.

**Override values are allowlisted, and the allowlist is the sole defence.** Razor
HTML-encodes `@` expressions and CSS does not decode entities, so `"Segoe UI"` would
arrive as an escape and be dropped — the CSS has to be a `MarkupString`, which leaves
nothing downstream to escape it. The Human owns the file, so this is a typo guard rather
than a privilege boundary, and it is `NameRules`' argument exactly.

**No JavaScript, and the selection is per installation.** Rejected: `localStorage` plus
an inline loader, which is per browser and needs a pre-paint script *and* an
`enhancedload` repair; and a cookie, transmitted on every request to carry something only
the page render reads. The accepted cost is a full page load when the Theme changes,
because `<head>` belongs to the server and Blazor's render tree cannot reach it.

**A stylesheet had never loaded, and nothing said so.** `App.razor` linked
`Team.App.styles.css` where the build emits `Huddle.App.styles.css` — a 2026-09-12 rename
leftover — and `@Assets[...]` returns an unresolved key verbatim rather than throwing.
`#blazor-error-ui` had therefore been visible on every page for a month, and
`ReconnectModal.razor.css` had never applied at all. Fixed before any tokenisation, and
recorded in [Traps](traps.md): the framework's not-found behaviour here is to return the
input.

**2026-09-13 — a Turn is visible while it happens, can be stopped, and says when
it fails.**

Roadmap items 3, 4 and 5 shipped together, because all three lived in
`PersonaRunner`'s event loop and the Room view and two of them needed the same
protocol bump.
[ADR-0008](../adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md) is
the decision in full.

**A half-arrived reply is a Draft, not a Message.** [Language](language.md)
defines a Message as one *persisted* unit of text, and a Draft is never written to
the Transcript. Rejected: teaching the append-only JSONL Transcript to revise a
line — the roadmap predicted that keeping deltas in memory would be far cheaper
and it was, `FileChatStore` was not touched at all.

**The wire changed, and `ProtocolVersion.Current` is now 3.** Say both halves:
this is the first entry since the original vocabulary rename to move it, and it
moved only because two new **types** were added — `ToolActivity` and `StopTurn` —
and `[JsonPolymorphic]` is closed, so an unregistered `"type"` throws in
`Deserialize`. New *properties* and new `ErrorCodes` values remain additive and
must never bump; [Traps](traps.md) is explicit that doing so is its own mistake.
`MessageDelta` was already registered at V2 and cost nothing to activate. Every
pipe client moved in the same commit: `PersonaRunner`, `DemoAgentHost`,
`tools/echo-bot.ps1` and the tests pinning literal JSON. Rejected: making the
protocol tolerate unknown types while we were breaking every client anyway — it
would have made this the last forced bump, but it weakens a strict-equality check
that catches real mistakes loudly, and it is its own decision.

**Stop means this Agent, now.** The live Turn ends and everything queued behind it
is discarded. Rejected: stopping only the live Turn, which the roadmap itself
warned reads as broken when five queued prompts then run anyway. A stopped Turn is
**not a failure** — it reports no health state and raises no alert, which has to be
said because the mechanism underneath it is a `CancellationToken` and everywhere
else here that means shutdown.

**Failures needed a model, not a badge.** The roadmap named three; the code had
twenty-one. `PersonaHealth` records them and `PersonaStatusResolver` combines them
with pipe liveness — health outranking connectivity, because any of the runner's
three loops can die and leave the pipe open, so an Agent can be deaf and still
report online. Rejected: pattern-matching the Adapter's own exception text to tell
quota from a network failure from expired credentials — that wording is not ours
and will change, so persistence is reported instead.

**"Plus a posted Message" is reversed**, exactly as ADR-0006 reversed it for the
Budget pause, and for a fourth reason that applies only here: an Agent that failed
to start cannot post anything. It is a strip in the Room view, and it requires a
*reason* rather than merely an unhealthy state — `Acp:Enabled` is false by
default, so listing every not-running Agent would have put a permanent alert in
every Room.

**2026-09-13 — model-facing text is configuration, not source.**

Twenty-two string literals across five files became Hooks: defaults in
`HookCatalog`, overrides in `{DataDir}/hooks.json`, edited at `/settings`.
[ADR-0007](../adr/0007-model-facing-text-is-configuration.md) is the decision in
full. Recorded on [Roadmap](roadmap.md) as item 13, after the fact — it was never
on that list before it was built.

**Defaults live in code, and the shipped JSON is generated from them.** Rejected:
the shipped file *being* the defaults, which admits a state where the file and the
code disagree about what the application does bare. With the catalog as the
authority that state cannot be represented — delete every file and the app still
runs on exactly the text it shipped with. A test pins the two together so the
generated file cannot rot.

**Editing a Hook never restarts a session.** Rejected: restarting so every edit
lands immediately, which would throw away an Agent's conversation memory each time
someone reworded a sentence. The accepted cost is that a `NextSession` Hook is
silently inert on a running Teammate, paid for with a badge on exactly those
fields — a system prompt is fixed at `session/new` and no design choice changes
that.

**`{{name}}` and not `<name>`.** `get_help` sends the model the literal line
`"[Room: <name> (id: <id>)]"` as documentation, which an angle-bracket syntax
would have silently eaten. A regression test pins that exact line.

**A Hook is a template, not an event.** The word is the repo owner's and the
panel is named for it, but nothing executes and nothing subscribes. Recorded
because it spends the word: executable extension points at these same sites will
need a different one.

**Left open, deliberately.** [Roadmap](roadmap.md) item 9 proposes a
*per-Persona* channel for the same tool surface this made globally configurable.
Both readings are coherent; having both without deciding which wins where is not.
The ADR states the options rather than inventing a design for an item nobody has
started.

**2026-09-12 — a Room has a Budget for agent replies, and the Human is asked
before it is raised.**

Two Agents that Mention each other used to reply until the app was stopped —
recorded in ADR-0004 as a deliberate omission, not an oversight. A Room now takes
only so many agent-authored Messages between one Human Message and the next. This
is [Roadmap](roadmap.md) item 2, delivered;
[ADR-0006](../adr/0006-a-room-has-a-budget-for-agent-replies.md) is the decision
in full.

**`ChatService` owns the only counter, and labels the Envelope with it.** The
original plan had `ReplyGate` keeping a client-side streak of its own. That cannot
survive the Human being able to extend a Budget: the grant lives in `ChatService`,
so a runner comparing against its own configured default would decline the
re-delivered Message and Continue would silently do nothing. Two counts on the
Envelope keep the comparison client-side and leave the runner holding no state.

**The pause is a question in the Room view, not a Message in the Room.** Rejected:
a `system` sender, which needs a third `UserKind` — a SQL `CHECK` constraint
wanting a fresh `App_Data`, and a wire enum inside `MemberInfo`; posting it as the
capped Agent, which would need exempting from the cap it announces; and posting it
as the Human, which would reset the Budget it was reporting. Continue grants one
more Budget and asks again at the next threshold, rather than lifting the cap for
the rest of the run.

**Continue re-delivers the Room's last Message**, because a Turn only ever begins
with a delivered Message and raising the allowance alone wakes nobody.

**The wire changed, additively, and `ProtocolVersion.Current` stays `2`.** Say both
halves: `MessagePosted` gained `agentMessagesSinceHuman` and `budget`, and
`ErrorCodes` gained `budgetExhausted`. An older client parses the line and ignores
what it does not know, which is the precedent ADR-0004 set when `Members` was
added — so bumping the version here would break every client for nothing.
[Traps](traps.md) now records that in both directions.

**2026-09-12 — a Persona's identity moves into its frontmatter, and Teams
become a field.**

The `personas/` folder became `Teams/`, enumerated recursively, and a Persona's
Name stopped being its filename. `name`, `title` and `alias` are now required
frontmatter; `teams` is optional. This is [Roadmap](roadmap.md) item 10,
delivered, plus the Teams concept item 10 did not cover.

**Team membership is a field, not a folder.** Sub-folders under `Teams/` exist
purely so a human can file things, and the code reads no meaning into them at
all — a Persona in `Teams/Household/` whose frontmatter says `teams: Business`
is on the Business team, and moving that file changes nothing. The alternative,
folder-as-team, was rejected on two counts: a Teammate could then only belong to
one Team, and renaming a folder would silently re-home everyone inside it.

**The SQLite keying went the opposite way from item 10's own recommendation,
for a reason item 10 did not have.** Item 10 recommended keeping
`persona_models` and `persona_efforts` keyed by *filename*, treated as a stable
internal id, so that editing `name:` would cost nothing. That reasoning holds
only while a file's path is immutable. Once Team sub-folders exist, the path is
something a human is *expected* to change, and a path-derived key would turn a
filing decision into a silent identity change — the exact no-op this feature
promises. Keying on the frontmatter `name` instead makes the move free and needs
no schema change, since `PRIMARY KEY(persona_name) COLLATE NOCASE` was already
the right comparison for a display Name. The accepted cost is the one item 10
was trying to avoid: editing `name:` is a rename, and `Update` has to move both
rows by hand or the Teammate silently loses its Model.

**Both sides of a collision are rejected, not one winner.** Two Personas sharing
a Name or an Alias, or one Alias equalling another's Name, takes out every file
involved. "First one wins" was rejected because the loser then cannot be fixed
by editing it — the edit simply does nothing, with no feedback anywhere. Loud
beats tidy. Comparison is case-insensitive to match `COLLATE NOCASE` and
`MentionParser`, which also closed the `Jarvis`/`jarvis` collision
[Known limits](known-limits.md) had recorded as unprevented between Personas.

**Required fields replaced "no schema, ever".** `PersonaStore`'s docstring used
to promise a plain-prose file with no frontmatter would work. That promise is
gone: identity has to come from somewhere, and a file that cannot supply it is
now a rejected file, listed on `/teammates` with its path and reason. Silently
ignoring it was rejected — a Teammate that does not appear, for no visible
reason, is the worst version of this.

**`alias` being required was challenged during the build and confirmed.** The
case against: `name` and `title` are obviously required, but a required `alias`
forces a handle to be invented for every Persona and creates a second namespace
that has to stay collision-free against every Name forever — all for
convenience, when `@Jarvis` already works. Making it optional, and unique only
when present, was one line. The repo owner kept it required. Treat this as settled
rather than an oversight; the migration script suggests a collision-free alias
per file precisely because the field cannot be left empty.

**`Title`/`Alias`/`Teams` were deliberately kept OFF the `Persona` record.** They
live only in `PersonaIndex`. `Persona.Text` already contains them, so
`PersonaSupervisor`'s whole-record value-equality diff still restarts a session
when any of them changes, without anyone having to remember to extend a
comparison — and `PersonaRunner`, `DotAcpAgentHostFactory` and
`SystemPromptComposer` needed no changes at all.

**Splitting `teams` on commas is done at the consumer, never in the parser.**
`PersonaFrontmatter` still refuses to read a comma-separated scalar as a list,
because `role: 'Router, triage, and cross-workstation continuity'` is real and
splitting it would shred a sentence into fields. Only the `teams` key is split,
after the generic parse. The accepted cost: a Team name can never contain a
comma.

**The wire did not change.** `Hello(Name, Description)` keeps its shape and
`ProtocolVersion.Current` stays `2`; only where the `Name` value comes from
moved.

**2026-09-12 — the product is Agency.Huddle; the code stays `Team`.**

"Team" is a generic noun one word from Microsoft Teams, so it fails as a name
anyone can search for. The product joins the Agency brand family in the dotted
grammar the sibling repo already uses (`Agency.Harness`).

**Ten names were checked; every descriptive one was taken.** Rooms, Council,
Roundtable, BrainTrust, BoardRoom and HQ are each already the name of a
multi-agent product or a large neighbour — GitHub's *Agent HQ* is one letter from
"Agency HQ". The one collision-free candidate, Cabinet, was declined on register.
`Agency.HQ` was chosen on 2026-09-11 and set aside the next day for the same
reason: it reads as uptight. Huddle is warmer and names the act — what the Chief
of Staff does when it pulls the CMO and CFO into a Room — rather than the
building or the staff.

**Huddle's collisions were known and accepted.** Slack Huddles is a named feature
of the product this one is shaped like, and `mcp-huddle` is an existing MCP server
with rooms, JSONL storage and a dashboard for humans to watch. This is a
non-commercial project; the repo owner weighed both against warmth and chose
warmth. The post's argument — the human in every Room by schema, a one-line reply
rule, verbs not stages, no engine — is what has to carry the differentiation.

**What changed: the marketing surface only.** `README.md`, the hub's title,
`docs/why-agency-huddle.md`, `CONTEXT-MAP.md` and the domain-context page.

**What did not change: two identifiers, deliberately.** The `mcp__team__` tool
prefix is model-facing prompt text in 12 code files and every persona, a test pins
it, and a model never reads it as a brand. The `Team:` configuration root is a
breaking change for any running install. Neither has a brand benefit that pays for
its cost. Glossary terms are untouched: *Teammate* and *Team Directory* are
domain words.

**Namespaces were the separate decision, and it was taken on 2026-09-12.** The
root is now `Agency.Huddle.*` across all six projects. The wire derived nothing
from them — every discriminator is a string literal — and the predicted
verification held exactly: warnings-as-errors gave a 0-warning rebuild of both
solutions, and both suites passed unchanged at 309 and 251 (8 E2E skipped).
Projects, assemblies, folders and the two solution files stayed `Team.*` at the
time; `<RootNamespace>` in each `.csproj` carried the split, and
`AcpReferenceTests` pinned the assembly name. They were renamed to `Huddle.*`
afterward, and the two solutions were collapsed into the single `Huddle.slnx`.

Two things the rename did cost, neither of which the compiler could see. The
`Logging:LogLevel` filters are category prefixes and had to move with the
namespaces or degrade silently — see [Traps](traps.md). And the open argument
from the original entry survives: `Huddle.Acp` is a chat-agnostic library that
arguably should not carry the product's name. It was renamed with the rest for
consistency inside one solution; reversing that root alone stays a
self-contained change if the library is ever extracted.

**The wire did not change.** `ProtocolVersion.Current` stays `2`.

**2026-09-11 — a Persona's frontmatter becomes its job description.**

`mcp__team__list_agents` told a caller *who* exists and nothing about what
each teammate does — `Chief of Staff.md` even told its own reader to work
around that by reading frontmatter off disk by hand, since the tool couldn't.
Every Persona file already carried a YAML frontmatter block (`role`,
`summary`, `consult_when`, ...); nothing in the code read it.

**What changed: `PersonaFrontmatter`.** A hand-rolled, dependency-free parser
(ported from a sibling repo's `Agency.Harness.Markdown.FrontmatterParser`, not
referenced across the repo boundary) splits a Persona's optional leading
`---`-delimited block into ordered fields. `list_agents` composes them into a
"Key: Value" line per non-`_`-prefixed field, title-cased, in file order —
`_`-prefixed fields are reserved for future programmatic use and never shown.

**Where the port diverges, and why.** The source parser guesses a scalar is a
list from commas, spaces or brackets when a caller asks for a named field as
one. Team's real frontmatter has quoted scalars *containing* commas — `role:
'Router, triage, and cross-workstation continuity'` — so that guess would have
split a sentence into fields. A field counts as a list here only when it
actually uses YAML block-list syntax. The port also strips YAML's `'...'`/
`"..."` quoting and unescapes `''` to `'`, neither of which the source parser
does, because Team's persona authors quote almost every scalar.

**A second bug came free with the parser.** `PersonaRunner.BuildDescription`
took the first line of a Persona's raw file for the pipe `Hello.Description`
— always `"---"` once frontmatter existed, silently, since nothing validated
it. It now reads the first line of `PersonaFrontmatter.Parse(...).Body`
instead.

**`PersonaStore`'s docstring no longer claims "no frontmatter and no
schema."** That was already false the day frontmatter first appeared in
`personas/*.md`; nothing enforces a schema still — no required fields, no
allowlist, unlike the source repo's `PersonaParser` — so a Persona with none
at all still works exactly as before.

**The wire did not change.** `ProtocolVersion.Current` stays `2`. Frontmatter
is read only by `PersonaFrontmatter`; `Persona.Text` — frontmatter included —
still becomes the whole system prompt, unchanged. The Model's decision to live
in SQLite, not front matter, below, is untouched: this is informational
metadata, not schema to extend.

**2026-09-11 — the system prompt names one tool, and that tool names the rest.**

Every App Tool added so far had been added twice: once to the factory, and once
to the prose in `SystemPromptComposer`. That is a list with no compiler behind it,
paid for on every Turn of every session, and the second copy is the one that
silently goes stale.

**What changed: `mcp__team__get_help`.** The prompt now opens with a canned
orientation — this is a chat application, call `get_help` when unsure — and the
tool answers with the Room model, the Reply Gate, the Mention rules and the whole
catalog, each tool named with its `mcp__team__` prefix and its own `Description`.
The catalog is built *from the tools the factory registered*, so a tool added
there documents itself. `GetHelpTool` therefore takes the other tools rather than
sitting among them, and is constructed last.

**The old block was kept, deliberately.** The fixed text below the Persona still
states the chat rules inline. Progressive discovery is then an amplification
rather than a precondition: an Agent that never calls a tool still behaves
correctly, and a model that does call `get_help` gets the detail that would be
wasteful to send every Turn. The cost is a duplicated rule, recorded in [Known
limits](known-limits.md).

**And an Invitation stopped being the Human's alone.** `InviteAsync` had existed
since the `/invite` command and had exactly one caller. It now has three:
the command, **Add teammate** on the Room header (`InviteTeammate.razor`), and
`mcp__team__invite_agent`. Nothing about the Invitation itself moved — the point
is that no door reimplements it.

**That exposed a real gap.** `invite_agent` and `post_message` both take a room
id, and **nothing in a Turn had carried one**: the prompt label was
`[Room: name]`. Both tools could therefore only ever reach a Room the Agent had
created itself in that session, and the failure was invisible, because a wrong id
comes back as ordinary tool error text. The label is now
`[Room: name (id: …)]`, and `get_help` says that is where a room id comes from.

**The wire did not change.** `ProtocolVersion.Current` stays `2`. App Tools are
settled between `Huddle.App` and the adapter over MCP; the pipe never learns a tool
exists.

**2026-09-11 — a Persona chooses its Model, and the list is discovered.**

Every Persona ran on whatever the adapter defaulted to, so every Agent cost the
same and reasoned the same. Giving a Persona a Model made it the *second* thing a
Persona is — the first time that record held anything but a name and a body.

**Where it is stored: SQLite, not front matter.** A `.md` a user can drop in has
nowhere to put a Model without inventing a schema, and `PersonaStore`'s own
docstring promises there is none. The table is new rather than a column on
`users` for a blunt mechanical reason: the schema is created with
`CREATE TABLE IF NOT EXISTS`, which **silently ignores an added column** on a
database that predates it. A new column would have forced everyone to delete
`App_Data`; a new table does not. A test builds a pre-change database and then
opens it with `PersonaModelStore` to pin exactly that.

**Where the list comes from: the agent, not a constant.** ACP has no
`models/list`; a catalog only arrives as a side effect of `session/new`
(`docs/acp/session-config-options.md`). So `ModelCatalogProbe` spawns a throwaway
adapter, handshakes, reads `configOptions`, and stops **before any Turn** — a
process, but no tokens. That is what makes it acceptable to run while
`Acp:Enabled` is `false`. The alternative, a hardcoded list of model ids, ages
the moment a new model ships and can offer something the installed adapter cannot
actually run. Every value in the picker provably came from the agent itself.

**How it is applied: `session/set_config_option`.** The vendored adapter
advertises no `models`/`SessionModelState`, so `session/set_model` does not exist
for us. A Model absent from the catalog is a logged warning and nothing more —
the session starts on the agent's default. A stale stored value must never be
able to brick a Persona.

**The restart came for free, after one correction.** The first design added a
`ModelsChanged` event, because `PersonaSupervisor` learns about Persona edits from
a `FileSystemWatcher` and a database write fires no filesystem event. That was
unnecessary. Putting `PersonaModelStore` behind `PersonaStore` made `Get` the
single place where file and database join, and `PersonasChanged` — already
raised, already subscribed by both the supervisor and the page — carried it. The
supervisor's diff widened from `Text` to the whole `Persona` and nothing else
moved. `Update` deliberately takes text and Model *together* and has no default
for the Model: two writes would raise two events and spawn two `node` processes
per save, and a default would let a surviving two-argument call silently wipe a
stored Model.

`PersonaStore` did gain a database dependency, which is a real loss. The property
that actually mattered survives intact: it still has no reference to
`ITeamDirectory`, so removing a Persona still cannot cascade into Agents, Rooms
or Transcripts.

**The wire did not change.** `ProtocolVersion.Current` stays `2`. Nothing about a
Model crosses the pipe — it is settled between `Huddle.App` and the adapter over
ACP, and the chat surface never learns what is behind an Agent.

**2026-09-11 — a Name is a display name, and the card that shows it.**

Two changes with one cause. `^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$` forced
`chief-of-staff` on something a person would write as `Chief of Staff`, which
made a Teammate read as a record rather than as somebody. Slack's own directory
was the reference: people there have names with spaces, and clicking one opens a
profile.

Allowing the space cost far more than the regex, because a Name is three things
at once — a wire identity, a Persona's filename, and a token parsed out of
message text. The third is the one that broke. **No pattern can find the end of
`@Emily Lee`**: it is the same characters as `@Emily` followed by the word "Lee",
and only the Room's membership can say which was meant. So `MentionParser` stopped
reading a Mention's shape out of the text and started matching Member Names
against it, longest first — the member list it had always been handed became the
authority it had never been asked to be. `/invite` had the same bug in miniature
and now captures everything after the command, leaving `InviteAsync` to reject
what the Team Directory does not hold.

A quoting syntax — `@"Emily Lee"` — was considered and rejected. Nobody types it,
Slack does not ask for it, and it would have put the burden of the ambiguity on
every writer instead of resolving it once.

The filename half stayed strict. Leading, trailing and doubled spaces are
refused, because Windows silently strips a trailing space and `coo ` would
otherwise be one file presenting as two Teammates. Chasing that turned up an
older hole: `$` in .NET matches before a trailing newline, so the previous rule
had accepted `"coo\n"`. Both guards now anchor with `\A` and `\z`.

With Names reading as people, the `/teammates` page no longer could. A flat list
with an inline `<textarea>` and an add form pinned to the bottom is file
management, so it became a grid of tiles opening one card — `TeammateCard`, with
viewing, editing and creating as three modes of a single layout rather than three
layouts that would drift. The parent holds the draft text so that switching
Teammates cannot show you the previous one's.

Renaming was deliberately left out; see [Known limits](known-limits.md).

**The wire did not change.** `ProtocolVersion.Current` stays `2`. Widening the
set of accepted Names is backward compatible — every Name an old client could
send is still valid, `tools/echo-bot.ps1` never validated one locally, and the
`Mentioned` flag every Agent relies on has always been computed server-side.

| Old | New |
| --- | --- |
| `^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$` | `\A(?=.{1,64}\z)[A-Za-z0-9](?:[ ]?[A-Za-z0-9_-])*\z` |
| Mentions found by regex | Mentions resolved against the Room's Members, longest Name first |
| `/invite` captured a Name by shape | `/invite` captures the rest of the line |
| `^` and `$` in `NameRules` | `\A` and `\z` |
| A list, an inline edit form, a bottom-of-page add form | A tile grid and one `TeammateCard` in three modes |

**2026-09-11 — one vocabulary across UX and code.**

The glossary that preceded this document banned the word "agent", yet `agent`
appeared 116 times in `src/` — more often than `Bot` (92) — because the App Tool
contract needed a word for *a Bot or a Persona* and nothing supplied one. A
prohibition that removes a word without replacing the concept does not get
obeyed; it gets routed around. Alongside that, the `/agents` page managed Persona
files, `Sandbox` named a directory that had to be documented as "not a jail", and
the UI still said "Direct Room" at a newcomer.

The resolution makes **Teammate** the umbrella, which frees **Agent** to carry
the single meaning the tool contract already gave it. `list_agents`,
`create_room` and the `agents[]` parameter were therefore correct as they stood
and were not renamed — no system-prompt edit, and no re-derivation of the
`mcp__team__` naming rule.

| Old | New |
| --- | --- |
| Bot | Agent |
| — | Teammate (new umbrella: the Human plus Agents) |
| Agent Host, Persona Bot | Agent, or `PersonaRunner` for the runtime |
| Agent Session | session |
| Direct Room, Group Room | Room (behaviour follows from member count) |
| Persona Library | the Persona library, lower case; `PersonaStore` in code |
| Sandbox, `Acp:SandboxDir` | Work Dir, `Acp:WorkDir` |
| `IDirectory`, `SqliteDirectory` | `ITeamDirectory`, `SqliteTeamDirectory` |
| `Team:DemoBot:*` | `Team:DemoAgent:*` |
| `/agents` page | `/teammates` page |

The wire and the database changed with the code: `welcome.botId` is now
`welcome.agentId`, the `kind` value `"bot"` is now `"agent"`, and
`ProtocolVersion.Current` went from `1` to `2`. Existing `App_Data` was discarded
rather than migrated.

Two things were deliberately left alone. **`Huddle.Acp` keeps ACP's vocabulary**,
for the reason given in [Two bounded contexts](../AgencyTeam.md#two-bounded-contexts). And
**`tools/echo-bot.ps1` keeps its filename**, because it is referenced by path from
documents that are not being edited; a file path is not vocabulary.
