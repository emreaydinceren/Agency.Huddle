# Testing

How this repo tests, why component tests come in two shapes, and the 35-step
manual checklist for what no test can prove. Read it before adding a test, and
work the checklist before calling an ACP-facing change done.

For the commands themselves, see [Build, test, run](../AgencyTeam.md#build-test-run)
in the hub: [AgencyTeam.md](../AgencyTeam.md).

## How the suite is built

This repo is test-first. Real stores over temp directories, and **no mocking
framework** — hand-written fakes under `tests/Huddle.Tests/Acp/Fakes/`.

### Conformance: a real Persona against a real ACP peer

Added 2026-09-16 with the Adapters work, and it closed a gap worth naming. Until then
every test either stopped at `FakeAgentHostFactory` or needed Node installed and
`TEAM_E2E=1` (the tests that show as **skipped** in every run). **Nothing exercised the
real `DotAcpAgentHostFactory` path at all.**

`tests/Huddle.Tests/Conformance/MockAdapterFixture.cs` closes it: the real factory, the
real `DotAcpAgentHost`, the real session, the real `AppToolServer` and the real prompt
composition, with **only the process launch** substituted for an in-memory duplex stream
pair onto a `FakeAcpAgent`. No process, microseconds, deterministic.

Three things to know:

- **Substitute at the launcher, never higher.** The seam is `IAgentProcessLauncher`, which
  `DotAcpAgentHostFactory` takes by injection. Substituting `IAgentHostFactory` instead
  would make the whole tier prove nothing — that is the layer these tests exist to cover.
- **`FakeAcpAgent.Received` is the oracle**, and it is what makes this tier worth its cost.
  A golden test proves `SystemPromptComposer` *composes* the right string; `Received`
  proves that string actually **arrives** in `session/new`'s `_meta`.
- **`OnInitialize` and `OnNewSession` fire during `StartAsync`'s own handshake**, so setting
  them after it returns is too late. `OnPrompt`, `OnSetConfigOption` and `OnSessionClose`
  are settable any time after.

`ProcessModeTests` is the **one** test in the folder that launches a real child process —
the built `mock-acp` — because that is the only thing in-proc cannot prove. Keep it to one.

This tier found two defects nothing cheaper could: a guard that rejected a designed empty
value, and the dispatch-ordering race now recorded in [Known limits](known-limits.md). Both
lived in **seams between** correctly-written components, which is exactly what unit tests
isolate away by construction.

Components are tested **three** ways now, and the difference still matters. A
page test fetches `/teammates` over HTTP and sees only the **prerender** —
`Routes` is `InteractiveServer`, so anything behind a click is absent from that
HTML. `HtmlRenderer` renders a component directly, adds no package, and is still
right for any component that is plain HTML. And since 2026-09-14, **bUnit** —
already a referenced package — is the way to test anything containing a MudBlazor
component.

**Use bUnit whenever a MudBlazor component is in the tree.** `MudBunitContext`
(`tests/Huddle.Tests/Ui/MudBunitContext.cs`) is the shared setup: `AddMudServices`,
loose JSInterop, and `RenderWithPopovers`, which wraps content together with
`MudPopoverProvider` and `MudDialogProvider` in one synthetic root — the providers
are *siblings* of the component that opens a popover or dialog, not descendants,
which is why a plain render will not do. Three things to know before writing one:

- **Dispose with `await using`, never `using`.** MudBlazor's `KeyInterceptorService`
  and `PointerEventsNoneService` implement only `IAsyncDisposable`, and a synchronous
  `Dispose` throws once any component touching them has rendered.
- **`MudSelect` renders its items into a popover that only populates on a real
  click.** A prerender can never see a non-selected option's label. This is the same
  *"anything behind a click is absent"* limit as above, now applying to every
  converted `<select>`.
- **`TeammateCard` cannot be tested in isolation at all.** `MudDialog` renders its
  content only once registered with a real dialog instance, and that registration
  goes through `IMudDialogInstanceInternal`, a type internal to MudBlazor's assembly.
  A hand-written fake satisfies the public cascading parameter and the dialog still
  renders nothing. Its tests drive the real `IDialogService`.

**What bUnit bought.** The old `HtmlRenderer` tests could not dispatch a click, so
several asserted on shapes rather than behaviour — one checked
`DoesNotContain("disabled")` across the whole document. Those are now real
interactions. The hole this section used to describe — that neither way could see how
a page passes parameters to a component — is closed differently than expected: the
card now takes `DialogParameters`, and `TeammateDialogParametersTests` reflects over
its real `[Parameter]` properties and asserts every key names one. The `@`-binding bug
that shipped once (see [Rules](rules.md)) is still caught, in its new shape.

**The stylesheet suites assert on source text, and it is the clearest case of it.**
`ThemeSourceTests` and the shared `CssSource` parser read `app.css` and the
`.razor.css` files as *text* and count what is declared, because **nothing in this
suite renders a browser** and a CSS custom property resolves entirely inside the
browser's cascade: *"did this element switch to the dark value?"* is unreachable
here, while *"is there a colour literal left in this file?"* is exact and total. So
the suite asserts what it can decide — no literals outside `MainLayout.razor.css`,
and every `var()` naming a MudBlazor variable or `--font-mono`, in `app.css`
(`AppCss_UsesOnlyMudBlazorVariables`) **and** in every scoped stylesheet
(`ScopedCss_UsesOnlyMudBlazorVariables`).

That second test exists because its absence nearly shipped a defect:
`ReconnectModal.razor.css` was left referencing nine deleted Tokens, and an
unresolved custom property resolves to **nothing**, not to a fallback — so the
reconnect modal would have rendered invisible at exactly the moment the circuit
drops. The colour-literal test could not see it, and the `app.css` variable test did
not scan that file. Both tests enumerate directories rather than naming files, so a
stylesheet added later is covered the day it appears.

What source text cannot see is covered by the manual checklist, and by one HTTP
round-trip — `AppShell_EveryLinkedStylesheetIsServed`, because whether a `<link>`
actually resolves is a fact about the server, not about the text.

**The re-probe on model change used to be the second behaviour this suite could
not reach. It is not any more, and this paragraph is a dated record of why.** The
old `HtmlRenderer` tests built `TeammateCard` from a parameter dictionary and
could not dispatch a click, so a `<select>` change — the only thing that triggers
the re-probe — was unreachable, and steps 14–19 of the checklist below were the
whole coverage. Since the card became a real `MudDialog` driven through
`IDialogService`, `ChangingTheModel_ReProbesTheEffortCatalog` proves it directly:
opening Create probes the effort ladder once for the agent's default model, and
picking a different Model probes it again for that model specifically. Its
neighbours prove what the user sees afterwards — that the discarded Effort is now
announced rather than dropped in silence (#45), and that the discard itself still
happens. The manual steps are still worth running, because only a real adapter can
show that the *advertised ladder* changed; what the suite now covers is the wiring.

## Manual checklist

The suite covers the agent path end to end through `FakeAgentHostFactory`, so it
is fast and free. What it cannot prove is that a *real* model finds the App
Tools. With `Team:Acp:Enabled=true` — which spends money — check:

> [!NOTE]
> Steps 20–23 are the exception: they are filesystem and page behaviour only, so
> they cost nothing and are worth running with `Team:Acp:Enabled=false` before
> you spend anything on the rest. Steps 24–27 all need a live agent, and 25–27
> deliberately spend a Budget's worth of Turns — set `Team:AgentMessageBudget`
> low before starting them. **Steps 28–35 are free too** — no agent, no money,
> browser and filesystem only — and they are the whole of what the stylesheet
> suites cannot see, because no test here renders a browser.

1. `/teammates` loads, the sidebar link reads **Teammates**, and the page is
   styled (catches a missed CSS class rename). Clicking a tile opens the card;
   **Edit** and **New teammate** open the same card in their own modes.
2. `pwsh tools/echo-bot.ps1 -Name echo` connects, proving the wire round-trip.
3. A Room of two Members replies without a Mention; adding a third makes it
   mention-gated.
4. Adding a Persona on `/teammates` brings an Agent online with a Room named
   after it.
5. `App_Data/work/<Persona>/` is created.
6. An Agent asked to list who exists calls `mcp__team__list_agents` and reports
   real names rather than inventing them.
7. **New teammate** offers a Model list read from the installed adapter, not a
   hardcoded one. Pick a non-default Model, save, and ask that teammate which
   model it is. Change the Model on the card and ask again — the answer changes,
   and the log shows the runner restarted. No *"not in the agent's catalog"*
   warning should appear.
8. Move `tools/acp/node_modules` aside and reload `/teammates`. The page still
   works and the picker says the agent will use its own default. Put it back.
9. An `App_Data` created before this change still opens, with `persona_models`
   added in place and no wipe required.

> [!TIP]
> If step 7's picker comes back empty against a real adapter, suspect the
> `initialize` handshake before concluding the adapter offers no models. We do
> not advertise `clientCapabilities.session.configOptions`. Its XML docs say it
> gates *boolean* options, so selects should arrive regardless — but that is
> inference, not a measurement. Adding
> `Session = new ClientSessionCapabilities { ConfigOptions = new SessionConfigOptionsCapabilities() }`
> to the `initialize` request in `DotAcpAgentHost` is a one-line change and the
> first thing to try. Nothing in the suite can settle this, because the fake
> agent answers whatever we tell it to.
10. A Persona named with a space — `Chief of Staff` — comes online, and
    `@Chief of Staff` in a Room of three or more actually reaches it. This is the
    one path no test can prove, because it depends on a real model writing the
    Mention out in full.
11. Ask a teammate what tools it has. It calls `mcp__team__get_help` and reports
    the real catalog rather than reciting the system prompt or inventing one.
    This is the only check that progressive discovery works end to end.
12. In a Room of three or more, ask a teammate to bring another agent in. It
    calls `mcp__team__invite_agent` with the id from its own `[Room: …]` label,
    the sidebar renames the Room with no refresh, and the invited Agent answers
    when Mentioned. Getting the id wrong here is the failure to watch for.
13. **Add teammate** on the Room header lists only Agents that are not already
    Members, and inviting one renames the Room the same way the command does.
14. Open a Create/Edit card — the Effort list populates with **no second**
    `node` spawn.
15. Change the Model — the Effort list **changes** (not merely repopulates)
    and the chosen level resets.
16. Pick a model with no effort support — the picker says so and nothing is
    stored.
17. Pick a level and save — the teammate restarts, with **no** *"not in the
    agent's advertised effort catalog"* warning in the log.
18. Save with Effort unchanged — the teammate does **not** restart.
19. Remove a teammate and recreate it with the same name — the old Effort must
    not resurrect.
20. **The Teams no-op.** Put a Persona in `App_Data/Teams/Business/`, give it a
    Model, then move the file to `App_Data/Teams/`. Nothing changes — same Name,
    same groups on `/teammates`, same Model. This is the check that most
    distinguishes Team-as-a-field from Team-as-a-folder, and the one most likely
    to regress if someone reintroduces a path-derived key.
21. **Folders are cosmetic.** A Persona filed under `Teams/Household/` whose
    frontmatter says `teams: Business` appears under Business, not Household.
22. **A malformed file is loud.** Delete `title:` from a Persona. It vanishes
    from the tile list and appears by path and reason in the rejected-files
    block. Then give two Personas the same `alias:` — *both* disappear and both
    are named.
23. **The watcher is live and recursive.** Edit a Persona nested in a Team
    sub-folder and confirm it reloads without a restart; rename the sub-folder
    and confirm every teammate in it stays reachable. These are two of the four
    silent watcher failures in [Traps](traps.md) and neither produces an error
    when it breaks.
24. **An Alias works everywhere a Name does.** With `alias: jar` on Jarvis:
    `@jar` reaches it in a Room of three or more, `/invite @jar` adds it, and a
    teammate calling `mcp__team__invite_agent` with `jar` succeeds — the Alias is
    advertised in `list_agents`' job description, so all three must accept it.
25. **A two-Agent loop actually stops.** Set `Team:AgentMessageBudget` low (6 or
    so — this checklist spends money) and put two Personas in one Room with
    instructions to keep talking to each other. The exchange must halt at the
    Budget rather than running on. This is the whole point of
    [ADR-0006](../adr/0006-a-room-has-a-budget-for-agent-replies.md), and the
    suite can only prove it against `FakeAgentHostFactory`.
26. **Continue resumes the same exchange, and the prompt survives a refresh.**
    At the pause, reload the page first — the prompt must still be there, which
    is the prerender path rather than the event path. Then click **Continue**:
    the Agent that was about to reply does so with no Human Message having been
    typed, and the Room halts again one Budget later. Check the resumed Agent's
    prompt does not contain the re-delivered Message twice, once as context-only
    Catch-up and once live.
27. **A refused Agent does not retry.** Watch a Persona that gets
    `budgetExhausted` back from `mcp__team__post_message`. It must stop rather
    than retry or post the same thing into another Room. Only a real model can
    prove the terminal wording works — same class as steps 11 and 12.
28. **Dark mode is the test.** Set Settings → Appearance to **Dark**. Walk `/`, a
    Room with a Draft streaming, `/teammates` with the card open and a rejected
    file present, and `/settings` on both tabs. Any element that stays light is a
    literal that survived tokenisation.
29. **System is pure CSS.** Choose **System** (or delete
    `App_Data/appearance.json`) and flip the operating system's theme with the app
    open. It must follow **with no reload and no navigation** — that path involves
    no script and no server.
30. **No flash.** With **Dark** chosen, hard-reload: the page must never paint
    light first. The Theme is a `<link>` the server emits, so a flash would mean it
    is not being emitted at all.
31. **The choice is per installation.** Open the app in a second browser or a
    private window: **the same Theme**. Restart the app: still the same. This is
    the deliberate consequence of the choice living in a file rather than in the
    browser.
32. **Overrides, by Token name.** Put
    `{"theme":"huddle-dark","overrides":{"--font-chat":"Georgia, serif","--accent":"#c14bd0"}}`
    in `App_Data/appearance.json` with the app running. The next page load shows
    Message text in a serif face and magenta avatar monograms — **and nothing else
    changes**, because every other Token falls through to the Theme. Keys are Token
    names; `chat_font` is not a key and never will be.
33. **A bad override is reported, not swallowed.** Set
    `"--surface-base": "red; } :root{"`. The app renders normally, the Appearance
    tab lists the rejected value, the log carries one warning, and **the file is
    unchanged**. Then set `"--not-a-token": "x"` and confirm the same treatment.
    Finally set `"theme": "dracula"` and confirm the built-in pair applies and the
    file still says `dracula`.
34. **Layering does what it claims.** Hand-write `wwwroot/themes/probe.css`
    containing only `:root { color-scheme: dark; --accent: #ff00ff; }`, add `probe`
    to `ThemeCatalog`, rebuild and select it. The accent must be magenta and
    **everything else must be the built-in dark palette** — not light, and not
    blank. Delete both afterwards.
35. **The reconnect modal.** Stop the server with the browser open. The dialog
    shows **one** state paragraph, on a themed panel, over a dimmed backdrop — in
    both Themes. All six at once means the scoped-CSS bundle is not loading again,
    which is the [Traps](traps.md) entry on `@Assets[...]`.
