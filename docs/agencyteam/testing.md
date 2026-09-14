# Testing

How this repo tests, why component tests come in two shapes, and the 35-step
manual checklist for what no test can prove. Read it before adding a test, and
work the checklist before calling an ACP-facing change done.

For the commands themselves, see [Build, test, run](../AgencyTeam.md#build-test-run)
in the hub: [AgencyTeam.md](../AgencyTeam.md).

## How the suite is built

This repo is test-first. Real stores over temp directories, and **no mocking
framework** — hand-written fakes under `tests/Huddle.Tests/Acp/Fakes/`.

Components are tested two ways, and the difference matters. A page test fetches
`/teammates` over HTTP and sees only the **prerender** — `Routes` is
`InteractiveServer`, so anything behind a click is absent from that HTML. To see
the card itself, `TeammateCardTests` renders the component directly with
`HtmlRenderer`, which ships with the framework and so adds no package. Reach for
it rather than asserting on a page's HTML for anything a user has to click to
reveal.

**Those two ways leave a hole, and it is not closeable here.** Neither can see
how a *page* passes parameters to a *component*: the page test never opens the
card, and the component test supplies its own parameter dictionary, bypassing the
markup. `HtmlRenderer` cannot help — simulating the click needs an
`eventHandlerId` from Blazor's internal render-tree walk, and `HtmlRootComponent`
exposes no route to it, so reaching it would mean reflecting into renderer
internals: reimplementing bUnit, the dependency this repo declines. A bug of
exactly that shape shipped once (see the `@`-binding rule in [Rules](rules.md)), so
`TeammatesRazorSourceTests` asserts against the `.razor` **source text** instead.
A source assertion is an unusual test; it is here because it is the only layer
that can see the defect at all.

**The stylesheet suites assert on source text for the same reason, and it is the
clearest case of it.** `ThemeSourceTests`, `ThemeFileTests` and the shared `CssSource`
parser read `theme.css`, `app.css` and the `.razor.css` files as *text* and count what
is declared. They do that because **nothing in this suite renders a browser**, and a
CSS custom property resolves entirely inside the browser's cascade: *"did this element
switch to the dark value?"* is unreachable here, while *"is there a colour literal left
in this file?"* is exact and total. So the suite asserts the thing it can decide — no
literals outside `theme.css`, every `var()` naming a declared Token, every Token having
a consumer, every Theme file declaring `color-scheme` and exactly one `:root` block,
`ThemeTokens.All` equal to the stylesheet in both directions. `TeammatesRazorSourceTests`
is the precedent, and the argument is identical: a source assertion is an unusual test,
and it is here because it is the only layer that can see the defect at all. What it
cannot see is covered by manual checklist steps 28–35, and by one HTTP round-trip —
`AppShell_EveryLinkedStylesheetIsServed`, because whether a `<link>` actually resolves
is a fact about the server, not about the text.

**The re-probe on model change is the second behaviour this suite structurally
cannot reach, for the same reason.** `Teammates.razor`'s `OnCardModelChanged`
only runs off a real `<select>` change event under `InteractiveServer`, and
`TeammateCardTests` renders `TeammateCard` from a parameter dictionary that
never sees the page's wiring at all. It is covered by manual checklist only —
steps 14–19 below.

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
