---
status: accepted
date: 2026-09-13
---

# Model-facing text is configuration, not source

Every word this application sent to a model was a string literal in C# — 22 of
them across five files. Changing one, to try a shorter orientation or reword the
reply rules, meant an edit, a rebuild and a restart. Prompt work is inherently
try-it-and-see, and that loop was too slow for it.

A **Hook** is one named piece of model-facing text. `HookCatalog` holds every
default in code; `{DataDir}/hooks.json` holds overrides only; `IHookSource`
resolves one over the other per key. The `/settings` page edits them.

Nothing about the application's behaviour changed. Four golden files pin the
composed output of all four surfaces, and every one reproduced byte-for-byte on
the first run of its conversion.

## A Hook is a template, not an event

The word is the repo owner's and the settings panel is named for it, but nothing
here executes, nothing subscribes, and the order blocks compose in is fixed in
code. A Hook is a named string with `{{placeholder}}` substitution.

This spends the word. If executable extension points are ever wanted at these
same sites — a block that shells out, a plugin — they will need a different name,
because "hook" will already mean a piece of text. That is recorded here rather
than discovered later.

## Two timings, because a system prompt cannot be changed

A system prompt is sent exactly once, in `session/new`'s `_meta.systemPrompt`.
There is no later event that re-reads it, which [Rules](../agencyteam/rules.md)
and [Traps](../agencyteam/traps.md) both already state for a Persona's text and
its Model.

So a Hook carries a `HookTiming`:

| Timing | Hooks | When an edit lands |
| --- | --- | --- |
| `Live` | Turn framing, the Room label, catch-up, every `get_help` section | The next turn. `get_help` re-renders on every call. |
| `NextSession` | The four system-prompt blocks, the five tool descriptions | Teammates started afterwards. Running ones keep what they were given. |

**Editing a Hook never restarts a session.** The alternative was considered and
rejected: restarting would make every edit take effect immediately, at the cost
of throwing away that Agent's conversation memory every time someone reworded a
sentence. `PersonaSupervisor.NeedsRestart` compares the `Persona` record, and a
Hook is deliberately not part of it.

The cost is that a `NextSession` edit is silently inert on a running Teammate.
The settings page carries a badge on exactly those fields, because that is the
one thing a reader cannot infer from the text itself.

## Defaults live in code, and the shipped file is generated from them

`HookCatalog` is the authority. `hooks.default.json` is generated from it, copied
beside the binary as the restore source, and pinned by a test asserting the two
still agree key for key.

The obvious alternative — the shipped JSON file *is* the defaults — was rejected
because it admits a state where the file and the code disagree about what the
application does bare. With the catalog as the authority that state cannot be
represented: delete every file and the app still runs on exactly the text it
shipped with.

An absent `hooks.json` is therefore normal, not an error, and the store does not
create one at startup. Writing 22 defaults nobody asked for would turn "I have
changed nothing" into a file that has to be kept in step with the code forever.

## The tool prefix is derived, never typed

[Rules](../agencyteam/rules.md) makes `mcp__team__*` binding: a tool named loosely
makes the model report that no such tool exists, and it fails silently — nothing
in any log says why. Letting a human hand-edit the prompt that names those tools
is exactly the way to reintroduce that.

So no Hook's text contains the prefix. `{{toolNames}}`, `{{toolName}}` and
`{{helpTool}}` are filled by code from the live tool roster, and the prefix is
built from the same `ToolServerName` constant handed to `AppToolServer`. One
line of executable code in the application names the tool server. A user can
choose not to mention the tool list — which `HookValidator` flags — but cannot
misspell a tool into nonexistence.

`{{…}}` rather than `<…>` for the same class of reason: `get_help` sends the
model the literal line `"[Room: <name> (id: <id>)]"` as documentation, and an
angle-bracket syntax would have silently eaten it.

## Validation reports and never refuses

`HookValidator` finds a missing required placeholder, a prompt that no longer
names every tool, an unknown token, an empty block. Every one is advisory.

A validator that blocks a save is a validator users route around, and the whole
point of this feature is trying wordings no checker can judge in advance. An
`Error` means "this will probably not work", never "this is refused".

## The open collision: two config channels for one tool surface

Roadmap item 9, **Per-Persona tool grants**, is not built yet. It touches the
same construction block in `DotAcpAgentHostFactory` and the same `get_help` body
this ADR just made configurable, and it chose a *different* configuration
channel: `_`-prefixed keys in a Persona's own frontmatter.

```yaml
_tools: [list_agents, follow_room]
```

If both ship as designed, the repo has two ways to configure one tool surface —
a global JSON file and a per-Persona frontmatter field — and no rule saying which
wins where.

That is not resolved here, deliberately: item 9 is not built, and inventing its
configuration story in advance would be guessing. What this ADR fixes is that
**whoever builds item 9 must decide it explicitly**, and has two coherent options:

- Hooks stay global and item 9 stays per-Persona, on the grounds that *which
  tools an Agent has* is a property of that Agent while *how the tools are
  described* is a property of the application. This is the cheaper reading and
  probably the right one.
- Hooks grow a per-Persona layer, and item 9 becomes one more thing that layer
  carries. This is more uniform and considerably more work, and it would make
  `IHookSource` resolution depend on which Persona is asking.

Item 9's roadmap entry should be read together with this section before it is
started.

## What this does not cover

Tool *result* strings (`"Created room …"`, `"Unknown agent …"`), the Budget
refusal in `ChatService`, and `AppToolServer`'s error results are still literals.
They are behaviour rather than wording, several are pinned character-for-character
by tests, and making them configurable buys prompt-experimenting value that is
unlikely to be used. That line may move later; it was drawn deliberately.

The Budget refusal in particular must stay one source feeding both doors into a
post — [Rules](../agencyteam/rules.md) makes its terminal wording binding — so if
it ever becomes a Hook it becomes exactly one Hook, not two.
