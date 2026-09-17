---
status: accepted
date: 2026-09-16
---

# Model and Effort reset when the Adapter changes

A Teammate card now has three cascading selects: Adapter, then Model, then Effort. Each
constrains the one below it. This records what happens to the two below when the top one
changes, because the answer is **deliberate data loss on an edit**, which is not a thing to do
silently.

## Why they cannot simply be kept

A Model id is meaningful only inside the catalog that advertised it.
`claude-sonnet-4-5-20250929` means nothing to `agency-acp`; `google/gemma-4-e2b` means nothing
to `claude-agent-acp`. There is no mapping between them and there could not be one — they are
different models from different vendors, and even a same-named model is a different deployment.

Effort is worse, because it is downstream of Model. Its ladder is whatever the Adapter
advertises, and [Language](../agencyteam/language.md) narrowed the definition in this same
change: whether that ladder varies *by Model* is now the Adapter's business.
`claude-agent-acp` advertises one ladder per Model; `agency-acp` advertises one per endpoint
surface. An Effort id carried across an Adapter change is a value from a ladder that may not
exist.

## The decision

**Changing the Adapter clears both Model and Effort, and says so inline.**

```text
Adapter changed
  ├─ model := null,  effort := null
  ├─ adapterResetModelAndEffort := (previous model or effort was set)
  ├─ availableModels := probe(adapter)            [await]
  ├─ StateHasChanged()                            ← MANDATORY
  └─ availableEfforts := probe(adapter, null)     [await]
```

Null means "use the Adapter's default", which is the normal case and always valid. So the reset
lands the Teammate in a working state rather than an empty one.

The note announcing it uses **`role="status"`, never `role="alert"`**. It is a consequence of
what the human just did, not an interruption — the same reasoning, and the same markup, as the
existing `effortResetByModelChange` note.

## The repaint between the two probes is mandatory

`this.StateHasChanged()` between the model probe and the effort probe is not a stylistic
flourish. `ComponentBase` renders a handler at its first yield and at its completion **and
nowhere in between**, so without it the Model select repaints when the *effort* probe answers.
That is issue #39, which measured two entries arriving at 265 ms and six at 8.5 s with no sign
the first reading was provisional.

[Rules](../agencyteam/rules.md) already carried this as a binding row for two dropdowns. There
are now three, and one more place to get it wrong.

## A defect this feature created, and had to fix

`LoadEffortsAsync` has always carried a generation counter, because the Model select could
always be changed twice quickly. `LoadModelsAsync` had none, and correctly so: nothing could
re-enter it, because the model catalog was read only when the card opened.

Adding a select **above** Model made `LoadModelsAsync` re-entrant for the first time. Change the
Adapter twice in quick succession and two model probes are in flight; if the first is slower, its
stale answer overwrites the newer one and the card offers the wrong Adapter's models with no
indication anything is wrong.

It now takes a ticket on entry and commits its result only if it still holds the newest ticket,
mirroring `LoadEffortsAsync` exactly. A test pins it by gating one probe open, running a second
to completion, then releasing the first.

This is worth recording because the defect did not exist before the feature and was predicted
from the *shape* of the change rather than found by a user.

## Consequences

- A human who switches Adapter to try a local model, then switches back, does not get their
  Model and Effort back. They are re-picked from the returning Adapter's catalog. Accepted: the
  alternative is storing a Model per (Persona, Adapter) pair, which is a schema change and a new
  rename cost for a convenience.
- The reset happens in the card, on change — not at save time and not in `PersonaStore`. A human
  who changes Adapter and cancels loses nothing.
- Saving after an Adapter change restarts the session, so the Teammate loses its conversation
  memory too. That is [ADR-0013](0013-an-adapter-is-a-property-of-the-persona.md)'s consequence,
  not this one's, but a human experiences them together and the helper text says so.
- A stored-but-unconfigured Adapter id is **synthesised into the choice list** rather than
  silently reset, exactly as `ModelChoices` already does, so the card shows what the file says.

## Rejected

**Keeping Model and Effort across the change.** A cross-Adapter model id is meaningless, and the
session would start on the Adapter's default anyway while the card claimed otherwise — the
silent-disagreement shape [Traps](../agencyteam/traps.md) exists to catch.

**Storing Model per Adapter.** A `persona_models` row per (Persona, Adapter) pair is a schema
change, a migration, and one more thing `PersonaRenameCascade` must touch — against the standing
Ordering warning, to spare a human two dropdown picks.

**Resetting silently.** Two controls changing themselves with no explanation is how a human
concludes the form is broken.

**`role="alert"`.** It interrupts. The human caused this, and an alert that fires on every
deliberate action is one nobody reads.
