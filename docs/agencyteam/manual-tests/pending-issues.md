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

**Expected** — the running session still has the old description.
**Actual** — it reports the new sentence. The Trace log shows why: the
`get_help` reply payload contains the edited `list_agents` description, while
every `tools/list` payload from session start still carries the original.

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
