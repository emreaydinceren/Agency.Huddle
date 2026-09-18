# Huddle.Adapters — handoff

**Date:** 2026-09-18 · **For:** whoever picks this up next, with no memory of how it got here

The Adapters feature is **built and shipped**, and as of 2026-09-18 nothing is blocked. What
remains is a milestone that no automated test contains and one piece of genuinely unspecified
engineering. This page is the shortest path into both.

**Read in this order, and stop when you have what you need:**

1. This page — state of play and what to do first.
2. [`Huddle.Adapters-LiveFindings.md`](Huddle.Adapters-LiveFindings.md) — what happened on contact
   with the real Adapter. Five findings, D-1 through D-5, and the only document here that is about
   reality rather than intent.
3. [`Huddle.Adapters-ProjectPlan.md`](Huddle.Adapters-ProjectPlan.md) **§D12** — the remaining tasks,
   written for someone with zero context.

[`Huddle.Adapters-Specifications.md`](Huddle.Adapters-Specifications.md) is the design and is still
binding, but it is 1,400 lines and you do not need most of it. Read **§15.8**, **§15.9** and the
**D-12 amendment in §17** if you are touching the conformance suite; otherwise leave it alone.

---

## 1. Where things stand

| | |
| --- | --- |
| **Feature** | D0–D11 shipped in PR #59, one squashed commit `76b4e17` |
| **Follow-up** | PR #60, **open and unreviewed** — `EnvironmentOverrides`, the per-Turn idle bound, and a docs catch-up |
| **This branch** | `docs/adapters-conformance-honesty-and-handoff`, stacked on PR #60 — spec and plan corrections plus this page |
| **Suite** | 1,294 tests green, 0 warnings |
| **Agency.NET** | **Done.** `0.1.197-gb4f68316af` published 2026-09-18; PR #218 merged. All four items we raised are fixed |
| **Milestone** | Not run. Nothing is blocking it any more |

### What actually works

A Persona can name an Adapter in frontmatter, the card offers the choice when more than one is
configured, the tool-name prefix follows the Adapter, model and effort catalogues are per-Adapter,
and a local model answers. **That last part is verified, not assumed** — a live `gemma-4-e2b`
returned a Persona's name and nonce codeword, and two concurrent sessions with different identities
did not bleed into each other.

### What does not

Nothing is broken. Two things are *unfinished*: the milestone has never been run, and the
conformance suite cannot be pointed at a real Adapter (§3 below).

---

## 2. Do these first

### (a) Get PR #60 reviewed and merged

It is three commits, each independently green, and the first of them is currently **the only way to
start `agency-acp` at all** — that Adapter takes its configuration from environment variables and
an Adapter Profile had no way to carry any.

### (b) Run ADAPTERS-01 and ADAPTERS-02 — this is the milestone

`docs/agencyteam/manual-tests/adapters.md`. Both are **free**: they need `mock-acp`, which this
solution builds, plus `node`. No GPU, no subscription, no money, no Agency.

This is the highest-value unclaimed work on the board. The milestone everyone has been describing —
*two Personas in one Room on different Adapters, live chunks, Stop leaves both resumable* — **is
these two tests**, and they have been runnable the whole time. All four ADAPTERS rows in
`manual-tests/tracker.md` are `Active` with empty result columns; Task 12.2's acceptance says
"executed, and their outcomes recorded", so it is not met.

ADAPTERS-03 and ADAPTERS-04 are paid. ADAPTERS-04 was blocked until 2026-09-18 and is now
runnable — check `agentInfo.version` first, for the reason in §4.

### (c) Task 12.1a — the only unspecified engineering left

`ProjectPlan` §D12. Split the conformance suite into portable and mock-only assertions, then
parameterise `MockAdapterFixture` over its launcher. Needs no Agency involvement. About a day.

---

## 3. The thing most likely to waste your time

**The conformance suite cannot be re-pointed at a real Adapter, and the plan used to claim it
could.** If you read the original Task 12.1 ("re-run every D10 test unchanged") and try it, you
will lose a day discovering why.

Two reasons, and the second is the one that matters:

- Every conformance test but `ProcessModeTests` is wired to `MockAdapterFixture`, which substitutes
  `IAgentProcessLauncher` with an in-proc stream pair. No configuration turns that into a launched
  process. This is fixable.
- **Five of the six conformance files assert through `FakeAcpAgent.Received`** — *what the Adapter
  was sent*. ACP gives a client no way to ask an agent that. Parameterising the launcher would let
  those tests run against `agency-acp` and leave them with **nothing to assert**.

So the split is not a fixture problem, it is a property of each assertion. Spec §15.8 now marks
every test `portable`, `mock-only` or `split`. Respect that column.

**Do not try to give a real Adapter a `Received` equivalent.** There is no wire call for it, and
inventing one turns the mock into a second implementation of Huddle behaviour, which Spec §4 (P8)
forbids for good reasons.

The mock-only tests are not second-class. T-23 — *the received prompt names `get_help` under an
unprefixed profile* — is the strongest test in the suite, because it catches the defect that makes
a Persona look broken rather than misconfigured. It just proves a direction rather than a round
trip.

---

## 4. Traps, each of which has already cost someone

**`ADAPTERS-04` is only meaningful on `0.1.197` or later — check the version, do not assume it.**
It asks whether a real local model calls `get_help` unprompted. On `0.1.195` and earlier the
Adapter never read `_meta.systemPrompt`, so the prompt naming `get_help` never arrived and the test
returned INCONCLUSIVE every time while looking like evidence about a model. That is fixed, but an
older Adapter on a machine somewhere will reproduce the trap silently. `initialize` reports the
version in `agentInfo.version`; read it before you trust a result.

**A published package is not the drop we validated, and you cannot simply re-run against it.**
Agency's pre-merge `0.1.197` was built from *uncommitted* work, so its `-ga1fc165f21` suffix points
at the commit the fix sits *on top of*. What published is **`0.1.197-gb4f68316af`** — same base
version, fresh build, different commit. **Identify a build from `agentInfo.version` in
`initialize`, never from a file name.**

The catch, found on 2026-09-18 while trying to honour this rule: **the published package is a
library, not a runnable host.** `AgencyDotNet.Acp.nupkg` contains `lib/net10.0/` and no executable,
so there is no vanilla-config check to run against the feed artifact. The published binary was
verified *statically* instead — the identity parser present, the `_meta.model` reader gone. If you
need a runtime check on a published version, build the host from that tag; a drop folder is not a
substitute and neither is the nupkg.

**`EnvironmentOverrides` keys must come from `appsettings.json`, never the environment-variable
provider.** That provider rewrites every `__` into `:`, so
`Team__Acp__Adapters__0__EnvironmentOverrides__Agent__DefaultModel` binds as the key
`Agent:DefaultModel`, which no process reads. The symptom is a profile that looks correctly
configured and still fails. No code can prevent this.

**`PersonaRunner`'s idle bound has two traps, and both are rules.** A timeout and a Stop cancel the
same token and land in the same `catch`; the Stop clause reports no health state, so routing a
timeout through it reports a hung Adapter as a healthy Persona. And the timeout must cancel the
**far side first**, because cancelling locally first makes `PromptAsync`'s `finally` null its
`promptCts`, after which `CancelAsync` silently no-ops and `session/cancel` is never sent. Both are
written up in `docs/agencyteam/rules.md`; read that row before touching the watchdog.

**`dotnet test Huddle.slnx --`** — the trailing `--` is required. Without it the run reports "Zero
tests ran" and reads as a pass.

**A full suite run occasionally crashes after every assertion passes.** `PersonaStore.OnWatcherError`
logs before it checks `disposed`, so a late watcher error during teardown throws on a callback
thread. Pre-existing, diagnosed, recorded as the third known flake in `known-limits.md`. Re-run;
do not treat it as a regression in whatever you changed.

---

## 5. ~~When Agency publishes~~ — done 2026-09-18

Agency published `0.1.197-gb4f68316af`. **All four documents that scoped the blocker to `0.1.195`
have been revised**: the live findings, `known-limits.md`, and the ADAPTERS-04 notes in both the
manual-test area file and the Tracker. Nothing here is pending.

What that unlocked: **Task 12.1b is now possible**, and **ADAPTERS-04 is now worth running** —
check `agentInfo.version` first, because on `0.1.195` or earlier it is meaningless rather than
merely failing.

What survived the fix, and is now its own entry in `known-limits.md`: **a small local Model may not
inhabit a Persona even though the Persona now reaches it.** On `gemma-4-e2b`, two sessions with
distinct identities both opened by asserting the base model identity; one recovered, one gave its
Persona's codeword while still answering as Gemma. The text arrives — the codewords prove it — a
2B-class Model just does not reliably hold a voice. That is a Model limit, not a protocol one, and
it is what ADAPTERS-04 now measures.

The correspondence lives in `E:\Repos\Agency\PRIVATE\Huddle\` as a numbered exchange, `01-` to
`15-`. It is gitignored on their side and is the only record of how the contract was negotiated.

---

## 6. What is deliberately not worth doing

- **Turning on progressive tool discovery on the Agency side.** Settled and recorded in their
  decision log as G-4. Our prompt is self-contained and names all seven tools; `get_help` is
  *semantic* and their `tool_help` is *syntactic*, and the two are near-homonyms. Chaining both is a
  failure a small local model cannot absorb. The trigger to revisit is our tool set outgrowing the
  prompt, which it has not.
- **Rendering model residency on the card.** `AgentModelOption.Description` carries it and nothing
  renders it. If that changes, `null` means *no information* and must never render as "not loaded" —
  Agency's contract has three states, not two.
- **A whole-turn timeout.** The idle bound measures silence deliberately. A whole-turn cap safe for
  a long tool-using Turn would leave a hang undetected for a quarter of an hour.
- **Harmonising the Stop path's cancel ordering with the watchdog's.** They are opposite on purpose;
  see §4.

---

## 7. The one pattern worth carrying out of this project

Three defects were found across two codebases — the system prompt never being read, an empty
`ApiKey` failing opaquely, and this plan's own "written once, run twice" claim. Every one of them
was covered by a green test that asserted **the layer above the one that breaks**: options *bound*
rather than an agent being *built* from them; a prompt being *composed* rather than *arriving*; a
fixture *existing* rather than being *re-pointed*.

None of them was a missing test. Each was a test that could not have gone red.

When you add a test here, ask what would have to break for it to fail, and whether that is the thing
you actually care about. If the answer is "the layer under this one is wired correctly", the test is
decorative.
