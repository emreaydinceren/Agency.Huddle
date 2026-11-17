# Known limits

What is absent on purpose. Read it before "fixing" something that looks missing
and before filing a bug: most entries below are decisions, five are known flakes,
and one is a known bug left in place deliberately. Do not treat any of them as
oversights or quietly add them.

Two entries have since been planned and one has since been built; each one says
so, and names its [Roadmap](roadmap.md) item or its ADR. Back to the hub: [Huddle.EngineeringGuide.md](../Huddle.EngineeringGuide.md).

- **The merge gate waits for the Human's Accept by Team, with a one-hour gap.** In every Team, whatever its `gates:` line says ([ADR-0078](../adr/0078-a-merge-into-main-waits-for-the-humans-accept-by-team.md), [ADR-0088](../adr/0088-a-needs-you-level-per-scope-lets-a-teams-local-work-run-without-an-accept.md)), a merge into main by any member is refused until a Task of the Team has a valid Accept (a local merge of a branch named after a released Task, under Run inside the Team, excepted). A command names no Task, so for **one hour** after an Accept that took a Task with no Action straight to Done, another merge by the Team is allowed too. A valid Accept on an open Task likewise carries a merge for **24 hours**, then the merge needs a fresh Accept; inside that time another branch's merge by the Team is allowed too, and an Accept in one Team also opens the merge for another Team the same Agent belongs to. A Team that wants no gap on the one-hour window gives every merging Task an Action. The gate guards against an Agent's ordinary mistake and is **not a security boundary**: it does not see a rebase onto main, `gh pr merge`, `git fetch . feat:main`, `git branch -f main`, a direct commit made on main, a script that merges, an alias or `$(…)`. It does read `NAME=value git merge`, `bash -c "git merge …"`, `pwsh -Command`, redirections, `git merge -`, `git push origin HEAD`, `+main` and `git pull origin <branch>`. `ProcessMergeGate` writes a refusal that says what to do, but the permission reply carries no reason, so that text only reaches the log: a refused Agent sees a plain denial and learns the rule from the process text in `AGENTS.md` and the Task tools. The same limits are shown to the Human as the "What this cannot catch" caption under the Needs You level control (`NeedsYouLevelCopy.LimitsNote`), and for a guarantee the Human protects the branch on their git server; issue #608 is the parked design for real enforcement.
- **A few edges stay open under the Needs You level ([ADR-0088](../adr/0088-a-needs-you-level-per-scope-lets-a-teams-local-work-run-without-an-accept.md)).** (1) The gate reads the command line, so a script that pushes or merges, `$(…)` and a git alias that already sits in a git config file (not set on the line, as `-c alias.x=push` is) get past it. The level and its cap are in `team.db`, so a script cannot change them, but it can run a push the gate never sees. (2) A Team deleted and created again with the same name keeps its old id, and so its old level and cap. (3) A Project's cap counts the spend recorded against its Rooms (#557), so a Turn in an Agent's shared session (no Room) counts toward no Project, a Team's cap counts its members' ledger, and the global cap counts every Agent. Spend in other currencies is not counted. (4) The cap stops nothing in flight: at the cap the scope falls back to Ask me and Agents stop at the next step that waits for the Human. The Room Budget stays the runaway-loop guard. (5) A Task's level is fixed where Huddle created it, so an Agent can still create a Task directly in a scope that runs inside the Team: the Team root, or a project folder it made or renamed itself, which then inherits the Team's level. (6) One Accept on a Task whose Action names a push lets any push by a member of that Team through for 24 hours (one hour after the Accept took the Task to Done), whatever the push sends, because a command names no Task. Items (1), (5) and (6) are also the "What this cannot catch" caption under the level control (`NeedsYouLevelCopy.LimitsNote`); issue #608 is the parked design for real enforcement.
- **An Agent's merge gate and file guards hold against a local allow rule, an allow hook, `disableAllHooks` and a rewritten settings file, but not against a script or a shell write (issue #432).** Every Agent session sends `options.settings.permissions.ask` (`Bash(*git*merge*)`, push, pull, `Edit`, `Write`; see rules.md), which is not on disk and beats those routes; the file hook stays as a second layer, and `AgentSettingsGuard` refuses an Agent's `Edit`/`Write` of `.claude/settings.local.json`, `merge-gate-ask.js` and Huddle's hooks folder. Still open: **(1)** a script (`bash merge.sh`), an alias, `$(...)` or an interpreter that calls git merges with no request; **(2)** a Bash or PowerShell write to the settings file, the hook script, `AGENTS.md`, a Raw original or `~/.claude` reaches no path guard (`printf … > .claude/settings.local.json` was approved); **(3)** a Claude profile that does not isolate gets only `options.env`: `settings` would replace the Adapter's computed settings and is known to drop a `CLAUDE_MODEL_CONFIG` override, so there the hook is the only layer; **(4)** `auto` and `acceptEdits` mode, the `PowerShell(...)` rule forms, `MultiEdit` and `NotebookEdit` are unmeasured (the Adapter reports `NotebookEdit` as kind other, so no guard sees it); **(5)** the Codex Adapter has neither the `ask` rule nor the hook. `.mcp.json`, `.claude/settings.json` and `.claude/hooks/*` in a Work Dir are not guarded on purpose: a development Team's Work Dir is its repository, and its Agent may edit them.
- **The merge gate runs only on a permission request.**
  **Where it is enforced.** Huddle decides only when the Adapter sends a permission request. In the Adapter's `auto` mode (which a user's own `~/.claude/settings.json` `defaultMode` set for every session until issue #414) the Adapter approves a shell command itself and sends none, so until issue #396 the gate never ran there: an Agent merged a gated Task's branch with no refusal. Huddle now writes a `PreToolUse` hook for the shell tools into each Agent's `.claude/settings.local.json` (`merge-gate-ask.js`, installed at every session start, other settings and hooks kept). For a command line naming `git` and then `merge` or `push` it answers `ask`, which makes the Adapter send the request, and `WorkDirPermissionHandler` decides as before; every other command gets no output and no request. Measured on the real Adapter in `auto` (Haiku, low Effort): a refused merge produced a request and was refused, an allowed merge produced a request and ran, `git status` produced no request, and the control without the hook merged with no request. Since issue #414 ([ADR-0079](../adr/0079-huddle-chooses-the-mode-of-the-sessions-it-starts.md)) an isolating Adapter's session no longer inherits `auto`; the hook stays for an Adapter entry that does not isolate, for a Human's `permissions.allow` rule, and for a Work Mode that does not ask. The hook is Claude Code's. **Codex has no such hook**, so under a Codex Adapter the gate still covers only what Codex sends as a permission request, and a Codex Agent in a mode that does not ask is not gated. The hook asks for a merge or push even when the gate would allow it (it over-asks and the handler decides); a script that merges (`bash deploy.sh`) is still not seen.
  `ProcessMergeGate` sees only a command run in a Persona's shell. It does not see a command run outside one, or a merge made while already on main by another route (a rebase, a fast-forward from a tool). Merging main into a feature branch stays allowed. See OD-1 in the [HumanGate spec](../Huddle.HumanGate-Specifications.md).
- **The blockers gate stops a status move, not early work, and an outside edit may remove a blocker ([ADR-0085](../adr/0085-a-blocked-task-waits-for-its-blockers.md)).** `blockers-done-before-start` refuses a Task moving to In Progress, Review, Needs You or Done while a blocker is open, but an Agent can still start the work (the 2026-11-14 run: the Developer committed before any status move); the process text and `developer.md` ask it not to, and a hired Agent keeps its own Persona copy. Only the manager or the Human may remove a blocker through a tool, but an edit to the Task file on disk that removes one is not restricted. It is still judged against the blockers before the edit.
- **A Task's timeline is immutable in Huddle, not on disk ([ADR-0086](../adr/0086-a-task-is-a-description-plus-an-immutable-timeline.md)).** No tool, control or `TaskService` method edits or deletes a comment, an Evidence entry or an event. A Task file is still a Markdown file: an editor can change or remove a line under `## Timeline`, and Huddle reads the file as written and logs `edited outside Huddle` only for the fields `TaskDiff` compares. An entry added in an editor counts as written (so a hand-added Evidence line feeds Proof, as a hand-added `## Evidence` line always did).
- **A refusal by one of Huddle's own tools is logged as `completed`, with the refusal in `result`.** The tools answer a refusal as ordinary text (MCP `isError: false`), so the Adapter reports the call completed; a distinct `refused` status would need each tool to mark its refusals. The tool-call log records a tool's result only for `mcp__team__` names, so an Adapter that sends no such prefix records none.
- **Huddle does not always move a ready gated Task to Needs You.**
  It moves a Task in Review when a write to the Task completes the Proof, and when a Proof input outside the Task file does (#593): `ProofInputWatcher` re-evaluates the Tasks in Review that attach a document whose newest History entry is `Reviewed`, and the parent of a child that reaches Done. Role coverage (a Persona's role), the Team's roster, its gates and its Definition of Done are not watched, so a Proof item they complete leaves the Task in Review until the next Task write or until the assignee moves it; the Agent prompts (`tool.updateTask.description`, `getHelp.tasks`) say so, and the card says Accept unlocks when the Task moves. A description edit that attaches an already reviewed document is a Task write but not one that re-evaluates Proof (only status, Human gate, Action and Evidence writes do).
- **A Team hired with Adapters off is not onboarded, and nothing tries again.**
  `HireService.OnboardManagerAsync` waits a minute for the manager to run, then logs a warning and gives up; `onboarded` stays unset, but no later start posts the steps. The Team keeps its process, so the Human can ask the manager to work through its `## Onboarding`.
- **Huddle does not advertise `session.compaction`, so an automatic compaction stays invisible.**
  Advertising it removes the "Compact conversation" tool call whose `rawOutput` gives `/compact` its token figures, and the replacement `compaction_update` carries none outside JetBrains AIR, as a paid spike showed on 2026-11-03. See [Adapter Upgrades §8](../Huddle.AdapterUpgrades-Specifications.md), which names the follow-up on the legacy `trigger` field.
- **An idle Agent does not hear of an `AGENTS.md` change until its next Turn.** Huddle adds the notice to a Turn and starts
  none of its own, so an Agent that is not asked anything keeps its old rules in mind. It hears at its next Turn in each Room
  ([ADR-0076](../adr/0076-a-running-agent-hears-of-an-agents-md-change-through-a-turn-hook.md)).
- **The Agent keeps `reported.md`, so a report it forgot to note can repeat ([ADR-0094](../adr/0094-an-agent-keeps-a-record-of-what-it-told-the-human.md)).** The Memory Prompt asks for one line after each report, and nothing checks that the Agent adds it. An item it did not note reads as new at its next report. Huddle writes the file from no Message.
- **Two Rooms of one Agent can report the same thing at once.** Each Room reads `reported.md` before it reports, and neither sees the line the other is about to add, so both say it in full and the file gets two lines. The next report sees both.
- **The Room in a `reported.md` line is the Agent's word.** The tool checks that a line has one line of text, not that the Room named is real or is the Room the Agent spoke in. A wrong Room name misleads only the reader of the file.
- **An Agent often takes "check X and report it" as a direct answer, and keeps no line (seen 2026-11-17).** The Memory Prompt says a direct answer to a question just asked is not a report (spec D-6). On Sonnet, a request worded as a question, also a Job's instruction such as "Tell me the time zone", was answered with no read of `reported.md` and no line (REPORTED-01, 02 and 07 failed as written); the same Agent kept the file when asked for "a status report". A Job's status request worded as one ("give me the launch budget status") did read and append. The prompt's line between a report and an answer is the open question; the wording is the Human's to tune in the Prompts panel.
- **Memory Tidy refuses most merges of small, overlapping files (seen 2026-11-17).** A-3 wants a merged body no longer than its sources together and no shorter than the largest. For two one-line files on one topic the Helper's merge was longer than both (it adds a heading or a joining sentence) or, for heavily overlapping files, shorter than the larger one. In the first paid week 8 of 9 refusals were A-3 and none were A-4 to A-7, so the token and description rules refused no fair step; the size rule did.
- **Memory Tidy has no one-click restore.** To undo a change the Human opens the copy in the Memory backups root and puts its text back by hand. A Restore action per line of the list is slice 6 of [the spec](../Huddle.MemoryTidy-Specifications.md), optional.
- **A crash between the Feed item and the journal's last line posts the item twice.** A pass posts one `MemoryTidied` item, then writes the `PassFinished` entry. If Huddle dies between the two, the next start finds a pass with no `PassFinished` and finishes it, which posts again.
- **A Memory Tidy round can take several days, and Huddle closed during a pass does not restart it.** `MemoryTidyScheduler` runs the weekly pass and, after a pass that ended at the Helper's daily cap, goes on at the first minute of the next local day, so one round can raise several Notifications. A pass cut short by closing Huddle is finished by recovery but not started again; the next regular time (or an overdue run) takes its place. A pass that threw is not retried. `Team:Memory:Tidy:When` is read once at start-up, so a change needs a restart.
- **The 90-day cleanup of the Memory backups root runs only when a pass starts**, so a lane whose `Team:Memory:Tidy:When` is invalid never cleans it.
- **Memory Tidy does not check a number written as a word, a month name, a person's name, or a "not" or "never" that reverses a sentence ([ADR-0095](../adr/0095-a-helper-tidies-memory-weekly-and-huddle-keeps-a-copy.md)).** The checked tokens are dates, numbers, links and e-mail addresses. A rewording can change a meaning without adding or losing one of them ("three" for "3", "March" for a date, "never" dropped). The Human can read the list and the copy in the Memory backups root.
- **A merge can move text from one file of a folder into another.** The checks stop text from crossing folders and from growing, not from moving. A fact can leave the file the Agent expects and sit in the kept one; the Agent's next Turn lists the change, and it can use `list` and `read`.
- **A restored file can be dropped again unless the Human adds `tidy: keep`.** Putting a dropped file's text back by hand marks nothing. The next pass may judge it out of date again.
- **A file can be halved once a pass, week after week.** A rewrite that cuts a body to less than half counts against `MaxDropsPerFolder` (3), so each pass may halve up to three files, and nothing stops the same file being halved again next week.
- **Where a file has no working description, text from its body can become one.** A new description is written only where none works, within 200 characters, one line and the token checks. That text is then in the Memory index of every session of that Agent.
- **The tidy sentence in the Memory Prompt is fixed when a session starts.** A session lives for weeks, so an Agent can be told Memory is tidied after the Human turned the Helper off, or not told after the Human turned it on.
- **Team Memory is not tidied.** A pass judges each Agent's own Memory only; a Team's and Huddle-wide Memory are outside it until slice 5, and Huddle-wide Memory is not yet judged.
- **One folder larger than one call is read in turns.** Files are shown in full from a rotating offset, so a large folder is tidied over several weeks, and a merge can only name files shown in the same call.
- **A date the Agent writes in `reported.md` is replaced.** The `memory` tool always sets the date of an appended line, so an Agent cannot back-date or post-date one. A line the Human writes by hand keeps its date, including one in the future, which age never drops.
- **An automatic compaction does not make an Agent hear the notices again.** Huddle cannot see it (the entry above), so
  after one the Agent may have lost a notice it was told. Only `/compact` re-tells every notice since the session began.
  The file is still the truth, and a new session reads it afresh.
- **A Turn that waits for the `AGENTS.md` Helper holds that Agent's other Rooms.** Turns run one at a time per Agent, so a
  Turn started just after an edit may wait up to 30 seconds, with its Room silent, and the Agent's Turns in other Rooms
  queue behind it. A Stop in the waiting Room ends the wait. A visible "reading a rule change" row would need a new Turn
  state (O-12 of the [AgentsMd Changes spec](../Huddle.AgentsMdChanges-Specifications.md)).
- **A Turn stopped just after its prompt was sent counts the notice as told.** Heard moved in the one case seen: the hook
  added 472 characters, and the next Turn added 0. Whether the Agent keeps the notice is not established. The rule's next
  edit or a `/compact` tells it again.
- **Text before and after a tool call or a Stop-hook continuation in one Turn runs together, with no break in the Room.**
  This is general behaviour, seen here through the DocHistory hook.
- **A removed Team keeps its `AGENTS.md` ledger head.** Creating a Team with the same name costs one Helper call, to
  compare the new file with the old head.
- **A short edit can talk its way into a skip.** The Helper's prompt does not escape the file's text, so words in a small
  edit can steer the Helper to call it not meaningful. The damage is bounded: a skip is believed only up to 200 changed
  characters, and the Feed lists every skip.
- **An Agent on a model without auto mode may open its first reply with an Adapter notice. Resolved 2026-11-03.**
  When the Human's own `~/.claude/settings.json` set `permissions.defaultMode` to `auto` and the Agent ran on a model
  with no auto mode, `claude-agent-acp` fell back to Accept edits and wrote "**Auto mode unavailable:** … using Accept
  edits instead." into the reply text, where Huddle could not tell it from the Agent's own words (seen in the Second
  Brain manual run on 2026-11-03, on adapter 0.75.1). Two changes retire it. Adapter 0.100.0 ships Claude Code 2.1.316,
  where the `haiku` alias is Haiku 5.5 and supports auto mode, so `haiku` no longer falls back at all. For a model that
  still lacks it (the pinned `claude-haiku-4-5-20251001`), Huddle now advertises `session.notices`
  (`Acp:AdvertiseSessionNotices`), so the Adapter sends the advisory as its own `notice` update, which Huddle logs and
  shows as a **Notice** row in the Turn's activity, apart from the reply. `RealAdapterNoticeTests` pins both halves live.
- **A pipe client can receive a Message whose sender is not in the delivery's `members` list.** A Message Huddle wrote itself
  (the wake-up after a Helper's change, [ADR-0065](../adr/0065-huddle-posts-as-itself.md)) carries the sender id `huddle`,
  which is never a Room Member. The protocol version is unchanged, and a client that looks the sender up in `members` must
  handle a miss, as it already must for a sender who has left the Room.
- **`DebouncedFileWatcherTests.Burst_OfWrites_ProducesOneSettledCallbackAfterTheBurstEnds` can fail once under the full parallel
  suite** (it saw a second callback on 2026-10-20 and passed three runs alone). It is timing-sensitive on a loaded machine and
  already skips when it sees a stall of a whole debounce window; a failure there with no change in `Watching/` is this, not a regression.
- **A document's History is what its editors reported.** An Agent adds to it by calling `log_change`, and nothing checks
  that the document changed or that every change was logged: only the tool's description asks an Agent to call it.
  Edits made outside Huddle are not recorded, and an Agent renamed later keeps its old Name on earlier entries
  ([ADR-0060](../adr/0060-a-documents-history-is-what-its-editors-report.md)). How often real Agents call the tool is
  measured by the paid probe (`TEAM_E2E_DOCHISTORY=1`, `tests/Huddle.Tests.Agents/Live/DocHistory/`), on Haiku at low Effort,
  5 runs per scenario. With the tool description alone, 0 of 35 runs logged. With the description, a system prompt
  paragraph and a Stop hook that sends the Agent back once after it touched a Markdown file, the 2026-10-19 rates were
  5 of 5 on every scenario: create, edit two files, review, object, no invented actor, asked outright, and (correctly) no
  call for a summary or a non-Markdown file. Those rates predate the read reminder (#354), which only an Agent whose role covers
  `verify` gets. Rates measured with it on 2026-11-04: a reviewer's review
  5 of 5 and object 4 of 5; an Agent with no such role, object 3 of 5 (LIVE-DH-9, recorded and not asserted). Haiku at low Effort is the hardest case the spec allows, but a Sonnet or
  Opus Agent, or an Adapter that does not run Claude Code hooks, is unmeasured, and a Turn that changes a document with
  `Bash` rather than `Write` or `Edit` is not seen by the hook. The links Huddle rewrites in other notes when a note is renamed or moved are
  not recorded in those notes' Histories, on purpose: the Human did not edit them.
- **Spike S-1 / OP-5 has not been run: whether Claude Code loads the repository's `CLAUDE.md` files into a Persona is unknown.** In development `DataDir` is inside the repository, so `E:\Repos\Huddle3\CLAUDE.md` and the one above it are ancestors of every Work Dir. If the Adapter loads them, a Persona's Instructions sit beside an unwanted second set. The check needs a live, paid Claude Code session and was not run when Instructions and the `memory` tool were built (INSTRMEM-07 in [instructions-memory.md](manual-tests/instructions-memory.md)). If the files do reach a Persona, the fix belongs to the isolation work (FC-V4), not to Instructions. Two soft boundaries are documented with the feature: the guards on `Instructions.md` and on a Team's Memory see a tool call's path arguments only, not shell redirects ([ADR-0070](../adr/0070-an-agent-reads-the-instructions-the-human-writes.md), [ADR-0071](../adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md)).
- **V0-1 (isolation) has not been run: is the Claude-profile isolation `_meta` real?** The question is whether a Claude-profile session started with it loads no `~/.claude/CLAUDE.md`, no hooks and no auto memory, and writes no auto memory. Result: Not yet run (deferred to user acceptance, 2026-10-30). Consequence: Phase 2a's *Start afresh* relies on it; until checked, a fresh session may still see the Human's Claude settings. Gates phase 2a ([the Long-Running Rooms spec](../Huddle.LongRunningRooms-Specifications.md) §4.2, LR-R4).
- **V0-2 (retention) has not been run: can session retention be set for an isolated session?** The routes are the Work Dir `.claude/settings.json` `cleanupPeriodDays`, or the `settings` `_meta`. Result: Not yet run (deferred to user acceptance, 2026-10-30); `Acp:SessionRetentionDays` not built. Consequence: Sessions unused for 30 days may still be deleted by Claude Code; the bigger phase 1 reread is the mitigation ([the Long-Running Rooms spec](../Huddle.LongRunningRooms-Specifications.md) §4.1, LR-R6).
- **A bare address-bar URL is not a reference.** Pasting `http://localhost:5100/rooms/<id>` opens the Room, but it is not recognised as a reference: no reference chip, no snapshot, no Referenced items line. Recognising it needs the app's own origin, and is planned for a later version ([ADR-0055](../adr/0055-a-reference-is-text-with-a-stable-key.md), [the references design](../Huddle.AtTagging-Specifications.md) §6.1).
- **A renamed or moved Library file shows as unavailable.** A reference is stored by path, so a Library file renamed or moved in the Library shows the unavailable chip in chat. It is not followed to its new place ([ADR-0055](../adr/0055-a-reference-is-text-with-a-stable-key.md), [the references design](../Huddle.AtTagging-Specifications.md) D-8). A Task's status change is not flagged, and a Team is linked by its Name: an in-app rename keeps its Team links working through the rename journal ([ADR-0084](../adr/0084-a-team-or-project-can-be-renamed.md)), but a Team folder renamed by hand in Explorer still makes them unavailable.
- **A Team or Project rename leaves three browser settings and some Agent writes behind.** The localStorage keys `teamsNav:expanded`, `roomList:closedFolders` and `library:team:*` are not mapped to the new name, so they fall back to their defaults (open groups and folders reset). An Agent's `MultiEdit`, `NotebookEdit` and `Bash` writes to the old folder are not caught: only Edit and Write are refused, silently, and such a write can bring the old folder back as an empty Team. The rename confirmation says so ([the rename design](../Huddle.TeamRename-Specifications.md) §6.3, §7).
- **A label typed by hand still mentions.** A reference's label is text. A hand-edited `[@Nova](/rooms/…)` puts a real `@Nova` in the Message, and Nova is Mentioned exactly as if `@Nova` were typed. Picker labels never contain a live `@`, so only a typed one does ([the references design](../Huddle.AtTagging-Specifications.md) §6.1).
- **Archiving a Room leaves its referencing Messages with no chip.** The Room still resolves, so its chip is not drawn at all; only a deleted Room shows `(unavailable)` ([the references design](../Huddle.AtTagging-Specifications.md) §8.4).
- **The `raw/` guard is soft.** It reads the path arguments of an Agent's file tools, not shell redirects, so a determined Agent can bypass it (D-31). Creating a new file and overwriting one are the same kind of tool call, so the guard tells them apart only by whether the target already exists. The **Raw files changed since filing** list that `get_help` shows, computed from each raw file's size and modification time against its baseline, is the backstop: it reports an overwrite that got through, and it does not prevent one.
- **Obsidian can't follow links into the Archive.** Huddle's own resolver can. A link Obsidian wrote to a Team or Project before it was archived breaks once that folder moves to `{DataDir}/Archive`. Obsidian does not follow the link there, so the link breaks in Obsidian even though Huddle can still resolve it.
- **Workspace writes are not restricted by Team membership.** The permission handler approves what it cannot identify, including paths outside `{DataDir}`, so an Agent can write into any Team's workspace. A membership deny rule and a watched repository need their own spec and a security review (D-39).
- **The picker's Enter guard also ignores `keyCode` 229.** On some Android IMEs (Samsung Keyboard) Enter may insert a newline instead of sending while the picker is closed. Safari's commit-Enter arrives with `keyCode` 229 and `isComposing` false, which is why the guard keeps both checks. Not yet checked by hand on Android Chrome: check it there before calling this closed.
- **An `@lib:`-style filter with a searchable phrase and no rows closes the picker.** It stays closed for that token; a new token reopens it ([the references design](../Huddle.AtTagging-Specifications.md) §5.1).
- **`![x](/rooms/a)` as an image is admitted.** The renderer's allowlist (D-10) lets a Room link be an image source. It makes one same-origin GET of the Room page and changes nothing; it is not a reference and has no chip ([the references design](../Huddle.AtTagging-Specifications.md) D-10).
- **Prompt blocks are built, and here is what they do not cover.** A Library image the Human's own
  Message names reaches the Adapter as an image, and an Adapter with no file tools gets a named
  document's text as a resource — [ADR-0036](../adr/0036-a-prompt-block-is-sent-only-when-the-adapter-advertised-it.md),
  [the design](../Huddle.PromptBlocks-Specifications.md). Six gaps are deliberate:
  - **Only a path the Human wrote in the Message that started the Turn.** Not Catch-up, not the
    Transcript, not another Agent's post. The Human attaching or pasting a file in the composer is
    [roadmap item 23](roadmap.md) B and is not built.
  - **Raster images only.** PNG, JPEG, GIF and WebP, by magic bytes. **SVG is never sent** (it can carry
    script), and **PDF and audio are not sent**: `claude-agent-acp` ignores a blob resource and audio, so
    they would vanish without an error. Every other file keeps its path line.
  - **An image stays in the Adapter session's context after the Turn.** It is not tracked, so naming the
    same file in a later Message sends it again. It counts against the token Budget like any other input;
    measured 2026-10-01, an image cost about 1,000 tokens however many megabytes it was.
  - **Limits fall back to the path line, with an Information log line and nothing on screen.** An image
    over 3 MiB, 8,000 px on a side, four per Turn or 8 MiB per Turn is sent as its path only. The Human
    is not told in the Room; the Agent's reply may say it could not see it.
  - **`TraceWire` logs the base64.** With `Team:Acp:TraceWire` on, an image is written into the log in
    full. Leave it off unless debugging a wire problem.
  - **A corrupt image does not fail the Turn.** The Adapter removes it from the conversation and the
    model says so (V-4); the image is not retried and cannot reappear, because only that Message sends it.
- **Adapter commands are built, and here is what they do not cover.** The Human can run
  `@Nova /compact` on an Agent, and the Agent says what it did —
  [ADR-0035](../adr/0035-an-adapter-command-is-a-message-the-human-addresses-by-mention.md),
  [the design](../Huddle.Commands-Specifications.md). Five gaps are deliberate:
  - **One command, one Adapter.** Only `compact` is allowed, and only on the synthesised Claude
    profile. A configured Adapter allows nothing until `Team:Acp:Adapters:*:Commands` names it, and
    changing that needs a restart. The other 50 commands the Claude Adapter advertises under isolation
    (`config`, `mcp`, `model`, `effort`, `init` among them) are not offered: most are wrong to reach
    from a chat Room, and two duplicate Huddle's own settings.
  - **The Mention is required, in every Room.** A bare `/compact` is still *"Unknown command"*, even
    in a Room of two. The shorthand needs the composer to know the live list, and stays open for a
    later version.
  - **A compaction's own cost is invisible.** `/compact` runs a model call, but the Adapter reports no
    `cost` on it (its figures are in `_meta.quota`, which nothing reads), so the Spend line does not
    move, and the token Budget sees the context fall and then takes the next update as its baseline.
    The price is a small **undercount**: the first ordinary Turn's own new tokens after a compaction
    are absorbed into that baseline. The alternative was a 40,000-token overcount per compaction.
  - **The list is empty until the Adapter's first update.** Straight after a restart, or before an
    Agent has connected, `@Nova /compact` is an ordinary Message, sent as text the Adapter does not
    read as a command. The update normally arrives within a second or two of the first Room Session.
  - **Two live checks have not run.** Whether Stop interrupts a compaction in flight (V-4), and the
    whole path in the real app (V-2), are deferred to user acceptance — see
    [manual-tests/adapter-commands.md](manual-tests/adapter-commands.md).
- **Turn detail and Spend are built, and here is what they do not cover.** A Draft
  shows its recent tool calls, an edit opens to an Edit preview, and an Agent's card
  shows what its Adapter has reported spending — [ADR-0034](../adr/0034-turn-detail-rides-on-tool-activity-as-optional-fields.md),
  [Roadmap](roadmap.md) item 21, [the design](../Huddle.TurnDetail-Specifications.md).
  Four gaps are deliberate:
  - **Both are lost on a restart, and Turn detail goes with its Draft.** The list of
    tool calls lives and dies with the Draft, in memory, and never reaches the
    Transcript. Spend is a figure for this run, not a ledger, and starts again from zero
    when the app does.
  - **One change is shown per call.** The Adapter can describe a call that edits one
    file several times; the preview shows the first change and says how many more it did
    not show. It is display only: never applied, never diffed.
  - **Six rows is a window, not a log.** A seventh call pushes the oldest out. A longer
    history, or a log the Human can open after the Turn, would need the persistence the
    design leaves out.
  - **Spend limits nothing and is not a Budget.** It is the Adapter's own figure,
    summed per currency across the sessions this run has seen. A local model reports no
    cost and shows no line, never a zero. Whether a cost continues or restarts when a
    closed Room Session is re-opened in the *same* Adapter process is unverified live
    (verification V-3), so a first re-open after an idle close could over- or
    under-count until it is run.
- **The runaway-loop guard is built, and here is what it does not cover.** A Room
  now has a Budget of agent-authored Messages between one Human Message and the
  next, and asks before granting another — [ADR-0006](../adr/0006-a-room-has-a-budget-for-agent-replies.md).
  Four gaps are deliberate:
  - **The counter is in memory and per Room.** A restart un-pauses every Room and
    shows a fresh Budget over a Transcript that already spent one. The same trade
    ADR-0004 accepted for the Catch-up buffer.
  - **An Agent can mint a fresh Budget by creating a Room.** `create_room` is
    uncapped, and each new Room starts with a full allowance. Only the per-Persona
    token Budget catches that, which is the strongest argument for it.
  - ~~**The token Budget is silent.**~~ **Closed 2026-09-13.** It is per Persona,
    so it still has no per-Room surface and no Continue prompt, but it no longer
    only writes to the log: a Persona that has spent it reads as Degraded on the
    Agent tile, with the reason — exactly where this entry predicted the honest
    home was. See [ADR-0008](../adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md).
  - **A Room now says why it stayed quiet, and here is what that still does not
  cover.** A Message naming no Agent, and a **Continue** that wakes nobody, are
  both explained in the Room view now — [ADR-0012](../adr/0012-a-room-says-why-it-stayed-quiet.md).
  Three gaps are deliberate:
  - **The note does not survive a reload.** It describes what one delivery meant, not
    durable state, so F5 loses it. Re-deriving on load would use membership as it is
    *now* and would re-explain old Messages under rules that were not in force when
    they were sent. The next Message re-evaluates, and `PersonaRunner` now writes the
    context-only line to the log, which it did not before.
  - **A Room whose Agents are all Offline or Degraded still explains nothing.** An
    unreachable Agent is not a recipient, so such a Room resolves to `NoRecipients` and
    renders no note — which keeps the note from telling the Human to name an Agent
    that cannot answer, but leaves that silence unexplained. Deliberate: shipping a
    wrong instruction was not an acceptable way to avoid it.
  - **The note reports a label, not an outcome.** The Reply Gate is client-side and is
    permission rather than obligation, so a pipe client may reply to a Message the Room
    called context-only. This is why the copy instructs ("name one to ask for a reply")
    and never predicts.
- **A reply refused for Budget still vanishes without trace.** The Draft types out a
  full answer and then goes, because `PersonaRunner`'s terminator reaches
  `Drafts.Complete` on every path a Turn can end. Nothing on screen says the reply was
  refused. Left out of ADR-0012 deliberately: there is no Message to attach it to, and
  retaining the Draft would mean splitting `Drafts.Complete`, whose whole contract is
  that both the terminator and a successful post call it.
- **A Message declined for Budget is not kept as Catch-up.** It is held for
    re-delivery instead. If you leave a Room paused and then type something rather
    than clicking Continue, the Agent's prompt will not carry the Message it was
    paused on; it is still in the Transcript and still on screen.
- **Following is built, and here is what it does not cover.** An Agent can ask to be
  woken by every Message in a Room with `follow_room` — [ADR-0005](../adr/0005-agent-topologies-are-emergent.md),
  [Roadmap](roadmap.md) item 8. Four gaps are deliberate:
  - **A follow is in memory and per Agent, and does not survive a restart.** `RoomFollows`
    is a Singleton, and `PersonaRunner` clears its own Agent's follows after each
    handshake, so a forgotten `unfollow_room` self-heals. The cost is the other
    direction: a coordinator restarted mid-pipeline silently stops being woken, and
    nothing says so.
  - **A follower is woken by every Message in that Room**, including exchanges between
    two other Agents it has no part in, and **each wake is a billed Turn**. The Room's
    Budget is what bounds that, which is why ADR-0005 made item 2 a hard prerequisite
    rather than a companion improvement.
  - **The Human cannot see who is following what.** There is no surface for it anywhere —
    not the Room view, not the Agent card. A Room that is quietly spending its Budget
    on a follower reads exactly like one that is not.
  - **A coordinator that forgets to call `follow_room` stalls silently**, which is the
    residual risk ADR-0005 names and accepts. No test can catch it: the suite answers
    through `FakeAgentHostFactory`, so it is a manual-checklist question in the same class
    as whether a real model finds any App Tool at all.
- ~~**One session per Persona spans every Room it is in.**~~ **Lifted, as code, 2026-09-23** — see
  [roadmap item 18](roadmap.md#18-one-session-per-room--delivered-code-2026-09-23) and
  [ADR-0024](../adr/0024-an-agent-holds-one-session-per-room.md), now **Accepted**. An Agent holds
  one **Room Session** per Room it is in, with the Persona text, Work Dir, Memory, App Tools and
  Adapter process still shared across all of them. Three gaps are deliberate, and one is unverified:
  - **What is given up is implicit recall of other Rooms** (RS §1.1). "What did we decide in the
    other Room?" is answered from Memory, or honestly not at all — a Room Session holds only its
    own Room's Messages, so a terse follow-up, a compaction summary and a Catch-up block can no
    longer resolve against the wrong Room. That is the design's whole point, not a residual gap.
  - **A coordinator following several work Rooms gets its old overview back only deliberately**,
    through Memory, reports in Messages and a `create_room` seed (RS §6.11) — never from a shared
    context window. The built-in Chief of Staff and the `team-building` Skill now teach this (RS-T13):
    one memory file per piece of work, and work Rooms report by Mentioning the coordinator in their
    own Room, because a work Room is not a Member of the coordinator's Room with the Human. An
    existing install picks up the new Chief of Staff text only through **Reset to default**.
  - **Follow-ups outside the spec, deferred on purpose** (RS Appendix C): dropping a duplicate
    reply when an Agent's own `post_message` and its Turn's reply land in the same Room in one
    Turn; a `read_room` App Tool for an Agent explicitly asked to look at another Room; recovering
    a dead Adapter process on the next Turn instead of waiting for Restart; deleting Claude Code's
    own transcript for a deleted Room.
  - **Unverified live:** resume across a rename, and whether `session/resume` re-applies the `_meta`
    system prompt, Model and Effort (RS Appendix B V-3); whether `agency-acp` holds several sessions
    per process and advertises `resume` (V-5) — until V-5 passes, a Persona configured on
    `agency-acp` should set `SessionPerRoom: false` (RS §6.12). The paid manual tests that would
    settle these — RS-M1 through RS-M10, and V-3/V-5 — are written in
    [manual-tests/room-sessions.md](manual-tests/room-sessions.md) and have **not been run**;
    deferred to the Human's own user acceptance testing, the same way File Changes' paid checks are.
  - **Measured for the Work Mode option, 2026-09-30:** a session set to `acceptEdits` and resumed
    from a new `claude-agent-acp` 0.75.1 process came back in `default` (Manual). The Adapter does
    not restore the mode, so Work Mode is re-applied after every resume
    ([Huddle.WorkModes-Specifications.md](../Huddle.WorkModes-Specifications.md) §6.3). The same
    run did not settle whether Model and Effort survive a resume.
  - **Measured for Work Mode, 2026-10-01**, by a script that drove the real 0.75.1 over stdio on
    Haiku (Windows, a Claude subscription, one run each), **outside the Huddle app**; the in-app
    runs remain the acceptance. **OQ-2, no:** in `plan` mode the plan is not text. The Adapter
    asked a `switch_mode` "Approve Plan" request with the plan in `rawInput.plan`, the plan guard
    refused it, the Turn ended `cancelled` with 126 characters of preamble, and `RoomSession` posts
    no cancelled Turn, so `plan` is on the default hidden list. **OQ-3, yes:** under `acceptEdits`
    a write outside the Work Dir still produced a permission request carrying
    `rawInput.file_path`, so the `~/.claude` guard survives (the path was a temp directory, not
    `~/.claude`, and `allow_always` was never chosen). **MW-2, yes:** under `acceptEdits` an edit
    inside the Work Dir produced no request and the Turn ended `end_turn`.
  - **OQ-5, measured 2026-10-01 (free, no prompt):** `auto` on a model without support (Haiku, with no Turn running) answered `acceptEdits` in the `set_config_option` response, then sent an `agent_message_chunk` reading "Auto mode unavailable: the selected model does not support Auto mode; using Accept edits instead." and a `current_mode_update` to `acceptEdits`. Opus, Sonnet and the default accepted `auto`. Setting `auto` on Opus and then switching the model to Haiku moved the mode to `acceptEdits` with only a `current_mode_update`, no message chunk. A chunk that arrives with no active Turn is
    dropped by `RoomSession`, so the notice is normally lost; if a Turn were already open it would be
    appended to that reply. `auto` stays hidden by default. Not tried inside the app.
  - **Still unrun, for Work Mode:** the in-app manual tests WORKMODE-06 to WORKMODE-09 in
    [manual-tests/work-mode.md](manual-tests/work-mode.md).
  - **A Persona set to `plan` is silent in the Room.** After an operator lifts the default block
    through `Team:Acp:HiddenModes`, a plan-mode Persona's Turn ends cancelled with no posted reply,
    because the plan lives only in the tool call. It stays so until phase 3 of the Work Modes
    design captures it ([roadmap item 22](roadmap.md#22-a-persona-has-a-work-mode--phase-1-delivered-code-2026-09-30)). A stored `plan` row on an install that has
    not lifted the block starts the Persona on the Adapter default with a warning.
- **File Changes is built, and here is what a Turn can miss** — see
  [roadmap item 11, delivered 2026-09-23](roadmap.md#11-telling-an-agent-which-watched-files-changed--delivered-code-2026-09-23),
  [ADR-0023](../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md), and FC §8. Six
  gaps are deliberate:
  - **A change made through `Bash`, or any tool call not reported as `Edit`, `Delete` or
    `Move` (other than a `write`, `append`, `replace` or `delete` through the `memory` App Tool,
    which is attributed), is not attributed to the Agent.** It comes back as an ordinary listed line on the
    Agent's own next Turn, in every Room including the one it was made in. An extra line, never
    a missed change — parsing shell commands for paths risks wrongly claiming another
    Agent's change instead.
  - **Two Agents editing the same file in the same Turn:** the other Agent's change is
    taken in with this Agent's own commit and is not listed in this Room. Needs two writers on
    one file within one Turn, and the Agent has just had the file open regardless.
  - **A tool that preserves a file's old size and modified time is not listed.** Editors and
    Agents do not do this.
  - **Huddle shutting down mid-Turn commits nothing**, so that Room's next list repeats this
    Turn's list, including the Agent's own edits. A shutdown-time write would slow teardown.
  - **A commit that fails on I/O behaves the same way.** A Turn must not fail on a bookkeeping
    write.
  - **A corrupt or deleted state file loses one list, not the app.** The next Turn lists
    nothing and saves a fresh baseline.
- **Isolating a Persona's session from the Human's own Claude Code settings is unverified,
  live.** One gap is measured and closed: the `_meta` never reached the Adapter's own reading of
  `permissions.defaultMode`, so Huddle now sets the session's mode itself (ADR-0079).
  `AdapterProfile.IsolateUserSettings` (`true` on the synthesised legacy profile,
  `false` by default on a configured `Adapters` entry) makes `DotAcpAgentHostFactory` send
  `settingSources: ["project", "local"]` and `settings.autoMemoryEnabled: false` in
  `session/new`'s `_meta.claudeCode.options` (RS §6.10, ACP A-4) — but the paid checks that
  would establish whether this actually keeps the Human's `~/.claude/settings.json`,
  `~/.claude/CLAUDE.md` and Claude Code's own auto-memory out of a Persona's session (RS
  Appendix B V-1, V-2; FC Appendix A FC-V; manual test FM-6) have **not been run**. Deferred to
  the Human's user acceptance testing; see
  [manual-tests/file-changes.md](manual-tests/file-changes.md). Until they are, treat memory
  (FC §6.15) as depending on an unverified mechanism.
  - **A known risk on the same mechanism:** `claude-agent-acp`'s `settings` option *replaces*
    its own computed settings rather than merging into them, so sending
    `autoMemoryEnabled: false` also drops any `CLAUDE_MODEL_CONFIG` model override that
    session would otherwise have carried. `settingSources` is not affected — it merges rather
    than replaces. `AdapterProfile.IsolateUserSettings`'s remarks carry this in code.
- **Account connectors are off for every Agent until the Human turns them on, and every connector call then asks first.** Every Agent session starts with `ENABLE_CLAUDEAI_MCP_SERVERS=false` unless the Human used the Agent card's Account connectors switch (stored in `persona_connectors`, written only by `PersonaStore.SetConnectors`). The switch exists only for the stock Claude Adapter with isolation; any other Adapter shows text instead, and the text claims nothing about whether that Adapter loads the connectors (spec 7.3 forbids the claim until each preset is checked). Every connector call asks first (spec 8.4). Limits: the `_meta` that carries the tool name is unproven for connectors until the paid check, so a request with no name and kind `Other` asks (fail safe); a Work Mode that never asks (`bypassPermissions`, `auto`) refuses the switch, and `acceptEdits` is allowed because the Adapter source reaches its permission check for a non-edit tool (not confirmed in Claude Code itself); a question unanswered for `UserInputTimeoutSeconds` (600 s) refuses and refuses the rest of that Turn's connector calls; arguments past 4 KB are hidden with a count; the Human's answer is also posted as a Message in the Room; a Work Dir `.mcp.json` server does not ask, because user-level servers do not load in Agents. A custom profile that runs Claude without isolating gets a `_meta` carrying only `options.env` with the connectors off (the stored "on" is ignored there); an Adapter that does not run Claude gets none (spec 3.6). On such a custom profile the Human-chosen Work Mode (a `defaultMode` or allow rule from the Human's own settings, which are not isolated there) can still skip the prompt, so the prompt is not guaranteed on a non-isolating profile. What stays soft, because no setting reaches it: (1) a `claude -p` run from Bash is a new process that Huddle's handler never sees, so it is not intercepted; (2) a Human-chosen skipping mode (`bypassPermissions`, `auto`) or a Human allow rule on a non-isolating profile skips the prompt there, because that profile's session takes the Human's own settings; (3) a shell command can write `team.db` and so the connectors row, since the guards see path arguments only (spec 6.2). A file an Agent writes in its Work Dir (an allow rule, a hook, `defaultMode`) did not get a stand-in connector tool past the handler, and Huddle's own `permissions.ask` rule for `mcp__claude_ai_*` is sent with the connectors on; that was measured on a stand-in server (source `project`), not a real connector, and the same file routes do self-grant ordinary tools, which is issue #432. MCP servers a Human put in `~/.claude.json` are **untested**: the isolation leaves `user` out of `settingSources`, but no check proved those servers stay out (spec 3.3).
- **Every Agent has its own browser, on by default, and only what it does to a page asks** ([ADR-0090](../adr/0090-an-agent-has-its-own-browser-through-playwright-mcp.md),
  [spec](../Huddle.AgentBrowser-Specifications.md)). The Human can switch it off per Agent on the card. The limits:
  - An Agent with `Bash` can still run Playwright itself, write its own MCP server config or read the profile
    folder (`browser-profiles/<Name>`, which holds the Agent's sign-ins). The switch governs only the server Huddle
    configures. This is the same soft boundary as the other ungoverned routes on this page.
  - The rule never to type a password, one-time code, card or bank number or government ID is an instruction.
    Huddle cannot see field types. The Human's check is the approval card for typing.
  - Every click asks, so Huddle cannot tell "Submit" from a link. `browser_navigate` does not ask, so a URL with query
    text is not approved. The card cannot show the page URL either: Huddle does not know it, and `element` is the
    Agent's own words.
  - `browser_network_request`, `browser_network_requests` and `browser_console_messages` are read tools and run
    without asking. On a page the Human signed in to they can show the model the site's tokens.
  - There is no live view from another device. Watching and signing in need the PC that runs Huddle. On Linux with no
    display the browser runs headless and the Human cannot sign in to a site for it.
  - One Agent per profile folder at a time: Chromium lets one browser hold a profile. Each Room's session starts its
    own server, so an Agent in two Rooms at once gets "Browser is already in use" in the second until the first
    calls `browser_close` or the 30-minute idle close fires. Claude Adapter Agents only; the Agency Adapter and a
    Claude profile without isolation get none.
  - The MCP node process (about 95 to 125 MB) starts with every isolating session, whether or not the Agent browses.
    The browser itself (8 to 10 processes, about 500 MB) starts on the first browser call.
    `Team:Browser:Enabled=false` removes both for the install.
  - A Persona file deleted by hand keeps its switch row until the app sees the file gone (the rescan removes it) or,
    if the app was off, until an Agent with the same name is hired, which clears it. A row for a missing Agent is
    harmless: nothing reads it.
- **One `AppToolServer` per Persona** — one loopback Kestrel each. Revisit past
  about four Personas.
- **`SystemPromptMode.Append` has never been evidenced against a live model.** If
  Personas do not take, suspect this first.
- **No test can prove a real model calls `mcp__team__get_help`.** The suite runs
  through `FakeAgentHostFactory`, which answers whatever it is told to, so it
  pins the *text* — that the prompt names the tool, and that the tool names the
  rest — and not the behaviour. Whether progressive discovery actually happens is
  a manual-checklist question, in the same class as whether a model finds any App
  Tool at all.
- **The inline guidance in the system prompt duplicates `get_help`.** Deliberate:
  the fixed block below the Persona still states the Reply Gate and the Mention
  rules, so an Agent that never calls a tool behaves correctly and `get_help` is
  an amplification rather than a precondition. The cost is two places to edit
  when a chat rule changes.
- **An Agent can invite into any Room whose id it holds**, including one it is
  not a Member of. `ChatService.InviteAsync` checks that the Room and the Agent
  exist, not who is asking. Acceptable while every Room contains the one Human.
- **Open-in-editor launches a process on the server, ungated in every
  environment.** Deliberate for a single-user PoC; unacceptable deployed.
- ~~**Streaming replies.**~~ **Built 2026-09-13**, and the reserved shape did
  exactly what it was reserved for: `messageDelta` needed no version change to
  activate. What it lands in is a **Draft** — in memory, never in the Transcript.
  Its own limits are below.
- **Authentication and authorisation.** None, by design, at this stage.
- **More than one Human.** The data model allows it; the seeding and UI do not.
  When it arrives, `/agents` is already named correctly for it — add a filter
  toggle rather than a second page.
- **Renaming an Agent now carries the whole Agent with it**, and what
  remains absent is narrower than it was. [Roadmap](roadmap.md) item 10 made a
  rename an edit to one frontmatter field; item 1's cascade landed 2026-09-15
  ([ADR-0011](../adr/0011-a-rename-moves-the-agent-not-its-history.md)). The
  Agent's row is renamed **in place, keeping its id**, so the Rooms, the
  memberships and the Transcripts follow; the Model and Effort rows move; the
  Work Dir moves; and Rooms still carrying an auto-derived name are re-derived
  while Rooms the Human named by hand are left alone. Four things it still does
  **not** do, each on purpose:
  - **History is not rewritten.** Messages already posted keep the Name they were
    posted under, because `ChatMessage` stores a denormalised `SenderName`
    alongside `SenderId`. Rewriting would need a rewrite path on the append-only
    `FileChatStore` and would edit a historical record in place.
  - **The session still restarts and still loses its conversation memory.** A
    system prompt is fixed at `session/new`; nothing here changes that.
  - **A rename whose new Name is already held by another Agent does nothing.**
    `RenameUser` returns false, the cascade stops before touching a Room or the
    Work Dir, and a warning is logged. Nothing says so on screen.
  - **The Work Dir move can lose a race and give up.** The old agent process may
    still hold it as its `cwd`; the move retries a handful of times and then logs
    a warning rather than failing the rename.
- **Removing a Persona still does not cascade**, and that is unchanged and
  deliberate — see the entry below on `PersonaStore`. A removal means the
  Agent is gone, so its Rooms and Transcripts are chat facts that outlive it;
  a rename means it is still here under another Name, which is why the same
  no-cascade rule read as correct for one and as a bug for the other.
- **A Room keeps auto-naming itself until somebody renames it, and that is
  detected by comparison rather than by a flag.** `ChatService.InviteAsync`
  re-derives a Room's name from its Agent Members only while the current name
  still equals what `RoomNaming.Derive` would have produced; a Human-chosen name
  therefore survives every later Invitation. There is no `name_is_custom` column
  and deliberately so — the schema is created with `CREATE TABLE IF NOT EXISTS`,
  so an existing `team.db` would never gain one (see [Traps](traps.md)). The
  accepted cost is one invisible edge: rename a Room to precisely the name
  auto-naming would have chosen and it stays auto-named, so the next Invitation
  re-derives over it. Nothing observable distinguishes the two states.
- **Two live 1:1 Rooms with the same Agent are reachable, since 2026-09-21.**
  Archiving a Room excludes it from `FindRoomWithExactMembersAsync`, so starting a
  chat with that Agent creates a fresh Room rather than resurrecting the
  archived one — a deliberate choice, recorded in
  [ADR-0018](../adr/0018-a-room-can-be-archived-or-deleted.md). Unarchive the first
  one afterwards and two non-archived Rooms now hold exactly `{Human, Agent}`,
  both named by `RoomNaming.Derive` and therefore identical in the sidebar.
  [ADR-0003](../adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md)'s
  one-two-Member-Room-per-Agent invariant no longer holds in that case. Nothing
  breaks; the lookup gained an `ORDER BY r.created, r.id` so which Room wins is at
  least deterministic (the oldest) rather than whatever SQLite returns first.
- **Renaming a Room does not make two Rooms distinguishable on its own.** Rooms
  nobody has renamed are still named after their Members and still carry no
  timestamp, no last-message preview and no other mark, which is the other half
  of [product observation 6](product-observations.md) and is not built.
- **Windows reserved device names are accepted as Names.** `CON`, `NUL`, `COM1`
  pass `NameRules` and become `CON.md`, which Windows will not create. Pre-dates
  the space change and is not guarded against.
- **Two Members whose Names differ only in case** resolve to whichever the
  longest-first ordering reaches first. `PersonaIndex` now prevents this between
  two *Personas* — `Jarvis` and `jarvis` are a case-insensitive collision and
  both files are rejected — but Members are not only Personas. Two raw pipe
  clients, or a pipe client and a Persona, can still register Names differing
  only in case, and nothing catches that.
- **The Model is per Persona.** Not per Room, not per Turn, and not switchable
  on a running session.
- **No live mid-session Effort switching.** Restart-on-change is the only
  mechanism, exactly like the Model and the system prompt it already applies to.
- **The Agent card shows the stored Effort, not the session's live
  `currentValue`.** If the adapter clamps a requested level back to
  `"default"`, the card will not say so — it only ever reads back what
  `PersonaEffortStore` has on file.
- **The model catalog is probed once per app run and cached.** Upgrade the
  adapter and the picker can be stale until restart. Only the picker: the Model
  actually applied is re-resolved against the live session every time one starts.
  A deliberate deviation from `acp-session-config.md`'s "caching across spawns is
  not fine", which protects selection rather than presentation.
- **That probe runs even with `Team:Acp:Enabled` false.** Opening a New/Edit
  agent card, and changing its Model select, spawns a throwaway Adapter
  regardless of the money flag — see [Rules](rules.md). It spends nothing,
  because it never starts a Turn; the flag exists for the thing that does.
  Gating the probe on `Acp:Enabled` would leave the picker empty in the default
  configuration and defeat the feature, so the row is expected, and it is
  expected to be short-lived: it appears and disappears within seconds. An
  Adapter from this path that PERSISTS is not the probe — that is a real
  Adapter, spending the operator's money in a configuration they switched off.
- **`session/set_model` is unavailable.** The vendored adapter advertises only
  `configOptions`, never the unstable `models`/`SessionModelState`. If a future
  adapter adds it, `ModelConfigOptions` is the single place to teach.
- ~~**A Persona's identity does not reach `agency-acp`.**~~ **Closed 2026-09-18.**
  `AgencyDotNet.Acp` 0.1.195 read only `_meta.model` and dropped the composed prompt
  silently, so every Persona there ran on the harness's baseline text and two
  Personas on that Adapter were indistinguishable. Fixed in **0.1.197**, which reads
  `_meta.systemPrompt` into `QueryContext.IdentityPrompt`. Verified end to end
  against a live model, not taken on report: a Persona's name and nonce codeword
  came back in the reply, and two concurrent sessions with different identities did
  not bleed. Recorded as D-1 in Live findings.
- **A small local Model may not *inhabit* a Persona, even though the Persona now
  reaches it.** This is what is left of D-1 above, and it is a property of the Model
  rather than a defect anywhere. Observed 2026-09-18 on `gemma-4-e2b`: given two
  distinct identities, both replies opened *"I am Gemma 4, a Large Language Model
  developed by Google DeepMind"*; one then adopted its Persona correctly, the other
  volunteered its own codeword while still answering as Gemma. The identity is
  demonstrably present — the codewords prove it is being read — a 2B-class Model
  simply does not consistently hold a voice. Untested whether a larger local Model
  does. Expect local Models to suit narrow Personas long before they suit ones with
  a strong character, which is the honest limit [Roadmap](roadmap.md) item 12 named
  before there was evidence for it.
- **A failed probe and a cancelled one look alike in the log.** Both return an
  empty catalog; the warning names the cause, but an authentication failure and a
  missing adapter both present to the user as "this agent advertises no models".
  Narrowed 2026-09-13: a *Persona that failed to start* for either reason now says
  which, on its tile. The model **picker** is still the ambiguous surface.
- **What a Draft does not survive.** It is in memory, per process, so a restart
  loses the streamed text of every Turn in flight. The Turn itself is not lost: the
  Turn Queue runs it again once on the next start, led by a cut-off note
  ([ADR-0091](../adr/0091-a-rooms-waiting-turns-are-kept-in-team-db.md)). Its
  text is capped at 256 KB; past that it stops growing rather than truncating what
  is already there. Both are deliberate — it is a Singleton holding model output
  for the life of the process, and an Agent killed without a clean disconnect
  never sends the terminator that would clear it.
- **What the Turn Queue does not cover** ([Turn Queue spec](../Huddle.TurnQueue-Specifications.md) §13).
  A crash in the instant between the runner writing `Started` and the server committing it re-runs that Turn
  as merely waiting, without the cut-off note (E-14). A form waiting when Huddle stops is part of its Turn and
  is asked again by the re-run, not kept (D-28). Follows and the Task in hand are in memory and are not
  restored, so a re-sent Task wake-up files a new Task under the Room's Project (E-4). A cut-off Turn that had
  already used `post_message` may answer the Human a second time; the cut-off note asks the Agent to check,
  nothing enforces it (E-15). A tool a cut-off Turn started can outlive Huddle (spike S-0: the Adapter dies with
  Huddle, its shell does not), so the re-run may overlap it; the note says so. The Room Budget lives in memory
  and starts at zero at every start, so a Message held for a paused Room is answered after a restart without
  waiting for Continue.
- **A waiting Proposal does not survive a restart, and the proposer is not told.**
  `ProposalStore` holds at most one Proposal per Room in memory, like a Draft, so a
  restart loses it and its card. The Agent that proposed is still waiting for an
  outcome Message that never comes; the Human has to ask again. Archiving or
  deleting the Room drops it on purpose. Deliberate for V1: persisting it would
  mean a table and a recovery path for a card the Human can recreate by asking.
- **A waiting form holds its Turn, and its Persona's slot, until it ends.** ACP `elicitation/create`
  (Claude's `AskUserQuestion`, the retry-after-refusal dialog, an MCP server's form) keeps the asker's Turn
  open while the `ElicitationCard` waits, unlike `ask_human`. The idle watchdog is paused for the wait, so a
  forgotten card is not reported as a hung Adapter, and `Acp:UserInputTimeoutSeconds` (default 600) ends it:
  the request is answered cancelled and the card goes. With `Acp:MaxConcurrentTurns` at its default of 1 the
  Persona's Turns in its other Rooms queue behind the card for that long. A restart loses the card and the
  Turn with it, because the request belongs to the Adapter process; an `ask_human` card, which holds no
  request, is kept in `team.db` instead ([Turn Queue spec](../Huddle.TurnQueue-Specifications.md) §5a). Answering a form never wakes the asker (the answer is a Message withheld from it); the
  accepted leaks are that another Agent that follows the Room still wakes, that the asker's own token
  Budget is not reset by the Human's answer, and that on resume the asker's catch-up may include the answer as
  context. A schema the card cannot show faithfully (a nested object, an array of objects, `$ref`/`allOf`, a type
  list, more than 20 fields or 50 options) is answered `decline` at once and no card appears. The wire trace
  shows the request as `_elicitation/create`, because the host renames it so `dotacp` routes it
  ([the Questions spec](../Huddle.Questions-Specifications.md) §6.8a).
- **A Claude Agent does not ask with `ask_human` on its own.** The Claude Adapter defers every
  MCP tool: the model sees the name `mcp__team__ask_human` and nothing else until it calls
  ToolSearch, so the description that says when to ask is never read. Measured 2026-10-01 on Haiku:
  given *"Help me plan a workout routine"* it wrote its questions as a list, twice; told to call
  the tool by name it did, and everything after worked. A paragraph on when to ask, `systemPrompt.askHuman`,
  now sits in the system prompt, which a model always reads. One live re-run on Haiku (2026-10-01) called
  ToolSearch and then the tool unprompted. That is one sample, so expect a model to still sometimes
  write a list, and treat the paragraph's wording as the thing to tune.
- **Stop is per Room.** Stopping an Agent in one Room stops only that Room's session
  ([ADR-0024](../adr/0024-an-agent-holds-one-session-per-room.md)): `StopAsync` routes the
  Stop to that Room's session, and a Room with no session yet is a no-op
  (`src/Huddle.App/Acp/Sessions/RoomSessionPool.cs:214-230`). The queue drain is per session
  too, so a Stop does not discard work queued for the Agent in its other Rooms
  (`src/Huddle.App/Acp/Sessions/RoomSession.cs:695`). The Persona text, Work Dir, Memory, App
  Tools and Adapter process are still shared across Rooms. An Adapter profile with
  `SessionPerRoom` false keeps one shared session for all of them; the default is true
  (`src/Huddle.App/Acp/AdapterProfileOptions.cs:74`).
- **Tool activity is never written to the Transcript.** It belongs to the Draft
  and goes when the Draft does, so scrollback shows what an Agent said and not
  what it did.
- **Quota, a network failure, and credentials expiring mid-session are
  indistinguishable.** All three arrive as one exception type carrying
  Adapter-authored wording, which nothing here pattern-matches, because it is not
  ours and it will change. They are reported by *persistence* instead: one failed
  Turn is noise, three in a row escalates the reason to say so. The Agent stays
  Degraded rather than Offline, because the session and the pipe may both be fine
  while the provider refuses.
- **A Degraded Agent is never restarted automatically.** The Human clicks Restart
  on the Agent card. Automatic recovery would need a policy nobody has asked
  for, and a restart clears what that Agent remembers.
- **What a Theme cannot do.** Roadmap item 6's four limits were retired on 2026-09-14
  when theming moved to MudBlazor — see
  [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md) — and the list was rewritten
  again on 2026-09-21 when a Theme became a single palette
  ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). Four different things are
  absent now:
  - **The application does not follow the device's light/dark setting.** There is no
    System option and no light/dark control at all: a Theme *is* a light one or a dark
    one, and the one you pick is the one you get, on every device, until you pick
    another. Doing this honestly needs a **pair** of authored Themes and a rule for
    resolving between them, the way VS Code's *Preferred Light* / *Preferred Dark* and
    "Sync with OS" work. Every one of the eight Themes has a real counterpart in the other mode, so
    the door is open; it is not built. This replaced a worse limit — until 2026-09-21 the
    preference could contradict the Theme, and selecting a dark Theme with `Light`
    showed Huddle's palette under that Theme's name.
  - **A Human can customize only the accent.** `appearance.json` holds the Theme ids and
    `customizations` per Theme (a palette and token override), but the Appearance tab writes
    only `palette.primary` (**Accent for <Theme>**, with Reset); other keys are for hand edits
    and the Theme lab, which exports JSON and saves nothing. The Library pane side
    setting was retired 2026-10-10 with the docked pane; a leftover `libraryPaneSide` key in an
    existing file is ignored without a warning and left untouched. The override map, its allowlist and the inline `<style>` are gone.
    Changing another colour permanently means editing a skin file under `Themes/Skins/`
    and rebuilding, or waiting for item 7's Theme import.
  - **A bad Theme id is logged, not shown.** `AppearanceStore` warns, names the unknown
    id, leaves the file untouched and falls back to the default Theme — but the
    Appearance tab no longer reports it, because the section that did belonged to the
    override layer. This is the one place the repo's "reported, never swallowed" habit
    is now weaker than it was; the log is the only surface.
  - **The choice is per installation, not per browser.** Unchanged, and still
    deliberate: it lives in `{DataDir}/appearance.json`, so a second browser, a private
    window and a phone on the same install all see the same Theme. One Human per
    installation is the assumption it rests on.
- **There is no high-contrast Theme.** The seventeen Visual Studio Code ports, including the
  two High Contrast ones, were removed on 2026-10-17
  ([ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md),
  superseded; [ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md) removed the Huddle and
  Custom Accent pairs). The picker offers the eight Lumen, Fluent, Nordic and Cobalt Soft Themes, and none of them is designed as a high-contrast mode. A Human who
  stored a removed id in `appearance.json` gets the default Theme and a log warning, not an error.
- **An Agent chooses its own Avatar, and here is what that does not cover.**
  Initials, a short label or an uploaded image, over a background colour —
  [ADR-0019](../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md). Six
  gaps are deliberate:
  - **An Avatar does not travel with a copied `.md` file.** It is in
    `avatars.json`, keyed by Name, so copying a Persona to another machine brings its
    text and not its face. Neither its Model nor its Effort travels either, for the
    same reason, and nobody has asked for those.
  - **Images are stored as uploaded — no resizing, no dimension cap.** Only the byte
    count is bounded, at 512 000. Checking pixels means decoding, which means an image
    decoder, a new package and a decompression-bomb surface; the browser scales the
    image down with CSS instead. A 500 KB image stays 500 KB on disk.
  - **An emoji outside the Basic Multilingual Plane is stored escaped.**
    `System.Text.Json` escapes surrogate pairs whatever encoder it is given, so `🦊`
    is written `🦊` — verified at the byte level, not assumed. It round-trips
    exactly and a Human may type the literal character in; only the file's readability
    suffers. See [Traps](traps.md).
  - **An orphaned entry is never pruned.** Delete a Persona's `.md` while the app is
    *not* running and nothing observes the deletion, so its entry and image stay. The
    same tolerance `appearance.json` gives a `theme` id naming nothing: keep the file,
    ignore what cannot be resolved. A deletion made while the app *is* running is
    caught by the watcher and does cascade.
  - **`RoomList` shows no avatar.** A Room is named after *all* its Agent Members and
    `Room` carries no member list, so no single Agent is recoverable from a sidebar
    row without a per-row membership query. A wrong face on a Room is worse than none.
  - **After a rename, Messages already posted keep the old Name** — history is never
    rewritten, and `ChatMessage.SenderName` is denormalised — so their avatars fall back
    to the old monogram while the Agent's current Name carries the chosen one. The
    Transcript records what was said at the time, faces included.
- **The avatar library has three limits of its own**
  ([ADR-0044](../adr/0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md)).
  - **A library created after start-up needs a restart to be served.** The
    `/avatar-library` static mount is added at start-up, and only when the folder
    exists then, because a `PhysicalFileProvider` throws on a missing root. A library
    folder created while Huddle runs is not served until the next start. The picker's
    button follows `master.json` and appears at once, but its thumbnails stay broken
    until then.
  - **Library images are served unauthenticated, as other avatars are.** The mount is
    same-origin with no sign-in, the same as `/agent-avatars`. The library holds
    illustrations, and nothing in it is personal, but a file placed in the folder is
    visible to anyone who can reach the app.
  - **The app does not resize library images.** Each file must be prepared beforehand
    to 256 × 256 and at most 512 000 bytes. The app checks the byte count and the file's
    magic bytes, and skips anything else with one warning on open. It never decodes an
    image, so a file that is the right size but the wrong shape is shown as it is.
- **Threads, reactions, message edits, message deletes, attachments, search,
  notifications.** *Room* archive and delete arrived 2026-09-21
  ([ADR-0018](../adr/0018-a-room-can-be-archived-or-deleted.md)); nothing here
  edits or removes an individual Message, and a Transcript is still never
  rewritten in place.
- **Former flake, FIXED 2026-09-27 — and it was a product bug, not the test's timing.**
  `PersonaSupervisorLifecycleTests.Shutdown_DisposesEveryHost` (quarantined in CI), and on
  `feat/library` most of the rest of that class, timed out in `WaitUntilAsync` on Linux.
  The recorded cause, "its 10-second token races `WaitUntilAsync`", was wrong: with the
  budget raised to 30 s, the same runs failed 30 s later. On Linux, `PersonaStore`'s
  `FileSystemWatcher` watches a new sub-folder only after it reads that folder's Created event.
  A Persona file written into the folder first raised nothing, and `AffectsATeamsFile` dropped
  the folder event. So a Team folder created and filled from outside the app could stay
  unloaded until some unrelated edit happened to trigger a rescan. The folder's Created event
  now schedules the rescan. `PersonaStoreTests.ExternalFileCreated_InANewlyCreatedTeamSubFolder_…`
  failed 12 of 80 runs under load before the fix. The quarantine is lifted.
- **Second known flake, pre-existing — DIAGNOSED 2026-09-16, FIXED 2026-11-01, and it was not
  only a flake.** A timing race in event ordering over the fake transport in
  `Huddle.Acp.Tests`, roughly one run in five, passing on rerun. **Three tests were
  known to show it**, which was the argument that the race is in the transport rather
  than in any one test: `PromptAsync_StreamsChunksInOrder_ThenTurnCompleted` and
  `PromptAsync_ThoughtAndToolCallEvents_ArePublished` (both quarantined by name in CI until the fix),
  and `DotAcpConcurrentHostTests.TwoHosts_ConcurrentPrompts_EachSessionOnlySeesItsOwnAgentsUpdates`,
  seen on 2026-09-15 in CI run 607 and **not quarantined**. All three fail the same way:
  a `TurnCompleted` arrives where a `MessageChunk` or a tool-call notification was
  expected.

  **The mechanism, caught with instrumented reproduction while building the Tier 3
  conformance suite.** The peer writes its `session/update` notifications and the final
  `session/prompt` response to the wire **strictly in order**. The client does not
  *dispatch* them in that order: StreamJsonRpc completes a response through a different
  path from the one that invokes notification handlers, and nothing sequences a
  response's continuation against notification handlers already in flight. One captured
  failing run showed chunk 1 dispatched on thread 6, the **response** dispatched on
  thread 7 fractions of a millisecond later, and chunks 2 and 3 only entering thread 6
  *after* that. `PersonaRunner.RunEventReaderAsync` then reads its channel in arrival
  order — correctly — but arrival order is no longer wire order, so `TurnCompleted`
  clears `activeTurn` and `AppendAndPublishDeltaAsync`'s null-turn guard (right for the
  "stream ended mid-turn" case it was written for) **silently drops the remaining
  chunks**. Every component behaves correctly on its own; the defect is the unstated
  assumption that wire order survives dispatch.

  **It is a content-loss bug, not just a test flake.** The same `activeTurn.Text`
  accumulator that feeds the live Draft is what gets posted, so a Turn affected by this
  loses text from the Message that lands in the Transcript, not merely from the
  in-flight render. It needs a warm thread pool to surface, which is why it reads as a
  flake: cold and quiet it does not race. A real adapter's process-pipe latency widens
  the gaps and makes it rarer than it is in-proc, but not impossible.

  **Mitigated 2026-09-23.** Room Sessions made it frequent: two Room Sessions prompting at
  once on one Adapter process lost a second Room's whole reply in the conformance suite. A
  barrier inside StreamJsonRpc needs its non-concurrent `SynchronizationContext`, but
  dotacp's `Connection` starts listening inside its constructor, which locks that setting
  first. So `PromptAsync` waited for 100 ms with no further update before publishing
  `TurnCompleted`. That was a timing margin, and CI run 1233 (2026-11-01) beat it on a PR
  that touched only the Gateway.

  **Fixed 2026-11-01 with a dispatch barrier that does not depend on timing.** Every inbound
  line already passes through our own line-buffered reader, `NdjsonMethodRewriteStream`, in
  wire order; the host now wraps the agent's output in it always, not only when it
  advertises elicitation. `WireOrderLedger` counts per session the `session/update`
  notifications that reader has queued and the ones `DotAcpClientAdapter.SessionUpdateAsync`
  has dispatched. After the response, `PromptAsync` waits until the dispatched count reaches
  the read count, and only then publishes `TurnCompleted`. The response line can only have
  been read after every update the peer wrote before it, so the wait covers all of them. It
  is woken by each dispatch, not polled, so a Turn no longer pays the 100 ms. A 2 s cap
  remains as a safety net against an accounting gap, for example an update counted but never
  deserialised. When the cap fires, it logs a Warning and settles the counts, so the gap is
  not owed again by every later Turn. `WireOrderBarrierTests` holds a dispatch back
  deterministically and pins the order. The timing test that the barrier replaces,
  `PromptAsync_ChunkDispatchedAfterResponse_StillPrecedesTurnCompleted`, is gone; it sent its
  chunk after the response, which the protocol forbids. The three tests above passed 30 of 30
  each, and 18 loaded whole-project runs passed. The CI quarantine lines are deleted.
- **Third known flake, pre-existing — DIAGNOSED 2026-09-17.** A full
  `dotnet test Huddle.slnx --` occasionally crashes the test host *after* every
  assertion has passed, with an `ObjectDisposedException` raised from
  `PersonaStore.OnWatcherError`. The cause is an ordering one, not a timing one:
  that handler calls `this.logger.LogWarning(...)` **before** it takes `watchGate`
  and checks `this.disposed` (`PersonaStore.cs:586-600`). During host teardown the
  logging provider can already be disposed when a `FileSystemWatcher` raises a
  late `Error` event, and because the handler runs on a watcher callback thread the
  throw is unhandled and takes the process with it. It reads as a flake because it
  needs a dropped-event overflow to land inside the teardown window. **Fixed
  2026-09-22** (Skills D8): the log moved inside the existing `disposed` guard, the
  same order `SkillStore.OnWatcherError` uses, and
  `PersonaStoreTests.OnWatcherError_AfterDispose_DoesNotLog` fails if it moves back.
  A test-host crash naming `OnWatcherError` is now a regression, not this flake.
- **A fourth flake, seen once, not diagnosed — 2026-09-22, seen again the week of 2026-09-23.**
  `PipeEndToEndTests.Disconnect_CleanClose_LogsExactlyOneInformationLine` failed once in a
  full run during the Skills build (`Assert.Single()` found no matching log line) and passed
  on the next full run and on its class alone. The test and the pipe code it covers were
  unchanged at the time, so it is recorded rather than read as a regression. It asserts on
  a log line written after the client disconnects, which suggests the assertion sometimes
  runs before that line is written; if it recurs, wait for the line rather than reading
  the log once. It recurred, intermittently, during the Room Sessions build — same symptom,
  passing on rerun — which is evidence for the theory above rather than a new cause.
  **DIAGNOSED and fixed 2026-09-29** — the theory was right. `AgentConnection.RunAsync`'s
  `finally` unregisters from the gateway *before* it writes the disconnect line, so "offline"
  does not mean "logged"; the test waited for offline, then read the log once. It now waits for
  the line itself on the same deadline. The product order is deliberate and unchanged. 6/200
  failing under 40-process load before, 0/200 after.
- **A fifth flake, seen during the Room Sessions build — DIAGNOSED and fixed 2026-09-24.**
  Three tests failed intermittently under full-suite load. Two were tests asserting before
  async work landed: `AgentProfileCardModelEffortTests.ViewMode_ShowsTheChosenModel` (a find-then-click racing
  the page's SQLite-backed completion render) and `RoomSessionTests.IdleTimeout_CancelsFarSideFirst`
  (reading the Turn-failure report straight after the far-side cancel, which precedes it).
  The third, `RoomSessionPoolTests.Max2_ConcurrentOpens_NeverExceedCap`, was **a real defect**,
  not timing: a session opened lazily inside its first Turn published `Idle` rather than `Busy`
  when the open finished, so a third Room opening at the live cap could pick it as
  `RoomSessionPool`'s eviction victim and close it mid-Turn, losing that Turn's reply.
  `RoomSession.OpenAndStartReaderAsync` now publishes `Busy` when a Turn is running, and
  `RoomSessionPoolTests.LazyOpenedSession_MidFirstTurn_IsNeverTheEvictionVictim` pins it
  deterministically. The same investigation found and fixed a sixth, unrecorded one of the first
  kind: `RoomSessionResumeTests` read the Room Session store right after the reply was posted,
  though the store entry is only written at Turn end.
- **Sixth known flake, new in Team Pages delivery — a real defect, fixed 2026-09-29.** `PersonaRenameCascadeTests.Rename_RenamesTheAgentRow_AndKeepsItsId` passed alone and failed under full-suite parallel load. It did not reproduce in ~240 loaded runs on 2026-09-29, but the investigation found a race in `PersonaStore` that produces exactly its symptom: the watcher's debounce rescan (`OnDebounceElapsed`) took only `watchGate`, so it could run inside an `Update` — after the file write, before `Update`'s own publish — and raise `PersonaRenamed` from the timer thread. `Update`'s publish then found nothing to diff and returned while the Team Directory row rename was still running elsewhere, so a caller reading it straight after saw the old Name. The same window let `PersonaStoreTests.Update_RaisesPersonasChangedOnce` see two events. The debounce now takes `writeGate` then `watchGate`, the order `Add` and `Update` already use, so an edit's write-and-publish is atomic against it; events are still raised outside both locks. `PersonaStoreTests.Update_ADebounceRescanDuringTheWrite_DoesNotStealTheRename` pins it deterministically (80/80 red with the lock removed, green with it).
- **Seventh known flake, pre-existing — DIAGNOSED and fixed 2026-09-29.** `RoomListTests.Archive_RemovesTheRoomFromTheSidebarList` (and, sharing the cause, `TaskViewNavTests.RightClickingARow_OpensTheSameMenu`, `EditView_…` and `RowMenu_…`, and `RoomListTests.Delete_Confirmed_RemovesTheRoom`): bUnit's synchronous `Click()` / `ContextMenu()` discard the dispatch task, so when a background render holds the dispatcher the handler runs after the call returns and the assert reads the old state. The tests now await `ClickAsync()` / `ContextMenuAsync()` inside `InvokeAsync` — see [BlazorTesting.md](../../agents/BlazorTesting.md). The three classes went from 12/64 loaded runs failing to 0/128. Many other bUnit tests still use the synchronous form and share the exposure; they have not been swept.
- **Full-run shutdown line — was not benign; fixed 2026-09-29.** The `[FATAL ERROR] Foreground threads were left running` line at the end of a full run was not a thread: every `TempDataDir` left its `team.db` pooled by `Microsoft.Data.Sqlite`, and the pools were closed one by one in a `ProcessExit` handler. xunit prints "Waiting 10 seconds for foreground threads" whenever exit takes over a second, and when it passed 11 s it force-exited and `dotnet test` reported `error: 1` — a red run with `failed: 0`. `TempDataDir.Dispose` now clears its own databases' pools before deleting; `TempDataDirTests` pins it for all four SQLite-backed stores.
- **Fixed 2026-09-29, was a known bug:** SQLite connection pooling kept a handle on every
  test's `team.db`, so `TempDataDir.Dispose`'s delete failed silently and each run left its
  directories behind — measured at 1,843 per `Huddle.Tests` run, not the 83 once recorded here
  (about 983,000 had built up under `%TEMP%\team-tests`, ~50 GB). Fixed with the shutdown line
  above; a run now leaves none.
- **Flaky under heavy parallel load — seen 2026-09-29, mostly fixed the same day.** With several
  full suites running at once on one machine, these failed:
  - `PersonaStoreTests.Update_ChangingTheName_…` and five tests of the same shape — **fixed**: they
    awaited the first `PersonasChanged` rather than the condition they assert.
  - `PersonaRenameCascadeTests.PersonaRenameCascade_RenamesTaskAssignee` — **fixed**: it seeded its
    Task with a raw file write, an outside edit that `TaskService.OnOutsideEdit` answers with a
    Change log append; when the store's own watcher found it, that append could land between
    `RenameAgentCore`'s read and write, fail the version check and skip the rename. It now seeds
    through `TaskStore.Create`.
  - `TeamPagesEndToEndTests.CatalogFlags_FolderWithoutMembers_AndLabelWithoutFolder` — **fixed**
    (11/16 loaded runs failing): `TeamFolderProvisioner` creates a folder for every valid Team label,
    so "a label with no folder" was only ever transient. The test now uses a label the provisioner
    refuses on every OS (`Ops?`).
  - `LibraryDocumentEditTests` — its dialog clicks moved to `ClickAsync()`; the cause was not proven.
  - **`TaskServiceTests` — still open.** The likely pair is `Close_DiskVersionChangedSinceRead_Conflict`
    and `Reopen_DiskVersionChangedSinceRead_Conflict`: they write an outside edit straight to disk and
    expect a Conflict, but if the store's 500 ms watcher rebuild runs first, `OnOutsideEdit`
    refreshes the index and the call succeeds. A deterministic fix needs a way to hold the debounce
    — a `src/` seam, not yet added.
  - `RoomSessionConformanceTests.TwoRooms_TwoSessionNewCalls_OneProcess` failed once with a
    `TimeoutException` and did not reproduce in ~700 loaded runs; if it recurs, capture whether the
    `session/prompt` response and `MessagePosted` arrived. The 100 ms quiet window that was the
    suspect was replaced by the dispatch barrier on 2026-11-01, so look first for the barrier's
    cap Warning in the log.
- **`PersonaRunnerTests` runs on a fake clock since 2026-09-29, with two deliberate gaps.**
  `RoomSession`'s idle watchdog and activity stamps now read the session's injected `TimeProvider`,
  so the idle-timeout tests advance a `FakeTimeProvider` instead of waiting out real seconds, and
  "nothing happens" windows became a follow-up probe message whose reply proves no earlier Turn
  ran. `BudgetExhausted_DoesNotBufferAsCatchUp` relies on pipe order for that proof, and
  `AgentGateway` sends fire-and-forget, so a reorder could let it pass wrongly — never fail
  wrongly. `Stop_InRoomA_WhileBTurnRuns_ClearsOnlyAsQueue` keeps a real 100 ms pause after the
  Stop, because nothing on the wire says the runner has handled it; it is the one timing test here
  that could still flake under load.
- **Tasks is built, and here is what it does not cover.** A Task is a Markdown file the Human
  and Agents share, and a change to one wakes its assignee — see
  [the Tasks spec](../Huddle.Tasks-Specifications.md),
  [ADR-0025](../adr/0025-in-tasks-a-team-is-a-folder-by-convention.md) and
  [ADR-0026](../adr/0026-a-change-to-a-task-wakes-its-assignee.md). Ten gaps are deliberate,
  unverified or interim:
  - **An offline assignee misses the wake-up.** The wake-up Message still posts and still sits
    in the Room's Transcript, but nothing prompts an Agent that has no running session to read
    it. It is caught up the way any Message is, on that Agent's next Turn.
  - **Startup edits wake no one.** A Task edited by hand while Huddle was stopped is logged as
    "edited outside Huddle" at the next start (D-16), but the wake-up that would normally follow
    a change is deliberately skipped — the same reasoning that keeps a bulk edit from flooding
    every assignee with wake-ups the moment the app comes up.
  - **Wake budgets reset on restart.** The per-Task budget of 10 Agent-made wake-ups (D-14,
    `Allow 10 more` on the TaskDetail banner) is in-memory state, like the Room Budget it
    parallels — a restart un-pauses every Task over a Change log that already spent one. A
    hand-off a spent budget refused is kept across a restart as a Notification on the Task. Its
    card's **Wake {Agent}** button wakes the Agent once, with the Human as the actor, and closes the
    Notification. Before a restart, `Allow 10 more` and the paused Room's Continue still deliver it.
    The counts still reset, so nothing wakes on its own after a restart; the Human decides.
  - **A request held by a spent Room Budget survives a restart as a Notification (#428).** The Room
    Budget is in memory and resets on restart. When an Agent's Message mentions another Agent in a
    Room whose Budget is spent, the Turn is refused and Huddle keeps one "held a request" Notification
    per Room and Agent (a Message that mentions two Agents makes two, so each Wake starts exactly one
    Turn). It is kept at startup only while the Room exists and is not archived, the Agent is still a
    Member and a known Persona, and the Agent has not posted in the Room since. Its **Wake {Agent}**
    button shows only when the Room is no longer paused in memory; it posts one Message from you that
    mentions the Agent and points at the held Message, then closes the Notification. Before a restart,
    **Continue** still re-delivers it and closes it. Only a mention is kept: an Agent that follows the
    Room, or a refusal for another reason, is not. Nothing wakes at startup. An Agent's Turn that posts
    nothing does not close it; a later Agent Message in the Room does.
  - **Tags can't contain `,` or `;`.** `TaskFileFormat` reuses `PersonaFrontmatter`'s list-join
    convention (`"; "`), so a tag containing either character would corrupt the round trip (D-18).
  - **A Task link reflects the index when the Message was last rendered.** `MarkdownRenderer`
    resolves an id against `TaskStore` at render time, not at read time, so a Message rendered
    before a Task existed, or before it was renamed away from a matching id, keeps whatever it
    resolved to then. Reopening the Room re-renders and catches up.
  - **A git checkout or pull marks every Task "edited outside Huddle" on next start.** A
    checkout sets every file's mtime to now, which `TaskStore`'s startup reconciliation cannot
    tell apart from a real hand edit, so it appends that entry to every Task in the repository.
    Accepted (corrections-B2 D6 item 14) rather than guarded against — there is no cheap way to
    tell a checkout from a hand edit from mtime alone.
  - **Tasks folder layout changed on 2026-09-25.** Tasks now live at
    `{DataDir}/{Team:Teams:Dir}/<Team>/[<Project>/]_tasks/[_closed/]<ID>.md`, moved from the
    interim `{DataDir}/Tasks/<Team>/...` layout by the Library effort's
    [ADR-0030](../adr/0030-a-team-folder-is-its-library-and-holds-its-tasks.md); a start-up
    migration moves any existing `{DataDir}/Tasks` folder into Team folders.
  - **`close_task` and `reopen_task`'s Conflict text is untested under a real concurrent-move
    race.** `update_task`'s Conflict arm is unreachable (`baseVersion` is always null there) and
    throws `UnreachableException`; Close and Reopen can still receive a genuine Conflict from
    `TaskStore.Move`'s disk-version check, but the real, sealed, file-backed `TaskStore` gives no
    deterministic seam to drive that race in a test, so the text stays live but unverified.
  - **Four paid manual tests have not yet been run:** TASKS-10, TASKS-11, TASKS-12 and TASKS-15 —
    see [manual-tests/tasks.md](manual-tests/tasks.md). Deferred to the Human's own user
    acceptance testing, the same way File Changes' and Room Sessions' paid checks are.
  - **Tests seen flaky under full-suite load during this delivery.** `AgentProfileCardTests.EditMode_TitleWrittenAsABlockScalar_LocksTheTitleBoxReadOnly`
    and `AgentProfileCardModelEffortTests.ViewMode_ShowsTheChosenModel` (both then in `AgentProfileCardTests`) are the fifth flake above, fixed
    2026-09-24. `RoomSessionPoolTests.Max2_ConcurrentOpens_NeverExceedCap` and
    `RoomSessionResumeTests.TurnStoppedAfterActivity_StillStoresEntry` are the same fix, watched
    rather than re-diagnosed. `PipeEndToEndTests.Disconnect_CleanClose_LogsExactlyOneInformationLine`
    is the fourth flake above, fixed 2026-09-29. `AgentsPageTests.AgentsPage_AAgentInTwoTeams_AppearsUnderBothHeadings`
    failed twice under full-suite load and was 5/5 green alone each time — **fixed 2026-09-29**, and
    it was not load at all: it counted the bare substring `Amy` in the raw `/agents` HTML, and the
    Blazor prerender marker's random base64 contains `Amy` about once in a hundred runs. It now
    counts the name element (`class="agents-item-name">Amy<`); 3/288 failing before, 0/288 after.
- **Sprints is built, and here is what it does not cover.** A Sprint groups Tasks in time, with a name and no dates, state or screen — see
  [the Sprints spec](../Huddle.Sprints-Specifications.md) and
  [ADR-0037](../adr/0037-a-sprint-is-a-named-group-of-tasks-in-one-markdown-file.md). Four gaps are deliberate:
  - **A removed Sprint's name survives only in Change log lines.** When the last Task leaves a Sprint, its line is removed from `sprints.md` and the
    Sprint vanishes from dropdowns and filters. A Task that held that Sprint shows its Change log entry `sprint: S2 (Auth hardening) → S1 (Release prep)`,
    but the Sprint name is gone unless another Task still holds it.
  - **A hand-deleted Sprint line leaves its Tasks pointing at a missing id.** If you edit `sprints.md` directly and delete a line, Tasks with that Sprint
    id show a warning in the field and are still filtered/grouped by the missing id. Nothing rewrites the Task files.
  - **A Sprint whose last Task file was deleted by hand stays in the list.** Removal runs only when a Task change vacates a Sprint, never on a
    reload, so a Task file deleted outside Huddle, or a missing Team folder, removes nothing. The line stays until you edit it out of `sprints.md`.
  - **A removal racing a save of the same Sprint can leave `S2 (missing)`.** If one change vacates `S2` while another save sets it, within a few
    milliseconds, the Task can end up holding an id that is no longer in the file. Type the name again to fix it.
- **The installer and desktop mode are built, and these are the deliberate gaps.** See
  [installer.md](installer.md) for what they do.
  - **No in-app Quit button.** Stop the app with `huddle stop`, or on Windows close the console
    window the launcher opened. A Quit button is roadmap item 24. Closing the browser tab does not stop
    the app. `stop` waits up to 45 seconds; `-Force` / `--force` ends a process that has hung.
  - **No code signing.** The installers are fetched by `irm` / `curl`, which add no download
    mark to the files, so Windows SmartScreen and macOS Gatekeeper do not prompt. The binaries
    themselves are unsigned (macOS ones are ad-hoc signed by the installer). Downloading the archives
    in a browser and opening them by hand would show the prompts.
  - **The Intel Mac and ARM64 Windows builds are tested only on CI runners.** The `Release`
    workflow smoke-tests them on `macos-15-intel` and `windows-11-arm` (the latter a GitHub public
    preview); neither has been run on real hardware. See [installer.md](installer.md#releasing-a-new-version).
  - **`latest` can need a retry.** `SHA256SUMS` and the archive are two requests. A release
    published between them fails the hash check; run the installer again.
  - **The PowerShell installer leaves a little behind after `irm | iex`.** It removes its own
    functions and variables, but one compiled helper type for the PATH broadcast stays in that
    session until the window is closed.
  - **Adapter shutdown is untested against a real Persona.** Persona runners now stop
    concurrently, and a runner that fails no longer skips the others. The smoke tests start no
    Persona (that would spend money), so a stop with live adapters has not been proved: if a
    `node` process from the install folder survives a stop, the upgrade step cannot replace
    `node` until it is gone.
  - **macOS below 15 is refused.** .NET 10 supports macOS 15, 26 and 27; macOS 14 reached end
    of life on 2026-09-14. Lower `MIN_MACOS_MAJOR` in `install.sh` if that proves too strict.
  - **Linux is not supported.**
- **Global Search is built, and here is what it does not cover.** The Room jump needs the whole Transcript in the DOM: a Room view that is ever paged or virtualised must load the target's page first ([the spec](../Huddle.GlobalSearch-Specifications.md), section 9.1). `Ctrl+K` in Edge is checked by GLOBALSEARCH-02; if the address bar takes the key, `Ctrl+Shift+K` is the fallback (section 5.2). Search keeps no index of its own, so a Library walk that reaches `Search:MaxVisitedEntries` (5000 by default, assumed, OP-2) stops and says `Results may be incomplete.`
- **Tabs is built, and here is what it does not cover.** Open Tabs are remembered per browser (two browser tabs on the app share one list, and the last to change wins); the strip scrolls sideways (a Tab is 14rem wide, the same as the widest) and does not scroll the active Tab into view; removing the very last Team leaves its Tab open (an empty Team catalog is read as "not loaded yet"); and editing a Task or saving a setting does not make a preview Tab permanent. See [the Tabs spec](../Huddle.Tabs-Specifications.md) section 8. Version 2 adds one more limit: there are at most two groups (`TabService.MaxGroups`), there is no drag to reorder or to move between groups, and a page that navigates itself while it sits in the unfocused pane (a Room redirecting after it is archived) moves the focused pane, because the router has one location (section 9, ADR-0046).

- **Needs You is in memory, and here is what that costs.** The tracker is a singleton keyed by Room id ([ADR-0050](../adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md), [the design](../Huddle.NeedsYou-Specifications.md)). After a restart it is rebuilt only from Transcripts, so a Question or Form that was waiting is gone (the stores are in memory too), and **a dismissal is forgotten**: a Room you dismissed comes back if its last unanswered Mention is still within the last 50 Messages. An unanswered Mention older than 50 Messages is not rebuilt. Only an `@` Mention of the Human, a Question, a Form or a spent Budget counts: an Agent's plain reply in a one-to-one Room does not.
- **Leaving a Team does not leave its Team Room.** No code removes a Room Member today (`ITeamDirectory` has no such call), so an Agent who leaves a Team stays a Member of that Team's Team Room, and keeps receiving its Messages. Nothing is posted when this happens ([ADR-0052](../adr/0052-a-room-belongs-to-the-root-or-to-one-team.md), [the design](../Huddle.TeamChats-Specifications.md) T4).
- **An existing Project folder named `Uploads` stops being a Project.** `Uploads` is reserved in a Team's folder for files shared in its chats, so `TeamsNav`, `TaskStore` and `TeamCatalog` no longer list a Project of that name. Its files and Tasks stay on disk, and a Project named *Uploads* cannot be created again.
- **Teams are keyed by name, so renaming a Team folder in Explorer archives its Rooms.** A Team's Rooms are found by its name ignoring case. Renaming the folder makes the old name disappear from the catalog only when no Agent still carries that name in its `teams:` line; then every Room scoped to it is archived, with its history. The new name gets a new Team Room, and the old Rooms stay under the old name; unarchiving one shows it with *(old name)* after it.
- **Team Memory in a Team chat covers every Team the Persona is in.** A Persona in a Team's chat still gets the Team Memory of every Team it belongs to (`DotAcpPersonaHost.BuildOptions`), not only the Room's Team. Narrowing it to the Room's Team is a later decision.
- **An Agent who has never connected joins its Team Room only after it first connects.** Huddle creates an Agent's User row on its first connection, so an Agent that was hired but never started is not yet in the Team Room. It is added on the reconciler's next run after that.
- **Deleting a Team Room brings it back.** The delete publishes a Room change, and the reconciler recreates an empty Team Room for any Team that still has a member. To stop it, archive the Team Room instead: an Archived Team Room is left alone ([the design](../Huddle.TeamChats-Specifications.md) D-8, D-10).
- **A reload leaves uploaded files behind with no Message (U13).** Removing a chip deletes its saved file, but a reload with chips pending loses the chips and keeps the files in the Room's folder. Nothing sweeps them; the Human removes them from the Library, and a Room's delete recycles them with its folder. See [the ChatUploads spec](../Huddle.ChatUploads-Specifications.md) §2 and [ADR-0053](../adr/0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md).
- **An Adapter without file tools cannot open an attachment (U19).** The Agent gets the path line of a file the Human attached, such as `brief.pdf`, and cannot read it: the same limit as any Library path the Adapter cannot open. See [the ChatUploads spec](../Huddle.ChatUploads-Specifications.md) §2.
- **Later edits to an uploaded file are not announced.** Team Uploads folders are pruned from File Changes scans and the Watched Folder resolver refuses to watch an Uploads folder, so an edit or delete of an uploaded file, by an Agent or the Human, reaches no Agent. The chat Message is the only notice. See [the ChatUploads spec](../Huddle.ChatUploads-Specifications.md) §6.8.
- **Unarchiving a Room of a vanished Team lasts until the next restart.** The reconciler archives a vanished Team's Rooms once, and remembers only in memory which ones it archived. A Room the Human unarchives stays in Chats, but a restart archives it again; a Team that returns after a restart does not get its Team Room unarchived.
- **Nordic follows its mockup, except where a Huddle rule wins.** The Nordic skin ([ADR-0058](../adr/0058-a-theme-is-a-palette-a-skin-and-traits.md), which replaced ADR-0057's style pack) keeps these on purpose:
  - the TextPrimary focus ring, not the mockup's blue ring;
  - the type scale, so 17px titles are 16px and 11.5, 12.5 and 13.5px text snaps to 12 and 13px;
  - the 760px Transcript column;
  - the 48, 240 and 280px shell widths, not 52, 268 and 304px;
  - 32px sidebar rows, not 30px;
  - 28px small buttons, not 26px;
  - spacing snapped to the 4px scale;
  - times, counts and row meta in ink-3 rather than the mockup's ink-4, which is TextDisabled at 3.74:1 on the dark ground;
  - the avatar colour recipe;
  - a 2px Primary edge on the active Tab;
  - a visible Line on rules that carry meaning.

  Nordic's faint 1.31:1 hairline is used only for region splits. A one-line field is 33px rather than the mockup's 34px, because the next padding step is off the spacing scale. The mockup's archetype glyphs and priority bars are not drawn.
- **Only the first Huddle to start moves its old Skills into a shared Skill Sources folder.** The move from the old `SkillPacks/` runs only while `SKILLS.md` does not exist. A second Huddle pointed at a folder another one already filled skips it: its Agents keep their copies in `Skills/`, but those Skills' catalog entries show **Not downloaded** on that computer and never **Update available**, until an AI tool adds a path for it (the row's **Copy fix prompt**). Accepted because the copy in `Skills/` is what an Agent uses ([the Skill Packs design](../Huddle.SkillPacks-Specifications.md) §6.13).
- **A computer whose name is one letter cannot have a computer line in `SKILLS.md`.** `E: C:\x` reads as a plain entry that is not a path (`R003`), because a computer name is 2 to 15 characters and a drive path such as `E:\x` must never look like one. The JSON object form `{ "E": "C:\\x" }` works, and the move writes a plain path with a warning when `Team:ComputerName` is one letter ([the Skill Packs design](../Huddle.SkillPacks-Specifications.md) §8.2).
- **A file linked into a Task is not tracked.** The Task keeps only the path line in its description. If the file is renamed or moved in the Library, the Preview dims the link, and nothing updates the Task. Deleting a path line leaves the uploaded file in `Uploads/Tasks/<ID>/`. See [TaskAttachments](../Huddle.TaskAttachments-Specifications.md).
- **A bind beyond loopback is plain HTTP.** `tailnet`, `lan`, `auto` in a container and `custom` serve `http`, which Huddle
  labels downgraded: nothing is encrypted and the page can be altered in transit. Tailscale Serve is the encrypted route.
- **Strict local means no secret on this computer.** It is on by default. Any program or other user account on the
  computer can use Huddle without signing in, which is the trust Huddle had before remote access existed. OpenClaw keeps
  a token on loopback; Huddle chose not to. Host filtering and the Origin check still stop DNS rebinding. Turn Strict
  local off in Settings › Remote access to require the token again. While it is on, Settings can show a pending Bind or
  Tailscale change, because the running options are forced to loopback and off.
- **There is no read-only device.** A device is Limited (`operator.read` and `operator.write`) or Admin. Read-only is
  backlog H-2 ([backlog.md](backlog.md#hardening-remote-access)).
- **Bind and Tailscale changes need a restart.** Settings › Remote access shows "Restart Huddle to apply" until then.
- **A proxy that does not forward headers looks like loopback.** Without `X-Forwarded-For` a local reverse proxy
  reaches Huddle from `127.0.0.1`, so the classifier sees loopback. List the proxy in `Team:Gateway:TrustedProxies` and use
  `Auth:Mode = trusted-proxy`.
- **Funnel and the real-phone Tailscale flows are tested by hand only.** They need a second device and a tailnet, so
  no automated test covers them ([manual tests](manual-tests/remote-access.md)). The live host-list refresh, the
  tailnet address lookup and the crash kill that stops an orphaned `tailscale serve` child are manual too (a test pins
  only that `KillOnParentExit` is set).
- **On macOS a Huddle crash can orphan the `tailscale serve` child.** `ProcessStartInfo.KillOnParentExit` is supported
  on Windows and Linux only, so on macOS the foreground child, and the route it holds, outlive a crashed Huddle until
  it is killed by hand. A clean shutdown still kills it.
- **The `allowed_origins_required` Security item cannot fire yet.** It depends on `GatewayRuntimeView.BareNameHostInUse`,
  which always returns `false` today, so a bare machine name that needs `AllowedOrigins` is not detected.
