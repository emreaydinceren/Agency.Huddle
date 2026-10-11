
# Specification
How Huddle implements the Requirements tab, built on what the Huddle repo already has.

## Architecture
Teams adds definitions as files, four services that act on them, and a few rules in existing services. Every Team still runs on Huddle's existing pieces: Teammate files, Room Sessions over ACP, Tasks and the Library.

[diagram: Architecture · definitions, services, team state]

- **Definitions are data:** team types, candidates, process templates, packs and the skills catalog are JSON or Markdown files. Code knows managers, roles, packs, gates and Requests, never a role name.

- **Hiring writes ordinary Teammates:** after hire, a member is a normal Teammate file plus `team.db` rows, so everything that works for Teammates today works for hires.

- **Gates live where changes already pass through:** `TaskService` for Task status, `WorkDirPermissionHandler` for shell commands.

- **Wake-ups reuse Task triggers:** the manager and blocked Tasks are added to the existing mechanism; a new `CadenceService` covers time-based digests and retros.

## Data formats
Everything new is a file in the data folder (`App_Data` by default) or a row in `team.db`, following Huddle's existing layout: `Teammates/` and `Teams/` are siblings, a Team folder is its Library, and its direct sub-folders are Projects.

```
App_Data/
  TeamTypes/<type>/            new: user team types and overrides (built-ins embedded, like SkillCatalog defaults)
    team-type.json
    candidates/<role>.md
    processes/<process>.md
  SkillPacks/<pack>.json       new: skill packs, shared across team types
  SkillsCatalog.json           new: generated catalog index (built-in copy plus user copy)
  Skills/<name>/               existing; adds .source.json for imported skills
  Teammates/<Name>/<Name>.md   existing; hire metadata in reserved _ keys
  Teams/<Team>/
    _team.md                   new: team type, manager, workspace, team card
    Process.md                 new: the Team's process (Team Library)
    Retro register.md          new: retrospective findings (Team Library)
    _retros/<date>.md          new: retro reports
    _tasks/                    existing; Requests are ordinary Task files here
  logs/tool-calls/<Name>/<yyyy-mm-dd>.jsonl   new: lasting tool-call log
  team.db                      existing; adds persisted spend
```

### team-type.json

| Field | Type | Meaning |
|---|---|---|
| schemaVersion | int | Manifest format version |
| id, version | string | Type id (folder name) and semver; hires record both |
| displayName, order | string, int | Dropdown position and a fallback label; the hiring pool's `teamTypes` entry gives the name and description |
| workspace | object | kind: repo, folder or none; label; required |
| coverage | string[] | Steps the team check bar requires, e.g. generate, challenge, verify |
| processes | object[] | id, file (under processes/), default |
| roles | object[] | One per role; fields below |

| Role field | Type | Meaning |
|---|---|---|
| id, title | string | Role id and title shown on the card |
| candidate | string | Path to the candidate Markdown file |
| manager | bool | Exactly one role per type sets it |
| required, maxPerTeam | bool, int | Start Team needs it; pool card greys out at the limit |
| covers | string[] | Coverage steps this role provides |
| model | object | tier: strongest, balanced, fast; differentVendorFrom: role ids |
| skills | object[] | name, source (builtin or owner/repo), path |
| packs | string[] | Suggested pack ids |
| requires | string[] | Connectors needed, e.g. email:read, git:push |
| suggestedNames | string[] | Names offered at hire |

### Candidate file
A candidate is a Teammate file without a name. Its frontmatter uses Huddle's existing Persona fields; any non-reserved key becomes a line of the public job description, which is how `consult_when` already works.

```
---
title: 'Reviewer'
consult_when: 'A change is in Review'
---
You assume every change is wrong until it is shown otherwise. ...
```

### Hired Teammate file
Hiring writes an ordinary `Teammates/<Name>/<Name>.md`. Hire metadata goes in reserved `_` keys, which Huddle already keeps out of the job description. Model, Effort and Work mode go to `team.db` as today; the avatar goes to `avatars.json`.

```
---
name: 'Jane'
title: 'Developer'
alias: 'jane'
teams: ['Web Team']
skills: ['test-driven-development', 'incremental-implementation', 'target-authoring', 'property-patterns']
adapter: 'claude-agent-acp'
consult_when: 'A Task is ready to build; .csproj, .props or .targets changes, build failures'
_role: 'development/developer@1.0.0'
_packs: ['msbuild-authoring@1.0.0']
---
<stance copied from the candidate, then any edits>
```

### Skill pack

```
{
  "id": "msbuild-authoring",
  "version": "1.0.0",
  "title": "MSBuild Authoring",
  "appliesTo": ["developer", "release"],
  "consultWhenAdds": ".csproj, .props or .targets changes, build failures, slow or broken incremental builds",
  "skills": [{ "name": "target-authoring", "source": "dotnet/skills", "path": "plugins/dotnet-msbuild/skills/target-authoring" }]
}
```

### Team file (`Teams/<Team>/_team.md`)

```
---
teamType: 'development@1.0.0'
manager: 'Joe'
workspace: 'E:\Repos\WebApp'
owns: '/web, the React front end, the design system'
process: 'Process.md'
---
```
The `owns` line and the manager form the team card that other managers read.

### Skill catalog entry (`SkillsCatalog.json`)
Generated from `SKILLS-CATALOG.md` plus the local clones: commit from `git rev-parse HEAD`, license from the clone's LICENSE file or per-skill license files. "Fixtures & Non-Skills" rows are left out.

```
{
  "id": "addyosmani/agent-skills/code-review-and-quality",
  "name": "code-review-and-quality",
  "description": "<from the skill's frontmatter>",
  "publisher": "Addy Osmani",
  "repo": "https://github.com/addyosmani/agent-skills",
  "path": "skills/code-review-and-quality",
  "link": "https://github.com/addyosmani/agent-skills/tree/<commit>/skills/code-review-and-quality",
  "commit": "<sha>",
  "license": "MIT",
  "category": "Code Review & Quality",
  "subCategory": "Code Review",
  "requires": []
}
```

### Imported skill provenance (`Skills/<name>/.source.json`)

```
{
  "repo": "https://github.com/addyosmani/agent-skills",
  "path": "skills/code-review-and-quality",
  "commit": "<sha>",
  "sha256": "<hash of the downloaded folder>",
  "publisher": "Addy Osmani",
  "license": "MIT",
  "importedAt": "2026-10-08T10:00:00Z"
}
```
Huddle's Skill validation today accepts flat `.md` supporting files only; imported skills with scripts or sub-folders need that rule widened, with scripts flagged at import (SK-6).

### Process document (`Teams/<Team>/Process.md`)

```
---
title: 'Lightweight flow'
template: 'lightweight-flow@1.0.0'
gates: ['qa-evidence-before-done', 'checks-before-merge']
retro: { every: 10, unit: 'done-tasks' }
---
# Lightweight flow
## When it applies
## Stages
## Who does what
## If a role is not hired
## Gates
## Cadence
## Escalation
```

| Gate id | Huddle refuses | Until |
|---|---|---|
| qa-evidence-before-done | Moving a Task to Done | An Evidence line from a member whose role covers verify |
| checks-before-merge | A Release-role merge or push tool call | An Evidence line with exit code 0 for the Team's check command |
| spec-approved-before-build | Moving a Task to In Progress | Its parent spec Task is marked approved by you |
| failing-test-before-fix | Moving a bug Task to Review | An Evidence line showing a failing test before the fix |

### Request (a Task with extra fields)
Tasks already keep unknown frontmatter keys and already have `blocked_by`, so a Request is an ordinary Task file in the receiving Team's `_tasks/` folder.

```
---
id: 'API-0042'
title: 'API returns 500 on empty cart'
status: 'Backlog'
creator: 'Joe'
assignee: 'Priya'
request: 'bug'            # bug | feature | feedback | question
from_team: 'Web Team'
requester: 'Joe'
triage: 'needs-info'      # accepted | declined | needs-info
decline_reason: ''
confirmed_by: ''          # requester-side QA, required to close
---
## Steps to reproduce
## Expected and actual
## Evidence
## Change log
```

### Evidence on a Task
A new `## Evidence` section, append-only like the existing Change log:

```
2026-10-08T17:04Z | Maya | dotnet test | exit 0 | logs/tool-calls/Maya/2026-10-08.jsonl#L212
```

### Tool-call log
One JSON line per tool call in `logs/tool-calls/<Name>/<yyyy-mm-dd>.jsonl`: time, room, Task (if known), tool, arguments summary, exit status, duration. Today tool-call rows live only in memory until the reply ends.

## Runtime behavior

### Hiring (HR-1 to HR-13)

- **New team:** `TeamFolderProvisioner` creates `Teams/<Team>/` and writes `_team.md` with the team type, workspace and an empty team card.

- **Manager hire:** the type's manager role is hired at once (steps 4 to 7).

- **Card state:** for each card in the Team area, Huddle resolves defaults: a name from `suggestedNames` not already used, an adapter and model for the role's tier from the adapters that answered `session/new` (`ModelCatalogProbe`), skills and packs, connectors in `requires`. Anything unresolved makes the card Needs setup.

- **Write the Teammate:** on Start Team (or on Save in Complete Setup for a running Team), Huddle writes `Teammates/<Name>/<Name>.md` from the candidate plus pack skills and `consultWhenAdds`, with `_role` and `_packs`. This goes through `PersonaStore.Add`, like the Chief of Staff's Approve.

- **Store settings:** Model, Effort and Work mode go to `team.db` (`PersonaModelStore`, `PersonaEffortStore`, `PersonaWorkModeStore`); the avatar to `AvatarStore`.

- **Install skills:** any skill not yet in `Skills/` is imported from its owner (Skill import, below) before the member starts.

- **Start:** `PersonaSupervisor` starts the member as today. Start Team starts members in order: manager first.

- **Onboarding:** the manager's first turn is seeded with the onboarding steps from `Process.md`: read the workspace, run the build and tests, write Team Memory notes, propose a starter Task.
**Cap:** the propose and Approve checks against `Acp:MaxTeammates` move to a per-Team limit plus an install-wide limit on live processes (open question in Requirements).
**Chief of Staff:** the `team-building` skill gains a step to pick a team type and propose its candidates; `propose_teammates` accepts a `role` reference so a Candidate can carry skills and model tier through its role, which a Candidate cannot do today.

### Priming (MS-7, PR-4)
`SystemPromptComposer` gains two blocks, both from configurable Prompts (ADR-0007):

- **Role block** for every hired member: name, role, Team, who to consult (from team cards and colleagues' job descriptions), and the skills line with explicit triggers.

- **Process block** for the manager: the full `Process.md`; other members get one line with its Library path. `Process.md` sits in the Team folder, which is already a watched folder, so edits reach the manager on its next turn (ADR-0023).

### Gates (PR-7, PR-8)

- **Task gates** (`qa-evidence-before-done`, `spec-approved-before-build`, `failing-test-before-fix`): `TaskService`, already the only way to change a Task, checks the Team's `Process.md` gates on every status change and refuses with a message naming the gate. The refusal is returned to the calling tool and shown in the Task panel.

- **`checks-before-merge`****:** `WorkDirPermissionHandler`, which already answers every permission request, refuses `git merge` and `git push` commands from the Team's workspace until the gate is met. This only works in work modes where the adapter asks before running shell commands; the UI says so.

- **You can override:** you can move any Task or approve any command yourself; overrides are logged in the Change log as `gate overridden by Human`.

### Manager wake-ups and blocked work (OR-2, RQ-6)

- `TaskTriggerService` adds the Team's manager (from `_team.md`) to the wake-up mention, unless the manager made the change. These count toward the existing 10 Teammate-made wake-ups per Task.

- When a Task reaches Done, `TaskTriggerService` finds Tasks whose `blocked_by` lists it; if all their blockers are Done, it wakes their assignees.

### Planning and sprints (PL-1 to PL-5)

- **Breakdown:** after you approve a spec Task, the Tech Lead creates child Tasks with the existing `create_task` tool, using `parent` for the spec and `blocked_by` for order. Assignment is left empty.

- **Assignment:** the manager is woken by the new Tasks (OR-2), reads colleagues' job descriptions with `list_agents` and their open Tasks with `list_tasks`, and sets one assignee with `update_task`. It respects the work-in-progress limit in `Process.md`.

- **Sprint proposal:** the manager calls a new `propose_sprint(name, taskIds)` tool, granted through the `team-management` skill. Huddle shows a proposal card in the manager's room with you, reusing the pattern of the Chief of Staff's ProposalCard.

- **Approve:** Huddle (not the agent) adds the Sprint line to `sprints.md` and sets `sprint:` on each Task. Decline posts your reason to the manager. ADR-0037's rule that only you create Sprints still holds: creation happens only on your approval.

- **Reassignment:** the manager may change an assignee; `TaskService` already writes the change to the Change log.

### Cadence (OR-3, RT-2)
A small `CadenceService` reads `digest` and `retro` from each Team's `Process.md` frontmatter. On schedule (time-based) or on count (Done Tasks), it posts a message that wakes the manager to write its digest, or the Retro agent to run. Huddle has no scheduler today.

### Request lifecycle (RQ-1 to RQ-9)

| From | Event | To | Who is woken |
|---|---|---|---|
| (none) | Requesting manager calls file_request | Backlog, triage empty | Receiving manager |
| Backlog | Receiving manager sets needs-info | Backlog, needs-info | Requesting manager |
| needs-info | Requesting manager adds the information | Backlog, triage empty | Receiving manager |
| Backlog | Receiving manager accepts | To Do, accepted | Requesting manager |
| Backlog | Receiving manager declines with a reason | Rejected, declined | Requesting manager and Chief of Staff |
| To Do to Review | Receiving Team works it | Usual statuses | Usual wake-ups, plus requesting manager on each change |
| Review | Receiving QA attaches evidence | Done | Requesting manager; blocked Tasks wake |
| Done | Requesting Team's QA confirms | Closed, confirmed_by set | Receiving manager |
**New tools**, granted through a built-in `team-management` skill that every manager holds, using the existing `SkillGrants` mechanism: `list_teams` (team cards), `file_request`, `triage_request`, `confirm_request`. Specialists do not get them, which enforces RQ-2.

### Retrospectives (RT-1 to RT-9)

- **Trigger:** on demand (`@<retro agent> run a retro`) or from `CadenceService`.

- **Gather:** a new `read_records(team, since)` tool returns a compact bundle: Task change logs, Review bounces, Budget pauses, wake-up limits hit, escalations, persisted spend, Requests, and tool-call log summaries. Transcripts are read through it too (a `read_room` tool was deferred in Huddle).

- **Ask:** the Retro agent opens a room with the members involved (you are added as always) and asks about specific moments.

- **Report:** writes `_retros/<date>.md` with the top 3 issues and evidence links.

- **Propose:** up to 3 changes as diffs, shown to you as a proposal card with Accept and Reject, the same pattern as the Chief of Staff's ProposalCard. On Accept, Huddle (not the agent) applies the edit.

- **Register:** each finding becomes a row in `Retro register.md`, with Status Open, Done, Partial, Verified or Changed, following the register already used to build Huddle (`Huddle.TeamPages-RetroActions.md`).

- **Verify:** the next retro checks Done rows against new evidence and marks them Verified, or Partial or Changed.

### Skill import (SK-1 to SK-10)

- Read the catalog entry or GitHub URL; show publisher, repo, link, commit and license.

- Download the folder at the commit (archive download of that path, no git needed).

- Validate frontmatter with the existing Skill rules; compare with the catalog entry.

- Scan for review flags; show them first.

- On confirm, write the folder into `Skills/<name>/` with `.source.json` and its sha256. `SkillStore` already picks up new folders within a second.

## Changes to existing Huddle components
Checked against the Huddle repo docs (User Guide, Engineering Guide, Skills, Tasks, Team Pages and Turn Detail specs, ADRs 0005 to 0037, code map, roadmap). Most of the feature extends what exists; the new pieces are team types, packs, the catalog, cadence and the records tool.

| Component (today) | Change | Requirements |
|---|---|---|
| PersonaStore, PersonaFrontmatter | Accept reserved _role and _packs; keep them out of the job description like other _ keys | MS-6 |
| CandidateChecker, ProposalService (Candidates cannot carry skills, model, effort, adapter) | Allow a role reference that brings skills and model tier from the team type | HR-13 |
| Acp:MaxTeammates (8, counts every Teammate) | Per-Team limit plus a live-process limit | HR-10 |
| TeamFolderProvisioner, TeamCatalog, TeamMembership | Write and read _team.md; record team type, manager, workspace, team card | HR-1, RQ-8 |
| TeamPage.razor (Members, Files, Tasks tabs) | Add the team setup screen (hiring pool) and a Process view | HR-3, PR-2 |
| TeammateCard.razor | Becomes the Complete Setup panel: packs, provenance badges, vendor hint, process step for managers | MS-1, SK-8 |
| SkillCatalog, SkillStore (flat .md files only, 64 KB each) | Accept sub-folders and scripts for imported skills, read .source.json, show provenance and "modified locally" | SK-7, SK-8, SK-9 |
| SkillGrants.Grantable (only validate_teammate, propose_teammates) | Add list_teams, file_request, triage_request, confirm_request, read_records | RQ-2, RT-3 |
| SystemPromptComposer | Role block for every hire; Process block for managers | MS-7, PR-4 |
| PromptCatalog | New configurable Prompts for the role block, process block, onboarding, digest and retro | NF-1 |
| TaskService (only way to change a Task) | Enforce Task gates from Process.md; Evidence section; Request fields | PR-7, RQ-1 |
| TaskTriggerService (wakes the assignee; 10 Teammate-made wake-ups per Task) | Also wake the Team's manager; wake blocked Tasks when blockers reach Done | OR-2, RQ-6 |
| WorkDirPermissionHandler (approves every request; refuses only ~/.claude writes) | Refuse merge and push until checks-before-merge is met | PR-7 |
| Tool-call rows (in memory only, last six) | Also append every call to logs/tool-calls/ | RT-8 |
| PersonaSpend (resets on restart) | Persist per member and per Task in team.db | RT-9, NF-6 |
| Settings (Prompts, Appearance, Personas, Skills, Library) | Add Team Types (list and Problems) and Skill import | TT-4, SK-2 |
| SprintStore (only the Human creates Sprints, ADR-0037) | Create a Sprint when you approve a manager's propose_sprint proposal; add propose_sprint to the team-management skill's grants | PL-4 |

| New component | Purpose | Requirements |
|---|---|---|
| TeamTypeCatalog | Load built-in and user team types, validate, list Problems | TT-1 to TT-6 |
| SkillPackStore | Load packs from SkillPacks/ | MS-4 |
| SkillsCatalogIndex | Read SkillsCatalog.json for the picker and import | SK-1 |
| SkillImporter | Download at a commit, validate, flag, write .source.json | SK-2 to SK-7 |
| HireService | Resolve card defaults, write the Teammate, install skills, start in order | HR-5 to HR-9 |
| ProcessGateService | Read gates from Process.md for TaskService and the permission handler | PR-7 |
| CadenceService | Wake managers for digests and Retro agents for retros | OR-3, RT-2 |
| RecordsTool (read_records) | Compact evidence bundle for retros | RT-3 |
| skills-catalog script | Generate SKILLS-CATALOG.json from the Markdown catalog and clones | SK-1 |
**Docs to update with it:** a new ADR for team types as data, one for gates in `Process.md`, one for Requests as Tasks; the User Guide's Teammates, Teams and Skills chapters; and `language.md` and `code-map.md`, which still describe the old `Acp:TeamsDir` layout.

## Delivery phases and traceability
Six phases, each ending at a gate that can be checked. The first real end-to-end run comes after Phase 3; Household in Phase 5 tests that a new team type needs no role code.

[diagram: Delivery phases · each ends at a gate]

| Phase | Requirements covered |
|---|---|
| 0 Foundations | RT-8, RT-9, HR-10, MS-6, NF-8 |
| 1 Team types and hiring | TT-1 to TT-7, HR-1 to HR-13, MS-1 to MS-7 |
| 2 Skills with provenance | SK-1 to SK-10, NF-1, NF-4 |
| 3 Process and gates | PR-1 to PR-8, OR-1 to OR-5, PL-1 to PL-5 |
| 4 Cross-team and retros | RQ-1 to RQ-10, RT-1 to RT-7 |
| 5 Household | TT-8, HR-7 (connectors), NF-11 |
| v2 | TT-9, PL-6, HR-14, MS-8, SK-11, PR-9, RQ-10 (enforced), RQ-11, RT-10; per-Teammate tool grants; ask before a tool runs |
