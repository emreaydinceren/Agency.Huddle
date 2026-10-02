---
status: accepted
date: 2026-09-30
---

# A Persona has a Work Mode, chosen from what its Adapter advertises

A **Work Mode** is the ACP `mode` config option: how much an Agent may do before it must ask
first. It is a Persona setting beside Model and Effort, picked from the list the Adapter
advertises at `session/new`, and applied through `session/set_config_option`. The full design
is [Huddle.WorkModes-Specifications.md](../Huddle.WorkModes-Specifications.md).

## The problem

Every Persona runs in the Adapter's default mode, and Huddle's permission handler answers every
`session/request_permission` itself. [Roadmap §20](../engineering/roadmap.md) recorded the
result on 2026-09-30: the Human decided auto-approve is acceptable for now, and a live Turn
showed that an edit inside the Work Dir still round-trips a request that is always approved.

Two things follow. There is no way to make a Teammate read-only by construction: the handler
approves whatever it is asked, so a reviewer can edit. And a Teammate that should edit freely
still pays a request per edit.

ACP has a standard place for this. `session/new` returns a `configOptions` entry with
`category: "mode"`, and `session/set_config_option` changes it. Huddle reads the `model` and
`thought_level` entries from the same array and ignores `mode`
([acp-session-config.md](../engineering/acp-session-config.md) lists it as "No").

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

**`bypassPermissions`, `auto` and `plan` are hidden by default**, and the hiding is enforced when
a session opens, not only in the picker. A stored value for a hidden mode is dropped with a
warning. An operator lifts the block with `Team:Acp:HiddenModes`. `plan` is hidden because the
refused exit ends the Turn cancelled and the plan is in a tool call, not in text, so nothing
reaches the Room until a later phase captures it. The picker by default lists Manual and Accept
edits.

**A Persona in `plan` mode cannot leave it.** The handler refuses the Adapter's request to
exit plan mode. Without that, the handler's own "prefer `allow_once`" rule would pick the
option that leaves plan mode on the first request. The guard stays even though `plan` is hidden
by default, for an operator who lifts the block.

## Why a setting and not a smarter permission handler

A handler decides after the Agent has already asked. A mode changes what the Agent is
*allowed to try*: in `plan` the Adapter does not run edit tools at all, and in `acceptEdits` it
does not ask about edits. A handler that refuses edits for a reviewer would give the model an
error to react to on every attempt. A mode gives it a tool set that matches its job.

## Why `default` is not filtered out, when Effort's is

Effort's ladder carries an `"default"` sentinel, and the app filters it because *null means send
nothing*, a different wire behaviour from sending `"default"`
([rules.md](../engineering/rules.md), the Effort row). A mode list has no such sentinel:
`claude-agent-acp` advertises `default` as **Manual**, a real mode meaning "always ask before
making changes". Filtering it would remove the one way to choose it explicitly. Null still
means send nothing, and is the blank option.

## What was read, and what was not

Read from the vendored `claude-agent-acp` 0.75.1 `dist/` on 2026-09-30. The first set of facts
was run on 2026-09-30 (the option and its values, and resume). Three more were run on 2026-10-01
and are listed under "Established live" below.

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

**Established live.** A script drove the real 0.75.1 over stdio on Haiku (Windows, a Claude
subscription, one run each, **outside the Huddle app**, answering permission requests as
Huddle's handlers would). The in-app manual tests remain the acceptance run.

- A session set to `acceptEdits` and resumed came back in `default`, as the source said
  (2026-09-30).
- **Plan mode: the plan does not reach the Room as text.** The Adapter asked a `switch_mode`
  request titled "Approve Plan" with the whole plan in `rawInput.plan`. The guard refused with
  `reject_once`; the Turn ended `cancelled`, the only assistant text was a 126-character
  preamble, and no file was written. `RoomSession` posts no cancelled Turn, so a plan Persona
  is silent. This is why `plan` joined the default hidden list, the fallback the spec had named.
- **The `~/.claude` guard survives `acceptEdits`.** A write outside the Work Dir still produced
  a `session/request_permission` (kind `edit`, `rawInput` `{file_path, content}`), and refusing
  it left the file unwritten. The test path was a temp directory, not `~/.claude`, and
  `allow_always` was never chosen.
- Under `acceptEdits`, an edit inside the Work Dir produced no request, and the Turn ended
  `end_turn`.

- **OQ-5, on 2026-10-01 (no prompt sent):** `auto` on a model without support (Haiku, with no Turn running) answered `acceptEdits` in the `set_config_option` response, then sent an `agent_message_chunk` reading "Auto mode unavailable: the selected model does not support Auto mode; using Accept edits instead." and a `current_mode_update` to `acceptEdits`. Opus, Sonnet and the default accepted `auto`. Setting `auto` on Opus and then switching the model to Haiku moved the mode to `acceptEdits` with only a `current_mode_update`, no message chunk. The clamp is therefore visible in the response, which
  Huddle reads back, and the notice chunk arrives with no Turn running. `RoomSession` drops a chunk that
  arrives with no active Turn, so it is normally lost; a race with the first Turn would append it to that reply.
  `auto` stays hidden by default.

**In the running app, on 2026-10-01:** the manual tests WORKMODE-06 (no permission request for an edit under Accept edits), WORKMODE-07 (the request to leave plan mode is refused and nothing is posted to the Room, which confirms OQ-2) and WORKMODE-09 (the mode is applied again after a restart) passed. WORKMODE-08 (the `~/.claude` guard under Accept edits) first **failed** in the app, in Accept edits and in the Adapter's default mode alike: `SessionUpdateMapper.SerializeRaw` wrote the tool call's `rawInput` as `{"file_path":[]}`, so the guard never saw a path and approved the write. That predates Work Modes. With the `SerializeRaw` fix it passed: the write was refused and no file was created. **Not yet run in the app:** WORKMODE-01 to WORKMODE-05.

## Consequences

- A Work Mode change restarts the Persona and forgets its Room Sessions, so the Teammate loses
  its conversation memory, as a Model change already does.
- The resume match rule becomes Adapter, Model, Effort and Work Mode. Existing entries stay
  valid for a Persona with no Work Mode.
- Under `acceptEdits`, edits inside the Work Dir stop reaching the handler. The `~/.claude`
  write guard therefore sees fewer calls. A live run showed an outside-Work-Dir write still
  reaches it, and the spec keeps the case as a manual test.
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
| Offering every advertised mode | `bypassPermissions` is advertised on Windows, `auto` moves permission decisions to the model and silently changes with the Model, and `plan` leaves the Room with no reply until the plan is captured from the tool call |
| A blocking approval card for plan exit | [ADR-0022](0022-an-agent-asks-the-human-with-a-question.md) and roadmap §20 record why a request that waits collides with the idle watchdog. The later phase is non-blocking |
| A per-Room mode now | It needs new wire messages and a UI keyed by Room. It is the second phase, and this one is what it builds on |
