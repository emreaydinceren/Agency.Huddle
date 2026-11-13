# Huddle.Seeder — Specification

**Date:** 2026-10-30 · **Status:** §1–§8 describe the seeder as built on `main` at `f092ecd0`;
§9 (the Home and Family Team) was built the same day · **Owner:** chat · **Code:** `src/Huddle.Seeder` (`huddle-seed`)
· **Tests:** `tests/Huddle.Tests.Domain/Seeder/`

This page says what `huddle-seed` builds and what every seed it builds must guarantee. It is
written for whoever changes the seeder next, whether a person or an agent. Change the
requirement here first, then the test that pins it, then the code.

This page covers what the seeder produces and why. It does not cover how to seed a data folder by
hand, or the on-disk formats of Agents, Tasks, Views and transcripts. Those are in
[DataSeeding.md](../agents/DataSeeding.md), which also lists, feature by feature, what
`software-co` holds.

> [!IMPORTANT]
> The seeder is C#. [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) applies to every
> file in it, and `dotnet build Huddle.slnx` must stay green.

---

## 1. Changing the seeder

Every requirement on this page has an id (`SEED-…`) and names the test that pins it. A change
goes through four steps, in this order:

1. **Edit the requirement row here.** Add a row, change its wording, or mark it *Removed*,
   with the reason in the [change log](#11-change-log).
2. **Change or add the test** named in *Pinned by*, and watch it fail.
3. **Change the scenario or writer code** until it passes.
4. **Update [DataSeeding.md](../agents/DataSeeding.md)** if what a tester finds in the seed
   changed, such as its feature table or the manifest.

A row whose *Pinned by* is `—` has no test yet. Treat it as a gap to close, not as permission
to drift.

**Links run both ways.** The spec names its tests and files, and the code names the spec:

- Every seeder type's `<summary>` ends with `Seeder spec §N` and the requirement ids it serves,
  for example `Seeder spec §4 (SEED-SAFE-1 to -5).` on `SeedRootGuard`.
- Every test that pins a requirement starts its `<summary>` with the ids, for example
  `SEED-SC-9: Six root Rooms…`.

When you add or move a requirement, update both ends in the same commit. To find a
requirement's code, search for its id with `rg "SEED-SC-9" src tests`.

> [!TIP]
> This page changes often, so it carries its own map. Before you open the seeder's code, read
> [Appendix A](#appendix-a-where-to-change-what). It names the file for each kind of edit, the
> tests that edit trips, and the traps already hit. Keep it current: an edit that taught you
> something about the code adds a line there in the same commit.

## 2. Goal and non-goals

**Goal.** One command turns an empty folder into a complete, realistic Huddle data folder.
"Overdue", "due soon" and "five minutes ago" stay true whatever day it runs. It costs nothing
to run and nothing to browse.

**Non-goals.**

- **It does not start Huddle.** It writes files and calls Huddle's own data API in-process.
- **It does not define file formats.** It reuses the app's writers wherever it can, and the
  tests compare every format it has to copy with the app's own reader.
- **A scenario is not a fixture for unit tests.** Tests read a seed back through the app's
  loaders. They do not assert on prose.
- **It does not seed real people or real data** (see [SEED-INV-12](#6-invariants-every-scenario-holds)).

## 3. Command line

```powershell
dotnet run --project src\Huddle.Seeder -- --scenario software-co --today 2026-10-30
```

| Id | Requirement | Pinned by |
| --- | --- | --- |
| SEED-CLI-1 | Options are `--scenario <name>` (case-insensitive; default `software-co`), `--root <folder>` (default `<system drive>\seeds\huddle\<scenario>`) and `--today yyyy-MM-dd` (default: today's UTC date). | `SeedCliTests.Parse_NoArguments_UsesDefaults`, `Parse_AllOptions_AreApplied` |
| SEED-CLI-2 | Exit codes: `0` success or help, `1` refused or failed run, `2` bad arguments: an unknown option, an unknown scenario, a missing value, or a malformed date. | `Parse_BadInput_ReturnsExitCodeTwo`, `Run_RefusedRoot_ExitsOneAndLeavesTheFolderAlone` |
| SEED-CLI-3 | `-h`, `--help` and `/?` print usage. Usage says the root is wiped and lists every scenario with its one-line description. | `Parse_Help_ExitsZeroAndMentionsTheWipe` |
| SEED-CLI-4 | A successful run prints the counts it seeded, the path of `manifest.md`, and a launch command that runs Huddle on the mock adapter. | `Run_FreshRoot_SeedsAndPrintsTheLaunchCommand` |

## 4. Safety of the seed root

The seeder deletes its root before writing. These rules make sure that root can never be
something that matters.

| Id | Requirement | Pinned by |
| --- | --- | --- |
| SEED-SAFE-1 | The root is wiped only when it is absent, empty, or holds the `.huddle-seed` marker the seeder writes. | `SeedRootGuardTests.Check_AbsentFolder_IsAllowed`, `Check_EmptyFolder_IsAllowed`, `Check_NonEmptyFolderWithMarker_IsAllowed`, `Check_NonEmptyFolderWithoutMarker_IsRefused` |
| SEED-SAFE-2 | A drive root and a folder one level below it are always refused. | `Check_DriveRoot_IsRefused`, `Check_OneLevelBelowDriveRoot_IsRefused` |
| SEED-SAFE-3 | A root inside a protected tree (the repository, the user profile), or one that contains a protected folder, is refused. | `Check_InsideProtectedTree_IsRefused`, `Check_ContainingProtectedFolder_IsRefused`, `Check_InsideProtectedAncestorOnlyFolder_IsAllowed` |
| SEED-SAFE-4 | A link inside a marked root is refused rather than followed. | `Check_LinkInsideMarkedRoot_IsRefused` |
| SEED-SAFE-5 | A refused root is left untouched, and the run exits `1`. | `Wipe_UnmarkedNonEmptyRoot_ThrowsAndKeepsTheFiles`, `SeedCliTests.Run_RefusedRoot_ExitsOneAndLeavesTheFolderAlone` |
| SEED-SAFE-6 | A rerun starts from a clean slate, `team.db` included. | `SeedRoundTripTests.Rerun_StartsFromACleanSlate` |

## 5. What a run writes

`SeedRunner.RunAsync` takes these steps in order. The order matters: Tasks need Room ids from
the database, and Feed items need both.

| Step | Writes | Notes |
| --- | --- | --- |
| 1. Wipe and mark | `<root>/.huddle-seed`, `<root>/data/` | [§4](#4-safety-of-the-seed-root) |
| 2. Layout marker | `data/Agents/.layout-migrated` | Without it, Huddle's one-time migration moves every non-Task note under `Teams/` into `Agents/_unsorted/` |
| 3. Database | `team.db`, `profile.json`, Room transcripts | Through `SqliteTeamDirectory` and `FileChatStore`, never through raw SQL |
| 4. Feed | Feed items, Helper usage and status in `team.db` | `FeedSeeder` |
| 5. Folders | `Teams/<Team>/<Project>/` | Projects with no Tasks still get their folder |
| 6. Agents and Skills | `Agents/<Name>/<Name>.md`, `Skills/<name>/SKILL.md` | `AgentFileWriter` |
| 7. Tasks | Task files, with their modified time set to the last Change log entry | Otherwise Huddle logs them as "edited outside Huddle" |
| 8. Notes and binaries | Library notes, memory, images, chat uploads, Task uploads | |
| 9. Sprints, history, settings | `sprints.md`, `doc-history/`, `views.json`, `avatars.json` | `sprints.md` only when a scenario has Sprints |
| 10. Manifest | `<root>/manifest.md` | Every entity with its ids and paths |

### The plan a scenario returns

A scenario is an `IScenario` (`Name`, `Description`, `Build(DateOnly today)`) listed in
`ScenarioCatalog.All`. `Build` returns a `SeedPlan`. Each property maps to one model record:

| `SeedPlan` property | Record | Becomes |
| --- | --- | --- |
| `Agents` | `SeedAgent(Name, Title, Specialty, Teams, Skills, Personality) { Gender, AvatarFile, Role? }` | An Agent file, an Agent user in `team.db`, and an avatar copied from the bundled Avatar Library ([§6.1](#61-avatars)). The alias is the lower-cased name |
| `Skills` | `SeedSkill(Name, Description, Body)` | A Skill folder |
| `Teams` | `SeedTeam(Name, Projects) { TeamType?, Manager?, Owns? }` | Team and Project folders, a `_team.md` holding the Team's avatar colour ([§6.1](#61-avatars)), and for a typed Team its manager and `Process.md` (SEED-HOME-8). Team membership lives on the Agents; `Role` writes a persona's `_role` |
| `Tasks` | `SeedTask` | A Task file under `Teams/<Team>[/<Project>][/_closed]/` |
| `Files` | `SeedFile(RelativePath, Content)` | Library notes and memory facts |
| `Rooms` | `SeedRoom(Key, Name, Members, Archived, Messages, Team, IsTeamRoom, WithReferences)` | A Room in `team.db`, plus its transcript |
| `BinaryFiles`, `Uploads`, `TaskUploads` | `SeedBinaryFile`, `SeedUpload`, `SeedTaskUpload` | Images and files in the Library, in chat upload folders, and in `Uploads/Tasks/<ID>/` |
| `DocHistory` | `SeedDocEdit` | `doc-history/…jsonl` entries |
| `Sprints` | `SeedSprint(Number, Name)` | `sprints.md`. The highest number is `@current` |
| `Feed` | `SeedFeedItem` | Feed items and Notifications |
| `ViewsJson` | raw JSON | `views.json` |
| `HumanAvatar` | the app's `Avatar` | The Human's entry in `avatars.json`. The Agents' entries come from `Agents` |

Message text can hold `{{lib:…}}`, `{{upload:…}}`, `{{room:…}}` and `{{team:…}}` tokens. The
seeder expands them and builds each Message's `References`. The token table is in
[DataSeeding.md](../agents/DataSeeding.md#tokens-in-seed-message-text).

## 6. Invariants every scenario holds

These hold for **every** scenario, including new ones. A new scenario gets a
`<Name>ScenarioTests` class with a test for each row the scenario can break, and a run of the
round-trip tests in [§7](#7-round-trip-the-seed-loads-in-the-real-app).

| Id | Requirement | Pinned by (in `software-co`) |
| --- | --- | --- |
| SEED-INV-1 | **Dates are offsets from the run date.** Days come from `--today`; Feed minutes count back from the run time, or from 15:00 UTC when `--today` is pinned. No scenario holds a literal calendar date in data the app compares with today. | — |
| SEED-INV-2 | **Overdue means at least two days late**, so a time-zone shift never turns it into "due today". | `Plan_OverdueTasksAreAtLeastTwoDaysLate` |
| SEED-INV-3 | **Task ids are unique**, use a 4-digit number, and carry the prefix the app derives from the Team name: its first four letters or digits, upper-cased. | `Plan_TaskIdsAreUniqueAndUseTheDerivedPrefix` |
| SEED-INV-4 | **Closed means finished.** A Task is in `_closed` exactly when its status is finished. The one exception is a Request that is Done but not yet confirmed, which stays open. | `Plan_ClosedMeansFinished` |
| SEED-INV-5 | **Change logs run forward and end in the past.** `LastTouchedDaysAgo` is at least 1. | `Plan_ChangeLogsRunForwardAndEndInThePast` |
| SEED-INV-6 | **References resolve.** Parents, `BlockedBy`, `DuplicateOf`, origin Rooms, Task ids in Messages, and attachment paths all exist in the plan. | `Plan_ReferencesResolve`, `Plan_RoomTranscriptsAreConsistent` |
| SEED-INV-7 | **Agent names and aliases are unique**, and none is `Chief of Staff` or `cos`. Huddle creates that built-in Agent itself (`BuiltinAgentSeeder`). | `Plan_AgentNamesAndAliasesAreUnique` (the built-in name: —) |
| SEED-INV-8 | **Transcripts are consistent.** Only the Human or a member sends, and Messages run forward in time. | `Plan_RoomTranscriptsAreConsistent` |
| SEED-INV-9 | **No token survives** into a written transcript. | `SeedRefreshTests.Tokens_NeverSurviveIntoATranscript_AndNamedRoomsCiteDocuments` |
| SEED-INV-10 | **Agents run on the mock adapter.** The printed launch command names `mock-acp`, so browsing a seed costs nothing. | `SeedCliTests.Run_FreshRoot_SeedsAndPrintsTheLaunchCommand` |
| SEED-INV-11 | **The app owns the formats.** Wherever a writer copies an app format (references, uploads, attachments, Sprints, Requests, doc history), a test compares it with the app's own function or reader. | The `SeedRefreshTests` class |
| SEED-INV-12 | **Nothing is real.** People, addresses, phone numbers and accounts are invented. Phone numbers come from ranges reserved for fiction (UK `01632 960000`–`960999`, US `555-0100`–`555-0199`). No government, bank or card number appears, real or fake-looking. | — |
| SEED-INV-13 | **Every Notification that waits is true** of the rest of the seed. Huddle resolves the others at start-up, so a false one silently disappears. | `SeedFeedTests.WaitingTaskNotifications_HoldAgainstTheSeededTasks`, `HelperState_MatchesItsWaitingFeedItems` |
| SEED-INV-14 | **Sprint names hold no dates**, because the run date moves and a name like "Week of 26 Oct" would be wrong on every other run. | — |

### 6.1 Avatars

Added 2026-10-30. Every Agent has a face, and every Team has its own colour, so a seed never opens
on a page of identical monograms.

| Id | Requirement | Pinned by `SeedAvatarTests.` |
| --- | --- | --- |
| SEED-AV-1 | **Every Agent has a picture from the bundled Avatar Library** (`src/Huddle.App/AvatarLibrary`, read through the app's `AvatarLibraryLoader`). The scenario names the file in `SeedAgent.AvatarFile`, choosing one whose gender matches the Agent's and that no other Agent in the scenario uses. | `Plan_EveryAgentNamesALibraryPicture` |
| SEED-AV-2 | **The picture's gender matches the name.** `SeedAgent.Gender` (`Man` or `Woman`) is the scenario's reading of the name, and the picture's `gender_presentation` in `master.json` must equal it. A mismatch fails the run, not just the test. | `Plan_EveryPictureMatchesItsAgentsGender` |
| SEED-AV-3 | **No two Agents in a scenario share a picture.** | `Plan_NoTwoAgentsSharePicture` |
| SEED-AV-4 | **The pick is a copy.** The seeder copies the bytes into `avatars/` under a stable name derived from the Agent and the picture's bytes (the same file on every run while the picture is unchanged, a new name when the library redraws it, because the app caches `/agent-avatars` as immutable) and writes the `avatars.json` entry through the app's `AvatarStore`, as the picker's Save does. Each Agent's background is a different colour of the app's `AvatarPalette`, chosen the same hue-spread way. | `SeedRoundTripTests.Avatars_EveryAgentsImageIsTheLibraryPicture`, `SeedRefreshTests.Avatars_ImageEntryResolvesToAFileOnDisk` |
| SEED-AV-5 | **Every Team has its own avatar colour.** Each Team gets a `_team.md` composed by the app's `TeamFile`, with an `avatarBackground` taken from `AvatarPalette`. No two Teams in a scenario share a colour, and Team colours are **spread by hue**: in `software-co` no two Teams are within 30° of each other on the colour wheel, so neighbouring shortcuts never read as the same colour. A Team with no `SeedTeam.TeamType` is type `blank`, has no manager and is `onboarded: true`, so nothing in the app reacts to it. A typed Team follows SEED-HOME-8. | `SeedRoundTripTests.Teams_EachHasItsOwnAvatarColour`, `SeedAvatarTests.Spread_KeepsTeamColoursApartInHue` |
| SEED-AV-6 | **The Human keeps a label avatar** (`ME` on `#d46002` unless the scenario sets `HumanAvatar`). | `SeedRoundTripTests.Avatars_EveryAgentsImageIsTheLibraryPicture` |

## 7. Round trip: the seed loads in the real app

These tests build a seed into a temporary root and load it back through Huddle's own loaders.
They are what turns the seeder red when the app changes underneath it.

| Id | Requirement | Pinned by `SeedRoundTripTests.` |
| --- | --- | --- |
| SEED-RT-1 | Every Agent loads without a rejection. | `Agents_AllLoadWithoutRejections` |
| SEED-RT-2 | Every Task loads and keeps its backdated Created and Updated dates. | `Tasks_AllLoadAndKeepTheirBackdatedDates` |
| SEED-RT-3 | A Task's `origin` points at its seeded Room. | `Tasks_OriginPointsAtTheSeededRoom` |
| SEED-RT-4 | Every Skill and every View loads without an issue. | `Skills_LoadWithoutIssues`, `Views_AllLoadWithoutErrors` |
| SEED-RT-5 | `team.db` holds every user and Room. Transcripts read back in order, from members only. | `Database_HoldsUsersAndRooms`, `Transcripts_ReadBackInOrderFromTheRoomMembers` |
| SEED-RT-6 | The layout marker is written, and notes stay under `Teams/`. | `Layout_MarkerIsWrittenAndNotesStayUnderTeams` |
| SEED-RT-7 | The manifest lists every seeded entity. | `Manifest_ListsTheSeededEntities` |

> [!NOTE]
> The round-trip tests build `software-co` today. Making them run against every scenario in
> `ScenarioCatalog.All` is decision [D-3](#10-decisions).

## 8. Scenarios built today

| Name | Default | What it is for |
| --- | --- | --- |
| `software-co` | Yes | **Feature coverage.** A small software company that exercises every seeded feature, so a manual tester finds each one with realistic content. |
| `uat` | No | **Manual-test cast.** Agents Ada and Bo on the mock adapter, each with an empty one-to-one Room. Used by the manual-test lane `P-LAUNCH-UAT`. |

### `software-co`

| Id | Requirement | Pinned by `SoftwareCoScenarioTests.` |
| --- | --- | --- |
| SEED-SC-1 | Exactly four Teams with two or more Projects (Platform: API v2 and Observability; Growth: Onboarding Revamp, Pricing Page and Lifecycle Emails; Support: Help Center and Incident Tooling; Home and Family: five Projects, §9), and exactly three Agents span two or more of them. | `Plan_HasFourWorkingTeamsAndSharedAgents` |
| SEED-SC-2 | Two edge Teams: Sandbox (nobody in it) and Records (people, no Projects). Records was named Archive until 2026-11-12, when the app's special "Archive" Team folder was retired. | `Plan_HasTheEdgeTeams` |
| SEED-SC-3 | Thirteen Agents: Grace, Marcus, Lena, Owen, Sofia, Dana, Alan, Maya and Ben, then Hattie, Theo, Ravi and Iris (§9.4). Alan holds the `incident-postmortem` Skill. | `Plan_HasTheAgreedCast` |
| SEED-SC-4 | Every one of the eight Task statuses, and the derived states: overdue, blocked, a blocker that is already finished, a sub-task, a Duplicate, unassigned, assigned to the Human, at the Team root and in a Project, tagged, and a description over 600 characters. | `Plan_CoversEveryTaskStatus`, `Plan_CoversTheDerivedTaskStates` |
| SEED-SC-5 | One Project with no Tasks. | `Plan_HasAProjectWithNoTasks` |
| SEED-SC-6 | Three kinds of memory: Team, Project and Agent work memory. | `Plan_HasThreeKindsOfMemory` |
| SEED-SC-7 | Library backlinks, every wikilink form, and exactly one unresolved link (`[[Legal-Checklist]]`). | `Plan_LibraryHasBacklinksAndOneUnresolvedLink`, `SeedRefreshTests.Library_OffersOutlineHeadings_AndEveryWikilinkForm` |
| SEED-SC-8 | Tags in every form the Tags panel reads, including nested tags shared across Teams. | `Plan_TaggedNotes_HaveTheExpectedTags`, `Plan_Library_HasNestedSharedTagsAcrossTeams` |
| SEED-SC-9 | Six root Rooms (two one-to-one, three groups, one archived, one custom-named) and ten Team-scoped Rooms (four Team Rooms, four Team chats, two Team one-to-ones). Each transcript has 8 to 15 Messages. | `Plan_HasTheAgreedRooms` |
| SEED-SC-10 | Views cover list, Kanban and closed scope. | `Plan_ViewsCoverListKanbanAndClosed` |
| SEED-SC-11 | Team Rooms, Message References, uploads, Task attachments, doc history, Sprints, Requests, avatars, Needs You and the Feed, as listed in [DataSeeding.md](../agents/DataSeeding.md#what-the-software-co-seed-contains-for-the-newer-features). | The `SeedRefreshTests` and `SeedFeedTests` classes |
| SEED-SC-12 | The description names the features it covers. | `Description_NamesTheCoveredFeatures` |
| SEED-SC-13 | **At least four documents** sit directly in every Team root and every Project folder, Sandbox excepted (it stays empty on purpose). A document is a `.md` or `.txt` Library note: not memory, not an image, not an upload, and not a note in a subfolder. Added 2026-10-30. | `Plan_EveryTeamAndProjectHasFourDocuments` |
| SEED-SC-14 | **Memory is plentiful.** Every Team with members has at least three Team memory facts, every Project at least one Project memory fact, and every Agent at least two facts in `Agents/<Name>/work/memory/`. Each fact is one line. Added 2026-10-30. | `Plan_MemoryCoversEveryTeamProjectAndAgent` |
| SEED-SC-15 | Documents added since 2026-10-30 never use a pinned tag (`support/incident`, `growth/research`) and link only to notes that exist, so the Tags panel counts (SEED-SC-8) and the single unresolved link (SEED-SC-7) stay as pinned. Other tags, such as `home/…`, are welcome. | `Plan_LibraryHasBacklinksAndOneUnresolvedLink`, `Plan_Library_HasNestedSharedTagsAcrossTeams` |

### `uat`

| Id | Requirement | Pinned by `UatScenarioTests.` |
| --- | --- | --- |
| SEED-UAT-1 | The cast is Ada and Bo only. | `Plan_IsAdaAndBoOnly` |
| SEED-UAT-2 | Each Agent has one empty direct Room. | `Plan_HasOneEmptyDirectRoomPerAgent` |
| SEED-UAT-3 | The catalog lists `uat` but never defaults to it. | `Catalog_ListsUatButDoesNotDefaultToIt` |

### What no scenario seeds yet

Each item below is a feature the app has that a fresh seed shows only in its empty state. A
`grep` of `src/Huddle.Seeder` on 2026-10-30 found none of them. Adding one is a new row in the
scenario that owns feature coverage, which is `software-co`.

| Feature | Its file | Why a tester would want it |
| --- | --- | --- |
| Team details (type, "What it's for", Library folder) | `Teams/<Team>/_team.md` | The Team page's Details tab opens empty |
| Process gates | `Teams/<Team>/Process.md` | Gate refusals are seeded only as Feed items, never by a real gate |
| Retrospectives | Cadence and retro register | The retro proposals flow has nothing to show |

---

## 9. The Home and Family Team

**Status:** Built 2026-10-30 in `software-co` ([D-1](#10-decisions)). Rows use the `SEED-HOME-` prefix.

### 9.1 Goal

**Show Huddle running a household as well as a company, in the same demo.** One Human opens
the seed and finds work Teams and a Home and Family Team side by side in one sidebar. Agents
carry the family's mental load: what is due this week, who owns what, what is waiting on
someone, and what the house needs next.

The Team is set up **as if it had been hired from the built-in `household` team type**. The
demo therefore also shows a typed Team: the type on the Team page and in search, a manager, a
`Process.md`, and a `_role` on every member's persona. The other Teams stay blank.

### 9.2 What it is based on

Research on 2026-10-30 into how people run a home and family "second brain" found five things
that shape this section:

- **Mental load is the problem.** The hard part is noticing, planning and remembering, not
  doing. Families reach for one shared place, a clear owner per task, a quick brain-dump
  inbox, and a weekly check-in.
- **Fair Play (Eve Rodsky)** splits household work into about 100 "cards". Each card has one
  owner, who handles it from noticing the need, through planning, to doing it. Splitting the
  planning from the doing is what fails. In Huddle, that is a Task with one assignee.
- **Home binders and Notion hubs** keep the same reference sections: emergency contacts,
  doctors and insurance, an index of where documents are, a maintenance schedule and
  contractors, a storage inventory, cleaning rotations, meal plans with a pantry and shopping
  list, and a family calendar.
- **AI "family chief of staff" products** appeared in late 2026: Fambot, Google's CC, and
  home-built versions on Claude. They read school emails into a daily plan and send a morning
  or Sunday briefing. That is an Agent posting in a Team Room.
- **Upkeep kills systems.** People drop a second brain that costs more time than it saves. The
  seeded Agents should be seen doing the filing.

### 9.3 Shape

| Id | Requirement | Pinned by `SoftwareCoScenarioTests.` unless named |
| --- | --- | --- |
| SEED-HOME-1 | One Team, **`Home and Family`** (folder `Teams/Home and Family/`; Task prefix `HOME`), with five Projects: `Kids and School`, `Meals`, `Home Maintenance`, `Family Admin` and `Holidays and Events`. | `Plan_HomeAndFamily_HasItsProjectsAndCast` |
| SEED-HOME-2 | Four Agents, Hattie, Theo, Ravi and Iris, in `Home and Family` only. None is named Chief of Staff ([SEED-INV-7](#6-invariants-every-scenario-holds)). | `Plan_HomeAndFamily_HasItsProjectsAndCast` |
| SEED-HOME-3 | The family members (Sam the partner, Mia and Leo the children, Grandma Rose) are **characters, not users**. Huddle has one Human. Sam's cards carry the Task tag `owner/sam` and stay unassigned. | `Plan_HomeAndFamily_FamilyAreCharactersNotUsers` |
| SEED-HOME-4 | The Team's Tasks use all eight statuses, and include an overdue Task, a Task due tomorrow assigned to You, a blocked Task, a parent with sub-tasks, an unassigned Task and a Human-gated Task. | `Plan_HomeAndFamily_TasksCoverEveryState` |
| SEED-HOME-5 | One Team Room, two Team chats and a Team one-to-one with Theo. The `Grandma's 70th` chat ends with an unanswered `@You` Mention, so Needs You has a household row beside `Launch crew`. | `Plan_HasTheAgreedRooms`, `SeedRefreshTests.NeedsYou_OneRoomHoldsAnUnansweredMention` |
| SEED-HOME-6 | A household fact in memory is quoted by an Agent in a transcript: Theo cites Mia's peanut allergy when he plans the week. | `Plan_HomeAndFamily_AgentQuotesItsMemory` |
| SEED-HOME-7 | Every contact, number and address follows [SEED-INV-12](#6-invariants-every-scenario-holds). The documents index says **where** a document is kept, never its number. | — |
| SEED-HOME-8 | **Set up like a hire from the `household` type, version 1.1.0.** `_team.md` has `teamType: household@1.1.0`, `manager: Hattie`, an `owns` line and `onboarded: true` (so no onboarding post). `Process.md` is the type's default process, read through the app's `TeamTypeCatalog`. Every member carries a `_role` of that version: Hattie `household-manager`, Theo `meal-planner`, Ravi `home-keeper`, Iris `schedule-keeper`. The other roles (`school-liaison`, `health-admin`, `finance-tracker`, `comparison-shopper`, `retro-agent`) are left unhired, which the process allows; with no Retro Agent the 14-day retro goes to Hattie. | `SeedRoundTripTests.Teams_HomeAndFamilyIsSetUpFromTheHouseholdType` |
| SEED-HOME-9 | The Team meets the same document and memory floors as every other Team (SEED-SC-13, -14), with no special case. | `Plan_EveryTeamAndProjectHasFourDocuments`, `Plan_MemoryCoversEveryTeamProjectAndAgent` |

### 9.4 Cast

| Agent | Title | Picture | Role and ownership |
| --- | --- | --- | --- |
| **Hattie** (woman) | Household Coordinator | `pixel-avatar_tan_f_11.png` | Manager (`household-manager`). Owns `Kids and School`, the Sunday preview and the "who owns what" list |
| **Theo** (man) | Meal Planner | `pixel-avatar_tan_m_14.png` | `meal-planner`. Owns `Meals`: menus, the pantry, the grocery list, allergies |
| **Ravi** (man) | Home Keeper | `pixel-avatar_tan_m_20.png` | `home-keeper`. Owns `Home Maintenance`: the maintenance calendar, contractors, warranties |
| **Iris** (woman) | Family Admin | `pixel-avatar_dark_f_01.png` | `schedule-keeper`. Owns `Family Admin` and `Holidays and Events`: renewals, insurance, appointments, where documents are kept |

Personalities follow the `software-co` pattern: a voice line, then *How you work*, then
*Boundaries*. Each Agent turns agreed work into a Task with one owner and records lasting
household facts as Team memory. Iris never gives financial advice: she tracks dates and
paperwork, and puts every spend to the Human through a Human-gated Task.

### 9.5 Library

Four or more documents sit in the Team root and in each Project. Some carry `home/…` tags so
the Tags panel has a household branch. None uses a pinned tag (`support/incident`,
`growth/research`) or an unresolved link.

| Folder | Documents |
| --- | --- |
| Team root | `Household-Manual.md`, `Emergency-Contacts.md`, `Who-Owns-What.md`, `Family-Calendar.md` |
| `Kids and School` | `School-Term-Dates.md`, `Clubs-and-Activities.md`, `School-Contacts.md`, `Homework-Routine.md` |
| `Meals` | `Weekly-Meal-Plan.md`, `Pantry.md`, `Family-Recipes.md`, `Grocery-List.md` |
| `Home Maintenance` | `Maintenance-Calendar.md`, `Contractors.md`, `Appliance-Warranties.md`, `Home-Inventory.md` |
| `Family Admin` | `Renewals.md`, `Documents-Index.md`, `Health-Appointments.md`, `Household-Budget-Dates.md` |
| `Holidays and Events` | `Grandma-70th-Plan.md`, `Birthdays.md`, `Summer-Holiday.md`, `Gift-Ideas.md` |

Memory: three Team facts (bin night, the Sunday check-in, the quiet hours), one per Project
(Mia's peanut allergy under `Meals`), and two per Agent.

### 9.6 Tasks

Huddle has no recurring Tasks, so a recurring job is **one Task per occurrence**: last year's
boiler service is closed, this year's is overdue. `Maintenance-Calendar.md` holds the rhythm.

| Id | Title | State it shows |
| --- | --- | --- |
| `HOME-0001` | Sign the school-trip consent form | To Do, due tomorrow, assigned to You |
| `HOME-0002` | Book this year's boiler service | To Do, overdue by three days; Ravi |
| `HOME-0003` | Boiler service, last year | Done |
| `HOME-0004` | Approve the boiler repair quote | Review, Human-gated; Ravi |
| `HOME-0005` | Renew the car insurance | To Do, blocked by `HOME-0006`; Iris |
| `HOME-0006` | Get two car insurance quotes | In Progress; Iris |
| `HOME-0007` | Plan Grandma's 70th | In Progress, parent of `HOME-0008` to `-0010`; Iris |
| `HOME-0008` | Book the venue | To Do; You |
| `HOME-0009` | Send the invitations | Backlog, blocked by `HOME-0008`; Hattie |
| `HOME-0010` | Order the cake | Backlog; Theo |
| `HOME-0011` | Plan next week's meals | Review; Theo |
| `HOME-0012` | Clear out the loft | Backlog, unassigned, at the Team root |
| `HOME-0013` | Swimming kit for Leo | To Do, unassigned, tag `owner/sam` |
| `HOME-0014` | Swimming kit (second copy) | Duplicate of `HOME-0013` |
| `HOME-0015` | Repaint the fence | Cancelled |
| `HOME-0016` | Switch to a meal-kit subscription | Rejected |
| `HOME-0017` | Fix the dripping kitchen tap | Done |
| `HOME-0018` | Book a parents' evening slot | To Do; Hattie |

### 9.7 Rooms, Views and Feed

| Item | Contents |
| --- | --- |
| **Team Room** `Home and Family` | All four Agents. Hattie posts the Sunday preview: this week's dates, what is overdue, and who owns what. |
| **Team chat** `Sunday planning` | Hattie, Theo and Iris agree the week and turn it into Tasks. Theo quotes the allergy memory. |
| **Team chat** `Grandma's 70th` | Iris and Hattie. Ends with Hattie asking `@You` to choose the venue, unanswered. |
| **Team one-to-one** `Theo` | "What's for dinner?", answered from `Weekly-Meal-Plan.md`. |
| **Views** | `Home this week` (active, sorted by due date), `Who owns what` (grouped by assignee, `tags` shown) and `Home board` (Kanban grouped by Project). |
| **Sprints** | None of its own. Sprints are global, so a household Sprint would become `@current` for every Team. |
| **Feed** | Three Tasks Hattie created from a school email (one grouped card), Ravi's edit to the maintenance calendar, the consent form assigned to You, the venue Mention, and the boiler quote's gate. |

### 9.8 Findings for the app, not the seeder

Writing this section turned up two gaps. Neither belongs in the seeder. They go to
[roadmap.md](engineering/roadmap.md) only if the Human wants them:

- **A View cannot filter by tag**, so "Sam's cards" is a column you read, not a View
  (`filter` has no `tags`, per [DataSeeding.md](../agents/DataSeeding.md#views)).
- **Tasks do not recur.** A household is mostly recurring work.

## 10. Decisions

| Id | Question | Recommendation | Alternative | State |
| --- | --- | --- | --- | --- |
| D-1 | Where does the Home and Family Team live? | A new scenario, `home-family`, would leave `software-co`'s pinned counts alone. | Add it to `software-co`, so one demo shows work and home side by side. | **Decided 2026-10-30:** in `software-co`, "all in one", so the one demo gets the most eyeballs. SEED-SC-1, -3 and -9 changed to match. |
| D-2 | The Team's name | **`Home and Family`**: the Human's own words. Spaces are legal in a Team name, and the prefix is still `HOME`. | `Home` (shorter, same prefix); `Home & Family` (would also exercise `&` in Team URLs and folder names). | **Decided 2026-10-30:** `Home and Family` |
| D-3 | Should the round-trip tests ([§7](#7-round-trip-the-seed-loads-in-the-real-app)) run against every scenario? | **Yes**, as a `[Theory]` over `ScenarioCatalog.All`, so a new scenario cannot ship without loading cleanly. | Keep one round-trip class per scenario. | **Open** |
| D-4 | Should the seeder check SEED-INV-7 and SEED-INV-12 itself? | **Yes**, a shared test over every scenario in the catalog. | Review them by eye. | **Open** |

## Appendix A. Where to change what

Notes for the next edit, so it starts from here instead of from a search of the code. Paths
are relative to `src/Huddle.Seeder/` unless they start with `tests/`. Checked against the code on
2026-10-30.

### A.1 File map

| File | Owns |
| --- | --- |
| `SeedCli.cs`, `SeederProgram.cs`, `LaunchCommand.cs` | Arguments, help text, exit codes, the printed launch command |
| `SeedRootGuard.cs`, `ProtectedFolder.cs` | What may be wiped ([§4](#4-safety-of-the-seed-root)) |
| `SeedRunner.cs` | The write order ([§5](#5-what-a-run-writes)). `WriteFolders` calls `TeamFileWriter` for each Team |
| `Model/*.cs` | One record per kind of seeded thing; `SeedPlan` gathers them |
| `Scenarios/ScenarioCatalog.cs` | The registered scenarios and the default |
| `Scenarios/SoftwareCoScenario.cs` | `software-co`: its description, `ViewsJson`, and how the parts below join into one `SeedPlan` |
| `Scenarios/SoftwareCoAgents.cs` | The nine Agents (with `Gender` and `AvatarFile`) and the one Skill |
| `Scenarios/SoftwareCoLibrary.cs` | Teams and Projects, the original notes and memory, images, uploads, Task uploads, doc history |
| `Scenarios/SoftwareCoNotes.cs` | Documents and memory added for SEED-SC-13 and -14. **Put new notes and memory here** |
| `Scenarios/SoftwareCoTasks.cs` | `Core` (41 Tasks), `Sprints`, the Sprint and former-title maps, and `All`, which appends the Requests |
| `Scenarios/SoftwareCoRequests.cs` | The five Request Tasks |
| `Scenarios/SoftwareCoHome.cs` | The Home and Family Team (§9): its `SeedTeam`, four Agents, 18 Tasks, four Rooms and Feed items. The Library and memory are in `SoftwareCoHomeNotes.cs` |
| `Writing/TeamFileWriter.cs` | Each Team's `_team.md` (blank, or typed with a manager) and a typed Team's `Process.md` |
| `Scenarios/SoftwareCoRooms.cs` | Room keys (constants) and every transcript |
| `Scenarios/SoftwareCoFeed.cs` | Feed items, and the Helper's id and name |
| `Scenarios/UatScenario.cs` | `uat`: Ada, Bo and their empty Rooms, with their own `ViewsJson` |
| `Writing/DatabaseSeeder.cs` | `team.db`, `profile.json`, Rooms and transcripts, through the app's `SqliteTeamDirectory` and `FileChatStore` |
| `Writing/AvatarWriter.cs` | Library pictures, `avatars.json`, and `Spread` (palette colours) |
| `Writing/MessageReferences.cs` | Expands `{{…}}` tokens and builds `References` |
| `Writing/TaskFileWriter.cs` | Task files and their Change log |
| `Writing/FeedSeeder.cs`, `DocHistoryWriter.cs`, `AgentFileWriter.cs` | Feed rows, `doc-history/`, and Agent and Skill files |
| `Writing/ManifestWriter.cs` | `manifest.md`, plus the helpers tests use: `Overdue`, `Blocked`, `LinksByNote`, `IsFinished` |
| `Writing/SeedIo.cs` | Text writes (UTF-8, LF, final newline) and `Resolve` for `/` paths |
| `tests/Huddle.Tests.Common/Seeder/SeededFolder.cs` | Seeds the default scenario into a temp folder on the fixed date 2026-09-29 |
| `tests/Huddle.Tests.Domain/Seeder/` | Every test this page names |

### A.2 Recipes

Each row lists the files to touch and the tests that will go red until you update them. *Exact*
means the test pins a literal list or count ([A.3](#a3-values-tests-pin-exactly)).

| To… | Touch | Tests it trips |
| --- | --- | --- |
| **Add or rename an Agent** | `SoftwareCoAgents.All`: the positional fields, then `{ Gender = …, AvatarFile = "…" }`. Pick the file from `src/Huddle.App/AvatarLibrary/master.json`: same `gender_presentation`, unused in the scenario, and different from the cast in top colour, hair and glasses where it can be (the library's low numbers in each group all wear black, so do not just take the next number). Add the Agent to Rooms and Feed actors by name. The background colour is assigned for you. | `Plan_HasTheAgreedCast` (exact), `SeedAvatarTests`, `Plan_RoomTranscriptsAreConsistent` |
| **Add a Team or Project** | `SoftwareCoLibrary.Teams`; Agents join through their `Teams` list. The colour and `_team.md` are automatic. A new folder must also get four documents and its memory (SEED-SC-13, -14). | `Plan_HasFourWorkingTeamsAndSharedAgents` (exact), `Plan_HasTheEdgeTeams`, `Plan_EveryTeamAndProjectHasFourDocuments`, `Plan_MemoryCoversEveryTeamProjectAndAgent` |
| **Add a document** | `SoftwareCoNotes.Documents`: `new("Teams/<Team>[/<Project>]/<Name>.md", """…""")`. No `#word` anywhere: a heading `# Title` is fine, `#tag` is not. A `[[Link]]` must match an existing file stem. Stems are global, so pick a new one. | `Plan_LibraryHasBacklinksAndOneUnresolvedLink`, `Plan_Library_HasNestedSharedTagsAcrossTeams` |
| **Add memory** | `SoftwareCoNotes.Memory`: one line ending `\n`, at `Teams/<T>/memory/`, `Teams/<T>/<P>/memory/` or `Agents/<N>/work/memory/`. | `Plan_MemoryCoversEveryTeamProjectAndAgent`, `Plan_HasThreeKindsOfMemory` |
| **Add a Task** | `SoftwareCoTasks.Core`. The id prefix is the Team's first four letters (`PLAT`, `GROW`, `SUPP`, `RECO`, `HOME`), and the next number is free. Dates are offsets; overdue is `DueOffset <= -2`. `Closed = true` only for a finished status. For a Sprint, add it to `SprintByTask`. | `Plan_CoversEveryTaskStatus` (exact count, 46), `Plan_TaskIdsAreUniqueAndUseTheDerivedPrefix`, `Plan_ClosedMeansFinished`, `Plan_ReferencesResolve` |
| **Add a Request** | `SoftwareCoRequests.All` (it joins `SoftwareCoTasks.All`). | `Plan_CoversEveryTaskStatus` (64), `Requests_ParseWithTheAppsRequestFields` |
| **Add a Room or Messages** | A key constant plus an entry in `SoftwareCoRooms.All`. Keep 8 to 15 Messages, sent only by members or the Human, running forward in time. Cite with `{{lib:…}}`, `{{room:key}}`, `{{team:Name}}`, `{{upload:file}}`. An unanswered `@You` outside `Launch crew` breaks Needs You. | `Plan_HasTheAgreedRooms` (exact), `Plan_RoomTranscriptsAreConsistent`, `NeedsYou_OneRoomHoldsAnUnansweredMention` (exact), `Tokens_NeverSurviveIntoATranscript_AndNamedRoomsCiteDocuments` |
| **Add a Feed item** | `SoftwareCoFeed.All`. A waiting Notification (`Notify: true` and no `ResolvedMinutesAgo`) must be true of the seed, or Huddle drops it at start-up. | `SeedFeedTests` (five grouped TaskCreated items from Owen, exact) |
| **Add a saved View** | `ViewsJson` in `SoftwareCoScenario.cs`, using the fields allowed in [DataSeeding.md](../agents/DataSeeding.md#views). | `SeedRoundTripTests.SavedViewIds` (exact list), `Plan_ViewsCoverListKanbanAndClosed` |
| **Add an image, upload or doc history** | `SoftwareCoLibrary.BinaryFiles`, `Uploads`, `TaskUploads` or `DocHistory`. | `SeedRefreshTests` (the matching test) |
| **Seed a new kind of thing** | A record in `Model/`, a `SeedPlan` property defaulting to `[]` (so other scenarios still compile), a writer in `Writing/` reusing the app's own writer, its call in `SeedRunner` in the right order, a `ManifestWriter` section, a round-trip test, and a row in [DataSeeding.md](../agents/DataSeeding.md). | A new round-trip test |
| **Add a scenario** | A class implementing `IScenario`, listed in `ScenarioCatalog.All`, plus `<Name>ScenarioTests`. `SeedAvatarTests` covers it automatically. | `SeedAvatarTests` (every scenario), `Catalog_ListsUatButDoesNotDefaultToIt` |

### A.3 Values tests pin exactly

When you change one of these on purpose, change the test in the same commit.

| Test | Pins |
| --- | --- |
| `SoftwareCoScenarioTests.Description_NamesTheCoveredFeatures` | The full description string |
| `Plan_HasTheAgreedCast` | The thirteen names in order, the Teams of Lena, Maya, Dana and Ben, and Alan's Skill |
| `Plan_HasFourWorkingTeamsAndSharedAgents` | Exactly Platform, Growth, Support and Home and Family have two or more Projects, and exactly three Agents span two or more of them |
| `Plan_HasTheEdgeTeams` | Records' members are exactly Grace and Dana |
| `Plan_CoversEveryTaskStatus` | 64 Tasks (41 core, 5 Requests, 18 Home and Family) |
| `Plan_HasTheAgreedRooms` | The six root Room names in order, the four Team Rooms, six other Team-scoped Rooms, two root one-to-ones, and the one archived Room |
| `Plan_LibraryHasBacklinksAndOneUnresolvedLink` | The only unresolved link is `Legal-Checklist` |
| `Plan_Library_HasNestedSharedTagsAcrossTeams`, `Plan_TaggedNotes_HaveTheExpectedTags` | Which notes carry `support/incident` and `growth/research`, and the exact tags of named notes |
| `SeedRoundTripTests.SavedViewIds` | The twelve View ids |
| `SeedRefreshTests.NeedsYou_OneRoomHoldsAnUnansweredMention` | Exactly `launch-crew` and `chat-home-grandma-70th` wait on You |
| `SeedRefreshTests.TeamRooms_AreScopedAsSeeded` | Four Team Rooms |
| `SeedCliTests.Run_FreshRoot_SeedsAndPrintsTheLaunchCommand` | The printed summary "Seeded 13 Agents, 6 Teams, 64 Tasks" |
| `SeedRefreshTests.Sprints_ParseWithTheAppsParser_AndTasksHoldThem` | Two Sprints |
| `SeedFeedTests` | Five TaskCreated items by Owen; the Helper has three consecutive failures |
| `UatScenarioTests` | Ada and Bo, and their two Rooms |

### A.4 Commands

```powershell
dotnet build Huddle.slnx
dotnet test tests/Huddle.Tests.Domain --no-build -- --filter-namespace "Agency.Huddle.Tests.Seeder"
dotnet test Huddle.slnx --no-build --
pwsh agents/scripts/Check-All.ps1
dotnet run --project src\Huddle.Seeder -- --today 2026-10-30
```

The seeder tests take a few seconds; the whole suite takes several minutes. Build the
**solution** before testing: test projects carry analyzers that a single-project build misses.

### A.5 Traps already hit

- **Line endings.** The repo stores LF and checks out CRLF, and `Check-Eol` wants CRLF. A file
  rewritten by a script often comes back LF. Run `pwsh agents/scripts/Check-Eol.ps1 -Fix` before
  committing. Prefer the Edit tool to scripted rewrites: Python's `re.sub` turns a `\n` in the
  replacement into a real newline, which once split a C# character literal.
- **`HAA0601` (boxing) is on in `src/`.** `new ArgumentOutOfRangeException(name, value, message)`
  boxes an enum and fails the build. Use the `(name, message)` overload.
- **`Check-Diff` refuses a string `Assert.Contains`** in a new test (`Assert.Contains("x", text)`).
  Assert the whole string with `Assert.Equal`. Collection membership is only a hint.
- **`Check-Visibility` is red on `main`** because of `src/Huddle.App/Components/Tasks/TaskDetailHeaderParts.cs`
  (commit `761b9ae6`), not because of the seeder. Do not chase it from here.
- **Editor diagnostics go stale** while the seeder's types change. Trust `dotnet build`, not the
  squiggles.
- **Huddle watches its folders.** Never seed into a data folder Huddle is running on: a write
  becomes an outside edit, Tasks log it, and Agents restart.
- **A Task file's modified time** is set to its last Change log entry. Without that, Huddle logs
  it as edited outside Huddle and the backdated Updated date is lost.
- **`Agents/.layout-migrated`** must exist, or Huddle's one-time migration moves notes out of
  `Teams/`.
- **`_team.md` needs a `teamType`** or the app ignores the whole file. The seeder writes
  `blank@1.0.0`, `onboarded: true` and no manager, so there is no onboarding post and no retro
  role. Adding a `manager:` line, together with a `Process.md` that has onboarding steps, makes
  Huddle post the onboarding prompt to that manager (`TeamOnboarding`) unless `onboarded` stays
  `true`.
- **The Avatar Library is read from `AppContext.BaseDirectory/AvatarLibrary`.** `Huddle.App`
  copies it to the seeder's and the tests' output folders as content. If it is missing, the run
  fails with "The Avatar Library at … could not be read."
- **The Human is one user.** Family members, customers and other people in a scenario are
  characters in text, never assignees or Room members.
- **`AvatarPalette.Colours` is ordered in runs of one hue** (tints of crimson, then blues, then purples)
  and repeats a few colours. `AvatarWriter.Spread` de-duplicates, then picks each next colour as far
  in hue as possible from those already chosen. Taking every n-th colour instead gave Platform and
  Growth two shades of crimson, and two of six Teams once got the same colour.
- **`Chief of Staff` / `cos` is taken** by the built-in Agent that Huddle creates itself.

## 11. Change log

| Date | Change |
| --- | --- |
| 2026-10-30 | Created. §1–§8 describe the seeder as built at `f092ecd0`. §9 proposes the Home Team, from research into home and family second brains. |
| 2026-10-30 | Added §6.1 Avatars (SEED-AV-1 to -6): library pictures matched to each Agent's gender, and a colour per Team. Added SEED-SC-13 to -15: four documents per Team root and Project, and more Team, Project and Agent memory. `SeedPlan.AvatarsJson` became `HumanAvatar`. |
| 2026-10-30 | SEED-AV-5: Team and Agent colours are spread by hue, so no two Teams look alike (Platform and Growth were both crimson, Sandbox and Archive both purple). |
| 2026-10-30 | Moved Home and Family to `household@1.1.0` (PR #281) and gave Theo, Ravi and Iris their roles (SEED-HOME-8). |
| 2026-10-30 | Decided D-1: the Home and Family Team goes into `software-co`. Rewrote §9 as built, set up from the `household` team type (SEED-HOME-1 to -9); SEED-SC-1, -3, -9, -15 and SEED-AV-5 changed to match. |
| 2026-10-30 | Decided D-2: the Team is named `Home and Family` (prefix `HOME`); §9 updated to match. |
| 2026-10-30 | Added back-links from code to spec: `Seeder spec §N` on every seeder type and test class, and the requirement ids at the start of every pinning test's summary (§1). |
| 2026-10-30 | Added Appendix A, the maintainer's map: a file map, edit recipes with the tests each trips, the values tests pin exactly, commands, and traps already hit. |
| 2026-11-12 | SEED-AV-4: the copied picture's name now includes its bytes, so a redrawn library picture gets a new URL; with a name from the Agent alone, browsers kept showing the old art from their immutable cache. Pinned by `SeedAvatarTests.Write_RedrawnPicture_GetsNewFileName`. |
