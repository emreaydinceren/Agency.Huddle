# UX issues

An audit of `src/Huddle.App` against [design-system.md](design-system.md),
[wireframes.md](wireframes.md), [MudBlazorDesign.md](../../agents/MudBlazorDesign.md) and
[MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md), taken on 2026-10-13
at `e7c9b89`, and worked through by the UX consistency delivery
([UX-Issues-Plan.md](UX-Issues-Plan.md)). It lists where the UI still does one thing two ways,
where code is copied instead of shared, and where it breaks a written rule.

It is a backlog, not a decision record. Each row says what was found, where, and a
suggested fix; fixing one is a normal change that also deletes its row.

The delivery closed 148 of the 152 rows the audit found and deleted them. The rows below are the
ones it did not close, each with a note saying what is left. The rules these rows measure against
are in [design-system.md](design-system.md); where a row's suggested fix and design-system.md
disagree, design-system.md wins. Start from [UXDesignGuidelines.md](UXDesignGuidelines.md) for the
rest of the UI resources. Since the delivery, the convention test
(`tests/Huddle.Tests/Ui/Conventions`) fails the build on a new violation of the machine-checkable
rules (CV1 to CV11), so those rows cannot reopen.

**Priority:** **P1** is visible to every user or breaks an accessibility rule. **P2** is
visible drift between screens. **P3** is internal hygiene. Paths are relative to
`src/Huddle.App/`. Container codes such as `C8` or `T7` are from
[wireframes.md](wireframes.md).

## Summary

| Area | Count | Rows |
| --- | --- | --- |
| [Inconsistencies](#1-inconsistencies) | 1 | UX-071 |
| [Code reuse](#2-code-reuse-opportunities) | 1 | UX-R22 |
| [Pattern violations](#3-pattern-violations) | 1 | UX-V14 |
| [Documentation drift](#4-documentation-drift) | 0 | |
| [Round-3 defects](#5-defects-found-in-the-round-3-visual-audit) | 17 | UX-E1 to UX-E17 (all closed: E1 to E16 in round 3, E17 decided 2026-10-17) |

## 1. Inconsistencies

| ID | P | Issue | Evidence | Suggested fix | Left to do |
| --- | --- | --- | --- | --- | --- |
| UX-071 | P3 | Pill chips have three paddings and font sizes | `.teammate-chip` `0.125rem 0.5rem` at 0.75rem; the chip cloud and Avatar Library `0.2rem 0.7rem` at 0.8125rem; `.prompts-field-placeholder` `0.25rem 0.5rem` with a 3px radius | `MudChip Size.Small`, or one chip class | The chip cloud is now the one pill (`0.25rem 0.75rem`, body2) and `.teammate-chip` is gone. `.prompts-field-placeholder` (`app.css`) still has its own `0.25rem 0.5rem` padding and radius |

## 2. Code-reuse opportunities

| ID | P | Issue | Evidence | Suggested fix | Left to do |
| --- | --- | --- | --- | --- | --- |
| UX-R22 | P3 | Repeated CSS blocks | Dotted reference links, room strip rows, group headings and the first/last-child resets are merged into `.ref-link`, `.room-strip`, `.group-heading`. `.status-text`, `.panel-card` and `.preview-block` are defined in `app.css` but no markup uses them | `.status-text`, `.panel-card` (or `MudPaper Outlined`), `.preview-block` | Move the status-text lines, the bordered panel cards and the preformatted preview blocks onto the three classes, or delete the classes |

## 3. Pattern violations

| ID | P | Issue | Evidence | Suggested fix | Left to do |
| --- | --- | --- | --- | --- | --- |
| UX-V14 | P2 | Status colours come from `TaskColors.cs` or a sibling pure function, never a call site | Needs You, the search notices and `TeammateCard` use `StatusColors`; both priority chips use `TaskColors`. `Shared/Composer.razor` colours its upload chip inline (`UploadState.Failed ? Color.Error : Color.Default`) | Pure mapping functions beside `TaskColors` | Add a `StatusColors.For(UploadState)` overload and use it in `Composer.razor` |

## 4. Documentation drift

None left. wireframes.md, language.md and the `app.css` root-size comment were corrected by the
delivery.

## 5. Defects found in the round-3 visual audit

Spotted in passing during the round-3 visual audit (16 Oct 2026, E1 to E12 in the audit's Part E).
They are not style drift; each was visible in the running app. Round 3 closed E1 to E16 and a later decision closed E17 (the column below says how); none is open.

| ID | P | Issue | Evidence | Suggested fix | Left to do |
| --- | --- | --- | --- | --- | --- |
| UX-E1 | P1 | The Human's name reads "YouYouYouYou" | Right sidebar, change log, Room system rows, Settings › Profile. The stored profile Name reads that way; older change-log entries read "YouYou" and "YouYouYou", so something appends "You" | Find what appends "You" and stop it; repair the stored Name | Closed, no code defect: diagnosed as a UI-automation artifact (typing appended to the pre-filled name, then saved through Settings › Profile). The Human resets the name by hand in Settings › Profile |
| UX-E2 | P2 | The document title stays "All Tasks" | The Tab says "Platform board" or "Task PLAT-0003" | Set the page title from the Tab's title | Fixed in round 3 (`r3/tasks`): the document title follows the Tab ("Task PLAT-0003" on an item route, the View's name otherwise) |
| UX-E3 | P2 | The New task draft shows "-0000:" before a title exists | Tasks › New task | Show a neutral placeholder until the draft has an ID and a title | Fixed in round 3 (`r3/tasks`): a draft shows no "-0000" ID and opens with the title field focused |
| UX-E4 | P3 | Grace and Lena each appear in two Teams | Teammates. If that is real, an "Also in Support" note beats a duplicate card | One card with an "Also in" note | Closed as by design, not a bug: a Teammate can be in two Teams (ADR-0031, the Engineering Guide's `teams` field, the User Guide, manual test TEAMMATESLIBRARY-05). The optional "Also in Support" note was not built; a product call for the Human |
| UX-E5 | P2 | The Model field shows "Reading the models this agent offers…" in a disabled-looking box with no spinner | Edit teammate | A progress indicator in the field while it loads | Fixed in round 3 (`r3/teams`): the select stays enabled and a thin progress bar sits under it while the list is read |
| UX-E6 | P2 | Each Hide switch is labelled "HideHide" | Settings › Library | Fix the label | Fixed in round 3 (`r3/set`): the label reads "Hide" and the add row is aligned |
| UX-E7 | P3 | `/settings/team-types` shows Profile under a "Team-types" crumb; the real tab `/settings/teamtypes` has the crumb "Teamtypes" | Settings URLs | Make the route, the tab and the crumb agree, with a readable crumb | Fixed in round 3 (`r3/set`): `/settings/team-types` redirects to `/settings/teamtypes`, and Settings pages show no breadcrumb |
| UX-E8 | P2 | The menu stays open above the scrim when it opens a dialog | Every menu that opens a dialog | The `SHR` idiom, applied by each menu's owner | Fixed in round 3 (`r3/shr` idiom, applied by `r3/shell`, `r3/tasks`, `r3/lib`, `r3/teams`): `MenuDialogs.AfterClosingAsync` closes the menu before the dialog opens |
| UX-E9 | P1 | Checked switches draw as unchecked | Tasks › Fields | Bind the switch to the stored value | Fixed in round 3 (`r3/tasks`): the Fields dialog binds each switch to the stored value, with display names and accessible names |
| UX-E10 | P1 | Close task closes at once and wakes the assignee, who may open a new Room for the wake | Task drawer › Close task | Confirm before closing, or say what the wake does | Fixed in round 3 (`r3/tasks`): Close task… asks first ("Close PLAT-0003?"); there is no undo snackbar |
| UX-E11 | P2 | The User Guide and the app disagree about the right sidebar | The empty sentence differs; the guide said three Library panels where there are two; the app's sentence promises Task panels, and a Task shows none | One sentence: "Nothing to show for this page." | Fixed in round 3: the guide and the app both read "Nothing to show for this page." (`r3/rside`) |
| UX-E12 | P2 | MudBlazor's disabled colours are fixed black alphas, near-invisible on a dark Theme | Dialogs | Take the disabled colours from the Theme | Fixed in round 3 (`r3/fnd`): disabled colours follow the Theme |
| UX-E13 | P3 | Archived chats shows "Created <date>", not "Archived <date>" | Archived chats dialog | Record when a Room was archived and show it | Fixed in round 3 (`r3/open-items`): the `archived_rooms` row already held the instant, so no schema change; `Room.ArchivedAt` reads it and the dialog shows "Archived Oct 3", falling back to "Created …" when it is unreadable |
| UX-E14 | P3 | The Move dialog shows raw Teammate-ID folder names | Library › Move | Look up the Teammate's display name for the folder | Fixed in round 3 (`r3/open-items`): a Teammate folder reads "Name · Title" from the Persona store, with the folder name kept in the tooltip |
| UX-E15 | P3 | The New teammate dialog still scrolls inside on a short window | Teammates › New teammate | Let the dialog's content fit, or move the persona text to its own step | Fixed in round 3 (`r3/e15`, then `fix/new-teammate-fit` after the running app still scrolled by about 40px): Title and Alias share one row with one-line helpers ("Shown beside the name.", "For @mentions."), and Model, Effort and Work mode are one row of equal selects (`.teammate-card-row`) with no helper text of their own, so nothing wraps in a one-third column. One Quiet-ink line under the row (`.teammate-card-row-helper`) carries "Changing any of these restarts the teammate, which clears what it remembers." (or the in-flight "Reading ..." note); each select's own explanation, and the Work mode's description, is its `title`. The blank option reads "Agent default" so it shows in full. Create and Edit share the markup; the content now lands near 650px of the 734px available at 1440x900, and a very short window still scrolls the dialog body |
| UX-E16 | P3 | The B7b scrollbar is left native | Teammates | Style it with the rest of the app's scrollbars | Fixed in round 3 (`r3/open-items`): the role and roster grids' scrollbar is thin and shows (in `--ink-edge`) only on hover or focus-within |
| UX-E17 | P2 | 17 status-chip colours in ported VS Code Themes stay under 4.5:1, even after the chip text moved from 70% to 50% toward Ink (Light Modern's High chip was 3.68:1 at 70%). The 50% mix also mutes every chip in the Huddle Themes | Status chips, ported Themes | Decide between a per-Theme chip-ink recipe and recording the 17 as documented shortfalls; review the muted look in Huddle Light and Dark | Decided 2026-10-17: keep the 50% mix; the 17 are documented shortfalls in `ThemeCatalogTests` (`DocumentedShortfalls`, pair `chip:<status>`), listed in [theme-authoring.md](theme-authoring.md) |

## See also

- [design-system.md](design-system.md): the rules this page measures against, and its own
  "Known gaps" section.
- [wireframes.md](wireframes.md): the container codes used above.
- [rules.md](rules.md): the binding rules for `src/Huddle.App`.
- [language.md](language.md): the words for interface copy.
