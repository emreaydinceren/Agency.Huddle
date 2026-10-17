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
| [Round-3 defects](#5-defects-found-in-the-round-3-visual-audit) | 12 | UX-E1 to UX-E12 |

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
They are not style drift; each is visible in the running app. The package named in "Left to do" owns
the fix.

| ID | P | Issue | Evidence | Suggested fix | Left to do |
| --- | --- | --- | --- | --- | --- |
| UX-E1 | P1 | The Human's name reads "YouYouYouYou" | Right sidebar, change log, Room system rows, Settings › Profile. The stored profile Name reads that way; older change-log entries read "YouYou" and "YouYouYou", so something appends "You" | Find what appends "You" and stop it; repair the stored Name | `DEF-E1` |
| UX-E2 | P2 | The document title stays "All Tasks" | The Tab says "Platform board" or "Task PLAT-0003" | Set the page title from the Tab's title | `TASKS` |
| UX-E3 | P2 | The New task draft shows "-0000:" before a title exists | Tasks › New task | Show a neutral placeholder until the draft has an ID and a title | `TASKS` |
| UX-E4 | P3 | Grace and Lena each appear in two Teams | Teammates. If that is real, an "Also in Support" note beats a duplicate card | One card with an "Also in" note | `TEAMS` |
| UX-E5 | P2 | The Model field shows "Reading the models this agent offers…" in a disabled-looking box with no spinner | Edit teammate | A progress indicator in the field while it loads | `TEAMS` |
| UX-E6 | P2 | Each Hide switch is labelled "HideHide" | Settings › Library | Fix the label | `SET` |
| UX-E7 | P3 | `/settings/team-types` shows Profile under a "Team-types" crumb; the real tab `/settings/teamtypes` has the crumb "Teamtypes" | Settings URLs | Make the route, the tab and the crumb agree, with a readable crumb | `SET` |
| UX-E8 | P2 | The menu stays open above the scrim when it opens a dialog | Every menu that opens a dialog | The `SHR` idiom, applied by each menu's owner | `SHR` sets it; each menu's owner applies it |
| UX-E9 | P1 | Checked switches draw as unchecked | Tasks › Fields | Bind the switch to the stored value | `TASKS` |
| UX-E10 | P1 | Close task closes at once and wakes the assignee, who may open a new Room for the wake | Task drawer › Close task | Confirm before closing, or say what the wake does | `TASKS` |
| UX-E11 | P2 | The User Guide and the app disagree about the right sidebar | The empty sentence differs; the guide said three Library panels where there are two; the app's sentence promises Task panels, and a Task shows none | One sentence: "Nothing to show for this page." | Guide fixed (round 3); `RSIDE` changes the app's sentence |
| UX-E12 | P2 | MudBlazor's disabled colours are fixed black alphas, near-invisible on a dark Theme | Dialogs | Take the disabled colours from the Theme | `FND` |

## See also

- [design-system.md](design-system.md): the rules this page measures against, and its own
  "Known gaps" section.
- [wireframes.md](wireframes.md): the container codes used above.
- [rules.md](rules.md): the binding rules for `src/Huddle.App`.
- [language.md](language.md): the words for interface copy.
