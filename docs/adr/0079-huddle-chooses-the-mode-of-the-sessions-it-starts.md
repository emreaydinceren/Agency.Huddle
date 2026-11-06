---
status: accepted
date: 2026-11-06
---

# Huddle chooses the mode of the sessions it starts

A Persona with no Work Mode used to run in "the Adapter's default". On a machine whose `~/.claude/settings.json` sets
`permissions.defaultMode`, that default is the Human's own setting, so a Huddle session's permission behaviour depended on
whose machine it ran on. With `auto`, the Adapter's safety classifier decided each tool call in place of Huddle: in test
ZERO-26 it refused Devon's `update_task`, one of Huddle's own App Tools, and the Team stopped (issue #414). The same mode is
why the merge gate never ran until the hook of issue #396.

## Decision

**On an Adapter with `IsolateUserSettings`, a session whose Persona has no usable Work Mode runs in `default` (Manual).**
`WorkModePolicy.EffectiveMode` makes the choice and `DotAcpPersonaHost` sends it the way it sends a chosen Work Mode, through
`session/set_config_option`, at every open and every resume.

| Persona's Work Mode | Adapter isolates | Adapter does not isolate |
| --- | --- | --- |
| None | `default` | Nothing sent, the Adapter's own |
| An offered mode | That mode | That mode |
| A hidden mode, from a hand-written row | `default`, with a warning | Nothing sent, with a warning |

If the session already opened in the mode chosen, nothing is sent, so a machine with no `defaultMode` sees the same wire as
before. An operator who hides `default` with `Team:Acp:HiddenModes` is obeyed, and the session keeps the Adapter's own mode.

The user's settings file is not touched. The merge gate hook (`MergeGateHook`) stays: a user's `permissions.allow` rule, a
Work Mode that does not ask, or an Adapter entry that does not isolate can still skip the request it forces.

### Why the isolation flag

`IsolateUserSettings` already means "this session should not run on the Human's Claude settings". It did not cover this one,
because of what the Adapter does with it (below). An Adapter entry that leaves it off is warned about at startup, and may be
another Adapter, whose modes are its own.

## What the Adapter honours

Read from `claude-agent-acp` 0.100.0 `dist/` on 2026-11-06, then run.

- The first mode comes from `resolveInitialPermissionMode`: an explicit one, which only a live-session reload passes; then
  a resumed transcript's last mode; then `permissions.defaultMode`.
- `SettingsManager` reads that default through `resolveSettings({ cwd })`, which never sees `settingSources`. So
  the isolation `_meta` does not stop it, and a session isolated from the user's settings still started in their mode.
- `_meta.claudeCode.options.permissionMode` does nothing: it is spread into the options before `permissionMode:
  initialPermissionMode` overwrites it.
- `session/set_config_option` calls `query.setPermissionMode`, and a later settings change does not undo it.
- A resumed session keeps its transcript's mode. This corrects ADR-0033, which read an older Adapter: a session that once
  ran in `auto` comes back in `auto` unless Huddle sends a mode, so the choice is applied on resume as well.

## Measured on the real Adapter

`SessionModeProbeTests` (paid, `TEAM_E2E_SESSIONMODE=1`), on 2026-11-06, with this machine's `defaultMode: auto` in place and
the isolation `_meta` on every session. A control sent no mode, as Huddle did before.

| Scenario | Model | Mode sent | Session reports | Requests to Huddle | Result |
| --- | --- | --- | --- | --- | --- |
| Control: `update_task` | Sonnet | None | `auto` | 0 | Ran; the Adapter decided |
| `update_task` | Sonnet | `default` | `default` | 1, `AllowOnce` | Ran; Huddle decided |
| Merge the gate refuses | Haiku | `default` | `default` | 1, `RejectOnce` | Not merged |
| Merge the gate allows | Haiku | `default` | `default` | 1, `AllowOnce` | Merged |
| `git branch`, ordinary work | Haiku | `default` | `default` | 1, `AllowOnce` | Branch made, no Human |
| Merge the gate refuses, with a chosen Work Mode | Haiku | `acceptEdits` | `acceptEdits` | 1, `RejectOnce` | Not merged |
| Resume of the control's session | | None | `auto` | | |
| Resume of the control's session | | `default` | `default` | | |

The classifier did not refuse the control's call this time: it is not deterministic, which is the point. In `auto` the
decision about Huddle's own tool is not Huddle's, and in `default` the classifier is not on the path.

## Consequences

- **A permission request per command.** Each tool call the Adapter does not count as read-only now comes to
  `WorkDirPermissionHandler`, which approves it with no Human unless a guard or the merge gate refuses (a claude.ai connector tool is the one exception: it stops and asks, Agent Connectors spec 8.4). It is one local
  JSON-RPC round trip and spends no tokens. Every machine without `defaultMode` has always paid it.
- **More calls reach the guards.** The `AGENTS.md`, Raw, Prompts and Team Memory guards and the `~/.claude` refusal see an
  edit they would not have seen under a user's `auto` or `acceptEdits`.
- **No classifier in Huddle sessions.** A machine with `auto` loses the classifier's refusals of dangerous commands in these
  sessions, and behaves as every other machine does: ADR-0033's "auto-approve stays the handler's behaviour" still holds.
- Paid runs on this machine now match other machines, and earlier runs' results here were on `auto`.
- `Huddle.Acp` is unchanged, and a Persona's stored `WorkMode` is unchanged, so a Room Session's resume match still holds.

## Rejected

| Alternative | Why not |
| --- | --- |
| Editing the user's `~/.claude/settings.json` | It belongs to the Human, and their own coding agent reads it |
| `permissions.defaultMode` in the Work Dir's `.claude/settings.local.json` | A second writer for one fact, read at spawn with a trust filter over it, and invisible in the session's own response. `set_config_option` reads back what took effect |
| `_meta.claudeCode.options.permissionMode` | The Adapter overwrites it |
| Removing `mcp__team__` from the classifier with an allow rule | Leaves the session in the Human's mode, so shell commands still skip Huddle |
| The first advertised mode that is not hidden | Discovered rather than named, but another Adapter's first mode may be read-only |
