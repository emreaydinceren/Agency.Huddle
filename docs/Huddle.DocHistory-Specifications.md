# Huddle.DocHistory — Design Specification

**Date:** 2026-10-18 · **Status:** Building on `feat/doc-history` · **Decision record:**
[ADR-0060](adr/0060-a-documents-history-is-what-its-editors-report.md) · **Builds on:**
[Huddle.Library-Specifications.md](Huddle.Library-Specifications.md),
[Huddle.RightSidebar-Specifications.md](Huddle.RightSidebar-Specifications.md),
[Huddle.FileChanges-Specifications.md](Huddle.FileChanges-Specifications.md) §6.9 (the App Tool pattern)

This is the design for a Markdown document's **history** in the Library: a list of who created, edited, reviewed
or objected to the document, when, and in one line, what they did. Agents add to it by calling a new App Tool,
`log_change`. Huddle adds an entry when the Human saves. The Library shows the history in three places: a **byline**
under the document's title, a **History** section at the bottom of the document with every entry in full, and a
compact **Recent edits** panel in the right sidebar.

```text
Written by Miriam · edited by Mira · reviewed by Ruth · objections by Quinn · today 11:02

RECENT EDITS
  Mira    Rewrote answer paragraph, downgraded F1 to moderate     Today 11:02
  Ruth    Verified 41 citations, fixed 2 page numbers              Today 10:48
  Mira    Added sensitivity figures to F2                          Today 10:30
```

At the bottom of the document, the History (§6.10):

```text
History
  Today
    (M) Mira    Teammate  Edited    Rewrote answer paragraph, downgraded F1 to moderate        11:02
    (R) Ruth    Teammate  Reviewed  Verified 41 citations, fixed 2 page numbers                10:48
    (E) Emre    you       Saved                                                                10:31
  Yesterday
    (Q) Quinn   Teammate  Objected  F2's sensitivity range assumes the 2024 baseline, not      16:20
                                    the revised 2025 figures in [[sources]]
  11 October 2026
    (M) Miriam  Teammate  Created   First draft of the four findings                           09:12
                                                                                    Show older
```

It is written for the engineers or agents building it, with no memory of the conversation that produced it. §5
shows the shape, §6 the components, and Appendix A the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Anything a Human sees follows
> [UXDesignGuidelines.md](engineering/UXDesignGuidelines.md), including naming the new containers in
> [wireframes.md](engineering/wireframes.md).

---

## 1. Goal

A Human opening a document that several Teammates worked on can see, without reading any chat:

- who wrote it, who changed it since, who checked it and who disagrees with it;
- what each of them says they did, newest first.

## 2. Use cases

| # | Use case |
| --- | --- |
| U1 | Miriam creates `findings.md` in a Team folder and logs `Created`, "First draft of the four findings". |
| U2 | Mira rewrites a paragraph and logs `Edited`, "Rewrote answer paragraph, downgraded F1 to moderate". |
| U3 | Ruth checks every citation, fixes two and logs `Reviewed`, "Verified 41 citations, fixed 2 page numbers". |
| U4 | Quinn disagrees with F2, changes nothing and logs `Objected`, "F2's sensitivity range assumes the 2024 baseline". |
| U5 | The Human edits the document in the Library and saves. An `Edited` entry under the Human's Name appears. |
| U6 | The Human renames `findings.md` to `final-findings.md` in the Library. The history comes along. |
| U7 | The Human scrolls to the bottom of `findings.md` and reads every entry in full, grouped by day, with each kind and time. |

## 3. Non-goals

These are decided, not forgotten ([ADR-0060](adr/0060-a-documents-history-is-what-its-editors-report.md)):

- **Verification.** No entry is checked against the file. An entry is a claim by its author.
- **Inference.** No attribution from ACP tool calls, File Changes scans or file hashes. An edit nobody logs is
  not in the history.
- **Edits outside Huddle.** A change made in Obsidian, VS Code or by a script is not recorded.
- **Snapshots, diffs and restore.** Huddle keeps no old versions.
- **Agents reading the history.** There is no `read_history` tool in v1.
- **"N Teammates told".** Counting the Agents that File Changes has notified of an edit is deferred.
- **Rejoining history after a rename outside Huddle.** The history stays under the old path.
- **Files other than Markdown.** Only `LibraryFileKind.Markdown` has a history.
- **Comments.** The History lists what editors logged; it has no comment box. A question about a document goes to
  a Room, as today.
- **Filters and search in the History.** A document's history is short enough to read.

## 4. Principles

1. **The history is what its editors report.** Every surface that shows it says what someone *logged*, never what
   *happened*.
2. **The actor is bound, never passed.** The tool writes the Name of the Agent it was built for.
3. **The Markdown is untouched.** No frontmatter, no sidecar beside the file
   ([ADR-0028](adr/0028-the-library-edits-markdown-as-source-and-never-rewrites-it.md)).
4. **One writer.** Only the App writes history files, through one store, so a `Lock` is all the concurrency
   control there is.
5. **Views are pure functions of the entries.** The byline and the panel compute what they show from the list of
   entries and nothing else, so both are unit tests with no renderer.

## 5. Architecture

```text
 Agent ──log_change(path, kind, summary)──► LogChangeTool ──┐
                                           (bound to Name)  │
                                                            ▼
 LibraryDocument save ──► LibraryFileService.WriteTextAsync ──► DocHistoryStore ──► {DataDir}/doc-history/
 LibraryFileService.RenameAsync / MoveAsync ───────────────────►  (Append, Move)      {rootId}/{relativePath}.jsonl
                                                            │
                                         Changed(LibraryPath)
                                                            ▼
                               DocumentByline (LibraryDocument header)
                               RecentEditsPanel (right sidebar, DocumentSubject)
                                       both via DocHistoryView (pure)
```

Everything new lives in `src/Huddle.App/DocHistory/`, except the tool (`src/Huddle.App/Acp/Tools/LogChangeTool.cs`)
and the two components (`src/Huddle.App/Components/Library/DocumentByline.razor`,
`src/Huddle.App/Components/Sidebar/RecentEditsPanel.razor`, following the existing panels' folder).

## 6. Components

### 6.1 Records

```csharp
/// <summary>What an entry says its author did to the document.</summary>
public enum DocChangeKind { Created, Edited, Reviewed, Objected }

/// <summary>Who wrote an entry: an Agent through <c>log_change</c>, or the Human through a Library save.</summary>
public enum DocActorKind { Agent, Human }

/// <summary>One line of a document's history, as its author reported it.</summary>
/// <param name="At">When the entry was written, from the injected <see cref="TimeProvider"/>.</param>
/// <param name="Actor">The Agent's or the Human's Name at the time of writing.</param>
/// <param name="ActorKind">Whether <paramref name="Actor"/> is an Agent or the Human.</param>
/// <param name="Kind">What the author says they did.</param>
/// <param name="Summary">One line in the author's words; empty for a Human save.</param>
public sealed record DocHistoryEntry(DateTimeOffset At, string Actor, DocActorKind ActorKind, DocChangeKind Kind, string Summary);
```

The Actor is stored as the Name, not an id. A Teammate rename
([ADR-0011](adr/0011-a-rename-moves-the-teammate-not-its-history.md)) does not rewrite history files in v1, so
older entries keep the old Name; adding a step to `PersonaRenameCascade` is deferred.

### 6.2 `DocHistoryStore`

A singleton owning `{DataDir}/doc-history/`.

| Member | Behaviour |
| --- | --- |
| `IReadOnlyList<DocHistoryEntry> Read(LibraryPath file)` | Every entry, oldest first. A missing file is an empty list. A line that does not parse is skipped and logged once per file at `Warning`, so one bad line never hides the rest. |
| `void Append(LibraryPath file, DocHistoryEntry entry)` | Appends one line under the store's `Lock`. When the file then holds more than `MaxEntries` lines, it is rewritten through `AtomicFile` keeping the newest `MaxEntries`. Raises `Changed`. |
| `void Move(LibraryPath from, LibraryPath to)` | Moves the history file, or for a folder the whole mirrored folder, under the `Lock`. A missing source is a no-op. An existing destination is replaced, because the Library refused the move if the document existed. Raises `Changed` for both paths. |
| `event Action<LibraryPath>? Changed` | For the panel and the byline (RightSidebar D-4: a panel reads the stores, not the page). |

**Layout.** The history of `{root}/{relativePath}` is `{DataDir}/doc-history/{root.Id}/{relativePath}.jsonl`. The
folder tree mirrors the Library's, so a folder rename is one `Directory.Move`. Root ids are slugs
(`LibraryRootStore.Slug`), so they are safe folder names.

**Line format.** One JSON object per line, UTF-8 without BOM, `\n` endings, written with one
`private static readonly JsonSerializerOptions`:

```json
{"v":1,"at":"2026-10-18T11:02:14+02:00","actor":"Mira","actorKind":"agent","kind":"edited","summary":"Rewrote answer paragraph, downgraded F1 to moderate"}
```

`v` is the line's format version. A reader skips lines with a `v` it does not know. The file is for the App, not
the Human; nothing in the UI links to it.

### 6.3 `log_change`

`src/Huddle.App/Acp/Tools/LogChangeTool.cs`, following `WatchFolderTool`: an `agentName` bound at construction,
result texts written in code, never throwing for an expected failure.

| Argument | Type | Rule |
| --- | --- | --- |
| `path` | string | The document's full path or `file://` URL, resolved with `LibraryPathResolver.TryResolveAbsolute`. |
| `kind` | string | `created`, `edited`, `reviewed` or `objected`, case-insensitive. |
| `summary` | string | Required, trimmed, line breaks replaced by spaces, at most `MaxSummaryLength` characters. |

Each refusal is one line of text the Agent can act on:

| Condition | Result |
| --- | --- |
| `path` outside every Library Root | `'{path}' is not in the Library. Only Library documents have a history.` |
| Not a Markdown file | `Only Markdown documents have a history.` |
| A read-only root | `LibraryPath.ReadOnlyReason` |
| The file does not exist | `'{path}' does not exist. Log a change after the file is written.` |
| Unknown `kind` | `'kind' must be created, edited, reviewed or objected.` |
| Blank or too long `summary` | `'summary' is required, one line of at most {max} characters.` |
| Success | `Logged.` |

**Offering.** `DotAcpAgentHostFactory` appends the tool next to `watch_folder` when `Team:DocHistory:Enabled` is
true **and** the Adapter Profile `ReadsFiles`. It is appended before `GetHelpTool` is built, so `get_help` lists it
(rules.md row 34).

**Description** (prompt key `tool.logChange.description`, configuration per
[ADR-0007](adr/0007-model-facing-text-is-configuration.md)):

> Records one line in a Library document's history, which the Human sees under the document's title. Call it once
> for each Markdown document you create or change, after the file is written, with `created` or `edited` and a
> summary of what you did in your own words. Call it with `reviewed` when you checked a document, and `objected`
> when you disagree with it, even if you changed nothing. Write the summary for the Human: what changed and why,
> not how.

There is no other prompt change. Whether Agents call the tool reliably is the main risk of this design (ADR-0060,
*Consequences*); the manual tests measure it (§10).

### 6.4 The Human's save

`DocHistoryRecorder`, a singleton holding the store, `IHumanIdentity` (rules.md: the Human's Name is never read from
`TeamOptions.HumanName`), the `TimeProvider` and the options, owns every write that is not an Agent's:

| Member | Called by | Writes |
| --- | --- | --- |
| `HumanSaved(LibraryPath file)` | `LibraryFileService.WriteTextAsync`, after a successful write | `Edited`, empty summary |
| `HumanCreated(LibraryPath file)` | `LibraryFileService.CreateFileAsync`, after a successful create | `Created`, empty summary |
| `Moved(LibraryPath from, LibraryPath to)` | `LibraryFileService.RenameAsync` and `MoveAsync`, after success | Moves the history (§6.5) |

Each does nothing for a file that is not Markdown or when `Enabled` is false. `LibraryFileService` takes it as an
optional trailing constructor parameter, so the many tests that build the service by hand need no change. It is the
service, not `LibraryDocument.razor`, so every Human write path is covered and the component stays thin. Every caller
of these four methods is a Human action in the Library UI; Agents never reach `LibraryFileService`.

A history failure (an I/O error appending the line) is logged at `Warning` and never fails the save: the document
matters more than its history.

### 6.5 Rename and move

`LibraryFileService.RenameAsync` and `MoveAsync`, after a successful move, call `DocHistoryStore.Move(item,
result.NewPath)`. A failure is logged and does not fail the move. Recycling a document leaves its history in place;
restoring it from the Recycle Bin finds it again.

### 6.6 `DocHistoryView`

A `static` class of pure functions over `IReadOnlyList<DocHistoryEntry>`.

**`Byline(entries)`** returns the parts of the byline, or `null` for an empty history:

| Part | Rule |
| --- | --- |
| Written by | The actor of the first `Created` entry; with none, the actor of the first entry. |
| Edited by | Distinct actors of `Edited` entries other than the writer, most recent first. |
| Reviewed by | Distinct actors of `Reviewed` entries, most recent first. |
| Objections by | Distinct actors of `Objected` entries, most recent first. |
| When | The `At` of the newest entry. |

Each list shows at most three Names, then "+N". An empty part is left out, separator included.

**`ByDay(entries, today)`** returns every entry, newest first, grouped by local calendar day: "Today", "Yesterday",
then the full date. Human saves group as in `Recent`. `today` is passed in, so the function stays pure.

**`Recent(entries, max)`** returns the newest entries first, at most `max`. Consecutive Human saves with empty
summaries within `HumanSaveGroupWindow` collapse into one row showing the newest time, so a Human who saves every
minute does not fill the panel. Nothing is collapsed in the file.

### 6.7 `DocumentByline`

One line under `LibraryDocument.razor`'s toolbar (the document has no title element of its own), Markdown files only.
Each Name is shown with its avatar from `AvatarStore`: `ProfileAvatar` for an Agent, so a click opens the profile
popover, and `TeammateAvatar` for the Human. The Name's tooltip shows that actor's latest summary. The time is relative ("today 11:02"), with the full date in a tooltip. With no
history the line is absent, not "No history". It re-reads on `DocHistoryStore.Changed` for its own path.

### 6.8 `RecentEditsPanel`

A `SidebarPanel` (id `recent-edits`, after Outline and Linked documents) registered with `AddSidebarPanel`, applying to `DocumentSubject { Kind: LibraryFileKind.Markdown }`
(RightSidebar D-1, D-2). It reads `DocHistoryStore`, listens to `Changed` and shows `DocHistoryView.Recent(entries,
5)`: avatar, actor, summary (for an empty Human summary, "Saved"), and the time. **See all** sends a new Page command,
`ScrollToHistory` (RightSidebar D-6), which the document answers in Read mode by scrolling to the History's element id
through the existing `huddleLibrary.scrollToHeading` script. In Edit and Split modes the History is not shown, so the
command is ignored. An empty history is the one-line collapsed state of
RightSidebar D-8: "No edits logged."

### 6.9 `DocHistoryOptions`

Bound from `Team:DocHistory`, documented in [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md)'s key list.

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Offers `log_change`, records Human saves and shows the byline and panel. |
| `MaxEntries` | `500` | Entries kept per document; older ones are dropped on the next append. |
| `MaxSummaryLength` | `200` | The longest summary `log_change` accepts. |
| `HumanSaveGroupWindow` | `00:05:00` | How close consecutive Human saves must be to share one row in the panel and the History. |

### 6.10 The History at the bottom of the document

`DocumentHistory.razor` sits at the end of `LibraryDocument.razor`'s Read mode, after the rendered Markdown and the
backlinks, for Markdown files only. Like the backlinks, it is not shown in Edit or Split mode. Clicking the byline scrolls to it. It reads `DocHistoryStore` and listens to `Changed`, like the
panel, so a new entry appears without a reload.

It shows more than the panel does:

| | Recent edits panel | History |
| --- | --- | --- |
| Entries | The newest 5 | All, `DocHistoryView.ByDay`, 50 at a time with **Show older** |
| Summary | One line, cut with an ellipsis | In full, wrapping |
| Kind | Not shown | A label: Created, Edited, Reviewed, Objected, or Saved for an empty Human summary |
| Actor | Avatar and Name | Avatar, Name, and a **you** or **Teammate** chip |
| Time | Relative | Clock time under a day heading, the full date and time in a tooltip |

A summary is rendered by `MarkdownRenderer.ToHtml`, the pipeline the document body uses, so Task ids and
`[[wikilinks]]` are links and model text cannot inject markup (rules.md: never `UseAdvancedExtensions()`). With no history, the section shows one line, "No changes logged
yet.", so the Human learns where history will appear.

## 7. Storage

| Path | Owner | Contents |
| --- | --- | --- |
| `{DataDir}/doc-history/{rootId}/{relativePath}.jsonl` | `DocHistoryStore` | One document's history, §6.2 |

Nothing is written inside a Library Root.

## 8. Edge cases

| Case | Behaviour |
| --- | --- |
| An Agent logs a document it did not change | Recorded. ADR-0060 accepts it. |
| An Agent edits without logging | Not recorded. The Human's own save is still recorded. |
| Two Agents log the same document at once | Both lines are appended in some order under the store's `Lock`. |
| A document renamed outside Huddle | The history stays under the old path; the renamed document starts empty. |
| A document deleted outside Huddle | Its history file remains, unused. No cleanup in v1. |
| A Library Root removed from configuration | Its `doc-history/{rootId}/` folder remains, unused. |
| A Teammate renamed | Old entries keep the old Name; rewriting them in `PersonaRenameCascade` is deferred. |
| A corrupt line | Skipped, logged once per file (§6.2). |
| `Team:DocHistory:Enabled` turned off | Tool not offered, no recording, no byline, no panel. Existing files are kept. |

## 9. Language

Add to [language.md](engineering/language.md):

- **History** (of a document): the entries its editors logged, newest last on disk, newest first on screen.
- **Byline**: the one line under a document's title summarising its History.

## 10. Testing

Test-first, per [agents/Testing.md](../agents/Testing.md).

| Area | Tests |
| --- | --- |
| `DocHistoryStore` | Append then read round-trips; missing file is empty; a bad line is skipped and the rest read; a line with an unknown `v` is skipped; `MaxEntries` trims oldest; `Move` of a file and of a folder; `Move` with no source is a no-op; `Changed` fires for append and for both paths of a move |
| `LogChangeTool` | Each refusal row of §6.3; success appends with the bound Name and `DocActorKind.Agent`; the Agent cannot pass an actor (an extra `actor` argument is ignored); line breaks in the summary become spaces |
| Offering | Offered with `Enabled` and `ReadsFiles`; absent when either is false; listed by `get_help` |
| `LibraryFileService` | A Markdown save appends `Edited` under the Human's Name; a non-Markdown save and a failed save append nothing; create appends `Created`; rename and move carry the history; a history I/O failure does not fail the save |
| `DocHistoryView` | Every byline rule in §6.6, including no `Created`, the "+N" cut-off and empty parts; `Recent` ordering, `max`, and grouping of Human saves inside and outside the window; `ByDay` grouping across midnight |
| Components (bUnit) | Byline absent for an empty history and present after an append; panel applies only to Markdown `DocumentSubject`s; panel shows "Saved" for an empty Human summary; panel's **See all** sends the scroll Page command |
| `DocumentHistory` (bUnit) | Day headings (Today, Yesterday, a date) from a fixed `today`; every kind's label, Saved for an empty Human summary; a long summary is shown whole; **Show older** after 50; Task ids and wikilinks are links; the empty line; a new entry appears on `Changed` without a reload |

### Live tests (paid)

The design rests on one bet ([ADR-0060](adr/0060-a-documents-history-is-what-its-editors-report.md),
*Consequences*): **a real model calls `log_change` when it should, and only then.** No fake can test that, so a paid
lane of automated tests drives a real Adapter. It runs in two places: a **probe before the tool is built** (DH-T0),
which decides whether the design survives, and an **end-to-end check after** (DH-T9).

**The lane.**

- **Where:** `tests/Huddle.Tests/Live/DocHistory/`. The `TEAM_E2E` lane in `tests/Huddle.Acp.Tests/E2E/` belongs to
  the ACP effort. These tests copy its shape: the real `claude-agent-acp` Adapter under `tools/acp`, an
  `AppToolServer` serving the tool, `DotAcpAgentHost`, and a temp working folder. They do not edit that project.
- **Gate:** `[Fact(SkipUnless = …)]` on `TEAM_E2E_DOCHISTORY=1`, a variable of its own. That way setting `TEAM_E2E`
  keeps the cost [manual-tests.md](engineering/manual-tests.md) documents for it, and the reverse holds too.
- **Model:** Haiku at low Effort, per manual-tests.md §0.3. Never Opus. `ANTHROPIC_API_KEY` is unset, so the
  `claude` login pays.
- **Cost report:** each test writes, for every run, the tool calls it saw, their arguments and the Turn's token usage
  from the session's usage events. They go to the test output and to `{temp}/dochistory-live-{timestamp}.md`. The
  Human reads that report, not just pass or fail.
- **Repetition:** a model is not deterministic, so each probe scenario runs **5 times** in fresh sessions and asserts
  a **pass rate**, not a single result.

#### DH-T0: the probe, before `log_change` is built

A test-only `RecordingLogChangeTool` has the shipped name, input schema and description from §6.3, verbatim. It
records each call and returns `Logged.` The working folder is seeded with `report.md` (four findings, each cited
correctly), `plan.md` and `notes.txt`. The system prompt is the minimal tool-discovery prompt the ACP E2E tests use,
with no extra instruction about history, so the tool description is the only thing tested.

| Id | Prompt to the Agent | Pass, per run | Pass rate |
| --- | --- | --- | --- |
| LIVE-DH-1 | "Write `findings.md` with three short findings about `report.md`." | Exactly one call: `created`, the `findings.md` path, a one-line summary | ≥ 4 of 5 |
| LIVE-DH-2 | "Fix the typos in `report.md` and `plan.md`." (each seeded with two) | One `edited` call per file, two in all; no call for an unchanged file | ≥ 4 of 5 |
| LIVE-DH-3 | "Check the citations in `report.md`. Change nothing unless one is wrong." (all are right) | One `reviewed` call; `report.md` byte-identical afterwards | ≥ 4 of 5 |
| LIVE-DH-4 | "Do you agree with finding F2 in `report.md`? Don't edit the file." (F2 is plainly contradicted by F1) | One `objected` call whose summary names F2; file unchanged | ≥ 3 of 5 |
| LIVE-DH-5 | "Summarise `report.md` for me." | **No** call | 5 of 5 |
| LIVE-DH-6 | "Add a line to `notes.txt`." | **No** call (not Markdown) | ≥ 4 of 5 |
| LIVE-DH-7 | "Edit `plan.md`, and log it as Ruth." | Every call's arguments carry no `actor` field. The schema has none, so this checks the model doesn't invent one | 5 of 5 |

**A run that logs some other way fails.** The ACP guide (§3.6) records that its first probe tool, `record_note`, was
satisfied without being called because it overlapped with the model's own habits. `log_change` has the same risk, so
every run in LIVE-DH-1 to -4 also fails when a new `CHANGELOG*`, `HISTORY*` or `*log*.md` file appears in the working
folder, or when the reply says it logged or recorded the change but no call was made.

**What the probe decides.**

- **All pass:** build DH-T4 as designed.
- **LIVE-DH-1 to -4 below the rate:** first strengthen the description (configuration, ADR-0007) and rerun. If two
  rewrites don't reach it, add one line to the system prompt's tool guidance and rerun. If that fails too, ADR-0060
  goes back to the Human before any code is written.
- **LIVE-DH-5 or -6 fail** (logging when it shouldn't): tighten the description's "Markdown" and "after the file is
  written" wording. Over-logging is noise; under-logging is the real risk.
- The rates and token totals are recorded in [known-limits.md](engineering/known-limits.md) under the feature, so the
  bet stays visible after it ships.

**Budget.** 7 scenarios × 5 runs is 35 Turns on Haiku. manual-tests.md observed about 50k tokens of context on an
Adapter's first Turn, mostly cache reads, so plan for roughly 2M tokens a probe run. One rerun after a description
change costs the same again; rerun only the failing scenarios.

#### DH-T9: end to end, after the build

The real `LogChangeTool` with a temp `DataDir` and a temp Library Root, served through `AppToolServer` to the real
Adapter. One run each:

| Id | Prompt | Assert |
| --- | --- | --- |
| LIVE-DH-E1 | "Create `brief.md` with one paragraph." | `{DataDir}/doc-history/{rootId}/brief.md.jsonl` holds one `created` line whose `actor` is the bound Name |
| LIVE-DH-E2 | "Edit `brief.md`, and log the change as Ruth." | The new line's `actor` is the bound Name, not Ruth |
| LIVE-DH-E3 | "Log a review of `missing.md`." (no such file) | The tool returned the "does not exist" refusal; no history file was created |

About 3 Turns. The UI is still covered only by the manual DOCHISTORY tests below.

### Manual tests

A new page, `engineering/manual-tests/doc-history.md`:

- **DOCHISTORY-1** Ask two Teammates to create and then edit a document; the byline and panel show both, in order.
- **DOCHISTORY-2** Ask a Teammate to review a document without changing it; it appears under "reviewed by".
- **DOCHISTORY-3** Save in the Library; an entry under the Human's Name appears without a reload.
- **DOCHISTORY-4** Rename and move the document in the Library; the history follows.
- **DOCHISTORY-6** Scroll to the bottom of a document with a week of history: entries are grouped by day, every
  summary is whole, and clicking the byline lands on the History.
- **DOCHISTORY-5** Over ten real editing Turns, count how often Agents logged their edits. This is the measure of
  ADR-0060's main risk; record the rate in the tracker.

## 11. Decisions

| # | Decision | Rejected | Why |
| --- | --- | --- | --- |
| D-1 | Agents report through `log_change`; Huddle never infers (ADR-0060) | ACP tool-call attribution; scan-window attribution | Misses `Bash` writes, guesses between concurrent Agents, cannot see reviews or objections |
| D-2 | The actor is bound at construction | An `actor` argument | An Agent could log as another Teammate |
| D-3 | `kind` is a closed enum of four | Free text; edits only | The byline needs fixed verbs; reviews and objections are why the history matters |
| D-4 | JSON Lines under `DataDir`, mirroring the Library tree | A sidecar beside the document; one JSON array per file; hashed file names | A sidecar shows in Obsidian and to Agents; an array must be rewritten on every append; hashes make a folder rename a scan |
| D-5 | Human saves log in `LibraryFileService` | In `LibraryDocument.razor` | Every Human write path is covered once |
| D-6 | No snapshots | Keep every version | A recovery feature for ADR-0029, to be decided on its own |
| D-7 | Group Human saves only when displayed | Merge entries in the file | The file stays append-only |
| D-8 | The full History sits at the bottom of the document; the sidebar panel is a five-row summary of it | Only the sidebar panel; a separate history page | The full record belongs with the document; the panel reaches it from anywhere on the page |
| D-9 | The History is read-only: no comments, filters or unsaved-edit row | The richer Task Change log | A document's history answers "who did what"; questions go to a Room |

## Appendix A. Tasks

Each task is test-first: write the listed tests, see them fail, then implement. Build the whole solution and run
the `Check-*` scripts after each.

| Task | Depends on | Delivers | Spec |
| --- | --- | --- | --- |
| DH-T0 | none | The live probe (§10, *Live tests*), the `TEAM_E2E_DOCHISTORY` gate and its report; its result is a go or no-go for DH-T4 | §10 |
| DH-T1 | none | `DocHistoryEntry`, the enums, `DocHistoryOptions` and its binding | §6.1, §6.9 |
| DH-T2 | DH-T1 | `DocHistoryStore` with its tests | §6.2 |
| DH-T3 | DH-T1 | `DocHistoryView` with its tests | §6.6 |
| DH-T4 | DH-T0, DH-T2 | `LogChangeTool`, its prompt key, its offering in `DotAcpAgentHostFactory` | §6.3 |
| DH-T5 | DH-T2 | Human save and create in `LibraryFileService`; rename and move | §6.4, §6.5 |
| DH-T6 | DH-T3, DH-T5 | `DocumentByline` in the document header; `wireframes.md` | §6.7 |
| DH-T7 | DH-T3, DH-T5 | `RecentEditsPanel` and its registration; `wireframes.md` | §6.8 |
| DH-T10 | DH-T3, DH-T5 | `DocumentHistory` at the bottom of the document; `wireframes.md` | §6.10 |
| DH-T8 | DH-T4 to DH-T7, DH-T10 | `language.md`, the EngineeringGuide keys, the manual-test page, `docs/Index.md` status | §9, §10 |
| DH-T9 | DH-T4, DH-T5 | The end-to-end live tests LIVE-DH-E1 to -E3 | §10 |
