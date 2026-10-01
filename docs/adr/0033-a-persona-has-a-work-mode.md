---
status: proposed
date: 2026-09-30
---

# A Persona has a Work Mode, chosen from what its Adapter advertises

A **Work Mode** is the ACP `mode` config option: how much an Agent may do before it must ask
first. It is a Persona setting beside Model and Effort, picked from the list the Adapter
advertises at `session/new`, and applied through `session/set_config_option`. The full design
is [Huddle.WorkModes-Specifications.md](../Huddle.WorkModes-Specifications.md).

## The problem

Every Persona runs in the Adapter's default mode, and Huddle's permission handler answers every
`session/request_permission` itself. [Roadmap §20](../agencyteam/roadmap.md) recorded the
result on 2026-09-30: the Human decided auto-approve is acceptable for now, and a live Turn
showed that an edit inside the Work Dir still round-trips a request that is always approved.

Two things follow. There is no way to make a Teammate read-only by construction: the handler
approves whatever it is asked, so a reviewer can edit. And a Teammate that should edit freely
still pays a request per edit.

ACP has a standard place for this. `session/new` returns a `configOptions` entry with
`category: "mode"`, and `session/set_config_option` changes it. Huddle reads the `model` and
`thought_level` entries from the same array and ignores `mode`
([session-config-options.md](../acp/session-config-options.md) lists it as "No").

## The decision

**A Persona may have a Work Mode.** It is the id of one of the modes its Adapter advertises,
and unset means the Adapter's default, which is the normal case.

- It is stored in its own sibling table, `persona_work_modes`, exactly as Effort is stored in
  `persona_efforts`.
- It is applied at every open and every resume, after Model and Effort, by
  `session/set_config_option`. The response is read back, because an Adapter may clamp what it
  was asked for.
- It is fixed for the life of a session in this version, so changing it restarts the Persona,
  like a Model change. A live switch is a later phase.
- Changing the Adapter clears it, with an inline note. This amends
  [ADR-0015](0015-model-and-effort-reset-when-the-adapter-changes.md).

**Modes are discovered, never listed in code.** The spec says categories are for UX and "MUST
NOT be required for correctness", and another Adapter's modes differ. Two ids do appear in
code, each for a stated safety reason: the hidden list, and the plan guard below.

**`bypassPermissions` and `auto` are hidden by default**, and the hiding is enforced when a
session opens, not only in the picker. A stored value for a hidden mode is dropped with a
warning. An operator lifts the block with `Team:Acp:HiddenModes`.

**A Persona in `plan` mode cannot leave it.** The handler refuses the Adapter's request to
exit plan mode. Without that, the handler's own "prefer `allow_once`" rule would pick the
option that leaves plan mode on the first request.

## Why a setting and not a smarter permission handler

A handler decides after the Agent has already asked. A mode changes what the Agent is
*allowed to try*: in `plan` the Adapter does not run edit tools at all, and in `acceptEdits` it
does not ask about edits. A handler that refuses edits for a reviewer would give the model an
error to react to on every attempt. A mode gives it a tool set that matches its job.

## Why `default` is not filtered out, when Effort's is

Effort's ladder carries an `"default"` sentinel, and the app filters it because *null means send
nothing*, a different wire behaviour from sending `"default"`
([rules.md](../agencyteam/rules.md), the Effort row). A mode list has no such sentinel:
`claude-agent-acp` advertises `default` as **Manual**, a real mode meaning "always ask before
making changes". Filtering it would remove the one way to choose it explicitly. Null still
means send nothing, and is the blank option.

## What was read, and what was not

Read from the vendored `claude-agent-acp` 0.75.1 `dist/` on 2026-09-30. Nothing here was run.

- The option is `id: "mode"`, `category: "mode"`, `type: "select"`. It offers `default`
  (Manual), `acceptEdits`, `plan` and `auto`, and `bypassPermissions` when the process is not
  root. Windows has no `geteuid`, so it is offered there. `dontAsk` is accepted by the parser
  but not advertised.
- A mode change through `session/set_config_option` emits `current_mode_update` and returns
  the full `configOptions`, with the applied value in `currentValue`.
- `auto` is clamped to `acceptEdits` on a model that does not support it, with a message chunk
  telling the user so. A model switch can clamp it later.
- The initial mode comes from the Adapter's own settings default, never from the session being
  resumed, so a resumed session should come back in Manual.
- `ExitPlanMode` is asked as a tool call titled "Approve Plan", kind `switch_mode`, carrying
  the plan text. Its options include two that raise the mode to `auto`, `bypassPermissions` or
  `acceptEdits` (kind `allow_always`), one that exits to Manual (`allow_once`), and one that
  reads "No, keep planning" (`reject_once`).

**Not established:** that a resumed session really comes back in Manual, what the model does
after "keep planning", and whether an edit *outside* the Work Dir still reaches the handler
under `acceptEdits`. The spec lists each as a spike to run before the code that depends on it.

## Consequences

- A Work Mode change restarts the Persona and forgets its Room Sessions, so the Teammate loses
  its conversation memory, as a Model change already does.
- The resume match rule becomes Adapter, Model, Effort and Work Mode. Existing entries stay
  valid for a Persona with no Work Mode.
- Under `acceptEdits`, edits inside the Work Dir stop reaching the handler. The `~/.claude`
  write guard therefore sees fewer calls, and the spec makes the outside-Work-Dir case a
  manual test.
- `src/Huddle.Acp` gains members, so the ACP effort is told first. `Huddle.Console` needs no
  change, because no `AgentEvent` type is added.
- Auto-approve stays the handler's behaviour for everything except the plan guard. Nothing
  here makes Huddle ask the Human before a tool runs; that stays roadmap §20.

## Rejected

| Alternative | Why not |
| --- | --- |
| A fixed list of Huddle-named modes | Another Adapter advertises different modes, and the spec says the category must not carry correctness. The Adapter's own list is the truth |
| `session/set_mode` | Marked for removal, and the v2 draft drops it. `set_config_option` is the durable path |
| A field in the Persona file | Model and Effort stay out of it. A Persona file is plain text people copy between installs, and a stored mode is a local choice about what runs unattended |
| Filtering `default` as Effort does | It is a real mode here, not a sentinel |
| Offering every advertised mode | `bypassPermissions` is advertised on Windows, and `auto` moves permission decisions to the model and silently changes with the Model |
| A blocking approval card for plan exit | [ADR-0022](0022-an-agent-asks-the-human-with-a-question.md) and roadmap §20 record why a request that waits collides with the idle watchdog. The later phase is non-blocking |
| A per-Room mode now | It needs new wire messages and a UI keyed by Room. It is the second phase, and this one is what it builds on |
