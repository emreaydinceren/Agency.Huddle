# Gitea batch A — revised after Wave 1 merged

Revised 2026-09-15, after PR #35 and PR #34 merged to `main` (`23c757c`).
**Nothing posted yet.**

Thirteen operations: **9 closes**, 1 duplicate merge, 3 comments on issues that stay open.

Every closure below was verified against `origin/main` by reading the merged file, not
inferred from a branch name.

---

## Closes — fixed and verified on `origin/main`

### 1. CLOSE #16 — reconnect modal shows two state paragraphs while retrying

> **Fixed on `main` (`23c757c`) by PR #34.**
>
> `ReconnectModal.razor.css`'s first-attempt rule is now exclusive of the retrying state:
> `#components-reconnect-modal.components-reconnect-show:not(.components-reconnect-retrying) .components-reconnect-first-attempt-visible`.
> Blazor adds `components-reconnect-retrying` alongside `components-reconnect-show` rather than
> replacing it, so both selectors matched at once and nothing hid the first paragraph.
>
> Two latent problems in the same rule block went with it. Lines 14 and 17 selected the **modal
> element itself** rather than a child paragraph, forcing `display: block` on the `<dialog>`
> whenever either class was present — a value that survives `close()` for as long as the class
> lingers. Both came from the Blazor template scaffold and had been unchanged since the initial
> commit, so their removal is a deliberate deviation from stock, recorded in the commit message so
> a future scaffold diff does not reinstate them. They are redundant: `ReconnectModal.razor.js`
> drives visibility with `showModal()`/`close()`, so the dialog carries `[open]` throughout and the
> UA's own `dialog[open] { display: block }` already applies.
>
> Re-confirming the correction from this thread for the record: this was never a missing scoped
> stylesheet. `SHELLNAV-26` and `PIPEEXTERNAL-34` both name a 404 on a `ReconnectModal` CSS file in
> their *Fail if* lines, and that attribution is wrong.

### 2. CLOSE #20 — `Open` silently does nothing when no app is registered for `.md`

> **Fixed on `main` (`23c757c`) by PR #34.**
>
> `OpenInEditor` now treats a `null` return from `Process.Start` as a failure and reports it in the
> existing `Could not open '<Name>': …` shape. `Process.Start` returning `null` is documented
> behaviour whenever no new process was started, so the check is right regardless of file
> association. Both existing `catch` blocks are untouched, so the missing-file path
> (`Persona 'Nova' does not exist.`) still reports as `TEAMMATECARD-31` proves it should.
>
> The method lives in `Components/Shared/TeammateCard.razor` now, having moved out of
> `Teammates.razor` during the MudBlazor migration.
>
> **One honest gap:** the null branch has no automated test. `Process.Start` is not injectable
> here, and reaching it for real would launch an OS process from the test suite. A test-only seam
> was deliberately not added. If that branch is ever worth pinning, the dependency wants to be a
> real injected abstraction rather than a hook — the same argument `CSharpPrinciples.md` makes for
> `TimeProvider` and `IFileSystem`.

### 3. CLOSE #24 — composer info/error line is not cleared on a Room switch

> **Fixed on `main` (`23c757c`) by PR #34.**
>
> `Composer.razor` now tracks a `loadedRoomId` and nulls `errorText`/`infoText` in
> `OnParametersSetAsync` when the Room changes — the same guard `InviteTeammate.razor:86-100`
> already had, which is what this issue proposed. The fix is in the component rather than an
> `@key` on `Chat.razor`, so it matches how the invite panel already solves it.
>
> This issue also asked for test coverage, and both halves exist now: a bUnit regression test
> (`ChatPageTests.Composer_StatusLine_DoesNotFollowARoomSwitch`) and a manual test,
> `INVITEROOMS-32`, cross-referenced from `INVITEROOMS-14`.

### 4. CLOSE #25 — keyboard focus ring is invisible on links and buttons

> **Fixed on `main` (`23c757c`) by PR #34.**
>
> `app.css` now declares a focus ring that beats MudBlazor's reset on source order:
>
> ```css
> a:focus-visible,
> button:focus-visible {
>     outline: 2px solid var(--mud-palette-text-primary);
>     outline-offset: 2px;
> }
> ```
>
> Two things this issue got right and one it could not have known.
>
> Right: the cause was MudBlazor, not the Theme, and the fix had to beat it. `a:focus-visible` and
> `button:focus` are both specificity (0,1,1), so the tie goes to whichever loads last, and
> `app.css` loads after `MudBlazor.min.css` — no `!important` needed. `.mud-button-root`'s reset is
> only (0,1,0) and loses outright.
>
> Also right, and load-bearing: re-verification found there was **no** substitute indicator either —
> on a focused button, `boxShadow: "none"`, `backgroundColor: "rgba(0,0,0,0)"`, `::before` content
> `none`, `::after` transparent. So nothing was being suppressed that could be restored; a ring had
> to be added.
>
> What could not have been known then: the colour has to come from `--mud-palette-text-primary`,
> **not** `--mud-palette-primary`. MudBlazor's dark palette *darkens* Primary, so `#5e2b60` on
> `#1b1b1f` measures **1.63:1** — under WCAG 1.4.11's 3:1 floor for a focus indicator, i.e. present
> in the DOM and invisible in exactly the theme this was reported against. `text-primary` measures
> 13.79:1 dark and 15.91:1 light. A test pins the colour choice, not merely the rule's existence.

### 5. CLOSE #27 — an agent disconnecting from the pipe is never logged

> **Fixed on `main` (`23c757c`) by PR #34.**
>
> `AgentConnection.RunAsync`'s `finally` now logs at Information alongside `Unregister`, in two
> shapes depending on whether a name was ever assigned — `Register` happens after the
> hello/name-validation early returns, so `this.Agent` can legitimately be null there:
>
> - `Agent connection {ConnectionId} for agent {AgentName} disconnected.`
> - `Agent connection {ConnectionId} disconnected before registration.`
>
> The existing `IOException` Warning (`Agent connection {ConnectionId} ended.`) is kept, so a
> faulted connection stays distinguishable from a clean goodbye.
>
> One deliberate consequence: a faulted connection now produces **both** lines, because the
> `finally` always runs. That is the point — every teardown now yields exactly one `disconnected.`
> line, so disconnects are countable, with the Warning as supplementary cause. That countability is
> what #33 needs.

### 6. CLOSE #29 — multi-line hooks can never return to "unmodified" by typing

> **Fixed on `main` (`23c757c`) by PR #34 — and the root cause was broader than this report, in a
> way worth recording.**
>
> This issue's analysis was right and the repo's own documentation was wrong. `docs/agencyteam/traps.md`
> asserted that *"Raw string literals normalise line endings to `\n`, whatever the file has… So
> `HookCatalog`'s defaults, and therefore every prompt sent to a model, carry no `\r` on any
> platform. Verified by serialising the catalog and finding zero `\r`."* That is false, and has been
> corrected.
>
> Checked directly against the compiled assembly — `Huddle.App.dll` contains the **CRLF** form of
> `systemPrompt.orientation`, `getHelp.intro` and `systemPrompt.chatRules`, and not the LF form.
> Raw string literals preserve their source file's line endings.
>
> **The consequence is wider than the badge.** Line endings depend on how the repo was checked out:
> `git cat-file blob main:src/Huddle.App/Hooks/HookCatalog.cs` stores 0 CRLF pairs and 395 bare LF,
> Windows converts on checkout via `core.autocrlf`, and the Linux CI container does not. So **every
> prompt sent to a model carried `\r` on a Windows build and not in CI**, and no test could see it —
> `PromptGoldenTests` and `HookDefaultsFileTests` both normalise line endings on both sides before
> comparing.
>
> The fix is therefore at the boundary rather than at the comparison: `HookDefinition.Default`
> normalises to `\n` once at construction, so model-facing text no longer depends on the checkout.
> `HookFieldFactory.ToFieldState` and `HookStore.ApplyEdit` also normalise, defensively, because a
> hand-edited `hooks.json` can still arrive with CRLF from a text editor. `hooks.default.json` had
> silently drifted to LF against a CRLF catalog and is regenerated; the anti-drift test now pins
> line endings explicitly instead of normalising them away.
>
> `HOOKSSETTINGS-10` step 7 is satisfiable again.

### 7. CLOSE #23 — Edit's Model select jumps to 'Default (recommended)'

> **Fixed by the MudBlazor migration (PR #35), verified 2026-09-15.**
>
> The mechanism this issue describes no longer exists: the raw `<select value="@this.Model">` is now
> `<MudSelect Value= ValueChanged=>`. Re-tested under the original conditions — a teammate stored as
> `sonnet`, a model the catalog **does** advertise, which the thread established is the only case
> that reproduced — opened on the first card open of a cold app run and sampled every 250 ms for 12 s.
> The value reads `sonnet` on the first sample and every sample after; it never becomes
> `Default (recommended)`.
>
> **What remains is a different defect, filed separately:** for the ~11 seconds a cold catalog takes
> to land, the dropdown opens with a short list that then grows, and the control displays the raw id
> `sonnet` before switching to the label `Sonnet`. Same window, different fault.
>
> One note for anyone re-testing: `MudSelect` renders no `<option>` elements, so an `option`-based
> oracle reads zero throughout and tells you nothing. Count `.mud-list-item` in the open popover.

### 8. CLOSE #28 — every destructive button renders in plain text colour

> **Fixed by the MudBlazor migration (PR #35), verified 2026-09-15.**
>
> The source-order tie this issue describes cannot occur any more: `.teammate-card-action` no longer
> exists. All four destructive controls are `<MudButton Color="Color.Error">` — `ResetAllControl.razor`
> (`Yes, reset everything`, `Reset all to defaults`) and `TeammateCard.razor` (`Confirm`, `Remove`).
>
> Re-tested on `/settings`: with `hooks.json` empty, `Reset all to defaults` renders disabled at
> `rgba(255, 255, 255, 0.26)`; after one character is typed into a hook field it enables and renders
> in the error colour, visibly distinct from the plain per-field `Reset` beside it.
>
> The orphaned `.teammates-danger` rule has since been deleted (PR #34), along with the stale
> reference to it in `ResetAllControl.razor`'s comment.

### 9. CLOSE #26 — reconnect modal is white with near-white text in the dark theme

> **Fixed, in two parts.**
>
> The contrast defect is gone as of the MudBlazor migration (PR #35). `f2cfdec` made MudTheme the
> single palette, so the app's token layer and MudBlazor's palette can no longer disagree —
> `ReconnectModal.razor.css` reads `var(--mud-palette-surface)` / `var(--mud-palette-text-primary)`
> where it used to read `var(--surface-raised)` / `var(--text-primary)`. Measured with dark mode on:
> `rgb(27, 27, 31)` on `rgb(230, 230, 234)`, roughly 14:1 against the ~1.06:1 originally reported.
>
> That measurement exposed a residual this issue predicted: MudBlazor's
> `#components-reconnect-modal{background-color:var(--mud-palette-background) !important}` still beat
> the app's `--mud-palette-surface`, and the dark palette's `Background` (`#1b1b1f`) is
> byte-identical to the page body behind it — so the modal was readable but had **no visible edge at
> all**. Fixed in PR #34 by matching the `!important`, which is exactly what this issue proposed.
>
> **A note for anyone re-testing this from the steps above: they are now stale.** ADR-0010 left
> exactly one theme, id `huddle`, with light/dark as a separate key. `{ "theme": "huddle-dark" }` is
> an unknown theme id — logged as a warning, ignored, leaving the app in light mode, so the check
> silently measures nothing. Use `{ "theme": "huddle", "dark": "dark" }`.

---

## Duplicate merge

### 10. Comment on #17, then CLOSE #30

**Comment to add to #17** (which stays open — the fix is not written yet):

> **`PERSONALIFECYCLE-17` (issue #30) is the same defect from the persona-lifecycle area.**
> Recording its evidence here and closing that issue as a duplicate.
>
> #30 could not distinguish two hypotheses and said so: *"either `session.Models` is not carrying
> what `ApplyModelAsync` resolved against, or the `Degraded` status is raised and then overwritten
> by a later `Online` before it can reach any surface."* This issue establishes the second with line
> numbers, and a code read confirms it.
>
> Three pieces of evidence from #30 worth keeping:
>
> - A `MutationObserver` watching `/teammates` from page load through Nova coming up recorded **no
>   `Degraded` state at any point** — not even a flash — across a 30-second window, on a run where
>   Nova was the only Persona on disk so nothing could mask it.
> - The tile's status tooltip is `null` (no `title` attribute), the card has no reason line, and the
>   Room has no strip — while the card still shows the stored id under MODEL, so the UI actively
>   asserts a model that is not in use.
> - **The catalog was provably non-empty**, ruling out `PersonaRunner`'s deliberate empty-catalog
>   exemption: storing `sonnet` instead, on the same machine and adapter, produced zero
>   `not in the agent's advertised model catalog` warnings and zero `Failed to set model` warnings.
>
> Confirmed against the current tree: `PersonaHealth.Report` is last-write-wins, with the exact no-op
> (same state *and* same reason) as its only special case. Nothing ranks severity, so a later
> `Online` erases a `Degraded` unconditionally. `PersonaStatusResolver` reads that single record and
> has no way to recover a lost state, so the fix belongs in `PersonaHealth` or in
> `PersonaSupervisor`'s unconditional `Online` report — not in the resolver.

**Comment to add to #30, then close:**

> Duplicate of #17, which describes the same defect with the root cause identified: the `Degraded`
> report raised inside `PersonaRunner.StartAsync` is overwritten by `PersonaSupervisor`'s
> unconditional `health.Report(name, PersonaState.Online, null)` immediately after the await, and
> `PersonaHealth.Report` is last-write-wins.
>
> That answers the open question in this report — the second of the two hypotheses is the right one.
> The evidence unique to this issue has been copied to #17.
>
> Closing here; `PERSONALIFECYCLE-17` and `STREAMINGTURN-23` both now point at #17.

---

## Comments on issues that stay open

### 11. Comment on #21 — pairs it with #32

> Paired with #32. Both are one question — what is Restart, and when is it offered? This issue says
> Restart on an **Offline** card does too much (it starts a paid-capable adapter with
> `Team:Acp:Enabled` false); #32, re-scoped, says there is no way to restart an **Online** one at
> all. Answering them separately risks two inconsistent decisions about the same button.
>
> One quote settles whether this is a defect or an accepted limit. `known-limits.md` already draws
> the line, in the entry about the catalog probe running with the flag off:
>
> > *"An Adapter from this path that PERSISTS is not the probe — that is a real Adapter, spending the
> > operator's money in a configuration they switched off."*
>
> This report observed exactly that: sampled every 0.9 s for 12 s, the adapter was up throughout. By
> the project's own written criterion this is a defect, not the documented probe exemption.
>
> Confirmed in the code: `Acp.Enabled` is read in exactly one place, `PersonaSupervisor`'s
> `ExecuteAsync` startup guard. `RestartAsync` → `RestartHostAsync` → `StartHostIfMissingAsync` never
> consults it.

### 12. RE-SCOPE #32 — the claim is wrong, the complaint is not

> **Correction: the Restart control exists. The underlying complaint is still real, and it is a
> different one.**
>
> `TeammateCard.razor` renders a Restart button gated on
> `this.status.State is PersonaState.Offline or PersonaState.Degraded`. That gating is deliberate,
> and the rationale is in the component's own header comment: *"Restart button is offered only while
> Status reads Offline or Degraded — the one screen a user needs Restart on is exactly the one a
> stale snapshot would hide it from."* True both before and after the MudBlazor migration.
>
> `APPTOOLS-22` step 15 and `HOOKSSETTINGS-39` step 6 both have the tester click Restart on an
> **Online** teammate, where by design it is absent. **Those two scripts are wrong, not the UI.**
>
> Worth recording because it is easy to repeat: a verification run on `main` saw
> `MESSAGE EDIT OPEN RESTART REMOVE` on a card and briefly read it as evidence this was fixed. It was
> not — that run used the free lane (`--Team:Acp:Enabled=false`), which leaves every Persona Offline,
> so Restart renders. The free lane structurally cannot reach the Online state this issue is about.
>
> **What survives, and is worth deciding:** there is no way to restart an Online teammate except by
> editing its Persona text and saving — making a change you do not want in order to get an effect you
> do, and losing the teammate's conversation memory on the way. `HOOKSSETTINGS-39` has testers append
> a single space for this, and that space is trimmed before the file is written, so the restart hinges
> on an in-memory comparison seeing a difference the file never records.
> `docs/agencyteam/product-observations.md` reaches the same friction independently from a user's seat.
>
> Re-scoping to that question plus the two script corrections. Paired with #21.

### 13. Comment on #33 — records the dependency on #27

> Blocked on #27, by this issue's own analysis: *"Log the reason a host is torn down, and correlate
> `The agent process disconnected.` with the session id and the Persona it belonged to. That warning
> currently names neither… the same gap already filed as #27."*
>
> #27 is now fixed on `main` (`23c757c`): every connection teardown logs exactly one `disconnected.`
> line at Information, naming the connection and the agent where one was assigned, with the faulted
> path keeping its distinct Warning. So disconnects are countable now, which is the instrument this
> diagnosis needs.
>
> Worth attempting again against that logging rather than against the log as it stood.
