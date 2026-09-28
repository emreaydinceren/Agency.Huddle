# Huddle.Library — Tracker

This is the Kanban status of every task in [Huddle.Library-ProjectPlan.md](Huddle.Library-ProjectPlan.md), built from [Huddle.Library-Specifications.md](Huddle.Library-Specifications.md). Deliverables and tasks are exactly as the plan defines them, and each task row gives its running number (`#N`) and model tag.

| Column | Meaning |
| --- | --- |
| **Active** | Prioritised and ready for a subagent, but not started, or blocked and waiting |
| **In Progress** | A subagent is running it now |
| **Done** | It meets the Acceptance criteria in the plan. A `.i` task can't be Done until its `.t` task is |

**Update rules.** Exactly one `✔` per row. Move a task to **In Progress** when its agent is dispatched, and to **Done** only after the manager has re-run the build and tests. A `🔁` row is a retrospective checkpoint: it is Done once its row in the plan's *Retrospective log* is filled in, and no task after it may start before then. The two **[Gate G1]** tasks stay Active until the Tasks effort has moved Tasks into `Teams/<Team>/…/_tasks/`. Only the manager updates this file, with `Conversation/scripts/Set-Tracker.ps1 -Path docs/Huddle.Library-Tracker.md -Task <ids> -State <Active|InProgress|Done>`.

**Status:** 110 of 143 tasks Done · 6 of 8 retrospectives done (R7, R8 skipped) · last updated 2026-09-27.

| **Deliverable / Task** | **Active** | **In Progress** | **Done** |
| --- | --- | --- | --- |
| **D0. Delivery scaffolding** | | | |
| 0.1 Branch, facts file and brief · #1 · Haiku | | | ✔ |
| 0.2 Platform spike: Recycle Bin and junctions on `net10.0` · #2 · Sonnet | | | ✔ |
| **D1. `TeammatePaths` (a pure refactor, old layout)** | | | |
| 1.1.t Test: `TeammatePaths` in the current layout · #3 · Haiku | | | ✔ |
| 1.1.i Implement `TeammatePaths` · #4 · Haiku | | | ✔ |
| 1.2.t Characterisation: every Work Dir join · #5 · Sonnet | | | ✔ |
| 1.2.i Move the Work Dir joins onto `TeammatePaths` · #6 · Sonnet | | | ✔ |
| 1.3.i Move the Persona-file joins onto `TeammatePaths` · #7 · Sonnet | | | ✔ |
| **D2. Library types and options** | | | |
| 2.1.t Test: `LibraryOptions` and `TeamsOptions` · #8 · Haiku | | | ✔ |
| 2.1.i Implement the options · #9 · Haiku | | | ✔ |
| 2.2.t Test: root, path and location records · #10 · Haiku | | | ✔ |
| 2.2.i Implement root, path and location records · #11 · Haiku | | | ✔ |
| 2.3.t Test: document and file records · #12 · Haiku | | | ✔ |
| 2.3.i Implement document and file records · #13 · Haiku | | | ✔ |
| 2.4.t Test: `LibrarySize.Format` · #14 · Haiku | | | ✔ |
| 2.4.i Implement `LibrarySize.Format` · #15 · Haiku | | | ✔ |
| 🔁 R1 — Opus retrospective after #15, and plan update | | | ✔ |
| 2.5.t Test: `WikiLink` and `LibraryReference` · #16 · Haiku | | | ✔ |
| 2.5.i Implement `WikiLink`, `LibraryReference` and the resolver interface · #17 · Haiku | | | ✔ |
| 2.6 Register the public types · #18 · Haiku | | | ✔ |
| **D3. The Teammates layout (ADR-0031)** | | | |
| 3.1.t Test: `Acp:TeammatesDir` and the retired key · #19 · Haiku | | | ✔ |
| 3.1.i Implement `TeammatesDir` and retire `TeamsDir` · #20 · Sonnet | | | ✔ |
| 3.2.t Test: `TeammatePaths` in the new layout · #21 · Haiku | | | ✔ |
| 3.2.i Implement the new `TeammatePaths` · #22 · Haiku | | | ✔ |
| 3.3.t Test: the one-level Persona scan · #23 · Sonnet | | | ✔ |
| 3.3.i Implement the one-level scan · #24 · Sonnet | | | ✔ |
| 3.4.t Test: renaming moves the Teammate folder · #25 · Sonnet | | | ✔ |
| 3.4.i Implement the folder move · #26 · Sonnet | | | ✔ |
| 3.5.t Test: `TeammateLayoutMigration` · #27 · Sonnet | | | ✔ |
| 3.5.i Implement `TeammateLayoutMigration` · #28 · Sonnet | | | ✔ |
| 3.6.t Test: the migration runs at start-up, first · #29 · Sonnet | | | ✔ |
| 3.6.i Run the migration at start-up · #30 · Sonnet | | | ✔ |
| 🔁 R2 — Opus retrospective after #30, and plan update | | | ✔ |
| 3.7.t Test: the Chief of Staff is seeded into its folder · #31 · Sonnet | | | ✔ |
| 3.7.i Seeder follow-through · #32 · Sonnet | | | ✔ |
| 3.8.t Test: `Teams/` and `Teammates/` must not overlap · #33 · Haiku | | | ✔ |
| 3.8.i Implement `LayoutGuard` · #34 · Haiku | | | ✔ |
| 3.9 `run.ps1 -Clean` for the new layout · #35 · Sonnet | | | ✔ |
| **G1. Tasks into Team folders (added 2026-09-25)** | | | |
| G1.0 Route Task fixtures through layout helpers · #35a · Sonnet | | | ✔ |
| G1.1.t Test: `TaskLayout` maps only `_tasks/` · #35b · Sonnet | | | ✔ |
| G1.1.i Implement the `_tasks` layout · #35c · Sonnet | | | ✔ |
| G1.2.t Test: `Team:Teams:Dir` is the Tasks root · #35d · Sonnet | | | ✔ |
| G1.2.i Implement the root switch · #35e · Sonnet | | | ✔ |
| G1.3.t Test: the Team/Project scan beside notes · #35f · Sonnet | | | ✔ |
| G1.3.i Implement the targeted scan · #35g · Sonnet | | | ✔ |
| G1.4.t Test: the watcher ignores Library notes · #35h · Sonnet | | | ✔ |
| G1.4.i Filter the watcher with `AffectsTasks` · #35i · Sonnet | | | ✔ |
| G1.5.t Test: migration step 4 · #35j · Sonnet | | | ✔ |
| G1.5.i Implement step 4 · #35k · Sonnet | | | ✔ |
| G1.6 Tasks docs · #35l · Haiku | | | ✔ |
| **D4. The path boundary and Library Roots** | | | |
| 4.1.t Test: `LibraryRootStore` · #36 · Sonnet | | | ✔ |
| 4.1.i Implement `LibraryRootStore` · #37 · Sonnet | | | ✔ |
| 4.2.t Test: `LibraryPathResolver.TryResolve` (the boundary table) · #38 · Sonnet | | | ✔ |
| 4.2.i Implement `LibraryPathResolver.TryResolve` · #39 · Sonnet | | | ✔ |
| 4.3.t Test: `TryResolveAbsolute` and `TryResolveScope` · #40 · Sonnet | | | ✔ |
| 4.3.i Implement the absolute and scope entry points · #41 · Sonnet | | | ✔ |
| **D5. `TextFileCodec` and file kinds** | | | |
| 5.1.t Test: decode and encode round trips · #42 · Sonnet | | | ✔ |
| 5.1.i Implement `TextFileCodec` · #43 · Sonnet | | | ✔ |
| 5.2.t Test: file kind detection · #44 · Haiku | | | ✔ |
| 5.2.i Implement `LibraryFileKinds` · #45 · Haiku | | | ✔ |
| 🔁 R3 — Opus retrospective after #45, and plan update | | | ✔ |
| **D6. `LibraryFileService`** | | | |
| 6.1.t Test: `LibraryNames.Validate` · #46 · Haiku | | | ✔ |
| 6.1.i Implement `LibraryNames.Validate` · #47 · Haiku | | | ✔ |
| 6.2.t Test: `LibraryProtection.For` · #48 · Haiku | | | ✔ |
| 6.2.i Implement `LibraryProtection.For` · #49 · Haiku | | | ✔ |
| 6.3.t Test: `ListAsync` · #50 · Sonnet | | | ✔ |
| 6.3.i Implement `LibraryFileService.ListAsync` · #51 · Sonnet | | | ✔ |
| 6.4.t Test: `ReadAsync` · #52 · Sonnet | | | ✔ |
| 6.4.i Implement `ReadAsync` · #53 · Sonnet | | | ✔ |
| 6.5.t Test: `WriteTextAsync` · #54 · Sonnet | | | ✔ |
| 6.5.i Implement `WriteTextAsync` · #55 · Sonnet | | | ✔ |
| 6.6.t Test: `CreateFileAsync` and `CreateFolderAsync` · #56 · Sonnet | | | ✔ |
| 6.6.i Implement create · #57 · Sonnet | | | ✔ |
| 6.7.t Test: `RenameAsync` and `MoveAsync` without links · #58 · Sonnet | | | ✔ |
| 6.7.i Implement rename and move · #59 · Sonnet | | | ✔ |
| 6.8.t Test: `RecycleAsync` · #60 · Sonnet | | | ✔ |
| 🔁 R4 — Opus retrospective after #60, and plan update | | | ✔ |
| 6.8.i Implement `IRecycleBin` and `RecycleAsync` · #61 · Sonnet | | | ✔ |
| 6.9.t Test: `WindowsRecycleBin` · #62 · Sonnet | | | ✔ |
| 6.9.i Implement `WindowsRecycleBin` · #63 · Sonnet | | | ✔ |
| **D7. Team folders (the Library side of ADR-0030)** | | | |
| 7.1.t Test: `TeamFolderCatalog.List` · #64 · Sonnet | | | ✔ |
| 7.1.i Implement `TeamFolderCatalog` · #65 · Sonnet | | | ✔ |
| 7.2.t Test: `TeamFolderProvisioner` · #66 · Sonnet | | | ✔ |
| 7.2.i Implement `TeamFolderProvisioner` · #67 · Sonnet | | | ✔ |
| 7.3.t Test: the Team folder is a Watched Folder · #68 · Sonnet | | | ✔ |
| 7.3.i Implement the implicit Team watch · #69 · Sonnet | | | ✔ |
| 7.4.t [Gate G1] Test: Tasks and the Library share `Teams/` · #70 · Sonnet | | | ✔ |
| 7.4.i [Gate G1] Close the hand-off gaps · #71 · Sonnet | | | ✔ |
| **D8. Wikilinks, backlinks and rename rewriting** | | | |
| 8.1.t Test: `WikiLinkParser.Parse` · #72 · Sonnet | | | ✔ |
| 8.1.i Implement `WikiLinkParser` · #73 · Sonnet | | | ✔ |
| 8.2.t Test: `WikiLinkResolver` · #74 · Haiku | | | ✔ |
| 8.2.i Implement `WikiLinkResolver` · #75 · Haiku | | | ✔ |
| 🔁 R5 — Opus retrospective after #75, and plan update | | | ✔ |
| 8.3.t Test: `WikiLinkRewriter.Rewrite` · #76 · Sonnet | | | ✔ |
| 8.3.i Implement `WikiLinkRewriter` · #77 · Sonnet | | | ✔ |
| 8.4.t Test: `WikiLinkIndex` · #78 · Sonnet | | | ✔ |
| 8.4.i Implement `WikiLinkIndex` · #79 · Sonnet | | | ✔ |
| 8.5.t Test: rename and move rewrite links · #80 · Sonnet | | | ✔ |
| 8.5.i Wire the rewrite into rename and move · #81 · Sonnet | | | ✔ |
| **D9. Rendering and chat → file links** | | | |
| 9.1.t Test: absolute paths in chat become Library links · #82 · Sonnet | | | ✔ |
| 9.1.i Implement chat path links · #83 · Sonnet | | | ✔ |
| 9.2.t Test: wikilinks and relative links in a note · #84 · Sonnet | | | ✔ |
| 9.2.i Implement note rendering · #85 · Sonnet | | | ✔ |
| 9.3.t Test: `LibraryReferenceResolver` · #86 · Sonnet | | | ✔ |
| 9.3.i Implement `LibraryReferenceResolver` · #87 · Sonnet | | | ✔ |
| 9.4.t Test: chat messages render Library links · #88 · Sonnet | | | ✔ |
| 9.4.i Wire `MessageList` · #89 · Sonnet | | | ✔ |
| **D10. Agents are told about pasted documents** | | | |
| 10.1.t Test: the four `turn.library*` prompts · #90 · Haiku | | | ✔ |
| 🔁 R6 — Opus retrospective after #90, and plan update | | | ✔ |
| 10.1.i Add the prompts · #91 · Haiku | | | ✔ |
| 10.2.t Test: `LibraryDocumentCollector` · #92 · Sonnet | | | ✔ |
| 10.2.i Implement `LibraryDocumentCollector` · #93 · Sonnet | | | ✔ |
| 10.3.t Test: the prompt block · #94 · Sonnet | | | ✔ |
| 10.3.i Implement the prompt block · #95 · Sonnet | | | ✔ |
| 10.4.t Test: the collector reaches every Turn · #96 · Sonnet | | | ✔ |
| 10.4.i Plumb the collector and `readsFiles` · #97 · Sonnet | | | ✔ |
| **D11. The editor and styles** | | | |
| 11.1 Vendor CodeMirror 6 · #98 · Sonnet | | | ✔ |
| 11.2.t Test: `LibraryEditor` and its interop · #99 · Sonnet | ✔ | | |
| 11.2.i Implement `library-editor.js` and `LibraryEditor.razor` · #100 · Sonnet | ✔ | | |
| 11.3 The Library styles · #101 · Haiku | ✔ | | |
| **D12. The explorer** | | | |
| 12.1.t Test: `LibraryTree` · #102 · Sonnet | ✔ | | |
| 12.1.i Implement `LibraryTree` · #103 · Sonnet | ✔ | | |
| 12.2.t Test: `LibraryDocument` reading states · #104 · Sonnet | ✔ | | |
| 12.2.i Implement `LibraryDocument` reading · #105 · Sonnet | ✔ | | |
| 🔁 R7 — Opus retrospective after #105, and plan update — skipped by the Human (2026-09-27, to save tokens) | | | ✔ |
| 12.3.t Test: editing, saving and unsaved edits · #106 · Sonnet | ✔ | | |
| 12.3.i Implement editing · #107 · Sonnet | ✔ | | |
| 12.4.t Test: `BacklinksPanel` · #108 · Haiku | ✔ | | |
| 12.4.i Implement `BacklinksPanel` · #109 · Haiku | ✔ | | |
| 12.5.t Test: rename, move, delete and New Project dialogs · #110 · Sonnet | ✔ | | |
| 12.5.i Implement the file-op dialogs · #111 · Sonnet | ✔ | | |
| 12.6.t Test: Copy and freshness · #112 · Sonnet | ✔ | | |
| 12.6.i Implement Copy and freshness · #113 · Sonnet | ✔ | | |
| 12.7.t Test: `LibraryExplorer` and scopes · #114 · Sonnet | ✔ | | |
| 12.7.i Implement `LibraryExplorer` · #115 · Sonnet | ✔ | | |
| 12.8.t Test: the `/library-files` image endpoint · #116 · Sonnet | ✔ | | |
| 12.8.i Implement the image endpoint · #117 · Sonnet | ✔ | | |
| **D13. Hosts: the pane, the page and Settings** | | | |
| 13.1.t Test: the Library Pane side setting · #118 · Haiku | ✔ | | |
| 13.1.i Implement the side setting · #119 · Haiku | ✔ | | |
| 13.2.t Test: `MainLayout` hosts the pane · #120 · Sonnet | ✔ | | |
| 🔁 R8 — Opus retrospective after #120, and plan update — skipped by the Human (2026-09-27, to save tokens) | | | ✔ |
| 13.2.i Implement the pane in `MainLayout` · #121 · Sonnet | ✔ | | |
| 13.3.t Test: the `/library` page · #122 · Haiku | ✔ | | |
| 13.3.i Implement the `/library` page · #123 · Haiku | ✔ | | |
| 13.4.t Test: Settings → Library panel · #124 · Sonnet | ✔ | | |
| 13.4.i Implement the Settings panel · #125 · Sonnet | ✔ | | |
| **D14. Docs and verification** | | | |
| 14.1 `language.md`: the Library section · #126 · Haiku | ✔ | | |
| 14.2 `AgencyTeam.md`: configuration and map · #127 · Haiku | ✔ | | |
| 14.3 `mudblazor.md` and `code-map.md` · #128 · Haiku | ✔ | | |
| 14.4 Manual test script · #129 · Haiku | ✔ | | |
| 14.5 Run the manual tests · #130 · Sonnet | ✔ | | |
| 14.6 Close out: ADRs, roadmap and CI · #131 · Sonnet | ✔ | | |
