# Component rollout plan

How the planned components in [ComponentCatalog.md](ComponentCatalog.md) are built and adopted
by parallel owner agents, in what order, and how each step is proved. It is the plan for the
catalog's [Patterns still built by hand](ComponentCatalog.md#patterns-still-built-by-hand) and
[Inconsistencies to fix](ComponentCatalog.md#inconsistencies-to-fix-per-planned-component).
Running owners in parallel follows [DeliveryPlaybook.md](../../agents/DeliveryPlaybook.md); this
page adds only what is particular to this work.

## Contents

- What is decided
- The rhythm for every component
- The guard that stops backsliding
- Order of waves
- Files that more than one component edits
- Proving a visual change
- The owner brief
- Questions to answer before each wave

## What is decided

| Ruling | Decision |
| --- | --- |
| Naming (D4) | No prefix: `Notice`, `SegmentedControl`, `DialogFooter`. A name that would clash with HTML or MudBlazor gets a sharper word (`ActionButton`) |
| Switch or checkbox (B6) | A switch for a setting that takes effect when flipped; a checkbox for picking items or a field saved with a form |
| Segmented control text (B1) | 13 px, selected item at weight 500, on a 28 px row |
| Build order | Bottom-up build, top-down adoption; a component is built when its first real user needs it |

Rulings for each component live in its entry under
[Inconsistencies to fix](ComponentCatalog.md#inconsistencies-to-fix-per-planned-component),
next to the question they settle. An owner never decides an open `Q-` question; it stops and asks.

## The rhythm for every component

Building a component is one file; moving callers onto it is where the effort and the risk are. So
each component goes through three steps, and the third one runs in parallel.

1. **Build.** One owner, one PR. It creates the component, makes the shared CSS changes the
   catalog lists for it, adds its convention rule with a baseline (next section), moves the first
   real user onto it, and proves the visual change. The `visual` items in the catalog's fix list are
   all in this PR, because they are global CSS and cannot be split by caller.
2. **Migrate, by area.** After the build PR merges, one owner per area moves that area's callers,
   one PR each, in parallel. The areas are the ones the convention baseline already uses: `Css`,
   `Library`, `Room`, `Settings`, `Shared`, `Shell`, `Tasks` and `Teams`. A file's area comes from
   its path prefix in `UiConventionSource.AreaPrefixes` (for example `Shared/Composer` is `Room`),
   not only its folder, and each area has its own baseline file in
   `tests/Huddle.Tests.Shell/Ui/Conventions/Baseline/`. So the areas hold different files and
   different baseline files, and do not collide. Each migration PR must lower its area baseline.
3. **Close.** When every area baseline for the rule reaches zero, the rule becomes
   zero-tolerance outside the wrapper's own file, as CV13 is for chips today.

Only one component migrates in a given area at a time. Two migrations in `Tasks` edit the same
files.

## The guard that stops backsliding

The "allow what exists, block what is new" check (the catalog's D1) is already built. The
convention test `UiConventionTests` counts violations per rule per file and holds each file to a
baseline that may only shrink: `Source_HasNoViolationsBeyondItsBaseline` fails on any new one, and
`Baseline_IsNoLooserThanTheSource` fails if a fix did not lower the baseline in the same change.
CV13 is zero-tolerance only because every chip has already moved and the area baselines are empty.

So each wrapper adds one rule, `CV14` onward, that counts the raw Mud tag it replaces:

- It is a function in `UiConventionScanner`, like `Cv13`, with the wrapper's own file counted as 0.
- It is added in every place the existing rules are: `UiConventionScanner.Rules` and `Scan`,
  `UiConventionSource.InScope`, and the rule list and mutation row in `UiConventionTests` (one
  added violation overruns the baseline).
- The B1 rename also edits `UiConventionSource.CssTrackFiles`, which names
  `HuddleSegmentedControl.razor.css` by file name.
- Its starting baseline is generated, not typed: run the test once with
  `HUDDLE_WRITE_BASELINES=1`.
- `Exceptions.txt` is manager-only. An owner never edits it.

Two limits to know. A rule only sees the tag text, so it cannot tell a wrapped use from a use that
should be wrapped; each rule says which tags and attributes it counts. And the baseline files are
shared across components, so two owners in the same area regenerate the same file, which is
another reason for one migration per area at a time.

## Order of waves

| Wave | Work | Owners at once | Why this order |
| --- | --- | --- | --- |
| 1 | **B1** `SegmentedControl` pilot: rename from `HuddleSegmentedControl`, 28 px geometry, colour tokens in the component, rule `CV14`. Manager writes the B6 rule into design-system.md meanwhile | 1 | Proves the build, rule, baseline and snapshot steps on six sites before 200 are touched |
| 2 | **B2** buttons by role, with **A2** `DialogFooter` as its first user. Then migrate by area | 1, then up to 8 | The most sites (205), the widest visual change, and `base.css` is shared |
| 3 | **A1** `Notice`, then **B3** text field and **B4** select. Migrate by area, one component per area at a time | 1, then up to 8 | Smaller and more regular than buttons; B3 and B4 build in one short PR each |
| 4 | Composites: **A4** the picker, **A7** `SettingSwitch`, **A6** `PropertyList`, **A3** `RowActionsMenu` with **B5** | 2 to 3 on disjoint files | They contain the earlier parts, so they come last |
| 5 | Opportunistic: **A5**, **A8**, the browser scripts (group C); fix D2 and D3 when in those files | as convenient | Small gains; do them when passing |

A wave starts when the one before has merged and `main` is green. The first build of each wave
runs alone.

## Files that more than one component edits

Edits at a different line in the same stylesheet still conflict on a rebase and, worse, can
invalidate each other's snapshot. This is the list from the catalog's fix lists; the owner
confirms it with a search before starting.

| File | Components that edit it |
| --- | --- |
| `wwwroot/css/base.css` | A1, A3, B2, B5, and the `paper` and `hairline` blocks that restyle B1 |
| `wwwroot/css/tasks.css` | B1, B2, B5 |
| `wwwroot/css/task-form.css` | B1, B2 |
| `wwwroot/css/sidebar.css` | A3, B1 (theme variants) |
| `wwwroot/css/room.css` | A1 |
| `wwwroot/css/library.css`, `FeedPage.razor.css` | B1 |
| `wwwroot/css/shell.css` | B2 (the 2 px icon gap) |
| `tests/.../Conventions/Baseline/*.txt`, `UiConventionScanner.cs`, `UiConventionSource.cs`, `UiConventionTests.cs` | every component |

That last row is why the build step is one owner at a time, and why the waves above never put two
builds in flight together.

## Proving a visual change

Every `visual` item changes pixels on purpose, so the proof is that nothing else moved.

1. Before editing, on `main`: `agents/scripts/Css-Snapshot.ps1 -Mode Capture -Screens` into a
   folder named for the baseline.
2. After the change: the same, into a second folder.
3. `-Mode Report` with a `-Declared` file listing exactly the changes the catalog says this
   component makes. It exits 0 only when nothing undeclared moved, and it warns about a declared
   change that matched nothing.
4. The owner attaches the report's undeclared count and the declared list to the PR. A visual change
   that is not in the catalog's fix list is a question for the manager, not a thing to ship.

All four Themes restyle shared controls from outside them (`paper`, `hairline`, `card`, `flat`), so
the capture covers all eight Theme and mode pairs, not only Lumen Light.

## The owner brief

Every owner gets the same brief shape, filled in for its component. Put the exact file and line
ranges in it (regenerate them with `grep -n` when writing it, as the playbook says).

- **Goal and done:** the component, the rule, the baseline, the proof, and the PR; what "done" shows.
- **Read first:** `CLAUDE.md`, the component's entry and its Inconsistencies subsection in the
  catalog, `design-system.md`, `agents/MudBlazorImplementation.md`, and `UXDesignGuidelines.md`.
- **Scope:** the files it may edit, from the overlap table; everything else is read-only.
- **Do not decide:** any `Q-` question without a ruling goes back to the manager.
- **Proof:** the Css-Snapshot steps above, and the tests it must run (the convention class, the
  Razor component tests, then the full suite once).
- **Update the catalog in the same PR:** move the row to the new entry, refresh the count and
  screenshot, and delete the planned row, per
  [Keeping this page current](ComponentCatalog.md#keeping-this-page-current).
- **Parallel-owner rules** from the playbook: fake addresses use `@example.com` and `gitleaks`
  runs before every push; a red `main` after a green PR is fixed before the next merge; its own
  browser tab, with `location.port` checked before any click; wait for subagents in the
  foreground; a 60 tool-call budget, then a hand-off note.
- **Model:** Opus for B1 and B2, which set the pattern and carry design decisions; Sonnet for the
  area migrations, which are mechanical and checked by the baseline.

## Questions to answer before each wave

Ask these in time; an owner started without them guesses. The full text and recommended answers
are in the catalog.

| Before | Questions |
| --- | --- |
| Wave 2 | `Q-B2-1` button radius (changes all 205 buttons), `Q-B2-2` destructive look, `Q-B2-3` icon gap; `Q-A2-1`, `Q-A2-2` |
| Wave 3 | `Q-A1-1` to `Q-A1-3`; `Q-B3-1` |
| Wave 4 | `Q-A4-1`, `Q-A4-2`; `Q-A6-1`, `Q-A6-2`; `Q-A7-1`, `Q-A7-2`; `Q-A3-1`, `Q-A3-2`; `Q-B5-1` |
| Wave 5 | `Q-A5-1`; `Q-A8-1` |
