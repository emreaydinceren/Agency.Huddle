# Gitea batch B — new issues

Drafted 2026-09-15 against `main` at `cc82e03`. **Nothing posted yet.**

Seven new issues. Six come from `docs/agencyteam/product-observations.md`, which no manual
test covers because every one of them describes behaviour that is working **as designed** —
which is exactly why the 464-test suite found none of them. The seventh is what survived
the closure of #23.

Two observations are deliberately **not** filed separately:

- **Observation 2** (the demo agents contradicted the two-Member rule) is fixed and merged,
  and recorded in `decisions.md` under 2026-09-15.
- **Observation 5** (Continue behaves like a coin flip) is folded into issue 2 below, because
  it is the same defect — the app knowing why nothing happened and not saying so — reached
  through a different control.

---

## 1. The Edit card is misleading for the ~11 seconds a cold model catalog takes to land

**Body:**

> Split out of #23, which is closed: the MudSelect migration fixed the selection drift that
> issue described. What it did not fix is that the Edit card asserts wrong things while the
> adapter's model catalog is still being probed.
>
> Measured on `main` on 2026-09-15, opening **Edit** on the first teammate-card open of a cold
> app run and sampling every 250 ms for 12 seconds:
>
> - **The list is short.** The Model dropdown opens with a handful of entries and grows once
>   the probe returns. `docs/agencyteam/product-observations.md` records the same thing from a
>   user's seat: *"Opening Edit early showed only Haiku; the full list (Sonnet, Fable, Opus)
>   appeared once the adapter had been probed. Silent and timing-dependent, so whether you see
>   the real choice depends on how fast you clicked."*
> - **The label is the raw id.** Until the catalog lands the control displays `sonnet`; after,
>   it switches to `Sonnet`. `ModelDisplayText` falls back to the raw id while
>   `AvailableModels` has no match yet.
>
> Both are faces of the same window, and they compound: during it the card offers an
> incomplete set of choices, labelled inconsistently, with no indication that either is
> provisional.
>
> The card is not silent about loading — there is a `ModelsLoading` hint reading *"Reading the
> models this agent offers…"* — but the select stays interactive and authoritative-looking
> beside it, which is what makes it misleading rather than merely slow.
>
> **Worth deciding rather than patching:** either disable the select while the catalog is
> loading, or keep it usable and make the transient state unmistakable. The stored value is
> never at risk — that was established in #23's thread — so this is purely about what the card
> asserts.
>
> **A note for whoever re-tests this:** `MudSelect` renders no `<option>` elements, so an
> `option`-based oracle reads zero throughout and tells you nothing. Count `.mud-list-item` in
> the open popover instead.

## 2. Silence is the product's main failure mode, and it is never explained

**Body:**

> From `docs/agencyteam/product-observations.md`, which calls this *"the biggest thing I would
> fix"* and closes with: *"If only one thing here gets attention, make it observation 1 — it is
> the cheapest to fix and it removes most of the confusion the other two cause."*
>
> Several mechanisms cause a teammate to correctly say nothing, and **none of them shows
> anything on screen**:
>
> | What happened | Why the teammate was right to stay quiet |
> | --- | --- |
> | Plain message in a Room with three or more Members | Nobody was `@`-mentioned, so it is context-only |
> | Message arriving in a Room whose Budget is spent | The turn is declined before any prompt is built |
> | **Continue** pressed on a paused Room | The re-delivered message mentioned nobody |
> | A reply refused for Budget | The draft types out a full answer, then vanishes |
>
> In every case the app knows precisely why it stayed quiet, writes a clear line about it to
> the server log, and shows the Human nothing. From the user's seat, "working as designed" and
> "broken" are indistinguishable.
>
> This is not hypothetical. The observation records concluding the app was wedged **twice**,
> once restarting it three times on a wrong theory, **with the server log open**. A user
> without the log has no route through it at all.
>
> **Observation 5 is folded in here** — of three **Continue** presses across that run, two
> granted the Budget correctly and produced no visible effect whatsoever, because the
> re-delivered message mentioned nobody. Continue is the Human's only control for un-pausing a
> Room, and pressing it can plausibly look like it did nothing.
>
> ## The design question is already settled — do not re-open it
>
> The obvious fix is a quiet line in the transcript. **That instinct has been reversed twice
> already**, and the roadmap records both:
>
> - Item 2, delivered: *"A posted Message needs a sender and there is no honest one — a `system`
>   sender costs a third `UserKind` (a SQL `CHECK` wanting a fresh `App_Data`, **and** a wire
>   enum inside `MemberInfo`), the capped Agent posting it is circular, and the Human posting it
>   resets the Budget it reports."* It shipped as the `.budget-prompt` block instead.
> - Item 3, delivered: *"It is a **strip in the Room view**."*
> - Item 11 repeats the warning: *"Resist inventing a `system` sender."*
>
> So: a Room-view affordance, never a posted Message, never a third `UserKind`.
>
> **What is genuinely open** is narrower: both existing precedents are *room-level* state (this
> Room is paused; this Persona is Degraded), whereas the natural wording here — *"Nova read this
> but was not addressed"* — is a *per-message* annotation, which has no precedent in this
> codebase. Settle that shape first, then build once.
>
> ## Why this is worth doing before roadmap item 8
>
> Item 8 (*Following a Room without being Mentioned*) already describes the same problem in the
> present tense — *"the stall produces no error"* — and the Ordering section warns that failure
> surfacing *"rises in value again once 8 ships, because a coordinator that has stalled and one
> that is thinking look identical."* Item 8 makes silence both more common and harder to read.

## 3. A Room can cross the two-to-three Member boundary without the Human knowing

**Body:**

> From `docs/agencyteam/product-observations.md`.
>
> A two-Member Room with Nova silently became a three-Member Room, because Zellandine used
> `invite_agent` to add itself so it could post there. That is legitimate — the tool exists and
> membership was enforced correctly. `known-limits.md` already records the related property:
> *"An Agent can invite into any Room whose id it holds."*
>
> The consequence is not obvious: the Room crossed the two-to-three boundary, so it switched
> from *answers everything* to *mention-gated*. The next ordinary message got silence. Quoting
> the observation: *"I had not invited anyone, had not noticed a membership change, and had no
> reason to think the rules had moved under me."*
>
> **Two Members and three Members are genuinely different products in the same window.** When a
> Room crosses that line, say so where the Human is looking — *"Zellandine joined. Messages here
> now need an `@mention`."*
>
> Same surface as the silence issue above, and worth building together: this is the one case
> where the Human can be told *before* the confusing silence rather than after it.

## 4. Teammates routinely reply twice to one Message

**Body:**

> From `docs/agencyteam/product-observations.md`.
>
> A teammate frequently produces two message rows for a single turn — its answer, plus a
> `post_message` of roughly the same thing:
>
> ```
> Nova :: Got it—I've saved "pumpkin" to memory.
> Nova :: Remembered: pumpkin. 🎃Done—pumpkin is now in my persistent memory.
> ```
>
> Nothing is broken. These are two deliberate messages, not a double-delivery bug, and the
> Budget counts them correctly. But it reads as the teammate talking to itself, it **burns
> Budget twice as fast as the user expects**, and it happened often enough across the run to
> look like the norm rather than the exception.
>
> The fix is most likely one hook edit in `HookCatalog` telling a teammate not to `post_message`
> what it is already about to say — candidates are `tool.postMessage.description` and the
> `getHelp.*` block.
>
> **Sequence this after #31.** `tool.*.description` is badged **Next session** but leaks live
> through `get_help`, which is #31, so until that is fixed this very edit rolls out
> inconsistently — a running teammate would see the new wording through `get_help` while its
> tool schema still carries the old.
>
> Editing model-facing text is governed by ADR-0007, and changing this wording will move the
> golden files; regenerate them the documented way rather than hand-editing.

## 5. Rooms with the same Members are indistinguishable in the sidebar

**Body:**

> From `docs/agencyteam/product-observations.md`.
>
> At one point the sidebar held two Rooms named `Nova, echo, Jarvis` and two named
> `Nova, Jarvis`. Rooms are named after their Members, so any two Rooms with the same Members
> are indistinguishable — no timestamp, no last-message preview, no distinguishing mark of any
> kind. The tester navigated by URL id for the rest of the run.
>
> Independently corroborated by the manual suite: `STARTUPCONFIG-07`'s recorded result notes
> *"Two same-named Rooms are distinguishable only by id in the sidebar."*
>
> This is easy to reach without trying — `STARTUPCONFIG-07` also establishes that **Start chat
> with two agents always mints a new Room** rather than reusing an existing one with the same
> membership, so duplicates accumulate through ordinary use.
>
> A last-message snippet or a created date under the name would settle it.

## 6. A teammate's memory is invisible and unmanageable, and it outlives what the UI promises

**Body:**

> From `docs/agencyteam/product-observations.md`. **Related to #22 but not the same issue, and
> it survives whatever #22 does.**
>
> Asked what word it had been asked to remember, a freshly restarted teammate answered with a
> word from a **different Room, two tests earlier** — because it had written that word to disk
> during an unrelated turn.
>
> #22 covers *where* a Persona may write, and confining it is necessary. This issue is the part
> that confinement does not address: a teammate accumulates memory across Rooms and across
> restarts, the Human has no view of what it holds, and no way to clear it.
>
> **It makes a UI promise false.** The Edit card states that saving *"restarts the teammate,
> which clears what it remembers"*. Once a teammate can write itself notes, that is no longer
> true — and it is the sentence several manual tests are built on, which is how this first
> surfaced as a string of confusing test results rather than as a product complaint.
>
> ## One constraint on the fix
>
> The obvious answer — clear the Work Dir on restart — **conflicts with roadmap item 11**, whose
> premise is that Personas keep durable files: *"An Agent writes files. `Bash` and `Write` run
> agent-side against the real disk — the Work Dir is not a jail — so a Persona that keeps notes,
> a memory file, or any working document is already doing so today."* The whole item exists to
> serve that.
>
> So the two halves should be decided together but not conflated: **#22 is where a Persona may
> write**; **this is what the Human can see and clear**. The answer to the first should not be
> allowed to force the second into "wipe everything".

## 7. A Model change silently drops the stored Effort

**Body:**

> From `docs/agencyteam/product-observations.md`.
>
> Changing a teammate's Model discards its stored Effort selection with no notice that it
> happened or why.
>
> The behaviour is correct — the new Model may not offer the same levels, and `known-limits.md`
> already records that *"the `thought_level` entry is rebuilt on every model switch"* and that
> effort is *"the ladder the Adapter advertises for that Model"*. The defect is only that the
> selection disappears silently, so a user who had deliberately chosen an Effort has it removed
> without being told.
>
> A single line in the Edit card saying the Effort was reset, and that the new Model advertises
> a different ladder, would cover it.
>
> Smaller than the rest of this batch, and a natural companion to the cold-catalog issue above
> since both are about the Edit card asserting something the user cannot otherwise verify.
