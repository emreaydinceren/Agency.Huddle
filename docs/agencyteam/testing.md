# Testing

How this repo tests, why component tests come in two shapes, and the 19-step
manual checklist for what no test can prove. Read it before adding a test, and
work the checklist before calling an ACP-facing change done.

For the commands themselves, see [Build, test, run](../AgencyTeam.md#build-test-run)
in the hub: [AgencyTeam.md](../AgencyTeam.md).

## How the suite is built

This repo is test-first. Real stores over temp directories, and **no mocking
framework** — hand-written fakes under `tests/Team.Tests/Acp/Fakes/`.

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
