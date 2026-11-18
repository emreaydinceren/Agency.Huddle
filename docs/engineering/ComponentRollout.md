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
- Lessons from the pilot
- Lessons from the area migrations

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
- The B1 rename also edited `UiConventionSource.CssTrackFiles`, which names
  `SegmentedControl.razor.css` by file name; a later rename of a tracked `.razor.css` does the same.
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
| 1 | **Done 2026-11-18.** **B1** `SegmentedControl` pilot: rename from `HuddleSegmentedControl`, 28 px geometry, colour tokens in the component, rule `CV14` (zero-tolerance from the start: all six sites moved in the build PR). Manager writes the B6 rule into design-system.md meanwhile | 1 | Proves the build, rule, baseline and snapshot steps on six sites before 200 are touched |
| 2 | **Done 2026-11-18** (build, then four area migrations in parallel worktrees: 136 of 141 raw tags moved; 5 stay raw and baselined, see the catalog's "Still raw"). **B2** `ActionButton` (roles `Primary`, `Secondary`, `Quiet`, `Danger`, `DangerQuiet`), with **A2** `DialogFooter` as its first user: rule `CV15`, control radius on every button, 65 tags moved (18 dialogs, the 15 Medium, the 14 with no variant, every Danger and Warning site). The migration then moved the CV15 backlog (141 tags) area by area, four owners at once | 1, then up to 8 | The most sites (205), the widest visual change, and `base.css` is shared |
| 3 | **A1 build done 2026-11-18** (`Notice`, `NoticeLevel`, `NoticeKind`, rule `CV16`, the 18 px icon and the notice's own 12 px spacing in `base.css`; 53 of 87 tags moved in the build: every Warning but the two Room banners, every non-dense site, the dialog errors, the Composer pair and the runtime-severity site). **Migration done 2026-11-18**: the other 32 tags moved in three parallel worktrees; only the two Room banners stay raw. **B3 and B4 build done 2026-11-18** (`TextField` with `TextFieldShape`, `SelectField<T>`, rules `CV17` and `CV18`, no stylesheet change): 48 of 62 text fields and 28 of 34 selects moved in the build, every multi-line, adorned, compact and nameless site among them. Backlog per area: Room 4 text fields and 1 select, Settings 6 and 0, Shared 3 and 4 (the 4 are `AgentProfileCard`'s picker, which moves with A4), Tasks 1 and 0, Teams 0 and 1. Migrate by area, one component per area at a time | 1, then up to 8 | Smaller and more regular than buttons; B3 and B4 build in one short PR each |
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
| `wwwroot/css/room.css` | A1 (done: the Composer pair's colour, size and margin) |
| `wwwroot/css/library.css`, `FeedPage.razor.css` | B1 |
| `wwwroot/css/shell.css` | B2 (the 2 px icon gap; removed in wave 2) |
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

**When it runs.** A capture is the slowest stage of a build (about 720 pages, in two captures), so
it does not run for every component. A build owner runs it only when the component makes a wide
visual change on purpose (the segmented control, the button roles, `Notice`). A component whose
change is small, or limited to a few sites, is proved by live `getComputedStyle` measurements of
those sites and the convention tests, and the manager runs one snapshot comparison for the whole
wave after its builds and migrations are merged: the commit before the wave against the commit
after it. A pure migration is always proved that way, once, never per owner.

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
| Wave 2 | Answered 2026-11-18, each at the catalog's recommendation: `Q-B2-1` control radius, `Q-B2-2` Text Error trigger and Filled Error confirm, `Q-B2-3` no 2 px gap; `Q-A2-1` Close-only info dialogs are pickers, `Q-A2-2` the cancel word may vary |
| Wave 3 | `Q-A1-1` to `Q-A1-3` answered 2026-11-18 at the catalog's recommendation (Warnings interrupt only when the Human must act now, an 18 px icon, always dense); `Q-B3-1` answered 2026-11-18 at the recommendation (4 rows, the Description's auto-grow kept) |
| Wave 4 | `Q-A4-1`, `Q-A4-2`; `Q-A6-1`, `Q-A6-2`; `Q-A7-1`, `Q-A7-2`; `Q-A3-1`, `Q-A3-2`; `Q-B5-1` |
| Wave 5 | `Q-A5-1`; `Q-A8-1` |

## Lessons from the pilot

B1 (`SegmentedControl`, 2026-11-18) set the pattern. What the plan did not say:

- **Check the snapshot tool before the baseline.** The seeder had renamed a Room scope
  ("Platform chat" to "in Platform") and the New chat label, so a fresh Capture stopped at the
  Room list and every `open-new-chat` page failed. B1 fixed the scope in `css-snapshot.mjs`; the
  New chat step still finds nothing (16 pages, the same in both captures, so they cancel out).
  Run a short `-Only feed` Capture first and read the log before starting the 7-minute one. A
  Capture cut short (B1's machine crashed mid-run) leaves an `-Out` folder the next Capture
  refuses to overwrite: capture into a new folder; a finished baseline stays valid.
- **`-SeedRoot` must be a folder named `css-snap*`** (the script refuses anything else, because
  it wipes it). Pass Windows paths from PowerShell, not Git Bash, which eats the backslashes.
- **A class rename turns every renamed element into "removed" plus "added"**, which hides the
  property changes the Report exists to show. Rewrite the baseline's element paths instead with
  `agents/scripts/css-snapshot-rename.mjs` (it maps old class names to new ones and re-sorts each
  path segment), then Report against the rewritten copy.
- **A fixed height and a trait border fight.** `paper` and `hairline` draw a 1 px border on the
  track, so a 28 px `border-box` track with 2 px padding leaves 22 px for 24 px items. The fix is a
  custom property the trait overrides (`--segmented-inset: 1px`), not a second geometry per Theme.
- **A public type in `Components/Shared` fails CA1716** (the namespace ends in a keyword). A
  parameter type must be public (rules.md), so it goes in `Agency.Huddle.App.Components`, as
  `AgentProfileCardMode` does.
- **Specificity moves when rules move into the component.** A rule reached through `::deep` gains
  the scope attribute, so a caller's override must name more classes than the component's rule;
  the Feed's Nordic badge pill names the control for that reason.
- **The shell hooks block `sed -i`**, and the Write tool and heredocs emit LF. A rename across many
  files is a PowerShell `[IO.File]::ReadAllText` and `.Replace` loop, which keeps CRLF.

B2 (`ActionButton` and `DialogFooter`, 2026-11-18) added:

- **The baseline writer rewrites every area file, CV11 included.** `HUDDLE_WRITE_BASELINES=1` wrote
  `Css.txt` from the raw CV11 counts, which `Exceptions.txt` already covers; restore that file with
  `git checkout` and keep only the files whose diff is the new rule's lines.
- **Development serves `wwwroot` from the source tree.** A Capture runs the app with `--no-build`, so a
  `.razor` edit during the 7-minute baseline is safe but a stylesheet edit lands in it. Edit the
  stylesheets only after the baseline finishes, and do not `dotnet build` while a Capture's app holds
  the binaries.
- **A wrapper's parameter must not share a name with an HTML attribute it may be given**, because Blazor
  matches parameters ignoring case: `Role` on `ActionButton` means a `role="…"` attribute cannot be
  passed through (a button needs none). `UserAttributes` there is splatted with `@attributes`; MudBlazor's
  own `UserAttributes` wants a mutable `Dictionary`.
- **`<DialogActions>` is the dialog's own fragment**, so a footer component goes inside it, not in place of
  it, and draws its buttons with no wrapper element so MudBlazor's row layout still applies.
- **Keep the sites' hook classes.** Tests and scripts find dialog buttons by class (`fields-dialog-save`);
  `DialogFooter` takes `CancelClass` and `PrimaryClass` for them, so no test selector had to move.
- **Hand-edit migrations in a script, not one Edit per site.** B2 moved 65 tags with two PowerShell scripts
  that match each tag by an anchor, map Variant and Color to the role, fail on anything unmapped and keep
  CRLF; each printed the new tag for review.
, from the repo root; `$s` is a scratch folder outside the repo):

```powershell
pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Capture -Screens -Reseed -SeedRoot C:\seeds\huddle\css-snap-b1 -Port 5320 -Out "$s\baseline"
# after the change and a dotnet build Huddle.slnx; no -Reseed, so both captures share the golden copy
pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Capture -Screens -SeedRoot C:\seeds\huddle\css-snap-b1 -Port 5320 -Out "$s\candidate"
node agents/scripts/css-snapshot-rename.mjs "$s\baseline" "$s\baseline-renamed" huddle-segmented-control=segmented-control huddle-segmented-indicator=segmented-indicator huddle-segmented-group=segmented-group huddle-segmented-active-item=segmented-active-item feed-count=segmented-badge feed-count-needsyou= task-toolbar-kind-icon=segmented-icon
pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Report -Baseline "$s\baseline-renamed" -Candidate "$s\candidate" -Out "$s\report" -Declared "$s\declared.txt"
# a convention baseline, only when a raw tag is left somewhere; B1 needed none (CV14 is 0 everywhere)
$env:HUDDLE_WRITE_BASELINES = '1'; pwsh agents/scripts/Run-Tests.ps1 -FilterClass '*UiConventionTests'; Remove-Item Env:HUDDLE_WRITE_BASELINES
```

## Lessons from the area migrations

Wave 2's four migration owners ran at once, one git worktree and one branch each, and the manager
merged the four branches into the working branch. What made it cheap, and what to repeat:

- **The baseline file is the work list.** Each owner read its area's `CV15|path|count` lines,
  moved what it could and regenerated the baseline. A site left raw stays in the file with its
  reason in the owner's report, so nothing is lost and nothing is silently skipped.
- **Create the worktrees from the working branch tip**, not from main, one per owner
  (`git worktree add -b mig/<name> <path> HEAD`). An owner commits on its own branch and never
  pushes; the manager merges. Four branches merged with no conflicts, because areas hold different
  files and different baseline files.
- **Leave a site raw rather than bend the component.** Five of 141 tags were not role buttons (an
  answer tile, an option card, a tree row, a recoloured add button). Owners reported them with the
  reason, which is the input for the next component, not a failure.
- **Run whole test projects, not several `--filter-class` values.** `dotnet test` rejects more than
  one filter-class argument, and `Run-Tests.ps1 -FilterClass` matched nothing for a pattern with a
  middle `*`. One pattern per call, or the whole project, worked.
- **Proof is one comparison, after the merge.** A pure migration should move nothing, so a single
  snapshot of the commit before the migrations against the commit after them replaces four
  per-owner captures.
- **A comparison needs the same `-SeedRoot` for both captures.** The seed embeds absolute file paths,
  so a different root made 38,618 differences on 178 pages. The one real residue of the wave 2
  comparison was the Feed's `Done · HH:MM` pill, whose width follows the clock at app start; it moved
  by 2 to 3 px on 24 pages and no migrated site was involved. Result: 0 differences from the
  migration of 136 buttons (before `c77dd6c9`, after `21832e69`, 720 pages in 8 Theme and mode pairs).
  Treat that pill as known noise.
- **A margin in a flex or grid container with a `gap` adds to the gap.** The `Notice` wave comparison
  (720 pages, same seed root, 270 undeclared differences, every one traced to the Notice rules) found
  one real defect: `.prompts-field` is a flex column with a 4 px gap, so the notice's 12 px above and
  below added to it and each field grew by 20 px. Fixed with `.prompts-field > .mud-alert.notice {
  margin-block: 0 }`. Put this in the owner brief for any component that carries its own margin:
  list the flex and grid parents of its sites and zero the margin there.
- **Merge main between waves, not during one.** Merging while owners worked would have invalidated
  their baselines; main was merged only when no owner had uncommitted edits.

A1 (`Notice`, 2026-11-18) added:

- **Move every site whose look changes in the build, so the migrations stay pure.** The always-dense
  ruling changes 26 sites' height and the spacing ruling every site with a margin class; the build moved
  all of those (53 tags), so each area migration of the 34 left is a tag swap whose only pixel change is
  the margin classes it removes, which its owner declares.
- **A behaviour rule the component enforces is a classification, and it changes tests.** Moving the role
  into `Kind` meant reading all 36 Warnings: four `alert` sites became `status`, and a test that found
  the notice by `[role=alert]` (`SessionsPanelTests`) had to find it by its hook class instead. Read the
  message and what the Human can do about it, not the severity.
- **A wrapper's spacing rule is a rule about siblings.** A margin on the notice adds to a flex
  container's `gap`, so the snapshot is where a doubled gap shows; the rule spaces a notice from its
  siblings only, never from its container's edges.
- **A shared treatment is a different component.** The Room banners look like notices but carry a
  hairline, a tint and a second bar; they stay raw and baselined rather than giving `Notice` a banner
  flag.
- **Search the whole solution for a parameter type's name, not only `Huddle.App`.** `NoticeSeverity`
  already existed in `Huddle.Acp.Abstractions`, which two components import, so the build failed with
  CS0104; the enum is `NoticeLevel`.
- **CV11 checks the wrapper's own CSS.** A literal `font-size: 18px` and a 9 px padding failed it; the
  icon uses `--icon-size-row` and on-scale spacing (8 px plus a 2 px nudge). Run the convention class
  before a Capture, because a stylesheet fix after it starts invalidates the run.
- **Measure the glyph, not its box.** MudBlazor sizes an icon's `svg` itself (`.mud-icon-size-medium`,
  1.5rem, so 21 px here), so a `font-size` on `.mud-alert-icon` shrank only the line box and the first
  capture missed it; only `getComputedStyle` on the `svg` in a live tab showed it. A fingerprinted
  stylesheet stays cached in an open tab after an edit: re-load the `link` with a query string to measure.
- **A sandbox may refuse `npx` downloads**, so `markdownlint` may not run: check MD047 (one final
  newline) and table shapes by eye, and say so.

B3 and B4 (`TextField` and `SelectField`, 2026-11-18) added:

- **Count the parameters before naming the options.** All 62 text fields bind a `string`, so `TextField` is not generic;
  the selects bind six types, so `SelectField<T>` is. Four looks (one line, toolbar, long, Markdown) became one
  `TextFieldShape` value instead of `Long`, `Compact` and a class, so a compact multi-line field cannot be written.
- **A rule name can be refused by an analyzer.** `CA1720` rejects `Long` as an identifier (a type name), so the option is
  `LongText`. Build the enum first, before the call sites are scripted against its names.
- **A required name is a render-time check, not `[EditorRequired]`.** "A `Label` or an `AriaLabel`" is an either-or that
  the Razor compiler cannot express, so the component throws on its first render; every bUnit test that rendered a
  nameless field (three in `SearchFieldTests`) had to name it, which is the point.
- **Pass optional Mud parameters by branch, not by `null`.** MudSelect keeps its value in parameter state, so passing
  `SelectedValues="null"` to a single select could reset it; `SelectField` renders one `MudSelect` for single and one for
  multiple selection, and leaves `ToStringFunc` out rather than pass a default that might change the shown text.
- **A wrapper's focus method returns what callers already await.** `MudTextField.FocusAsync` is a `ValueTask`, and a caller
  wrapped it in a `Func<ValueTask>`; `TextField.FocusAsync` returns `ValueTask` too.
- **The migration script needs the attribute regex to respect quotes.** A `Class="@(x ? "a" : null)"` is cut at the first
  inner quote by `"[^"]*"`; the script rejoined the pieces with a missing space. Print every converted tag and read them.
- **`/tasks/new` in a live lane creates a View**, not a Task: measure a Task's edit page at
  `/tasks/item/<id>/edit` instead.
