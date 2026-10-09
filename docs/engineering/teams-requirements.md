
# Huddle Teams: Requirements and Specification
 · 
Requirements for ready-to-hire agent Teams in Huddle: team types, hiring, member setup, skills with clear provenance, a manager per Team, process documents, cross-team Requests and retrospectives. The technical specification is in the Specification tab.

## Problem and goals
Today a Huddle user builds a team by writing Teammate files or approving a team the Chief of Staff invents from scratch. That team starts as one model in several roles, with no built-in verify step and no shared process. Teams makes a good team the default: hire tested specialists in minutes, and the team runs its own work while you stay in every room.
**Problems**

- **Setup is manual.** Roles, stances and skills are written by hand or invented each time.

- **Teams converge.** Every proposed Teammate starts on the default model, so reviewers share the author's blind spots.

- **No Verify step.** Review is a status, not a check against ground truth.

- **No process.** Nothing tells the team how work moves from idea to done.

- **Skills have no provenance.** Skills are copied in by hand, with no record of publisher, version or license.

- **Teams work alone.** There is no structured way for one Team to ask another for work.

- **Mistakes repeat.** Nothing turns a team's failures into process changes.
**Goals**

- A user sets up a working Development Team in about two minutes without editing files.

- Every Team covers Generate, Challenge and Verify, with at least two model vendors when available.

- Roles, team types, skills and processes are data, not code; a new team type needs no code change.

- Every skill shows publisher, repo, commit and license, and is downloaded from its owner.

- One manager per Team runs the work by judgment from a process document; a few hard gates are enforced in code.

- You stay in every room and own goals, specs and final acceptance.
**Design principles carried from the brainstorm**

- Roles are people; stages are verbs.

- Separate context per member; different models where it matters; opposed stances.

- Routing is flexible, checking is strict.

- Tasks are the backbone, not chat mentions.

## Users and scenarios
The primary user is one person running Huddle on their own machine: a developer who wants a team of agents on a repo, and the same person at home wanting help with household work.

| Actor | Who | Owns |
|---|---|---|
| You (the Human) | The person running Huddle | Goals, specs, acceptance, approvals; a member of every room |
| Chief of Staff | The one built-in Teammate | Building Teams; rolling up manager digests for you |
| Manager | One per Team (Development Manager, Household Manager) | Running the Team's work from its process document |
| Specialist | Every other hired member | Its role's job, from its stance and skills |
| Retro agent | Optional hire | Reviewing records and proposing process changes |
**Scenario 1: hire a Development Team (v1)**
Emre picks Development Team, names it Web Team, points it at his repo and chooses the Small preset. Joe (Development Manager) is hired automatically; he adds a Developer, Reviewer and QA from the pool. Every card is Ready with defaults, the Reviewer and QA land on a different vendor than the Developer, and he clicks Start Team. Joe reads the repo, runs the build and tests, writes the stack notes to Team Memory, and proposes a small starter Task.
**Scenario 2: specialize two Developers (v1)**
The Web Team needs both front-end and build work. Emre hires a second Developer, Jane, and adds the MSBuild Authoring pack. Her job description now says to consult her on .csproj and build failures, so Joe routes build Tasks to her without anyone tagging her.
**Scenario 3: one Team asks another (v1)**
The Web Developer hits a 500 from the API. Joe files a Bug report Request into the API Team's backlog with steps and logs. The API Team's manager accepts it, the API Team fixes and verifies it, and the Web Team's QA confirms the fix before the Request closes. The Web Task that was blocked by it wakes up.
**Scenario 4: a retrospective after a sprint (v1)**
At the end of a sprint the Retro agent reads the Task change logs and transcripts, finds that three Tasks bounced from Review for missing tests, asks the Developer about one of them, and proposes one change to `Process.md`: write the failing test before the fix. Emre accepts it.
**Scenario 5: hire a Household Team (v1 second type)**
Emre picks Household and hires a School Liaison. The card shows Needs setup until he connects email. The liaison then turns school emails into parent to-dos, events and a weekly digest, and drafts replies for a parent to send.

## Scope
v1 ships the Development Team end to end and Household as the second team type, proving the format needs no code change. Everything else waits for v2 or stays out.

| Area | v1 | v2 | Out of scope |
|---|---|---|---|
| Team types | Development Team; Household with Household Manager and School Liaison | Small Business; remaining Household roles | A pipeline engine |
| Hiring | Setup screen, hiring pool, presets, Ready / Needs setup, Start Team | Specialized postings in the pool; lending a specialist to another Team |  |
| Members | Model, Adapter, Effort, skills and packs carried by the hire; Complete Setup panel | Product Manager and Design Reviewer roles; "update available" for role templates |  |
| Skills | JSON catalog, import from the owner pinned to a commit, .source.json, visible provenance | "Update available" with a diff; whole-repo pick list | Pulling skills live on every run; a marketplace |
| Orchestration | One manager per Team; manager wake-ups; Chief of Staff digests |  |  |
| Process | Four templates, Process step, Process.md in the Team Library, hard gates enforced | More Household process templates |  |
| Cross-team | Requests as Tasks, triage, blocked work wakes, team card | Shared contract space |  |
| Retrospectives | Retro agent, on demand and on process triggers; lasting tool-call log | Cross-team pattern rollup |  |
| Safety | Hard gates; managers cannot be removed | Per-Teammate tool grants (roadmap item 9); ask before a tool runs (roadmap §20) | Sandboxing; authentication |
Per-Teammate tool grants are v2, so in v1 "Reviewer and Security are read-only" is an instruction in their stance, not an enforced limit. The doc says so wherever it matters.

## Functional requirements
Each requirement has an ID for traceability and a release. "Must" is implied for v1 rows.

### Team types (TT)

| ID | Requirement | Release |
|---|---|---|
| TT-1 | A team type is a folder with a team-type.json manifest and one Markdown file per candidate role. Huddle's code holds no role names. | v1 |
| TT-2 | The team-type dropdown lists available types, ordered by the manifest's order. | v1 |
| TT-3 | Built-in types ship with Huddle; a user folder with the same id replaces or extends one. | v1 |
| TT-4 | An invalid manifest is not offered and is listed under Settings › Team Types › Problems with the reason. | v1 |
| TT-5 | Each type declares exactly one manager role. | v1 |
| TT-6 | A manifest declares workspace kind, coverage, presets, processes, and per role: limits, required, model tier, vendor diversity, skills, packs, required connectors and suggested names. | v1 |
| TT-7 | The Development Team type ships Development Manager, Tech Lead, Developer, Reviewer, Security Engineer, QA Engineer and Release Engineer. | v1 |
| TT-8 | The Household type ships Household Manager and School Liaison. | v1 |
| TT-9 | Small Business type; remaining Household roles. | v2 |

### Hiring and team setup (HR)

| ID | Requirement | Release |
|---|---|---|
| HR-1 | Creating a Team asks for team type, Team Title, and a workspace when the type needs one. | v1 |
| HR-2 | The manager is hired automatically when a Team is created. It can be replaced but not removed. | v1 |
| HR-3 | The hiring pool shows one card per role. Drag a card into the Team or click + to hire; a preset hires a set of roles in one step. | v1 |
| HR-4 | A pool card greys out when its role reaches maxPerTeam. | v1 |
| HR-5 | Every hire is a new member of exactly one Team, except you and the Chief of Staff. The same role can be hired into many Teams under different names. | v1 |
| HR-6 | A hire arrives Ready: suggested name, generated avatar, default skills, stance, and a model chosen from its tier. | v1 |
| HR-7 | A card shows Needs setup only when no default exists: a name conflict, no logged-in adapter for its tier, or a connector in requires that is not connected. | v1 |
| HR-8 | A team check bar shows coverage (Generate, Challenge, Verify), vendor count and process count. | v1 |
| HR-9 | Start Team is enabled when every card is Ready and every required role is hired. | v1 |
| HR-10 | The Teammate cap fits Teams. Today Acp:MaxTeammates (8) counts every Teammate in the install, which blocks a second Team. | v1 |
| HR-11 | Custom Role starts blank or from an existing role and is saved as a user candidate for next time. | v1 |
| HR-12 | Duplicate team: same roster with new names, a new workspace and fresh memory. | v1 |
| HR-13 | The Chief of Staff proposes Teams from a team type's candidates instead of inventing roles. | v1 |
| HR-14 | Specialized postings in the pool ("Developer (Web)"); lending a specialist to another Team. | v2 |

### Member setup and priming (MS)

| ID | Requirement | Release |
|---|---|---|
| MS-1 | The Complete Setup panel has, in order: identity (name, title, alias, avatar), model (adapter, model, effort), skills (packs, then individual skills), persona (public job description and private stance), work mode. | v1 |
| MS-2 | Members are mentioned by name; the role is their title. | v1 |
| MS-3 | The panel warns when a member shares a vendor with a role in its differentVendorFrom. | v1 |
| MS-4 | A skill pack adds its skills and extends the member's public job description with its consultWhenAdds. | v1 |
| MS-5 | The panel warns above 10 skills, and when two skills share a catalog Sub Category. | v1 |
| MS-6 | A hire records its role template id and version and each pack's id and version. | v1 |
| MS-7 | A member is primed with its name, role, Team, stance, who to consult, and its skills with descriptions and explicit triggers, read on demand with read_skill. | v1 |
| MS-8 | "Update available" for role templates and packs. | v2 |

### Skills catalog, import and provenance (SK)

| ID | Requirement | Release |
|---|---|---|
| SK-1 | SKILLS-CATALOG.json is generated from SKILLS-CATALOG.md and the local clones; it is never edited by hand and holds facts and links, never skill text. | v1 |
| SK-2 | Import from a catalog entry or a GitHub URL of one skill folder. | v1 |
| SK-3 | Before downloading, import shows publisher, repo, link, commit and license. | v1 |
| SK-4 | Import downloads the whole skill folder from the owner's repo at the pinned commit. | v1 |
| SK-5 | Import checks the downloaded frontmatter against the catalog and shows any difference. | v1 |
| SK-6 | Import flags scripts, shell commands, network calls and paths outside the Work Dir at the top of the review screen. | v1 |
| SK-7 | Import writes Skills/<name>/.source.json with repo, path, commit, sha256, publisher, license and import time. | v1 |
| SK-8 | Every skill shows publisher, repo link, commit and license in the picker, on member cards and on its page; a missing license is stated plainly. | v1 |
| SK-9 | A locally edited skill shows "modified locally". | v1 |
| SK-10 | Skills are never fetched at run time. | v1 |
| SK-11 | "Update available" with a diff; whole-repo pick list; import from a local folder, .zip or pasted SKILL.md. | v2 |

### Orchestration (OR)

| ID | Requirement | Release |
|---|---|---|
| OR-1 | Every Team has exactly one manager; the Chief of Staff talks to managers, not to specialists. | v1 |
| OR-2 | A change to a Task in a Team wakes that Team's manager as well as the assignee. | v1 |
| OR-3 | A manager posts status digests that link to Tasks and evidence; the Chief of Staff rolls them up for you as decisions and risks. | v1 |
| OR-4 | A manager's stance says it never marks work Done itself; the Done gate (PR-7) enforces the evidence part. | v1 |
| OR-5 | You can talk to any member directly; the hierarchy never blocks access. | v1 |

### Planning and assignment (PL)
The Tech Lead breaks the work down (what and how); the manager plans sprints and assigns (who and when); you approve the spec and the sprint scope.

| ID | Requirement | Release |
|---|---|---|
| PL-1 | The Tech Lead breaks an approved spec into Tasks of a day or less, each tied to acceptance criteria, ordered with blocked_by links. | v1 |
| PL-2 | When no Tech Lead is hired, the manager writes the spec and does the breakdown, as the process fallback says. | v1 |
| PL-3 | The manager assigns each Task to one member, using colleagues' public job descriptions (including what their packs add) and their current load against the work-in-progress limit in Process.md. | v1 |
| PL-4 | The manager proposes a sprint (name and Tasks) with a propose_sprint tool. You see it as a proposal card; on Approve, Huddle creates the Sprint and sets sprint: on those Tasks. Sprints are still created only with your approval. | v1 |
| PL-5 | The manager reassigns a Task when its owner is overloaded or stuck; the reassignment is recorded in the Task's Change log. | v1 |
| PL-6 | When a Product Manager is hired, it writes the problem and success metric before the Tech Lead's technical spec. | v2 |

### Process (PR)

| ID | Requirement | Release |
|---|---|---|
| PR-1 | A team type ships process templates. The Development Team ships Lightweight flow (default), Spec-driven, Sprints and Maintenance. | v1 |
| PR-2 | The manager's Complete Setup panel has a Process step: pick a template, preview, edit, or start blank. | v1 |
| PR-3 | The chosen process is copied into the Team Library as Process.md, recording its template id and version. | v1 |
| PR-4 | The manager is primed with Process.md; other members are given a link to it. | v1 |
| PR-5 | An edit to Process.md reaches the manager on its next turn. | v1 |
| PR-6 | Every process document follows one outline: when it applies, stages with ready and done criteria, who does what by role, missing-role fallbacks, gates, cadence, escalation. | v1 |
| PR-7 | Hard gates declared in the frontmatter are enforced by Huddle: qa-evidence-before-done, checks-before-merge, spec-approved-before-build, failing-test-before-fix. | v1 |
| PR-8 | Turning a hard gate off needs an explicit confirmation that states the risk. | v1 |
| PR-9 | More Household process templates. | v2 |

### Cross-team Requests (RQ)

| ID | Requirement | Release |
|---|---|---|
| RQ-1 | A Request is a Task filed into another Team's backlog, with a kind (Bug report, Feature request, Feedback, Question) and that kind's required fields. | v1 |
| RQ-2 | Only managers file and triage Requests; a specialist asks its own manager. | v1 |
| RQ-3 | The receiving manager triages: Accept, Decline with a reason, or Needs info. | v1 |
| RQ-4 | Every status change of a Request wakes the requesting manager. | v1 |
| RQ-5 | A Request closes only after the requesting Team's QA confirms the result. | v1 |
| RQ-6 | A Task with blocked_by wakes its assignee when every blocker reaches Done. | v1 |
| RQ-7 | A decline or a priority clash escalates to the Chief of Staff, then to you. | v1 |
| RQ-8 | Each Team has a team card listing what it owns and its manager; managers can read every team card. | v1 |
| RQ-9 | The Chief of Staff's rollup shows open cross-team Requests per Team. | v1 |
| RQ-10 | No Team writes another Team's workspace. v1 states it in every stance; v2 enforces it with tool grants. | v1 / v2 |
| RQ-11 | A shared contract space both Teams can read. | v2 |

### Retrospectives (RT)

| ID | Requirement | Release |
|---|---|---|
| RT-1 | A Retro agent can be hired; it runs on a different model than the manager it reviews, with a fresh context. | v1 |
| RT-2 | A retro runs on demand and on the cadence in Process.md. | v1 |
| RT-3 | The Retro agent reads evidence first: Task change logs, transcripts, Budget pauses, wake-up limits hit, escalations, spend, Requests and the tool-call log. | v1 |
| RT-4 | It then asks the members involved targeted questions about specific moments. | v1 |
| RT-5 | It produces a report with the top 3 issues linked to evidence, and at most 3 proposed changes as diffs to Process.md, a stance or a skill assignment. Removing a rule counts. | v1 |
| RT-6 | Findings go into a register in the Team Library with ID, Finding, Action, Owner, Status (Open, Done, Partial, Verified, Changed) and Evidence / next check. | v1 |
| RT-7 | Each accepted change is checked at the next retro, and becomes Verified only when later evidence shows it worked. | v1 |
| RT-8 | Tool calls are kept in a lasting log; today they live only in memory until the reply ends. | v1 |
| RT-9 | Spend is persisted per member and per Task; today it resets on restart. | v1 |
| RT-10 | The Chief of Staff rolls up patterns that repeat across Teams. | v2 |

## Screens (conceptual mockups)
Five conceptual wireframes for the main new screens. They show layout and content, not visual design; names and values are illustrative.

### Screen 1: Team setup and hiring pool (TT-2, HR-1 to HR-9)

[diagram: Screen 1 · Team setup and hiring pool]
The manager is already hired and can only be replaced. Cards greyed in the pool have reached their limit. Ravi needs setup because no logged-in adapter matches his tier, so Start Team waits; the check bar shows coverage, vendors and processes.

### Screen 2: Complete Setup panel (MS-1 to MS-7, SK-8)

[diagram: Screen 2 · Complete Setup panel]
The MSBuild pack adds its skills and extends Jane's public job description, which is how her manager knows to route build work to her. Every skill shows publisher, repo and commit. For a manager, this panel adds a Process step (pick a template, preview, edit). Saving restarts the member, as any Teammate change does today.

### Screen 3: Skill import review (SK-2 to SK-8)

[diagram: Screen 3 · Skill import review (illustrative data)]
Flags sit at the top because most people will not read the whole skill. Provenance is shown before anything downloads, and the install is pinned to the commit shown. The skill and its values are illustrative.

### Screen 4: A cross-team Request in triage (RQ-1 to RQ-9)

[diagram: Screen 4 · A cross-team Request in triage]
The Request is an ordinary Task in the API Team's backlog with a kind, the requesting Team and three triage actions. The side panel shows what it blocks, who must confirm it, and who wakes on each change.

### Screen 5: Retrospective report (RT-1 to RT-7)

[diagram: Screen 5 · Retrospective report and proposals]
Evidence comes first, each issue linked to its source. Proposals are diffs you accept or reject; Huddle applies an accepted change, not the agent. The register keeps findings open until a later retro shows the change worked, following the register format Huddle's own development already uses.

## Non-functional requirements

| ID | Area | Requirement |
|---|---|---|
| NF-1 | Transparency | Every skill, role template, pack and process shows where it came from and which version is in use. No hidden defaults. |
| NF-2 | Files you can open | Team types, candidates, packs, processes, retro registers and .source.json are plain JSON or Markdown in the data folder, readable and editable outside Huddle, consistent with Huddle's file-first design. |
| NF-3 | Human in every room | Nothing in this feature creates a room without you, or a path that hides work from you. |
| NF-4 | Safety of imported skills | An imported skill is treated as installed code: reviewed before install, pinned, never changed without your consent. |
| NF-5 | Honest limits | Where v1 relies on a stance instead of enforcement (read-only reviewers, no writes to another Team's workspace), the UI says so. |
| NF-6 | Cost visibility | Each member card and each Team shows spend; the team check bar shows how many processes a Team runs. Managers' digests link instead of paraphrasing, to keep context small. |
| NF-7 | Setup time | A Small Development Team can be hired and started in under 2 minutes with no file edits, given one logged-in adapter. |
| NF-8 | Restart safety | A restart loses no hire, process, Request or retro finding; all live in files or team.db. |
| NF-9 | Model independence | Manifests name model tiers, not model names, so they keep working when models change or only a local model is available. |
| NF-10 | Local-first | Huddle keeps running on your own machine with no account or server; network is needed only to import or update a skill. |
| NF-11 | Domain neutrality | Code knows managers, roles, packs, gates and Requests, never "Development Manager" or any other role name. |

## Success metrics
The software-factory pillars, measured on one real feature run end to end with a hired Team, then tracked per sprint. Targets are proposals to confirm after the first run.

| Pillar | Metric | How it is measured | Proposed target |
|---|---|---|---|
| Setup | Time to a started Small Development Team | From "New team" to Start Team | Under 2 minutes |
| Autonomy | Human interventions per Done Task | Your messages and approvals in that Task's rooms | Falls sprint over sprint |
| Automation | Share of stages that ran without you | Task change log: stage changes made by members | Rises sprint over sprint |
| Quality | Defects caught before Done | Review bounces and QA failures, versus regressions found after Done | Most defects caught before Done |
| Quality | Verify coverage | Done Tasks with QA evidence attached | 100% where the gate is on |
| Learning | Retro actions that work | Accepted retro changes marked Verified at a later retro | Most accepted changes Verified |
| Cost | Spend per Done Task | Persisted spend, per member and Task | Tracked, compared across processes |

## Open questions

- **Teammate cap:** raise `Acp:MaxTeammates`, make it per Team, or replace it with a limit on live processes? Every Teammate is a process.

- **Workspace for dev Teams:** a Teammate's working folder is `Teammates/<Name>/work/`. Does each Developer get a git worktree of the Team's repo there, and do Reviewer and QA read the repo through a watched folder?

- **Second vendor:** which adapter is the default second vendor for Reviewer, Security and QA: another cloud adapter, or a local model through `agency-acp`? `agency-acp` has no file tools today, which limits what a reviewer can check.

- **Done acceptance:** should "you accept" be a hard gate (only you can move a Task to Done), or stay a process rule?

- **Retro agent placement:** a hire in each Team, or one retro role under the Chief of Staff serving every Team?

- **Skill licenses:** how to treat repos with no license (Anthropic per-skill licenses, Vercel Labs, amaancoderx): allow import with a warning, or block?

- **Team card storage:** in a new `Teams/<Team>/_team.md` (an idea already noted in the Team Pages spec), or in `team.db`?

- **Sprints:** Huddle's Sprints are only an id and a name with no dates. Does the Sprints process need start and end dates to trigger its retro?
