# Huddle.TeamPlaybooks — Design Specification

**Date:** 2026-11-18 · **Status:** Built 2026-11-18 (PR 678) ·
**Decision record:** [ADR-0098](adr/0098-a-team-has-a-manager-and-a-member-playbook.md), which amends
[ADR-0041](adr/0041-retro-proposals-are-applied-only-by-the-humans-click.md) and
[ADR-0075](adr/0075-a-second-brain-layout-in-every-team.md); §13 holds the decisions this spec adds ·
**Builds on:** [ADR-0007](adr/0007-model-facing-text-is-configuration.md) (Prompts),
[ADR-0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) (wakes),
[ADR-0076](adr/0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md) (Turn hooks),
[ADR-0071](adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md) (Team Memory),
[ADR-0089](adr/0089-a-rooms-project-files-the-tasks-asked-for-in-it.md) (a child files in its parent's Project),
[ADR-0096](adr/0096-a-review-is-a-task-owned-by-the-reviewer-not-a-state.md) (a review is a Task)

This is the design for **Team Playbooks**. A Playbook says how to be a member or the manager of a Team, in any kind
of work. It also covers **missions**: a mission is a Project's goal, a Task the Human assigns the manager, which the
manager keeps in the Project's Team Memory. Huddle adds no field for it. ADR-0098 made the decisions; this page
settles what it left open and says what to build, in what order, and how to test it.

It is written for the engineers or agents building it, with no memory of the conversation that produced it. §3 is
the model, §4 the shipped texts, §5 the files, §6 delivery to the Agent, §7 the Playbooks tab, §8 missions, §9
retros, §10 Process trimming, §11 the docs that change, §12 the slices with their tests, and §13 the decisions.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here overrides either.

---

## 1. Goal

1. **Every Agent in a Team knows how to work in one.** A member does a step, checks it, decides the next one and
   records it on the Task. The manager does the same on a mission. Neither needs a Skill to learn it.
2. **The text is the Human's, the mechanics are Huddle's.** The Human edits a Playbook Huddle-wide or for one Team.
   The part the code enforces, the **Huddle rules**, stays readable and cannot be edited away.
3. **An improved default reaches every Team that did not customize.** A Team that did is told when the default
   moved on, shown what changed, and can reset.
4. **A Project's mission costs no new mechanism.** The Human gives it as an ordinary Task assigned to the manager,
   the existing assignee wake fires, and the manager keeps the goal in the Project's Team Memory, which every member
   already reads.
5. **Process gets shorter.** The generic paragraphs copied into the built-in process files move into the Huddle
   rules, once.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U1 | The Human opens a Team page | The tab rail reads Members, Library, Process, Playbooks, Details. Playbooks shows a Manager and a Member section, each with its Huddle rules above the text and a chip saying "Huddle default" |
| U2 | Nova, a member of *Sales* only, starts a session | Its system prompt holds the Member Huddle rules and the Member Playbook, after the `AGENTS.md` block. No Manager text |
| U3 | Bo manages *Sales* | Bo's prompt holds both Huddle rules and both Playbooks, the Member ones first |
| U4 | The Human clicks **Customize for this Team** on *Sales*' Member section | `Teams/Sales/_member-playbook.md` is written with the current default and its `basedOn` hash, and the Library opens it for editing. The chip reads "Customized for this Team" |
| U5 | The Human saves an edit to that file | At their next Turn in each Room, *Sales*' members get one block saying the Member Playbook for *Sales* changed, with its new text |
| U6 | Later the Human edits `systemPrompt.memberPlaybook` on Settings › Prompts | Teams without a file get the new text at their next Turn. *Sales*' section shows **Default updated**, with the change from the old default to the new one |
| U7 | The Human clicks **Keep my version** | The notice goes; the file's `basedOn` now names the current default. Its text is unchanged |
| U8 | The Human clicks **Reset to default** and confirms | The file is deleted. *Sales*' members get the default at their next Turn |
| U9 | Ivy is a member of *Sales* (customized) and *Ops* (default) | Ivy's prompt holds the Member Huddle rules once, the default Member Playbook once labelled "for Ops", then *Sales*' own labelled "for Sales" |
| U10 | The Human moves SALES-0012 into the *Launch* Project, assigns it to Bo and says in the Team Room "SALES-0012 is Launch's goal" | Nothing new in Huddle: one ordinary Task change, and Bo is woken by the existing assignee rule (ADR-0026) |
| U11 | Bo's Turn | Following the Manager Playbook, Bo writes `Teams/Sales/Launch/memory/mission.md` (the goal, what finished looks like, `SALES-0012`) with the `memory` tool, then creates child Tasks with `parent: SALES-0012` and no `project`; they are filed in *Launch* (ADR-0089) |
| U12 | Nova, a member of *Sales*, starts a session | The Team Memory listing in its prompt already includes *Launch*'s `mission.md` and its description |
| U13 | An Agent's Write or Edit targets `_member-playbook.md` or `_manager-playbook.md` | Refused by the permission handler, as an `AGENTS.md` write is |
| U14 | A retro proposes a change to the Member Playbook of a Team that never customized | The card shows the change against the default text, titled "Customizes the Member Playbook". Accept writes the file |
| U15 | The Chief of Staff finishes building a Team | It files the goal it collected as a Task in the new Team, assigned to its manager, and tells the Human to move it into a Project when they create one |

---

## 3. The model

### 3.1 Parts and their sources

Each Playbook has two parts. The **text** is editable; the **Huddle rules** are not.

| Part | Source when not customized | Source when customized | Who edits |
| --- | --- | --- | --- |
| Member Playbook text | Prompt `systemPrompt.memberPlaybook` (override in `prompts.json`, else the shipped default) | `Teams/<Team>/_member-playbook.md` | The Human: Settings › Prompts Huddle-wide, the Library for one Team |
| Manager Playbook text | Prompt `systemPrompt.managerPlaybook` | `Teams/<Team>/_manager-playbook.md` | The same |
| Member Huddle rules | Embedded `Playbooks/HuddleRules/member.md` | The same: never per Team | No one; it ships with the app |
| Manager Huddle rules | Embedded `Playbooks/HuddleRules/manager.md` | The same | No one |

The **effective default** is the Prompt's resolved text: the Human's Settings override when there is one, else the
catalog default. Everything below that says "the default" means the effective default.

### 3.2 Who reads what

| Agent | Reads |
| --- | --- |
| In no Team the catalog lists | No Playbook |
| A member of one or more Teams | Member Huddle rules, then each of its Teams' Member Playbooks (§6.2) |
| The manager of one or more Teams (`manager:` in `_team.md`, `TeamSummary.Manager`) | The above, then Manager Huddle rules, then the Manager Playbook of each Team it manages |

### 3.3 States of one Playbook for one Team

| State | When | Tab shows (§7) | Agent gets |
| --- | --- | --- | --- |
| **Default** | No file | The effective default, read-only, chip "Huddle default" | The effective default |
| **Customized** | The file exists and its `basedOn` equals the hash of the effective default | The file, chip "Customized for this Team" | The file's body |
| **Default updated** | The file exists and its `basedOn` differs from the effective default's hash | As Customized, plus the notice and the diff | The file's body: never the new default |
| **Unknown base** | The file exists with no readable `basedOn` (made by hand) | As Customized, with the caption "Huddle can't tell which default this started from." and **Keep my version** | The file's body |
| **Unreadable** | The file exists and cannot be read | An error alert naming the path | The effective default, and a logged Warning |

---

## 4. The shipped texts

Each text is built on the loop of ADR-0098: do a step, check the result, decide the next step, record it. The
Huddle rules name tools through placeholders the code fills with the Adapter's prefix (rules.md: "A Prompt's text
never contains `mcp__team__`"). The editable texts name **no tool**, only actions ("comment on the Task"), so a
Human's edit can never misspell a tool name; the Huddle rules carry the names (D-7).

A placeholder whose tool the session does not offer renders "(not available here; tell the Human)", as
`SystemPromptComposer.ToolUnavailable` does today.

### 4.1 Member Huddle rules (`Playbooks/HuddleRules/member.md`)

Placeholders: `{{addCommentTool}}`, `{{addEvidenceTool}}`, `{{logChangeTool}}`, `{{createTaskTool}}`, `{{updateTaskTool}}`.

```markdown
Huddle enforces these rules. Where the Playbook text says otherwise, these win.

- Record on the Task. A note goes on the Task with {{addCommentTool}}. A check you ran is Evidence, posted
  with {{addEvidenceTool}}; when the Team's Definition of Done names a command, the Evidence carries that
  command and its exit code. Gates read the Task, not the Room.
- A draft for review is a file. Save it as Markdown in the Team's `outputs/` folder and put its full path, in
  backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs
  the review on it with {{logChangeTool}} and the kind `reviewed`, and only then posts Evidence. A draft
  written only in the description does not meet the review item.
- A review is a Task, not a state. To ask for one, create it with {{createTaskTool}} as a child of the
  work Task, tagged `review` (`verify` for a QA check) and assigned to the reviewer, then add it to the
  work Task's `blocked_by` with {{updateTaskTool}}. The work Task stays In Progress. A review Task is Done
  only when the reviewer approves; findings go on it as a comment and it stays open.
- A refusal from a gate names what is missing. Do that and try once more. If you cannot, say so on the Task
  and stop; never repeat the same call unchanged.
- A Request in the Backlog is not work yet. It leaves the Backlog only when the manager triages it.
- `AGENTS.md` and the Playbooks are the Human's files. Huddle refuses your edits to them; propose a change
  in a retro instead.
```

### 4.2 Manager Huddle rules (`Playbooks/HuddleRules/manager.md`)

Placeholders: `{{triageRequestTool}}`, `{{closeTaskTool}}`, `{{createTaskTool}}`, `{{memoryTool}}`.

```markdown
These add to the Member rules, for the Team you manage.

- Only a manager files and triages Requests. Triage one in your Backlog with {{triageRequestTool}}:
  accept, decline or ask for more information; declining and asking both need a reason.
- Closing a Task with {{closeTaskTool}} is not marking it Done: use it for a question you answered, with the
  answer recorded on the Task first.
- Done is reached only through the Team's Definition of Done and gates, for a mission as for any Task.
- Only the Human creates a Project.
- A child Task made with {{createTaskTool}}, `parent` set and no `project` is filed in its parent's Project.
- You write the Team Memory of the Teams you manage, a Project's included, with {{memoryTool}}.
```

### 4.3 Member Playbook default (`systemPrompt.memberPlaybook`)

```markdown
## How to work on a Task

Work in passes. Each pass is one loop:

1. **Do one step** of the Task: one piece you can finish and check.
2. **Check it** against what the Task says finished looks like. Where the Team asks for proof, gather it.
3. **Decide what comes next:** go on, ask for a review, say what blocks it, or ask the Human.
4. **Record it** as a comment on the Task: what you did, what you checked, what comes next. Post proof as
   Evidence. End the comment with a line `State: done`, `State: handed off`, `State: blocked` or `State: waiting for the Human`. The next pass, yours or anyone's, starts from that comment, not from your memory.

End every pass in one named state: **done**, **handed off**, **blocked** or **waiting for the Human**. Name it on the last line of the comment, as `State: handed off`. Do not
start another pass on the same Task unless something changed.

## Handing off

Say what is finished, where the file is, what you checked and what is left. Name who takes it next. To ask a review, create the review Task the Huddle rules describe, rather than only asking in a Room.

## When you are blocked

Say on the Task what blocks it and who can unblock it. If another Task blocks it, link that Task. Then stop and
take other work.

## Asking

- Look first. Don't ask for a fact you can look up or infer, an opinion the Human wants from you, or a detail
  they already gave.
- When you need the Human's preferences, constraints or goals, ask with options they can tap rather than a
  list of questions.
- Write "@all" only when every other Agent must answer, never to reply to one member.
- When you and another Agent disagree, put both views on the Task and bring it to the Human.

## Scope

Do the Task you were given. If you find more work, propose it as a new Task; don't widen this one. When the
Task is in a Project, that Project's `mission.md` in Team Memory says what the Project is for: keep your work
pointed at it.
```

### 4.4 Manager Playbook default (`systemPrompt.managerPlaybook`)

```markdown
## How to run the Team

You are also a member: the Member Playbook applies to you. This adds what a manager does.

### Triage first

Each time you are woken, sort what landed on you:

- **A question:** answer it on the Task and close it.
- **Small work:** assign it as it is, to one owner.
- **A mission:** break it into child Tasks (below).
- **An ask made in a Room:** write it as a Task first, so it is on record.
- **An ask too large for a few Tasks:** suggest to the Human that they start a Project for it. Don't start one.

### Running a mission

A mission is a Project's goal. The Human gives it to you as a Task in the Project, assigned to you, and says it
is the goal; if you are not sure a Task is one, ask. Then:

- Write it to the Project's Team Memory as `mission.md`: the goal in a line or two, what finished looks like,
  and the Task's id. Keep it that short, and correct it in place when the goal changes.
- Break the Task into child Tasks under it on your first pass, even when part of it waits on the Human. A Task that needs the Human's input is a child Task assigned to no one, or a question to the Human. It never stops the rest of the plan.

Run it in passes, like any Task:

1. **Do one step:** assign a child Task, unblock one, review one, or add one.
2. **Check** the child Tasks' states and Evidence against the mission.
3. **Decide** the next assignment, a new Task, a question for the Human, or that the mission is ready to
   verify.
4. **Record** the decision as a comment on the mission Task. Your next wake-up starts from it, so write what
   you decided and why, and what you are waiting for. End it with a `State:` line, as the Member Playbook says.

Give each child Task one owner, a title, and a line saying what finished looks like. Keep few Tasks in
progress at once. Creating Backlog Tasks needs no approval: create them and carry on.

When every child Task is Done or no longer needed, verify the mission Task against what finished looks like and
record Evidence that links each child Task. Archiving the Project is the Human's call.

### Reporting

In a digest or a report, say what is done, what is due next, what slipped and what needs the Human, each with
its Task. Keep it short.
```

### 4.5 Size

| Text | Shipped length (chars, LF) | Ceiling pinned by `PlaybookTextSizeTests` |
| --- | --- | --- |
| Member Huddle rules | about 1,550 | 1,800 |
| Manager Huddle rules | about 800 | 1,500 |
| Member Playbook default | about 1,750 | 2,500 |
| Manager Playbook default | about 2,000 | 2,500 |

Measured from the drafts above, before placeholders are filled. A manager in one Team reads about 6,400 characters
of Playbook with the frame and headings, roughly 1,600 tokens; a member about 3,500. That is the size of the shipped
Huddle-wide `AGENTS.md` (2,042 characters) plus one Skill body, and was chosen with Claude Haiku in mind (D-3). The
ceilings are pinned so a later edit that grows a shipped text fails a test, and the exact texts are pinned by the
prompt goldens.

---

## 5. Files and storage

### 5.1 A customized Playbook file

`Teams/<Team>/_member-playbook.md` and `Teams/<Team>/_manager-playbook.md`. UTF-8, any line endings. The leading
`_` keeps the catalog from reading them as Projects (`TeamNames.IsReservedProjectName`). The Library refuses
`_`-prefixed names under Teams (`_team.md` included); `LibraryPathResolver` lets these two through, directly in a
Team folder only, so the Library lists and opens them.

```markdown
---
basedOn: 'sha256:3f9a0c1e7b2d4a65'
---
## How to work on a Task
…
```

- `basedOn` is the first 16 hex characters of the SHA-256 of the effective default's text, LF-normalised and
  trimmed, at the moment the file was written by **Customize**, **Keep my version** or a retro Accept (§9). It
  names a default, not a date.
- The **body** is what the Agent reads. The frontmatter never reaches a model, as with `AGENTS.md` (`AgentsMdBody`).
- A file the Human creates by hand without frontmatter is valid: its state is **Unknown base** (§3.3).
- A `PlaybookFile` record parses and composes it, with the shape of `ProjectFile`: unknown frontmatter lines are
  kept verbatim, and `Parse` never fails.

### 5.2 The base store (open item 1)

To show what changed in the default, Huddle needs the old default's text, not only its hash. It is kept in
`{DataDir}/playbook-defaults/<kind>-<hash>.md`, one file per distinct default ever customized from, written by
`PlaybookBaseStore` whenever it stamps a `basedOn`. A base file is never edited. At startup a base no
customized file names is deleted. The folder is Huddle's, not the Human's, so it is under `{DataDir}`, not
`Teams/`, and is not in the Library.

**Default updated** is computed, never stored:

```text
state = file missing                           → Default
        basedOn unreadable                     → Unknown base
        basedOn == Hash(effective default)     → Customized
        otherwise                              → Default updated
diff  = RetroDiff.Unified(name, base(basedOn), effective default)   when the base file exists
```

When the base file is missing, the notice still shows, with "Huddle no longer has the earlier default, so it can't
show what changed." in place of the diff.

**The Huddle rules are not compared** (D-2). They are never in a Team's file, so a Team cannot fall behind them:
every Agent always gets the shipped rules. A change to them arrives with an app update and reaches running Agents
through the Turn hook (§6.4). The tab has no **Default updated** state for them.

### 5.3 Configuration

Two keys join the `Team:` configuration in [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md):

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:Playbooks:MaxChars` | 4000 | The most of one Playbook's text an Agent receives; the rest is cut (§6.3) |
| `Team:Playbooks:MaxTotalChars` | 16000 | The most all Playbook parts of one session may hold (§6.3) |

---

## 6. Delivery to the Agent

### 6.1 Composition

`SessionPromptInputs` builds a `PlaybookSnapshot` beside the `AgentsMdSnapshot`, from the Persona's `teams`
labels in their written order, spelled as the catalog spells each Team (as `AgentsMdScopes.Scan` does). The
snapshot is pure data: a list of `PlaybookSection(Kind, Teams, Text, Source)`.

`SystemPromptComposer.ComposeParts` gains `AddPlaybookParts(parts, prompts, playbooks, toolNames)` right after
`AddAgentsMdPart` and before `AddIdentityParts`, so the order becomes: orientation, Persona text, `AGENTS.md` block,
**Playbooks**, identity, and the rest unchanged. A `null` or empty snapshot leaves the prompt byte-identical, so every
existing golden is unchanged for an Agent in no Team.

| Part (label) | Source (`PromptPartSource`) | Text |
| --- | --- | --- |
| Playbooks | `Prompt("systemPrompt.playbooks")` | The frame: one new Prompt, below |
| Huddle rules (Member) | new `Embedded("huddleRules.member")` | `## Huddle rules for members` and the rendered rules |
| Member Playbook | `Prompt("systemPrompt.memberPlaybook")` for the default, `File(path)` for a Team's | One heading per section (§6.2) and the text |
| Huddle rules (Manager) | `Embedded("huddleRules.manager")` | Only for a manager |
| Manager Playbook | As Member | Only for a manager |

The frame, `systemPrompt.playbooks` (Timing `Live`, no placeholders):

> Your Teams' Playbooks follow. They say how to work in a Team, whatever the work. The Huddle rules are enforced by
> Huddle. The Playbook text is the Human's; where a Team has its own, it replaces the default for that Team only.

`PromptPartSource.Embedded` is new: the Turn detail and Prompt blocks pages show it as "Built in, not editable".

### 6.2 An Agent in several Teams (open item 2)

Order and labels:

1. Member Huddle rules, once.
2. **The default Member Playbook, once,** headed `## Member Playbook (Huddle default) for Ops and Docs`, naming
   every one of the Agent's Teams that has no file, in `teams` order. Left out when every Team has its own.
3. **Each customized Member Playbook,** in `teams` order, headed `## Member Playbook for Sales (this Team's own)`.
4. For a manager: Manager Huddle rules once, then the default and the customized Manager Playbooks of the Teams it
   manages, by the same two rules.

Two Teams whose files happen to hold the same text are still sent separately: equality of text is not checked, so
a label never names a Team whose file it did not come from. Member before Manager, because the Manager Playbook adds
to the Member one (ADR-0098).

### 6.3 Size limits at delivery (open item 3)

- **Per Playbook:** a body longer than `MaxChars` is cut at the last line break before it, and the line "The rest of
  this Playbook is in `{path}`; read it before you work on this Team's Tasks." follows (a Prompt,
  `systemPrompt.playbookCut`). An Adapter without file tools gets "The rest was cut; ask the Human to shorten this
  Playbook." instead (`systemPrompt.playbookCutNoFiles`). The default is never cut: its shipped ceiling is below
  `MaxChars`, and an override over it is cut the same way.
- **In total:** when the parts would pass `MaxTotalChars`, customized Playbooks are replaced, last in `teams` order
  first, by the pointer line alone. The Huddle rules and the defaults are never dropped.
- Neither limit refuses a save. The tab warns (§7.3), as `PromptValidator` reports and never refuses (rules.md).

### 6.4 An edit reaches a running Agent

A new Turn hook, `PlaybooksTurnHook` (`HookName = "Playbooks"`), joins `TurnHookPipeline.ProductionOrder` right
after `AgentsMdTurnHook`: Playbooks rank below the Human's `AGENTS.md` rules and above Memory and File Changes. It
follows the ADR-0076 pattern (D-15, D-16, D-17), and is one class and one entry there.

- **State:** the JSON of a map from section key to hash, `started` and `heard`, as `AgentsMdTurnHook` keeps.
  Section keys: `rules:member`, `rules:manager`, `member:default`, `manager:default`, `member:<Team>`,
  `manager:<Team>`. The hash is of the text as sent, heading included, after rendering and cutting, so a change to
  the Teams a default section names (a Reset, or joining a further default Team) is told with its text.
- **Tool names in told rules:** the hook cannot see the session's tool list, so Huddle rules told mid-session (an
  Agent joins its first Team or becomes a manager) name the tools bare (`add_comment`), not with the Adapter's
  prefix. The next session's system prompt names them as offered (D-13).
- **Fresh open:** records the current map as heard. **Compacted:** resets heard to started, as the AGENTS.md hook does.
- **Before each Message Turn:** builds the current snapshot for the Persona with the same builder §6.1 uses, and
  compares. For each section that is new or whose hash changed, it adds the section's whole current text under
  `turn.playbookChanged`:

  > The {{playbook}} changed. It applies from now on and replaces what you read before:
  >
  > {{text}}

  For a section that is gone (the Agent left the Team, or stopped managing it) it adds `turn.playbookGone`: "The
  {{playbook}} no longer applies to you." The block opens once with `turn.playbookHeader`: "Your Playbooks changed
  since you last read them." `{{playbook}}` is the section's heading text, such as "Member Playbook for Sales (this
  Team's own)".
- **A Reset** is a change of the `member:<Team>` section into a `member:default` entry: the Agent is told the Team now
  uses the default, with its text.
- **A Settings › Prompts save** of a Playbook key changes `member:default` or `manager:default` for every session
  that reads it, and reaches each at its next Turn. This is why both keys are Timing `Live` (D-8): the settings page
  must not badge them "next session".
- **No Helper call.** A Playbook is capped at a few thousand characters, so the whole text is cheaper and more
  exact than a summary. This is the difference from the AGENTS.md hook (D-9).
- **File Changes:** `_member-playbook.md` and `_manager-playbook.md` in a Team folder leave the File Changes report,
  as an in-scope `AGENTS.md` does (ADR-0076 D-12), so an Agent is not told twice.

### 6.5 Existing installs

Installed Teams keep their `AGENTS.md`, which Huddle never overwrites, so a Team hired before this feature still
carries the draft-is-a-file paragraph in its process and now also reads it in the Member Huddle rules. ADR-0098
accepts that as harmless. The same holds for an installed Huddle-wide `AGENTS.md` that still carries the "@all" and
asking lines.

---

## 7. The Playbooks tab

### 7.1 Where it sits

`TeamPageTab` gains `Playbooks` (label "Playbooks", segment `playbooks`, icon
`Icons.Material.Outlined.MenuBook` in `TabIcons`). `TeamPageTabs.For` adds it on a Team page, after Process and
before Details, whatever features are on: Playbooks need neither the Library nor Tasks to be read. A Project page
has no Playbooks tab.

### 7.2 Layout

```text
M15  PlaybooksTab (section.playbooks-tab)
├── M15a  section.playbook-section[data-kind=manager]
│   ├── M15b  header row: group-label "Manager Playbook" + state chip
│   ├── M15c  Huddle rules: MudPaper Outlined (.playbook-rules), lock icon, caption, rendered Markdown
│   ├── M15d  Default updated notice: MudAlert Info (.playbook-updated), with its own actions and diff
│   ├── M15e  the text: div.playbook-text.library-rendered
│   └── M15f  action row: MudStack Row (.playbook-actions)
└── M15a  section.playbook-section[data-kind=member]  (same children)
```

Manager first, then Member, the order ADR-0098 names them. Each section is a `section` with `aria-label` "Manager
Playbook" or "Member Playbook".

### 7.3 Texts and states

Settled here, so the tests can assert them exactly (agents/Testing.md):

| Container | Default | Customized | Default updated | Unknown base |
| --- | --- | --- | --- | --- |
| `M15b` chip | "Huddle default" | "Customized for this Team" | "Default updated" (Color.Info) | "Customized for this Team" |
| `M15c` caption | "Huddle enforces these. They are always sent and can't be edited." | same | same | same |
| `M15e` | The effective default, rendered | The file's body, rendered | The file's body | The file's body, then the caption "Huddle can't tell which default this started from." |
| `M15f` buttons | **Customize for this Team** (Outlined, Small, Edit icon), **Edit the default** (Text, Small) | **Edit** (Outlined, Small), **Reset to default…** (Outlined, Small) | as Customized | **Edit**, **Keep my version**, **Reset to default…** |

- **Customize for this Team** writes the file with the effective default as its body and the current `basedOn`,
  stores the base (§5.2), then opens the file in the Library editor, as the Process tab's **Edit** opens `AGENTS.md`
  (`ProcessTab.Edit`). It needs the Library on; with it off, the button is disabled with the tooltip "Turn on the
  Library to customize a Playbook." A file that already exists (written meanwhile) is opened, not overwritten.
- **Edit the default** navigates to Settings › Prompts.
- **Edit** opens the file in the Library editor. There is no editor on the tab: the Library's (Edit, Split,
  `MarkdownToolbar`) is the one Markdown editor (UXDesignGuidelines catalog).
- **The `M15d` notice** reads "Huddle's default {Member|Manager} Playbook changed after this Team customized it."
  Its actions: **Show changes** toggles `pre.playbook-diff` under it (the unified diff of §5.2, the retro card's
  `pre.retro-change-diff` look); **Keep my version** restamps `basedOn` to the current default and stores the base,
  leaving the body as it is; **Reset to default…** as below. With no base file the diff is replaced by the sentence
  in §5.2 and **Show changes** is absent.
- **Over the limit:** a body longer than `MaxChars` shows `MudAlert` Warning above `M15e`: "This Playbook is {n}
  characters. Agents read the first {max}; shorten it." (numbers with `N0`, invariant culture).
- **Unreadable:** `MudAlert` Error, `role="alert"`: "Huddle couldn't read {path}. Agents get the Huddle default until
  it can be read." `M15e` then shows the default.
- The tab follows `TeamCatalog`-style file watching: `PlaybookStore.Changed` fires on a write to either file in the
  Team folder, and on a save of either Prompt key, and the tab re-renders.

### 7.4 Reset and its confirm (`O15`)

**Reset to default…** opens the shared confirm, `DialogService.ConfirmAsync`, with Cancel first (UX-R12):

- Title: "Reset the Member Playbook of Sales?" (or Manager)
- Message: "This Team's own Member Playbook is deleted, and its members read the Huddle default from their next Turn."
- Verb: **Reset**, `danger: true`.

**Reset** deletes the file. Its base is pruned at the next start if nothing else names it. A Library history entry,
if the Library keeps one for that file, is the Library's business and is not promised in the copy.

### 7.5 Settings › Prompts

The two keys appear in a new group, "Playbooks", with:

| Key | Label | Helper text |
| --- | --- | --- |
| `systemPrompt.memberPlaybook` | Member Playbook | How every member of a Team works. A Team with its own Member Playbook ignores this one. Running Agents hear of an edit at their next Turn. |
| `systemPrompt.managerPlaybook` | Manager Playbook | How a Team's manager runs it; it adds to the Member Playbook. A Team with its own ignores this one. Running Agents hear of an edit at their next Turn. |

Both carry the "relied" warning (`OverlayPromptFiles`' `relied: true`). The Huddle rules are not on this page.

### 7.6 Guarding the Playbook files

A new guard, `HumanTeamFilesGuard`, joins `PermissionGuardList.InOrder` right after `AgentsMdGuard`. It refuses an
Agent's Edit, Write, Delete or Move of `Teams/<Team>/_member-playbook.md` and `Teams/<Team>/_manager-playbook.md`
(depth 2), and of the same paths through an 8.3 short name or a stream name, refused outright, as
`AgentsMdGuard.UsesAlias` does. The reason is a new Prompt, `systemPrompt.teamFileRefusal`: "This file is the
Human's. Propose a change in a retro, or ask the Human." Reads are never refused (D-4).

**What it does not stop.** Like every path guard, it does not see `Bash`, `MultiEdit` or `NotebookEdit`, nor the
Human editing the file by hand. That soft edge is the same one `AGENTS.md` has, and is listed in
[known-limits.md](engineering/known-limits.md) (§11).

---

## 8. Missions

A mission is a Project's goal. ADR-0098 keeps it out of Huddle's data: there is no `mission:` key, no service, no tag,
no picker and no Project page area. It is two habits the Playbooks teach (§4.3, §4.4) on top of what already exists.

### 8.1 How one starts

1. The Human puts a Task in the Project (it may have come from the Human, the Chief of Staff or an accepted Request)
   and assigns it to the Team's manager, as any Task. That one Task change wakes the manager through the assignee
   rule (ADR-0026).
2. The Human says the Task is the Project's goal, in the Task or in a Room. Knowing it is the manager's judgement;
   when unsure, it asks (§4.4).

### 8.2 What the manager keeps

The manager writes `mission.md` to the Project's Team Memory, `Teams/<Team>/<Project>/memory/`, with the `memory`
tool, as it writes any Team Memory (ADR-0071). The file follows the Memory format, so it needs a description:

```markdown
description: Read when you work on a Launch Task: the Project's goal and what finished looks like.

Ship the spring catalogue to the three retail partners by 31 March.
Finished: each partner has confirmed receipt of the final PDF.
Task: SALES-0012
```

- **Every member sees it.** `TeamMemoryIndex` already lists the Team-wide and per-Project Memory of every Team a
  Persona belongs to, so the file's name and description are in each member's prompt with no new part.
- **It stays short.** A summary and the Task id, never a copy of the Task: the Task holds the work, its Timeline and
  its Done.
- **It is corrected in place** when the goal changes ("keep a fact once"). Memory Tidy leaves Team Memory alone
  (ADR-0095).

### 8.3 Breaking it down and finishing

Child Tasks get `parent` set to the mission Task and no `project`; `create_task` files them in the parent's Project
(ADR-0089). The mission is met when its Task is Done, through the Team's Definition of Done and gates. Archiving the
Project stays the Human's.

### 8.4 Team-building (open item 4)

In step 6 of the `team-building` Skill, for every Team the Chief of Staff builds, from a type or from proposed Agents,
it files the goal it collected in step 1 as one Task in that Team with `create_task`: the title is the goal, the
description says what a finished result looks like, and the assignee is the Team's manager. When the Team has no
manager, it leaves the Task unassigned in the Backlog and tells the Human to assign it once they name a manager. It then
tells the Human: "When you create this Team's first Project, move {Id} into it; {Manager} will treat it as the Project's
goal." The Skill gains that step and its sentence; no tool changes (D-10).

---

## 9. Retros (amends ADR-0041)

- `propose_retro_changes` accepts `_member-playbook.md` and `_manager-playbook.md` as a `targetFile`, beside
  `AGENTS.md`, `_team.md` and `Name.md`. Its refusal becomes "targetFile must be AGENTS.md, _team.md,
  _member-playbook.md, _manager-playbook.md or the file of an Agent (Name.md)." and `tool.proposeRetroChanges.description`
  names the two files.
- **A customized Playbook:** the change is a find and replace against the file's body, exactly as for any target.
- **An uncustomized one** (no file): the find runs against the effective default. The card's file line reads
  "{file} (customizes this Team's Playbook)". **Accept** writes the file: the default with the replacement, a fresh
  `basedOn`, and its base stored. The register row reads "Customized the Member Playbook".
- **Never the Huddle rules.** They are in no file, so a find that only matches them fails with the existing "The text
  to replace is not in the file exactly once." The `retrospective` Skill gains the line "The Huddle rules part of a
  Playbook cannot be changed; propose changes to the Playbook text only."
- The change reaches the Team's Agents through the Turn hook (§6.4), like any edit.

---

## 10. Process trimming

Swept on 2026-11-18 by grouping every line of the 29 files under `src/Huddle.App/TeamTypes/Builtin/*/processes/`
(26 team types; `development` has four processes). 25 of the 29 files hold the paragraph below.

| Text | Where | Action |
| --- | --- | --- |
| "A draft that the Definition of Done reviews is a file, not text in the Task: …" (the whole paragraph, identical) | 25 files: every `*-basics.md`; none of `development`'s four processes, which word it their own way since ADR-0096 | Delete. Its rule is now in the Member Huddle rules (§4.1) |
| "- A disagreement between agents goes to the Human with both views written on the Task." (identical) | 8 files: `docs-team`, `events`, `household`, `job-search`, `legal`, `life-admin`, `sales-outreach`, `small-business` | Delete. Now in the Member Playbook's **Asking** |
| Escalation bullets that name the type's own roles ("…goes to the proposal lead, then to the Human…") | `content`, `grant-proposal`, `home-lab`, `learning`, `oss-maintainers`, `product`, `development` | Keep: they are the domain's |
| Gate bullets (`qa-evidence-before-done`, `blockers-done-before-start`, `checks-before-merge`, `accept-before-merge`) | The types that declare them | Keep: they describe this type's declared gates |
| "Suggest a starter Task." | All 29 | Keep: Onboarding is Process (ADR-0098) |
| `retro:`, `digest:`, `gates:`, the Definition of Done | All | Keep: the code reads them |

Every heading stays. `ProcessDocument` reports a missing outline heading ("Escalation", "Cadence" and five more), and
each of the eight files keeps at least one Escalation bullet after the deletion. Each edited file's `template:`
version gains a minor step, so a Team hired later records the trimmed process.

The shipped Huddle-wide `AGENTS.md` (`Overlays/second-brain/AGENTS.md`) loses three lines, now in the Member
Playbook: the "@all" line, the "ask with options" line and the "Don't ask for a fact you can look up" line.

The `team-management` Skill stays the tool manual. Under its heading it gains: "How to manage, from triage to
running a mission, is in your Manager Playbook; this Skill covers the tools."

---

## 11. What other documents gain

These pages are not edited by this spec; each change lands with the slice that makes it true (§12).

- **[wireframes.md](engineering/wireframes.md)**, Figure 6: `M4a`'s notes read "Members, Library, Process,
  Playbooks, Details"; `M4b` lists `PlaybooksTab`; new rows `M15` and `M15a` to `M15f` (§7.2), with badges in the
  figure. Overlays: `O15` Reset Playbook confirmation (§7.4). Codes are the next free ones on 2026-11-18 (`M14` and
  `O14` were the highest). The Project page does not change.
- **[language.md](engineering/language.md)**, under People and processes, beside **Process**:
  - **Playbook**: how to work in a Team in any kind of work, read in full by its Agents every session. One per role
    kind, Manager and Member, each a Huddle default until the Human customizes it for a Team. *Avoid*: guide,
    handbook, Skill (a Skill is read on demand).
  - **Manager Playbook**: the Playbook the Team's manager reads as well as the Member Playbook; it only adds to it.
  - **Member Playbook**: the Playbook every member of a Team reads, the manager too.
  - **Huddle rules**: the read-only part of each Playbook, the mechanics Huddle enforces. Shipped with the app,
    never per Team. *Avoid*: system rules, guardrails.
  - **Mission**: a Project's goal, given as a Task the Human assigns the Team's manager and kept by the manager in
    the Project's Team Memory as `mission.md`. *Avoid*: objective, epic.
  - **Skill**'s *Avoid* list loses "playbook".
  - **Process**: its entry gains "Generic guidance on how to work is in the **Playbooks**, not here."
- **[Huddle.UserGuide.md](Huddle.UserGuide.md)**: **Running a Team** gains a Playbooks section (the tab, customizing,
  Default updated, Reset) and a short Missions section (give the goal as a Task assigned to the manager). Its
  **Renaming or deleting Teams and Projects** section, which still says Huddle offers neither, is corrected for
  ADR-0084 and archiving (ADR-0098 Consequences).
- **[Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md)**: the two `Team:Playbooks:` keys (§5.3).
- **[known-limits.md](engineering/known-limits.md)**: the soft edge of §7.6.
- **[code-map.md](engineering/code-map.md)**: a `Playbooks/` row (`PlaybookFile`, `PlaybookStore`,
  `PlaybookBaseStore`, `PlaybookSnapshot`, `HuddleRules`) and `HumanTeamFilesGuard`.
- **ADR-0098** moves to Accepted when slice S1 merges.

---

## 12. Delivery slices

Each slice ships alone, leaves the build green and changes nothing a Human sees until its own UI lands. Test first,
per [agents/Testing.md](../agents/Testing.md): every test below starts red, asserts user-facing text exactly, and a
test that passes on arrival gets a mutation. Every new test has a `///` summary. The test project for each class is
the one `agents/scripts/test-split-map.psd1` gives its folder; goldens live in
`tests/Huddle.Tests.Hosted/Acp/Golden/`.

| # | Slice | Ships | Depends on |
| --- | --- | --- | --- |
| S1 | **Texts and composition** | The two Prompt keys as overlay prompt files (`Overlays/second-brain/prompts/memberPlaybook.md`, `managerPlaybook.md`), the two embedded Huddle rules, `systemPrompt.playbooks`, the cut Prompts, `PlaybookFile`, `PlaybookSnapshot` and its builder, `PromptPartSource.Embedded`, `AddPlaybookParts`, the two config keys. Reads a hand-made Team file already. The mission habit ships here, as Playbook text | none |
| S2 | **Running Agents hear of edits** | `PlaybooksTurnHook`, its three Prompts, the File Changes exclusion | S1 |
| S3 | **The Playbooks tab** | `TeamPageTab.Playbooks`, `PlaybooksTab`, `PlaybookStore`, `PlaybookBaseStore`, Customize, Keep my version, Reset and its confirm, the diff, the Settings group, `HumanTeamFilesGuard` | S1 |
| S4 | **Process trimming** | §10's deletions in the process files and the shipped `AGENTS.md`, the `team-management` line, `language.md`, the user guide's Playbooks and Missions sections | S1 (the rules must ship before the paragraph leaves) |
| S5 | **Retros target Playbooks** | §9 | S3 |
| S6 | **Team-building's first mission** | §8.4 in the `team-building` Skill | S1 |

### 12.1 Tests per slice

**S1**

- `PlaybookTextSizeTests`: each shipped text is at most its ceiling (§4.5); a mutant that appends a paragraph fails.
- `PlaybookComposerTests` (`SystemPromptComposer`): no snapshot gives byte-identical output; a member gets the
  Member rules and default, in that order, after the AGENTS.md block and before Identity; a manager gets Manager parts
  after Member ones; two default Teams give one default headed with both names in `teams` order; a customized Team
  is headed "(this Team's own)"; a Team whose every member Team is customized gets no default; no Playbook for an
  Agent in no catalog Team; each part's label and `PromptPartSource` asserted whole.
- `HuddleRulesRenderTests`: each placeholder filled with the prefixed name; an absent tool renders the unavailable
  text; the rendered text holds no `{{`.
- `PlaybookCutTests`: a body over `MaxChars` is cut at a line break with the file pointer, and with the no-files
  line for an Adapter without file tools; the total cap drops customized Playbooks last-first, never rules or
  defaults.
- `PlaybookFileTests`: parse and compose round-trip; no frontmatter is Unknown base; unknown lines kept; frontmatter
  never in the body.
- Goldens: one member and one manager system prompt (`PromptGoldenTests`), both Adapter prefix forms (rules.md);
  `prompts.default.json` regenerated and `PromptCatalogTests`' count updated (Testing.md "Changing prompts").
- `PromptCatalogTests`: each new key's Timing, placeholders and label, and no `mcp__team__` in its default, as the
  catalog's other per-key tests assert.

**S2**

- `PlaybooksTurnHookTests`: a fresh session hears nothing; a file edit tells the whole new text once, then nothing;
  a Settings save of the default tells every session that reads it; Reset tells the default text; leaving a Team or
  losing the manager role tells `turn.playbookGone`; compaction resets heard to started; an unparseable state is
  logged and treated as unknown; an unchanged map returns no addition.
- `TurnHookPipelineTests`: the production order is TeamRenamed, AgentsMd, Playbooks, HuddleMemory, FileChanges.
- `FileChangesTurnHookTests`: a Playbook file edit is not in the File Changes report.

**S3**

- bUnit `PlaybooksTabTests`: each state of §7.3 renders its chip, caption, buttons and text exactly; Default updated
  shows the notice and toggles the diff; no base file hides **Show changes** and shows the sentence; over the limit
  shows the warning with formatted numbers; unreadable shows the error and the default; Customize is disabled with
  the Library off.
- `PlaybookStoreTests`: Customize writes body and `basedOn` and stores the base; it never overwrites an existing
  file; Keep my version changes only `basedOn`; Reset deletes the file; the state function of §5.2 for each case;
  startup prunes an unnamed base and keeps a named one.
- `TeamPageTabsTests`: Playbooks between Process and Details on a Team page, absent on a Project page, present with
  Process off.
- `HumanTeamFilesGuardTests`: refuses Edit, Write, Delete and Move of both Playbook files at depth 2; allows a read;
  allows the same name in `outputs/` or a reserved folder; refuses a short-name or stream alias; the refusal text
  exactly; `PermissionGuardList` order pinned.
- Hosted: the reset confirm's title, message and verb.

**S4**

- `BuiltinProcessTrimTests`: no built-in process file holds the draft paragraph or the identical disagreement line;
  each still parses with no missing outline heading; each edited `template:` version moved.
- `OverlayAgentsMdTests`: the three lines are gone from the shipped `AGENTS.md`.
- The `team-management` Skill line, asserted in the Skill catalog test that already reads the defaults.

**S5**

- `ProposeRetroChangesToolTests`: both Playbook files accepted; the refusal text exactly.
- `RetroProposalServiceTests`: a customized target applies like any file; an uncustomized target previews against
  the default, writes the file with `basedOn` and base on Accept, and writes the register row text; a find that
  matches only the Huddle rules is "not in the file exactly once".

**S6**

- The Skill text test asserts the new step's sentence. The behaviour is checked by hand (PLAYBOOK-12).

### 12.2 Manual tests

A new page, `docs/engineering/manual-tests/team-playbooks.md`, written with S3 and added to
[docs/Index.md](Index.md) §2 and §5 in the same commit, with cases:

| Id | Case | Slice |
| --- | --- | --- |
| PLAYBOOK-01 | The tab on a fresh Team: both sections, Huddle default, rules read-only | S3 |
| PLAYBOOK-02 | Customize, edit in the Library, save; the chip changes | S3 |
| PLAYBOOK-03 | A member's next Turn in two Rooms tells the new text once per Room | S2 |
| PLAYBOOK-04 | Edit the default on Settings › Prompts; an uncustomized Team's Agent hears it; the customized Team shows Default updated with the diff | S2, S3 |
| PLAYBOOK-05 | Keep my version; Reset to default and its confirm | S3 |
| PLAYBOOK-06 | An Agent in two Teams, one customized: the Prompt blocks page shows the order and labels of §6.2 | S1 |
| PLAYBOOK-07 | An Agent tries to Write `_member-playbook.md`; refused | S3 |
| PLAYBOOK-08 | Move a Task into a Project, assign it to the manager and call it the goal; the manager writes the Project's `mission.md` and plans child Tasks in the Project | S1 |
| PLAYBOOK-09 | A member's Prompt blocks page lists the Project's `mission.md` in Team Memory | S1 |
| PLAYBOOK-10 | A retro proposes a change to an uncustomized Member Playbook; Accept customizes | S5 |
| PLAYBOOK-11 | **Live, paid, Claude Haiku at low effort:** a member on a two-step Task ends each pass with a comment in a named state; a manager given a goal Task writes `mission.md`, creates child Tasks and comments its decision on the mission Task | S1 |
| PLAYBOOK-12 | The Chief of Staff builds a Team and files the goal Task assigned to the manager; the Human moves it into the first Project | S6 |

[prompts-settings.md](engineering/manual-tests/prompts-settings.md) gains one case: the Playbooks group, its helper
texts, and no "next session" badge on either key.

---

## 13. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | A customized file records `basedOn`, a hash of the effective default, in its frontmatter; the old default's text is kept in `{DataDir}/playbook-defaults/` so the diff is real (open item 1) | A date or app version: a Settings edit changes the default without either. Only a hash, with no text: the tab could say the default changed but not how |
| D-2 | The Huddle rules are not versioned per Team and have no Default updated state | Comparing them too: they are never in a Team's file, so a Team cannot fall behind them, and the Turn hook already tells running Agents |
| D-3 | Shipped texts are capped at 2,500 characters per Playbook, 1,800 for the Member rules and 1,500 for the Manager rules, pinned by a test; delivery cuts a body at `MaxChars` (4,000) and all parts at `MaxTotalChars` (16,000) (open item 3) | No limit: a Human's long Playbook would be read by Haiku in every session. Refusing a save: rules.md says validation reports and never refuses |
| D-4 | Huddle refuses an Agent's write to both Playbook files with a path guard, `HumanTeamFilesGuard`, after `AgentsMdGuard` | No guard: an Agent could rewrite the Playbook it is told to follow |
| D-5 | A mission is an ordinary Task plus a Team Memory file the manager writes; Huddle holds no mission data | A `mission:` key in `_project.md` with a service, a reserved tag, a picker and a Project page area: a subsystem for one sentence, needing a guard on `_project.md`, refusals and an Apply for outside edits |
| D-6 | What finished looks like stays prose in the Task; no `Acceptance:` line is parsed | A fixed `Acceptance:` prefix: code would parse a convention today's Tasks do not follow |
| D-7 | The editable texts name no tool; the Huddle rules name them through prefix placeholders | Tool names in the editable text: a Human edit could misspell one, and the model would report the tool missing (rules.md) |
| D-8 | Both Playbook keys are Timing `Live` | `NextSession`: the Settings page would badge them inert on running Agents, which the Turn hook makes false |
| D-9 | A separate `PlaybooksTurnHook` that resends the whole changed text, in the ADR-0076 pipeline after `AgentsMdTurnHook` | Extending `AgentsMdTurnHook`: its ledger, polling and Helper are keyed to files on disk, and the default is a Prompt; a short text is cheaper to resend than to summarize |
| D-10 | Team-building files the goal Task assigned to the new Team's manager, and the Human moves it into the first Project (open item 4) | Waiting for the first Project: the Chief of Staff has no event for a Project being created |
| D-11 | Default text is sent once, labelled with every Team that uses it; customized ones by Team, in `teams` order, Member before Manager (open item 2) | A copy per Team: the same text twice costs tokens, and the model must compare the copies to learn they agree |
| D-12 | The manager keeps the mission in the Project's Team Memory as a summary and the Task id | A copy of the Task's text: two texts drift. A reserved `mission` tag: a mechanism for what the manager already knows from the Human |
| D-13 | Huddle rules told by the Turn hook name tools bare | Plumbing the session's tool list into the hook scope, which is built before the tool set: new plumbing for a rare case (a first Team, or a new manager, mid-session). "Not available" for every tool would be false |

---

## 14. Open questions

| Id | Question | How it is settled |
| --- | --- | --- |
| Q-1 | Does Haiku at low effort follow the loop and end passes in a named state with the shipped texts? | PLAYBOOK-11, before ADR-0098 is marked built |
| Q-2 | Does a manager reliably recognise a goal Task from the Human's words, and write `mission.md`, without a marker? | PLAYBOOK-08 and PLAYBOOK-11. If it does not, the fallback is one Manager Playbook line, not a field |
| Q-3 | Should Settings › Prompts deep-link to a key, so **Edit the default** lands on the field? | Out of scope; the link opens the Prompts section |
