# Pending issues — to be filed on the issue board

Findings from the manual test run that could not be filed while the issue
tracker was unavailable. Each entry is written as a ready-to-paste issue: move
it to the board and delete it from here.

---

## 1. A **Next session** hook reaches a running session through `get_help`

**Found by** APPTOOLS-22 · **Severity** medium · **Area** `src/Huddle.App/Acp/Tools/GetHelpTool.cs`, `src/Huddle.App/Hooks/`

### What happens

The Hooks settings page badges `list_agents description` (and every other
`tool.*.description`) as **Next session**, meaning the edit is not supposed to
affect a teammate that is already running. It does affect one — immediately,
with no restart — because `get_help` renders the same hook live.

### Why

Each tool exposes its description as a live hook read:

```csharp
public string Description => this.hooks.Render("tool.getHelp.description", NoValues);
```

`GetHelpTool.BuildHelp()` runs on every call and walks `this.otherTools`,
reading each tool's `Description` at that moment. So the hook has two readers
with two different lifetimes:

| Reader | When it reads | Honours the badge? |
| --- | --- | --- |
| MCP `tools/list` (the session's tool schema) | once, at session start | yes |
| `get_help`'s tool catalog | on every call | **no** |

The badge describes the first reader only. The second is the one that matters
in practice: this application uses progressive discovery, so the system prompt
names tools without describing them, and `get_help` is how a model actually
learns what a tool does. Asked without calling anything, a teammate that had
not called `get_help` said it could not see the description at all.

### Reproduction

1. Start the app with `Nova` **Online** and Trace logging on.
2. Settings → Hooks → append a unique sentence to **list_agents description**
   (badged **Next session**) and Save. Do not restart anything.
3. In `Nova`'s room ask it to call `mcp__team__get_help`.
4. Ask: `Without calling anything, what does the description of
   mcp__team__list_agents say at the end?`

**Expected** — a teammate that is already running cannot be served the new
description until it restarts.
**Actual** — the Trace log's `App tool server replied to tools/call:` line for
`get_help` contains, inside its TOOLS catalog, the edited `list_agents`
description, served minutes after the save with no restart in between. Every
`tools/list` payload from session start still carries the original text.

Judge this on the wire payload, not on what the model then says. The payload is
what the tool server actually served; a model's answer can also come from an
earlier turn, or from a session that was replaced underneath it.

Use a *different* sentinel word in the **Next session** field from the one in
any **live** field. With the same word in both, the model can be repeating the
live hook it just read and the result proves nothing — the test's own
INCONCLUSIVE clause warns about this.

### Suggested fix

Decide which reader the badge describes, then make them agree. Either

- snapshot each tool's `Description` when the session's tool list is built, so
  `get_help` reports what the session's schema says; or
- re-badge `tool.*.description` as live and say so in the field's help text,
  since `get_help` is the surface a model actually reads.

The first keeps one session internally consistent, which is the property the
badge is promising.

---

## 2. No **Restart** control on the teammate card

**Found by** APPTOOLS-22 step 15 · **Severity** low · **Area** teammate card

APPTOOLS-22 instructs the tester to "click **Restart** on the card". No such
button exists — the card offers **Edit**, **Open** and **Remove**. Restarting a
session is reachable only as a side effect of **Edit** → **Save**, which does
work (a `phase=register` line for a new session id appears, and the previous
agent process logs `The agent process disconnected.`).

Either add the control the test expects, or amend the test to say that editing
and saving the Persona is the restart mechanism. Worth deciding rather than
leaving the test describing a button that is not there.

---

## 3. Unexplained ACP session churn: a session is created, then immediately disconnects

**Found by** PERSONALIFECYCLE-26, -28, HOOKSSETTINGS-39 · **Severity** low–medium · **Area** `src/Huddle.App/Acp/` (`DotAcpAgentHost`, `DotAcpClientAdapter`)

### What happens

Several times per run, the log shows this triple with nothing in the UI to match
it:

```
[agent stderr] [session/create] sessionId=<new> phase=register …
Dropping update for unknown session <that same id>
The agent process disconnected.
```

The teammate's tile stays **Online** throughout. No adapter process is replaced
— a genuine restart always replaces the Persona's `claude-agent-acp` node
process, so these are new ACP sessions inside the existing process.

### Why it matters

It is harmless to watch but it corrupts the obvious oracle. Counting
`phase=register` lines is the natural way to ask "did this restart the
teammate?", and the churn inflates that count: a save that restarts once can
show two or three new sessions. Three separate tests had to fall back on
counting adapter *processes* instead, and a reader of the log alone would
reasonably conclude that a no-op save restarts a teammate twice. It may also be
paying for session setup nobody asked for.

### What is known

- It follows activity, not time: 75 seconds idle produced none.
- It is not caused by saving the Edit card with nothing changed — a controlled
  repeat of that save produced zero new sessions.
- At least one instance directly followed a turn whose
  `post_turn_summary` carried `"status_category":"blocked"`.
- `Dropping update for unknown session` names the session that was *just*
  registered, which suggests a registration/teardown race rather than a stale
  message.

### Suggested next step

Log the reason a host is torn down, and correlate `The agent process
disconnected.` with the session id it belonged to. Right now that warning names
neither the Persona nor the session, so the log cannot say which teammate lost
its process or why — which is the same gap already filed as the disconnect
logging issue.
