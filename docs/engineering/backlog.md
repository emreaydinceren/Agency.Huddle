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

## Finishing the tests for Task attachments

**Raised:** 2026-10-18, after the review of the first build of
[TaskAttachments](../Huddle.TaskAttachments-Specifications.md) (branch `feat/task-attachments`).
The review's code gaps are fixed: "take theirs" deletes the files, a read or write error on one file does not stop the rest,
Revert, Cancel and closing the page cancel a running upload, a pick past the cap shows the spec text, the progress bar shows,
both services are injected normally, and the picker search leaves folders out of its hit budget and shows the path as dimmed text.
Writing the tests also found, and fixed, a snackbar bug: one already showing under the key `task-attach` swallowed the next message.

Three behaviours have no test, because bUnit cannot drive them without a change to the code or the harness:

- A stream error on one file does not stop the next. `InputFileContent` cannot make `OpenReadStream` throw; it needs an `IBrowserFile` fake.
- Cancel or Revert during an upload leaves no file behind. It needs a save that can be held open while the test clicks Revert.
- The progress bar shows while an upload runs. The upload finishes before a test can look.

**Also:** write the manual tests for A1, A3, A8, A10, A11 and A14 in
[manual-tests/tasks.md](manual-tests/tasks.md) and update the Index row, which says "none yet". The spec's case "`TaskUploadFolders.For`
returns `null` with no Uploads root" cannot be set up, because the Uploads root is always built in; drop it from Appendix A.

**Done when:** the three tests exist (add a hook to `TaskUploadService` only if there is no other way), the manual tests are written,
and `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` pass.

## Linking URLs inside longer property text

**Raised:** 2026-10-29, from the open point OP-3 of
[Front matter properties](../Huddle.FrontMatterProperties-Specifications.md). Ships after the core of that spec.

The spec links a property value only when the **whole** value is an `http:`, `https:` or `mailto:` URL. A Text value
such as `See https://example.com/plan for details` shows the URL as plain text. The reference app's help says text
properties may hold URLs, but not whether a URL inside longer text is clickable.

**Change, if FP-M1 shows the reference app links it:**

- `LibraryProperties.razor`: split a Text value into text runs and URL runs, and draw each URL run as a link with the
  same scheme rule (D-11). Do not use a `MarkupString`: build the runs as Razor elements.
- Find URLs with one `[GeneratedRegex]` that stops at whitespace, and drops trailing `.`, `,`, `)` and `;`, as an
  autolink does.
- The same splitting applies to List items.

**If FP-M1 shows it does not:** close this entry, and change the spec's OP-3 to "No, as in the reference app".

**Done when:**

- The spec's OP-3 is closed one way or the other.
- If built: bUnit tests show a URL in the middle of text becomes an `a` with the right `href`; `javascript:` inside text
  stays text; trailing punctuation is not part of the link; text holding both a `[[link]]` and a URL draws both.

**Risk:** the attribute rule (D-11). The `href` must come only from a matched, scheme-checked run.

## Hardening remote access

**Raised:** 2026-10-30, while designing [Remote access](../Huddle.RemoteAccess-Specifications.md), a faithful port of
OpenClaw's gateway model. Instant revoke, rate limiting, trusted-proxy auth and admin scopes are in V1 now. These four
are what is left, each with its own design here; the **V1** column says where the spec stops. Ship them after V1, in
any order.

| # | Item | V1 | Design |
| --- | --- | --- | --- |
| H-1 | **Huddle's own TLS for the direct binds** | Plain HTTP, labelled downgraded; Tailscale Serve is the HTTPS path (spec §3) | Huddle as its own certificate authority, as `mkcert` does: a root CA created once, its key encrypted with Data Protection, **with X.509 name constraints** limiting it to `10/8`, `172.16/12`, `192.168/16`, `.local` and the machine name (written with `AsnWriter`; .NET has no built-in extension), so a stolen key cannot impersonate other sites. A leaf certificate for the bound addresses and names, at most 825 days (Apple's limit), renewed and re-issued through `ServerCertificateSelector`. HTTPS on the LAN address and plain HTTP on `127.0.0.1`, same port number, different addresses. A **Download certificate** link and per-OS install steps (iOS: install the profile, then turn on full trust under About › Certificate Trust Settings; Android: install as a CA certificate; Windows and macOS: trusted root). Session cookies gain `Secure`. About 1-1.5 weeks with device testing. **Not** a plain self-signed certificate: permanent warnings teach people to click through certificate errors. OpenClaw builds no browser TLS either |
| H-2 | **Read-only devices** | `operator.read` and `operator.write` are always granted together, because Huddle's UI has no read-only mode (spec §6.7) | Grant `operator.read` alone, as OpenClaw allows. Every action that changes something (post, edit, create, move, delete, run) checks `operator.write` in its service, and the UI hides those controls for a read-only session. Start with an inventory of the write paths; the size of that list decides whether this is worth it |
| H-3 | **Live address change** | A direct bind's new IP address needs a restart for the host list and the setup-link QR (spec §6.4) | On `NetworkChange.NetworkAddressChanged`, recompute the host list and the setup-link addresses and signal the same `IOptionsChangeTokenSource<HostFilteringOptions>` that V1 already uses for the Tailscale name. The LAN bind already listens on all interfaces, so only the lists change |
| H-4 | **Theme Lab copy over plain HTTP** | `theme-lab.js` calls `navigator.clipboard` directly, which browsers refuse on the downgraded binds | Route its copy through `huddleClipboard.copy` in `app.js`, which already falls back to `execCommand("copy")` outside a secure context |

**Change:** for each item, write its test first, as the spec's Appendix C does for V1, then the code. When an item
ships, update the spec where its **V1** column points, and delete its row here.

**Done when:** each shipped item's tests pass, `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` pass, the
spec says what changed, and [known-limits.md](known-limits.md) no longer lists what the item fixed.

**Risk:** H-1 is mostly device work: the ASN.1 for name constraints and the per-OS install steps are where it goes
wrong, and each OS needs a hand test. H-2 may turn out larger than its benefit.

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
| `task-list-resize.js` (174 lines) | none | MudEx has no data-grid column resizing. |
| `MarkdownRenderer` | `MudExMarkdown` | **No.** It wraps MudBlazor.Markdown; see the row above. |

**Reopen MudEx when** the Library should preview files beyond Markdown, text and images.
Then adopt it for `MudExFileDisplay` and replace `ConfirmDialog` in the same change.
