# Backlog

Small engineering work that has been agreed but not scheduled: refactors, cleanups and
evaluations whose answer changed nothing yet. It is for whoever picks up spare capacity on
the chat surface. Product features belong in [roadmap.md](roadmap.md); what is
deliberately absent belongs in [known-limits.md](known-limits.md).

Each entry says what to change, where, what done looks like, and why it was raised.
Delete an entry in the same commit that delivers it.

## Folding the clipboard helpers into one

**Raised:** 2026-10-17, while evaluating the MudBlazor add-on packages (see
[Packages evaluated and declined](#packages-evaluated-and-declined)).

Five places copy text through the same `huddleClipboard.copy` helper in `wwwroot/app.js`
and then show a success or warning snackbar. Each repeats the same steps: call the helper,
branch on the `bool` it returns, and show a message under a fixed snackbar key. Only the
text, the wording and the key differ.

| Caller | Copies | Snackbar key |
| --- | --- | --- |
| `Components/Tasks/TaskClipboard.cs` | A Task id, or a Task link | `copy` |
| `Components/Shared/RoomLinkCopier.cs` | A Room's reference text | `copy` |
| `Library/LibraryClipboard.cs` | A Library file path | `library-copy` |
| `Skills/Ui/PromptClipboard.cs` | A prompt for the Human's AI tool | `skill-prompt-copied` |
| `Components/Settings/Skills/CopyPathMenu.razor` | An install file path (calls the helper inline) | its own |

**Change:** add one internal helper that takes the text, the success message, the
refusal message and the snackbar key. It should return whether the copy succeeded. Keep
each caller's wording as a constant on the caller, because the wording is fixed by the
spec. Then make the five callers use it.

**Done when:**

- `huddleClipboard.copy` appears in exactly one C# file.
- Every message a Human sees is unchanged, word for word.
- `TaskClipboardTests`, `RoomLinkCopierTests` and the Library and Skills copy tests pass
  unchanged.
- One new test covers the helper's success and refusal paths.

> [!WARNING]
> Keep the clipboard call the **first `await`** in the click handler. Browsers allow a
> clipboard write only during the click's brief permission window, and an earlier
> `await` can close it (Spec §13.13.1; the comments on `TaskDetail.CopyIdAsync` and
> `Tasks.OnCopyIdAsync`). The shared helper must not await anything before it calls
> `huddleClipboard.copy`.

`LibraryClipboard` passes no `CancellationToken` today, so the shared helper will add one.
Pass `CancellationToken.None` from click handlers, as the Task callers already do.

## Upgrading MudBlazor to 9.11.0

**Raised:** 2026-10-17. MudBlazor 9.10.0 isn't pinned on purpose. It was the newest
version when commit `c760ec5c` installed it on 2026-09-14. Version 9.11.0 shipped on
2026-09-27, and nothing in our setup flags new versions.

The release lists no breaking changes. It fixes components we use:

| Fix in 9.11.0 | Where we use it |
| --- | --- |
| `MudTreeView`: changes to icon, ripple and expansion at runtime apply to every item | `LibraryTree` |
| `MudDataGrid`: the table stays full width when its columns are narrower | `TaskListView` |
| `MudChipSet`: every chip re-renders when a set parameter changes | `SearchBar`, `TaskDetail` |
| `MudAutocomplete`: scrolling to the highlighted item works after disabled items | `TaskDetail`, `TaskPicker` |

The release also cuts memory per component. In PR #13711, `MudComponentBase` falls from
1,527 to 1,057 bytes per instance. That matters on Blazor Server, where every open
circuit holds its components in server memory. The release notes' headline, "renders 31%
less" than v9.8.0, does not say what was measured, so don't rely on it.

**Change:** set `MudBlazor` to `9.11.0` in `Directory.Packages.props`. It is a shared root
file, so announce the change before making it (see [CLAUDE.md](../../CLAUDE.md)). Then
build and test the solution and fix what breaks.

Expect two kinds of failure:

- **New build errors.** The release extends the `MUD0002` analyzer, which flags removed
  Razor parameters. Warnings are errors in our build.
- **Markup changes in component tests.** Several components now render less or lighter
  markup (`MudButton`, `MudChip`, `MudListItem`, `MudMenuItem`, `MudSelect`,
  `MudDataGrid`). Check that a failing bUnit test shows a harmless markup change, not a
  behaviour change, before you update it.

**Done when:**

- `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` pass on Windows.
- Every doc that says a fact was "checked against 9.10.0" says 9.11.0. That includes
  [design-system.md](design-system.md), [mudblazor.md](mudblazor.md),
  `agents/MudBlazorImplementation.md`, [UXDesignGuidelines.md](UXDesignGuidelines.md)
  and the Library, Global Search, Adapters and Skill Packs specs.
- These facts are rechecked against 9.11.0: `MudDrawer` has no resize, `MudGlobal`
  cannot set `Size` or `Variant`, and `MudHotkey` modifiers are side-specific. The
  release notes mention none of them, so each probably still holds.

## Packages evaluated and declined

These were checked against the code they would supposedly replace, and rejected. Read
this before proposing them again. Re-evaluate only if what the reason depends on
changes.

| Package | Version checked | Why not |
| --- | --- | --- |
| [MudBlazor.Markdown](https://github.com/MyNihongo/MudBlazor.Markdown) | 9.10.0 | `MarkdownRenderer` is mostly our own rules (Task ids, wiki links, Library paths, Mention pills, heading ids, safe-URL checks), not Markdown-to-HTML. `MudMarkdown` parses its own `Value`. It renders links as `MudLink` with only an address, so our classes and data attributes are lost. It renders unknown inlines (our Mention pill) as nothing and does no URL checking. Adopting it moves our code into a Markdig extension plus a subclass of their component. It also turns 102 plain string tests into bUnit tests. |
| [MudBlazor.Extensions](https://www.nuget.org/packages/MudBlazor.Extensions) (MudEx, fgilde) | 9.11.4 | **Skipped** (decided 2026-10-17). See [Where MudEx would fit](#where-mudex-would-fit) for the one piece that could justify reopening it. It requires MudBlazor 9.11.0 or later (we pin 9.10.0), so every MudBlazor upgrade waits on its single maintainer. It pulls in about 25 packages (ExcelDataReader, SharpCompress, MsgReader, FluentValidation, a PDF viewer, Sqlite, BlazorJS and MudBlazor.Markdown), all for CI's vulnerable-dependency check to track. |

### Where MudEx would fit

The aim is to maintain less of our own code by using off-the-shelf parts. So each MudEx
component was paired with the custom code it might replace. Most of our UI code is
spec rules and domain logic, which no library can own. The generic plumbing it could
take over is small:

| Our code | MudEx candidate | Verdict |
| --- | --- | --- |
| `ConfirmDialog.razor` (36 lines) | `MudExMessageDialog`, `ShowConfirmationDialogAsync` | **Could replace it.** `ConfirmDialogExtensions` stays, to keep the danger styling and the verb button (UX-R12). Saves about 36 lines. |
| `NameDialog.razor` (138 lines) | `MudExPromptDialog` | **No.** Its `CanConfirm` returns only `bool`. It cannot show the validator's message, and it cannot keep the dialog open when an async submit fails (UX-024, UX-025). |
| `LibraryDocument`'s viewer for Image, SVG, Text and Other | `MudExFileDisplay` | **Worth reopening.** Ours is a few lines per kind and shows no preview for Other. MudExFileDisplay would add PDF, Office, zip, CSV and email previews off the shelf. This is a feature gain more than a deletion, and the heavy dependencies listed above exist for exactly this component. |
| `LibraryTree.razor` (606 lines) | `MudExTreeView` | **No.** The lines are scope rules, lazy loading through `LibraryFileService` and a path comparer that keeps selection across refreshes. MudExTreeView adds view modes and filtering, not those. |
| `LibraryExplorer`, `LibraryFileOps` | `MudExFileManager` | **No.** Ours enforces Library scopes, preview counts and protection of the open document. |
| `ResizableEndDrawer` + `huddleResize` | `MudExSplitter`, `MudExSplitPanel` | **No, unless the design changes.** Those resize two panes side by side, not a temporary drawer over the page ("Drawer anatomy" in the design system). |
| `task-list-resize.js` (174 lines) | none | MudEx has no data-grid column resizing. |
| `MarkdownRenderer` | `MudExMarkdown` | **No.** It wraps MudBlazor.Markdown; see the row above. |

**Reopen MudEx when** the Library should preview files beyond Markdown, text and images.
Then adopt it for `MudExFileDisplay` and replace `ConfirmDialog` in the same change.
