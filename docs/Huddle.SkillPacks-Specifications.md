# Huddle.SkillPacks — Design Specification

**Date:** 2026-10-17 · **Status:** Built 2026-10-18 on `feat/skill-packs`; the manual tests (SKILLPACKS-01 to -07) not yet run by a person · **Vision:**
[Huddle.SkillPacksSpec.md](Huddle.SkillPacksSpec.md) · **Decision record:** a successor to
[ADR-0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md), to be written before
any code (Appendix A, task P0) · **Vocabulary:** [language.md](engineering/language.md)
(**Skill**, **Pack**)

This is the design for replacing how Huddle gets Skills and Skill Packs from outside. Today
Huddle downloads them from public GitHub and ships two Packs and a catalog of 265 entries (263 distinct Skill names) inside the
app. After this change **Huddle makes no network calls for Skills**: the Human's own AI tool
fetches them wherever it likes and records pointers in a registry file, SKILLS.md, and Huddle
follows the pointers, checks what they lead to, and offers the result to Agents.

It is written for the engineers or agents who build it, with no memory of the conversation
that produced it. The [vision](Huddle.SkillPacksSpec.md) is the user guide and stays the
source for what the Human sees; this page decides how it works. Where the two disagree, this
page wins and says so. Read §5 for the shape, §6 for your component, §8 for the rules the
vision left open, and Appendix A for the ordered, test-first task list.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

---

## 1. Goal

Let the Human add, update and retire Skills and Packs from any source they can reach, without
Huddle ever going online, signing in or downloading.

Concretely:

1. **Huddle reads pointers, nothing else.** SKILLS.md lists one row per pack or catalog file;
   each file lists its Skills with a local path. Huddle follows those paths and reads what is
   there.
2. **One contract, written down.** `InstallSkillPack.md` tells any AI tool what Huddle expects,
   and the `huddle-packs validate` command runs the same checks Huddle runs, so what passes in
   the AI tool works in Huddle.
3. **Agents never change behind the Human's back.** Choosing a Pack or Skill copies it into
   `Skills/`; a change at the pointer shows **Update available** until the Human applies it.
4. **Nothing is lost in the move.** The two shipped Packs, the shipped catalog and every
   imported Skill reach the new format on first start, with no Agent restarted.
5. **The screens explain themselves.** The Skills section carries enough guidance text that a
   Human who has never read the User Guide can work out what each tab is for and what to do.

**Why this matters.** Shipped Packs and catalog go stale between releases and cannot be
retired. The downloader reaches only public GitHub, because Huddle has no browser and no way
to sign in. An AI tool such as Claude Code already has the Human's sign-ins and can read any
repository layout; Huddle only needs to say what it wants and check what it gets.

---

## 2. Use cases

Each row is a scenario the finished system must handle. §13 walks U1 end to end.

| # | The Human does | What must happen |
| --- | --- | --- |
| U1 | Selects **Add with your AI tool**, pastes `https://github.com/anthropics/skills`, copies the prompt into Claude Code | Claude Code clones it, writes `packs/document-skills.skillpack.json`, `source.json` and `NOTES.md`, runs `huddle-packs validate --strict` until it passes, and adds a Packs row. Within a second Huddle shows **Document skills is ready · Show** and the card wears **New** |
| U2 | Selects the **Document skills** chip on a bookkeeper's card | The four Skill folders are copied into `Skills/`, each with `.source.json`; the Agent's `skills:`, `consult_when:` and `_packs:` change; it restarts |
| U3 | The AI tool pulls a new commit of anthropics/skills | Cards for the Pack and its holders show **Update available**; nothing changes for any Agent |
| U4 | Applies the update in `G7` | Changed Skill copies are replaced for every holder, with no restart; checked Agents get the membership change and restart |
| U5 | Starts this version of Huddle for the first time | The move writes SKILLS.md, six catalog files holding 263 Skills, the Sources' notes, rows for the skills repository and for every imported Skill. No Agent file changes; no card shows **Update available** |
| U6 | Opens Build a team for a reviewer while `code-review-and-quality` has no path | The draft shows **Needs setup** with **Copy prompt**; **Approve & hire** waits |
| U7 | Shares the Skill Sources folder with a laptop that cloned to `C:\src` | Rows with only `DESK-01:` entries are **Not downloaded** on the laptop; **Copy fix prompt** asks the laptop's AI tool to add a `LAPTOP:` entry |
| U8 | Edits SKILLS.md by hand and breaks a row | That row is **Broken** with the check that failed; every other row still loads |
| U9 | The AI tool is mid-write when Huddle reads a pack file | No **Broken** flash: Huddle rereads 2 seconds later |
| U10 | Removes a Source | After **Remove Source?**, its notes, rows and the files it kept in the Skill Sources folder go; its clone and the Agents' copies stay |
| U11 | A fetched Skill's `tools:` names `propose_sprint` | `S011`: **Broken**. Only built-in Skills grant App Tools |
| U12 | Runs `huddle-packs validate` with no Huddle running | Same findings as Huddle shows, same summary line, exit code 1 if any error |
| U13 | `source.json` marks a Source **Withdrawn** | Its Packs and Skills show a red chip; new hires cannot take them; holders are flagged and keep working |
| U14 | Opens Settings › Skills for the first time | Each tab opens with an intro that says what it is, where its contents come from and what Huddle does not do |
| U15 | Uses no AI tool: clones by hand, writes the files and the row | It appears exactly as if an AI tool had done it |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Any network call from Huddle for Skills** | Decision 2 of the vision. Sign-in is the AI tool's job; Huddle has no browser |
| **An agent inside Huddle that imports** | Decision 1. The Human's own AI tool does it, guided by `InstallSkillPack.md` |
| **An approval step** | Decision 5. The checks, not a click, keep out what Huddle cannot use safely |
| **Fetched Skills granting App Tools** | Decision 15. Only built-in Skills do (`S011`) |
| **Agents reading files beyond top-level `.md`** | Unchanged runtime rule. Sub-folders and scripts are copied and flagged, never read |
| **Running, building or installing anything a Skill contains** | Huddle never executes Skill content; `InstallSkillPack.md` forbids the AI tool to |
| **Huddle editing pack or catalog files** | They belong to the AI tool. Outside two one-off cases, Huddle writes only SKILLS.md (remove a row), `hidden.json`, `log.jsonl` and the copies in `Skills/`. The two cases: the move writes the first SKILLS.md and the files it carries over, once (§6.13), and **Remove** deletes a pack, catalog or Source folder that lives in the Skill Sources folder (§6.12) |
| **Deleting a clone** | Huddle deletes only inside the Skill Sources folder and `Skills/` |
| **Merging concurrent writes from two Huddles** | Last write wins ([ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)) |
| **A per-Agent copy of a Skill** | One copy per Skill name in `Skills/`, as today. §8.4 explains what that means for updates |
| **Writing to the Skill Sources folder from the Library** | It is a read-only Library root |

---

## 4. Design Principles

1. **Pointers, not downloads.** Huddle's only input is local paths. Every path can be absolute,
   relative to the file it is in, or one per computer.
2. **One set of checks, two callers.** The checks live in one library; Huddle and
   `huddle-packs` call the same code with the same inputs, so their findings cannot drift.
3. **The registry is read whole and published as one snapshot.** Readers never see a
   half-updated registry, as with `SkillPackStore` today.
4. **Being wrong is loud, not disabling.** A broken row is a **Broken** card with the reason;
   the rest of the registry still loads. A missing folder is **Not downloaded**, not an error.
5. **The first row wins.** Every tie, between two rows, two catalogs or two origins of a name,
   is broken by row order in SKILLS.md, and the loser carries the finding.
6. **Copy, then change the Agent.** An Agent's file names a Skill only after its copy is
   in place and checked. Agents use copies, never clones.
7. **Model-facing text is configuration.** `InstallSkillPack.md`'s fixed sections and every
   prompt the Human pastes come from Settings › Prompts
   ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)). Guidance text in the UI is
   not model-facing and lives in the components.
8. **Simple over complete.** Hand-rolled argument parsing, existing `FileSystemWatcher` and
   hashing code, no new packages.

---

## 5. Architecture Overview

### 5.1 Projects and components

Two new projects join the chat surface's subtree. Adding them to `Huddle.slnx` is a shared
root change and is announced before it is made (CLAUDE.md, "Two projects, one build root").

| Project | Assembly | Kind | References | Holds |
| --- | --- | --- | --- | --- |
| `src/Huddle.SkillPacks` | `Agency.Huddle.SkillPacks` | Class library | Markdig only | Everything both callers share: the registry reader, per-computer paths, the file schemas, the resolver, `SkillFolderHash`, `SkillValidator`, `SkillReviewScanner` and every check |
| `src/Huddle.Packs` | `huddle-packs` | Console exe | `Huddle.SkillPacks` | The `huddle-packs` command (§6.4) |
| `src/Huddle.App` | (unchanged) | Web app | adds `Huddle.SkillPacks` and `Huddle.Packs` | The registry service, watcher, copier, install-file writer, the move, prompts and the UI |

`Huddle.SkillPacks` must not reference `Huddle.App`, `Huddle.Contracts` or anything ASP.NET,
so `huddle-packs` stays small and starts fast. What the checks need to know about one Huddle
(its roles, its installed and built-in Skills, the App Tool names only built-in Skills may
grant) reaches them as a `CheckContext` value. Huddle builds it from its own services;
`huddle-packs` reads it from the **context file** Huddle writes beside `InstallSkillPack.md`
(§6.7). That file is the only coupling between the two programs.

```text
                ┌──────────────────────── Huddle.SkillPacks ────────────────────────┐
                │ RegistryReader ─► Registry ─► RegistryResolver ─► ResolvedRegistry │
                │ PathEntries   PackFile  CatalogFile  SourceFile   SkillFolderHash  │
                │ SkillValidator   SkillReviewScanner   Checks.Run(…, CheckContext)  │
                └───────────────▲───────────────────────────────────▲───────────────┘
                                │                                   │
   ┌──────────────── Huddle.App ┴──────────────────┐     ┌──────────┴── huddle-packs ──┐
   │ RegistryWatcher ─► SkillRegistry (snapshot)   │     │ validate · roles · installed │
   │ SkillCopier ─► Skills/<name>/ + .source.json  │     │ skills · schema · --version  │
   │ InstallFileWriter ─► InstallSkillPack.md      │     │ reads the context file       │
   │                     + install-context.json ───┼────►│                              │
   │ SkillSourcesMove (once)   SkillPromptBuilder  │     └──────────────────────────────┘
   │ Settings › Skills (G4–G7), AgentProfileCard,      │
   │ BuildTeam, RoleFormDialog, Library root       │
   └───────────────────────────────────────────────┘
```

### 5.2 How the pieces meet

1. **The AI tool** reads `InstallSkillPack.md`, fetches, writes pack, catalog and Source files,
   runs `huddle-packs validate`, and adds a row to SKILLS.md.
2. **`RegistryWatcher`** sees the change (§8.5), runs the reader, resolver and checks, and
   publishes a new `SkillRegistry` snapshot.
3. **The UI** subscribes to `SkillRegistry.Changed` and redraws: a new row shows **New**, a
   failed one **Broken**.
4. **The Human chooses** a Pack or Skill. `SkillCopier` copies each Skill into `Skills/`
   (§8.6); only then does the Agent's file change, and `SkillStore` sees the new copies as
   it does today.
5. **`InstallFileWriter`** rewrites `InstallSkillPack.md` and the context file whenever roles,
   installed Skills or the registry change, at most once a second.

### 5.3 Contracts fixed before any stream starts

These are frozen in task P1 so the library, the command and the app can be built in parallel:

| Contract | Defined in | Used by |
| --- | --- | --- |
| `Finding(Severity, Code, File, Message)` and the check codes of §6.3 | `Huddle.SkillPacks` | Checks, `huddle-packs` output, the UI's **Broken** reasons |
| `CheckContext` and the context file's JSON shape (§7.2) | `Huddle.SkillPacks` | `InstallFileWriter` writes it; `huddle-packs` reads it |
| The pack, catalog and `source.json` schemas, `schemaVersion: 1` (§7.2) | `Huddle.SkillPacks` | Everything |
| `ResolvedRegistry` and the item states of §8.3 | `Huddle.SkillPacks` | `SkillRegistry`, the UI, `CardResolver` |

---

## 6. System Components

### 6.1 `Huddle.SkillPacks` library overview

**Purpose.** Hold everything Huddle and `huddle-packs` must do the same way: read SKILLS.md and
the files it points at, resolve them into Packs, catalogs and Skills with a state each, and
run the checks. It has no I/O of its own beyond the `IFileReader` it is handed, no logging, no
clock and no ASP.NET, so the same code runs in the web app, in the console command and in
tests.

**Project.** `src/Huddle.SkillPacks`, assembly and root namespace `Agency.Huddle.SkillPacks`,
one package reference (Markdig, already in `Directory.Packages.props`). It must not reference
`Huddle.App`, `Huddle.Contracts` or the command ([§5.1](#51-projects-and-components)). Every
type listed here is `public`, because two other assemblies use it; each public method that takes
a reference-type parameter guards it with a throw helper (`CA1062`). Everything else is
`internal` and `sealed`.

**Folders and namespaces** (file-scoped namespaces match the folder; no folder is named
`Registry` or `Checks`, because a namespace would then collide with the type of that name):

| Folder / namespace suffix | Types | Section |
| --- | --- | --- |
| (root, `Agency.Huddle.SkillPacks`) | `IFileReader`, `PhysicalFileReader`, `InstallContext` | [§6.2](#62-registry-model-and-file-readers), [§6.3](#63-the-checks) |
| `Reading` | `RegistryReader`, `Registry`, `RegistryRow`, `RowKind`, `LineSpan`, `PathEntries`, `PathEntry` | §6.2 |
| `Files` | `PackFile`, `PackSkill`, `CatalogFile`, `CatalogEntry`, `SourceFile`, `SourceKind`, `SourceStatus`, `SignInMode`, `ParseResult<T>` | §6.2 |
| `Resolving` | `RegistryResolver`, `ResolvedRegistry`, `PackEntry`, `SkillEntry`, `ResolvedCatalog`, `ResolvedSource`, `ChangedPaths`, `SourceId`, `ItemState` | §6.2, [§8.3](#83-resolving-packs-catalogs-and-skills) |
| `Folders` | `SkillFolderHash`, `SkillValidator`, `SkillReviewScanner` (moved from `Huddle.App`) | §6.3 |
| `Checking` | `Checks`, `CheckContext`, `CheckOptions`, `CheckResult`, `Finding`, `Severity` | §6.3 |

**Data flow inside the library.** The resolver loads and structures; `Checks.Run` is the one
place that judges, and the one place that returns findings and states.

```text
SKILLS.md ─► RegistryReader.Load (Length first, text only if ≤ 1 MB) ─► Registry (rows + findings)
                                            │
   pack / catalog / source.json text ─► PackFile.Parse / CatalogFile.Parse / SourceFile.Parse
                                            │            (shape findings: P001–P003, D001, D002, O001, O003)
                                            ▼
                     RegistryResolver.Resolve(Registry, IFileReader, CheckContext)
                                            │   loads files, resolves paths, catalogs and names, hashes folders;
                                            │   emits only R003, the parsers' findings and the Source-id check; assigns NO state
                                            ▼
                                     ResolvedRegistry
                                            │
                     Checks.Run(ResolvedRegistry, CheckContext, CheckOptions, IFileReader)
                                            │   includes the resolver's findings once, runs every other check, computes all states
                                            ▼
                            CheckResult(Findings, States)
```

**Rules that hold for every type here.**

1. **Pure.** Given the same text, the same `IFileReader` answers and the same `CheckContext`,
   the output is the same. Time comes in as a `DateTimeOffset` on `IFileReader`'s answers, never
   from a clock.
2. **Never throws for bad content.** A malformed file, a missing folder or an unreadable path
   becomes a `Finding`. Only a caller bug (a `null` argument) throws.
3. **Findings carry the file, absolute.** `Finding.File` is always an absolute path (SKILLS.md's
   own path for a finding about the registry). Shortening it for display, relative to the Skill
   Sources folder, is the caller's job: `huddle-packs` does it for its lines
   ([§6.4](#64-the-huddle-packs-command)) and the UI does it on a card.
4. **Immutable.** Records with `IReadOnlyList<T>` members; no list is shared with a caller.

**Dependencies on §8.** Reading: [§8.1](#81-reading-skillsmd). Paths: [§8.2](#82-per-computer-paths).
Resolving and states: §8.3. Hashing: [§8.4](#84-noticing-an-update).

---

### 6.2 Registry model and file readers

**Purpose.** Turn text into typed records and say what is wrong with it, nothing more. The
readers know the shapes of SKILLS.md, `.skillpack.json`, `.skillcatalog.json` and `source.json`;
they know nothing about whether a path leads anywhere (the resolver and checks do).

**Inputs / outputs.**

| Reader | In | Out |
| --- | --- | --- |
| `RegistryReader.Load(IFileReader files, string registryDir, string computer)` | The folder SKILLS.md is in | `Registry`. Asks `files.FileExists` (a missing file is `R001`), then `files.Length` **first**: over 1 MB it reads nothing and returns a `Registry` with `R011` and no rows. Otherwise it reads the text and calls `Read` |
| `RegistryReader.Read(string text, string registryDir, string computer)` | The text of SKILLS.md (at most 1 MB, because only `Load` supplies it), the folder it is in, this computer's name | `Registry` |
| `PackFile.Parse(string json, string fileName)` | The text of a pack file and its file name (for `P003`) | `ParseResult<PackFile>` |
| `CatalogFile.Parse(string json, string fileName)` | The same for a catalog file | `ParseResult<CatalogFile>` |
| `SourceFile.Parse(string json, string fileName)` | The same for `source.json` | `ParseResult<SourceFile>` |
| `PathEntries.Parse(string cell)` / `PathEntries.FromJson(JsonElement)` | A SKILLS.md cell, or a JSON `path` value | `PathEntries` |

#### Types

`IFileReader` (root namespace) is the only way the library touches a disk. `PhysicalFileReader`
implements it over `System.IO`; tests use `InMemoryFileReader` (in `tests/Huddle.Tests`).

```csharp
namespace Agency.Huddle.SkillPacks;

public interface IFileReader
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    byte[] ReadAllBytes(string path);
    long Length(string path);
    DateTimeOffset LastWriteTime(string path);
    IReadOnlyList<string> EnumerateFiles(string directory, bool recursive);
    IReadOnlyList<string> EnumerateDirectories(string directory);

    /// <summary>The file attributes, for <see cref="FileAttributes.ReparsePoint"/> (S004).</summary>
    FileAttributes Attributes(string path);
}
```

```csharp
namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>Which table a row came from.</summary>
public enum RowKind { Pack, Catalog }

/// <summary>First and last line of a row in SKILLS.md, 1-based and inclusive (§8.1 step 7).</summary>
public readonly record struct LineSpan(int FirstLine, int LastLine);

/// <summary>One entry of a path cell: a plain path, or a path for one computer.</summary>
/// <param name="Computer"><see langword="null"/> for a plain entry (every computer); otherwise the computer's name as written.</param>
/// <param name="Path">The text of the path, trimmed, not yet resolved or checked.</param>
public sealed record PathEntry(string? Computer, string Path);

/// <summary>All the entries a path cell or a JSON <c>path</c> holds (§8.2).</summary>
public sealed record PathEntries(IReadOnlyList<PathEntry> Entries)
{
    public static PathEntries Empty { get; }
    public static PathEntries Parse(string cell);               // SKILLS.md cell: one entry per line
    public static PathEntries FromJson(JsonElement element);    // string, or { "DESK-01": "...", "*": "..." }

    /// <summary>The path text for <paramref name="computer"/>: its own entry, else the plain/"*" entry, else null.
    /// An entry whose text is empty also gives null (a Skill not fetched yet).</summary>
    public string? Resolve(string computer);

    /// <summary>Resolve, then make absolute against <paramref name="baseDirectory"/> with Path.GetFullPath; accepts / or \.</summary>
    public string? ResolveFull(string computer, string baseDirectory);

    public bool HasEntryFor(string computer);        // true if own entry or a plain entry exists, even if empty (R009 is "false")
    public bool HasDuplicateComputer { get; }        // R010
    public bool HasTwoPlainEntries { get; }          // R013
    public bool IsEmpty { get; }                     // no entry at all
}

/// <summary>One row of the Packs or Catalogs table.</summary>
/// <param name="Kind">Which table.</param>
/// <param name="Index">0-based position in its table; the tie-breaker everywhere ("the first row wins").</param>
/// <param name="LocalPath">The <c>Local path</c> cell, split into entries.</param>
/// <param name="OriginUrl">The <c>Origin URL</c> cell as text (a link contributes its URL), or null when empty or absent.</param>
/// <param name="LastUpdatedText">The <c>Last updated</c> cell as written, or null.</param>
/// <param name="LastUpdated">The cell parsed as <c>yyyy-MM-dd</c>, or null when absent or not a date (R006).</param>
/// <param name="Notes">The <c>Notes</c> cell, line breaks joined with a space, or null.</param>
/// <param name="Span">The row's lines in SKILLS.md; Remove deletes exactly these lines (§8.8). <c>default</c> (0, 0) for a virtual row.</param>
/// <param name="IsVirtual">True for a row that is not in SKILLS.md: <c>huddle-packs validate &lt;path&gt;</c> makes one for a pack or catalog file that no row points at yet (§6.4). It has no source span, so Remove never sees it.</param>
public sealed record RegistryRow(
    RowKind Kind,
    int Index,
    PathEntries LocalPath,
    string? OriginUrl,
    string? LastUpdatedText,
    DateOnly? LastUpdated,
    string? Notes,
    LineSpan Span,
    bool IsVirtual = false)
{
    /// <summary>A virtual row for <paramref name="path"/>: one plain path entry, no other cell, no span, <see cref="Index"/> after the real rows of its table.</summary>
    public static RegistryRow Virtual(RowKind kind, int index, string path);
}

/// <summary>What <see cref="RegistryReader.Read"/> found in SKILLS.md.</summary>
/// <param name="Directory">The folder SKILLS.md is in; relative paths resolve against it.</param>
/// <param name="Packs">The Packs table's rows, in file order. Empty when the table is missing.</param>
/// <param name="Catalogs">The Catalogs table's rows, in file order.</param>
/// <param name="Findings">R001, R002, R003 (empty path), R006, R007, R011, R012 and the path findings R010 and R013.</param>
public sealed record Registry(
    string Directory,
    IReadOnlyList<RegistryRow> Packs,
    IReadOnlyList<RegistryRow> Catalogs,
    IReadOnlyList<Finding> Findings);
```

The Remove code of [§6.12](#612-removing-and-hiding) only reads `Span`. `Registry` has no mutating
members: a removal is a text edit followed by a new read. `Registry` has `Packs` and `Catalogs`
and nothing called `Rows`; a row is addressed by its table (`Kind`) and its `Index` in that table,
and file order across both tables is `Span.FirstLine`. A virtual row is added by copying the
registry (`registry with { Packs = [.. registry.Packs, row] }`) before it is resolved.

**Per-computer entries.** A row keeps its entries exactly as written, so one SKILLS.md read
serves every computer and a test can ask any computer name. `Resolve("DESK-01")` and
`Resolve("LAPTOP")` on the same row return different paths. Only the resolver, which is given
this computer's name through `CheckContext.ComputerName`, picks one.

**Row identity.** `RegistryRow` has no id of its own. Two stable identities are derived:

| Identity | Form | Used for |
| --- | --- | --- |
| Row key | `"packs:"` or `"catalogs:"` (lowercase) plus the first entry's path, made absolute against `Directory`, upper-cased with `ToUpperInvariant()`, `/` turned into `\` | The `seen` list ([§6.5](#65-skillregistry-and-registrywatcher)) only; the same path on two computers can differ, and that is fine because the list is per Huddle. A Skill's key is `"skill:"` plus its name |
| Pack id / catalog id | The `id` inside the file | Everything else: the keys of `CheckResult.States` (`pack:<id>`, `catalog:<id>`, `skill:<name>`; `row:pack:<index>` or `row:catalog:<index>` for a file too broken to give an id, [§6.3](#63-the-checks)), and the only identity an Agent's `_packs` line uses |

#### The three file records

```csharp
namespace Agency.Huddle.SkillPacks.Files;

/// <summary>The outcome of reading one file: the record when its shape is usable, and the findings either way.</summary>
public sealed record ParseResult<T>(T? Value, IReadOnlyList<Finding> Findings) where T : class
{
    public bool Ok => this.Value is not null;
}

/// <summary>A Skill inside a Pack: its own folder, or a named entry of a catalog. Exactly one of Path and Catalog is set (P009).</summary>
/// <param name="Name">The Skill name; must follow the Skill name rule.</param>
/// <param name="Path">Where the folder is, absolute, relative to the pack file, or per computer. Null when <paramref name="Catalog"/> is set.
/// <c>"path": ""</c> is a present <c>path</c> (so P009 is satisfied) and means <b>Not downloaded</b>; give <paramref name="Origin"/> with it.</param>
/// <param name="Catalog">The id of a catalog in SKILLS.md that lists this Skill. Null when <paramref name="Path"/> is set.</param>
/// <param name="Origin">The pinned tree URL when the Pack gives its own path; ignored when <paramref name="Catalog"/> is set.</param>
public sealed record PackSkill(string Name, PathEntries? Path, string? Catalog, string? Origin);

/// <summary>A <c>*.skillpack.json</c> file (§7.2).</summary>
public sealed record PackFile(
    int SchemaVersion,
    string Id,
    string Version,
    string Title,
    string Source,
    IReadOnlyList<string> AppliesTo,
    string? ConsultWhenAdds,
    IReadOnlyList<PackSkill> Skills)
{
    public const int KnownSchemaVersion = 1;
    public static ParseResult<PackFile> Parse(string json, string fileName);
}

/// <summary>One Skill a catalog lists. <c>Path</c> is empty until the Skill is fetched.</summary>
public sealed record CatalogEntry(
    string Name,
    string Description,
    string Origin,
    string? Publisher,
    string? License,
    string? Category,
    string? SubCategory,
    IReadOnlyList<string> Requires,
    PathEntries Path);

/// <summary>A <c>*.skillcatalog.json</c> file (§7.2).</summary>
public sealed record CatalogFile(
    int SchemaVersion,
    string Id,
    string Title,
    string Source,
    IReadOnlyList<CatalogEntry> Skills)
{
    public static ParseResult<CatalogFile> Parse(string json, string fileName);
}

public enum SourceKind { Github, Git, Local }
public enum SourceStatus { Active, Deprecated, Withdrawn, Moved }
public enum SignInMode { None, Required }

/// <summary>A <c>sources/&lt;source&gt;/source.json</c> file (§7.2). Written by the AI tool; Huddle reads it.
/// <c>Ref</c>, <c>Commit</c> and <c>LocalPath</c> are optional. The move writes no <c>commit</c> because a catalog's entries can be pinned to different commits,
/// so O002 is raised for a <c>github</c> or <c>git</c> Source without one only when some pack or catalog entry naming the Source fails D004.</summary>
public sealed record SourceFile(
    int SchemaVersion,
    SourceKind Kind,
    string? Url,
    string? Ref,
    string? Commit,
    PathEntries? LocalPath,
    DateTimeOffset FetchedAt,
    string FetchedBy,
    SignInMode SignIn,
    SourceStatus Status,
    string? StatusReason,
    string? MovedTo)
{
    public static ParseResult<SourceFile> Parse(string json, string fileName);
}
```

**Reading rules for all three.**

1. **Tolerant JSON.** UTF-8, a BOM is accepted, comments and trailing commas are not. Property
   names match `OrdinalIgnoreCase`. Unknown properties are ignored, so a newer AI tool's extra
   keys do not break an older Huddle.
2. **`schemaVersion`** must be present and equal to `KnownSchemaVersion` (1). Anything else is
   `P001` (`D001` for a catalog, `O001` for `source.json`) and `Value` is `null`.
3. **Shape findings, produced here and only here:**

   | Code | Produced by | When |
   | --- | --- | --- |
   | `P001` | `PackFile.Parse` | Not JSON, not an object, or an unknown `schemaVersion` |
   | `P002` | `PackFile.Parse` | `id`, `version`, `title`, `source` or `skills` is absent or blank |
   | `P003` | `PackFile.Parse` | `id` breaks the id rule, or `fileName` is not `<id>.skillpack.json` |
   | `P008`, `P009` (shape part) | `PackFile.Parse` | Duplicate Skill names inside the file; a Skill with both or neither of `path` and `catalog`. The part of `P009` that needs the catalog is the resolver's |
   | `D001` | `CatalogFile.Parse` | Not JSON, unknown `schemaVersion`, `id`, `source` or `skills` absent, or `id` does not match `fileName` |
   | `D002` | `CatalogFile.Parse` | An entry lacks `name`, `description` or `origin`, breaks the name rule, or repeats a name. The entry is dropped and the rest load |
   | `O001` | `SourceFile.Parse` | Not JSON, unknown `schemaVersion`, an unknown `kind`, `status` or `signIn`, or a missing required field (`kind`, `fetchedAt`, `fetchedBy`, `signIn`, `status`; `url` for `github` and `git`). `ref`, `commit` and `localPath` are never required |
   | `O003` | `SourceFile.Parse` | `status` is not `active` and `statusReason` is blank; `moved` and `movedTo` is blank |

   The §6.3 table owns the wording of each message; this section owns which reader raises it.
   Every other code (`R003`–`R009`, `P004`–`P007`, `D003`–`D005`, `S…`, `O002`, `O004`, `O005`,
   `X…`) needs a second file, a folder or the context, and is raised by `Checks.Run` or the
   resolver.
4. **A bad entry does not take the file down** (`D002`, and a bad Skill in a pack is `P002` for
   that Skill with the Pack still `Value != null`). Pack-level errors (`P001`–`P003`) do:
   `Value` is `null` and the row is **Broken**.
5. **`path` is read by `PathEntries.FromJson`**: a string is one plain entry; an object is one
   entry per key, with `"*"` as the plain entry; an absent key or `null` is no entry; any other
   JSON type is `P002` (`D002` in a catalog).
6. **Dates** (`fetchedAt`) are ISO-8601 with an offset, parsed with `CultureInfo.InvariantCulture`.

#### Resolved records

```csharp
namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>The state a Pack, catalog or Skill is shown in (§8.3, §8.4, vision "The Skills page at a glance").</summary>
public enum ItemState
{
    /// <summary>Passes the checks and is downloaded; no Agent holds it and nothing changed.</summary>
    Available,

    /// <summary>The files at the pointer differ from the copy in Skills/ (§8.4).</summary>
    UpdateAvailable,

    /// <summary>The file is fine but a Skill's path is empty, leads nowhere, or has no entry for this computer.</summary>
    NotDownloaded,

    /// <summary>The Source marks it deprecated. Still choosable.</summary>
    Deprecated,

    /// <summary>The Source pulled it. New hires cannot take it.</summary>
    Withdrawn,

    /// <summary>The pointer leads nowhere or an error check fails.</summary>
    Broken,

    /// <summary>Held by one or more Agents. NEVER produced by the library: it needs Agent files.
    /// <c>SkillRegistry</c> derives it for display.</summary>
    InUse,

    /// <summary>The row appeared since the Human last opened its tab. NEVER produced by the library: it is a
    /// UI-only overlay.</summary>
    New,
}
```

**Why `New` and `InUse` are in the enum but never produced by the library.** The enum is the
vocabulary of the chip a card wears, so it lists all eight states of the vision's table. The
library knows files, not people: it cannot know which rows the Human has already looked at, and
it cannot know which Agents hold a Skill. `New` depends on the viewer (the `seen` list in
`{DataDir}/skill-sources.json`, [§7.2](#72-schemas)), and `InUse` on the Agent files. If either
were computed in the library, `huddle-packs` would print a state that differs between two Huddles
reading one folder. So `Checks.Run` produces the six states a pure read can decide, in
`CheckResult.States` ([§6.3](#63-the-checks)), and
`SkillRegistry.DisplayState` ([§6.5](#65-skillregistry-and-registrywatcher)) lays `InUse` and
`New` over an `Available` item. Precedence, first match wins:
`Broken` > `Withdrawn` > `NotDownloaded` > `UpdateAvailable` > `Deprecated` > `InUse` > `New` > `Available`.
Update, deprecation and withdrawal are therefore never hidden behind a quieter chip, and a New
item that is already in use shows `InUse`.

```csharp
/// <summary>A Pack as resolved against this computer.</summary>
/// <param name="Row">The Packs row that led to it.</param>
/// <param name="FilePath">The full path the row resolved to; null when it has no path on this computer.</param>
/// <param name="File">The parsed file; null when the row is Broken or Not downloaded at file level.</param>
/// <param name="SkillNames">The names of the Pack's Skills, in file order.</param>
/// <param name="Source">The Source id from the file's <c>source</c> field (e.g. <c>github.com/anthropics/skills</c>).</param>
/// <param name="MissingSkills">Skills that are Not downloaded, so a card can name them.</param>
/// <param name="Findings">The loading findings that concern this Pack or its file (not its Skills' own). <c>Checks.Run</c> adds the rest; states are only in <c>CheckResult.States</c>.</param>
public sealed record PackEntry(
    RegistryRow Row,
    string? FilePath,
    PackFile? File,
    string Id,
    string Version,
    string Title,
    string Source,
    IReadOnlyList<string> AppliesTo,
    string? ConsultWhenAdds,
    IReadOnlyList<string> SkillNames,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<Finding> Findings);

/// <summary>A catalog as resolved against this computer.</summary>
public sealed record ResolvedCatalog(
    RegistryRow Row,
    string? FilePath,
    CatalogFile? File,
    string Id,
    string Title,
    string Source,
    IReadOnlyList<string> SkillNames,
    int DownloadedCount,
    IReadOnlyList<Finding> Findings);

/// <summary>A Source, as read from <c>sources/&lt;id&gt;/source.json</c>.</summary>
/// <param name="Id">The Source id as pack and catalog files write it, with <c>/</c> separators.</param>
/// <param name="File">Null when the Source has no valid <c>source.json</c> (O001).</param>
/// <param name="HasNotes">Whether <c>NOTES.md</c> exists.</param>
public sealed record ResolvedSource(string Id, SourceFile? File, bool HasNotes, IReadOnlyList<Finding> Findings);

/// <summary>One Skill, once per distinct name in the whole registry (§8.3 step 4).</summary>
/// <param name="Name">The Skill name; also the folder name in <c>Skills/</c>.</param>
/// <param name="FolderPath">The full path of the Skill's folder on this computer; null when Not downloaded for lack of a path.</param>
/// <param name="Origin">The pinned URL, from the catalog entry or the Pack Skill; the identity two places must share (D003).</param>
/// <param name="Providers">Where the Skill is listed, as <c>pack:&lt;id&gt;</c> or <c>catalog:&lt;id&gt;</c>, in registry order. The first provider's data wins.</param>
/// <param name="PointerSha256">The hash of the folder at the pointer (§8.4); null when not downloaded or not hashed yet.</param>
/// <param name="InstalledSha256">The <c>sha256</c> in <c>Skills/&lt;name&gt;/.source.json</c> when that copy exists and has the same origin; otherwise null.</param>
public sealed record SkillEntry(
    string Name,
    string Description,
    string? Publisher,
    string? License,
    string? Category,
    string? SubCategory,
    IReadOnlyList<string> Requires,
    string Origin,
    string? Source,
    string? FolderPath,
    IReadOnlyList<string> Providers,
    string? PointerSha256,
    string? InstalledSha256,
    IReadOnlyList<Finding> Findings);

/// <summary>Everything one read of the registry produced.</summary>
public sealed record ResolvedRegistry(
    Registry Registry,
    IReadOnlyList<PackEntry> Packs,
    IReadOnlyList<ResolvedCatalog> Catalogs,
    IReadOnlyList<SkillEntry> Skills,
    IReadOnlyList<ResolvedSource> Sources,
    IReadOnlyList<Finding> Findings,
    DateTimeOffset ReadAt)
{
    public static ResolvedRegistry Empty { get; }

    public PackEntry? FindPack(string id);                          // OrdinalIgnoreCase on id; first row wins
    public SkillEntry? FindSkill(string name);                      // Ordinal on name
    public IReadOnlyList<PackEntry> PacksForRole(string roleId);   // AppliesTo contains roleId, any state, registry order
    public ResolvedSource? FindSource(string id);
    public IReadOnlyList<PackEntry> PacksUsingCatalog(string catalogId);   // Packs with a Skill that names this catalog (Remove a catalog, §6.12.3)
    public IReadOnlyList<PackEntry> PacksOfSource(string sourceId);        // Packs whose file names this Source (Sources tab, Remove a Source)
    public IReadOnlyList<ResolvedCatalog> CatalogsOfSource(string sourceId);
    public string RowKey(PackEntry pack);                           // the identity of §6.2 "Row identity"
    public string RowKey(ResolvedCatalog catalog);
}
```

```csharp
public sealed class RegistryResolver
{
    /// <summary>Resolves every row of <paramref name="registry"/> against this computer.</summary>
    /// <remarks>Structural only (§8.3): loads files, resolves paths, catalog references and names, and hashes folders.
    /// It raises only the findings it cannot avoid while loading (R003 and the parsers' P001–P003, P008, P009 shape, D001,
    /// D002, O001, O003) and assigns no state. <paramref name="context"/> supplies the computer name and the
    /// <c>Skills/</c> folder for <see cref="SkillEntry.InstalledSha256"/>.</remarks>
    public static ResolvedRegistry Resolve(Registry registry, IFileReader files, CheckContext context);
}
```

`ResolvedRegistry.Findings` holds the loading findings only, in a stable order: registry-level
first, then each Pack in row order, each catalog, each Source, then each Skill in the order of
its first provider. `Checks.Run` copies them once into `CheckResult.Findings` and adds the rest;
`huddle-packs validate` prints `CheckResult.Findings`, and the UI shows the subset on each item.

**Errors.** None thrown for content. A caller bug throws `ArgumentNullException`. An
`IFileReader` that throws `IOException` mid-read is treated as the file being absent (the same
as `SkillProvenance.TryRead` does today).

**Tests to write** (xunit v3, in `tests/Huddle.Tests/SkillPacks/Reading/` and `…/Resolving/`; there
is no separate library test project):

| Test class | Pins |
| --- | --- |
| `RegistryReaderTests` | §8.1: tables found by heading, columns by header, cell flattening (code spans, `<br>`), row spans, R001/R002/R011/R012 |
| `PathEntriesTests` | §8.2: computer entry beats plain, drive letters are never computer names, `"*"`, R010, R013 |
| `PackFileTests`, `CatalogFileTests`, `SourceFileTests` | Reading rules 1–6: unknown `schemaVersion`, `"path": ""` is Not downloaded, optional `ref`/`commit`/`localPath`, a bad catalog entry drops alone |
| `RegistryResolverTests` | §8.3: catalog references, first row wins, D003, D005, no states assigned, same input gives the same output |

---

---

### 6.3 The checks

#### What it is

One static entry point, `Checks.Run`, in `Huddle.SkillPacks`. Given the registry as the
resolver left it ([§8.3](#83-resolving-packs-catalogs-and-skills)) and what this Huddle knows
about itself, it returns every finding, the state each item ends in and a summary. Huddle
(`RegistryWatcher`, [§6.5](#65-skillregistry-and-registrywatcher)) and `huddle-packs`
([§6.4](#64-the-huddle-packs-command)) call the same method, so their findings cannot drift
(principle 2). It reads files and writes nothing; it opens no network connection.

#### Types

```csharp
namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>How serious a finding is. An error makes its item Broken; a warning is shown on the details card.</summary>
public enum Severity { Warning = 1, Error = 2 }

/// <summary>One thing a check found. The four fields are the whole public contract.</summary>
/// <param name="Severity">Warning or error, after <see cref="CheckOptions.Strict"/> has been applied.</param>
/// <param name="Code">A code from the table below, such as <c>S004</c>.</param>
/// <param name="File">The absolute path of the file or folder the finding is about; SKILLS.md for a finding about the registry.</param>
/// <param name="Message">Specific to this finding: names the Skill, role, column or limit. One line.</param>
public sealed record Finding(Severity Severity, string Code, string File, string Message);

/// <summary>What the checks need to know about one Huddle. Huddle builds it from its services; huddle-packs reads it from install-context.json.</summary>
public sealed record CheckContext(
    string HuddleVersion,
    int SchemaVersion,
    string ComputerName,
    string DataDir,
    string SkillSourcesDir,
    string SkillsDir,
    IReadOnlyList<RoleInfo> Roles,
    IReadOnlyList<InstalledSkill> InstalledSkills,
    IReadOnlyList<string> AppToolNames,
    DateTimeOffset Written)
{
    /// <summary>The context <c>huddle-packs</c> runs with: one to one with the context file Huddle wrote (§6.7).</summary>
    public static CheckContext From(InstallContext installContext);
}

/// <summary>A role a Pack's <c>appliesTo</c> may name. <paramref name="SuggestedPacks"/> are the Pack ids the hiring pool suggests for it (check R008).</summary>
public sealed record RoleInfo(string Id, string Title, string Description, IReadOnlyList<string> SuggestedPacks);

/// <summary>A Skill folder in <c>Skills/</c>. <paramref name="BuiltIn"/> Skills ship with Huddle; the rest were copied from a pointer or written by the Human.</summary>
/// <paramref name="Origin"/> and <paramref name="Sha256"/> are null for built-in Skills and for Skills the Human wrote.
public sealed record InstalledSkill(string Name, string? Origin, string? Sha256, bool BuiltIn);

/// <summary>Switches for one run.</summary>
/// <param name="Strict">Raises S001 and R009 from warning to error. The AI tool sets it on what it just added.</param>
/// <param name="OnlyPath">Check only the rows whose resolved file is this path or lies under it (<c>validate &lt;path&gt;</c>); <see langword="null"/> checks all. Registry-level findings (R001, R002, R011, R012, X002) are always reported; R008 is skipped when this is set.</param>
/// <param name="ToolVersion">The version of the program calling Run, for X002. <see langword="null"/> skips X002 (Huddle itself passes null).</param>
public sealed record CheckOptions(
    bool Strict = false,
    string? OnlyPath = null,
    string? ToolVersion = null);

/// <summary>The state the checks leave an item in. Huddle refines Usable into New, Available, In use, Update available, Deprecated and Withdrawn (§8.3, §8.4).</summary>
public enum ItemCheckState { Usable, NotDownloaded, Broken }

/// <summary>One SKILLS.md row as checked: its table and index there (§6.2), and its first line in SKILLS.md (0 for a virtual row).</summary>
public sealed record RowCheck(RowKind Kind, int Index, string LocalPath, int Line, ItemCheckState State, int Errors, int Warnings);

/// <summary>The counts of the <c>PACKS-VALIDATE</c> line (§6.4). Skills counts distinct Skill names reached by the rows checked.</summary>
public sealed record CheckSummary(int Rows, int Packs, int Skills, int Errors, int Warnings);

/// <summary>Everything Run returns. <paramref name="States"/> is keyed <c>pack:&lt;id&gt;</c>, <c>catalog:&lt;id&gt;</c>, <c>skill:&lt;name&gt;</c> or, for a file too broken to read an id from, <c>row:pack:&lt;index&gt;</c> / <c>row:catalog:&lt;index&gt;</c>. It is the only place states are computed.</summary>
public sealed record CheckResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<RowCheck> Rows,
    IReadOnlyDictionary<string, ItemCheckState> States,
    CheckSummary Summary);

public static class Checks
{
    /// <summary>Runs every check once and returns the findings in the order of the rules below.</summary>
    /// <param name="files">The same reader the resolver used; the S checks list and read Skill folders through it.</param>
    public static CheckResult Run(
        ResolvedRegistry registry,
        CheckContext context,
        CheckOptions options,
        IFileReader files,
        CancellationToken cancellationToken = default);
}
```

`Run` guards `registry`, `context`, `options` and `files` with `ArgumentNullException.ThrowIfNull`.
It copies the loading findings the reader and resolver produced (`ResolvedRegistry.Findings`)
into its result exactly once and adds its own, so a caller sees each finding once and cannot tell
which stage produced a code. A test passes an `InMemoryFileReader`.

**`CheckContext` and install-context.json.** The fields match the context file
([§6.7](#67-installskillpackmd-and-the-context-file), [§7.2](#72-schemas)) one to one:

| Field | install-context.json key | Used by |
| --- | --- | --- |
| `HuddleVersion` | `huddleVersion` | X002 |
| `SchemaVersion` | `schemaVersion` | The reader refuses a file with a higher number (exit 2, §6.4) |
| `ComputerName` | `computer` | Per-computer paths (R009, R010, R013, S001) |
| `DataDir` | `dataDir` | Where `huddle-packs` looks for the rest |
| `SkillSourcesDir` | `skillSourcesDir` | The Source files O001–O005 read, and the display of paths |
| `Roles` | `roles[]`: `id`, `title`, `description`, optional `suggestedPacks` | P005, R008, `huddle-packs roles` |
| `InstalledSkills` | `installed[]`: `name`, `origin`, `sha256`, `builtIn` | S005, S006, `huddle-packs installed` |
| `AppToolNames` | `appTools[]` | S011: the tools only built-in Skills may grant (today `SkillGrants.Grantable`) |
| `SkillsDir` | `skillsDir` | Where the copies are; S006 and `huddle-packs installed` |
| `Written` | `written` | Shown by `--version`-style diagnostics only; no check depends on it |

`suggestedPacks` is optional so that R008 can run in `huddle-packs` too; a context file without
it never raises R008.

#### Rules that hold for every run

1. **Order of findings is deterministic.** Sort by (a) the row's first line in SKILLS.md
   (`Span.FirstLine`; virtual rows last), with findings about the registry itself (no row) first; (b) `File`, ordinal ignoring case;
   (c) `Code`, ordinal; (d) `Message`, ordinal. Row order is SKILLS.md's own order, with Packs
   and Catalogs interleaved as they appear. Nothing depends on the clock, directory
   enumeration order or hash order; the same inputs give the same list byte for byte.
2. **One finding per fact.** A Skill reached by two rows (a Pack and the catalog that names
   it) is checked once and its findings belong to the first row, in file order, that reaches
   it. Findings about a Skill's files use the Skill's folder as `File`, or the file inside it
   when the finding is about one file (S007, X001).
3. **`File` is always absolute.** Display rules (relative to the Skill Sources folder) belong
   to the caller ([§6.4](#64-the-huddle-packs-command), "Output lines").
4. **Codes never change meaning, and a retired code is not reused.** A check that stops
   making sense is removed from the table and its code goes on the retired list in
   `CheckCatalog.Retired`; a check whose meaning shifts gets a new code. Severity may change
   between releases only by deliberate edit of this table. `CheckCatalogTests` pins every
   code in use and every retired code.
5. **Strict.** `Strict` raises exactly R009 and S001 to error. Nothing else changes.
6. **Errors set state.** The item a finding belongs to takes the state in the table's *State*
   column; the worst state wins (Broken over Not downloaded over Usable). The state of a Pack
   follows [§8.3](#83-resolving-packs-catalogs-and-skills) step 6.
7. **Not downloaded is not noise.** A catalog Skill whose `path` is an empty string, or whose
   path object has `"*": ""`, is Not downloaded and produces **no** finding, because the
   catalog the move writes starts with 263 of them ([§6.13](#613-the-move)). S001 fires for a **Pack's** Skill with no usable
   path (a Pack is chosen whole), and for any Skill whose path is filled in and leads to no
   folder. A per-computer path with no entry for this computer is R009, never S001.
8. **Fetched Skill.** Every Skill the checks see was reached through the registry (a Pack's
   `path`, a Pack's `catalog` entry, or a catalog entry); that is what "fetched" means.
   Built-in Skills live only in `Skills/`, are never in `ResolvedRegistry`, and are therefore
   never checked, so the S-checks never need an `isFetched` flag. S005 keeps a fetched Skill
   from taking a built-in Skill's name.

#### The check catalog

*Runs on* names what the check reads: **Registry** (SKILLS.md as a whole), **Row**, **Pack
file**, **Catalog file**, **Source** (`source.json`, `NOTES.md`), **Skill** (the folder a path
leads to). *Fix hint* is the text the Human sees under the message on a **Broken** card, a
details card (`G6`) or a problem list; the code follows it in brackets, last, as the guidance
rules require.

| Code | Severity | Runs on | Check | State it causes | Fix hint shown to the Human |
| --- | --- | --- | --- | --- | --- |
| `R001` | Error | Registry | SKILLS.md exists and has a `Packs` table, a `Catalogs` table, or both, each under its own level-2 heading | None; nothing is read | SKILLS.md is missing or has no Packs or Catalogs table. Ask your AI tool to follow InstallSkillPack.md and recreate it, or restore it from a backup. |
| `R002` | Error | Registry | Each table has a `Local path` column | The table's rows are not read | Add a column headed Local path to that table, or ask your AI tool to repair it. |
| `R003` | Error | Row | The row's local path is not empty and, resolved for this computer, leads to a file that exists | Broken | The file is not at that path. If you moved the clone, ask your AI tool to correct the row; if you deleted it, remove the row. |
| `R004` | Error | Row | No two rows resolve to the same file (the first row wins; the later one carries the finding) | The later row: Broken | Two rows point at the same file. Remove the later one. |
| `R005` | Error | Row | A Packs row points at `*.skillpack.json`, a Catalogs row at `*.skillcatalog.json` | Broken | This row is in the wrong table or points at the wrong kind of file. Move it, or fix its path. |
| `R006` | Warning | Row | `Last updated` is empty or a `yyyy-MM-dd` date | None | Write the date as yyyy-MM-dd, for example 2026-10-17. |
| `R007` | Warning | Row | `Origin URL` is empty or an absolute `https`, `ssh` or `git` URL | None | Give the full address, starting with https://. Huddle never opens it. |
| `R008` | Warning | Registry | Every Pack id a role's `suggestedPacks` names is provided by some Packs row | None | The hiring pool suggests a Pack that is not here. Ask your AI tool to add it, or ignore this. |
| `R009` | Warning (error with `--strict`) | Row, Skill | A per-computer path has no entry for this computer and no `*` entry | Not downloaded, here only | This file is not on this computer yet. Copy the fix prompt; your AI tool will fetch it here and add an entry for this computer. |
| `R010` | Error | Row, Skill | A per-computer path names the same computer twice (compared ignoring case) | Broken | Two entries name the same computer. Keep one. |
| `R011` | Error | Registry | SKILLS.md is at most 1 MB | None; nothing is read | SKILLS.md is far larger than a list of files should be. Restore it from a backup; it should hold one row per file, not per Skill. |
| `R012` | Warning | Registry | Only one `Packs` and one `Catalogs` heading (a second one's table is ignored) | None | Merge the second table into the first, or rename its heading. |
| `R013` | Error | Row, Skill | A path holds at most one plain entry (a line with no computer name) | Broken | Two entries apply to every computer. Keep one, or give each its computer's name. |
| `P001` | Error | Pack file | The file is JSON, an object, with a `schemaVersion` this Huddle knows (1) | Broken | The Pack file is not valid JSON, or was written for a newer Huddle. Ask your AI tool to rewrite it with `huddle-packs schema pack`. |
| `P002` | Error | Pack file | `id`, `version`, `title`, `source` and `skills` are present and of the right type | Broken | A required field is missing. Run `huddle-packs schema pack` to see which. |
| `P003` | Error | Pack file | `id` is lowercase letters, digits and single hyphens, and the file is `<id>.skillpack.json` | Broken | Rename the file to match the id, or fix the id. |
| `P004` | Error | Pack file | No earlier Packs row has the same `id` (first row wins) | The later Pack: Broken | Another Pack already uses this id. Give one of them a new id. |
| `P005` | Error | Pack file | Every `appliesTo` entry is a role in this Huddle (`CheckContext.Roles`) | Broken | One of the roles is not in this Huddle. Run `huddle-packs roles` for the list, or add the role first. |
| `P006` | Warning | Pack file | `appliesTo` is not empty | None | No Agent card will offer this Pack. List the roles it is for. |
| `P007` | Warning | Pack file | `consultWhenAdds` is not empty and is at most 200 characters | None | Say in one short phrase when an Agent should reach for this Pack. |
| `P008` | Error | Pack file | No Skill name appears twice in one Pack | Broken | A Skill is listed twice. Remove one entry. |
| `P009` | Error | Pack file | Each Pack Skill gives **exactly one** of `path` and `catalog` (a present `path` counts even when it is `""`, which means Not downloaded); a `catalog` names a catalog in SKILLS.md that lists a Skill of that name | Broken | A Skill in this Pack gives both a path and a catalog, neither, or names a catalog entry that does not exist. Give one, and check the catalog id and Skill name. |
| `D001` | Error | Catalog file | The file is JSON with a known `schemaVersion`, an `id` matching `<id>.skillcatalog.json`, a `source` and a `skills` list | Broken | The catalog file is invalid or its id does not match its file name. Run `huddle-packs schema catalog`. |
| `D002` | Error | Catalog file | Each entry has `name`, `description` and `origin`; `name` is a valid Skill name used once in this catalog | Broken | A catalog entry is incomplete or repeated. Fix the entry named in the message. |
| `D003` | Error | Pack file, Catalog file | No two Packs or catalogs give one Skill name different `origin` strings (compared exactly; the first row's Skill keeps the name) | The later Skill: Broken | Two Sources offer a Skill with this name but from different places. Rename one, or drop it from one Source. |
| `D004` | Warning | Catalog file, Pack file | `origin` names a commit, not a branch (a `/tree/<40 hex digits>/` or `/blob/…` URL, or a local origin) | None | The origin is not pinned to a commit, so the Skill can change under you. Ask your AI tool to pin it. |
| `D005` | Error | Catalog file | No earlier Catalogs row has the same catalog `id` (first row wins) | The later catalog: Broken | Another catalog already uses this id. Give one of them a new id. |
| `S001` | Warning (error with `--strict`) | Skill | A path is filled in and leads to a folder; also raised for a **Pack's** Skill whose path is empty | Not downloaded | This Skill's folder is not on this computer. Copy the prompt; your AI tool will fetch it. |
| `S002` | Error | Skill | The folder has a `SKILL.md`; its frontmatter has a `name` that is valid and equals the folder name, and a `description` of at most 500 characters | Broken | SKILL.md is missing, or its name or description is wrong. The Skill's name must equal its folder name. |
| `S003` | Error | Skill | At most 200 files, and 5 MB in total | Broken | The folder is too big for a Skill. Point at a smaller folder. |
| `S004` | Error | Skill | No symbolic link or junction; no name Windows cannot store (`<>"\|?*`, a reserved device name such as `CON`, a trailing dot or space); no two names that differ only in case | Broken | The folder holds a link or a file name Windows cannot keep. Remove it from the Skill. |
| `S005` | Error | Skill | The name is not one of Huddle's built-in Skills | Broken | This name belongs to a Skill that ships with Huddle. Rename the Skill. |
| `S006` | Error | Skill | No Skill of this name in `Skills/` came from a different origin (compared without the commit, §6.3 below) | Broken | A Skill with this name is already installed from somewhere else. Rename this one, or remove the installed one. |
| `S007` | Warning | Skill | No `.md` file directly in the folder is over 64 KB (65,536 UTF-8 bytes) | None | Agents skip files over 64 KB. Split the file. |
| `S008` | Warning | Skill | The folder holds no files in sub-folders or not ending in `.md` | None | Agents read only the .md files directly in the Skill's folder; the rest is copied but never read. |
| `S009` | Warning | Skill | A licence is declared, is not proprietary, and equals the Source's | None | The licence is missing, proprietary or differs from the Source's. Read it before you share the folder. |
| `S010` | Warning | Skill | No scripts, shell commands, network calls or paths outside the Work Dir | None | The Skill ships scripts or commands. Huddle never runs them, and Agents cannot read them. |
| `S011` | Error | Skill | `tools:` names no App Tool (`CheckContext.AppToolNames`) | Broken | Only Huddle's built-in Skills can grant App Tools. Remove the tool from the `tools:` line. |
| `S012` | Warning | Skill | `description` is at most 300 characters | None | A long description crowds an Agent's Skill list. Shorten it. |
| `O001` | Error | Source | `sources/<source>/source.json` exists for the Source a pack or catalog names, matches the `source.json` schema (`ref`, `commit` and `localPath` are optional) and has a known `kind` | Broken | This Source has no valid source.json. Ask your AI tool to write it; `huddle-packs schema source` shows the format. |
| `O002` | Warning | Source | A `github` or `git` Source has a `commit`. Raised only when the Source has none **and** some pack or catalog entry naming it fails `D004` (a Source whose every entry is pinned to a commit needs no single one) | None | The Source is not pinned to a commit. Ask your AI tool to record it. |
| `O003` | Error | Source | A `deprecated`, `withdrawn` or `moved` status has `statusReason`, and `moved` has `movedTo` | Broken | The Source's status needs a reason (and, if moved, where to). |
| `O004` | Warning | Source | `NOTES.md` exists and is at most 16 KB | None | The Source has no notes, or they are too long to read. Ask your AI tool to write or shorten them. |
| `O005` | Warning | Source | `NOTES.md` opens with front matter naming its `layout` | None | Add `layout:` to the notes' front matter so the next visit starts from it. |
| `X001` | Error | Pack file, Catalog file, Source, Skill | No file holds what looks like a secret ([patterns below](#x001-secret-patterns)) | Broken | A file seems to hold a password or token. Remove it, and change the secret: it may already be copied. |
| `X002` | Warning | Registry | `CheckOptions.ToolVersion` equals `CheckContext.HuddleVersion` | None | huddle-packs and this Huddle differ in version. Use the command line from InstallSkillPack.md. |

Compared with the vision's table, this one adds `R011`–`R013`, `D005`, `S012` and `X002`, makes
`S001` and `R009` errors only with `--strict`, narrows `O002`, widens `P009` and makes `S006`
ignore the commit. [§14](#14-design-notes--rationale) gives the reasons.

#### Existing rules mapped to the new codes

`SkillValidator`, `SkillReviewScanner` and `SkillFolderHash` move from `Huddle.App` into
`Huddle.SkillPacks` ([§6.1](#61-huddleskillpacks-library-overview)). `Checks.Run` calls them
per Skill folder and converts what they return into findings. Nothing is reworded unless the
table says so.

| Today | Where | Becomes | Notes |
| --- | --- | --- | --- |
| Rule 1: no `SKILL.md` ("'x' has no SKILL.md.") | `SkillValidator` | `S002` error | Message kept |
| Rule 3: `name` missing, not matching `\A[a-z0-9]+(?:-[a-z0-9]+)*\z`, or not equal to the folder name | `SkillValidator` | `S002` error | The regex is the "Skill name rule" everywhere in this spec |
| Rule 4: `description` missing, or over 500 characters | `SkillValidator` | `S002` error | |
| Rule 4: `description` over 300 characters | `SkillValidator` | `S012` warning | New code |
| Rule 5: a `tools:` entry **not** in `Grantable` ("not a tool a Skill can grant") | `SkillValidator` | Not reported | Harmless in a fetched Skill |
| Rule 5 inverted: a `tools:` entry that **is** in `Grantable` | None (today it is accepted and grants the tool) | `S011` error | The one behaviour change. The entries are compared with `CheckContext.AppToolNames`, case-insensitively, after splitting on `,` and `;` as today |
| Rule 6: unknown frontmatter key (info) | `SkillValidator` | Not reported | Fetched Skills carry `license`, `version` and `allowed-tools`; an info line per key would bury real findings |
| Rule 7: a `.md` file over 65,536 UTF-8 bytes | `SkillValidator` | `S007` warning | |
| "More than 200 files" (only when supporting files are allowed) | `SkillValidator` | `S003` error | Now always on: every Skill reached by a pointer may carry supporting files |
| Total size 5 MB, `MaxFiles` 200 | `SkillDownloader` (goes) | `S003` error | The downloader is deleted, so the rule moves to a `SkillFolderRules` class in the library |
| Symlinks, `<>"\|?*`, reserved device names, case-only duplicates | `SkillDownloader` (goes) | `S004` error | Same class. A symlink or junction is found with `FileAttributes.ReparsePoint` |
| Sub-folder and non-`.md` files (flat-file rule) | `SkillValidator` rule 7, loosened by `allowSupportingFiles` | `S008` warning | One finding per Skill: *"scripts/ and 9 other files are never read by Agents"* (first sub-folder or file, then the count) |
| `ReviewFlagKind.Script`, `ShellCommand`, `NetworkCall`, `PathOutsideWorkDir` | `SkillReviewScanner` | `S010` warning | One finding per kind per Skill: *"ships scripts: scripts/office/unpack.py and 3 more"*. The scanner's heuristics are unchanged |
| `ReviewFlagKind.NoLicense` | `SkillReviewScanner` | `S009` warning | Part of S009; the rest is below |
| The scanner "only warns; it never blocks" | `SkillReviewScanner` | Unchanged | S010 stays a warning |

`SkillValidator` parses frontmatter with `PersonaFrontmatter.Parse`, which is `internal` to
`Huddle.App` (961 lines) and cannot move with it: the library references Markdig only. The
library therefore gets a small `SkillFrontmatter.Parse(string text)` that returns the same
fields for the shapes a `SKILL.md` has: `key: value` lines between `---` fences, quoted
values, and folded or literal block scalars (`>`, `|`) collapsed to one line with single
spaces. `SkillValidatorTests` runs its existing cases against both parsers and asserts the
`name`, `description` and `tools` values agree. App code that reads Persona files keeps
`PersonaFrontmatter`.

**Two more rules a moved Skill needs in `Huddle.App`.** (1) `SkillStore` must ignore the
`tools:` line of any folder in `Skills/` that has a `.source.json`, so a hand-edited copy
cannot grant what S011 forbids. Built-in Skills have no `.source.json`. (2) `SkillCopier`
([§6.6](#66-skillcopier-and-sourcejson)) runs the S checks on the *copy* before the swap
([§8.6](#86-reading-and-copying-while-the-ai-tool-writes) step 5), so S011 is enforced at the
moment a Skill could start to matter.

**S009 in full.** The effective licence is, in order: the catalog entry's `license` (for a Pack
Skill reached through `catalog`, that entry), the `license` key in `SKILL.md`'s frontmatter, then
`"see <file>"` when the folder has `LICENSE`, `LICENSE.md`, `LICENSE.txt` or `COPYING`. If none
exists the message is *"no licence found"*. If it begins with `Proprietary` (ignoring case) the
message is *"licence is Proprietary (LICENSE.txt)"*. If the catalog entry and the frontmatter
both declare one and they differ ignoring case, the message is *"licence MIT in SKILL.md differs
from Apache-2.0 in the catalog"*. One finding per Skill, the first of those that applies.

**S006 and origins.** Two origins name the same Skill when their *identity* is equal. For a
URL of the form `https://host/owner/repo/tree/<ref>/<path>` (or `blob`) the identity is
`host/owner/repo/<path>`, lower-cased up to the path; for anything else it is the trimmed
string without a trailing `/`. `D003` still compares the exact strings, as §8.3 step 4 says.

#### X001 secret patterns

Run over every text file the registry reaches: each pack and catalog file, `source.json`,
`NOTES.md`, and every file in a Skill folder up to 1 MB each (a file with a NUL in its first
8 KB is binary and skipped). All are `[GeneratedRegex]` with `RegexOptions.CultureInvariant`
and a 1-second match timeout; a timeout counts as no match. A finding names the pattern and the
line, and shows at most the first four characters of the match, then `…`; it never prints the
secret.

| # | Name | Regex |
| --- | --- | --- |
| 1 | GitHub token | `\b(?:ghp\|gho\|ghu\|ghs\|ghr)_[A-Za-z0-9]{36}\b` |
| 2 | GitHub fine-grained token | `\bgithub_pat_[A-Za-z0-9_]{22,}\b` |
| 3 | GitLab token | `\bglpat-[A-Za-z0-9_-]{20,}\b` |
| 4 | AWS access key id | `\b(?:AKIA\|ASIA)[0-9A-Z]{16}\b` |
| 5 | Slack token | `\bxox[abprs]-[A-Za-z0-9-]{10,}\b` |
| 6 | API key with a vendor prefix | `\bsk-(?:ant-\|proj-)?[A-Za-z0-9_-]{32,}\b` |
| 7 | Private key header | `-----BEGIN (?:RSA \|EC \|DSA \|OPENSSH \|PGP \|ENCRYPTED )?PRIVATE KEY(?: BLOCK)?-----` |
| 8 | Password line | `(?im)^\s*["']?(?:password\|passwd\|pwd)["']?\s*[:=]\s*["']?(?!(?:your\|example\|changeme\|placeholder\|xxx\|<\|\$\{?\|\{\{\|\*{3}))[^\s"'<>]{4,}` |
| 9 | Bearer or Basic credential | `\bAuthorization:\s*(?:Bearer\|Basic)\s+[A-Za-z0-9._~+/=-]{20,}` |
| 10 | Credentials in a URL | `\b[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]{3,}@` |

(The pipes are escaped for the table; in code they are plain `|`.) Pattern 8 skips values that are
obviously placeholders, so a Skill that shows `password=<your password>` passes. Everything else
is a hit.

#### Where the text lives

The *Fix hint* column is **UI text**, not model-facing text, so it lives in code, not in
Settings › Prompts ([ADR-0007](adr/0007-model-facing-text-is-configuration.md) covers only text
a model reads). It is one static table in the library:

```csharp
public sealed record CheckInfo(string Code, Severity Severity, string Title, string FixHint);

public static class CheckCatalog
{
    /// <summary>Every code in use, in table order.</summary>
    public static IReadOnlyList<CheckInfo> All { get; }

    /// <summary>Codes that were once used and must never come back.</summary>
    public static IReadOnlyList<string> Retired { get; }

    /// <summary>The entry for a code, or null for an unknown one.</summary>
    public static CheckInfo? Find(string code);
}
```

The UI shows `Finding.Message`, then `CheckCatalog.Find(code).FixHint`, then the code in
brackets. `huddle-packs` prints only the message; its caller is an AI tool. `InstallSkillPack.md`
§10 ("Checking your work", [§6.7](#67-installskillpackmd-and-the-context-file)) lists the same
hints so the AI tool can act on a code without Huddle's screen; the writer builds that list
from `CheckCatalog.All`, so the two cannot differ. §10's *wording* for the AI tool is model-facing
and comes from the prompt `installSkillPack.body` ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)); the hint
list it embeds is generated, not edited.

#### Messages

A message is one line, names the thing that failed, and is written for a person who has not read
this page. Tests pin the code, severity and file exactly and the message by `Contains` on the
named values. Examples:

| Code | Message |
| --- | --- |
| `R003` | *Packs row 3: E:\Clones\x\packs\a.skillpack.json does not exist.* |
| `P005` | *appliesTo: "accountant" is not a role in this Huddle; see huddle-packs roles* |
| `P009` | *Skill "target-authoring": catalog "dotnet-skills" has no Skill of that name.* |
| `S002` | *SKILL.md's name 'doc' does not match its folder name 'docx'.* |
| `S011` | *tools: 'propose_sprint' is an App Tool; only Huddle's built-in Skills can grant it.* |
| `X001` | *GitHub token (pattern 1) at line 12: ghp_…* |

#### Errors and edge cases

| Situation | Result |
| --- | --- |
| A file cannot be read (permission, locked) | `Checks.Run` does not throw; the item gets an error finding with the code of the check that needed the file (`P001`, `D001`, `S002`, `O001`) and the message *"cannot read: {reason}"* |
| A folder vanishes between resolve and check | Same, and S001 |
| `cancellationToken` is cancelled | `OperationCanceledException` propagates; no partial result |
| Two rows reach one Skill folder by different spellings (`..\`, case) | Paths are compared after `Path.GetFullPath` and ignoring case, so it is one Skill |
| A very large catalog (1,000 Skills, most with empty paths) | Only Skills with a filled-in, existing path read their folders |

#### Tests (`tests/Huddle.Tests/SkillPacks/Checks/`)

| Test class | Pins |
| --- | --- |
| `CheckCatalogTests` | Every code in use and every retired code; each has a check text and a fix hint |
| `RegistryChecksTests` | Each R, P, D and O code raised once and not raised once |
| `SkillFolderChecksTests` | S001–S012 raised and not raised; S011 only for names in `AppToolNames` |
| `StrictTests` | `--strict` raises exactly R009 and S001; an empty catalog path stays silent |
| `SecretScanTests` | Each X001 pattern, a placeholder password passes, a finding never repeats the secret |
| `OrderingTests` | Deterministic order; a shared Skill reported once, on its first row; each loading finding appears once in `CheckResult` |
| `ContextTests` | X002, R008 without `suggestedPacks`, O002 only when an entry fails D004, `CheckContext.From` |

---

### 6.4 The `huddle-packs` command

#### What it is

A framework-dependent console program, `src/Huddle.Packs`, assembly `huddle-packs`, that runs
the checks of [§6.3](#63-the-checks) from a shell. It never writes a file and never opens a
network connection ([§8.9](#89-shipping-huddle-packs) says how it ships). It runs without
Huddle, and while Huddle runs on the same folder. Its only coupling to Huddle is the context
file `install-context.json`, which Huddle writes beside `InstallSkillPack.md`.

| File | Holds |
| --- | --- |
| `src/Huddle.Packs/Program.cs` | `Program.Main`, `Program.Run` |
| `.../ArgumentParser.cs` | `ParsedArguments` and the hand-rolled parser |
| `.../DataLocator.cs` | Data folder, context file and Skill Sources folder |
| `.../Commands/ValidateCommand.cs`, `RolesCommand.cs`, `InstalledCommand.cs`, `SkillsCommand.cs`, `SchemaCommand.cs`, `HelpText.cs` | One class per command |
| `.../FindingWriter.cs` | The line and JSON output of `validate` |
| `src/Huddle.SkillPacks/InstallContext.cs` | `InstallContext.Parse(string json)` and `ToJson()`, shared with `InstallFileWriter` so the two programs agree; `CheckContext.From` builds the checks' input from it |
| `src/Huddle.SkillPacks/Schemas/pack.schema.json`, `catalog.schema.json`, `source.schema.json` | Embedded resources |

#### Commands

| Command | Needs context | Prints |
| --- | --- | --- |
| `validate [<path>]` | Yes | Findings, then the summary line |
| `roles` | Yes | Every role id a Pack may name |
| `installed` | Yes | Every Skill in `Skills/` |
| `skills [<text>]` | Yes | Every Skill in every catalog and Pack |
| `schema pack\|catalog\|source` | No | The JSON Schema |
| `--version` | No | Version and schema version |
| `--help`, `-h`, `help` | No | Usage |

#### Options

| Option | Default | Applies to | Effect |
| --- | --- | --- | --- |
| `--data-dir <path>` | See below | All that need context | The Huddle data folder |
| `--json` | off | `validate`, `roles`, `installed`, `skills` | One JSON document instead of lines |
| `--quiet` | off | `validate` | Prints errors and the summary only; counts still include warnings |
| `--strict` | off | `validate` | Raises R009 and S001 to errors |

An option that does not apply to the command is accepted and ignored. An unknown option, a missing
value, a missing or unknown command, a second positional argument where one is allowed, or an
unknown `schema` kind is exit code 2 with the usage on standard error. Options may come before
or after the command and its path. A repeated option: the last wins.

#### Finding the data folder, the context file and the Skill Sources folder

1. **Data folder.** In order: `--data-dir`; the environment variable `HUDDLE_DATA_DIR`; the
   first of `App_Data` under the current directory and `App_Data` beside the executable that
   contains `install-context.json`; otherwise the first of these (so the error names a real
   place). A relative value is resolved against the current directory, not the executable.
2. **Context file.** `{dataDir}/install-context.json`, read once with
   `InstallContext.Parse`, then `CheckContext.From`.
3. **Skill Sources folder.** The context file's `skillSourcesDir`; if that is missing or empty,
   `{dataDir}/SkillSources`. SKILLS.md is `{skillSourcesDir}/SKILLS.md`.
4. **Computer name** for per-computer paths: the context file's `computer`, so a run
   matches what Huddle sees, not what `Environment.MachineName` says.

The context file is the only thing read from Huddle's data besides what SKILLS.md points at.
Huddle's own `appsettings` are not read; if the Human set `Team:SkillSourcesDir`, Huddle has
already put the answer into the context file.

| Situation | Message (standard error) | Exit |
| --- | --- | --- |
| No context file | *No install-context.json in {dataDir}. Start Huddle once so it writes it, then run this again.* | 2 |
| Not valid JSON, or a required field missing | *install-context.json in {dataDir} cannot be read: {reason}. Start Huddle once so it writes it again, then run this again.* | 2 |
| `schemaVersion` higher than this program knows | *install-context.json was written for schemaVersion {n}; this huddle-packs checks {m}. Use the huddle-packs from InstallSkillPack.md.* | 2 |
| Data folder missing or not readable | *Cannot read data folder {dataDir}: {reason}* | 2 |
| Skill Sources folder or SKILLS.md missing | Not exit 2: finding `R001` (error) and the summary line, exit 1 | 1 |

#### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | It ran and found no errors. Warnings may be present. Also `--help`, `--version`, and the listing commands that ran |
| `1` | `validate` found one or more errors |
| `2` | It could not run: a bad option or path, or a data folder or context file it cannot use |

#### `validate`

`validate` reads the context file, reads SKILLS.md and resolves it with
`RegistryResolver.Resolve(registry, files, context)`
([§8.1](#81-reading-skillsmd), [§8.3](#83-resolving-packs-catalogs-and-skills)), runs
`Checks.Run(resolved, context, new CheckOptions(Strict: strict, OnlyPath: path, ToolVersion: version), files)`
and prints.

**Choosing the rows.** `<path>` is resolved against the current directory with
`Path.GetFullPath`; if it does not exist and was relative, against the Skill Sources folder.

| `<path>` is | Rows checked |
| --- | --- |
| Not given, or SKILLS.md, or the Skill Sources folder | All |
| A file or folder | Every row whose resolved pack or catalog file is at or under `<path>`, **or** that reaches a Skill folder at or under `<path>` |
| A `*.skillpack.json` or `*.skillcatalog.json` file that no row points at | A **virtual row**, appended after the real ones with `Line` 0, so the AI tool can check a file before it adds the row. R004 and R008 are not raised for it; P004 and D005 are, against the real rows |
| A folder with a `SKILL.md` directly in it that no row reaches | A virtual Skill: the S checks run on it alone and `Skills=1`, `Rows=0` |
| Anything else (it exists, matches nothing) | Exit 2: *No row in SKILLS.md points at or under {path}.* |
| A path that does not exist and matches no row | Exit 2: *Path not found: {path}* |

**Output lines.** One line per finding, in the order of [§6.3](#63-the-checks), then one `OK`
line per checked row that has no error, then the summary line. Columns are separated by two
spaces:

| Column | Width | Content |
| --- | --- | --- |
| Severity | 5, left-aligned | `ERROR`, `WARN` or `OK` |
| Code | 4 | The code; blank for `OK` |
| File | The longest file shown in this run | The finding's file: **relative to the Skill Sources folder, with `/` separators, when the file is under it; otherwise absolute, as written by the OS**. The registry itself is `SKILLS.md` |
| Message | Rest of line | The message, newlines replaced by a space; no trailing spaces |

```text
ERROR  P005  packs/document-skills.skillpack.json     appliesTo: "accountant" is not a role in this Huddle; see huddle-packs roles
ERROR  S001  E:\Clones\anthropics-skills\skills\pdf   path leads to no folder
WARN   S008  E:\Clones\anthropics-skills\skills\docx  scripts/ and 9 other files are never read by Agents
OK           SKILLS.md                                Packs row 3 points at packs/document-skills.skillpack.json
PACKS-VALIDATE Rows=1 Packs=1 Skills=4 Errors=2 Warnings=11
```

- An `OK` line says *"{Table} row {n} points at {local path as written}"* where `n` counts rows
  in that table from 1. A virtual row says *"{path} is not in SKILLS.md yet; add a row"*, as `OK`
  if it has no error.
- **The summary line is always last** and has exactly this shape:
  `PACKS-VALIDATE Rows=<n> Packs=<n> Skills=<n> Errors=<n> Warnings=<n>`. It is printed whenever
  validation ran, including `--quiet`, and not on exit 2. `Errors` and `Warnings` count all
  findings, whatever `--quiet` hid.
- `--quiet` drops warning and `OK` lines only.
- **Standard output** holds the lines; **standard error** holds only exit-2 messages.

**`--json`.** One object, written with `System.Text.Json` (camel case, lower-case severity,
indented, UTF-8 without a BOM, no summary line). `file` uses the same display form as the
lines. `--quiet` limits `findings` to errors.

```json
{
  "summary": { "rows": 1, "packs": 1, "skills": 4, "errors": 2, "warnings": 11 },
  "findings": [
    { "severity": "error", "code": "P005", "file": "packs/document-skills.skillpack.json",
      "message": "appliesTo: \"accountant\" is not a role in this Huddle; see huddle-packs roles" }
  ]
}
```

#### The other commands

All sort ordinally, ignoring case, so output is stable. Without `--json`, columns are separated
by one tab and a line has no trailing tab. With `--json`, each command prints one object whose
single property is the list.

| Command | Line format | `--json` |
| --- | --- | --- |
| `roles` | `<id>\t<title>` | `{ "roles": [ { "id", "title", "description" } ] }` |
| `installed` | `<name>\t<origin>\t<sha256>`; a built-in Skill's origin is `built-in` | `{ "installed": [ { "name", "origin", "sha256", "builtIn" } ] }` |
| `skills [<text>]` | `<name>\t<downloaded\|not-downloaded>\t<sources>\t<description>`; `<sources>` is `pack:<id>` or `catalog:<id>` items joined by `,`; description cut at 120 characters | `{ "skills": [ { "name", "downloaded", "sources": [], "category", "subCategory", "description", "origin" } ] }` |
| `schema pack\|catalog\|source` | The embedded schema text, unchanged, then a newline | (not applicable; always the schema) |
| `--version` | `huddle-packs <version> (schemaVersion <n>)` | |
| `--help` | See below | |

- `skills` keeps a Skill when `<text>` is absent, or its name, description, category or
  sub-category contains `<text>` ordinally ignoring case. A Skill in a Pack and a catalog is one
  line. `downloaded` means the path resolves, for this computer, to an existing folder.
  No error findings are needed; it reads the registry with the resolver and shows what it can.
- `schema` reads `Agency.Huddle.SkillPacks.Schemas.pack.schema.json` (and `catalog`, `source`)
  from the library assembly. Schemas are JSON Schema draft 2020-12. A test keeps each schema
  and the reader's rules in step (§7.2 owns the content).
- `<version>` is the assembly's informational version with any `+build` suffix removed, the
  same text `Directory.Build.props` gives Huddle.
- `--help` prints the usage block below. When the context file can be read it adds a last line,
  *Install file: {dataDir}\InstallSkillPack.md*; it never fails because the file cannot be read.

```text
huddle-packs <command> [options]

  validate [<path>]        Check SKILLS.md and what it points at, or only the rows at <path>
  roles                    List the role ids a Pack may name
  installed                List the Skills installed in Huddle
  skills [<text>]          List the Skills in every catalog and Pack
  schema pack|catalog|source
                           Print a JSON Schema
  --version, --help

  --data-dir <path>        Huddle's data folder (default: HUDDLE_DATA_DIR, else App_Data)
  --json                   Print JSON
  --quiet                  validate: errors and the summary only
  --strict                 validate: a Skill or file that is not downloaded is an error

Exit codes: 0 no errors, 1 errors found, 2 could not run.
```

#### Program structure

Argument parsing is by hand, in the style of `Huddle.Console`: a `for` loop over `args` with a
`switch` on each token, a value taken as `args[++i]` after a bounds check, and a
`#pragma warning disable S127` with the same justification around the loop. The parser returns
`null` after writing the usage, as `Huddle.Console.Program.ParseArguments` does, so a test can
assert on it without running a command. No new package.

```csharp
namespace Agency.Huddle.Packs;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        return Program.Run(args, Console.Out, Console.Error, Environment.GetEnvironmentVariable,
            Environment.CurrentDirectory, AppContext.BaseDirectory);
    }

    /// <summary>Runs one command; everything it prints goes to the two writers, so a test runs it in-process.</summary>
    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<string, string?> getEnvironmentVariable,
        string currentDirectory,
        string executableDirectory,
        IFileReader? files = null);
}

internal sealed record ParsedArguments(
    string Command,          // "validate", "roles", "installed", "skills", "schema", "version", "help"
    string? Argument,        // the path, the search text or the schema kind
    string? DataDir,
    bool Json,
    bool Quiet,
    bool Strict);
```

- `Run` never lets an exception escape: an `IOException`, `UnauthorizedAccessException` or
  `JsonException` becomes an exit-2 message. Anything else is a bug and propagates, so the
  process exits with the runtime's own failure code.
- All environment, current-directory and executable-folder access goes through the parameters, so
  tests never touch the real ones. `files` defaults to the disk.
- `huddle-packs` has no `async` code; every read is synchronous and the program has nothing to
  cancel.
- The project sets `InternalsVisibleTo("Huddle.Tests")` for `Program.Run`, `ArgumentParser` and
  `DataLocator`.
- A line is written with `TextWriter.WriteLine`, so line endings follow the platform; nothing in
  the output depends on them.

#### Tests (`tests/Huddle.Tests/SkillPacks/Cli/`)

All run `Program.Run` in-process with `StringWriter`s, a temporary data folder written by a
`PacksFixture` helper (context file, SKILLS.md, pack and catalog files, Skill folders), and a
fixed computer name.

| Test class | Pins |
| --- | --- |
| `ArgumentParserTests` | Usage and exit 2 for bad input; options before or after the command; last repeated option wins |
| `DataLocatorTests` | `--data-dir` over `HUDDLE_DATA_DIR` over the default; Skill Sources folder and computer name from the context file |
| `ContextFileTests` | The exact missing-context sentence; invalid JSON and a newer `schemaVersion` exit 2; `ToJson` then `Parse` round-trips |
| `ValidateOutputTests` | Fixed columns, relative paths under the Skill Sources folder, `OK` lines, `--quiet`, the summary line last and exact |
| `ValidateExitCodeTests` | 0, 1 and 2 as specified, with and without `--strict` |
| `ValidateJsonTests` | The `--json` shape |
| `ValidatePathFilterTests` | Row filtering, virtual rows and virtual Skills, the two path errors |
| `ListCommandTests`, `SchemaCommandTests` | `roles`, `installed`, `skills`, `schema` output |
| `ParityTests` | The CLI's findings equal `Checks.Run` on the same tree; `validate` never writes a file |

### 6.5 `SkillRegistry` and `RegistryWatcher`

**Purpose.** Keep one current, immutable picture of the registry for the whole app, and tell
subscribers when it changes. `SkillRegistry` replaces `SkillPackStore` and `SkillsCatalogIndex`;
`RegistryWatcher` is the hosted service that decides when to refresh it
([§8.5](#85-watching-pointers)).

**Split of work.** The watcher owns *when*: it holds the file watchers and the poll timer, and
turns settled events into a **scope** (everything; these pack/catalog files; these Skill
folders). `SkillRegistry` owns *what*: it reads, resolves, checks, publishes and tracks what is
new. The watcher calls `SkillRegistry.RebuildAsync(scope, ct)`; nothing else does, except the
**Reload** button (`ReloadAsync`).

#### Types

```csharp
namespace Agency.Huddle.App.SkillSources;

/// <summary>One published picture of the registry. Replaced whole, never edited.</summary>
/// <param name="Version">Starts at 1, +1 per publish; lets a component tell "same picture" cheaply.</param>
/// <param name="Resolved">What the resolver produced.</param>
/// <param name="Checked">What <c>Checks.Run</c> produced: every finding, and the state of every Pack, catalog and Skill.</param>
/// <param name="NewKeys">Row keys (§6.2) and <c>skill:&lt;name&gt;</c> keys that are in this snapshot and not in <c>seen</c>.</param>
/// <param name="PackHolders">Pack id to the names of the Agents whose <c>_packs</c> names it; the count is the list length.</param>
/// <param name="SkillHolders">Skill name to the names of the Agents whose <c>skills:</c> holds it; the count is the list length.</param>
/// <param name="HiddenPacks">Pack ids in <c>hidden.json</c>.</param>
/// <param name="HiddenSkills">Skill names in <c>hidden.json</c>.</param>
internal sealed record RegistrySnapshot(
    long Version,
    ResolvedRegistry Resolved,
    CheckResult Checked,
    IReadOnlySet<string> NewKeys,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PackHolders,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SkillHolders,
    IReadOnlySet<string> HiddenPacks,
    IReadOnlySet<string> HiddenSkills);

/// <summary>What <see cref="SkillRegistry.Changed"/> carries.</summary>
/// <param name="Snapshot">The snapshot just published.</param>
/// <param name="AppearedKeys">Keys present now and not in the previous snapshot; the UI says "X is ready" for the ones that are usable.</param>
/// <param name="ChangedSkills">Skill names whose state or <c>PointerSha256</c> differs from the previous snapshot.</param>
internal sealed record RegistryChange(
    RegistrySnapshot Snapshot,
    IReadOnlyList<string> AppearedKeys,
    IReadOnlyList<string> ChangedSkills);

/// <summary>Which part of the registry to re-read.</summary>
internal sealed record RebuildScope(bool Everything, IReadOnlyList<string> Files, IReadOnlyList<string> SkillFolders)
{
    public static RebuildScope All { get; } = new(true, [], []);
}

/// <summary>The registry as the app uses it. Implements <c>ISkillAvailability</c> (§6.10.1) so <c>CardResolver</c> stays a pure static class.</summary>
internal sealed class SkillRegistry : ISkillAvailability, IDisposable
{
    public RegistrySnapshot Current { get; }
    public event Action<RegistryChange>? Changed;

    public string ComputerName { get; }
    public string SkillSourcesDir { get; }          // §8.7: skill-sources.json "dir", else Team:SkillSourcesDir, else {DataDir}/SkillSources
    public string RegistryPath { get; }             // SKILLS.md in that folder

    public ItemState DisplayState(PackEntry pack);
    public ItemState DisplayState(SkillEntry skill);
    public bool IsHidden(PackEntry pack);
    public bool IsHidden(SkillEntry skill);
    public IReadOnlyList<PackEntry> PacksForRole(string roleId);    // not hidden, registry order, any state
    public SkillAvailability Of(string skillName);                    // what the registry knows about a name (enum in §6.10.1); ISkillAvailability
    public PackEntry? FindPack(string packId);                        // Current.Resolved.FindPack; ISkillAvailability
    public string? SubCategoryOf(string skillName);                   // sub-category of the first catalog entry of that name, empty becomes null; ISkillAvailability
    public IReadOnlyList<string> HolderNames(PackEntry pack);         // Agents whose _packs names it, from Current.PackHolders
    public IReadOnlyList<string> HolderNames(SkillEntry skill);       // Agents whose skills: holds it, from Current.SkillHolders
    public SkillReadiness CheckReady(IReadOnlyList<string> packIds, IReadOnlyList<string> skillNames);

    public Task ReloadAsync(CancellationToken ct);                              // Reload button: RebuildScope.All, drops caches
    public Task RebuildAsync(RebuildScope scope, CancellationToken ct);        // the watcher
    public Task MarkSeenAsync(IReadOnlyCollection<string> keys, CancellationToken ct);
    public CheckContext BuildCheckContext();
}

/// <summary>Whether a set of Packs and Skills can be handed to Agents right now (used by CardResolver and HireService).</summary>
/// <param name="Ready">Skills, with their Pack Skills expanded, that are usable and downloaded.</param>
/// <param name="Missing">Items that are not: the Pack or Skill, the state (NotDownloaded, Broken, Withdrawn, unknown name) and a one-line reason for a card.</param>
internal sealed record SkillReadiness(IReadOnlyList<SkillEntry> Ready, IReadOnlyList<MissingItem> Missing)
{
    public bool IsReady => this.Missing.Count == 0;
}

/// <param name="Name">The Pack id or Skill name.</param>
/// <param name="Title">The Pack title, or the Skill name again.</param>
/// <param name="CatalogId">The catalog that lists a missing Skill, or null for a Pack or a Skill no catalog lists.</param>
internal sealed record MissingItem(string Name, bool IsPack, ItemState State, string Reason, string Title, string? CatalogId);
```

#### Behaviour

**The snapshot.** `Current` is read through `Volatile.Read` and replaced with
`Interlocked.Exchange`, so a reader on any thread sees a whole snapshot, as with `SkillPackStore`
today. Rebuilds are serialised by a `SemaphoreSlim(1,1)`; while one runs, further requests are
merged into one follow-up whose scope is the union, so a burst of writes costs one extra pass,
not one per event.

**A rebuild**, in order:

1. `RegistryReader.Load(files, SkillSourcesDir, ComputerName)`: a missing file is `R001`; one
   over 1 MB is `R011`, decided from its length before any text is read ([§6.2](#62-registry-model-and-file-readers)).
   A scope without SKILLS.md reuses the previous `Registry`.
2. Build the `CheckContext` ([below](#building-the-checkcontext)).
3. `RegistryResolver.Resolve(registry, files, context)`, where `files` is an `IFileReader`
   over the real disk with two caches that the scope invalidates: file text by path (keyed by
   write time and length) and folder hash by path ([§8.4](#84-noticing-an-update), "When hashing
   runs"). A scope of `Files` invalidates those files; `SkillFolders` those folders'
   hashes; `Everything` both. Only the rows and Skills in the scope are re-resolved; the rest are
   carried over from the previous `ResolvedRegistry`, as [§9](#9-incremental-vs-full-processing)
   lays out. A change to SKILLS.md, **Reload** and start-up are full reads.
4. `Checks.Run(resolved, context, options, files)` over the whole result, which is cheap once the
   I/O is cached. Its `CheckResult` (findings and states) goes into the snapshot.
5. Read `hidden.json` and `skill-sources.json` (`seen`), count holders from the Agent files,
   compute `NewKeys` and `AppearedKeys`.
6. Publish: `Current = snapshot`; then, outside any lock, `Changed?.Invoke(change)`. A subscriber
   that throws is caught per handler and logged; it never blocks the others.
7. Append lines to the affected Sources' `log.jsonl` through `SourceLog` for rows that appeared,
   went Broken or recovered. The line schema is defined once, in [§7.2](#sourcessourcelogjsonl); `SourceLog` fills `at`, `computer` and `huddle`.
8. Ask `InstallFileWriter` to refresh (it debounces to once a second, [§6.7](#67-installskillpackmd-and-the-context-file)).

A rebuild that throws (a bug, or `UnauthorizedAccessException` on SKILLS.md) is logged, keeps
the previous snapshot and publishes nothing. The next event retries.

**`Changed` and threads.** The event is raised on a thread-pool thread. Razor components
subscribe in `OnInitialized`, unsubscribe in `Dispose`, and call `InvokeAsync(StateHasChanged)`,
as every other `Changed` event in the app is consumed.

**Holders and persona changes.** A change to an Agent file does not re-read the registry. The
service subscribes to `PersonaStore`'s change event and republishes a new snapshot that reuses
the same `ResolvedRegistry` with fresh `PackHolders` and `SkillHolders`, then raises `Changed`
with empty `AppearedKeys`. It also subscribes to `SkillStore.SkillsChanged`, because a copy
made or removed in `Skills/` changes `InstalledSha256`; that does require a rebuild with scope
`SkillFolders` for the touched names.

#### The "New" overlay

New means: the Human has not yet seen this row. It is per viewer of this Huddle, so it is kept
in this Huddle's `{DataDir}/skill-sources.json` as `seen`, a list of keys, not in the shared
Skill Sources folder ([§7.2](#72-schemas)). Keys:

| Item | Key |
| --- | --- |
| A Pack | The Packs row key (§6.2 "Row identity") |
| A catalog | The Catalogs row key |
| A Skill | `skill:` plus its name. Only a *downloaded* Skill (`FolderPath` not null) can be new, so the 263 catalog Skills the move writes ([§6.13](#613-the-move)) do not greet the Human with 263 chips; one becomes new when its `path` first leads to a folder |

Rules:

1. **Baseline.** If `seen` is absent from `skill-sources.json` (a fresh install, or the first
   start after the move), the first complete rebuild writes `seen` with every key then present.
   Nothing is New. This is why the move's own output never wears **New**.
2. **New** = a key in the snapshot and not in `seen`. `NewKeys` holds them.
3. **Marking seen** is explicit. A tab calls `MarkSeenAsync(keys)` when the Human leaves it or
   closes Settings, with the keys that tab showed, not when it opens: the vision's *"since you
   last opened this tab"* needs the chip to show once during the visit. The call merges into
   `seen` and writes the file through `SkillSourcesSettings`, the one writer of `skill-sources.json`, through a temporary
   file and a move; it keeps `dir` as it is, so Sources › Folder and `MarkSeenAsync` never overwrite each other.
4. **Pruning.** Whenever `seen` is written, keys that no longer exist in the snapshot are
   dropped, so the list cannot grow without bound. A row that is removed and added again is New
   again.
5. **Display.** `DisplayState` applies the precedence of [§6.2](#62-registry-model-and-file-readers):
   `New` shows only when the library state is `Available` and no Agent holds the item.

#### Building the `CheckContext`

`BuildCheckContext()` copies, at the moment of the rebuild, from services the app already has.
It never reads the context file (that file is for `huddle-packs`).

| `CheckContext` member ([§6.3](#63-the-checks)) | Taken from |
| --- | --- |
| `HuddleVersion` | The assembly's informational version, `+commit` suffix cut |
| `SchemaVersion` | `1` |
| `ComputerName` | `Team:ComputerName`, else `Environment.MachineName` |
| `DataDir`, `SkillSourcesDir` | `TeamOptions.DataDir` made absolute; `SkillSourcesDir` as above |
| `Roles` (id, title, description, suggestedPacks) | `RoleLister.List()` ([§6.7](#67-installskillpackmd-and-the-context-file)): `CustomRoleStore.Roles`, `HiringPoolCatalog.Jobs` and the installed team types' roles, de-duplicated by id. `SuggestedPacks` is the pool job's `Packs`, empty for the others |
| `InstalledSkills` (name, origin, sha256, builtIn) | `SkillStore.All`: `builtIn` is `Source is Default or Overridden`; `origin` and `sha256` come from `Provenance` when present, else null |
| `AppToolNames` | `SkillGrants.Grantable` |
| `Written` | `TimeProvider.GetLocalNow()`, with its offset (injected, `timeProvider = null` defaults to `TimeProvider.System`) |

The same shape is what `InstallFileWriter` serialises into `install-context.json`, so a finding
`huddle-packs` prints and a finding Huddle shows come from the same inputs.

#### Start-up order

Agents must never start against a registry that is half-read, and a first start must not
race the move. The rule: **the move, then the first complete read, both finish before any
Agent host starts.**

1. `RegistryWatcher` is an `IHostedService` registered in `ServiceCollectionExtensions` **before**
   `BuiltinAgentSeeder` and `PersonaSupervisor`. The host awaits each `StartAsync` in
   registration order, so the next service does not begin until this one returns.
2. `StartAsync` runs, in this order, and awaits each:
   1. `SkillSourcesMove.RunAsync` ([§6.13](#613-the-move)): runs only when `SKILLS.md`
      does not exist; otherwise returns `null` at once.
   2. Any `Skills/.trash/<name>-*` whose `Skills/<name>/` is missing is moved back; then
      `Skills/.trash` and `Skills/.staging` are emptied ([§8.6](#86-reading-and-copying-while-the-ai-tool-writes) step 7).
   3. `SkillRegistry.ReloadAsync` — the first full read, which also writes the `seen` baseline
      and asks `InstallFileWriter` for the first `InstallSkillPack.md`.
   4. The file watchers and the poll timer are created. They are *not* needed for correctness at
      start, only afterwards.
3. If step 2.1 throws, the exception is logged; `StartAsync` continues to 2.3, which publishes a
   snapshot whose only finding is `R001`. Agents start; every Pack and Skill shows as absent
   and the Sources tab says why. The move re-runs on the next start because `SKILLS.md` is still
   missing (its last step, [§8.8](#88-writing-skillsmd) "Create").
4. If 2.3 throws, the snapshot stays `ResolvedRegistry.Empty` and the watcher still starts, so
   the next event can repair it.

Because the copies Agents use live in `Skills/` and `SkillStore` reads them as today, an
Agent never needs the registry to run. The registry matters at choice time, in cards and in
Build a team.

#### Public API by caller

| Caller | Uses |
| --- | --- |
| **Settings › Skills** (all tabs, Details, `G7`) | `Current`, `Changed`, `DisplayState`, `IsHidden`, `MarkSeenAsync`, `ReloadAsync`; for Remove and Hide, the services of [§6.12](#612-removing-and-hiding), which call `ReloadAsync` afterwards |
| **`AgentProfileCard`**, **`RoleFormDialog`** | `PacksForRole`, `Of`, `Current.Resolved.FindSkill`, `DisplayState`, `HolderNames` to show "In use by N"; `ISkillCopier.EnsureAvailableAsync` before an Agent file changes |
| **`CardResolver`** (static) | Takes an `ISkillAvailability` (`SkillRegistry` implements it: `Of`, `FindPack`, `SubCategoryOf`) from its caller; no dependency on the service class itself, so its tests need no host |
| **`HireService`**, **`BuildTeam`** | `ISkillCopier.EnsureAvailableAsync(names, ct)` replaces `ISkillInstaller.EnsureInstalledAsync` and `InstallSkillsAsync`; it uses `CheckReady` and `CopyAsync` ([§6.6](#66-skillcopier-and-sourcejson)). A problem it returns becomes the draft's **Needs setup** (`skillsBlocked`), and a hire proceeds only when it returns none |
| **`SkillPromptBuilder`** | `Current.Resolved` for the rows and findings a fix prompt names; `CheckReady(...).Missing` for **Needs setup** |
| **Update dialog** (`G7`) | `ISkillCopier.CopyAsync` for the Skills whose files changed or were added |
| **`InstallFileWriter`** | `BuildCheckContext()`, `Current` |
| **Library root** ([§6.11](#611-the-skill-sources-library-root)) | `SkillSourcesDir` |

#### `RegistryWatcher`

```csharp
internal sealed class RegistryWatcher(
    SkillRegistry registry,
    SkillSourcesMove move,
    IOptions<TeamOptions> options,
    ILogger<RegistryWatcher> logger,
    TimeProvider? timeProvider = null) : IHostedService, IDisposable
```

It implements [§8.5](#85-watching-pointers) without change: watchers, watch roots, the 32-watcher cap
and the 30-second poll are all defined there. What this section adds is the hand-off: when the
queue goes quiet the watcher maps the changed paths to a `RebuildScope` and calls
`registry.RebuildAsync`. Folder-hash work runs on the thread pool; nothing it does touches the UI
thread.

**Errors.**

| Situation | Behaviour |
| --- | --- |
| SKILLS.md missing | `R001` in the snapshot; the Packs tab shows `G4d` |
| SKILLS.md over 1 MB | `R011`; not parsed; previous good rows are *not* kept |
| `skill-sources.json` unreadable or not JSON | Treated as absent for `dir`; `seen` as absent (so nothing is New); a warning is logged |
| `hidden.json` unreadable | Nothing hidden; a warning is logged |
| The Skill Sources folder does not exist | Created at start; if it cannot be created, the snapshot is empty and the Sources tab shows the folder as the problem |
| A subscriber throws | Caught, logged, others still run |
| `RebuildAsync` cancelled | `OperationCanceledException` propagates; no snapshot is published |

**Tests to write.**

| Test class | Pins |
| --- | --- |
| `SkillRegistryTests` | Snapshot versions, `AppearedKeys`, partial and full rebuilds, holders republished without re-reading, `CheckReady`, a throwing subscriber |
| `SkillRegistrySeenTests` | The New rules 1–5, and that a `seen` write keeps `dir` |
| `SkillRegistryContextTests` | `BuildCheckContext`: built-in flag, `Team:ComputerName` |
| `RegistryWatcherTests` | Start-up order (move, `.trash` restore, first read, watchers), a move that throws, registration before `PersonaSupervisor` |

---

### 6.6 `SkillCopier` and `.source.json`

**Purpose.** Put a chosen Skill's files into `Skills/<name>/` safely and record where they came
from. It is the only code that writes a Skill folder into `Skills/` for a fetched Skill. The
algorithm is [§8.6](#86-reading-and-copying-while-the-ai-tool-writes), which this section does
not repeat; it fixes the API, the order of calls around it and the errors.

Callers depend on the interface `ISkillCopier`, never on `SkillCopier`, so a test replaces it
(a sealed class cannot be mocked). `HireService`, `AgentProfileCard`, `BuildTeam` and the update card
`G7` ([§6.9.8](#698-the-update-card-g7)) use the members below and no others.

**Inputs / outputs.**

```csharp
namespace Agency.Huddle.App.SkillSources;

/// <summary>What the rest of the app may ask of the copier.</summary>
internal interface ISkillCopier
{
    /// <summary>Copies each Skill into <c>Skills/&lt;name&gt;/</c>, one after another, and reports each outcome.</summary>
    /// <param name="skills">Entries taken from the current snapshot. Entries that are not usable are reported as failures, not thrown.</param>
    /// <param name="ct">Cancels the copy; any staging folder is removed and the exception propagates.</param>
    Task<CopyResult> CopyAsync(IReadOnlyList<SkillEntry> skills, CancellationToken ct);

    /// <summary>Makes sure every named Skill a hire or an Agent save needs is in <c>Skills/</c>: a built-in name or a name already there
    /// is skipped, a <see cref="SkillAvailability.Copyable"/> one is copied with <see cref="CopyAsync"/>, and an existing folder is never replaced.</summary>
    /// <param name="skillNames">The distinct Skill names a batch of Agents carries.</param>
    /// <param name="ct">Cancels the copy.</param>
    /// <returns>One <c>SkillSetupItem</c> (§6.10.1) per name that is not ready, with the copy failure or the registry state as its detail; empty when every Skill is present.</returns>
    Task<IReadOnlyList<SkillSetupItem>> EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct);
}

internal sealed class SkillCopier(
    SkillRegistry registry,
    IOptions<TeamOptions> options,
    SourceLog log,
    ILogger<SkillCopier> logger,
    TimeProvider? timeProvider = null) : ISkillCopier
{
    public Task<CopyResult> CopyAsync(IReadOnlyList<SkillEntry> skills, CancellationToken ct);
    public Task<IReadOnlyList<SkillSetupItem>> EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct);
}

/// <summary>The outcome of a batch. The caller changes an Agent file only when <see cref="Ok"/>.</summary>
internal sealed record CopyResult(IReadOnlyList<CopiedSkill> Copied, IReadOnlyList<CopyFailure> Failed)
{
    public bool Ok => this.Failed.Count == 0;
}

/// <param name="Name">The Skill.</param>
/// <param name="Sha256">The hash of the copy now in <c>Skills/</c>.</param>
/// <param name="Outcome">Created, Replaced (new files swapped in), or AlreadyCurrent (the copy already matched the pointer; nothing written).</param>
internal sealed record CopiedSkill(string Name, string Sha256, CopyOutcome Outcome);

internal enum CopyOutcome { Created, Replaced, AlreadyCurrent }

/// <param name="Kind">Why it failed; the UI chooses its wording and fix from this.</param>
/// <param name="Message">A sentence for the Human; the exact text is below.</param>
internal sealed record CopyFailure(string Name, CopyFailureKind Kind, string Message);

internal enum CopyFailureKind { NotUsable, Unstable, CheckFailed, NameTaken, Io }
```

**Caller contract.**

1. The caller takes entries from `SkillRegistry.Current.Resolved` (or `CheckReady(...).Ready`).
   `CopyAsync` re-reads nothing from the registry except to confirm each Skill's state
   (`SkillRegistry.DisplayState`) is still usable in the *current* snapshot. `EnsureAvailableAsync` classifies each name with
   `SkillRegistry.Of`, passes the `Copyable` ones to `CopyAsync` and reports the rest; it does
   not touch an `UpdateAvailable` Skill that is already in `Skills/`.
2. A batch is **independent per Skill**: every Skill is attempted, in input order, even after a
   failure, so the Human sees every problem at once. Skills already swapped stay swapped; a
   good copy with nobody pointing at it is harmless and will be the same copy next time.
3. **The caller changes an Agent's file only when `Ok`** ([§8.6](#86-reading-and-copying-while-the-ai-tool-writes), last paragraph). On failure the Agent is unchanged and
   the card shows `Failed[].Message`.
4. One `SemaphoreSlim(1,1)` **per Skill name** serialises copies of that name (async, so not a
   `Lock`). Two Agents choosing the same Pack at once do not race on `Skills/<name>/`; the
   second finds `AlreadyCurrent`. Copies of different names run one after another within a batch.

**Per Skill**, in order:

| Step | Rule | On failure |
| --- | --- | --- |
| 1. Usable? | The Skill's state in the current snapshot is `Available`, `UpdateAvailable`, `Deprecated` or `InUse`, and `FolderPath` is not null. Withdrawn is refused for a first copy and allowed for a re-copy of a Skill already in `Skills/` | `NotUsable` |
| 2. Name free? | `Skills/<name>/` is absent, or has a `.source.json` with the same `origin`, or is a leftover of a failed earlier copy. A folder with no `.source.json` is a Skill the Human wrote or a built-in override and is never replaced (`S005`/`S006` at copy time) | `NameTaken` |
| 3. Already current? | The pointer's hash equals `.source.json`'s `sha256` and the copy's own hash equals it too | Reported `AlreadyCurrent`; stop |
| 4. Copy | [§8.6](#86-reading-and-copying-while-the-ai-tool-writes) steps 1–7, including the three attempts | `Unstable`, `CheckFailed` or `Io` |
| 5. Record | Write `.source.json` into the staged copy before the swap (below) | `Io` |
| 6. Log | One `copied` line in the Source's `log.jsonl` ([§7.2](#sourcessourcelogjsonl) defines the line) | Never fails the copy |

**Exact messages** (the `{…}` are filled in; these are the strings tests pin):

| Kind | Message |
| --- | --- |
| `NotUsable` | *"{name} is not ready to use ({state}). Fix it on Settings › Skills first."* |
| `Unstable` | *"The files for {name} kept changing while Huddle copied them. Try again when your AI tool has finished."* |
| `CheckFailed` | *"{name} failed {code} after copying: {message}"* — the first error finding of the S checks on the copy, for example `S011` |
| `NameTaken` | *"A different Skill called {name} is already in Skills/, so Huddle did not replace it."* |
| `Io` | *"Huddle could not write {name} to Skills/: {reason}"* |

**`.source.json`.** The record keeps the seven fields `SkillProvenance` writes today (`repo`, `path`,
`commit`, `sha256`, `publisher`, `license`, `importedAt`) and adds four optional ones
(`schemaVersion`, `source`, `origin`, `copiedFrom`). An old Huddle reads a new file (it ignores
keys it does not know) and a new Huddle reads an old file (the new keys are optional).
Field-by-field schema and a JSON example are in [§7.2](#72-schemas). How the old seven are filled
when the Skill came from a pointer:

| Field | Filled from |
| --- | --- |
| `repo` | The repository part of `origin` when `origin` is a `…/tree/<commit>/…` URL; otherwise the Source's `url`; otherwise `local:<source id>` |
| `path` | The part of `origin` after the commit; otherwise the folder name |
| `commit` | The commit in `origin`; otherwise the Source's `commit`; otherwise `unpinned` |
| `sha256` | `SkillFolderHash.ComputeFolder` of the staged copy (equal to `before` and `after`, [§8.6](#86-reading-and-copying-while-the-ai-tool-writes) step 4) |
| `publisher` | The entry's `publisher`; otherwise the owner in `repo`; otherwise `unknown` |
| `license` | The entry's `license`; otherwise `unknown` |
| `importedAt` | `TimeProvider.GetLocalNow()` when the copy was made, ISO-8601 with its offset |
| `schemaVersion`, `source`, `origin`, `copiedFrom` (new) | `1`; the Source id; the entry's `origin`; the pointer folder on this computer |

`SkillProvenance` gains these four as optional members, `TryRead` still requires the original
seven, and `Write` writes all eleven through `AtomicFile.WriteText` as today. For an old file,
the missing fields are derived: `origin` as `https://github.com/{repo}/tree/{commit}/{path}` and
`source` as `github.com/{repo}`, so an imported Skill compares equal to its catalog entry after the
move and shows no **Update available**. A Skill whose `source` names a Source no longer in the
registry shows **Source removed** in `SkillProvenanceLine.razor`.

`SkillStore.ScanDisk` changes in two ways: it skips folders whose name starts with `.` (`.staging`,
`.trash`), and it ignores the `tools:` line of every folder that has a `.source.json`, so a copied
Skill can never grant an App Tool even if someone edits the copy ([§14](#14-design-notes--rationale), D-20).
Its **Modified locally** comparison of the copy's hash with `sha256` is unchanged.

**What it never does.** It does not go online, does not run anything in a Skill, writes only inside
`Skills/` (plus one appended line in the Source's `log.jsonl`), does not follow a symbolic link or junction (`S004` is checked on the source
before the copy starts, and the copy skips reparse points as a second guard), and does not
change an Agent file.

**Errors beyond the table.** `OperationCanceledException` is not caught: the staging folder is
deleted in a `finally` and the exception propagates. A staging folder that cannot be deleted is
logged and left for the next start-up sweep. A crash between the two renames of the swap is
repaired at the next start: `Skills/.trash/<name>-*` is moved back if `Skills/<name>/` is
missing, then `.trash` and `.staging` are emptied.

**Tests to write.**

| Test class | Pins |
| --- | --- |
| `SkillCopierTests` | Created, Replaced, AlreadyCurrent; swap order and restore; Unstable after three attempts (fake `TimeProvider`); `CheckFailed` on `S011`; `NameTaken` for a Human's folder or another origin; `.git` and `.source.json` not copied; reparse points refused; one failure does not stop the others; cancellation removes staging; the per-name semaphore |
| `SkillCopierEnsureTests` | `EnsureAvailableAsync`: built-in and present names skipped, a Copyable name copied once, problems reported and never thrown, an `UpdateAvailable` copy never replaced by a hire |
| `SkillProvenanceTests` (updated) | The mapping table; an old file reads with the new fields null and its `origin` derived (D7); a new file reads with the old record shape |

---

### 6.7 `InstallSkillPack.md` and the context file

**Purpose.** Keep two files in `{DataDir}` true to this Huddle at all times: `InstallSkillPack.md`,
the one document any AI tool reads to work for this Huddle, and `install-context.json`, the
same facts for `huddle-packs`, which reads it so the checks know this Huddle's roles, installed
Skills and App Tool names without referencing the app ([§5.1](#51-projects-and-components)).
`InstallFileWriter` is the only writer of both.

**Responsibilities.**

- Write both files once at start-up, after the move ([§6.13](#613-the-move)), and again when
  roles, installed Skills, the registry, the Skill Sources folder or the
  `installSkillPack.body` prompt change, coalesced to at most one write per second.
- Build `InstallSkillPack.md`: front matter, the body rendered from the prompt, and appendices
  A to D with their size caps.
- Build `install-context.json` from the same inputs, uncapped.
- Write each through a temporary file and a move, context file first, so the front matter's
  `context:` never names a file older than the document.
- Skip a write whose content would be the same as the last one, so file times only move when
  something changed.

**Inputs / outputs.**

| In (service) | What it gives | Changes signalled by |
| --- | --- | --- |
| `HiringPoolCatalog` | `Jobs`: `PoolJob(Id, Title, Description, …)`, shipped and user pool merged | `Changed` |
| `CustomRoleStore` | `Roles`: `CustomRole(Id, Title, Description, …)`, from `custom-roles.json` | `Changed` |
| `TeamTypeCatalog` | `Types`: `TeamTypeManifest` with `Roles`: `RoleSpec(Id, Title, Job, …)`, the installed team types, shipped and user | `Changed` |
| `SkillStore` | `All`: `Skill(Name, Source, FolderPath, Provenance, …)`, everything in `Skills/` | `SkillsChanged` |
| `SkillRegistry` ([§6.5](#65-skillregistry-and-registrywatcher)) | `Current`: the `ResolvedRegistry` snapshot: rows, catalogs, Sources | `Changed` |
| `SkillSourcesSettings` | The Skill Sources folder (`skill-sources.json`, else `Team:SkillSourcesDir`) | `Changed` |
| `PromptStore` | `Render("installSkillPack.body", values)` | `PromptsChanged` |
| `CheckCatalog.All` (library, [§6.3](#63-the-checks)) | Code, severity, check and fix hint of every check: the one source of the `{{checkHints}}` table | none; compiled in |
| `IOptions<TeamOptions>` | `DataDir`, `ComputerName`, `HuddlePacksPath` ([§8.7](#87-configuration-huddle-reads)) | none; read once |
| `SkillGrants.Grantable` | The App Tool names only built-in Skills may grant | none; compiled in |
| `TimeProvider` | `GetLocalNow()` for `written`, timers for the coalescing | none |

Out: `{DataDir}/InstallSkillPack.md` and `{DataDir}/install-context.json`. Both live beside the
Skill Sources folder, never inside it, because they describe one Huddle and the folder can be
shared.

#### Where the roles come from

A role is anything a Pack's `appliesTo` may name (check `P005`). Three services hold roles, and
the writer takes the union through one class so `Appendix A`, the context file and the
`CheckContext` the app's own checks use can never disagree:

```csharp
/// <summary>The union of every role this Huddle knows, deduplicated by id.</summary>
internal sealed class RoleLister(HiringPoolCatalog pool, CustomRoleStore customRoles, TeamTypeCatalog teamTypes)
{
    /// <summary>Every role, ordered by <see cref="RoleInfo.Id"/> ordinal.</summary>
    /// <returns>The roles, as the library's <c>RoleInfo(Id, Title, Description, SuggestedPacks)</c> ([§6.3](#63-the-checks)).</returns>
    public IReadOnlyList<RoleInfo> List();
}
```

| Order | Source | Id | Title | Description | `SuggestedPacks` |
| --- | --- | --- | --- | --- | --- |
| 1 (wins a clash) | `CustomRoleStore.Roles` | `CustomRole.Id` (`custom-…`) | `Title` | `Description` | `[]` |
| 2 | `HiringPoolCatalog.Jobs` | `PoolJob.Id` | `Title` | `Description` or `""` | `PoolJob.Packs`, or `[]` |
| 3 | Each `TeamTypeCatalog.Types[].Roles` | `RoleSpec.Id` | `Title` | The pool job's description when `RoleSpec.Job` is set, else `""` | That pool job's `Packs` when `RoleSpec.Job` is set, else `[]` |

An id seen twice keeps the first row in that order. The result is the same for every caller;
`RoleLister` is also what builds `CheckContext.Roles` (task P8). It does not read `Skills/` or
the registry.

#### When it writes

`InstallFileWriter` is an `IHostedService`. Hosted services start in registration order, which
is: `SkillRegistry` (a singleton), `RegistryWatcher`, `InstallFileWriter`, and only then
`BuiltinAgentSeeder` and `DataInitializer`. `RegistryWatcher.StartAsync` runs the move
([§6.13](#613-the-move)) and the first full read ([§6.5](#65-skillregistry-and-registrywatcher));
the writer does not run the move. Being registered after the watcher, its first write already
sees the moved rows, and being before the seeder and `DataInitializer`, the files exist before
any Agent starts.

```csharp
/// <summary>Where <c>InstallSkillPack.md</c> is; lets <c>SkillPromptBuilder</c> avoid the sealed writer.</summary>
public interface IInstallFileLocation
{
    /// <summary>The full path of <c>{DataDir}/InstallSkillPack.md</c>.</summary>
    string InstallFilePath { get; }
}

internal sealed class InstallFileWriter(
    RoleLister roles, SkillStore skills, SkillRegistry registry, PromptStore prompts,
    SkillSourcesSettings sources, IOptions<TeamOptions> options,
    ILogger<InstallFileWriter> logger, TimeProvider? timeProvider = null)
    : IHostedService, IInstallFileLocation, IDisposable
{
    /// <summary>Where <c>InstallSkillPack.md</c> is: <c>{DataDir}/InstallSkillPack.md</c>.</summary>
    public string InstallFilePath { get; }

    /// <summary>Where the context file is: <c>{DataDir}/install-context.json</c>.</summary>
    public string ContextFilePath { get; }

    /// <summary>Asks for a write; coalesced, never blocks. Also called by <b>Reload</b>.</summary>
    public void RequestWrite();

    /// <summary>Writes both files now, once, then starts listening for changes. Does not run the move.</summary>
    public Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops listening and drops a pending write.</summary>
    public Task StopAsync(CancellationToken cancellationToken);
}
```

| Step | Rule |
| --- | --- |
| Start | `StartAsync` calls the write routine directly (no timer), so the files exist before any Agent starts. Only then does it subscribe to the seven change events above |
| Request | Every event calls `RequestWrite()`. It marks the writer dirty; if a timer is already pending it returns |
| Coalesce | The timer fires at `max(0, lastWriteStart + 1 s − now)`. So the first change after a quiet minute writes at once, and a burst of changes writes once per second, always with a final write after the last change |
| Write | The timer callback clears the dirty flag **before** reading the inputs, so a change during the write schedules another write rather than being lost. `lastWriteStart` is set when the callback starts |
| Same content | The writer hashes the document and the context file with their `written` value removed. Equal to the last hash, and both files exist: nothing is written |
| Failure | An `IOException` or `UnauthorizedAccessException` (an AI tool or editor holds the file open on Windows) is logged as a warning and the old files stay. The writer retries after 5 seconds, up to 5 times in a row, then waits for the next change |
| Stop | Disposes the timer and the subscriptions. A callback that is already running finishes |

The callback catches only those two exception types. Anything else is a bug and must reach the
host, not be swallowed.

#### The write

1. Read every input once (each is an immutable snapshot). The inputs are not one atomic view; a
   change between two reads schedules another write one second later, which corrects it.
2. Build `RoleInfo[]` from `RoleLister`, `InstalledSkill[]` from `SkillStore.All`, and the
   App Tool names from `SkillGrants.Grantable`, sorted ordinally.
3. Render the body: `prompts.Render("installSkillPack.body", values)`, with the values below.
4. Compose the document and the context JSON; compare hashes (above).
5. Write `install-context.json` to `install-context.json.tmp-<guid>` in `{DataDir}`, then
   `File.Move(temp, path, overwrite: true)`; then the same for `InstallSkillPack.md`. UTF-8
   without a byte-order mark, `\n` line endings. A temp file left by a failure is deleted in a
   `finally`.

| Body placeholder | Value |
| --- | --- |
| `{{computer}}` | `Team:ComputerName`, else `Environment.MachineName` |
| `{{skillSourcesDir}}` | The Skill Sources folder, full path |
| `{{registryPath}}` | `{skillSourcesDir}/SKILLS.md`, full path |
| `{{validateCommand}}` | The command line below, without a trailing path or options |
| `{{checkHints}}` | A Markdown table generated from `CheckCatalog.All` ([§6.3](#63-the-checks)): code, severity, what it checks and how to fix it, one row per code in use. The body holds no hand-written hint table, so the hints the AI tool reads cannot differ from the ones the Human sees |

`{{validateCommand}}` is `"<huddle-packs.exe>" validate --data-dir "<DataDir>"`. When
`Team:HuddlePacksPath` (or the default of [§8.7](#87-configuration-huddle-reads)) ends in
`.dll`, it is `dotnet "<huddle-packs.dll>" validate --data-dir "<DataDir>"`. All paths are full
paths with Windows separators on Windows.

#### The front matter

Generated, never configurable. Order and keys are fixed; the keys are the vision's, plus
`context`.

```yaml
---
huddle: 2.4.0
schemaVersion: 1
dataDir: E:\Huddle\App_Data
skillSourcesDir: E:\Huddle\App_Data\SkillSources
registry: E:\Huddle\App_Data\SkillSources\SKILLS.md
context: E:\Huddle\App_Data\install-context.json
validate: '"C:\Program Files\Huddle\huddle-packs.exe" validate --data-dir "E:\Huddle\App_Data"'
computer: DESK-01
written: 2026-10-17T16:25:00+02:00
---
```

| Key | Value |
| --- | --- |
| `huddle` | The assembly's `AssemblyInformationalVersion` with any `+commit` suffix cut: the same string `huddle-packs --version` prints, which `X002` compares |
| `schemaVersion` | The `schemaVersion` of the pack, catalog and `source.json` files (§7.2), which is also the context file's |
| `dataDir`, `skillSourcesDir`, `registry`, `context` | Full paths |
| `validate` | The `{{validateCommand}}` value, always in single quotes |
| `computer` | As `{{computer}}` |
| `written` | `timeProvider.GetLocalNow()` as `yyyy-MM-dd'T'HH:mm:sszzz`, invariant culture |

A scalar is written bare unless it is empty, contains `: ` or ` #`, starts with a YAML
indicator (`' " & * ! | > % @ ` ` [ ] { } ? - #`) or has leading or trailing space; then it is
single-quoted with `'` doubled. `validate` always contains `"`, so it is always quoted.

#### The document

```text
<front matter>

# InstallSkillPack.md

<the rendered body: the 11 numbered sections of Appendix C>

## Appendix A. Roles
## Appendix B. Installed Skills
## Appendix C. SKILLS.md today
## Appendix D. Where to look
```

The title and the four appendix headings are fixed in code. The body is the only configurable
part ([Appendix C](#appendix-c--default-prompt-texts) holds its default); a hand edit of
anything else is overwritten by the next write that changes content. The body's own headings
are `## 1.` to `## 11.`, which is why a prompt can point at "section 9" and
[§8.6](#86-reading-and-copying-while-the-ai-tool-writes) can call the temporary-file request
"§2".

#### The appendices

In this part, "appendix A" to "appendix D" are the four generated appendices of
`InstallSkillPack.md`; the specification's own [Appendix C](#appendix-c--default-prompt-texts)
holds the default prompt texts. All four are Markdown tables, so a model reads columns without parsing prose. A cell is
written by one function: `|` becomes `\|`, line breaks and runs of white space become one space,
the text is trimmed, and a description over 160 characters is cut with `…`. An id, name or file
name sits in backticks (a backtick inside it is dropped).

| | Heading | Columns | Rows | Cap |
| --- | --- | --- | --- | --- |
| A | Roles | `Id`, `Title`, `Description` | `RoleLister.List()` | **200** |
| B | Installed Skills | `Name`, `Origin`, `Built in` | `SkillStore.All`, by name ordinal | **300** |
| C | SKILLS.md today | Rows: `Table`, `Local path`, `State`, `Id`, `Source`. Sources: `Source`, `Kind`, `Status`, `Commit`, `Clone` | One row per SKILLS.md row, then one per Source, both in file order | 100 each |
| D | Where to look | `Catalog`, `Source`, `Skills`, `Downloaded`, `File` | One row per catalog, in file order | 100 |

A row past a cap is dropped and one line follows the table, in italics, with the exact text:

| Appendix | Line |
| --- | --- |
| A | *{N} more; run huddle-packs roles.* |
| B | *{N} more; run huddle-packs installed.* |
| C | *{N} more; open SKILLS.md.* |
| D | *{N} more; run huddle-packs skills.* |

`N` is the count dropped. Only A and B have caps in the vision; C and D follow because a
shared folder can list many Sources, and one row per file stays small in practice. Nothing is
capped in `install-context.json`.

How each cell is filled:

| Cell | From |
| --- | --- |
| B `Origin` | The `origin` in `Skills/<name>/.source.json` ([§6.6](#66-skillcopier-and-sourcejson)); `built in` for a `SkillSource.Default` or `Overridden` Skill; `yours` otherwise |
| B `Built in` | `yes` for `Default` or `Overridden`, else `no` |
| C `Local path` | The path this computer resolves to, or *(no entry for DESK-01)* |
| C `State` | The row's state from [§8.3](#83-resolving-packs-catalogs-and-skills): `Available`, `In use by N`, `Update available`, `Not downloaded`, `Deprecated`, `Withdrawn`, or `Broken (P005)` with the first error code |
| C `Id` | The Pack or catalog `id`, or `-` when the file did not load |
| C Sources `Status`, `Commit`, `Clone` | `source.json`'s `status` (and `statusReason`), the first 7 characters of `commit`, and `localPath`; empty when absent |
| D `Skills`, `Downloaded` | The catalog's entry count, and how many resolve to a folder on this computer |

Appendix A example:

```markdown
## Appendix A. Roles

| Id | Title | Description |
| --- | --- | --- |
| `bookkeeper` | Bookkeeper | Keeps the books and prepares statements |
| `developer` | Developer | Writes and changes code |
```

#### The context file

`install-context.json` has the fields the vision lists. Its type, `InstallContext`, lives in
`Huddle.SkillPacks` ([§5.3](#53-contracts-fixed-before-any-stream-starts), schema in
[§7.2](#72-schemas)) so the writer and `huddle-packs` share one definition. Property names are
camelCase, the file is indented, and it is written with one `private static readonly
JsonSerializerOptions`.

```json
{
  "schemaVersion": 1,
  "huddleVersion": "2.4.0",
  "computer": "DESK-01",
  "dataDir": "E:\\Huddle\\App_Data",
  "skillSourcesDir": "E:\\Huddle\\App_Data\\SkillSources",
  "skillsDir": "E:\\Huddle\\App_Data\\Skills",
  "roles": [
    { "id": "bookkeeper", "title": "Bookkeeper", "description": "Keeps the books and prepares statements", "suggestedPacks": [] }
  ],
  "installed": [
    { "name": "team-building", "origin": null, "sha256": null, "builtIn": true },
    { "name": "target-authoring", "origin": "https://github.com/dotnet/skills/tree/a660de8…/target-authoring",
      "sha256": "9f2c…", "builtIn": false }
  ],
  "appTools": ["file_request", "list_teams", "propose_sprint"],
  "written": "2026-10-17T16:25:00+02:00"
}
```

| Field | From |
| --- | --- |
| `roles` | `RoleLister.List()`, all of them, each with `suggestedPacks` (the Pack ids the hiring pool suggests for it; `[]` when none). `huddle-packs` raises `R008` from it |
| `installed` | `SkillStore.All`, all of them. `origin` and `sha256` come from `.source.json` and are `null` for a built-in or hand-written Skill |
| `skillsDir` | The folder `Team:Acp:SkillsDir` resolves to |
| `appTools` | `SkillGrants.Grantable`, sorted ordinally. `S011` checks a fetched `SKILL.md`'s `tools:` line against it |
| `written` | The same instant as the document's |

**Implementation notes.**

- `InstallFileWriter` lives in `src/Huddle.App/SkillSources/`. It is registered with
  `AddHostedService` immediately after `RegistryWatcher`; a test pins the order.
- `Reload` on the Packs tab and a change to the Skill Sources folder on the Sources tab call
  `RequestWrite()`.
- The body default is also what **Reset all** restores. Settings › Prompts validates an edited
  body: the five placeholders in the table above are required, so a reworded body cannot lose
  the validate command or the check hints.
- `PromptTiming` for `installSkillPack.body` is `Live`: an edit reaches the next write, not a
  running Agent.

**Constraints.** The writer never reads or writes anything under the Skill Sources folder, and
never throws out of a timer callback for an I/O reason.

**Errors.**

| Condition | Result |
| --- | --- |
| `{DataDir}` not writable, or the target held open | Warning logged: *"InstallSkillPack.md could not be written: {Message}. Retrying in 5 seconds."* Old files stay |
| Body prompt renders empty | Cannot happen: `PromptValidator` rejects an empty override. A test pins that the default renders |
| No `huddle-packs` beside the app | `validate` falls back to `dotnet "<path>.dll"`; if neither file exists the path is still written and `huddle-packs` is missing; the Sources tab shows the command line, so the Human sees it |

**Tests** (`tests/Huddle.Tests/SkillPacks/Install/`; a temp `{DataDir}`, `FakeTimeProvider`, hand-written
fakes for the stores):

| Test class | Pins |
| --- | --- |
| `InstallFileWriterTests` | Both files written at start, context first; front matter keys in order and YAML quoting; the `dotnet` fallback; the body from its prompt key with all five placeholders filled; at most one write a second, none when nothing changed, none lost during a write; a locked file retried; no temp file left; a reader never sees half a file; registered after `RegistryWatcher` |
| `InstallAppendixTests` | Appendices A–D in order, the caps and their exact "N more" lines, cell escaping |
| `InstallContextFileTests` | The JSON has every §7.2 field, is uncapped, carries `suggestedPacks`, nulls for built-ins, `appTools` sorted |
| `RoleListerTests` | The union of custom roles, pool jobs and team-type roles, sorted and de-duplicated |
| `InstallBodyDefaultTests` | Eleven numbered sections; the raw default is at most 9,600 bytes, and the rendered fixed part (body plus the generated `{{checkHints}}` table, appendices excluded) at most 20,480 with 260-character paths ([Appendix C](#appendix-c--default-prompt-texts)); an override missing a required placeholder is rejected |

---

### 6.8 Prompts the Human pastes

**Purpose.** Build, in one place, every prompt the Human copies into their AI tool, and the
path of `InstallSkillPack.md` in the format their tool can open. Nothing here talks to a tool or
the network; it returns text.

**Responsibilities.**

- One method of `SkillPromptBuilder` per prompt: each of the four tasks in `G5`, plus **Fetch one
  Skill** and **Needs setup**, which have no task in `G5`.
- Render the prompt's key with named placeholders, through `IPromptSource`, so
  Settings › Prompts can reword every one ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)).
- Keep Human-typed and file-derived text from breaking the prompt: one line, capped, and
  findings shown as an indented block.
- Convert the install file's path to the format the Human picks (`G5b`).

#### The prompts

Each prompt starts by telling the AI tool to read `InstallSkillPack.md` at `{{installFile}}`
first. The defaults are in [Appendix C](#appendix-c--default-prompt-texts).

| Key | Method | Placeholders (required in bold) | Called by |
| --- | --- | --- | --- |
| `skillPrompt.add` | `Add(where, purpose, format)` | **`{{installFile}}`**, **`{{where}}`**, `{{purposeLine}}` | `G5` task **Add from a link or folder**, opened from the Pack bar's **Add with your AI tool** (`G4b`), the Skills tab's **Import skill** and the Packs empty state (`G4d`) |
| `skillPrompt.findForRole` | `FindForRole(roleId, roleTitle, purpose, format)` | **`{{installFile}}`**, `{{roleTitle}}`, **`{{roleId}}`**, `{{purposeLine}}` | `G5` task **Find Packs for a role**, with the role filled in when opened from an Agent card's **Find more Packs for Developer** |
| `skillPrompt.update` | `Update(sourceIds, format)` | **`{{installFile}}`**, **`{{sources}}`** | `G5` task **Check for updates**, opened from the Pack bar's **Check for updates** |
| `skillPrompt.fix` | `Fix(table, localPath, state, findings, format)` | **`{{installFile}}`**, `{{table}}`, **`{{localPath}}`**, `{{state}}`, `{{problems}}` | `G5` task **Fix a problem**; **Copy fix prompt** on a **Broken** or **Not downloaded** Pack or catalog card (`G4c`) |
| `skillPrompt.fetchSkill` | `FetchSkill(skillName, catalogId, catalogPath, origin, format)` | **`{{installFile}}`**, **`{{skillName}}`**, `{{catalogId}}`, `{{catalogPath}}`, `{{origin}}` | **Copy prompt** beside a **Not downloaded** Skill, on an Agent card, the role form, a draft's Skills list and the Skills tab |
| `skillPrompt.needsSetup` | `NeedsSetup(roleId, roleTitle, missing, format)` | **`{{installFile}}`**, `{{roleTitle}}`, **`{{roleId}}`**, **`{{missing}}`** | **Copy prompt** on a **Needs setup** draft card in Build a team (`BuildTeam.razor`, beside `skillsBlocked`) |

All six have `PromptTiming.Live`: the prompt is built when the Human clicks, so an edit reaches
the next copy. A placeholder in the template that the method does not fill is left as written
by the renderer; a test pins that every placeholder of every key is filled.

`G5`'s own fields supply the arguments: the task choice (`G5a`) picks the method, the fields
pick the values, and the read-only box shows the result. `G5` never writes anything. **Copy fix
prompt** calls `Fix` directly with the row's state and findings.

#### The builder

```csharp
/// <summary>How a path is written for the AI tool that will open it. Public because the Razor components take it as a <c>[Parameter]</c>.</summary>
public enum PathFormat { Windows, Wsl, ForwardSlashes }

/// <summary>Builds every prompt the Human pastes. Takes the install file's location through an interface, not the writer itself, so a test passes a fixed path.</summary>
internal sealed class SkillPromptBuilder(IPromptSource prompts, IInstallFileLocation installFile)
{
    /// <summary>The install file's path in the chosen format, unquoted; what <b>Copy path</b> copies.</summary>
    public string InstallFilePath(PathFormat format);

    public string Add(string where, string? purpose, PathFormat format = PathFormat.Windows);
    public string FindForRole(string roleId, string roleTitle, string? purpose, PathFormat format = PathFormat.Windows);
    public string Update(IReadOnlyList<string> sourceIds, PathFormat format = PathFormat.Windows);
    public string Fix(string table, string localPath, string state, IReadOnlyList<Finding> findings, PathFormat format = PathFormat.Windows);
    public string FetchSkill(string skillName, string catalogId, string? catalogPath, string? origin, PathFormat format = PathFormat.Windows);
    public string NeedsSetup(string roleId, string roleTitle, IReadOnlyList<MissingItem> missing, PathFormat format = PathFormat.Windows);
}
```

`Finding` is the library's `Finding(Severity, Code, File, Message)`. `MissingItem(Name, IsPack, State, Reason, Title, CatalogId)` is the record `SkillRegistry.CheckReady` returns in `SkillReadiness.Missing` ([§6.5](#65-skillregistry-and-registrywatcher)); `NeedsSetup` takes those items as they are. The Human's last choice of
`PathFormat` is held by a scoped `PathFormatChoice` service (default `Windows`, not saved), so
the dialog, the Sources tab and **Copy fix prompt** agree for a session.

**Filling the values.** Every method builds a `Dictionary<string, string>` keyed by the token
with braces, as `PromptRenderer` expects, then calls `prompts.Render(key, values)` and tidies the
result.

| Value | Rule |
| --- | --- |
| `{{installFile}}` | The converted path. If it contains white space it is wrapped in double quotes, so `Read "E:\My Files\InstallSkillPack.md"` stays one argument |
| Any text the Human typed (`where`, `purpose`, `roleTitle`) | One line: control characters dropped, runs of white space (including line breaks) one space, trimmed. `where` is cut at 500 characters, `purpose` at 300, `…` marking a cut. An empty `where` throws `ArgumentException` |
| `{{purposeLine}}` | `What they are for: <purpose>.` for `Add`, `It should get better at: <purpose>.` for `FindForRole`; a final `.` of the text is not doubled; empty when `purpose` is blank |
| `{{sources}}` | `all the Sources in SKILLS.md` when `sourceIds` is empty, else one `- <source id>` line each |
| `{{table}}`, `{{state}}`, `{{localPath}}` | As given, one line each |
| `{{problems}}` | Errors first, then warnings, at most 20, each `<ERROR\|WARN> <code> <file>: <message>` on one line of at most 300 characters, indented four spaces so a reader treats it as quoted data; past 20: `    … and N more; run validate on the row's path.` No findings: `    (none recorded)` |
| `{{catalogPath}}` | The catalog file's path for this computer, or `(no path on this computer)` |
| `{{origin}}` | As given, or `(unknown)` |
| `{{missing}}` | One line per `MissingItem`: `Pack "<Title>" (<Name>)`, e.g. `Pack "MSBuild Authoring" (msbuild-authoring)`; or `Skill "<Name>" (catalog <CatalogId>)`; or `Skill "<Name>" (not in any catalog)` when `CatalogId` is null |

The renderer runs one pass, so a value that contains `{{…}}` is copied as text and never
expanded again. **Tidy** then trims trailing spaces from each line, replaces three or more
line breaks by two, and trims the end: a line holding only an empty `{{purposeLine}}` leaves a
single blank line, not a gap.

#### Copy path formats (`G5b`)

`PathFormats.Convert(string windowsPath, PathFormat format)` is a pure static function. Its
input is a full Windows path (`Path.GetFullPath` of `InstallFilePath`).

| Format | Rule | Example in → out |
| --- | --- | --- |
| **Windows path** | The input with every `/` turned into `\` | `E:\Huddle\App_Data\InstallSkillPack.md` |
| **Forward slashes** | Every `\` turned into `/`; the drive letter and its case stay | `E:/Huddle/App_Data/InstallSkillPack.md` |
| **WSL path** | `X:\rest` becomes `/mnt/x/rest` (drive letter lower-cased, `\` to `/`, the rest's case kept) | `/mnt/e/Huddle/App_Data/InstallSkillPack.md` |

Edge cases, all pinned by tests:

| Input | Windows | Forward slashes | WSL |
| --- | --- | --- | --- |
| `\\?\C:\x\f.md` (extended prefix) | `C:\x\f.md` | `C:/x/f.md` | `/mnt/c/x/f.md` |
| `\\server\share\f.md` (UNC) | unchanged | `//server/share/f.md` | `//server/share/f.md` (WSL cannot mount it on its own) |
| `\\wsl$\Ubuntu\home\emre\f.md`, or `\\wsl.localhost\Ubuntu\home\emre\f.md` | unchanged | `//wsl$/Ubuntu/home/emre/f.md` | `/home/emre/f.md` |
| Drive root `C:\` | `C:\` | `C:/` | `/mnt/c/` |
| A path already starting `/` (Huddle on Linux) | unchanged | unchanged | unchanged |
| A path with spaces | unchanged | unchanged | unchanged; the **prompt**, not **Copy path**, adds the quotes |

A path with no drive and no leading `/` or `\\` throws `ArgumentException`: the builder always
passes a full path.

#### A catalog entry, in the house style

The seven `PromptDefinition`s are appended to `PromptCatalog.BuildAll()`, after the existing
entries. Placeholders are listed **with their braces**, as the other entries do. This is the
shape of one; the text in `Default` is the one in Appendix C.

```csharp
new PromptDefinition(
    Key: "skillPrompt.add",
    Label: "Add Skills or Packs (prompt you paste)",
    HelperText:
        "The prompt Settings › Skills copies for your AI tool when you ask it to add Packs or Skills " +
        "from a link or folder. Must keep {{installFile}} and {{where}}: without the first the tool " +
        "does not know what to read, and without the second it does not know where to look.",
    Default:
        """
        (the skillPrompt.add text of Appendix C, verbatim)
        """,
    Placeholders: ["{{installFile}}", "{{where}}", "{{purposeLine}}"],
    RequiredPlaceholders: ["{{installFile}}", "{{where}}"],
    Timing: PromptTiming.Live),
```

The same change touches four more places, in the same commit:

| Place | Change |
| --- | --- |
| `src/Huddle.App/prompts.default.json` | Seven new keys, regenerated from the catalog (`PromptDefaultsFileTests` fails otherwise) |
| `Components/Settings/PromptFieldFactory.cs` | `GroupOrder` gains `("installSkillPack.", "Skill Packs: install file")` and `("skillPrompt.", "Skill Packs: prompts you paste")`. A key that matches no prefix shows in no group, so without this the seven prompts could not be edited |
| `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs` | `All_HasExpectedPromptCount` goes from 95 to 102 |
| `docs/Huddle.EngineeringGuide.md` and the prompts page the guide names | The seven keys, with their placeholders and timing |

**Constraints.** The builder holds no state beyond its two services. It never reads
`SKILLS.md` itself; the caller passes the row, findings and Source ids from `SkillRegistry`.

**Errors.**

| Condition | Result |
| --- | --- |
| `where` blank | `ArgumentException` from `ArgumentException.ThrowIfNullOrWhiteSpace`; `G5` disables **Copy prompt** until it has text |
| A user override drops a required placeholder | Saved as an error by `PromptValidator` (Settings › Prompts shows it); the builder never sees it |
| A path not rooted | `PathFormats.Convert` throws `ArgumentException` |

**Tests** (`tests/Huddle.Tests/SkillPacks/Prompts/`; a hand-written `IPromptSource` fake that returns
the catalog defaults and records the values it was given):

| Test class | Pins |
| --- | --- |
| `SkillPromptBuilderTests` | Each method fills its placeholders, typed text becomes one capped line and is never expanded, the `{{purposeLine}}`, `{{sources}}`, `{{problems}}` and `{{missing}}` rules, a quoted install path with spaces |
| `SkillPromptDefaultsTests` | Every default starts by telling the tool to read the install file, is 5 to 15 lines, leaves no `{{…}}` once rendered, and is in `prompts.default.json` |
| `PathFormatsTests` | The three formats and every edge case in the table above |
| `PromptCatalogTests` (updated) | The seven keys sit in the two new Settings groups, placeholders are braced, required ones are a subset; the count is 102 |

### 6.9 Settings › Skills (G4–G8) and its guidance text

#### 6.9.1 What it is

Settings › Skills (`G3` today, a single `SkillsPanel` table) becomes three tabs, **Skills**,
**Packs** and **Sources** (`G4a`), over one `SkillRegistry` snapshot ([§6.5](#65-skillregistry-and-registrywatcher)).
It draws what the registry resolved and writes almost nothing itself:

| It reads | It writes |
| --- | --- |
| `SkillRegistry.Current` (`ResolvedRegistry` plus holders, see below), `SkillStore.All` and `Issues`, `SkillSourcesSettings`, `PersonaStore` (through `SkillRegistry.HolderNames`) | `skill-sources.json`: `dir`, through `SkillSourcesSettings.SetDirAsync` (the Folder button); `seen` is owned by `SkillRegistry`, and leaving a tab calls `SkillRegistry.MarkSeenAsync`, which writes it ([§6.5](#65-skillregistry-and-registrywatcher)). Also `hidden.json` (Hide), and, through the flows of [§6.12](#612-removing-and-hiding), SKILLS.md rows and Source folders. Nothing else. It never writes a pack or catalog file and never opens a network connection |

The prompt a card copies is built by `SkillPromptBuilder` ([§6.8](#68-prompts-the-human-pastes)); the
copy of a Skill into `Skills/` is `SkillCopier` ([§6.6](#66-skillcopier-and-sourcejson)). This section
decides where they are called from.

**Two constraints from `rules.md` shape every component below.** A `[Parameter]` may not be of an
internal type (Razor generates public component types), and `SkillRegistry`, `PackEntry` and the
rest are internal to the App. So every part component takes only plain values (an id string, a
`bool`, a `long` revision) and `@inject`s `SkillRegistry` to read what it draws. A child does not
re-render when its parameters are unchanged, so the host passes `Revision="@this.revision"`, a copy
of `SkillRegistry.Current.Version`, which the registry increments on every snapshot; each part's
markup reads `this.Registry.Current` and treats `Revision` as the reason to redraw.

**What the UI needs from the snapshot.** Every member is the one [§6.5](#65-skillregistry-and-registrywatcher) defines; nothing here is renamed.

| Needs | Used for |
| --- | --- |
| `Current.Version` (`long`), `Current.Resolved.ReadAt` (`DateTimeOffset`) | The re-render key; "SKILLS.md read 1 minute ago" |
| `Current.Resolved.Packs` and `.Catalogs` (`PackEntry`, `ResolvedCatalog`): `Id`, `Title`, `Version`, `Source`, `AppliesTo`, `ConsultWhenAdds`, `SkillNames`, `MissingSkills`, `Row` (`LocalPath`, `OriginUrl`, `LastUpdated`, `Notes`), `State`, `Findings`; `SkillRegistry.HolderNames(pack)` | Cards, Details, Sources rows |
| `Current.Resolved.Skills` (`SkillEntry`): `Name`, `Description`, `Source`, `Origin`, `License`, `Category`, `SubCategory`, `FolderPath`, `State`, `Findings`; `HolderNames(skill)`, `IsHidden(skill)`. `ModifiedLocally` is not in the registry: it comes from the `SkillStore` Skill with the same name | The Skills tab, the pickers |
| `Current.Resolved.Sources` (`ResolvedSource`): `Id` (`github.com/anthropics/skills`), `File` (`Kind`, `Url`, `Commit`, `FetchedAt`, `FetchedBy`, `Status`, `StatusReason`, `MovedTo`), `HasNotes`; `PacksOfSource(id)`, `CatalogsOfSource(id)` | The Sources tab, the Remove flows |
| `Current.Resolved.Findings` (all, with `Finding(Severity, Code, File, Message)`) | **Broken** causes, the section problem count, the **Problems** list |
| `DisplayState(item)`, `Current.NewKeys`, `Current.Resolved.RowKey(pack)` | The chips, **New**, the keys passed to `MarkSeenAsync` |
| `PacksForRole(string roleId)`, `Of(string skillName)`, `SubCategoryOf(string skillName)` | [§6.10](#610-agent-card-role-form-and-build-a-team) |
| `Task ReloadAsync(CancellationToken)`, `event Action<RegistryChange>? Changed`, `Task MarkSeenAsync(...)` | **Reload**, subscriptions, leaving a tab |

`ItemState` is the library enum of [§6.2](#62-registry-model-and-file-readers) (`Available`,
`UpdateAvailable`, `NotDownloaded`, `Deprecated`, `Withdrawn`, `Broken`, plus `InUse` and `New`,
which only `SkillRegistry.DisplayState` produces). The UI never recomputes a state: a chip shows
`DisplayState(item)`, and the one extra chip is **In use by N** ([§6.9.4](#694-state-chips)).

#### 6.9.2 Component breakdown

All files are under `src/Huddle.App/Components/Settings/Skills/` (new folder) unless stated. Every
control follows the house defaults: `Size.Small` buttons and chips, `Variant.Outlined` and
`Margin.Dense` inputs, sentence-case labels, an `aria-label` on every icon-only control
([UXDesignGuidelines.md](engineering/UXDesignGuidelines.md), "The rules most often broken").

| Component | Draws | MudBlazor / house parts | Parameters | Subscribes to |
| --- | --- | --- | --- | --- |
| `SkillsPanel` (rewritten, `Components/Settings/`) | `G4`, `G4a` | `MudTabs` (three `MudTabPanel`s, `KeepPanelsAlive="false"`; the Skills and Packs headers carry a `MudBadge` with the count of **New** items on that tab, the Sources header none) | none. Reads `?view=skills\|packs\|sources` (default `skills`) | `SkillRegistry.Changed`, `SkillStore.SkillsChanged`, `SkillSourcesSettings.Changed`; each through `RendererDispatch.RunAsync(this.InvokeAsync, …)`; unsubscribes in `Dispose` |
| `SkillsTab` | The Skills list | `SettingsIntro`, `SearchField` (`Compact`), a `TaskFilterChip` "State: All ⌄", a `MudSwitch` "Show hidden", `MudDataGrid<SkillRow>` (read-only, `Hover`, `Dense`, no column options, no paging: about 300 rows), `ProblemList` | `Revision` | none; the host passes `Revision` |
| `PacksTab` | `G4b`, `G4c`, `G4d` | `SettingsIntro`, `PackBar`, a `MudStack` of `PackCard`s, `PackEmptyState` | `Revision`, `Highlight` (string, a pack id to flash) | none |
| `PackBar` | `G4b` | `MudToolBar Dense Gutters="false"` with `.toolbar-hairline`; three `MudButton`s; a status `MudText Typo.caption` | `Revision` | none |
| `PackCard` | `G4c` | `MudPaper Outlined`, `SkillStateChips`, a `MudMenu` (`Filled.MoreVert`, `.hover-reveal-menu-button`) | `PackId`, `Revision`, `Flash` (bool) | none |
| `PackEmptyState` | `G4d` | `EmptyState` (`Title`, `Text`, `Action` holding the three steps and the buttons) | none | none |
| `SourcesTab` | The Sources tab (`G4f`) | `SettingsIntro`, `StoredAt` ("Skill Sources folder"), `SourceRow`s, catalog rows, `ProblemList` | `Revision` | none |
| `SourceRow` | One Source | `MudPaper Outlined`, `StatusChip` for its `status`, a `MudMenu` | `SourceId`, `Revision` | none |
| `SkillStateChips` | The chips of one item | `StatusChip` per chip, each in a `MudTooltip` (`ShowOnFocus`; verify the parameter against 9.10.0, [MudBlazorImplementation.md](../agents/MudBlazorImplementation.md#checking-an-api-against-9100)) | `State` (`ItemState`, a public library enum: the value of `SkillRegistry.DisplayState`), `UsedBy` (int), `Reason` (string?), `HolderNames` (string, "Jane, Omar") | none |
| `AddWithAiDialog` | `G5`, `G5a` | `MudDialog` opened with `HuddleDialogOptions.SmallForm`; `MudRadioGroup<SkillTask>`; `SearchField`-style `MudTextField`s; a read-only `MudTextField Lines="5"` with `.long-text-field`; `CopyPathMenu` | `Task` (string, a public type: `add`, `find`, `update`, `fix`, parsed to `SkillTask.Add`, `FindForRole`, `CheckForUpdates`, `Fix`), `RoleId`, `Where`, `SourceIds` (string, comma list), `RowPath`, `SkillName` | none; reads once at open (an open dialog ignores new parameters) |
| `CopyPathMenu` | `G5b` | `MudMenu` with a `MudButton` activator "Copy path ▾" and three `MudMenuItem`s | `Format` / `FormatChanged` (`PathFormat`, [§6.8](#68-prompts-the-human-pastes)), `OnCopied` | none |
| `SkillsHelpDialog` | `G8` | `MudDialog` (`SmallForm`), Close only; the help text of one tab rendered as Markdown | `View` (string: `skills`, `packs`, `sources`) | none |
| `PackDetailsDialog` | `G6` | `MudDialog` (`SmallForm`), `MudSimpleTable` for the Skills, `MudCollapse` for the notes | `PackId` | none |
| `UpdateDialog` | `G7` | `MudDialog` (`SmallForm`), `MudCheckBox<bool>` per Agent | `PackId` (or `SkillName` for a Skill outside a Pack) | none |
| `RemovePackDialog` | `G6a` | `MudDialog` (`SmallForm`) with a three-way ending | `PackId` | none |
| `SkillArrivalWatcher` (`Components/Layout/`, no markup, in `MainLayout`) | The "ready" snackbar | `ISnackbar`, `NavigationManager` | none | `SkillRegistry.Changed` |

New plain types (all `sealed`, `internal` unless the table says `public`; in `Agency.Huddle.App.Skills.Ui`):

| Type | Role |
| --- | --- |
| `SkillStateText` | Static: `For(ItemState)` returns chip text, `Color`, icon and the one-sentence explanation. The sibling of `TaskColors` and `StatusColors`; the only place that maps a state to a look |
| `SkillProblemText` | Static: `Compose(Finding)` returns *"{message} {fix hint} ({code})"*, the hint taken from `CheckCatalog.Find(code).FixHint` ([§6.3](#63-the-checks); [§6.9.12](#6912-guidance-text)) |
| `SkillGuide` | Static: the three intros, the button sentences and `HelpFor(view)`, the Markdown of `SkillsHelpDialog` ([§6.9.12](#6912-guidance-text)) |
| `SkillRow` | The Skills tab's row: a `SkillStore` Skill and/or a `SkillEntry`, joined by name |
| `SkillTask` | Enum: `Add`, `FindForRole`, `CheckForUpdates`, `Fix`, `FetchSkill`, `NeedsSetup`. The first four are the radio choices of `G5a`; `FetchSkill` and `NeedsSetup` are the prompts that copy at once without a dialog ([§6.9.7](#697-the-prompt-dialog-g5-g5a-g5b)). `SkillPromptBuilder` and `PromptClipboard` take it |
| `SkillSourcesSettings` (`Agency.Huddle.App.Skills`, singleton, [§8.7](#87-configuration-huddle-reads)) | `string Dir` (`skill-sources.json` `dir`, else `Team:SkillSourcesDir`, else `{DataDir}/SkillSources`); `Task<bool> SetDirAsync(string? dir, CancellationToken ct)` (writes only `dir`, `null` drops the override, returns `false` and writes nothing when the folder does not exist); `event Action? Changed`. It does not own `seen`: that is `SkillRegistry`'s ([§6.5](#65-skillregistry-and-registrywatcher)), and the two share the file through one writer that merges keys |
| `PackUpdatePlan`, `PackUpdatePlanner` | The pure computation behind `G7` ([§6.9.8](#698-the-update-card-g7)) |
| `PathFormat` (**`public`**: it is the type of the `CopyPathMenu.Format` parameter), `PathFormats` | Defined in [§6.8](#68-prompts-the-human-pastes) (`Windows`, `Wsl`, `ForwardSlashes`; `Convert`); the Skills UI only uses them |
| `PromptClipboard` | `CopyAsync(IJSRuntime, ISnackbar, string text, CancellationToken)`; the same `copy` function in `clipboard.js` that `TaskClipboard` uses (through `ClipboardCopier`); snackbar *"Prompt copied. Paste it into your AI tool."* (key `skill-prompt-copied`) or the Ctrl+C warning |
| `PathFormatChoice` | Defined in [§6.8](#68-prompts-the-human-pastes): scoped, one `PathFormat` per circuit, default `Windows`. The Human's last choice in `G5b` survives until the page reloads. Not persisted |

`SettingsTab` and `Settings.razor` change in one place: the Skills row in `Sections` shows a
`MudBadge` with the count of **Broken** rows (Packs, catalogs and Skills whose `DisplayState` is
`Broken`) plus `SkillStore.Issues` (`G2a` "Skills 1"); `Settings` subscribes to `SkillRegistry.Changed`
for it. **That is the only meaning of the section badge.** A tab label never shows problems: it shows the
count of **New** items on that tab ([§6.9.9](#699-the-new-chip-and-the-snackbar)).

#### 6.9.3 The three tabs

**Skills tab** (`G4e`). Intro, `StoredAt` ("Copies stored at" and `SkillStore.SkillsDirectory`), then
the action row (**Import skill**, which now opens `G5` on **Add from a link or folder**, replacing
`SkillImportDialog`; **Reload**), the filter row, the grid, **Problems**.

| Column | Holds |
| --- | --- |
| Name | `SkillNames.Display(name)` at weight 600; the description below it in `Typo.caption` |
| Source | For a Skill with a copy: the existing `SkillProvenanceLine`. Otherwise the `SkillEntry` origin as *publisher · repo · short commit · licence*, or **Built in** / **Yours** for a Skill with no `.source.json`. A folder put straight into `Skills/` with no `.source.json` is **Yours**, as today, and its `tools:` is honoured (a known limit, §12) |
| State | `SkillStateChips`, plus the existing **modified locally** chip |
| Used by | `SkillRegistry.HolderNames(skill)`, truncated with `.truncate`, the full list in a tooltip |
| (menu) | **⋯**: *Copy prompt* (Not downloaded), *Review update* (Update available), *Hide* or *Unhide*, *Show catalog in Library*, *Restore default* (the existing Overridden action, with its existing confirm) |

Rows come from `SkillStore.All` joined by name with `Current.Resolved.Skills`; a registry Skill with no copy
is a row with **Not downloaded** or **Available** and no folder. Built-in and Yours Skills carry no
registry state: they show only **Built in** or **Yours** and **In use by N**. Hidden Skills are
omitted unless **Show hidden** is on, and then carry a `Filled.VisibilityOff` **Hidden** chip. The
**State** filter offers *All*, *Available*, *In use*, *Update available*, *Not downloaded*,
*Deprecated*, *Withdrawn*, *Broken*. Rows sort by name.

**Packs tab** (`G4b`, `G4c`, `G4d`). Intro, `PackBar`, a card per Pack in SKILLS.md order, then
nothing else; Packs with a problem are cards, not a list below. **Show hidden** is a `MudSwitch`
in the bar's overflow. With no Packs row at all, `PackEmptyState` replaces the bar's body (the bar
itself stays).

**Sources tab** (`G4f`).

| Block | Holds |
| --- | --- |
| Install file | The path of `{DataDir}/InstallSkillPack.md` in code type, **Copy path ▾**, and the line *This computer: DESK-01* (`Team:ComputerName`) |
| Folder | The Skill Sources folder with **Change…** (`FolderPickerDialog`; refuses a folder that does not exist; writes `skill-sources.json` through `SkillSourcesSettings.SetDirAsync`) and **Use default** (`SetDirAsync(null, ct)`) shown when `SkillSourcesSettings.Dir` differs from the folder `Team:SkillSourcesDir` configures. A line under it: *Changing the folder re-reads everything. It never moves files.* |
| Skills repository | Read only: the folder `Team:SkillsRepositoryDir` names, or *Not set*, with the line *Set in configuration; used once, when Packs moved to SKILLS.md.* It is not stored in `skill-sources.json` and has no button |
| Sources | One `SourceRow` per Source: kind icon, id path, URL, short commit, *fetched {date} by {tool}*, a `status` chip, counts (*2 Packs · 1 catalog · 61 Skills*), and **⋯** (*Show notes*, *Check for updates*, *Copy prompt* when `moved`, *Remove*). A `deprecated` or `withdrawn` Source shows its reason on the row, in text. A `moved` Source shows *This Source moved to openai/plugins* and a **Copy prompt** that adds the new one |
| Catalogs | A row per catalog: title, id, Source, *106 Skills, 1 downloaded*, **Last updated**, a state chip and **⋯** (*Show in Library*, *Copy fix prompt*, *Remove*) |

*Show notes* opens the Library at `skill-sources`, path `sources/<id>/NOTES.md`. *Check for updates* on a
row opens `G5` on **Check for updates** with that Source ticked.

#### 6.9.4 State chips

One chip per fact, drawn through `StatusChip` (tinted, never a solid fill, always a leading icon).
`SkillStateText.For` is the only mapping. Precedence is not decided here: `SkillRegistry.DisplayState`
applies it ([§6.2](#62-registry-model-and-file-readers), "Why `New` and `InUse` are in the enum"), so
the state chip is `SkillStateText.For(DisplayState(item))`, and **New** is one of its values. The
chips on a card are, in order: that state chip; **In use by N** when `UsedBy > 0` and the state chip
is not already `InUse` (the vision's card 2 shows **In use by 2** with **Update available** beneath it).

| Item state | Chip text | `Color` | Icon | Sentence (tooltip, focus and `G6`) |
| --- | --- | --- | --- | --- |
| `New` | **New** | `Info` | `Filled.FiberNew` | *Its row appeared since you last opened this tab. It is usable already; the chip only draws your eye.* |
| `Available` | **Available** | `Success` | `Filled.CheckCircle` | *It passes Huddle's checks and no Agent has it yet. You can give it to an Agent now.* |
| `InUse` | **In use by 2** | `Primary` | `Filled.Group` | *2 Agents carry it: Jane and Omar. They use a copy in Skills/, never your AI tool's clone.* |
| `UpdateAvailable` | **Update available** | `Warning` | `Filled.SystemUpdateAlt` | *The files your AI tool has now differ from the copies Agents use. Nothing changes until you apply the update.* |
| `NotDownloaded` | **Not downloaded** | `Default` | `Filled.CloudOff` | *Huddle knows this exists, but there is no folder for it on this computer. Copy a prompt and ask your AI tool to fetch it.* |
| `Deprecated` | **Deprecated** | `Default` | `Filled.NotInterested` | *Its Source still lists it but advises against it: {reason}. You can still choose it.* |
| `Withdrawn` | **Withdrawn** | `Error` | `Filled.Block` | *Its Source pulled it: {reason}. New hires cannot take it. Agents who have it keep working and are flagged.* |
| `Broken` | **Broken** | `Error` | `Filled.Error` | *A check failed or a pointer leads nowhere. The card names the check and the file. The rest of SKILLS.md still works.* |
| Hidden (flag) | **Hidden** | `Default` | `Filled.VisibilityOff` | *You hid it, so it is left out of lists. Agents who have it keep it. Unhide brings it back.* |
| Modified locally (existing) | **modified locally** | `Warning` | `Filled.Edit` | *Someone edited this Skill's copy on this computer. Applying an update replaces those edits.* |

`{reason}` is `StatusReason` from `source.json`. Amber is attention, so only **Update available**
and **modified locally** are amber; **Not downloaded** is neutral because nothing is wrong.

**The cause line.** A tooltip is never the only place a cause lives. Each card has one line of
`Typo.caption` under its title, in text, drawn from the state:

| State | Cause line |
| --- | --- |
| `Broken` | `SkillProblemText.Compose` of the first error finding for the row, naming the file, for example *"packs/document-skills.skillpack.json: appliesTo lists "accountant", which is not a role in this Huddle. Use a role from huddle-packs roles. (P005)"* |
| `NotDownloaded` | *"{skill} has no folder on this computer."* or, for several, *"3 of 4 Skills have no folder here: a, b and c."* The text says *"There is no entry for DESK-01"* when the cause is `R009` or `S001` with a per-computer path |
| `UpdateAvailable` | *"target-authoring has new files (3 changed)."* |
| `Deprecated`, `Withdrawn` | The reason, then *"(from the Source's source.json, seen {date})"* |
| `InUse` | The holders, *"Jane, Omar"* |
| `Available`, New | None |

#### 6.9.5 The bar, the cards and Reload

`PackBar` (`G4b`): **Add with your AI tool** (filled primary, `ActionIcons.Add`, opens `G5` on `add`),
**Check for updates** (outlined, opens `G5` on `update`), **Reload** (outlined, `Filled.Refresh`),
then the status line *"3 Sources · 1 new · 1 problem · SKILLS.md read 1 minute ago"*. The pieces are
joined by `·`, a count of zero is left out, `Plural.Of` forms every noun, and *read …* comes from a
new `ReadAgoText.Format(readAt, now)` (*just now*, *1 minute ago*, *N minutes ago*, *N hours ago*,
then `HumanDate.Format`) using the injected `TimeProvider`.

A Packs row that fails before any file is read still draws a card: its title is the file name, its
chip **Broken**.

`PackCard` (`G4c`), the vision's fields in the vision's order:

| Row | Holds |
| --- | --- |
| Title row | Pack title (`Typo.subtitle1`, 600); version or short commit; the chips, right-aligned |
| Meta | `Source` · *4 Skills* · *updated 2026-10-17* via `HumanDate.Format` with `HumanDate.Full` in the tooltip |
| Roles | The `appliesTo` role titles, comma-joined, `+N` past three, the full list in a tooltip |
| Cause line | See [§6.9.4](#694-state-chips) |
| Actions | **Review update** (small outlined) when `UpdateAvailable`; **Copy fix prompt** when `Broken` or `NotDownloaded`; **⋯** |

**⋯** items, in order: *Details*, *Review update* (if applicable), *Copy fix prompt* (if applicable),
*Hide* or *Unhide*, *Show in Library*, *Remove from Agents* (only `Withdrawn` with holders),
divider, *Remove* (`.menu-item-destructive`). The activator carries `MenuDialogs.ActivatorLabel(title)`
and each dialog-opening item uses `MenuDialogs.AfterClosingAsync`.

**Reload** calls `SkillRegistry.ReloadAsync`, which re-reads SKILLS.md, every pack, catalog and
`source.json`, and re-hashes every Skill folder, ignoring the 2-second grace of
[§8.6](#86-reading-and-copying-while-the-ai-tool-writes). The button shows
`MudProgressCircular Size.Small Indeterminate` and is disabled while it runs. It ends with the snackbar
*"Reloaded. 1 problem found."* or *"Reloaded. No problems."* (key `skills-reload`).

#### 6.9.6 The empty state, `G4d`

Shown while SKILLS.md has no Packs row (a missing SKILLS.md counts, and then the bar's **Reload**
stays). `EmptyState` with `Icon="Outlined.AutoAwesome"`:

| Slot | Text |
| --- | --- |
| `Title` | *No Packs yet* |
| `Text` | *Huddle does not download Skills; your AI tool does. It signs in where it needs to, fetches the Skills wherever it likes, and adds a pointer to SKILLS.md. Huddle picks it up.* |
| `Action` | An ordered list, *1. Select Add with your AI tool and say where the Skills are. 2. Paste the prompt into Claude Code, Codex or a similar tool. 3. Come back here; what it added is ready to use.*, then **Add with your AI tool**, **Copy path ▾** and the link **Learn more** (opens `SkillsHelpDialog` on Packs, [§6.9.12](#6912-guidance-text)) |

#### 6.9.7 The prompt dialog, `G5`, `G5a`, `G5b`

`AddWithAiDialog` writes nothing. Title *Add with your AI tool*. Actions: **Close** only
(`Variant.Text`); the copy buttons are inside the body. Entry points and what they pre-fill:

| From | `Task` | Pre-filled |
| --- | --- | --- |
| **Add with your AI tool**, **Import skill** | `add` | nothing |
| **Check for updates** (bar), a Source row's menu | `update` | all Sources, or the row's |
| An Agent card's **Find more Packs for {Role}** | `find` | `RoleId` |
| A **Broken** card or catalog row, **Copy fix prompt** | none: copies at once | |
| `G5a` choice **Fix a problem** | `fix` | `RowPath` when opened from a row |

Fields per task (`G5a`; a required field left empty keeps the prompt box and both copy buttons
disabled, with the field's helper text in `Color.Error`):

| Task (`G5a` radio) | Line under it | Fields |
| --- | --- | --- |
| **Add from a link or folder** | *Your AI tool fetches what you point to, writes the Pack or catalog files, checks them and adds the rows.* | **Where are the Skills?** (required, one line, at most 500 characters, a link or a folder), **What are they for?** (optional, at most 300) |
| **Find Packs for a role** | *Your AI tool looks through the catalogs you already have, suggests up to three Packs for the role and asks you before it fetches anything.* | **Role** (required; a `MudSelect` of the roles `RoleLister.List()` returns), **What should it get better at?** (optional) |
| **Check for updates** | *Your AI tool pulls newer commits for the Sources you pick and updates their files. Huddle then shows Update available where Agents' copies differ.* | **Sources**: *All Sources* switch, else a checkbox per Source |
| **Fix a problem** | *Your AI tool reads what Huddle reported about a row and repairs the row or the files it points to.* | **Which row?** (required `MudSelect` of rows that are **Broken** or **Not downloaded**, each *"Document skills — Broken (P005)"*) |

The prompt box shows the text the matching `SkillPromptBuilder` method returns (`Add`, `FindForRole`,
`Update` or `Fix`, [§6.8](#68-prompts-the-human-pastes)), with the path in the format `PathFormatChoice` holds. `G5b` (**Copy path ▾**) has **Windows path**,
**WSL path** and **Forward slashes**, each showing its own result; choosing one sets the format
(the prompt box redraws) and copies the bare path, with the snackbar *"Path copied."*. **WSL path** is
disabled with *"Not available for a network path"* when the path has no drive letter.
**Copy prompt** calls `PromptClipboard.CopyAsync`. Under the box, the three steps of the vision, and
the line *"Huddle sends this nowhere. It goes to your clipboard only."* A copy that fails selects the
text and shows the Ctrl+C warning, as `TaskClipboard` does.

Every other **Copy prompt** and **Copy fix prompt** button in the app (a Not downloaded Skill, a
draft's Needs setup) copies at once without opening `G5`, using the matching
`skillPrompt.fetchSkill`, `skillPrompt.fix` or `skillPrompt.needsSetup` template.

#### 6.9.8 The update card, `G7`

`PackUpdatePlanner.Plan` returns a `PackUpdatePlan`; the dialog only draws it. It follows the correction in [§8.4](#84-noticing-an-update): a change to a Skill's
files is shared and has no per-Agent choice; membership has. `PackUpdatePlanner` reads
`SkillRegistry.Current`, `HolderNames` and the Agent files.

```csharp
internal sealed record PackUpdatePlan(
    string PackId, string Title, string FromVersion, string ToVersion,
    IReadOnlyList<SkillFileChange> FileChanges,
    IReadOnlyList<SkillMembershipChange> Added,
    IReadOnlyList<SkillMembershipChange> Removed,
    string? ConsultWhenFrom, string? ConsultWhenTo,
    IReadOnlyList<AgentChange> Agents);

internal sealed record SkillFileChange(string Name, string FromCommit, string ToCommit, int FilesChanged,
    bool ModifiedLocally, IReadOnlyList<string> AffectedAgents);

internal sealed record AgentChange(string Name, string Role, IReadOnlyList<string> AddSkills,
    IReadOnlyList<string> RemoveSkills, bool ConsultWhenChanges, bool VersionChanges);

internal static class PackUpdatePlanner
{
    /// <summary>The plan for one Pack; <c>Agents</c> holds the Agents whose <c>_packs</c> names it.</summary>
    public static PackUpdatePlan Plan(
        string packId, SkillRegistry registry, SkillStore skills,
        IReadOnlyList<Persona> agents, IFileReader files);

    /// <summary>The plan for a Skill outside a Pack: <c>FileChanges</c> only.</summary>
    public static PackUpdatePlan PlanSkill(
        string skillName, SkillRegistry registry, SkillStore skills,
        IReadOnlyList<Persona> agents, IFileReader files);
}
```

How `FileChanges` is computed, per Skill the Pack lists whose state is `UpdateAvailable`:
`FromCommit` is the `commit` in the copy's `Skills/<name>/.source.json` (short form, seven characters);
`ToCommit` is the commit in the pointer's origin URL, `SkillEntry.Origin` (`…/tree/{commit}/…`, the
catalog or pack row's commit, [§8.3](#83-resolving-packs-catalogs-and-skills)), falling back to the Source's
`source.json` `commit` when the origin names none. `FilesChanged` compares per-file hashes of the copy and
of the pointer's folder: `SkillFolderHash.FileHashes` (new, beside `Compute`) returns relative path to
SHA-256 using the same per-file `Normalize` that `Compute` uses (CRLF to LF for text, `.git` and
`.source.json` skipped), so a line-ending difference is not a change. The count is the files whose hash
differs plus the files present on one side only. `ModifiedLocally` comes from `SkillStore`, and
`AffectedAgents` from `SkillRegistry.HolderNames`. All reads go through `IFileReader`, so the planner
is tested with `InMemoryFileReader`.

Layout (the vision's wireframe, with §8.4 applied):

```text
┌ Update: MSBuild Authoring  1.0.0 → 1.1.0 ────────────────────── G7 ┐
│ repos/skills · updated 2026-10-17                                  │
│                                                                    │
│ Skill files that change for everyone                               │
│   ~ target-authoring   a660de8 → 4c1e9a2   3 files changed         │
│     Used by Jane and Omar. Their copy changes as soon as you       │
│     update; they do not restart.                                   │
│   ⚠ You edited target-authoring on this computer. Updating         │
│     replaces your edits.                                           │
│                                                                    │
│ Skills added or removed                                            │
│   + incremental-build   dotnet/skills @ 4c1e9a2   MIT              │
│   Consult when adds  "… build failures" → "… slow incremental…"    │
│   [x] Jane   Developer          adds incremental-build; restarts   │
│   [x] Omar   Release engineer   adds incremental-build; restarts   │
│                                                                    │
│                       [ Not now ]   [ Update and replace my edits ]│
└────────────────────────────────────────────────────────────────────┘
```

| Rule | Behaviour |
| --- | --- |
| **Skill files section** | One row per Skill with `UpdateAvailable`: `~ name  fromCommit → toCommit  N files changed`, then *"Used by {names}. Their copy changes as soon as you update; they do not restart."* (*"No Agent has it yet."* with none). **No checkboxes.** A Skill the Pack newly lists (`+`) is not here; it is membership |
| **Modified locally** | Each `SkillFileChange.ModifiedLocally` row carries a `MudAlert Severity.Warning Dense` (`role="alert"`): *"You edited {name} on this computer. Updating replaces your edits."* If any row has it, the primary button reads **Update and replace my edits** with `Color.Warning`; otherwise **Update** |
| **Membership section** | `+` Skills added to the Pack (with Source, short commit and licence), `−` Skills it no longer lists, the `consultWhenAdds` change as *from → to*, and a **checkbox per Agent** that holds the Pack and whose `skills:`, `consult_when:` or `_packs` version would change. Each row says what changes: *"adds incremental-build; restarts"*. All start checked |
| **Primary button label** | **Update N** (N Agents checked) when any is checked; **Update files** when none is checked but file changes exist; disabled when the plan is empty. The vision's `[ Update 2 ]` is the first form |
| **Not now** | Closes with nothing written. Unchecked Agents keep their copies' membership and **Update available** stays until the next update |
| **Update does** | In this order: (1) `ISkillCopier.CopyAsync` copies every `FileChanges` Skill and every `Added` Skill into `Skills/` ([§6.6](#66-skillcopier-and-sourcejson)); one failure stops here and writes nothing else; (2) for each checked Agent, writes `skills:`, `consult_when:` and `_packs:` with `PackMembership` ([§6.10.4](#6104-agent-card)) and restarts it; (3) one `update-applied` line per Pack or Skill ([§7.2](#sourcessourcelogjsonl)). A Skill whose files changed is live for its holders at once because Skill bodies are read live |
| **Which Skills a removal takes** | A `−` Skill leaves a checked Agent's `skills:` only when neither the Agent's role nor another Pack it holds provides it, as `TogglePack` already does |
| **Single Skill** | Opened with `SkillName`, the dialog has the Skill files section only, titled *"Update: {name}"* |
| **Deprecated or Withdrawn** | A one-line `MudAlert` above everything: *"{Source} marks this Pack {Deprecated\|Withdrawn}: {reason}."* |

**`Removed` is always empty.** Huddle keeps no earlier copy of a Pack file, so it cannot tell which Skills a Pack has stopped listing. A Skill the Pack dropped stays on its holders until the Human removes it on the card. Guessing from the Agent file would risk taking off a Skill the Human gave by hand.

#### 6.9.9 The New chip and the snackbar

**New.** The registry decides ([§6.5](#65-skillregistry-and-registrywatcher), "The 'New' overlay"): `seen`
in `{DataDir}/skill-sources.json` holds the keys the Human has seen, `Current.NewKeys` the keys in the
snapshot that are not in it, and `DisplayState` shows **New** for an `Available` item no Agent holds.
Keys are lowercase-prefixed: `packs:<id>`, `catalogs:<id>` (`Current.Resolved.RowKey(row)`) and `skill:<name>`. What this section adds:

1. The move needs nothing here: the first complete read writes the baseline, so the move's own output
   and an upgrade never show **New**.
2. **New** stays while the Human is on the Skills section. Leaving the tab (a different `view`,
   another Settings section, or `Dispose`) calls `SkillRegistry.MarkSeenAsync` with the keys that tab
   listed. A row that arrives while the Human watches stays **New** until they leave.
3. The status line counts `NewKeys` on the Packs tab. A tab label's badge is the count of **New**
   items on that tab: `packs:`/`catalogs:` keys on Packs, `skill:` keys on Skills; Sources has none. This is
   the only meaning of a tab badge; the section-menu badge (`G2a`) counts problems instead
   ([§6.9.2](#692-component-breakdown)).

**Snackbar.** `SkillArrivalWatcher` reads each `RegistryChange` (`AppearedKeys`, `ChangedSkills`) and the new
`Snapshot`. A Pack or Skill that becomes usable (`Available`, `InUse`, `UpdateAvailable`) from absent,
`Broken` or `NotDownloaded` raises one snackbar. The watcher takes `Current` as its baseline when it is
created, so it never announces what was already there:

| Case | Text, action | Key |
| --- | --- | --- |
| One Pack | *"Document skills is ready"* · **Show** → `/settings/skills?view=packs&highlight=document-skills` | `skill-ready-<id>` |
| One Skill | *"target-authoring is ready"* · **Show** → `?view=skills&highlight=target-authoring` | `skill-ready-<name>` |
| More than three in one snapshot | *"5 Packs and Skills are ready"* · **Show** → `?view=packs` | `skill-ready-many` |

`Severity.Success`, the `ISnackbar` action `Show`. The destination scrolls the card into view and
gives it the `.search-hit-flash` highlight (which already has a reduced-motion branch). The watcher
runs wherever the Human is in the app, since the AI tool finishes while they are elsewhere.

#### 6.9.10 Details, `G6`

`PackDetailsDialog` opens from **⋯** › **Details** or a click on the card title. It draws the
vision's wireframe in `Typo` rows:

| Block | Holds |
| --- | --- |
| Header | Title; *Source @ short commit · updated {date}*; the chips |
| *What this state means* | The state's sentence from [§6.9.4](#694-state-chips) in body text, and the cause line. Always visible |
| SKILLS.md row | Local path (resolved), Origin URL as a link, **Last updated**, **Notes** |
| For, Consult when | The roles; the `consultWhenAdds` phrase in quotes |
| Skills table | `MudSimpleTable`: Skill, State chip, Licence, Files, Warnings (*Scripts, unread files, …* from `S007`–`S010`, each with its sentence on hover and in a caption below the table) |
| Findings | Every finding for the Pack and its Skills through `SkillProblemText.Compose`, errors first |
| Notes from your AI tool | **Show notes** opens the Source's `NOTES.md` in the Library; `MudCollapse` shows its first 20 lines |
| Actions | **Remove**, **Show in Library**, **Close**, with their sentences as `Typo.caption` under the row |

**Unread files** (`S008`) reads: *"Agents read only the .md files directly in a Skill's folder, up to
64 KB each, so scripts/ and references/ are ignored. A Skill that depends on them may work less well
in Huddle than in the tool it was written for."*

#### 6.9.11 Wireframes: proposed rows for `docs/engineering/wireframes.md`

New codes follow the highest existing, `G3b`. There are fourteen: `G4`, `G4a`, `G4b`, `G4c`, `G4d`,
`G4e`, `G4f`, `G5`, `G5a`, `G5b`, `G6`, `G6a`, `G7` and `G8`. Ten were fixed by the brief; `G4e`, `G4f`,
`G6a` and `G8` are additions this section needs. Add them in the same commit as the code
([UXDesignGuidelines.md](engineering/UXDesignGuidelines.md), "Two obligations"), draw their badges in
the Settings figure, and update the notes of `G2a`, `G3`, `B9b`, `B9c`, `B9e` and `B10`.

| Code | Name | Component | Notes |
| --- | --- | --- | --- |
| `G4` | Skills section | `SkillsPanel` | Replaces the single table of `G3`. Three tabs over one registry snapshot. The Skills row in the section menu (`G2a`) shows a badge: the count of Broken rows plus Skill issues |
| `G4a` | Skills tab rail | `MudTabs` | **Skills**, **Packs**, **Sources**, each with a count badge; the selected tab is `?view=` in the URL |
| `G4b` | Pack bar | `PackBar` | **Add with your AI tool** (filled), **Check for updates**, **Reload**, then "3 Sources · 1 new · 1 problem · SKILLS.md read 1 minute ago" |
| `G4c` | Pack card | `PackCard` | Title, version, chips, Source · Skill count · updated date, roles, cause line, **Review update** or **Copy fix prompt**, and **⋯** (Details, Hide, Show in Library, Remove) |
| `G4d` | Packs empty state | `PackEmptyState` on `EmptyState` | Shown while SKILLS.md lists no Packs: three numbered steps, **Add with your AI tool**, **Copy path ▾**, **Learn more** |
| `G4e` | Skills list | `SkillsTab` (`MudDataGrid`) | Every Skill: built in, yours, fetched, and catalog Skills not downloaded. Search, State filter, **Show hidden**, **Import skill**, **Reload** |
| `G4f` | Sources tab | `SourcesTab`, `SourceRow` | Install file with **Copy path ▾**, This computer, Folder, Skills repository (read only), a row per Source, a row per catalog |
| `G5` | Prompt dialog | `AddWithAiDialog` (a form dialog, `O2`) | Four tasks, the fields the task needs, the built prompt in a read-only box, **Copy path ▾**, **Copy prompt**, three numbered steps. Writes nothing |
| `G5a` | Task choice | `MudRadioGroup<SkillTask>` | **Add from a link or folder**, **Find Packs for a role**, **Check for updates**, **Fix a problem**, each with a line saying what the AI tool will do |
| `G5b` | Copy path menu | `CopyPathMenu` | **Windows path**, **WSL path**, **Forward slashes**; the prompt box follows the last choice |
| `G6` | Details card | `PackDetailsDialog` | The Source and commit, the SKILLS.md row, roles, `consult_when`, a row per Skill with licence, files and warnings, findings, notes |
| `G6a` | Remove Pack dialog | `RemovePackDialog` | Asks **Keep their Skills** or **Remove from Agents too** when Agents hold the Pack |
| `G7` | Update card | `UpdateDialog` | Skill file changes (affected Agents listed, no checkboxes), Skills added or removed, the `consult_when` change, a checkbox per Agent for membership |
| `G8` | Skills help | `SkillsHelpDialog` | In-app help for one tab, opened by **Learn more** at the end of each intro and by the empty state. Close only; text from the User Guide section |

`B9c` gains the line *"Needs setup: Pack "MSBuild Authoring" not downloaded"* with **Copy prompt**; `B10`'s
sentence about importing Skills is replaced (see [§6.10](#610-agent-card-role-form-and-build-a-team)).

#### 6.9.12 Guidance text

This is UI text; it lives in the components and in `SkillStateText`, `SkillProblemText` and
`SkillGuide` (a static class of the intros and the button sentences), not in Settings › Prompts
([ADR-0007](adr/0007-model-facing-text-is-configuration.md) covers model-facing text only).

**Rules carried over from the vision, with how this design meets each.**

| Rule | How |
| --- | --- |
| Every tab opens with a `SettingsIntro` of three to five short sentences, then **Learn more** | `SkillGuide.SkillsIntro`, `PacksIntro`, `SourcesIntro`; a test counts the sentences (the **Learn more** link is not one of them) |
| The tab's word is defined in the first sentence | Skills: "A Skill is…"; Packs: "A Pack is…"; Sources: "A Source is…" |
| Says what Huddle does not do | Each intro has "Huddle never downloads, signs in or deletes a clone it did not make" in some form |
| Says what happens next after each action | The dialog's three steps; the snackbar; the **Copy prompt** snackbar *"Prompt copied. Paste it into your AI tool."* |
| Every state chip and every button has a one-sentence explanation shown on hover, on keyboard focus and in `G6` | `SkillStateText` and the table below; a test fails when a state or button has none |
| Each task in `G5a` has a line under it | [§6.9.7](#697-the-prompt-dialog-g5-g5a-g5b) |
| Problems say which check failed, in which file, and what fixes it, the code last | `SkillProblemText.Compose` |
| Every intro ends with **Learn more**, which opens `SkillsHelpDialog`; the app links to no external guide | `SkillGuide.HelpFor(view)`; the `Learn more` link is a button-styled `MudLink` that opens `G8` |
| Nothing essential only in a tooltip | The cause line, the empty state and the state sentence in `G6` are text |

**Learn more and `SkillsHelpDialog`.** Each intro ends with **Learn more**. It opens `SkillsHelpDialog`
(`G8`), an in-app dialog with a Close button, so the Human never leaves Huddle and nothing links to
`docs/Huddle.UserGuide.md`. The dialog's text is drawn from the User Guide: the same commit adds three
subsections, "The Skills tab", "The Packs tab" and "The Sources tab", under "Giving Agents Skills",
and `SkillGuide.HelpFor(view)` holds the Markdown of the matching one. A test reads the User Guide and
fails when a paragraph of a help text is not found there word for word, so the two cannot drift. The
dialog title is the tab name (*Skills*, *Packs* or *Sources*); the text opens with a heading-free first
paragraph, uses the same words as the intro, and never exceeds 250 words.

**Intros (improved from the vision's drafts).**

| Tab | Intro |
| --- | --- |
| **Skills** | A Skill is a folder of instructions that an Agent reads when its work calls for it. This list shows every Skill Huddle knows about: built in, fetched by your AI tool, or listed in a catalog but not on this computer yet. A Skill marked **Not downloaded** has no folder here; **Copy prompt** gives your AI tool what it needs to fetch it. Huddle never downloads Skills itself. Giving an Agent a Skill copies it to that Agent, so later changes reach it only when you apply an update. **Learn more** |
| **Packs** | A Pack is a named set of Skills for some roles, such as MSBuild Authoring. Choosing a Pack on an Agent gives it all the Pack's Skills at once and tells its manager when to use them. Packs come from SKILLS.md, a list your AI tool keeps; Huddle never fetches one itself. To add one, select **Add with your AI tool**, say where it is, and paste the prompt into Claude Code, Codex or a similar tool. Whatever passes Huddle's checks is ready to use at once; there is no approval step. **Learn more** |
| **Sources** | A Source is one place Skills come from: a GitHub repository, another Git server or a folder on this computer. Your AI tool keeps notes for each Source on how it found and fetched it, so the next update starts from what it learned. Everything here lives in the Skill Sources folder, which you can share with another Huddle. Removing a Source removes its notes and rows, but never the clone itself. Your AI tool reads the install file first; **Copy path** copies its location. **Learn more** |

**Every button and menu item has one sentence.** Shown as the tooltip (hover and focus), and for
buttons in a dialog as a `Typo.caption` line under the row.

| Control | Where | Sentence |
| --- | --- | --- |
| Add with your AI tool | Bar, empty state | *Opens a prompt to paste into Claude Code, Codex or another AI tool, which fetches the Skills and adds them to SKILLS.md.* |
| Check for updates | Bar, Source menu | *Opens a prompt that asks your AI tool to look for newer versions of the Sources you pick. Huddle itself never goes online.* |
| Reload | Bar, Skills tab | *Reads SKILLS.md and every file it points to again, right now, instead of waiting for a change.* |
| Import skill | Skills tab | *Opens a prompt that asks your AI tool to fetch a single Skill from a link or folder.* |
| Copy prompt | Not downloaded Skill, draft card | *Copies a prompt that asks your AI tool to fetch this one Skill. Paste it into your AI tool.* |
| Copy fix prompt | Broken or Not downloaded card | *Copies a prompt that names the row and the checks it failed, for your AI tool to repair.* |
| Copy path ▾ | Sources tab, `G4d`, `G5` | *Copies the location of InstallSkillPack.md, in the form your AI tool can open.* |
| Windows path, WSL path, Forward slashes | `G5b` | *E:\…, /mnt/e/… or E:/…: pick the one your AI tool runs under. Huddle remembers the choice until you reload the page.* |
| Details | Card menu | *Shows where this came from, its Skills, their licences and warnings, and the notes your AI tool kept.* |
| Review update | Card, menu, Skill row | *Shows what changed and which Agents are affected, and lets you apply it.* |
| Hide, Unhide | Card, Skill row | *Leaves it out of lists on this Huddle and any that shares the folder. Nothing is deleted and Agents who have it keep it.* |
| Show hidden | Switch | *Includes what you hid, so you can unhide it.* |
| Show in Library | Card menu | *Opens the pack or catalog file, read only, in the Library.* |
| Show notes | Source row, `G6` | *Opens the notes your AI tool kept for this Source, in the Library.* |
| Remove (Pack) | Card menu, `G6` | *Removes its row from SKILLS.md and its file if Huddle's folder holds it. Your AI tool's clone stays.* |
| Remove from Agents | Withdrawn card | *Takes this Pack off every Agent that has it. Their copies of the Skills stay.* |
| Remove (catalog) | Catalog row | *Removes the catalog's row, and its file if Huddle's folder holds it. Agents keep their copies.* |
| Remove (Source) | Source row | *Deletes this Source's notes, its rows and the files they point to inside Huddle's folder. The clone stays.* |
| Change… | Folder | *Chooses another Skill Sources folder, for example a synced one shared with another Huddle. It does not move files.* |
| Use default | Folder | *Goes back to the folder from configuration.* |
| Learn more | Each intro, `G4d` | *Opens a help page in Huddle about this tab; it takes you nowhere else.* |
| Update N, Update files, Update and replace my edits | `G7` | *Applies the update: copies the changed Skill files, then changes the Agents you ticked.* |
| Not now | `G7` | *Closes without changing anything.* |
| Keep their Skills, Remove from Agents too | `G6a` | *Only stops tracking the Pack, or also takes off the Skills nobody else gave them.* |

**Problem fix sentences (`SkillProblemText`).** The composed text is *"{library message} {fix hint}
({code})"*. The fix hint is `CheckCatalog.Find(code).FixHint` from [§6.3](#63-the-checks), the one table
of check codes, severities and hints (every code from `R001` to `X002`, including `S012`); this section
keeps no second list. A test iterates every code in `CheckCatalog.All` and fails when one composes
without a hint or without the code last. Two additions the UI makes, outside the hint: for `S007` to
`S010` and `S012` (warnings) it adds *"Nothing is blocked. Open Details to see the files."*, and for
`R009` and `S001` with a per-computer path, *"There is no entry for {computer}."*

#### 6.9.13 bUnit tests

In `tests/Huddle.Tests/Ui/SkillPacks/`. Each has a `///` summary; the first column names the file.

| Test class | Pins |
| --- | --- |
| `SkillsPanelTests` | Three tabs render in order Skills, Packs, Sources; `?view=packs` selects Packs; an unknown `view` selects Skills; A `SkillRegistry.Changed` raised off the UI thread repaints the open tab; `Dispose` unsubscribes all three events; A tab header badge counts that tab's **New** items; the section-menu badge (`G2a`) counts Broken rows plus `SkillStore.Issues`; zero shows no badge |
| `SkillStateChipsTests` | Every `ItemState` (including `New` and `InUse`) renders its text, `status-chip-{colour}` class and icon from `SkillStateText`, and none is colour-only; `InUse` shows "In use by 2" and a state of `UpdateAvailable` with holders shows both chips; A state's sentence is in the tooltip and the chip is keyboard focusable |
| `SkillStateTextTests` | Every `ItemState` value has chip text and a one-sentence explanation ending in a full stop |
| `PackCardTests` | Title, version, Source, Skill count, roles, updated date via `HumanDate`, and chips render; roles past three show "+N"; A `Broken` card shows the composed cause line and **Copy fix prompt**, not **Review update**; An `UpdateAvailable` card shows **Review update**; `NotDownloaded` names the missing Skills in text; The **⋯** menu order is Details, Review update, Copy fix prompt, Hide, Show in Library, divider, Remove; Remove from Agents appears only for `Withdrawn` with holders |
| `PackBarTests` | The three buttons exist; the status line joins "3 Sources · 1 new · 1 problem · SKILLS.md read 1 minute ago" and leaves out a zero count; **Reload** calls `ReloadAsync`, disables while it runs and ends with the keyed snackbar |
| `ReadAgoTextTests` | Boundaries: just now, 1 minute ago, N minutes ago, N hours ago, then a date |
| `PackEmptyStateTests` | With no Packs rows the empty state shows title, explanation, three steps and the three controls; with one it does not |
| `AddWithAiDialogTests` | Opening with `Task="update"` selects **Check for updates**; each radio has its sentence under it; An empty **Where are the Skills?** disables the prompt box and both copy buttons; filling it enables them; The prompt for **Add** contains the install file path and the Where text, and the purpose sentence only when given; **Find Packs for a role** names the role title and id; **Fix a problem** lists only **Broken** and **Not downloaded** rows; Choosing **WSL path** rewrites the path in the prompt box; the choice survives re-opening in the same circuit; **Copy prompt** calls the clipboard helper with the prompt and shows the keyed snackbar; a refused copy shows the Ctrl+C warning; The dialog writes no file (no `SkillSourcesSettings`, no `MarkSeenAsync` and no SKILLS.md write). The path forms themselves are pinned by `PathFormatsTests` ([§6.8](#68-prompts-the-human-pastes)) |
| `PackDetailsDialogTests` | Renders the row's Last updated and Notes, roles, the `consult_when` phrase, a row per Skill, and the state sentence as text; A Skill with `S008` shows "Scripts, unread files" and the unread-files sentence below the table |
| `PackUpdatePlannerTests` | A changed Skill file is in `FileChanges` with its affected Agents and never in `Agents`; an added Skill is in `Added`; An Agent whose `skills:` already has the new Skill and whose version matches is not listed; A `−` Skill the Agent's role also provides is not in `RemoveSkills` |
| `UpdateDialogTests` | The Skill files section has no checkbox; the membership section has one per listed Agent, all checked; A Modified locally Skill shows the warning and the button reads **Update and replace my edits**; The button reads **Update 2** with two checked, **Update files** with none checked, and is disabled for an empty plan; **Not now** writes nothing; **Update** copies before it rewrites any Agent, and a copy failure leaves every Agent file untouched |
| `SkillArrivalWatcherTests` | A Pack becoming `Available` raises one snackbar with key `skill-ready-<id>` and a **Show** action; the first snapshot raises none; Four arrivals in one snapshot raise one "ready" snackbar |
| `SkillsTabSeenTests` | A key in `Current.NewKeys` shows **New**; leaving the tab calls `MarkSeenAsync` with the keys the tab listed (and not before); a row that arrives while the tab is open stays **New** until then |
| `SkillsTabTests` | A row per Skill; a catalog Skill with no copy shows **Not downloaded** and **Copy prompt**; built-in shows **Built in**; **Hide** removes the row; **Show hidden** brings it back with the **Hidden** chip; the State filter narrows rows |
| `SourcesTabTests` | The install path, computer name and Folder render; **Change…** with a missing folder is refused; a `moved` Source shows the moved line and **Copy prompt**; A Source with `deprecated` status shows its reason in text |
| `GuidanceTextTests` | Each intro is three to five sentences, defines its word first, contains "never", and is followed by **Learn more**; no intro contains a link or a `.md` path; Every control in the button table has a non-empty sentence; every `CheckCatalog.All` code composes with its fix hint and the code last; No intro, state sentence or button sentence contains "simply", "just" or "easy" |
| `SkillsHelpDialogTests` | **Learn more** on each tab and on the empty state opens `SkillsHelpDialog` with that tab's text and a Close button, and navigates nowhere |
| `SkillsHelpTextTests` | Every paragraph of `SkillGuide.HelpFor(view)` occurs word for word in the matching subsection of `docs/Huddle.UserGuide.md`; no help text exceeds 250 words |

### 6.10 Agent card, role form and Build a team

#### 6.10.1 The shape of the change

Today a Skill or Pack named on a card is installed later, by `ImportingSkillInstaller` at **Approve &
hire** (which downloads from GitHub). After this change Huddle downloads nothing, so every place that
names a Skill answers three questions with the registry: is it usable (has a folder here), can it be
copied now, and is anything wrong with it. The rule of [§4](#4-design-principles) principle 6 applies
everywhere: **an Agent's file names a Skill only after its copy is in `Skills/`.**

One shared vocabulary:

```csharp
/// <summary>What the registry knows about a Skill name.</summary>
internal enum SkillAvailability
{
    /// <summary>Built in, or already in Skills/. Needs nothing.</summary>
    Present,

    /// <summary>In the registry, usable, with a folder here; a copy into Skills/ is all that is missing.</summary>
    Copyable,

    /// <summary>Listed in a catalog or Pack, but no folder here.</summary>
    NotDownloaded,

    /// <summary>A check failed for it.</summary>
    Broken,

    /// <summary>Its Source withdrew it.</summary>
    Withdrawn,

    /// <summary>Named by no catalog or Pack, and not in Skills/.</summary>
    Unknown,
}

/// <summary>One thing a card or draft needs before it can be hired.</summary>
internal sealed record SkillSetupItem(SkillSetupKind Kind, string Name, SkillAvailability Problem, string Detail);
```

`SkillSetupKind` is `Pack` or `Skill`. `SkillRegistry.Of(name)` ([§6.5](#65-skillregistry-and-registrywatcher)) answers in this order: built in or a
`Skills/` folder (SkillStore) → `Present`; otherwise the registry's `SkillEntry` by name
([§8.3](#83-resolving-packs-catalogs-and-skills): one per name) → its state mapped as `Available`,
`InUse`, `UpdateAvailable`, `Deprecated` to `Copyable`, `NotDownloaded`, `Broken`, `Withdrawn`;
otherwise `Unknown`.

`ISkillAvailability` is a small interface (`SkillAvailability Of(string skillName)`, `PackEntry?
FindPack(string packId)`, `string? SubCategoryOf(string skillName)`) that `SkillRegistry` implements
(the same three members, [§6.5](#65-skillregistry-and-registrywatcher)), so `CardResolver` stays a pure
static class tested without the registry (a sealed class cannot be mocked).

#### 6.10.2 `CardResolver` and the hiring pool

`SkillRef(Name, Source, Path)` stays, because `hiringpool.json` and `TeamTypeManifest` still carry
it. **A `SkillRef`'s `Source` and `Path` are ignored from now on.** The 104 refs in `hiringpool.json`
name GitHub repositories; roles resolve Skills **by name** through the registry. The names that
matter are those of the move's catalogs ([§6.13](#613-the-move)), where the 263 Skills it writes are
listed, so every pooled Skill is found as **Not downloaded** until fetched. The data is not edited;
only the reading changes.

| Part | Today | After |
| --- | --- | --- |
| Signature | `Resolve(role, namesInUse, answered, tierMap, avoidAdapters, IReadOnlyList<SkillPack> packs, connectors, chosenName)` | The `packs` parameter becomes `ISkillAvailability skills` |
| Role `Packs` ids | An unknown id is skipped; a known one adds its `SkillRef`s | An id **no row provides** is still skipped silently (R008 already warns on the Packs tab; a stock install with no skills repository must still hire a developer). A known Pack adds its Skills by name and its version to `Packs`. A known Pack whose state is `NotDownloaded` makes a `SkillSetupItem(Pack, id, NotDownloaded, …)`; `Broken` one `Broken`; `Withdrawn` one `Withdrawn` |
| Role `Skills` | Taken as listed | Each name goes through `skills.Of(name)`: `Present` and `Copyable` are fine; `NotDownloaded`, `Broken`, `Withdrawn`, `Unknown` each make a `SkillSetupItem(Skill, …)` |
| Pack Skills | `SkillRef` copied from the pack file | `new SkillRef(name, "registry", null)`; each name also goes through `skills.Of` and a missing one joins the Pack's item rather than being a second item |
| `CardState` | `…, Readiness, Reasons` | Adds `IReadOnlyList<SkillSetupItem> SkillSetup` |

**How it joins `CardReadiness.NeedsSetup`.** `Readiness` is `NeedsSetup` when `reasons.Count > 0` **or**
`SkillSetup.Count > 0`. Every `SkillSetupItem` also adds one line to `Reasons` through
`SkillSetupText.Reason(item)`, so `ProfileCard`'s existing *"Needs setup: …"* line, `HireService`'s
`unready` check and the existing tests that read `Reasons` keep working unchanged. The sentences:

| `Problem` | Reason |
| --- | --- |
| Pack, `NotDownloaded` | *Pack "MSBuild Authoring" not downloaded* |
| Skill, `NotDownloaded` | *Skill "code-review-and-quality" not downloaded* |
| `Broken` | *Pack "x" is broken: {first error message}* |
| `Withdrawn` | *Pack "x" was withdrawn by its Source: {reason}* |
| `Unknown` | *Skill "x" is not in any catalog* |

`SkillSetupItem`s are also what the UI turns into **Copy prompt** (all but `Withdrawn`, which has no
prompt: the Human drops it from the draft). A static `SkillSetupCheck.For(IReadOnlyList<string>
skillNames, IReadOnlyList<VersionedRef> packs, ISkillAvailability skills)` is the single function
that builds the list, used by `CardResolver` for a role's defaults and by `BuildTeam` again after a
draft is edited (the resolver is not re-run then, because it would redraw the name and model).

**Callers.** `RoleHirer` and `TeamDuplicator` swap `SkillPackStore packs` for `ISkillAvailability
skills` and pass it. A duplicate is a new hire, so a missing or withdrawn Skill stops it with the same
reason lines. `HireService`'s constructor drops `SkillPackStore packs` and `ISkillInstaller installer`
and takes `SkillRegistry registry` (for `MergeConsultWhen`, which reads the Pack's `ConsultWhenAdds`
through `registry.FindPack(id)` instead of `packs.Get(id)`) and `ISkillCopier copier`.

#### 6.10.3 `ISkillInstaller` goes; what replaces `EnsureInstalledAsync`

`ISkillInstaller` and `ImportingSkillInstaller` are deleted, with their registration in
`ServiceCollectionExtensions` and `ImportingSkillInstallerTests`. The seam stays testable through `ISkillCopier`, which [§6.6](#66-skillcopier-and-sourcejson) defines (`CopyAsync` and
`EnsureAvailableAsync`) and `SkillCopier` implements. This section uses
`EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct)`, which returns the
`SkillSetupItem`s (§6.10.1) for the names that are not ready, and an empty list when every Skill is present.

`EnsureAvailableAsync` does not download, review-gate or consult the catalog: **a check that every
named Skill is present in `Skills/` or copyable, copying the copyable ones** through the safe copy of
[§8.6](#86-reading-and-copying-while-the-ai-tool-writes). A built-in name returns nothing. It never
overwrites a `Skills/` folder that exists (an `UpdateAvailable` Skill is not auto-updated by a hire).
The old behaviour of refusing a Skill whose review shows more than a missing licence is gone: the S
checks decide, and `S010` is a warning.

| Caller | Before | After |
| --- | --- | --- |
| `HireService.StartTeamAsync` (line 142) | `await installer.EnsureInstalledAsync(skills, ct)`; *"Skills not installed: x."* | Removed (#562): it had no production caller. **Approve & hire** copies the Skills through `EnsureSkillsAsync` (next row) before it writes anything |
| `HireService.InstallSkillsAsync(IReadOnlyList<SkillRef>, ct)` (line 373) | Delegates to the installer | Renamed `EnsureSkillsAsync(IReadOnlyList<SkillRef>, ct)`; maps names, delegates to `copier.EnsureAvailableAsync`. `BuildTeam` is its caller |
| `PrepareHiresAsync` | Imports, sets `skillsBlocked` | Below |

#### 6.10.4 Agent card

All changes are in `AgentProfileCard.razor`.

**Pack chips (lines 391–405).** `this.rolePacks` becomes the list `SkillRegistry.PacksForRole(roleRef.RoleId)`
returns: Packs whose `appliesTo` contains the role id, not hidden unless the Agent holds them,
excluding `Broken` (those show on the Packs tab). Chip rendering moves to a new `PackChip.razor` in
`Components/Shared/` (parameters `PackId`, `Active`, `OnToggle`, plain values, because the card is
already 2100 lines):

| Pack state | Chip |
| --- | --- |
| `Available`, `InUse` | As today: a Filter `Chip` (`filter-chip`), `is-set` when active, `aria-pressed` |
| `UpdateAvailable` | As today, with a leading `Filled.SystemUpdateAlt` and a link-styled **Update available** beside it that opens `UpdateDialog` |
| `NotDownloaded` | Not offered as a chip. A line under the row instead: *Pack "MSBuild Authoring" is not downloaded.* with **Copy prompt** (`skillPrompt.fetchSkill`). The line is text, so a touch user can read it |
| `Deprecated` | As today, with a **Deprecated** icon and its reason in the tooltip |
| `Withdrawn` | Not offered to add. If the Agent holds it: a red chip and a `MudAlert Severity.Warning Dense` (`role="status"`, class `agent-profile-card-warning`): *"Pack "x" was withdrawn by its Source: {reason}. {Name} keeps working."* with **Remove from this Agent** |

Under the chips, a `MudLink` **Find more Packs for {Role title}** opens `AddWithAiDialog` on `find` with
the role filled in. It shows whenever the card has a role.

**Toggling is still an in-memory edit; copying happens on Save.** `TogglePack` changes only `this.skills`,
`this.text` and the `_packs` and `consult_when` lines, as it does now. Its body is extracted into
`PackMembership.Add(text, pack, …)` and `PackMembership.Remove(text, pack, roleSkills, otherPacks)`
(static, pure, in `Agency.Huddle.App.Teams`), which [§6.12](#612-removing-and-hiding) and `G7` reuse, so
one rule decides which Skills leave with a Pack. The version written to `_packs` is the Pack file's
`version`. Copying is added to `SaveAsync` (line 1819), before the `PersonaStore` write, so a
cancelled card leaves nothing behind (the existing comment at line 852 says the same):

1. Collect `this.skills`.
2. `ISkillCopier.EnsureAvailableAsync(this.skills, ct)`. While it runs the Save button shows the
   in-button spinner and the card is not dismissable.
3. On problems, show them in the card's existing error area and **do not write**: *"Skills not ready:
   x (not downloaded). Copy a prompt below, then save again."*
4. On success, the existing `PersonaStore.Update` runs, and the Agent restarts as it does today.

`SaveDraftAsync` (line 1892, the draft path) copies nothing; the draft returns its names and `BuildTeam`
checks them at **Approve & hire**.

**The Skills select (lines 340–375).** It lists:

1. For a draft only: nothing "Not installed yet" any more.
2. `OrderedSkills` (the `SkillStore` Skills) as today.
3. Registry Skills that are `Copyable` and have no folder in `Skills/` yet, each with the caption
   *"Huddle copies it into Skills/ when you save."* (a draft: *"…when you approve."*). They are
   offered from the registry, not the catalog list.

Not-downloaded Skills are not options in the select. Below it, a collapsed `MudCollapse` titled *"Not
on this computer yet (N)"* lists them with name, description, origin line and **Copy prompt** each, so
a Skill that cannot be chosen is still visible with its remedy. Hidden Skills are omitted unless
already selected. The helper text under the select is unchanged: *Changing Skills restarts this Agent.*

`DraftSkillChoices` (parameter, line 661) becomes `DraftSkillChoices` of `(Name, Description, FromRole,
Origin, Availability)` where `Availability` is `Copyable` or `NotDownloaded`; a role's not-downloaded
Skills show checked and disabled in the draft's Not-on-this-computer list with their state.

**Warnings (line 1688–1715).** `subCategoryOf` is `this.Services.GetService<SkillRegistry>()?.SubCategoryOf`
(registry catalogs, first row wins) instead of `SkillsCatalogIndex.SubCategoryOf`. See
[§6.10.7](#6107-memberwarnings).

#### 6.10.5 Role form

`RoleFormDialog` loses `@inject SkillsCatalogIndex SkillCatalog` and gains `@inject SkillRegistry Registry`.
The Skills `MudSelect` (lines 27–48) lists:

| Group | Items | Caption |
| --- | --- | --- |
| Installed | `InstalledSkills.All` as today | the provenance line |
| In the registry | `Registry.Skills` that are `Copyable` or `Available`, not in `Skills/`, not hidden | origin; *"Huddle copies it into Skills/ when an Agent in this role is hired."* |

**Not downloaded Skills are not options in the select.** They are listed below it in a collapsed *"Not
on this computer yet (N)"* section, each with **Copy prompt**; a role saved without one simply lacks
it. A Skill the role already names (editing an old role) that is not usable is kept in the select as a
checked item with the caption *"Not downloaded"* so editing does not silently drop it, and the dialog
helper becomes *"Skills not downloaded are listed below; ask your AI tool to fetch them."* The helper
text of the select reads *"Skills this Huddle has or can copy; an Agent in this role carries them."*.
`CustomRole` and `custom-roles.json` do not change.

#### 6.10.6 Build a team

Changes in `BuildTeam.razor`:

| Part | Change |
| --- | --- |
| `@inject SkillPackStore Packs`, `@inject SkillsCatalogIndex SkillCatalog` (lines 30–31) | Replaced by `@inject SkillRegistry Registry` |
| `DraftSkillChoices` (line 958) | Uses the registry: the role's Skills that are not in `Skills/` come first, tagged `Copyable` or `NotDownloaded`; then other `Copyable` registry Skills. The catalog is no longer walked |
| `SkillRefFor` (line 981) | Always `new SkillRef(name, "registry", null)`; the catalog branch goes |
| Draft creation (line 1004) | `CardResolver.Resolve(spec, [], …, this.Registry, …)`; `DraftMember.Reasons` carries the reasons, and a new field `SkillSetup` carries the `SkillSetupItem`s |
| After **Save draft** | `DraftMember.SkillSetup` is recomputed with `SkillSetupCheck.For(draft skills, draft packs, this.Registry)` and the draft's `Reasons` replace the Skill lines with the new ones; non-Skill reasons (name, adapter) are kept, and so is the separate list of unconnected connectors (never a reason, #545) |
| `ProfileCard` | Shows the draft's reasons as today, *"Needs setup: Pack "MSBuild Authoring" not downloaded"*. Under it, **Copy prompt** (`skillPrompt.needsSetup`, listing every item, grouped by Pack) when any item is not `Withdrawn`, and for `Withdrawn` the words *"Edit the draft to drop it."* The card's own buttons (Re-roll, Edit, Remove) stay |
| `CanApprove` | Adds `&& !this.drafts.Any(draft => !draft.Existing && draft.SkillSetup.Count > 0)` |
| `ApproveHint` | A new first case: *"Some Agents need setup. Copy their prompt, paste it into your AI tool, and they update here by themselves."* |

**Approve & hire waits.** The button is disabled while any new draft has a `SkillSetupItem`. The
page subscribes to `SkillRegistry.Changed` and recomputes every new draft's `SkillSetup`, so when the
AI tool finishes and the Skill turns usable the card flips to Ready and the button enables with no
click, and `SkillArrivalWatcher`'s snackbar says why. A draft in edit mode (`draft.Existing`) is
never checked.

**`PrepareHiresAsync` (line 1175).**

```csharp
IReadOnlyList<SkillRef> needed = [.. this.drafts.Where(static draft => !draft.Existing).SelectMany(static draft => draft.Skills).DistinctBy(static skill => skill.Name, StringComparer.Ordinal)];
IReadOnlyList<SkillSetupItem> notReady = await this.Hiring.EnsureSkillsAsync(needed, this.pageCancellation.Token);
if (notReady.Count > 0)
{
    this.skillsBlocked = true;
    return $"Skills not ready: {string.Join(", ", notReady.Select(static item => item.Name))}. Copy a prompt for your AI tool from the cards that need setup, then try again.";
}
```

`this.pageCancellation` is the page's own `CancellationTokenSource`, cancelled and disposed in
`Dispose`, so leaving the page stops a copy. `CheckDrafts` still runs first. `skillsBlocked` still draws a link, now *"Open Settings › Skills"* to
`/settings/skills?view=skills`. The `B10` notes in wireframes.md change from "first imports the Skills
the roster needs" to "first copies the Skills the roster needs into `Skills/`; one that cannot be
copied stops the approval with 'Skills not ready: …'". Because copying happens before `CreateTeam`,
a refusal still leaves nothing behind.

`EnsureSkillsAsync` copies into `Skills/` before the Team exists. That is the same ordering as today
(installs before anything is written) and is safe: a copy in `Skills/` that no Agent names is not
a change to any Agent.

#### 6.10.7 `MemberWarnings`

`MemberWarnings.For` keeps its signature; its `Func<string, string?> subCategoryOf` argument is now
`SkillRegistry.SubCategoryOf` at both call sites (`AgentProfileCard`, and wherever Build a team's
Complete Setup panel computes warnings). `SkillRegistry.SubCategoryOf(name)` returns the `subCategory`
of the first catalog entry with that name in SKILLS.md row order (empty string becomes `null`), so the
overlap warning *"a and b are both in API & Interface Design."* behaves as before. A new sibling,
`MemberWarnings.ForSources(IReadOnlyList<string> skills, IReadOnlyList<VersionedRef> packs,
ISkillAvailability availability)`, returns sentences for what the Agent holds: *"Pack "x" was
withdrawn by its Source: {reason}."*, *"Skill "y" was withdrawn…"*, *"Pack "x" is deprecated: {reason}."*,
and for a `_packs` entry whose Pack no row provides any more, *"Pack "x" is no longer in SKILLS.md.
{Name} keeps its Skills; remove the Pack here, or ask your AI tool to add it back."*
Both lists are drawn as `MudAlert Severity.Warning` in `SetupWarnings`, in the order shared vendor,
too many Skills, sub-category, withdrawn, deprecated.

#### 6.10.8 Withdrawn blocks new hires

| Place | Behaviour |
| --- | --- |
| `CardResolver` | A Withdrawn Pack or Skill is a `SkillSetupItem`, so the card is **Needs setup** and cannot be hired |
| `BuildTeam` draft | **Approve & hire** waits; the Human edits the draft to drop it |
| `HireService.EnsureSkillsAsync` | Returns `Withdrawn` as a problem, so a roster that slipped past (a Source withdrew it between draft and approve) stops with *"Skills not ready: x."* |
| `AgentProfileCard` pack chip | A Withdrawn Pack is not offered; the Skills picker omits a Withdrawn Skill unless already selected |
| Existing Agents | Never touched. The red chip and `MemberWarnings.ForSources` warning say so |

#### 6.10.9 Tests

| Test class | Pins |
| --- | --- |
| `CardResolverTests` (updated) | A known Pack with `NotDownloaded` makes the card `NeedsSetup` with *Pack "MSBuild Authoring" not downloaded* in `Reasons` and a `SkillSetupItem` |
| `CardResolverTests` | A role Skill with `Unknown`, `Broken`, `Withdrawn`, `NotDownloaded` each gives the matching item and reason; `Present` and `Copyable` give none; A Pack id no row provides is skipped without setup; a stock install still resolves the built-in developer's card with its Pack skipped, and its pooled Skills (not built in, so not on a fresh computer) make it Needs setup until fetched, while with those Skills present it is Ready; `SkillRef.Source` and `Path` are ignored: two refs with different Source and Path and the same name resolve the same |
| `SkillSetupCheckTests` | One Pack with two missing Skills gives one Pack item, not three |
| `BuildTeamTests` (moved from `HireServiceStartTests`, #562) | **Approve & hire** stops with *Skills not ready* when `ISkillCopier` reports a problem and creates no Team and writes no Agent (`Approve_SkillNotReady_BlocksBeforeAnythingIsWritten`) |
| `BuildTeamTests` (moved from `HireServiceStartTests`, #562) | The Skills are copied once, by distinct name, before the Team or any Agent is written (`Approve_TwoDraftsSharingASkill_CopiesItOnceBeforeAnythingIsWritten`) |
| `SkillCopierEnsureTests` | A built-in name is skipped; a `Present` name is not recopied; a `Copyable` name is copied once; `NotDownloaded`, `Broken`, `Withdrawn` and `Unknown` come back as problems and write nothing |
| `AgentProfileCardPackTests` | `Available` and `InUse` Packs render toggle chips; `NotDownloaded` renders the not-downloaded line with **Copy prompt** and no chip; `UpdateAvailable` shows the icon and opens `UpdateDialog`; `Withdrawn` held shows the red chip, the warning and **Remove from this Agent**; `Withdrawn` not held is not offered; **Find more Packs for {Role}** opens `AddWithAiDialog` on `find` with the role |
| `AgentProfileCardSaveTests` | **Save** copies a `Copyable` Skill before `PersonaStore.Update`; a copy failure shows the message and writes no file; **Cancel** copies nothing; `SaveDraftAsync` copies nothing |
| `AgentProfileCardSkillsTests` | The select offers `Copyable` registry Skills with the caption; `NotDownloaded` ones appear only in the collapsed list with **Copy prompt** |
| `PackMembershipTests` | `Remove` keeps a Skill the role or another Pack provides; `Add` then `Remove` returns the original text |
| `RoleFormDialogTests` | The select lists installed and `Copyable` Skills, not catalog-wide; a not-downloaded Skill the role names stays checked with *Not downloaded* |
| `BuildTeamSetupTests` | A draft with a not-downloaded Pack shows *Needs setup*, **Copy prompt**, and disables **Approve & hire**; the hint reads the new sentence; A `SkillRegistry.Changed` that makes the Skill usable flips the draft to Ready and enables the button; Editing a draft to drop a Withdrawn Skill clears the item; an existing Agent in edit mode is never checked; `PrepareHiresAsync` with a problem sets `skillsBlocked`, shows *Skills not ready: …*, and creates no Team |
| `MemberWarningsTests` | `subCategoryOf` from the registry gives the same overlap warning as before; `ForSources` lists Withdrawn and Deprecated |
| `ServiceCollectionExtensionsTests` (updated) | `ISkillInstaller` is no longer registered; `ISkillCopier` resolves to `SkillCopier` |

### 6.11 The Skill Sources Library root

#### 6.11.1 What changes

The Skill Sources folder becomes a **fourth built-in Library root**, after Teams, Agents and Uploads, that
the Human can read and search but not change. Every write goes through `LibraryFileService`, so the
rule is enforced in the service and the resolver, never only in the UI. Huddle's own writes into the
folder (the flows of [§6.12](#612-removing-and-hiding), `log.jsonl`, `hidden.json`) do not pass through
`LibraryFileService`, so they are unaffected; the Library is the Human's door, not Huddle's.

| Item | Value |
| --- | --- |
| Id | `skill-sources` |
| Display name | *Skill Sources* |
| Full path | `SkillSourcesSettings.Dir` (`skill-sources.json`, else `Team:SkillSourcesDir`, else `{DataDir}/SkillSources`; [§8.7](#87-configuration-huddle-reads)) |
| Kind | `LibraryRootKind.SkillSources` (new; appended to the enum, so no stored value moves) |
| Read only | `LibraryRoot.ReadOnly = true` |

```csharp
public sealed record LibraryRoot(string Id, string DisplayName, string FullPath, LibraryRootKind Kind, bool ReadOnly = false);

public sealed record LibraryPath(LibraryRoot Root, string RelativePath, string FullPath, LibraryNodeRole Role, bool ReadOnly = false)
```

`LibraryPath.ReadOnly` is the answer every writer asks. It is set by the resolver, not copied from the
root: it is `true` when `Root.ReadOnly` **or** the resolved `FullPath` lies inside any read-only root.
That keeps the rule true when a pinned root, a junction or a link reaches into the Skill Sources
folder, in the way that "the role a resolved path receives follows the RESOLVED location, not the
root id the caller named" already holds for Teams and Agents.

#### 6.11.2 `LibraryRootStore`

| Change | Detail |
| --- | --- |
| Constructor | Gains `SkillSourcesSettings skillSources` |
| `BuildRoots` | Appends `new LibraryRoot(SkillSourcesRootId, "Skill Sources", dir, LibraryRootKind.SkillSources, ReadOnly: true)` after Uploads |
| Slug reservation | `takenSlugs` (line 218) includes `skill-sources`, so a pinned root named "Skill Sources" becomes `skill-sources-2` |
| Follows the setting | The store subscribes to `SkillSourcesSettings.Changed`; the handler calls a new `RefreshSkillSources()` that rebuilds the snapshot with the new directory (keeping pinned roots, hidden set and `LoadError`) and raises `RootsChanged` outside the lock. Everything that already listens (`LibraryPanel`, the Library explorer, the Composer picker, `WikiLinkIndex`) repaints |
| Hide | `SetHidden` accepts `skill-sources` (the guard at line 134 gains it). A hidden root stays in `Roots` and still resolves, like the others |
| Missing folder | The root stays listed; a listing fails with the existing "isn't available" message. The move creates the folder before any Agent starts, and `SkillSourcesSettings.SetDir` refuses a folder that does not exist |

`Settings › Library` (`LibraryPanel`) lists the root with the caption *Read only* and no remove or edit
control, and the **Hide** switch the other built-ins have. Its `G3` note in wireframes.md gains the
row.

#### 6.11.3 Enforcement points

**`LibraryPathResolver`.**

| Method | Change |
| --- | --- |
| `TryResolve` (line 46), at the `new LibraryPath(…)` on line 89 and for the root itself | Passes `ReadOnly: this.IsReadOnly(root, current)` |
| `TryResolveChild` (line 103), at line 139 | Same, using `parent.ReadOnly || this.IsReadOnly(parent.Root, current)` |
| `TryResolveAbsolute`, `TryResolveScope` | Go through `TryResolve`, so they inherit it |
| New `IsReadOnly(LibraryRoot root, string resolvedFullPath)` | `root.ReadOnly` or `resolvedFullPath` equals or lies under any `AllRoots` entry with `ReadOnly`, compared with `FolderSnapshot.PathComparer` on separator-terminated prefixes, after the link-following step has produced the final location |

**`LibraryProtection.For`** returns `new LibraryProtection(false, false, false, LibraryPath.ReadOnlyReason)`
first, before the role switch, when `path.ReadOnly`. `LibraryTree` already draws Rename, Move and
Delete disabled from it. `ReadOnlyReason` is *"The Skill Sources folder is read only. Ask your AI tool to
change it."* A new test pins that the tree's menu for a read-only item has no enabled write verb.

**`LibraryFileService`**, each at the point after its existing re-resolve (`fresh`), first thing:

| Method | Refusal |
| --- | --- |
| `WriteTextAsync` (line 638) | `if (fresh.ReadOnly)` returns `LibraryResult<LibraryEntry>(null, ReadOnlyReason)`; before the missing-file branch so `EnsureParentFolderCreated` never runs |
| `CreateFileAsync` (717), `CreateFolderAsync` (775) | The *folder* argument, re-resolved, is read only |
| `CreateFromStreamAsync` (877), `DeleteUnsentUploadAsync` (842) | Same on the folder or file |
| `RenameAsync` (989), `RecycleAsync` (1222) | `fresh.ReadOnly` (already covered by `LibraryProtection`, but checked explicitly so the service holds without the UI) |
| `MoveAsync` (1113) | `fresh.ReadOnly` for the item **and** for `targetFolder`; moving out of or into the folder is refused. Copying a file *out* (the clipboard's copy, which creates in the destination) is a read of the source and stays allowed |
| The link-rewrite pass of rename and move (about line 1371 and 1666) | Skips any note whose resolved path is read only. A rename elsewhere never edits SKILLS.md or a source note, even when they link to it |
| `ReadAsync` (495) | Sets `Editable` to `false` for a read-only path, so `LibraryEditor` opens in view mode with the reason, as it does for a non-Markdown file today |

Any other `AtomicFile` or `File.*` writer under `Library/` that takes a `LibraryPath` follows the same rule;
the review checklist for this root is `grep -n "AtomicFile\|File\.\(Write\|Move\|Delete\)" Library/*.cs`
and each hit is either Huddle's own write or guarded.

**Other classifiers.** `LibraryDocumentCollector` (line 322) gains the label `skill sources`;
`LibrarySearchProvider` and `WikiLinkIndex` need no change (they walk every root; a search for a
Skill name finds SKILLS.md and the source notes, and `hideUnderscoreFolders` stays Teams-only).

#### 6.11.4 What the root shows first

`LibraryExplorer` opens a root with no remembered selection on its first document. For
`LibraryRootKind.SkillSources`, that is `SKILLS.md` when it exists, through
`LibraryRoots.DefaultDocument(LibraryRoot)` (returns `"SKILLS.md"` for this kind, otherwise `null`,
which keeps today's behaviour). The tree beside it lists, in this order: `SKILLS.md`, `packs/`,
`catalogs/`, `sources/`, `fetched/`, `hidden.json`. The outline and search work as for any Markdown.
A remembered selection wins, as for the other roots.

A clone outside the folder is not in this root
([ADR-0027](adr/0027-the-library-sees-only-configured-roots.md)); the Human pins it on Settings › Library.

#### 6.11.5 Tests

| Test class | Pins |
| --- | --- |
| `LibraryRootStoreSkillSourcesTests` | `Roots` ends with `skill-sources`, kind `SkillSources`, `ReadOnly` true, path from `SkillSourcesSettings.Dir`; Changing the setting raises `RootsChanged` once and `Roots` shows the new path; pinned roots and hidden set survive; A pinned root named "Skill Sources" gets a different slug; `SetHidden("skill-sources", true)` hides it from `VisibleRoots` only |
| `LibraryPathResolverReadOnlyTests` | A path in the root, and one in a pinned root that overlaps it, resolve with `ReadOnly` true; a path elsewhere is false; A junction in a writable root that points into the folder resolves read only |
| `LibraryFileServiceReadOnlyTests` | Each of `WriteTextAsync`, `CreateFileAsync`, `CreateFolderAsync`, `CreateFromStreamAsync`, `RenameAsync`, `MoveAsync` (item and target) and `RecycleAsync` returns the refusal and touches no file (hash before and after equal); A rename elsewhere that SKILLS.md links to does not rewrite SKILLS.md; `ReadAsync` returns not editable; reading and searching still work |
| `LibraryProtectionTests` | A read-only path gives `CanRename`, `CanMove`, `CanDelete` all false with `ReadOnlyReason` |
| `LibraryExplorerSkillSourcesTests` | Opening the root with no selection shows `SKILLS.md`; a remembered selection wins; another root is unchanged |
| `LibraryPanelTests` (updated) | The root shows *Read only*, a Hide switch and no remove control |

### 6.12 Removing and hiding

#### 6.12.1 Ground rules

1. **Huddle deletes only inside the Skill Sources folder, and only what it names below.** It never
   deletes a clone, a pack or catalog file that lives outside the folder, or a `Skills/<name>/` copy.
   (`Skills/.staging` and `.trash` are emptied by the copier, [§8.6](#86-reading-and-copying-while-the-ai-tool-writes).)
2. **A path is inside the folder** when its resolved full path, after `Path.GetFullPath`, starts with the folder
   path plus a separator, with `FolderSnapshot.PathComparer`. A reparse point is never followed: a
   delete of a path that is or contains one removes the link, never its target.
3. **Huddle edits SKILLS.md only by deleting a row's lines**, exactly as
   [§8.8](#88-writing-skillsmd) describes (the rest of the file is written back byte for byte).
4. **Every flow is an outcome in a snackbar**, keyed, `Severity.Success` or `Error`, and writes a
   `log.jsonl` line where a Source is known.
5. **Order is safest first**: Agents, then the row, then the file, then notes, so a failure leaves
   something re-runnable. The row is removed before its file, so a crash never leaves a row pointing
   at nothing that Huddle itself made.

`log.jsonl` lines are written by `SourceLog.Append(string sourceId, LogLine line)` to
`sources/<source-id-path>/log.jsonl`, with `LogLine(string Event, string Subject, string? Action = null,
string? Detail = null, string? Code = null, IReadOnlyDictionary<string, object?>? Data = null)`; `SourceLog`
fills `at`, `computer` and `huddle`. The line schema, the events and the `data` keys of each are defined
once, in [§7.2](#sourcessourcelogjsonl); the flows below name the event and its `data` and nothing else.
When the Pack or catalog names no known Source, or its `sources/` folder does not exist, no line is
written and no folder is created.

#### 6.12.2 Remove a Pack

Entry: card **⋯** › **Remove**, or **Remove** in `G6`. The Pack's holders are `SkillRegistry.HolderNames(pack)` (an
Agent whose `_packs` lists its id).

**No holders.** `DialogService.ConfirmAsync(title, message, "Remove", danger: true)`:

| | Text |
| --- | --- |
| Title | *Remove "Document skills"?* |
| Message, file inside the folder | *This removes its row from SKILLS.md and deletes packs\document-skills.skillpack.json from the Skill Sources folder. No Agent has this Pack. Your AI tool's clone is not touched.* |
| Message, file outside | *This removes its row from SKILLS.md. The file E:\Repos\skills\packs\x.skillpack.json is outside the Skill Sources folder, so Huddle leaves it where it is. No Agent has this Pack. Your AI tool's clone is not touched.* |

**With holders.** `RemovePackDialog` (`G6a`), title *Remove "Document skills"?*:

> *Jane and Omar have this Pack. Choose what happens to them.*
>
> **Keep their Skills** — *Their Skills and copies stay exactly as they are. Only the Pack's mark on their cards goes, so it no longer offers an update.*
>
> **Remove from Agents too** — *Also takes off the Skills nobody else gave them, and the Pack's "consult when" phrase. They restart.*
>
> *Either way: the row is removed from SKILLS.md and, if the file is in the Skill Sources folder, the file is deleted. Your AI tool's clone is not touched.*

Buttons: **Cancel**, **Keep their Skills** (outlined), **Remove from Agents too** (filled,
`Color.Error`), in that order. Escape cancels.

**Files written or deleted**, in order:

| # | Step | File |
| --- | --- | --- |
| 1 | For each holder: **Keep** drops the `_packs` entry only (`PackMembership.DropRecord`), leaving `skills:` and `consult_when:`. **Remove too** calls `PackMembership.Remove` (the same code as the card's chip, so it keeps a Skill the Agent's role or another Pack provides, and removes the `consultWhenAdds` clause) and restarts the Agent. Written through `PersonaStore` | `Agents/<Name>/<Name>.md` |
| 2 | Delete the row's lines | `SkillSources/SKILLS.md` |
| 3 | If the pack file is inside the folder, `File.Delete` | `SkillSources/packs/<id>.skillpack.json` (or wherever it is) |
| 4 | Remove the id from `packs` in `hidden.json` if present. `hidden.json` is rewritten through a temporary file and a move; no other entry is touched | `SkillSources/hidden.json` |
| 5 | Append the log line | `sources/<id>/log.jsonl` |

A failure in step 1 stops the flow before step 2 (Agents already written stay written; running it
again is safe because the operations are idempotent). A failure in 2 stops before 3. A failure in 3 after
2 leaves an orphan file that a later `R005`-free listing ignores, and the snackbar says *"Removed the
row, but could not delete {file}. Delete it yourself."* The Skills the Pack named are untouched in
`Skills/` in both options, and appear on the Skills tab as the plain copies they are (no longer in a Pack).

Log line: event `removed`, `subject` `pack:document-skills`, and `data` `{"row":"packs/document-skills.skillpack.json","fileDeleted":true,"agents":"kept","holders":["Jane","Omar"]}` where `agents` is `kept`, `removed` or `none`.

Snackbar: *"Document skills removed."* (key `skill-removed`).

**Remove from Agents (Withdrawn).** Card **⋯** › **Remove from Agents**, shown only for a
`Withdrawn` Pack with holders. `ConfirmAsync("Remove from 2 Agents?", "Jane and Omar will lose the Skills no one else gave
them, and the Pack's "consult when" phrase. They restart. The Pack's row and file stay.", "Remove from Agents", danger: true)`.
It runs step 1 of **Remove too** only, and logs event `unheld` (`subject` `pack:<id>`, `data.holders`).

#### 6.12.3 Remove a catalog

Entry: Sources tab › catalog row › **⋯** › **Remove**. Packs that name this catalog's Skills with
`catalog` are computed first (`ResolvedRegistry.PacksUsingCatalog(id)`).

`ConfirmAsync`, danger, verb **Remove**:

| | Text |
| --- | --- |
| Title | *Remove catalog "dotnet-skills"?* |
| Message | *This removes its row from SKILLS.md and deletes catalogs\dotnet-skills.skillcatalog.json from the Skill Sources folder. Its 106 Skills stop being listed. Agents keep their copies in Skills/.* then, only when Packs use it: *These Packs name Skills in it and will show Broken until your AI tool fixes them: MSBuild Authoring.* then, when the file is outside: the "leaves it where it is" sentence of [§6.12.2](#6122-remove-a-pack) |

Files: SKILLS.md row (step 2), the catalog file when inside the folder (step 3), the log line (step 5)
`removed` (`subject` `catalog:<id>`) with `data` `row`, `fileDeleted`, `skills` (the count), `packsBroken` (their ids).
`hidden.json` is not touched: a hidden Skill name that no catalog lists is harmless, and Huddle
does not prune names it cannot be sure are gone (a catalog can be mid-write elsewhere).

#### 6.12.4 Hide a Skill or a Pack

Huddle never edits a catalog or Pack file, so removing one Skill from a catalog is **Hide**.

| Action | Entry | Writes |
| --- | --- | --- |
| Hide a Skill | Skills tab row **⋯** › **Hide** | `hidden.json`: adds the name to `skills` |
| Unhide | **Show hidden** on, then **⋯** › **Unhide** | removes it |
| Hide a Pack | Card **⋯** › **Hide** | adds the id to `packs` |
| Unhide a Pack | **Show hidden**, **⋯** › **Unhide** | removes it |

`hidden.json` shape (shared with other Huddles on the folder):

```json
{ "schemaVersion": 1, "packs": ["document-skills"], "skills": ["qa-kit"] }
```

There is no confirmation: it deletes nothing and is reversible. The snackbar is *"qa-kit hidden"* with
the action **Undo** (key `skill-hidden`), which removes the entry. The write is a temporary file and a
move; a malformed `hidden.json` is treated as empty for reading and **not** overwritten: Hide shows
*"hidden.json could not be read, so nothing was changed. Fix or delete it."* (an `Error` snackbar).

What hiding does: the item leaves the Skills tab, the Packs tab, the Agent card's Pack chips and
Skills select, the role form and Build a team's pickers, and `huddle-packs skills`. What it does not do:
touch an Agent that holds it (it stays selected and listed on that card), delete anything, or
change the item's state in the registry. Hiding is shared across the Huddles that share the folder.

Log line in the item's Source log: event `hidden` or `unhidden`, `subject` `skill:qa-kit` or `pack:<id>`.

#### 6.12.5 Remove a Source

Entry: Sources tab › Source row › **⋯** › **Remove**. Counts come from `ResolvedSource`: its rows (every
Packs and Catalogs row whose pack or catalog file names this Source in `source`), and the files it keeps.

`ConfirmAsync`, danger, verb **Remove Source**:

| | Text |
| --- | --- |
| Title | *Remove Source?* |
| Message | *This permanently deletes the notes kept for github.com/anthropics/skills: NOTES.md, source.json and log.jsonl.* then *It also removes 2 rows from SKILLS.md (Document skills, anthropics-skills) and deletes their files from the Skill Sources folder.* then *Agents keep their copies in Skills/. Your AI tool's clone at E:\Clones\anthropics-skills is not touched.* then *If you share this folder with another Huddle, it loses the notes too.* |

The `localPath` sentence is left out when `source.json` is unreadable. The rows sentence is left out
when there are none.

**Files written or deleted**, in order:

| # | Step | File or folder |
| --- | --- | --- |
| 1 | For each row that names the Source, in SKILLS.md order: delete its lines ([§8.8](#88-writing-skillsmd)), re-reading the file each time so line numbers stay true | `SkillSources/SKILLS.md` |
| 2 | Delete each such row's pack or catalog file if it is inside the folder | `packs/…`, `catalogs/…` |
| 3 | Delete the Source's folder recursively (a reparse point inside is unlinked, not followed); then delete its now-empty parents up to but not including `sources/` | `sources/github.com/anthropics/skills/` |
| 4 | Write one line to the application log (`ILogger`, Information): `Source {SourceId} removed with {Rows} rows` | not `log.jsonl`: it is deleted in step 3 |

Step 3 comes last so a failure earlier keeps the notes and can be retried. A path that resolves outside
`SkillSources/sources/` (a malformed id such as `..\x`) is refused with *"That Source is not in the
Skill Sources folder, so Huddle did not touch it."*
`Skills/<name>/` copies stay; their provenance line (`SkillProvenanceLine`) shows a
**Source removed** chip when `.source.json`'s source no longer exists in the registry. The `hidden.json`
entries stay. A Source that holds the two Packs of the move (the skills repository) is removed the same way.

Snackbar: *"Source removed. 2 rows and their files are gone; the clone and Agents' copies are untouched."*

#### 6.12.6 What is never deleted

| Thing | Why |
| --- | --- |
| A clone, wherever it is | Decision 3: where a Skill is cloned does not matter; Huddle did not make it |
| A pack or catalog file outside the Skill Sources folder | Same; the row goes, the file stays |
| `Skills/<name>/` copies | Agents use them; removal of a Pack or Source never reaches an Agent unless the Human chooses **Remove from Agents too** |
| `InstallSkillPack.md` and `install-context.json` | Huddle rewrites them on change; they list the new state |
| `fetched/<name>/` (the move's copies) | They are Skill folders other rows may point at; they are removed only if a Source removal's own rows name them, never by id |

#### 6.12.7 Tests

| Test class | Pins |
| --- | --- |
| `RemovePackTests` | With no holders the dialog is a confirm with the two message variants; confirming deletes the row's lines and the file inside the folder and leaves a file outside; **Keep their Skills** drops only `_packs` and leaves `skills:` and `consult_when:` byte for byte; **Remove too** removes the Skills no other Pack or role gave and the phrase, and restarts the Agent; An Agent write failure stops before SKILLS.md is touched; a file-delete failure after the row is gone says so and leaves SKILLS.md edited; The `hidden.json` id is removed; other entries and a malformed file are untouched; The log line is event `removed`, `subject` `pack:<id>`, with `data` `row`, `fileDeleted`, `agents` and `holders`; no line and no folder is created when no Source dir exists; **Remove from Agents** (Withdrawn) leaves the row and file in place |
| `RemoveCatalogTests` | The message names Packs that use the catalog; SKILLS.md and the file inside the folder go; `hidden.json` is unchanged |
| `RemoveSourceTests` | Rows naming the Source and their in-folder files go, then the Source folder; a clone is untouched; a file outside the folder stays; A reparse point inside `sources/<id>/` is unlinked, its target kept; A Source id that resolves outside `sources/` is refused and nothing is deleted; A failure in step 1 keeps the notes folder |
| `SkillsMdWriterTests` | Removing a row writes every other byte unchanged (CRLF kept) and retries when the file changed in between |
| `HiddenStoreTests` | Hide and Unhide round-trip; a malformed file is not overwritten; the write is through a temporary file |
| `HideTests` | A hidden Skill leaves the Skills tab, the pickers and the role form, but stays selected on an Agent that holds it; **Undo** restores it |
| `SkillProvenanceLineTests` (updated) | A copy whose Source no longer exists shows **Source removed** |
| `SourceLogTests` | Appends one line per call, never rewrites; the line carries the fields of [§7.2](#sourcessourcelogjsonl) (`at`, `computer`, `huddle`, `event`, `subject`) and `computer` is `Team:ComputerName`; concurrent appends do not interleave |

### 6.13 The move

**Purpose.** Carry what Huddle ships and what the Human has already imported into the new
format, once, on the first start of this version, by writing exactly the files an AI tool
following `InstallSkillPack.md` would have written. After it runs there is one way in. It
changes no Agent, restarts nobody and opens no network connection.

`SkillSourcesMove` lives in `src/Huddle.App/SkillSources/Move/` (namespace
`Agency.Huddle.App.SkillSources.Move`). It is the only code that reads the old formats. The
mechanics it relies on are in [§8](#8-core-algorithms--processing-logic): the SKILLS.md table
shape ([§8.1](#81-reading-skillsmd)), per-computer paths ([§8.2](#82-per-computer-paths)),
catalog ids ([§8.3](#83-resolving-packs-catalogs-and-skills)), the hash rules
([§8.4](#84-noticing-an-update)), writing SKILLS.md ([§8.8](#88-writing-skillsmd)) and the
`Team:SkillsRepositoryDir` key ([§8.7](#87-configuration-huddle-reads)).

**Responsibilities.**

- Decide whether to run, and do nothing when it has run.
- Read four old inputs, read-only: the embedded `Skills/SkillsCatalog.json` (or the Human's
  `{DataDir}/SkillsCatalog.json`), the Human's `{DataDir}/SkillPacks/*.json`, the folders in
  `{DataDir}/Skills/` that have a `.source.json`, and the skills repository named by
  `Team:SkillsRepositoryDir`.
- Write the Skill Sources folder's first content: `SKILLS.md`, `catalogs/`, `packs/`,
  `sources/`, `fetched/`.
- Record what it did in each Source's `log.jsonl` and in a report the UI shows once.

**What it must never do.** Each line is pinned by a test (see the test table).

| Never | Because |
| --- | --- |
| Write, rename or delete any Agent file (`<Name>.md`, `_packs`, `skills:`, `consult_when:`) | Agents must not change behind the Human's back. The files in `fetched/` are the bytes Agents already use, so nothing differs |
| Restart an Agent or ask the supervisor for anything | It runs before any Agent starts, and a no-op to them afterwards |
| Open a network connection, or take an `HttpClient` | Decision 2: Huddle makes no network calls for Skills. The move has no constructor parameter that could |
| Write inside `Skills/` (no new folder, no edit to a `.source.json`) | `Skills/` is what Agents read. The move reads it and copies out of it |
| Delete or edit `{DataDir}/SkillPacks/`, `{DataDir}/SkillsCatalog.json` or anything in the skills repository | An older Huddle that opens the same data folder must still work. The move only reads them |
| Run when `SKILLS.md` exists | Idempotence and sharing (see below) |
| Stop Huddle from starting | A failed move is logged and retried on the next start; the app runs with an empty registry |

#### When it runs

`RegistryWatcher.StartAsync` calls `SkillSourcesMove.RunAsync` as its first step and awaits it,
then does the first registry read, then starts watching. `SkillRegistry`, `RegistryWatcher` and
`InstallFileWriter` are registered in that order, all before `BuiltinAgentSeeder` and
`DataInitializer` ([§6.14](#614-code-that-goes)), so the move has finished before any Agent
starts. Hosted services start in registration order.

`SkillRegistry` must not read the disk in its constructor, because the host constructs every
hosted service and its dependencies before the first `StartAsync`. Its `Current` starts as a
snapshot over `ResolvedRegistry.Empty`, and `RegistryWatcher` publishes the first real one
(`ReloadAsync`) after the move. Anything that reads the registry before then sees an empty one and
the later `Changed` event ([§6.5](#65-skillregistry-and-registrywatcher), "Start-up order").

#### Idempotence and sharing

- **The marker is `SkillSources/SKILLS.md`.** If it exists the move returns `null` at once and
  reads none of the four inputs, not even `Team:SkillsRepositoryDir`. That key is read once, by
  the move, and never again.
- SKILLS.md is the **last** file placed, so its existence means every other file was placed
  first. A run that stopped earlier left no marker and runs again from the start.
- **A shared Skill Sources folder** that already holds SKILLS.md, written by another Huddle,
  skips the move on this one. This Huddle's already-imported Skills then have no `fetched/` copy
  and their catalog entries stay **Not downloaded** here; the Agents keep their copies in
  `Skills/` and nothing shows **Update available**. Only the first Huddle to start moves its
  Skills. This is acceptable because a copy in `Skills/` is what an Agent uses, and an AI tool
  can add a path for this computer with **Copy fix prompt**.
- Deleting `SKILLS.md` by hand makes the move run again. **It never overwrites an existing pack
  file, catalog file, `source.json` or `NOTES.md`**: it skips each one and logs a `moved` line
  with `action` `kept-existing`. It writes only the files that are missing, and SKILLS.md. A
  `log.jsonl` is appended to, never replaced. Nothing the Human or an AI tool added since is
  deleted or changed, but a row for it is lost with SKILLS.md; that is the Human's doing.
  `fetched/<name>/` is the move's own folder and is always rewritten, so a copy interrupted by a
  crash is completed.

#### Crash safety: stage, then place, with SKILLS.md last

**Decision.** Build the whole result in `SkillSources/.move-<random>/` (8 hex characters), then
move its files into place, then SKILLS.md, then delete the staging folder.

Why not "write straight into place and rely on a rerun": a shared folder would show half the
catalogs to another Huddle, and a copy interrupted halfway would leave a `fetched/<name>/` that
looks complete. Why not "rename the staging folder to the target": `SkillSources/` already
exists when the Human has opened the Library root, set a custom folder or run Huddle once on an
older build, so a folder rename cannot be relied on. Staging sits inside the Skill Sources
folder so every move is on one volume and is a rename, even when that folder is a network
share.

| Step | What | If the process dies here |
| --- | --- | --- |
| Stage | Every file is written under `.move-<random>/`, mirroring the final layout | Nothing exists outside the staging folder. The next start deletes it and begins again |
| Place | Each staged file is moved to its final path, in this order: `fetched/`, `sources/`, `catalogs/`, `packs/`. `fetched/` files use `overwrite: true`; a pack, catalog, `source.json` or `NOTES.md` that already exists is skipped (`kept-existing`, see [Idempotence and sharing](#idempotence-and-sharing)); a `log.jsonl` is appended to | Some files are in place, no SKILLS.md. The next start rebuilds the staging and places what is missing; what is already there is complete (each file was placed by one rename) and equal to what would be written. No SKILLS.md means no registry exists, so nothing in place is referenced yet |
| Commit | `SKILLS.md` is moved with `overwrite: false` | If it throws `IOException` because it now exists, another Huddle won the race; discard the staging and report nothing |
| Clean | The staging folder is deleted | An orphan `.move-*` folder. The next start deletes every `.move-*` older than 10 minutes, whether or not SKILLS.md exists. A younger one may belong to a Huddle that is running now |

Placement goes through `IMoveFiles.Place(string from, string to, bool overwrite)`, so a test
fake can throw on the Nth call to simulate a crash without a test-only hook in the move itself.
The production implementation is a thin wrapper over `File.Move` that creates the target folder.

Files from a previous crashed run that the new run does not write (for example a `fetched/<name>`
for a Skill the Human has since deleted) stay where they are. They are not referenced by any
row, so they are harmless; `fetched/` is the move's own folder and the Human may delete it.

#### Types

```csharp
namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>Moves today's Packs, catalog and imported Skills into the Skill Sources folder, once.</summary>
internal sealed class SkillSourcesMove(
    IOptions<TeamOptions> options,
    SkillSourcesSettings sources,
    IMoveFiles files,
    TimeProvider clock,
    ILogger<SkillSourcesMove> logger)
{
    /// <summary>Runs the move unless SKILLS.md exists.</summary>
    /// <returns>The report, or <see langword="null"/> when the move did not run.</returns>
    public Task<MoveReport?> RunAsync(CancellationToken ct);
}

/// <summary>Places one staged file; the seam a crash test replaces.</summary>
internal interface IMoveFiles
{
    void Place(string from, string to, bool overwrite);
}

/// <summary>What the move did, shown once as a snackbar and kept in skill-sources-move.json.</summary>
internal sealed record MoveReport(
    DateTimeOffset At,
    bool Failed,
    int Catalogs,
    int CatalogSkills,
    int OmittedDuplicates,
    int ShippedPackRows,
    int UserPacksMoved,
    int SkillsPointedAt,
    int SkillsLeftInPlace,
    IReadOnlyList<string> SkillsWithIgnoredTools,
    IReadOnlyList<MoveProblem> Problems);

/// <summary>One thing the move could not carry over.</summary>
internal sealed record MoveProblem(string Subject, string Message);
```

`MoveReport` is saved to `{DataDir}/skill-sources-move.json` (not in the shared folder), through
a temporary file and a rename, together with `"shown": false`. `MainLayout` asks
`MoveReportStore.TakeUnshown()` on its first render, shows the snackbar, and the store writes
`"shown": true`. The snackbar is therefore shown once per Huddle even if the first start had no
open window. A move that carried over nothing of the Human's, as on a new data folder, is saved
with `"shown": true` straight away, so it shows no snackbar.

| Report | Snackbar (`Severity`, text) |
| --- | --- |
| Nothing of the Human's moved: `ShippedPackRows`, `UserPacksMoved`, `SkillsPointedAt` and `SkillsLeftInPlace` are 0, no `Problems`, and the catalog split was the embedded one, not the Human's `SkillsCatalog.json` | None: saved with `"shown": true` |
| No problems | Success: *Packs and Skills moved to SKILLS.md* · **Show** |
| `Problems.Count > 0` | Warning: *Packs and Skills moved to SKILLS.md · {n} could not be moved* · **Show** |
| `Problems.Count == 0` and `SkillsWithIgnoredTools.Count > 0` | Warning: *Packs and Skills moved to SKILLS.md · {n} Skills no longer grant tools* · **Show** |
| `Failed` | Error: *Huddle could not move Packs and Skills to SKILLS.md. It will try again next start.* · **Show log** |

**Show** opens Settings › Skills › Packs (the route belongs to [§6.9](#69-settings--skills-g4g8-and-its-guidance-text)).
**Show log** opens the Library on `SkillSources/sources/local/my-packs/log.jsonl`. The move
creates the `local/my-packs` Source folder, with its `log.jsonl`, on every run, even when every
file failed, so **Show log** always has something to open. When `Failed` before that folder could
be created, no `SKILLS.md` exists and the failure is only in Huddle's own log (below). The
report's detail view lists `SkillsWithIgnoredTools` under the heading *Skills whose tools Huddle
now ignores* (see Step 4).

#### Inputs the move reads

| Input | Read how | Missing or bad |
| --- | --- | --- |
| Embedded `Skills/SkillsCatalog.json` (265 entries; the move writes 263 of them, see Step 1) | `LegacyCatalogReader`, a private copy of the parse rules of today's `SkillsCatalogIndex` (the class itself goes in [§6.14](#614-code-that-goes)) | Throws `InvalidOperationException` as today; caught by the move as `Failed`. Pinned by `SkillsCatalogFileTests` |
| `{DataDir}/SkillsCatalog.json` | Same reader. **When it parses, it replaces the embedded catalog entirely**, exactly as `SkillsCatalogIndex.Reload` does today. It does not merge | Malformed or unreadable: the embedded catalog is used and a `MoveProblem` names the file |
| `{DataDir}/SkillPacks/*.json`, ordered by file path | `LegacyPackReader`, a private copy of `SkillPackStore.Parse` (fields `id`, `version`, `title`, `appliesTo`, `consultWhenAdds`, `skills[{name, source, path}]`) | Listed in the report and not moved (see Packs) |
| `{DataDir}/Skills/*/` with a valid `.source.json` | `SkillProvenance.TryRead` and `SkillFolderHash.ComputeFolder` | A folder with no valid record is a built-in override or the Human's own Skill and is ignored. A name that starts with `.` is ignored |
| `Team:SkillsRepositoryDir` | `Directory.Exists`, then `File.Exists` for `packs/msbuild-authoring.skillpack.json` and `packs/code-review-and-quality.skillpack.json`, then `NOTES.md` | Empty: no rows for the two shipped Packs (below) |
| `Team:ComputerName` | `TeamOptions.ComputerName` (default `Environment.MachineName`) | |

The embedded catalog stays in the app **for one release**: this release keeps
`Skills\SkillsCatalog.json` as an `EmbeddedResource` and `Build-SkillsCatalog.ps1`; the next
release deletes both ([§6.14](#614-code-that-goes)). In that next release the move still runs for
a Human who skips a version, finds no embedded catalog, writes no catalog files, and puts
*"The catalog Huddle used to ship is no longer part of this version"* in the report as a
`MoveProblem`. The Human asks their AI tool for any catalog Skill they want. This is the cost of
not carrying 200 KB of JSON forever.

#### Step 1: claim the Skill names

Skill names are unique across the registry ([§8.3](#83-resolving-packs-catalogs-and-skills)
step 4), and the old catalog is not: `retro` is listed by `garrytan/gstack` and by
`mattpocock/skills`, and `test-driven-development` by `addyosmani/agent-skills` and by
`obra/superpowers`, each with a different origin. If both were written, the checks would report
`D003` on a fresh install. The move therefore settles each name once, in this order:

1. **An imported Skill claims its name first.** A folder in `Skills/` with a valid `.source.json`
   holds a name under its recorded repository and path.
2. **Then the first catalog, in the order SKILLS.md will list them** (largest catalog first, ties
   by id, Ordinal), keeps the name. This is the same winner `SkillsCatalogIndex.FindByName`
   gives today.
3. **An entry whose name is already claimed under a different origin is omitted**, with one
   `moved` line (`action` `duplicate-omitted`) in its Source's `log.jsonl` and a sentence in that Source's
   `NOTES.md`. It is counted in `OmittedDuplicates`. Both pairs above are omitted:
   `mattpocock/skills`'s `retro` and `obra/superpowers`'s `test-driven-development`.

That makes **263** catalog Skills, not 265: dotnet/skills 106, garrytan/gstack 61,
mattpocock/skills 37, addyosmani/agent-skills 25, anthropics/skills 20, obra/superpowers 14.
The vision's counts for those two repositories are two high. Neither omitted Skill was
reachable by name before, so nothing the Human could use is lost.

#### Step 2: split the catalog into one file per repository

Group the entries by `repo`, keeping each group in file order. Each group becomes a Source, a
catalog file and a Catalogs row.

**Catalog id and Source path.** From the repository URL `https://github.com/<owner>/<repo>`:

| Value | Rule | Example |
| --- | --- | --- |
| Catalog `id` | `<owner>-<repo>`, lowercased; each run of characters other than `a-z0-9` becomes one `-`; leading and trailing `-` trimmed. If two groups give the same id, the second gets `-2` | `dotnet/skills` → `dotnet-skills` |
| Catalog file | `catalogs/<id>.skillcatalog.json` | `catalogs/dotnet-skills.skillcatalog.json` |
| Source id path | `github.com/<owner>/<repo>` for host `github.com`; `<host>/<owner>/<repo>` for any other host | `sources/github.com/dotnet/skills/` |
| Source `kind` | `github` for `github.com`, otherwise `git` | |

The ids for the six repositories are `dotnet-skills`, `garrytan-gstack`, `mattpocock-skills`,
`addyosmani-agent-skills`, `anthropics-skills` and `obra-superpowers`. The two shipped Packs
name `dotnet-skills` and `addyosmani-agent-skills`, so these two ids are a contract with the
skills repository and are pinned by `CatalogSplitTests`.

**Field map.** One old entry becomes one catalog entry:

| Old `SkillsCatalog.json` field | New catalog field | Rule |
| --- | --- | --- |
| `id` (`owner/repo/skill`) | none | Identity is now the catalog `id` plus `name`. Nothing else stores the old id |
| `name` | `name` | Unchanged |
| `description` | `description` | Unchanged |
| `publisher` | `publisher` | Unchanged |
| `repo` | the catalog's `source` (`github.com/<owner>/<repo>`) | Once per file, not per entry |
| `path` (in the repository) | part of `origin` | Not stored on its own. The new `path` field means a local folder |
| `link` and `commit` | `origin` | The tree URL at the pinned commit. Use `link` when it is not empty (it equals `{repo}/tree/{commit}/{path}` for all 265 entries; `gstack`'s root Skill has an empty path and no trailing slash). Otherwise build `{repo}/tree/{commit}/{path}`, and with no `commit`, `{repo}/tree/HEAD/{path}` (which `D004` will warn about) |
| `license` | `license` | Unchanged, possibly empty |
| `category` | `category` | Unchanged |
| `subCategory` | `subCategory` | Unchanged. `MemberWarnings` overlap warnings read it |
| `requires` | `requires` | Unchanged |
| (new) | `path` | `""` (Not downloaded), unless Step 4 points it at `fetched/<name>` |

A catalog file:

```json
{
  "schemaVersion": 1,
  "id": "dotnet-skills",
  "title": "dotnet/skills",
  "source": "github.com/dotnet/skills",
  "skills": [
    {
      "name": "target-authoring",
      "description": "Canonical patterns for writing custom MSBuild targets. USE FOR: …",
      "publisher": "Microsoft dotnet",
      "origin": "https://github.com/dotnet/skills/tree/a660de83c2f9cad071a5d568f7d77041ccd07059/plugins/dotnet-msbuild/skills/target-authoring",
      "license": "MIT",
      "category": "Build, CI/CD & Release",
      "subCategory": "MSBuild Authoring",
      "requires": [],
      "path": "../fetched/target-authoring"
    }
  ]
}
```

JSON is written indented, camelCase, LF, UTF-8 without BOM, with the final newline, so the
golden test is stable. A path written by the move uses `/`.

**`source.json` for each of the six.** `kind: github`, and **no single `commit`**, because the
entries of one repository can be pinned to different commits (`dotnet/skills` entries are all at
`a660de8` today, but nothing guarantees it):

```json
{
  "schemaVersion": 1,
  "kind": "github",
  "url": "https://github.com/dotnet/skills",
  "fetchedAt": "2026-10-17T16:25:00+02:00",
  "fetchedBy": "Huddle catalog",
  "signIn": "none",
  "status": "active"
}
```

`ref`, `commit` and `localPath` are omitted. Those three fields are optional in the schema ([§6.2](#62-registry-model-and-file-readers),
[§7.2](#72-schemas)), and `O002` ("no `commit`, so not pinned") is raised for a Source without a
`commit` **only when some catalog or Pack entry naming it fails `D004`** ([§6.3](#63-the-checks)).
Every move-written entry is pinned to a 40-character commit, so a fresh install shows no `O002`.

**`NOTES.md` for each.** Front matter with `layout: huddle-catalog` (so `O005` passes), then
short sections in the shape `InstallSkillPack.md` §6 asks of an AI tool:

```markdown
---
layout: huddle-catalog
manifest: catalogs/dotnet-skills.skillcatalog.json
---
# dotnet/skills

**Found:** the catalog Huddle 2.4.0 shipped inside the app (`SkillsCatalog.json`). Huddle split
it into this file on 2026-10-17. Nobody has cloned this repository for Huddle; the Skills
here are listed, not downloaded.

**Layout:** one entry per Skill, each pinned to a commit in its `origin`. `path` is empty
until a Skill is fetched. Two Skills in other catalogs share a name with a Skill here and
were left out: none.

**To update:** fetch a Skill into any folder, set its `path`, and run `huddle-packs validate`.
To refresh the whole list, read the repository's manifest at a newer commit and rewrite this file.
```

The last sentence of **Layout** lists omitted duplicates by name for the two affected Sources
(*"… and were left out: retro (kept in garrytan/gstack)."*). The Huddle version in **Found** is
`typeof(SkillSourcesMove).Assembly` informational version.

#### Step 3: Packs, from the old schema

The two shipped Packs and the Human's own are written differently.

**The two shipped Packs** (`msbuild-authoring`, `code-review-and-quality`) are never read from
the old embedded JSON. They live in the skills repository, already in the new schema, and the
move only adds rows for them. `E:\Repos\skills` on this computer holds
`packs/msbuild-authoring.skillpack.json`, `packs/code-review-and-quality.skillpack.json` and
`NOTES.md`; each Pack names its Skill by `{ "name": "...", "catalog": "..." }` with catalog ids
`dotnet-skills` and `addyosmani-agent-skills`, and both are `version: "1.0.0"`, which is why
the `_packs: ['msbuild-authoring@1.0.0']` lines Agents carry still match.

| `Team:SkillsRepositoryDir` | `packs/<id>.skillpack.json` in it | What the move writes |
| --- | --- | --- |
| Empty, or not an existing folder | | **No row** for either Pack, no `sources/local/skills/`. A role that suggests one gets `R008` (a warning), and its draft card shows **Needs setup** with **Copy prompt**, which asks the AI tool to clone the repository. A non-empty folder that does not exist is a `MoveProblem`: *"Team:SkillsRepositoryDir '{dir}' does not exist."* |
| An existing folder | exists | A Packs row, with a per-computer entry `{ComputerName}: {abs path}` |
| An existing folder | missing | No row for that Pack, and a `MoveProblem`: *"{dir} has no packs/{id}.skillpack.json."* The other Pack still gets its row |

The row's cells are: **Local path** `DESK-01: E:\Repos\skills\packs\msbuild-authoring.skillpack.json`,
**Origin URL** empty, **Last updated** the date of the move, **Notes**
*Shipped with Huddle; moved from the old format*. The order is `msbuild-authoring`, then
`code-review-and-quality`, then the Human's Packs.

The path is the absolute, normalised form of the key's value, without a trailing separator. If
`Team:ComputerName` does not match the computer-name rule of
[§8.2](#82-per-computer-paths) (2 to 15 letters, digits or hyphens), the move writes the plain
path, which every computer uses, and records a `MoveProblem` that says so. A path that contains a
backslash followed by ASCII punctuation, or any of `` ` * _ [ ] < > | & ! ``, is written as a
code span (with `|` as `\|`), which [§8.1](#81-reading-skillsmd) step 5 reads back literally.

When the folder is set, the move also writes `sources/local/skills/source.json`
(`kind: local`, `localPath` the folder, `fetchedBy: "Huddle move"`, no `url`) and
`sources/local/skills/NOTES.md`, copied byte for byte from the repository's `NOTES.md` when it has
one (the repository's front matter names `layout: huddle-skillpacks`), otherwise a three-line
note with that same front matter. The Source id is `local/skills` (a `local/name` id,
[§7.2](#sourcessourcesourcejson)), so the Packs name `"source": "local/skills"` and the notes live in
`sources/local/skills/`.

**The Human's own Packs.** Each `{DataDir}/SkillPacks/*.json` is read with the old rules. A file
becomes `packs/<id>.skillpack.json` in the Source `local/my-packs` (`kind: local`, `localPath` the
`SkillPacks` folder, `fetchedBy: "Huddle move"`). The order is by `id`, Ordinal.

| Old field | New field |
| --- | --- |
| (none) | `schemaVersion: 1` |
| `id` | `id`. Must satisfy `P003` (lowercase letters, digits, single hyphens) |
| `version` | `version`, unchanged, so the `_packs` entry still matches |
| `title` | `title` |
| (none) | `source: "local/my-packs"` |
| `appliesTo` | `appliesTo` |
| `consultWhenAdds` | `consultWhenAdds` (`""` when absent) |
| `skills[]` `{ name, source, path }` | `skills[]`, by the first rule below that applies |

A Pack Skill is mapped by the first of these that applies:

1. **A catalog entry matches** on repository and path (`https://github.com/{source}` and `path`,
   `.git` and slashes ignored, case-insensitive, as `FindByRepoPath` does) and its name equals
   the old `name`, and the entry was not omitted in Step 1: `{ "name": N, "catalog": C }`. The
   Pack and the catalog then share one copy.
2. **The Skill is already imported:** `Skills/N/.source.json` names the same repository and
   path, and Step 4 copied it: `{ "name": N, "path": "../fetched/N", "origin": <tree URL at the
   recorded commit> }`.
3. **Otherwise** a catalog-less entry: `{ "name": N, "path": "", "origin":
   "https://github.com/{source}/tree/HEAD/{path}" }`, with `{path}` empty when the old path was
   null. It shows **Not downloaded**. The `HEAD` origin gets a `D004` warning (not pinned), which
   is true. For this to be valid, [§6.2](#62-registry-model-and-file-readers) allows an empty
   `path` on a Pack Skill that gives an `origin`; the Pack Skill rule "give `path` or `catalog`"
   (`P009`) means a *present* key, and `""` is present.

A Pack Skill whose old `source` is `builtin` has no new form: a built-in Skill belongs to no
Source and `S005` rejects its name. The whole Pack is skipped with a `MoveProblem`.

**A Pack that does not move.** It gets one `MoveProblem` and one `moved` line (`action` `pack-skipped`) in
`sources/local/my-packs/log.jsonl`, and `SkillPacks/` is left alone so the Human can fix it:

| Reason | `MoveProblem.Message` |
| --- | --- |
| Not valid JSON, or a required field missing | *"{file}: not a valid Pack file ({why})."* |
| `id` fails `P003` | *"{file}: id '{id}' must be lowercase letters, digits and single hyphens."* |
| Two files give the same `id` | *"{file}: another file already defines Pack '{id}'."* (the first file wins, as today) |
| A Skill's `source` is `builtin` | *"{file}: Skill '{name}' is built in and cannot be in a Pack."* |
| The `id` is `msbuild-authoring` or `code-review-and-quality` | Not a problem: see next paragraph |

**A Pack that replaced a shipped one.** Today a user file whose `id` equals a shipped Pack's
replaces it. The vision says the move "keeps yours and hides the shipped one". Hiding writes the
Pack **id** into `hidden.json`, which would hide the Human's own Pack too, since the ids are
equal, and adding both rows is `P004`. So the move **keeps the Human's Pack and adds no row for
the shipped one**, with a `moved` line (`action` `pack-replaced`) and no problem.

The old `source` strings may be `owner/repo` only. A Pack Skill from a non-GitHub source cannot
occur in the old format.

#### Step 4: the Human's imported Skills

For each folder `Skills/<name>/` with a valid `.source.json` (`SkillProvenance.TryRead`):

1. **Modified?** Compute `SkillFolderHash.ComputeFolder` of the folder. If it differs from the
   record's `sha256`, the Skill was edited locally. The move **leaves it in place**: no
   `fetched/` copy, its entry stays **Not downloaded**, `SkillsLeftInPlace` goes up and a
   `moved` line (`action` `skill-left`) says *"edited locally; no pointer written"*. This is deliberate. A
   copy of an edited folder would differ from the recorded hash, so the card would show **Update
   available** on a Skill nobody updated. The Agents' copy is untouched and still shows
   **Modified locally**.
2. **Copy** the folder, without `.source.json` and `.git`, to
   `.move-<random>/fetched/<name>/`. Then hash the copy. It must equal the record's `sha256`.
   If it does not (a file changed while copying), discard the copy and treat the Skill as
   left in place with the reason *"changed while copying"*. At most 200 files and 5 MB, the
   `S003` limits, are copied; larger folders are left in place with *"too large to copy"*.
3. **Point at it.** The copy is `fetched/<name>/`. The `path` written is `../fetched/<name>`
   (relative to the catalog file, `/` separators).
4. **Find its catalog entry** by repository and path (the `FindByRepoPath` rule) with name equal
   to the folder name.
   - **Found:** set that entry's `path`, and set its `origin` to the tree URL at the
     **recorded** commit (`{repo}/tree/{commit}/{path}`). The recorded commit may differ from
     the shipped catalog's commit, and the copy is what was downloaded at the recorded one.
   - **Not found** (a Skill imported from a GitHub link): add an entry to the catalog for its
     repository, creating that catalog and its Source with Step 2's rules if it is not one of
     the six. `name` is the folder name, `description` comes from `SKILL.md` (through the shared
     `SkillValidator`), `publisher` and `license` from the record, `origin` as above,
     `category` and `subCategory` empty, `requires` `[]`.
5. **Count it** in the Catalogs row's Notes: *"From SkillsCatalog.json; 106 Skills, 1
   downloaded"*. A catalog the move creates for an outside repository reads *"Imported by
   Huddle; 1 Skill, 1 downloaded"*.

The Skill's `.source.json` in `Skills/` is **not rewritten**. It keeps today's shape (`repo`,
`path`, `commit`, `sha256`, `publisher`, `license`, `importedAt`). [§6.6](#66-skillcopier-and-sourcejson)
must keep reading that shape, deriving the Source from `repo`, so `Skill.ModifiedLocally` and the
provenance line work on Skills the move did not touch.

Built-in Skills, built-in overrides and Skills the Human wrote have no `.source.json` and are
ignored.

#### Step 5: write SKILLS.md

The file is written from the fixed template of [§8.8](#88-writing-skillsmd), into the staging
folder, as UTF-8 without BOM, LF, with a final newline. Rows:

- **Packs**, in order: the two shipped Packs (when they have rows), then the Human's, by id.
- **Catalogs**, largest first (by the number of Skills the file holds), ties by id Ordinal. Local
  paths are relative, `catalogs/<id>.skillcatalog.json` and `packs/<id>.skillpack.json`.
- **Origin URL** is the repository URL for a catalog, and empty for a Pack.
- **Last updated** is `clock.GetLocalNow()` formatted `yyyy-MM-dd`.
- **Notes** as in the table.

| Row | Notes cell |
| --- | --- |
| A shipped Pack | *Shipped with Huddle; moved from the old format* |
| One of the Human's Packs | *Your own Pack; moved from SkillPacks/* |
| A catalog of the six | *From SkillsCatalog.json; {n} Skills* and, when d > 0, *, {d} downloaded* |
| A catalog the move created | *Imported by Huddle; {n} Skill(s), {d} downloaded* |

No row is ever written for a Skill. A row is written for every catalog and Pack file, even one
that is entirely **Not downloaded**.

#### Step 6: report, log lines and Huddle's log

`log.jsonl` is append-only and written by Huddle. The line schema is defined once, in
[§7.2](#sourcessourcelogjsonl) (`SourceLog`, [§6.12](#612-removing-and-hiding), fills `at`,
`computer` and `huddle`). The move writes every line with `event` `moved`, the `action` below, the
`subject` shown and the sentence as `detail`, into the staging copy of each Source's
`log.jsonl`, so they appear with the Source:

| `action` | Written to | `subject` | `detail` |
| --- | --- | --- | --- |
| `catalog-split` | each catalog's Source | `catalog:<id>` | *"Split {n} Skills out of SkillsCatalog.json (catalog {id})."* |
| `duplicate-omitted` | the omitted entry's Source | `skill:<name>` | *"'{name}' not listed: the name belongs to {otherCatalog}."* |
| `skill-pointed` | the Skill's Source | `skill:<name>` | *"{name} points at fetched/{name}, copied from Skills/{name} at {commit}."* |
| `skill-left` | the Skill's Source | `skill:<name>` | *"{name} stays in Skills/ only: {reason}."* |
| `pack-moved` | `local/my-packs` | `pack:<id>` | *"Pack {id} {version} moved from SkillPacks/{file}."* |
| `pack-skipped` | `local/my-packs` | `file:<file>` | *"{file} not moved: {reason}."* |
| `pack-replaced` | `local/my-packs` | `pack:<id>` | *"Pack {id} replaces the one Huddle shipped; no row was added for the shipped one."* |
| `pack-row` | `skills` | `pack:<id>` | *"Row added for {id} at {path}."* |
| `pack-missing` | `skills` | `pack:<id>` | *"{dir} has no packs/{id}.skillpack.json."* |
| `completed` | every Source written | `file:SKILLS.md` | *"Move finished."* |

Huddle's own log (`ILogger`, direct calls, as the rest of `Huddle.App` allows): `Information`
*"Moving Packs and Skills into {Dir}"* at the start and *"Moved {Catalogs} catalogs ({Skills}
Skills, {Omitted} omitted), {Packs} Pack rows, {Pointed} Skills pointed at, {Left} left in place"*
at the end; `Warning` per `MoveProblem`; `Error` with the exception when the move fails;
`Debug` *"SKILLS.md exists; move skipped"* when it does not run.

#### Errors and edge cases

| Situation | Behaviour |
| --- | --- |
| Skill Sources folder cannot be created or written | Caught (`IOException`, `UnauthorizedAccessException`); `Failed = true`; `Error` log; no SKILLS.md; retried next start |
| Cancelled (host stopping) | The staging folder is deleted and `OperationCanceledException` propagates |
| An imported Skill's folder cannot be read | That Skill is left in place with the reason; the rest go on |
| Embedded catalog missing or malformed | `Failed = true` (a build defect; `SkillsCatalogFileTests` pins it) |
| A user Pack's `appliesTo` names a role that does not exist | Moved as is. `P005` then marks it **Broken**, with the fix in its message; the move has no role list to check against |
| Two Huddles start together on one shared folder | Both stage; the loser's SKILLS.md move throws and it discards its staging. Files in `fetched/` from both are written with overwrite; last write wins ([ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)) |
| `Skills/` holds a Skill whose catalog entry was omitted in Step 1 (a duplicate name) | The imported Skill claimed the name first, so the *other* repository's entry is the one omitted |

**Performance.** About 263 entries to group and serialise, plus one folder copy and two hashes
per imported Skill (at most 200 files and 5 MB each). It runs on the thread pool, awaited at
start-up, and finishes in well under two seconds for ten imported Skills. It runs once.

**V1 vs V2.** V1 moves once and never again. A later release deletes the embedded catalog, the
two legacy readers and this component's tests that depend on them, keeping only the idempotence
and crash-safety tests, which still guard a Human who upgrades across versions.

#### Tests

Tests live in `tests/Huddle.Tests/SkillPacks/Move/`. Each has a `///` summary and asserts.
`MoveFixture` builds a temp `{DataDir}` with the embedded catalog, a fake `Skills/`, optional
user packs, optional skills repository, a `FakeTimeProvider` fixed at 2026-10-17 16:25
(+02:00) and `Team:ComputerName = DESK-01`.

| Test class | Pins |
| --- | --- |
| `MoveDetectionTests` | A first start writes SKILLS.md and returns a report; an existing SKILLS.md makes the move return `null` and touch nothing, not even `Team:SkillsRepositoryDir`'s folder; a second run gives the same tree; `Skills/` is unchanged |
| `MoveCrashSafetyTests` | A fake `IMoveFiles` that throws at every placement in turn leaves no SKILLS.md, and a rerun gives an uninterrupted run's tree; SKILLS.md is placed last with `overwrite: false`; stale `.move-*` folders are cleaned and a young one is left; a failure never escapes |
| `CatalogSplitTests` | Six catalog files with 106/61/37/25/20/14 Skills (263, two duplicates omitted and logged); the six ids exactly; every field of the field map, `path` `""`, `origin` from the old `link` and its fallbacks; the user override catalog replaces the embedded one; each Source's `source.json` and `NOTES.md` front matter |
| `ShippedPackRowsTests` | No rows when `Team:SkillsRepositoryDir` is empty or missing; otherwise one row per Pack with a `DESK-01:` entry, `sources/local/skills/NOTES.md` copied byte for byte, and both Packs resolving against the fixture copy in `tests/Huddle.Tests/SkillPacks/Fixtures/skills-repo/` (never `E:\Repos\skills` itself) |
| `UserPacksMoveTests` | The old-schema field map, `source: "local/my-packs"`, versions kept, the three mapping rules, each skip reason, `SkillPacks/` untouched, a user Pack with a shipped id gets the only row |
| `ImportedSkillsMoveTests` | An imported Skill is copied to `fetched/<name>/` with its recorded hash, `.source.json` and `.git` excluded, and its catalog entry points there; a Skill outside the catalog gets its own catalog, Source and row; a locally modified Skill is left in place with `path` `""` and a `skill-left` line |
| `MoveBoundaryTests` | Agent files, `Skills/` and the old inputs are byte-identical after the move; no `HttpClient` in the move's namespace; no Skill shows **Update available** and `msbuild-authoring@1.0.0` still matches |
| `MoveOutputChecksTests` | `Checks.Run` over the output finds no error, no `O002` and no `D003`; SKILLS.md round-trips through `RegistryReader` |
| `MoveReportTests` | The three snackbar texts; `MoveReportStore.TakeUnshown` returns the report once, across a restart; a blank data folder's report is saved shown, while a user Pack, the Human's `SkillsCatalog.json`, an imported Skill, shipped Pack rows or a problem alone each leave it unshown |
| `SkillsMdGoldenTests` | The golden SKILLS.md below |

**The golden test.** `SkillsMdGoldenTests.SkillsMd_ForFixture_MatchesGolden` runs the move over
the fixture (the real embedded catalog; imported `target-authoring` from `dotnet/skills` and
`code-review-and-quality` from `addyosmani/agent-skills`, both unmodified; one user Pack
`my-pack`; a skills repository holding the two shipped Packs; computer `DESK-01`; date
2026-10-17) and compares `SKILLS.md` byte for byte to
`tests/Huddle.Tests/SkillPacks/Move/Golden/SKILLS.expected.md`, in which `{{skillsRepo}}` stands
for the fixture's repository folder. It follows the repository's golden rule
([Testing](../agents/Testing.md#changing-prompts-and-golden-files)): delete the golden and run the
test to reseed it, then review the diff. The expected content:

```markdown
# Skills registry

> We do not recommend editing this file by hand. Your AI tool adds and updates its rows by
> following InstallSkillPack.md, and a mistake here makes a Pack or Skill show as Broken in
> Huddle.

Every Pack and Skill this Huddle knows about. Each row points at a file on this computer.

## Packs

| Local path | Origin URL | Last updated | Notes |
| --- | --- | --- | --- |
| DESK-01: {{skillsRepo}}\packs\msbuild-authoring.skillpack.json | | 2026-10-17 | Shipped with Huddle; moved from the old format |
| DESK-01: {{skillsRepo}}\packs\code-review-and-quality.skillpack.json | | 2026-10-17 | Shipped with Huddle; moved from the old format |
| packs/my-pack.skillpack.json | | 2026-10-17 | Your own Pack; moved from SkillPacks/ |

## Catalogs

| Local path | Origin URL | Last updated | Notes |
| --- | --- | --- | --- |
| catalogs/dotnet-skills.skillcatalog.json | https://github.com/dotnet/skills | 2026-10-17 | From SkillsCatalog.json; 106 Skills, 1 downloaded |
| catalogs/garrytan-gstack.skillcatalog.json | https://github.com/garrytan/gstack | 2026-10-17 | From SkillsCatalog.json; 61 Skills |
| catalogs/mattpocock-skills.skillcatalog.json | https://github.com/mattpocock/skills | 2026-10-17 | From SkillsCatalog.json; 37 Skills |
| catalogs/addyosmani-agent-skills.skillcatalog.json | https://github.com/addyosmani/agent-skills | 2026-10-17 | From SkillsCatalog.json; 25 Skills, 1 downloaded |
| catalogs/anthropics-skills.skillcatalog.json | https://github.com/anthropics/skills | 2026-10-17 | From SkillsCatalog.json; 20 Skills |
| catalogs/obra-superpowers.skillcatalog.json | https://github.com/obra/superpowers | 2026-10-17 | From SkillsCatalog.json; 14 Skills |
```

The expected file separates the title, note, description and each heading with a blank line, and
ends with one newline. A second golden, `catalogs/dotnet-skills.skillcatalog.json`, pins the JSON
layout of one catalog file the same way, with `target-authoring` pointed at its copy.

**What the move needs from the sections that own the rest** (all settled there):

- [§6.2](#62-registry-model-and-file-readers), [§7.2](#72-schemas): a Pack Skill with
  `"path": ""` and an `origin`; `source.json` with no `ref`, `commit` or `localPath`.
- [§6.3](#63-the-checks): `O002` only when some entry naming the Source fails `D004`.
- [§6.6](#66-skillcopier-and-sourcejson): keeps reading today's `.source.json` shape.
- [§7.2](#sourcessourcelogjsonl): the log line shape; and (Appendix B, `LibraryHiddenFolders`) that
  `.move-*` folders are hidden from the Library root like any name that starts with `.`.
- [§6.10](#610-agent-card-role-form-and-build-a-team): a `_packs` entry whose Pack is not in the
  registry shows a card warning (the vision's step 7).

---

### 6.14 Code that goes

Everything on this page that is deleted, what takes its place, and the dependency-injection
changes. Nothing is deleted until its replacement is built (Appendix A orders the tasks), and
the solution builds after each one.

#### What is deleted or moved

Paths are under `src/Huddle.App/` unless a column says otherwise.

| Code | Replaced by | Notes |
| --- | --- | --- |
| `Skills/SkillDownloader.cs`: `SkillDownloader`, `DownloadedSkill`, `DownloadResult` | Nothing in Huddle. The AI tool downloads | Decision 2. The 2-minute `HttpClient` goes with it |
| `Skills/SkillImporter.cs`: `SkillImporter`, `ISkillImporter`, `ImportPreview`, `ImportOutcome` | `SkillCopier` (copy from a pointer, [§6.6](#66-skillcopier-and-sourcejson)) and the shared `S` checks | The 15-second `HttpClient` that resolved a branch to a commit goes with it |
| `Teams/ImportingSkillInstaller.cs` | A registry check: a Skill that is not downloaded makes the draft **Needs setup**, and `SkillCopier` copies the rest ([§6.10](#610-agent-card-role-form-and-build-a-team)) | `Approve & hire` no longer downloads |
| `ISkillInstaller` in `Teams/HireService.cs` (lines 61 to 69) | `HireService` calls `ISkillCopier` ([§6.6](#66-skillcopier-and-sourcejson)); [§6.10.3](#6103-iskillinstaller-goes-what-replaces-ensureinstalledasync) says how | Line 142 (`EnsureInstalledAsync`) and `InstallSkillsAsync` (line 373) change either way |
| `Components/Settings/SkillImportDialog.razor` | The prompt dialog `G5` ([§6.9](#69-settings--skills-g4g8-and-its-guidance-text)) | **Import skill** in `SkillsPanel.razor` (line 109) opens `G5` on **Add from a link or folder** |
| `Skills/SkillsCatalogIndex.cs`: `SkillsCatalogIndex`, `SkillsCatalogEntry` | `SkillRegistry` ([§6.5](#65-skillregistry-and-registrywatcher)): lookups by name, by catalog and by sub-category | `FindById` and `FindByRepoPath` have no caller left. `SubCategoryOf` moves to `SkillRegistry`. The move keeps a private `LegacyCatalogReader` |
| `TeamTypes/SkillPackStore.cs`: `SkillPackStore`, `SkillPack`, `SkillPackProblem` | `SkillRegistry`: `Current.Resolved.Packs`, `PacksForRole(roleId)`, `Current.Resolved.Findings` | The move keeps a private `LegacyPackReader` |
| `TeamTypes/Builtin/SkillPacks/msbuild-authoring.json`, `code-review-and-quality.json` | The skills repository (`E:\Repos\skills\packs\*.skillpack.json`) | Decision 8 |
| `Skills/SkillFolderHash.cs`, `SkillValidator.cs`, `SkillValidation.cs`, `SkillReviewScanner.cs` | **Moved** to `src/Huddle.SkillPacks/Folders/` (same names, namespace `Agency.Huddle.SkillPacks.Folders`) | Not deleted: `huddle-packs` and Huddle share them. `SkillFolderHash` gains the `.git` skip ([§8.4](#84-noticing-an-update)) |
| `Skills/SkillProvenance.cs` | **Stays**, changed ([§6.6](#66-skillcopier-and-sourcejson)) | Still read by `SkillStore` and `SkillProvenanceLine` |
| `Skills/SkillOrigin.cs` | **Stays** | Its callers move with the Skills list |
| `Teams/TeamPaths.cs`: `SkillPacksDir`, `SkillsCatalogFile` | **Stay**; read only by the move ([§8.7](#87-configuration-huddle-reads)) | Reword their `///` summaries to say so. `TeamPathsTests` keeps pinning them |
| `Huddle.App.csproj` line 42, the `TeamTypes\Builtin\SkillPacks\*.json` `EmbeddedResource` | Deleted | Line 39 (`Skills\SkillsCatalog.json`) **stays for one release**, then goes with `Build-SkillsCatalog.ps1` and the move's `LegacyCatalogReader` |
| `agents/scripts/Build-SkillsCatalog.ps1` | Stays one release | It writes the file the move splits. Deleted with line 39 |
| Registrations in `ServiceCollectionExtensions.cs` | See the next table | |

Other callers of the deleted types change in place; each is in Appendix B.

| Caller | Today | After |
| --- | --- | --- |
| `Components/Pages/BuildTeam.razor` lines 30 and 31 | `@inject SkillPackStore Packs`, `@inject SkillsCatalogIndex SkillCatalog` | `@inject SkillRegistry Registry` |
| `Components/Teams/RoleFormDialog.razor` line 13 | `@inject SkillsCatalogIndex SkillCatalog` | `@inject SkillRegistry Registry`; the picker lists every usable and **Not downloaded** Skill |
| `Components/Shared/AgentProfileCard.razor` line 1609 | `GetService<SkillPackStore>()?.ForRole(role.RoleId)` | `GetService<SkillRegistry>()?.PacksForRole(role.RoleId)` |
| `Components/Shared/AgentProfileCard.razor` line 1711 | `GetService<SkillsCatalogIndex>()` for `SubCategoryOf` | `GetService<SkillRegistry>()` |
| `Teams/HireService.cs` line 106 | A `SkillPackStore packs` constructor parameter | `SkillRegistry registry` and `ISkillCopier copier` |
| `Teams/RoleHirer.cs` line 31, `Teams/TeamDuplicator.cs` line 30 | A `SkillPackStore packs` constructor parameter | `ISkillAvailability skills` (`SkillRegistry` implements it, [§6.10.1](#6101-the-shape-of-the-change)) |
| `Teams/CardResolver.cs` lines 105 to 119 | Expands `role.Packs` from a `SkillPackStore` snapshot | The same expansion over an `ISkillAvailability`, reporting **Needs setup** for a missing or not downloaded Pack or Skill |

#### Dependency-injection changes

All in `src/Huddle.App/ServiceCollectionExtensions.cs`. Line numbers are those of the file as it
is on 2026-10-17 and move as earlier edits land, so each row names the statement as well.

| Lines | Today | Change |
| --- | --- | --- |
| 149 | `services.AddSingleton<SkillPackStore>();` | **Remove**. Add `services.AddSingleton<SkillRegistry>();` in its place |
| 153 | `services.AddSingleton<SkillsCatalogIndex>();` | **Remove** |
| 233 | `services.AddSingleton<SkillStore>();` | Stays |
| 235 to 246 | The comment *"Skill import (SK-2 to SK-7)…"* and the registrations of `SkillDownloader` (two `new HttpClient` calls in total, lines 238 and 244), `SkillImporter` and `ISkillImporter` | **Remove all twelve lines**. The solution then constructs no `HttpClient` for Skills |
| 300 | `AddSingleton<ISkillInstaller, ImportingSkillInstaller>()` | **Remove**; `ISkillCopier` ([§6.6](#66-skillcopier-and-sourcejson)) replaces it. `ServiceCollectionExtensionsTests` line 215 changes with it |
| before 484 (`AddHostedService<DataInitializer>()`), and so before 494 (`BuiltinAgentSeeder`) | | **Add**, in this order: `services.AddSingleton<SkillSourcesSettings>();` `services.AddSingleton<IMoveFiles, FileMoveFiles>();` `services.AddSingleton<SkillSourcesMove>();` `services.AddSingleton<MoveReportStore>();` `services.AddSingleton<RegistryWatcher>();` `services.AddHostedService(sp => sp.GetRequiredService<RegistryWatcher>());` |
| after the `SkillRegistry` line | | **Add**: `services.AddSingleton<ISkillCopier, SkillCopier>();`, `InstallFileWriter` (singleton plus `AddHostedService(sp => …)`, the same pair idiom as `PersonaSupervisor`, **after** `RegistryWatcher`), `SkillPromptBuilder`, `SourceLog`, `RoleLister`, `HiddenStore`, `LibraryRoot` registration for the read-only root ([§6.11](#611-the-skill-sources-library-root)). `SkillSourcesSettings` is registered once, with the hosted-service block above |

`RegistryWatcher` is registered as a singleton **and** a hosted service, as `PersonaSupervisor`
is, because `AgentProfileCard` and the Skills panel must reach the very instance that runs (the
comment above `PersonaSupervisor` in the file records why a second one flakes). The registration
order is a requirement: hosted services start in registration order, and the move must finish
before `PersonaSupervisor` starts. `ServiceCollectionExtensionsTests` gains
`AddTeamServices_RegistryWatcherStartsBeforePersonaSupervisor`, which reads the registered
`IHostedService` descriptors and asserts the index order.

`Huddle.App` gains two `ProjectReference`s (`Huddle.SkillPacks`, `Huddle.Packs`, [§8.9](#89-shipping-huddle-packs)) in
`Huddle.App.csproj`.

#### Tests that go

| Test | Becomes |
| --- | --- |
| `Skills/SkillDownloaderTests.cs`, `Skills/SkillImporterTests.cs`, `Skills/FakeSkillImporter.cs` | Deleted |
| `Teams/ImportingSkillInstallerTests.cs` | Deleted; the **Needs setup** behaviour is pinned by `CardResolverTests` and the `HireService` tests |
| `Skills/SkillsCatalogIndexTests.cs`, `TeamTypes/SkillPackStoreTests.cs`, `TeamTypes/BuiltinPacksTests.cs` | Deleted. `SkillRegistry` tests replace the lookups. The shipped-Pack content is pinned by the move's `ShippedPackRowsTests`, against the fixture copy of the skills repository |
| `Ui/SkillImportDialogTests.cs` | Deleted; `G5` has its own bUnit tests |
| `Skills/SkillsCatalogFileTests.cs` | **Stays one release**, pinning the embedded catalog the move reads; deleted with the embed |
| `Skills/SkillFolderHashTests.cs`, `SkillValidatorTests.cs`, `SkillReviewScannerTests.cs` | **Moved** to `tests/Huddle.Tests/SkillPacks/` with their subjects |

---
## 7. Data Model / Storage Layer

No database table. Everything is a file, or memory that a restart rebuilds. Two folders matter:
`{DataDir}` (this Huddle, never shared) and the **Skill Sources folder** (`{DataDir}/SkillSources`
by default, [§8.7](#87-configuration-huddle-reads); the one that can be shared between Huddles
and computers).

### 7.1 Storage map

Paths are relative to `{DataDir}` unless they start with `SkillSources/`, which means the Skill
Sources folder wherever it is. **Owner** is the party whose format it is and who may change it by
hand; **Writer** is what writes it at run time.

| Data | Where | Owner | Writer | Reader | Shared? |
| --- | --- | --- | --- | --- | --- |
| Instructions for AI tools | `InstallSkillPack.md` | Huddle | `InstallFileWriter` (start-up and on change, once a second at most) | The Human's AI tool; the Human | No |
| Context file | `install-context.json` | Huddle | `InstallFileWriter`, with the file above | `huddle-packs` | No |
| This Huddle's Skills settings | `skill-sources.json`: folder override (`dir`) and `seen` keys. Nothing else | Huddle | `SkillSourcesSettings` (Sources › Folder; `SkillRegistry.MarkSeenAsync` writes through it) | `SkillRegistry` | No |
| Registry | `SkillSources/SKILLS.md` | The AI tool | `SkillSourcesMove` creates it; the AI tool or the Human adds and edits rows; Remove deletes a row's lines ([§8.8](#88-writing-skillsmd)) | `RegistryReader`, `huddle-packs`, the Library | Yes |
| Skill pack files | `SkillSources/packs/*.skillpack.json`, or any path a Packs row names | The AI tool | The AI tool; the move writes the user's own moved Packs; Remove deletes a file inside the folder | `PackFile.Parse`, `huddle-packs` | Yes if inside the folder |
| Skill catalog files | `SkillSources/catalogs/*.skillcatalog.json`, or any path a Catalogs row names | The AI tool | The AI tool fills `path`; the move writes the six shipped ones; Remove deletes a file inside the folder | `CatalogFile.Parse`, `huddle-packs` | Yes if inside the folder |
| Source record | `SkillSources/sources/<source>/source.json` | The AI tool | The AI tool; the move writes the first one per Source | `SourceFile.Parse` | Yes |
| Source notes | `SkillSources/sources/<source>/NOTES.md` | The AI tool | The AI tool; the move writes the first | The AI tool; the Human; the Library | Yes |
| Source log | `SkillSources/sources/<source>/log.jsonl` | Huddle | `SourceLog`, append-only | The Human; the Sources tab | Yes (each line names the computer that wrote it) |
| Hidden items | `SkillSources/hidden.json` | Huddle | The hide and unhide commands ([§6.12](#612-removing-and-hiding)) only; the move never writes it | `SkillRegistry` | Yes |
| The move's report | `skill-sources-move.json` | Huddle | `SkillSourcesMove`, once; `MoveReportStore` marks it shown | `MoveReportStore` (the snackbar after the move) | No |
| The move's staging | `SkillSources/.move-<random>/` | Huddle | `SkillSourcesMove`; removed when the move ends, and a stale one at the next start ([§6.13](#613-the-move)) | Nothing; never read as data | Yes, briefly |
| Copies the move made | `SkillSources/fetched/<name>/` | Huddle (the move) | `SkillSourcesMove`, once | Pointers in the catalog and pack files it wrote | Yes (but see the "Not downloaded" rule for computers that lack the folder) |
| The clones | Anywhere the AI tool chose | The AI tool | The AI tool | Pointers; `SkillCopier` | No |
| Copies Agents use | `Skills/<name>/*`, `Skills/<name>/.source.json` | Huddle | `SkillCopier`; built-in and hand-written Skills are written as today | `SkillStore`, Agents through `read_skill` | No |
| Copy staging and trash | `Skills/.staging/`, `Skills/.trash/` | Huddle | `SkillCopier`; emptied at start-up | Nothing; `SkillStore.ScanDisk` skips names starting with `.` | No |
| An Agent's choices | `_packs`, `skills`, `consult_when` in the Agent's `.md` | The Human | `AgentProfileCard`, `HireService`, `SkillCopier`'s callers | `PersonaFrontmatter`, `SkillRegistry` (holder counts) | No |
| Old Packs | `SkillPacks/*.json` | The Human | Nothing after the move | `SkillSourcesMove` once | No |
| Old catalog override | `SkillsCatalog.json` (user override), embedded `Skills/SkillsCatalog.json` | Human / code | Nothing | `SkillSourcesMove` once | No |
| Registry snapshot, hash caches, `seen` in memory | `SkillRegistry` | App | `SkillRegistry` | UI, `CardResolver`, `HireService` | No; rebuilt on restart |
| Skill Sources Library root | Computed from the Skill Sources folder, read-only; not stored | App | none | Library | n/a ([§6.11](#611-the-skill-sources-library-root)) |

**Where a file lives decides who sees it.** Anything under the Skill Sources folder is visible to
every Huddle that shares it; anything under `{DataDir}` is this Huddle's alone. That is why `seen`
(a viewer's memory) is in `skill-sources.json` and `hidden.json` (a decision about the shared
registry) is in the Skill Sources folder.

### 7.2 Schemas

Rules shared by every file below:

- **UTF-8**, no BOM written (a BOM is read). JSON is indented, two spaces, **camelCase** keys,
  LF line endings, written through a temporary file and a move (`AtomicFile.WriteText`) when
  Huddle writes it.
- **`schemaVersion`** is an integer; this design defines version `1` for every file that has the
  key. A reader rejects any other value as a schema error, and ignores unknown keys.
- **Dates and times** are ISO-8601 with an offset (`2026-10-17T16:40:00+02:00`) in JSON, and
  `yyyy-MM-dd` in SKILLS.md. Huddle writes local time with its offset; a reader accepts any
  offset, including the `Z` of files written before this change.
- **Source id** (the `source` field of pack and catalog files, and the folder under `sources/`):
  `host/owner/repo` or `local/name`, made of lowercase letters, digits, `.` and `-`, with `/`
  between parts; no `..`, no leading `/`, no drive letter. Anything else is `O001`, so a Source
  folder can never lie outside `sources/` ([§14](#14-design-notes--rationale), D-23).
- **Names.** *Skill name rule*: `^[a-z0-9]+(?:-[a-z0-9]+)*$`, 1 to 64 characters, equal to the
  folder name. *Id rule* (Pack, catalog): the same pattern.
- **A path** is where the per-computer form of [§8.2](#82-per-computer-paths) applies. It is
  either a string (every computer) or an object whose keys are computer names and an optional
  `"*"` (every other computer). In SKILLS.md the same idea is one line per entry, `NAME: path`.
  Fields that take this form are marked **path**; every other string is plain text. A relative path
  is relative to the folder of the file it is written in.

#### `*.skillpack.json`

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `schemaVersion` | int | Yes | `1` (`P001`) |
| `id` | string | Yes | Id rule; the file is named `<id>.skillpack.json`; unique across SKILLS.md (`P002`, `P003`, `P004`) |
| `version` | string | Yes | Free text: a version or a short commit. Compared for equality with the Agent's `_packs` entry |
| `title` | string | Yes | Shown on the card |
| `source` | string | Yes | The Source id (rule above), e.g. `github.com/anthropics/skills`; names `sources/<source>/` |
| `appliesTo` | string[] | No (warning `P006` if empty) | Role ids; each must be a role in this Huddle (`P005`) |
| `consultWhenAdds` | string | No (warning `P007` if empty or over 200 characters) | The phrase added to `consult_when` |
| `skills` | object[] | Yes | At least one; names unique (`P008`) |
| `skills[].name` | string | Yes | Skill name rule |
| `skills[].path` | **path** | One of `path` or `catalog` | Where the Skill's folder is on each computer. May be `""`, which means **Not downloaded** (give `origin` with it). A present `path`, even `""`, satisfies `P009` |
| `skills[].catalog` | string | One of `path` or `catalog` | A catalog id from SKILLS.md; the Pack uses that catalog entry's `path`, `origin` and `license` (`P009`) |
| `skills[].origin` | string | No (recommended) | A URL pinned to a commit; with `catalog`, ignored |

```json
{
  "schemaVersion": 1,
  "id": "msbuild-authoring",
  "version": "1.0.0",
  "title": "MSBuild Authoring",
  "source": "github.com/dotnet/skills",
  "appliesTo": ["developer", "release-engineer"],
  "consultWhenAdds": ".csproj, .props or .targets changes, build failures",
  "skills": [
    { "name": "target-authoring", "catalog": "dotnet-skills" },
    {
      "name": "incremental-build",
      "path": { "DESK-01": "E:\\Clones\\dotnet-skills\\skills\\incremental-build", "LAPTOP": "C:\\src\\dotnet-skills\\skills\\incremental-build" },
      "origin": "https://github.com/dotnet/skills/tree/4c1e9a2/skills/incremental-build"
    }
  ]
}
```

A Pack that today's `SkillPackStore` file would have held (`Id`, `Version`, `Title`, `AppliesTo`,
`ConsultWhenAdds`, `Skills`) maps field for field; only `source`, `schemaVersion` and the Skill
`path` or `catalog` are new.

#### `*.skillcatalog.json`

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `schemaVersion` | int | Yes | `1` (`D001`) |
| `id` | string | Yes | Id rule; equals the file name before `.skillcatalog.json`; unique across the Catalogs table (`D001`, `D005`) |
| `title` | string | No | For people; the id is shown when absent |
| `source` | string | Yes | The Source id (`D001`) |
| `skills` | object[] | Yes | May be empty |
| `skills[].name` | string | Yes | Skill name rule; unique in the catalog (`D002`) |
| `skills[].description` | string | Yes | One line. Its length is not checked, because the catalog Huddle shipped has descriptions over 500 characters; the `SKILL.md` description of the Skill itself is checked: error over 500, `S002`; warning over 300, `S012` |
| `skills[].origin` | string | Yes | URL pinned to a commit (`D004` warns on a branch) |
| `skills[].publisher`, `license`, `category`, `subCategory` | string | No | Same as today's `SkillsCatalog.json` |
| `skills[].requires` | string[] | No | Skill names this one depends on; default `[]` |
| `skills[].path` | **path** | No | Empty or absent until fetched |

```json
{
  "schemaVersion": 1,
  "id": "addyosmani-agent-skills",
  "title": "Addy Osmani's agent skills",
  "source": "github.com/addyosmani/agent-skills",
  "skills": [
    {
      "name": "code-review-and-quality",
      "description": "Reviews changes for correctness, readability and risk. Use before merging.",
      "publisher": "Addy Osmani",
      "license": "MIT",
      "category": "Quality",
      "subCategory": "Code Review",
      "origin": "https://github.com/addyosmani/agent-skills/tree/1401c8b8030e023baeebb31781a6653fe8e93026/skills/code-review-and-quality",
      "requires": [],
      "path": { "DESK-01": "E:\\Repos\\skills\\fetched\\code-review-and-quality", "*": "" }
    }
  ]
}
```

#### `sources/<source>/source.json`

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `schemaVersion` | int | Yes | `1` |
| `kind` | string | Yes | `github`, `git` or `local` |
| `url` | string | For `github` and `git` | The Source's URL; no credentials in it |
| `ref` | string | No | The branch or tag followed. `ref`, `commit` and `localPath` are all optional |
| `commit` | string | No | The full commit fetched. A `github` or `git` Source without one raises `O002` only when some pack or catalog entry naming it fails `D004` (the move writes none, because the entries of one repository can be pinned to different commits) |
| `localPath` | **path** | No (expected for `local`) | Where the clone is, for the AI tool's next visit. Huddle does not use it |
| `fetchedAt` | string | Yes | ISO-8601 with an offset |
| `fetchedBy` | string | Yes | Name of the AI tool, or `Huddle catalog` for the move |
| `signIn` | string | Yes | `none` or `required`; never a credential |
| `status` | string | Yes | `active`, `deprecated`, `withdrawn` or `moved` |
| `statusReason` | string | When `status` is not `active` (`O003`) | One sentence for the Human |
| `movedTo` | string | When `status` is `moved` (`O003`) | A Source id or URL |

```json
{
  "schemaVersion": 1,
  "kind": "github",
  "url": "https://github.com/anthropics/skills",
  "ref": "main",
  "commit": "dbd4588f9e1033efb41dad4bef2f7947c8993d44",
  "localPath": { "DESK-01": "E:\\Clones\\anthropics-skills", "LAPTOP": "C:\\src\\anthropics-skills" },
  "fetchedAt": "2026-10-17T16:40:00+02:00",
  "fetchedBy": "Claude Code",
  "signIn": "none",
  "status": "active"
}
```

#### `Skills/<name>/.source.json` (new version)

Written by `SkillCopier` ([§6.6](#66-skillcopier-and-sourcejson)) into every copy it makes.
**Backward compatible with today's `SkillProvenance`**: the seven existing fields keep their
names, types and meaning, and stay required for the reader; four optional fields are added. A
file written by the importer today reads as a new-version file with the four fields absent. A
file written now reads under the old `SkillProvenance.TryRead`, which ignores keys it does not
know, because its wire record names only the seven.

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `repo` | string | Yes (old) | Repository URL; filled from `origin`, else the Source's `url`, else `local:<source id>` |
| `path` | string | Yes (old) | The Skill's folder inside the repository, else the folder name. A plain string here, never per computer: it is a repository path, not a disk path |
| `commit` | string | Yes (old) | Pinned commit, else the Source's `commit`, else `unpinned` |
| `sha256` | string | Yes (old) | `SkillFolderHash.ComputeFolder` of the copy at the moment it was made; lowercase hex, 64 characters |
| `publisher` | string | Yes (old) | Entry's `publisher`, else the owner in `repo`, else `unknown` |
| `license` | string | Yes (old) | Entry's `license`, else `unknown` |
| `importedAt` | string | Yes (old) | ISO-8601: local time with its offset when Huddle writes it now; old files hold UTC with `Z`. The time of the copy |
| `schemaVersion` | int | No (new) | `1`. Absent means an old file |
| `source` | string | No (new) | The Source id (`github.com/anthropics/skills`). Absent: derived from `repo` as `github.com/<owner>/<repo>` |
| `origin` | string | No (new) | The pinned URL the copy came from. Absent (an old file): derived as `https://github.com/{repo}/tree/{commit}/{path}`, so an imported Skill compares equal to its catalog entry after the move |
| `copiedFrom` | string | No (new) | The pointer folder on the computer that made the copy. Informational; never read for a decision, because it differs per computer |

```json
{
  "repo": "https://github.com/anthropics/skills",
  "path": "skills/docx",
  "commit": "dbd4588f9e1033efb41dad4bef2f7947c8993d44",
  "sha256": "9f2c1a0e7b4d5c6a8e3f1b2d4c5a6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f",
  "publisher": "Anthropic",
  "license": "Proprietary",
  "importedAt": "2026-10-17T16:41:07+02:00",
  "schemaVersion": 1,
  "source": "github.com/anthropics/skills",
  "origin": "https://github.com/anthropics/skills/tree/dbd4588f9e1033efb41dad4bef2f7947c8993d44/skills/docx",
  "copiedFrom": "E:\\Clones\\anthropics-skills\\skills\\docx"
}
```

`SkillStore.ScanDisk` and `SkillProvenanceLine.razor` keep working from `sha256` and the seven
old fields; they use `source` only to show **Source removed**.

#### `install-context.json`

Written beside `InstallSkillPack.md` ([§6.7](#67-installskillpackmd-and-the-context-file)); it is
the only coupling between Huddle and `huddle-packs`. Every field is written; the reader requires
all of them, and a missing one is exit code 2 (*"install-context.json in {dataDir} cannot be read:
missing {field}. Start Huddle once so it writes it again, then run this again."*).

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `schemaVersion` | int | Yes | `1` |
| `huddleVersion` | string | Yes | The Huddle that wrote it; `X002` compares it with `huddle-packs`'s own |
| `computer` | string | Yes | This computer's name for [§8.2](#82-per-computer-paths) (`Team:ComputerName`, else the machine name) |
| `dataDir` | string | Yes | Absolute path |
| `skillSourcesDir` | string | Yes | Absolute path |
| `skillsDir` | string | Yes | Absolute path of the folder holding the copies |
| `roles` | object[] | Yes | `id`, `title`, `description` (one line) and optional `suggestedPacks` (string[], the Pack ids the hiring pool suggests for the role: `PoolJob.Packs`) per role Packs may name. `suggestedPacks` is what lets `huddle-packs` raise `R008`; a file without it never does |
| `installed` | object[] | Yes | One per Skill in `Skills/` and each built-in default: `name`, `origin` (string or `null`), `sha256` (string or `null`), `builtIn` (bool) |
| `appTools` | string[] | Yes | The App Tool names only built-in Skills may grant (`SkillGrants.Grantable`) |
| `written` | string | Yes | ISO-8601 with an offset |

```json
{
  "schemaVersion": 1,
  "huddleVersion": "2.4.0",
  "computer": "DESK-01",
  "dataDir": "E:\\Huddle\\App_Data",
  "skillSourcesDir": "E:\\Huddle\\App_Data\\SkillSources",
  "skillsDir": "E:\\Huddle\\App_Data\\Skills",
  "roles": [
    { "id": "developer", "title": "Developer", "description": "Writes and reviews code.", "suggestedPacks": ["msbuild-authoring", "code-review-and-quality"] },
    { "id": "bookkeeper", "title": "Bookkeeper", "description": "Keeps the books." }
  ],
  "installed": [
    { "name": "read-skill", "origin": null, "sha256": null, "builtIn": true },
    { "name": "docx", "origin": "https://github.com/anthropics/skills/tree/dbd4588/skills/docx", "sha256": "9f2c1a0e7b4d5c6a8e3f1b2d4c5a6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f", "builtIn": false }
  ],
  "appTools": ["propose_sprint", "propose_agents", "validate_agent"],
  "written": "2026-10-17T16:25:00+02:00"
}
```

Its size is bounded by the roles and Skills the machine has; unlike the appendix of
`InstallSkillPack.md`, it is never truncated.

#### `SkillSources/hidden.json`

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `schemaVersion` | int | Yes | `1` |
| `packs` | string[] | No | Pack ids to hide; default `[]` |
| `skills` | string[] | No | Skill names to hide; default `[]` |

```json
{ "schemaVersion": 1, "packs": ["msbuild-authoring"], "skills": ["brainstorming"] }
```

Missing file means nothing is hidden. Hiding never edits a pack or catalog file. Hiding is not
removing: a hidden item still resolves, still passes or fails checks and can still be held by an
Agent; it just stops showing in lists and cannot be picked for a new hire.

#### `{DataDir}/skill-sources.json`

This Huddle's own settings, written by `SkillSourcesSettings` through a temporary file and a
move, as `LibraryRootStore` writes `library-roots.json`. It holds two things, `dir` and `seen`, and each writer keeps the other's key as it finds it. The skills repository folder is not here: it is only the configuration key `Team:SkillsRepositoryDir` ([§8.7](#87-configuration-huddle-reads)), read once by the move.

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `schemaVersion` | int | Yes | `1` |
| `dir` | string | No | The Skill Sources folder. When present it wins over `Team:SkillSourcesDir` ([§8.7](#87-configuration-huddle-reads)); a folder that does not exist is refused when set. Plain string: this file is per computer already, so it needs no per-computer form |
| `seen` | string[] | No | The row keys ([§6.2](#62-registry-model-and-file-readers), "Row identity") and `skill:<name>` keys the Human has seen. **Absent means "no baseline yet"**: the first full read writes one and nothing is New ([§6.5](#65-skillregistry-and-registrywatcher)) |

```json
{
  "schemaVersion": 1,
  "dir": "D:\\Shared\\SkillSources",
  "seen": [
    "packs:D:\\SHARED\\SKILLSOURCES\\PACKS\\DOCUMENT-SKILLS.SKILLPACK.JSON",
    "catalogs:D:\\SHARED\\SKILLSOURCES\\CATALOGS\\DOTNET-SKILLS.SKILLCATALOG.JSON",
    "skill:docx"
  ]
}
```

#### `sources/<source>/log.jsonl`

One JSON object per line, UTF-8, LF, appended with `FileMode.Append` and `FileShare.ReadWrite`,
one write per line so two Huddles interleave whole lines. Lines are kept under 4 KB. The file
is never rewritten and not rotated by this design. Huddle writes it; an AI tool is told never
to touch it.

**This table is the one definition of a log line.** The sections that write lines ([§6.5](#65-skillregistry-and-registrywatcher), [§6.6](#66-skillcopier-and-sourcejson), [§6.12](#612-removing-and-hiding), [§6.13](#613-the-move)) name the event and fill `subject`, `action`, `detail`, `code` and `data`; `SourceLog` fills `at`, `computer` and `huddle`.

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `at` | string | Yes | ISO-8601 with an offset |
| `computer` | string | Yes | The computer that wrote the line ([§8.2](#82-per-computer-paths) name: `Team:ComputerName`, else the machine name) |
| `huddle` | string | Yes | The Huddle version |
| `event` | string | Yes | One of the events below |
| `subject` | string | Yes | What the event is about, prefixed with its kind: `pack:<id>`, `catalog:<id>`, `skill:<name>` or `file:<path>` |
| `action` | string | No | A finer label where one event covers several cases; only `moved` uses it |
| `detail` | string | No | One sentence for people |
| `code` | string | No | A check code when the event is `row-broken` |
| `data` | object | No | Event-specific values, named in the table below; an AI tool or a person reads them, Huddle never does |

| Event | Written by | `subject` | `data` keys |
| --- | --- | --- | --- |
| `row-appeared`, `row-broken`, `row-recovered` | `SkillRegistry` after a rebuild | `pack:` or `catalog:` | none |
| `copied` | `SkillCopier` | `skill:` | `sha256`, `outcome` (`Created`, `Replaced`) |
| `update-applied` | The update card (`G7`) | `pack:` or `skill:` | `agents` (names restarted) |
| `hidden`, `unhidden` | Hide and Unhide | `pack:` or `skill:` | none |
| `unheld` | **Remove from Agents** | `pack:` | `holders` (names) |
| `removed` | Remove a Pack or catalog | `pack:` or `catalog:` | `row` (the Local path as written), `fileDeleted` (bool), `agents` (`kept`, `removed` or `none`, Packs only), `holders`, `skills` (count, catalogs only), `packsBroken` (ids, catalogs only) |
| `moved` | The move | `pack:`, `catalog:`, `skill:` or `file:` | none; `action` is one of `catalog-split`, `duplicate-omitted`, `skill-pointed`, `skill-left`, `pack-moved`, `pack-skipped`, `pack-replaced`, `pack-row`, `pack-missing`, `completed` |

```json
{"at":"2026-10-17T16:41:02+02:00","computer":"DESK-01","huddle":"2.4.0","event":"row-appeared","subject":"pack:document-skills","detail":"Document skills dbd4588, 4 Skills, ready to use"}
```

### 7.3 Configuration

Four `Team:` keys (`SkillSourcesDir`, `ComputerName`, `SkillsRepositoryDir`, `HuddlePacksPath`)
and the file that overrides the first (`skill-sources.json`, key `dir`) are settled in [§8.7](#87-configuration-huddle-reads),
which also lists the Engineering Guide rows that must be added with the code; the existing
`Team:Acp:SkillsDir` is unchanged. This section adds none.

### 7.4 Data flow

1. **The AI tool fetches.** It reads `InstallSkillPack.md`, clones or downloads to a folder of its
   choice, writes a pack or catalog file and `sources/<source>/source.json` and `NOTES.md`, runs
   `huddle-packs validate`, and adds one row to SKILLS.md.
2. **`RegistryWatcher` notices.** A watcher event (or the 30-second poll) settles, becomes a
   `RebuildScope` and calls `SkillRegistry.RebuildAsync` ([§8.5](#85-watching-pointers)).
3. **Read.** `RegistryReader` turns SKILLS.md into rows; `PackFile`, `CatalogFile` and
   `SourceFile` read the files the rows name ([§8.1](#81-reading-skillsmd); a half-written file
   is read again, [§8.6](#86-reading-and-copying-while-the-ai-tool-writes)).
4. **Resolve and check.** `RegistryResolver` builds the Packs, catalogs and one `SkillEntry`
   per Skill name and hashes changed Skill folders; then `Checks.Run` judges the result with a
   `CheckContext` built from Huddle's own services and returns every finding and state
   ([§6.5](#65-skillregistry-and-registrywatcher)).
5. **Publish.** `SkillRegistry` adds holders, hidden items and `seen`, replaces its snapshot and
   raises `Changed`; `SourceLog` appends `row-appeared` to the Source's `log.jsonl`.
6. **Show.** Settings › Skills redraws; the new card wears **New**; a snackbar offers **Show**.
   Leaving the tab calls `MarkSeenAsync`, which writes `seen` to `{DataDir}/skill-sources.json`.
7. **Refresh the contract.** `InstallFileWriter` rewrites `InstallSkillPack.md` and
   `install-context.json`, so the next AI tool run (and `huddle-packs`) sees the new row and
   Skill list.
8. **The Human chooses.** On a card the Human picks the Pack. `SkillCopier` copies each Skill
   from its pointer into `Skills/.staging/`, checks the copy, writes `.source.json`, swaps it into
   `Skills/<name>/` and logs `copied` ([§6.6](#66-skillcopier-and-sourcejson)).
9. **Only then the Agent changes.** `skills:`, `consult_when:` and `_packs:` in the
   Agent's `.md` are rewritten; the Agent restarts. `SkillStore` sees the new folder and
   raises `SkillsChanged`.
10. **Holders update.** `SkillRegistry` recounts holders from the Agent files and republishes
    the same resolved registry with new counts; the card shows **In use by 1**.
11. **Later, the AI tool updates the clone.** Back to step 2. The pointer folder's hash now
    differs from `.source.json`'s `sha256`, so the Pack and its holders show **Update available**
    ([§8.4](#84-noticing-an-update)); nothing in `Skills/` or any Agent file changes until
    the Human applies it.
12. **Removing** deletes inside the Skill Sources folder only, rewrites SKILLS.md by deleting a
    row's lines ([§8.8](#88-writing-skillsmd)), and ends at step 2 again. Clones and the copies in
    `Skills/` stay.
13. **`huddle-packs`** takes steps 3 and 4 alone: it reads `install-context.json`, builds the same
    `CheckContext` with `CheckContext.From`, and prints the findings. It writes nothing.

---

## 8. Core Algorithms / Processing Logic

The eight mechanics the vision left open are settled here. Each subsection states the rule
first and the reason after it; §14 records the alternative each one beat.

### 8.1 Reading SKILLS.md

`RegistryReader.Read(string text, string registryDir, string computer)` is a pure function
in `Huddle.SkillPacks`. It returns a `Registry` of rows and a list of findings, and never
throws for bad content.

1. **Size and encoding.** The file is read as UTF-8; a BOM is tolerated. A file over 1 MB is
   not parsed (`R011`, error), so a runaway write cannot stall the watcher.
2. **Parse with Markdig**, with the pipe-table extension only (the same package the Library
   uses, already in `Directory.Packages.props`). No HTML is rendered.
3. **Find the tables.** For each level-2 heading whose plain text, trimmed and compared
   `OrdinalIgnoreCase`, is `Packs` or `Catalogs`, the table is the **first pipe table after
   the heading and before the next level-1 or level-2 heading**. Any other table, anywhere, is
   ignored, so people can add their own. A second `Packs` or `Catalogs` heading is `R012`
   (warning) and its table is ignored.
4. **Map columns by header.** Header cells are trimmed and matched `OrdinalIgnoreCase` against
   `Local path`, `Origin URL`, `Last updated` and `Notes`. Order is free; unknown columns are
   ignored; a missing `Local path` column is `R002`.
5. **Read a cell as plain text.** The cell's inlines are flattened: a code span contributes its
   literal text (so `` `E:\a b\c.json` `` is the path `E:\a b\c.json`), emphasis contributes its
   children, a link contributes its URL in `Origin URL` and its text elsewhere, and each
   `<br>`, `<br/>` or `<br />` HTML inline is a line break. `\|` is a literal bar (Markdig's
   own escape). Each line is trimmed.
6. **Split a path cell into entries** ([§8.2](#82-per-computer-paths)). An empty `Local path`
   is `R003`.
7. **Keep the source span.** Each row records its first and last line number in the file, so
   a removal ([§8.8](#88-writing-skillsmd)) deletes exactly those lines.

Rows keep their file order, which is the tie-breaker everywhere below: **the first row wins**.

### 8.2 Per-computer paths

The same rule reads a SKILLS.md path cell and a `path` value in a pack or catalog file.

**In SKILLS.md**, each line of the cell is one entry:

| Line | Reads as |
| --- | --- |
| `E:\Clones\x\a.skillcatalog.json` | A plain entry: every computer |
| `catalogs/a.skillcatalog.json` | A plain entry, relative to SKILLS.md's folder |
| `DESK-01: E:\Clones\x\a.skillcatalog.json` | An entry for the computer `DESK-01` |

A line is a computer entry when it matches
`\A(?<computer>[A-Za-z0-9][A-Za-z0-9-]{1,14}): (?<path>.+)\z`: a name of **2 to 15**
letters, digits or hyphens, then a colon **and a space**. A drive letter is one character and
is followed by `\` or `/`, never a space, so `E:\…` can never read as a computer named `E`.
Fifteen is the Windows NetBIOS limit, which is what `Environment.MachineName` returns.

**In JSON**, `path` is either a string (a plain entry) or an object whose keys are computer
names, plus an optional `"*"` key for every other computer:

```json
"path": { "DESK-01": "E:\\Clones\\skills\\docx", "LAPTOP": "C:\\src\\skills\\docx", "*": "" }
```

**Resolving** for this computer, named by `Team:ComputerName` (default
`Environment.MachineName`):

1. The entry whose name equals this computer's, compared `OrdinalIgnoreCase`, wins.
2. Otherwise the plain entry (or `"*"`), if any.
3. Otherwise there is no path here, and the row or Skill is **Not downloaded** here (`R009`,
   warning). `S001` is a different case: a path exists for this computer but is empty, or leads
   to no folder.

Two entries for one computer are `R010` (error); two plain entries are `R013` (error). A
relative path is resolved against the folder of the file it is written in, accepting `/` or
`\`, with `Path.GetFullPath`. Environment variables and `~` are **not** expanded: they differ
between computers, which is the problem per-computer entries already solve.

### 8.3 Resolving Packs, catalogs and Skills

`RegistryResolver.Resolve(Registry, IFileReader, CheckContext)` turns rows into a `ResolvedRegistry`:
Packs, catalogs and one `SkillEntry` per distinct Skill name, each with a state.

1. **Load each row's file** at its resolved path. A row whose path leads nowhere is `R003`
   (**Broken**). A file that fails its schema is `P001`–`P003` or `D001` (**Broken**), unless it
   changed in the last two seconds ([§8.6](#86-reading-and-copying-while-the-ai-tool-writes)).
2. **Index catalogs by `id`.** Catalog ids are unique across the Catalogs table: a second
   catalog with an `id` already seen is `D005` (error) and is **Broken**; the first row keeps
   the id.
3. **Resolve each Pack Skill.**
   - With `path`: the Skill is the folder that path resolves to.
   - With `catalog`: find the catalog with that `id`, then the entry with that `name`. The Pack
     Skill takes the entry's `path`, `origin` and `license`. A missing catalog or entry is
     `P009`, which makes the **Pack** Broken. A Pack Skill must give `path` or `catalog`,
     never both (`P009`).
4. **One Skill per name.** Skill names are unique across the whole registry, because each
   lands in the flat `Skills/<name>/` folder. When the same name comes from two places with the
   same `origin`, they are one Skill. With different origins it is `D003`: the first row's Skill
   keeps the name, the later one is **Broken**.
5. **State of a Skill**: **Broken** if any error check fails; **Not downloaded** if it has no
   path here or the path leads to no folder; otherwise usable, refined by
   [§8.4](#84-noticing-an-update) into **Available**, **In use by N** or **Update available**,
   and by its Source's `status` into **Deprecated** or **Withdrawn**.
6. **State of a Pack**: **Broken** if its file or any Skill is Broken; **Not downloaded** if
   any Skill is Not downloaded. A Pack is chosen whole, so a Pack with one missing Skill cannot
   be chosen; its card names the missing Skills.

### 8.4 Noticing an update

**What is hashed.** `SkillFolderHash.ComputeFolder` as it is today: every file in the folder,
recursively, ordered by relative path, CRLF normalised to LF for text extensions, skipping
`.source.json`. One change: it also skips any `.git` folder, for the case of a Skill that is
the root of its own clone. Normalising line endings matters here, because two computers
sharing one Skill Sources folder may check the same commit out with different line endings.

**What the catalog's `sha256` is compared with (S010).** A second method, `SkillFolderHash.ComputeCatalogFolder`
(`ComputeCatalog` for in-memory files), lays the bytes out the same way but reads CRLF as LF in **every file that
is not binary**, where binary means a NUL in the first 8000 bytes, as git decides. A clone made with
`core.autocrlf=true` turns `.sh`, `.py` and `.js` files into CRLF, and the extension list above would read that
healthy Skill as altered. The first method is not widened, because it produced the hashes already recorded in
`.source.json` and a wider rule would make every one of them read as modified. The resolver computes the catalog
hash only for an entry that pins one and does not cache it.

**What the hash is compared with.** Huddle holds **one copy per Skill**, `Skills/<name>/`,
shared by every Agent that holds it. Its `.source.json` records the hash of the copy as it
was made (`sha256`, as today). Two comparisons follow:

| Compared | Differs means | Shown as |
| --- | --- | --- |
| The pointer folder's hash with `.source.json`'s `sha256` | The AI tool brought new files | **Update available** |
| The copy's own hash with `.source.json`'s `sha256` | Someone edited the copy | **Modified locally**, as today (`Skill.ModifiedLocally`) |

When both hold, `G7` warns that applying the update discards the local edits.

**For a Pack**, an Agent's Pack is out of date when any of these hold:

- the Pack file's `version` differs from the version in the Agent's `_packs` entry;
- the Pack lists a Skill the Agent's `skills:` does not hold, or no longer lists one it
  added;
- any of its Skills shows **Update available**.

> [!IMPORTANT]
> This corrects the vision's update card (`G7`). Because a Skill's copy is shared, applying an
> update to a **Skill's files** changes it for every Agent that holds it, at once and with
> no restart (Skill bodies are read live, [Skills spec §4](Huddle.Skills-Specifications.md#4-design-principles)
> principle 7). `G7` therefore lists those Agents as **affected**, without checkboxes. The
> checkboxes stay only for **membership** changes: Skills added to or removed from the Pack,
> its `consult_when` phrase and the `_packs` version, which are per Agent and do restart it.

**When hashing runs.** Hashing a folder reads it in full, so it runs only for the Skill
folders under a changed path ([§8.5](#85-watching-pointers)), at start-up, on **Reload**, and
never on the UI thread. Results are cached by folder path with the newest write time and file
count seen; a folder whose newest write time and count are unchanged is not re-read.

### 8.5 Watching pointers

The `RegistryWatcher` (a hosted service in `Huddle.App`) keeps the `SkillRegistry` current.
It builds on the existing `DebouncedFileWatcher` (`src/Huddle.App/Watching/`), which already
debounces by 500 ms.

| Watched | How | Why |
| --- | --- | --- |
| The Skill Sources folder | One watcher, recursive | SKILLS.md, the pack and catalog files kept there, `source.json` and `NOTES.md` |
| Each folder holding a pack or catalog file outside it | One watcher per distinct folder, not recursive | A pointer to a file anywhere |
| Each Skill folder outside it | One watcher per **watch root**, recursive | Skill folders inside one clone share a root |

**Watch roots.** Take the distinct resolved Skill folders outside the Skill Sources folder. A
folder's watch root is its parent folder. Then drop every root that lies under another root.
For the anthropics example, four folders under `E:\Clones\anthropics-skills\skills\` give the
single root `…\skills\`. At most **32** watchers are created in total; past that, the rest
are polled.

**Events.** Every settled event from every watcher goes into one queue. When the queue goes
quiet, the watcher works out what the changed paths touch: SKILLS.md re-reads everything; a
pack or catalog file re-resolves that row; a path under a Skill folder re-hashes that Skill.
Then it publishes a new snapshot, as `SkillPackStore` does today, and raises
`SkillRegistry.Changed`. The set of watchers is rebuilt whenever the resolved paths change.

**Polling.** Every **30 seconds** a timer checks what a watcher cannot: paths that do not
exist yet (a clone still to come, a drive not plugged in), roots past the 32-watcher cap, and
roots on network drives, where `FileSystemWatcher` misses events. A poll compares the newest
write time and file count, and only a difference triggers the same processing. A watcher that
raises `Error` (buffer overflow, a removed drive) is disposed, its root moves to polling, and
the next quiet period triggers a full re-read.

### 8.6 Reading and copying while the AI tool writes

An AI tool may be mid-write when Huddle looks. Two rules keep that from showing as damage.

**Reading.** A pack, catalog or `source.json` file that fails to parse **and** was written in
the last **2 seconds** is not reported yet. The watcher reads it again 2 seconds later; only a
second failure makes the row **Broken**. Writing those files through a temporary file and a
rename is in `InstallSkillPack.md` §2 as a request, never relied on.

**Copying** a Skill into `Skills/<name>/`, when a Pack or Skill is chosen or an update applied:

1. **Wait for quiet.** If any file in the source folder was written in the last 2 seconds,
   wait until none has been for 2 seconds, up to 30 seconds.
2. **Hash the source** (`before`).
3. **Copy** into `Skills/.staging/<name>-<random>/`, skipping `.git` and `.source.json`.
4. **Hash the source again** (`after`) and **hash the copy**. All three must agree. If they
   do not, discard the staging folder and start again from step 1, at most three times; then
   fail with *"The files for {name} kept changing while Huddle copied them. Try again when
   your AI tool has finished."*
5. **Check the copy**, not the source, with the S checks. A failure discards it.
6. **Write `.source.json`** into the copy: the Source, `origin`, commit, the hash and the time.
7. **Swap.** Rename `Skills/<name>/` (if any) to `Skills/.trash/<name>-<random>/`, move the
   staging folder into place, then delete the trash. A failure between the renames puts the old
   folder back. At start-up, any `.trash/<name>-*` whose `Skills/<name>/` is missing (a crash
   between the two renames) is moved back first; then `.trash` and `.staging` are emptied. Both
   are never read as Skills
   (`SkillStore.ScanDisk` gains a rule to skip folder names that start with `.`, which the
   Skill name rule could never accept anyway).

Only after every Skill of a choice has been swapped does Huddle change the Agent's file, so
an Agent never names a Skill whose copy failed.

### 8.7 Configuration Huddle reads

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:SkillSourcesDir` | `{DataDir}/SkillSources` | The Skill Sources folder. Pointing it at a synced folder or a checkout shares it |
| `Team:ComputerName` | `Environment.MachineName` | The name per-computer paths are matched against. Set it when two computers share a name, and in tests |
| `Team:SkillsRepositoryDir` | empty | Read once, by the move ([§6.13](#613-the-move)): where this computer's copy of the skills repository is. Empty means the move adds no rows for the two Packs Huddle used to ship |
| `Team:HuddlePacksPath` | `huddle-packs.exe` beside the app, else `dotnet huddle-packs.dll` | The command line `InstallSkillPack.md` gives for validating |

**Folder set on the Sources tab.** **Sources › Folder** writes `{DataDir}/skill-sources.json`
(`{ "dir": "…" }`), through a temporary file and a move, as `LibraryRootStore` does for
`library-roots.json`. When that file names a folder, it wins over `Team:SkillSourcesDir`. A
folder that does not exist is refused when it is set. Changing it re-reads everything and
writes `InstallSkillPack.md` again; it never moves files.

**Existing keys that stay.** `Team:Acp:SkillsDir` (default `Skills`) is still where the copies
Agents use live. There are no other Skills keys today, and `TeamPaths.SkillPacksDir` and
`TeamPaths.SkillsCatalogFile` are read only by the move.

**These keys need rows** in the [Engineering Guide](Huddle.EngineeringGuide.md)'s `Team:`
table, in the same commit as the code that reads them.

### 8.8 Writing SKILLS.md

Huddle writes SKILLS.md in exactly two cases: the move creates it, and **Remove** deletes
rows from it. It never reformats it.

- **Create.** The move writes the whole file from a fixed template: the title, the
  "We do not recommend editing this file by hand" note, a line of description, and the two
  tables.
- **Remove a row.** Read the file, parse it, find the row by its resolved path, and delete
  its source lines ([§8.1](#81-reading-skillsmd) step 7). Everything else is written back byte
  for byte, through a temporary file and a rename. If the file changed between the read and
  the write (compared by length and write time), start again, at most three times.

### 8.9 Shipping `huddle-packs`

| Question | Answer |
| --- | --- |
| **Form** | A framework-dependent console app, `src/Huddle.Packs`, assembly `huddle-packs`, on the same target framework as `Huddle.App`. It needs the .NET runtime Huddle already needs, and nothing else |
| **Where it lands** | Beside the app. `Huddle.App` has a `ProjectReference` to it, so a build or publish puts `huddle-packs.exe`, `.dll` and `.runtimeconfig.json` in the app's output folder. Task P9 checks that the apphost is copied; if it is not, a `Copy` target after build does it |
| **On `PATH`?** | No. `InstallSkillPack.md` gives the full command line in its front matter, so the AI tool never has to find it |
| **Version** | The same version as Huddle, from `Directory.Build.props`. `--version` prints it and the `schemaVersion` it checks |
| **Mismatch** | The context file records the Huddle version that wrote it. If it differs from `huddle-packs`'s own, every run prints `X002` (warning): *"huddle-packs 2.4.0 is checking data written by Huddle 2.5.0; use the command line in InstallSkillPack.md."* |
| **Argument parsing** | By hand, as `Huddle.Console` does. No new package |

---

## 9. Incremental vs Full Processing

| Trigger | Work done |
| --- | --- |
| Start-up | The move if SKILLS.md is missing (§6.13); then a **full read**: parse SKILLS.md, load every file, hash every reachable Skill folder, run every check, publish |
| SKILLS.md changed | Full read, but hashes come from the cache (§8.4) unless a folder's newest write time or file count changed |
| A pack or catalog file changed | Re-load and re-resolve that row; re-run the checks for it and for any Pack that names its catalog; publish |
| A `source.json` or `NOTES.md` changed | Re-run the `O` checks for that Source and refresh the states of its items |
| A path under a Skill folder changed | Re-hash that Skill; re-run its `S` checks; refresh its state and those of the Packs that hold it |
| **Reload** (`G4b`) | Full read with the hash cache cleared |
| Folder setting changed (§8.7) | Rebuild the watchers, then a full read |
| `hidden.json` changed | Refresh visibility only; no checks |

Every path ends in one new snapshot. A partial update copies the previous snapshot and
replaces only what changed, so readers never see a mix.

---

## 10. Background Workers / Async Components

| Worker | Kind | Runs | Must not |
| --- | --- | --- | --- |
| `SkillSourcesMove` | Called once by `RegistryWatcher.StartAsync`, before the first full read | At start-up, only when SKILLS.md is missing | Run after any Agent has started; touch an Agent file |
| `RegistryWatcher` | `IHostedService`, one per process | From start-up to shutdown | Block the UI thread; throw out of its loop (a failed read is logged and the previous snapshot kept) |
| Poll timer | Inside `RegistryWatcher`, `TimeProvider`-based | Every 30 s | Hash a folder whose write time and count are unchanged |
| `InstallFileWriter` | Subscribes to `SkillRegistry.Changed` and to role and Skill changes | Coalesced, at most once a second | Write when nothing in its output changed (it compares before writing) |
| `SkillCopier` | Called by the UI and `HireService` | On demand | Run two copies of one Skill name at once (one `SemaphoreSlim` per name, since the copy is async) |

All timing uses an injected `TimeProvider`, so tests drive the 500 ms debounce, the 2-second
quiet rule and the 30-second poll without sleeping.

---

## 11. Performance Expectations

| Measure | Target | Why it is reachable |
| --- | --- | --- |
| Full read with the six shipped catalogs and nothing downloaded | Under 300 ms | 263 small JSON entries and one Markdown file; no folders to hash |
| Full read with 50 downloaded Skills, cache warm | Under 500 ms | Hashes come from the cache; only write times and counts are read |
| Full read with 50 downloaded Skills, cache cold | Under 3 s, off the UI thread | 50 folders of at most 5 MB each (`S003`) |
| A new row to a **New** card | Under 1.5 s | 500 ms debounce, one row loaded, one snapshot |
| `huddle-packs validate` on the same data | Under 2 s, cold start included | Framework-dependent, no ASP.NET, no host |
| Copying one Skill | Under 1 s for 5 MB | Two hashes and one copy of at most 5 MB |
| Memory | Under 5 MB for the snapshot | Entries and findings, never file bodies |

---

## 12. Edge Cases and Failure Modes

Each row says what the Human sees and what Huddle does, then names the test that pins it. A
name with a dagger (†) is a test this table adds: it is not in a component section's own test
list, and the Appendix A task in brackets writes it. A bare check code in the last column means
`RegistryChecksTests`, `SkillFolderChecksTests` or `ContextTests` already raise and clear that code
([§6.3](#63-the-checks)). Two rules hold for every row: Huddle never throws for bad content
([§6.1](#61-huddleskillpacks-library-overview) rule 2), and an Agent keeps working, because it
reads the copy in `Skills/`, never the pointer ([§4](#4-design-principles) principle 6).

### 12.1 The registry file and the folder around it

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **SKILLS.md missing after the move**, or deleted by hand while Huddle runs | The next rebuild publishes a snapshot whose only finding is `R001`; no row survives. The Packs tab shows `G4d`; **Reload** stays. Cards and drafts treat every Pack as unknown. Agents are untouched. At the **next start** `RegistryWatcher.StartAsync` calls `SkillSourcesMove` before its first full read, and the move runs again because SKILLS.md is its marker; it writes SKILLS.md and any missing file and never overwrites a file that exists ([§6.13](#613-the-move)) | `R001` in `RegistryChecksTests`; `Rebuild_SkillsMdDeleted_PublishesR001AndNoRows` † (P12); `RunAsync_SkillsMdDeletedByHand_RunsAgain` † (P16) |
| **Empty or garbage SKILLS.md**: 0 bytes, plain prose, binary, invalid UTF-8 | No `Packs` or `Catalogs` heading, so `R001`; invalid bytes decode to `U+FFFD` and never throw. A table without a `Local path` column is `R002` and its rows are not read. Another table is ignored ([§8.1](#81-reading-skillsmd) step 3) | `Read_EmptyText_R001` †, `Read_BinaryGarbage_R001AndNoThrow` †, `Read_TableWithoutLocalPath_R002` † (all P2) |
| **SKILLS.md over 1 MB** | `R011`; the file is not parsed, and the previous good rows are **not** kept. The check reads the length first, so a runaway write cannot stall the watcher | `Read_OverOneMegabyte_R011AndNoRows` † (P2); `R011` in `RegistryChecksTests` |
| **SKILLS.md half written** by the AI tool when Huddle reads | Rows that parse are used. A pack, catalog or `source.json` file that fails to parse and was written in the last 2 seconds is not reported; the watcher reads it again 2 seconds later and only a second failure is **Broken** (U9, [§8.6](#86-reading-and-copying-while-the-ai-tool-writes)) | `Resolve_ParseFailureWithinTwoSeconds_NotReportedYet` † (P5); `Run_ParseFailureAfterRetry_IsBroken` † (P6) |
| **A drive is unplugged** (clone, pack file or the Skill Sources folder on `E:`) | A pack or catalog file on it is `R003` (**Broken**); a Skill folder on it is `S001` (**Not downloaded**). Update checks stop for it, and the copies in `Skills/` stay. The watcher raises `Error` and the root moves to the 30-second poll; when the drive returns, the poll sees the path appear and triggers a full re-read, so the cards recover without a click | `R003_FileOnMissingDrive_Broken` † (P6); `Watcher_Error_MovesRootToPollingAndFullReread` † (P12); `Poll_PathAppearsLater_TriggersRebuild` † (P12) |
| **The Skill Sources folder on the unplugged drive** (`skill-sources.json` `dir`, or `Team:SkillSourcesDir`) | At start Huddle tries to create it. If it cannot, the snapshot is empty and the Sources tab names the folder as the problem; the move reports `Failed` and retries next start. If the drive is back as an empty folder, the move runs there as on a fresh install | `StartAsync_SkillSourcesDirOnMissingDrive_EmptySnapshotAndSourcesTabNamesFolder` † (P12); `RunAsync_FolderNotWritable_ReportsFailedAndStartsApp` |
| **`skill-sources.json` points at a folder that does not exist** (deleted since it was set) | `SetDir` refuses a missing folder, so this only happens after the fact. Start-up creates the folder, as for the default; the move may then run in it. `skill-sources.json` that is not JSON is treated as absent (no `dir`, no `seen`, so nothing is **New**) and a warning is logged | `SetDir_MissingFolder_Refused` † (P10); `StartAsync_DirMissing_CreatesIt` † (P12); `SkillSourcesJson_Unreadable_TreatedAsAbsent` † (P10) |
| **The clone moved** | A pack or catalog file at the old path is `R003`; a Skill folder is `S001`. Both name the old path. **Copy fix prompt** asks the AI tool to correct the row. With a per-computer entry only this computer's entry needs changing. Copies in `Skills/` are unchanged, so nothing is lost | `R003_EmptyPath_Raised`; `S001` in `SkillFolderChecksTests`; `BrokenCard_ShowsCopyFixPrompt` † (P20) |
| **Two Huddles share the Skill Sources folder and write at once** | Last write wins ([ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)). `log.jsonl` lines are whole appends and interleave without tearing. `hidden.json` is replaced whole through a temporary file. A SKILLS.md row removal re-reads and retries up to 3 times if the length or write time changed. Two first starts both stage the move; the loser's SKILLS.md `Place` throws and its staging is discarded. `seen`, `InstallSkillPack.md` and the context file are per Huddle, so they never collide | `SourceLogTests` (concurrent appends); `SkillsMdWriterTests` (retry); `HiddenStoreTests`; `RunAsync_OtherHuddleWinsRace_DiscardsStaging` † (P16) |
| **A shared folder already holds SKILLS.md** when this Huddle first starts (U7) | The move is skipped. This Huddle's imported Skills have no `fetched/` copy; their catalog entries are **Not downloaded** here; nothing shows **Update available**. Rows with only another computer's entry are **Not downloaded** (`R009`) and **Copy fix prompt** asks for this computer's entry | `RunAsync_SharedFolderFromOtherHuddle_SkipsAndLeavesSkillsAlone`; `R009_NoEntryForThisComputer_NotDownloaded` |
| **A computer named with one letter** (for example `E`) | A SKILLS.md line `E: C:\x` cannot be a computer entry: a name is 2 to 15 characters, so it reads as a plain entry whose text is not a path (`R003`). The JSON object form has no length rule, so `{ "E": "C:\\x" }` works. The move writes the plain path and a `MoveProblem` when `Team:ComputerName` breaks the rule. A drive path `E:\x` is never a computer entry | `Resolve_DriveLetterPath_IsNotAComputerEntry`; `Parse_OneLetterComputerLine_IsPlainEntryAndR003` † (P2); `FromJson_OneLetterComputerKey_Resolves` † (P2); `Rows_InvalidComputerName_WritesPlainPath` |
| **Two entries for one computer, or two plain entries** | `R010` or `R013`, **Broken**, with the fix hint of the code | `R010`, `R013` in `RegistryChecksTests` |

### 12.2 Files that Packs and catalogs point at

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **The same Skill name in a Pack path and in a catalog** | Same `origin`: one Skill, the first provider's data wins. Different `origin` (compared exactly): `D003`, the first row keeps the name, the later Skill is **Broken**. A Pack Skill that gives both `path` and `catalog`, or neither, is `P009` | `Run_SameNameDifferentOrigin_FirstRowWins_LaterIsBroken`; `Resolve_PackSkillByCatalog_TakesCatalogPathAndOrigin`; `SharedSkill_ReportedOnceOnFirstRow`; `P009_PathAndCatalog_Raised` |
| **Two catalogs share an `id`, or two Packs share an `id`** | `D005` or `P004`; the later row is **Broken**, the first keeps the id | `D005_SecondCatalogSameId_FirstWins`; `P004` in `RegistryChecksTests` |
| **A Skill folder holds a junction or symbolic link** | `S004`, **Broken**. The copier checks the source before it copies and skips reparse points as a second guard, so nothing outside the folder is ever read into `Skills/`. A junction in `sources/<id>/` is unlinked, never followed, when a Source is removed | `S004` in `SkillFolderChecksTests`; `CopyAsync_ReparsePointInSource_IsRefused`; `RemoveSource_ReparsePointInside_IsUnlinkedTargetKept` |
| **A Skill name that Windows cannot store**, or two names that differ only in case | `S004`, **Broken** | `S004` in `SkillFolderChecksTests` |
| **A catalog Skill's `path` is empty** (the 263 the move writes) | **Not downloaded** and no finding, even with `--strict`; only a Pack's Skill with no usable path is `S001`. A Pack Skill with `"path": ""` is valid and shows **Not downloaded** | `EmptyCatalogPath_NoFinding_EvenStrict`; `Parse_PackSkillEmptyPath_IsValidAndNotDownloaded` |
| **A Pack with one missing Skill** | The Pack is **Not downloaded** and cannot be chosen whole; its card names the missing Skills. A role that suggests a Pack id no row provides skips it silently (D-18); `R008` warns on the Packs tab | `CheckReady_PackWithMissingSkill_ListsMissingWithReason`; `R008_NoSuggestedPacks_NeverRaised` |
| **A catalog with 5,000 entries** | It loads: nothing in the schema caps the count, only Skills with a filled-in, existing `path` read folders. The Skills tab is the cost: §6.9.3 sizes it for about 300 rows with no paging. This section's decision (task P19): the grid uses `Virtualize`, and a full read of a 5,000-entry catalog with nothing downloaded stays under 1 s | `Read_FiveThousandEntryCatalog_UnderOneSecond` † (P5); `SkillsTab_FiveThousandRows_RendersWindowOnly` † (P19) |
| **A Skill's `SKILL.md` names an App Tool in `tools:`** (U11) | `S011`, **Broken** at the pointer. A copy that slipped past is checked again by the copier (`CheckFailed`, no folder left). `SkillStore` ignores the `tools:` line of every folder in `Skills/` that has a `.source.json`, so a copied Skill can never grant an App Tool | `S011` in `SkillFolderChecksTests`; `CopyAsync_SkillWithAppToolInTools_FailsCheckS011AndLeavesNoFolder`; `All_FolderWithSourceJson_IgnoresToolsLine` † (P11) |
| **A Skill imported before the move had a `tools:` line that named an App Tool** | This is a deliberate change of behaviour: once the move gives the folder its new `.source.json`, `SkillStore` ignores the line, so the Skill no longer grants that tool. The move's report lists each such Skill by name, so the Human can see what changed | `Report_ListsImportedSkillWhoseToolsNamedGrantableTool` † (P16); `All_FolderWithSourceJson_IgnoresToolsLine` † (P11) |
| **A fetched Skill takes a built-in Skill's name, or a name already in `Skills/` from another origin** | `S005` or `S006`, **Broken**. `S006` ignores the commit, so an update does not collide with its own copy | `S005`, `S006` in `SkillFolderChecksTests`; `Commit_Ignored_ForS006` |
| **A file looks like it holds a secret** | `X001`, **Broken**. The message shows at most four characters of the match, never the secret. Binary files are skipped; a regex that times out counts as no match | `SecretScanTests` (every test) |
| **A file is locked** (the AI tool or an editor holds it open) | An `IOException` from the reader is treated as the file being absent: the item gets an error finding *"cannot read: {reason}"* with the code of the check that needed it, and the next watcher event or poll retries. `huddle-packs` is unaffected by locks it can read around | `Resolve_FileCannotBeRead_FindingNotThrow` † (P5); `Validate_WorksWhileFilesAreLocked` |
| **A `source` id is malformed** (`..` in it, a leading `/`, a drive such as `C:`, upper case, an empty segment; a malformed or hostile file) | A `source` id is `host/owner/repo` or `local/name`: lowercase letters, digits, `.` and `-` in each segment, and `/` between segments; no `..`, no leading `/`, no drive. Anything else is `O001` (error) with *"source id is not valid"*, and the resolver reads no file for it, so a Source folder can never lie outside `sources/`. Remove Source applies the same rule | `SourceId_DotDot_O001AndNothingRead` †, `SourceId_InvalidShapes_AreO001` † (both P5); `RemoveSource_IdResolvesOutside_Refused` |
| **A `source.json` marks the Source Withdrawn** (U13) | Its Packs and Skills show a red chip and are not offered to new hires; `CardResolver` makes a `SkillSetupItem` with no prompt, so a draft stays **Needs setup**. Agents that hold it keep working and are flagged on the card, with **Remove from this Agent** | `CardResolver_WithdrawnSkill_NeedsSetup`; `AgentProfileCardPackTests` (Withdrawn held); `MemberWarningsTests` (`ForSources`) |
| **The Source is deprecated or has moved** | **Deprecated** is still choosable and shows its reason. `moved` shows the new Source and **Copy prompt** to add it | `SourcesTabTests` (deprecated, moved) |

### 12.3 What an Agent holds

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **An Agent's `_packs` names a Pack no longer in SKILLS.md** | The Agent keeps working. Its card shows a warning: *Pack "x" is no longer in SKILLS.md. {Name} keeps its Skills.* with **Remove from this Agent**. **Spec gap:** §6.13 promises this warning and §6.10.7 does not define it; task P25 adds `MemberWarnings.ForSources` the case "Pack not in registry". Not **Update available**, because there is nothing to compare | `ForSources_PackNotInRegistry_Warns` † (P25); `AgentProfileCardPackTests.PackNotInRegistry_ShowsWarning` † (P25) |
| **An Agent holds a Pack at a version the Pack no longer has** | The Pack and the Agent show **Update available** (`_packs` version differs). Nothing changes until the Human applies `G7` | `PackUpdatePlannerTests` (version change) |
| **Applying an update, and a Skill's copy was edited** | `G7` warns and the button reads **Update and replace my edits**. The pointer hash differs from `.source.json` and the copy hash differs too, so both chips show | `UpdateDialogTests` (Modified locally) |
| **Save on a card when a Skill cannot be copied** | The Agent file is not written; the card lists the Skills that are not ready with **Copy prompt**; **Cancel** copies nothing. The same holds in Build a team: **Approve & hire** waits, and `PrepareHiresAsync` stops before `CreateTeam` | `AgentProfileCardSaveTests`; `BuildTeamSetupTests` |
| **A Skill becomes ready while Build a team is open** | The page listens to `SkillRegistry.Changed`, flips the draft to Ready and enables **Approve & hire** by itself | `BuildTeamSetupTests` (Changed flips the draft) |
| **A hidden Skill that an Agent holds** | It leaves lists and pickers but stays selected on that Agent; **Undo** restores it | `HideTests` |
| **`hidden.json` is corrupt** | Reading treats it as empty, so nothing is hidden, and a warning is logged. **Hide** and **Unhide** do not overwrite it: *"hidden.json could not be read, so nothing was changed. Fix or delete it."* Remove Pack leaves it alone | `HiddenStoreTests` (malformed not overwritten); `RemovePackTests` (malformed untouched) |

### 12.4 Copying into `Skills/`

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **The source keeps changing while it is copied** | Wait for 2 quiet seconds (up to 30), hash before and after, three attempts, then *"The files for {name} kept changing…"* (`Unstable`). The staging folder is deleted; no Agent changes | `CopyAsync_SourceChangesDuringCopy_RetriesThenReportsUnstable`; `CopyAsync_RecentWrite_WaitsForQuietUpTo30Seconds` |
| **Copying fails midway (disk full, permission)** | `Io`, *"Huddle could not write {name} to Skills/: {reason}"*. The staging folder is removed in a `finally`; if it cannot be, the start-up sweep takes it. `Skills/<name>/` is untouched because the swap was not reached. The other Skills of the batch are still attempted. `CopyResult.Ok` is false, so the caller writes no Agent file | `CopyAsync_DiskFullDuringCopy_ReportsIoAndRemovesStaging` † (P13); `CopyAsync_OneOfThreeFails_OthersAreCopied_OkIsFalse` |
| **The swap fails between the two renames** | The old folder is put back at once | `CopyAsync_SwapFails_RestoresOldFolder` |
| **Huddle crashes mid-swap** | `Skills/<name>/` is missing and `Skills/.trash/<name>-*` holds the old copy. At the next start the sweep first restores every `Skills/.trash/<name>-*` whose `Skills/<name>/` is missing, then empties the rest of `.trash`, then empties `.staging` ([§6.5](#65-skillregistry-and-registrywatcher) start-up, [§6.6](#66-skillcopier-and-sourcejson)) | `StartAsync_TrashHoldsSkillAndFolderMissing_RestoresItBeforeSweep` † (P13) |
| **Huddle crashes mid-move** | No SKILLS.md, so the move runs again. `.move-*` folders older than 10 minutes are deleted whether or not SKILLS.md exists; a younger one is kept (another Huddle may be running). Files already placed are overwritten by the same bytes. SKILLS.md is the last `Place` and uses `overwrite: false` | `RunAsync_CrashWhilePlacing_LeavesNoSkillsMd`; `RunAsync_AfterCrash_NextRunCompletesWithSameTree`; `RunAsync_OrphanStagingOlderThanTenMinutes_IsDeleted` |
| **The move runs again after SKILLS.md was deleted by hand** | It rewrites the six catalogs and the Sources' notes. **Spec risk:** a catalog the AI tool had since filled with `path` values is overwritten with empty paths, because `Place` uses `overwrite: true`. Task P16 decides: before overwriting a catalog file that exists, keep the existing file's `path` values for entries of the same `name`, and note the merge in `log.jsonl` | `RunAsync_ExistingCatalogWithPaths_KeepsPathsOnRerun` † (P16) |
| **Two requests copy the same Skill** | One `SemaphoreSlim` serialises batches; the second finds `AlreadyCurrent` and writes nothing | `CopyAsync_TwoBatchesSameSkill_SecondIsAlreadyCurrent` |
| **A folder in `Skills/` has no `.source.json`** | It is the Human's own Skill or a built-in override. It is never replaced (`NameTaken`). If an AI tool wrote a folder straight into `Skills/` in breach of `InstallSkillPack.md` §2, Huddle treats it as the Human's own Skill: it must pass `SkillValidator`, and its `tools:` line **is** honoured, because the rule that ignores `tools:` keys on `.source.json`. This is the one way past `S011`; the Human wrote or allowed it | `CopyAsync_ExistingFolderWithoutSourceJson_IsNameTakenAndUntouched`; `All_FolderWithoutSourceJson_IsYours` † (P11) |
| **A `Skills/` entry starting with `.`** (`.staging`, `.trash`) | Never read as a Skill | `ScanDisk_DotFolders_AreSkipped` † (P11) |

### 12.5 Watching

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **33 or more watch roots** | The Skill Sources folder, the pack-file folders and the Skill watch roots together create at most **32** watchers. Roots under another root are dropped first; the rest are polled every 30 seconds by newest write time and file count | `WatchRoots_33Roots_CreatesAtMost32AndPollsTheRest` † (P12); `WatchRoots_NestedRoots_Collapse` † (P12) |
| **A root is on a network drive or UNC share** | `FileSystemWatcher` misses events there, so the root is polled, not watched. A change shows within 30 seconds, not 1.5 | `WatchRoots_NetworkRoot_IsPolled` † (P12) |
| **A watcher raises `Error`** (buffer overflow, removed drive) | It is disposed, its root moves to polling and the next quiet period triggers a full re-read | `Watcher_Error_MovesRootToPollingAndFullReread` † (P12) |
| **A rebuild throws** (a bug, or `UnauthorizedAccessException` on SKILLS.md) | Logged; the previous snapshot stays; the next event retries | `Rebuild_Throws_KeepsPreviousSnapshot` † (P12) |
| **A subscriber of `Changed` throws** | Caught per handler and logged; the others still run | `Changed_SubscriberThrows_OthersStillCalled` |
| **Many events at once** (a clone checkout) | Requests merge into one follow-up whose scope is the union; the hash cache skips any folder whose newest write time and count are unchanged | `Rebuild_BurstOfScopes_MergesIntoOneFollowUp` † (P12) |

### 12.6 `huddle-packs` and the context file

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **`install-context.json` is missing** when `huddle-packs` runs | Exit 2 on standard error: *install-context.json not found in {dataDir}. Start Huddle once so it writes install-context.json, or pass --data-dir.* Nothing is validated and no summary line is printed | `MissingFile_Exit2_NamesStartHuddle` |
| **The context file is not JSON, lacks a field, or has a higher `schemaVersion`** | Exit 2 with the matching message of [§6.4](#64-the-huddle-packs-command) | `InvalidJson_Exit2`; `NewerSchemaVersion_Exit2` |
| **`huddle-packs` and Huddle differ in version** | `X002` (warning) on every run; exit code unchanged. Huddle itself passes no `ToolVersion`, so it never raises `X002` | `ContextFromOtherVersion_PrintsX002Warning_Exit0`; `X002_NullToolVersion_Skipped` |
| **SKILLS.md or the Skill Sources folder is missing** | Not exit 2: `R001` (error), the summary line, exit 1 | `MissingSkillsMd_R001_Exit1` |
| **`validate <path>` matches nothing, or the path does not exist** | Exit 2 with *No row in SKILLS.md points at or under {path}.* or *Path not found: {path}* | `PathMatchesNothing_Exit2`; `PathMissing_Exit2` |
| **Huddle is not running, or is running on the same folder** | Same findings either way; `validate` never writes | `Validate_NeverWritesFiles`; `Validate_GivesSameFindings_AsChecksRun` |
| **`InstallSkillPack.md` cannot be written** (the AI tool holds it open, `{DataDir}` read-only) | A warning is logged, the old files stay, and the write retries after 5 seconds, up to 5 times in a row, then waits for the next change. A temp file left by a failure is deleted | `Write_FileLocked_LogsRetriesAndKeepsOldFile`; `Write_TempFile_NeverRemainsAfterFailure` |
| **No `huddle-packs` beside the app** | The install file still carries the command line (`dotnet "<dll>"` when the path ends in `.dll`); the Sources tab shows it, so the Human sees what is missing | `Write_HuddlePacksPathDll_UsesDotnet` |

### 12.7 Removing and the Library

| Situation | What happens | Pinned by |
| --- | --- | --- |
| **Remove a Pack whose file is outside the Skill Sources folder** | The row goes; the file stays; the dialog says so | `RemovePackTests` (file outside) |
| **An Agent write fails during Remove** | The flow stops before SKILLS.md is touched; running it again is safe | `RemovePackTests` (Agent write failure) |
| **A file delete fails after the row is gone** | *"Removed the row, but could not delete {file}. Delete it yourself."* An orphan file is ignored | `RemovePackTests` (file-delete failure) |
| **Remove a catalog that Packs use** | The dialog names the Packs; they show **Broken** (`P009`) until the AI tool fixes them; `hidden.json` is not pruned | `RemoveCatalogTests` |
| **A write reaches the Skill Sources folder through the Library** (a pinned root, a junction) | `LibraryPath.ReadOnly` is true when the resolved path lies inside any read-only root; every write returns the refusal and touches no file | `LibraryPathResolverReadOnlyTests`; `LibraryFileServiceReadOnlyTests` |
| **The Skill Sources folder is hidden or missing in Settings › Library** | The root stays listed and resolves; a listing fails with the existing "isn't available" message | `LibraryRootStoreSkillSourcesTests` |

### 12.8 Limits that do not need a test of their own

| Limit | Value | Where it is enforced |
| --- | --- | --- |
| Files per Skill, size per Skill | 200 files, 5 MB (`S003`) | `SkillFolderChecks` |
| `SKILL.md` description | Error over 500, warning over 300 | `S002`, `S012` |
| A `.md` file Agents read | 64 KB (`S007` warns) | `S007` |
| `NOTES.md` | 16 KB (`O004` warns) | `O004` |
| Findings in a fix prompt | 20, 300 characters each | `SkillPromptBuilder.Fix` |
| Appendix rows in `InstallSkillPack.md` | A 200, B 300, C 100, D 100; the context file has no cap | `InstallFileWriter` |
| Install file body | 9,600 bytes raw; 20,480 bytes for the rendered fixed part with 260-character paths | `InstallBodyDefaultTests` |
| Watchers | 32, then the 30-second poll | `RegistryWatcher` |

---

## 13. End-to-end Flow

Two walks through the whole system. Each step names the component and the file it touches. A
file is **read** (R) or **written** (W); `{DS}` is `{DataDir}`, `{SS}` the Skill Sources folder.

### 13.1 U1: the Human adds Document skills with Claude Code

The Human has never added anything. Huddle has started once, the move has run, and
`InstallFileWriter` has written `{DS}/InstallSkillPack.md` and `{DS}/install-context.json`.

| # | Who | Component | What happens | Files |
| --- | --- | --- | --- | --- |
| 1 | Human | `PacksTab` / `PackEmptyState` (`G4d`) | Selects **Add with your AI tool**. `AddWithAiDialog` (`G5`) opens on task `add`. It writes nothing | none |
| 2 | Human | `AddWithAiDialog`, `SkillPromptBuilder.Add`, `PathFormatChoice`, `PathFormats` | Types `https://github.com/anthropics/skills` in **Where are the Skills?** and a purpose. `Add` renders the key `skillPrompt.add` through `PromptStore`, with the install file's path converted to the chosen format and quoted if it holds a space. The prompt box shows it | R `prompts.default.json` or the Human's override in the prompts store |
| 3 | Human | `PromptClipboard` | **Copy prompt** puts the text on the clipboard; snackbar *Prompt copied. Paste it into your AI tool.* Huddle sends it nowhere | none |
| 4 | Human, then Claude Code | outside Huddle | Pastes the prompt. The tool reads `InstallSkillPack.md`: the front matter (`validate:` command, `computer: DESK-01`), the 11 sections, appendix A roles, B installed Skills, C the SKILLS.md rows, D catalogs | R `{DS}/InstallSkillPack.md` |
| 5 | Claude Code | outside Huddle | Asks about sign-in (none needed), clones `anthropics/skills` to `E:\Clones\anthropics-skills` pinned to a commit. Writes, each through a temporary name and a rename | W `{SS}/sources/github.com/anthropics/skills/source.json`, `NOTES.md`; W `{SS}/packs/document-skills.skillpack.json` |
| 6 | Claude Code | `huddle-packs validate packs/document-skills.skillpack.json --strict` | `Program.Run` parses the arguments (`ArgumentParser`), finds the data folder (`DataLocator`), reads the context file (`InstallContext.Parse`, then `CheckContext.From`), reads SKILLS.md with `RegistryReader.Load`, adds a **virtual row** for the file no row points at yet, resolves it (`RegistryResolver.Resolve`), runs `Checks.Run` with `Strict`, and prints lines and the `PACKS-VALIDATE` summary. The first run says `ERROR P005 … "accountant" is not a role`; the tool fixes `appliesTo` and runs again until `Errors=0`. It writes nothing | R `{DS}/install-context.json`, `{SS}/SKILLS.md`, the pack file, `source.json`, `NOTES.md`, the four Skill folders |
| 7 | Claude Code | outside Huddle | Adds one Packs row (`Local path`, `Origin URL`, `Last updated`, `Notes`) at the end of the table, through a rename. Runs `validate` once more with no path | W `{SS}/SKILLS.md` |
| 8 | Huddle | `DebouncedFileWatcher` (500 ms) in `RegistryWatcher` | The recursive watcher on `{SS}` sees the writes. When the queue goes quiet, the changed paths become a `RebuildScope`: SKILLS.md changed, so everything is re-read, and the watch roots are rebuilt because a new Skill folder resolved outside `{SS}` (one root, `…\anthropics-skills\skills\`) | none |
| 9 | Huddle | `SkillRegistry.RebuildAsync` | Reads SKILLS.md (length first; over 1 MB is `R011`) and calls `RegistryReader.Read`. Builds the `CheckContext` from `RoleLister`, `SkillStore.All`, `SkillGrants.Grantable`. `RegistryResolver.Resolve` loads the pack file (`PackFile.Parse`), `source.json` (`SourceFile.Parse`) and `NOTES.md`; hashes the four Skill folders on the thread pool (`SkillFolderHash`, cached by write time and count); `Checks.Run` returns the findings and states. Reads `hidden.json` and `seen`, counts holders, and finds the row key in `NewKeys` and `AppearedKeys`. Publishes the snapshot and raises `Changed` | R SKILLS.md, pack file, `source.json`, `NOTES.md`, four Skill folders, `{SS}/hidden.json`, `{DS}/skill-sources.json` |
| 10 | Huddle | `SourceLog.Append` | Writes the `row-appeared` line: *Document skills dbd4588, 4 Skills, ready to use* | W `{SS}/sources/github.com/anthropics/skills/log.jsonl` |
| 11 | Huddle | `InstallFileWriter.RequestWrite` | Within one second, writes the context file and then `InstallSkillPack.md`, whose appendix C now lists the row. Skipped if the content, ignoring `written`, is the same | W `{DS}/install-context.json`, `{DS}/InstallSkillPack.md` |
| 12 | Huddle | `SkillsPanel`, `PacksTab`, `PackBar`, `PackCard`; `SkillArrivalWatcher` | Subscribers repaint through `InvokeAsync`. The new `PackCard` wears **New** (`DisplayState`: Available, nobody holds it, key in `NewKeys`). The status line reads *1 Source · 1 new · …*. `SkillArrivalWatcher` sees the Pack go from absent to usable and shows *Document skills is ready* with **Show** | none |
| 13 | Human | `NavigationManager` | **Show** goes to `/settings/skills?view=packs&highlight=document-skills`; the card scrolls into view and flashes (`.search-hit-flash`). The whole path from step 7 takes under 1.5 seconds | none |
| 14 | Human | `PacksTab` | Leaves the tab. `SkillRegistry.MarkSeenAsync` merges the keys into `seen` through `SkillSourcesSettings` (temporary file and a move), keeping `dir`. The chip is gone next time | W `{DS}/skill-sources.json` |
| 15 | Human | `AgentProfileCard` of a bookkeeper | The card's Pack chips come from `PacksForRole("bookkeeper")` and now include **Document skills** (U2 starts here: toggling it edits the card in memory; **Save** runs `ISkillCopier.EnsureAvailableAsync`, which copies the four Skills into `Skills/` through `.staging`, then `PersonaStore.Update` writes `skills:`, `consult_when:` and `_packs:`, and the Agent restarts) | W `{DS}/Skills/<name>/…`, `.source.json`; W the Agent's `.md` |

A failure at any step leaves something re-runnable. A pack file that fails to parse inside the
2-second window is read again, not reported (step 9). A Pack with a missing Skill shows **Not
downloaded** with **Copy fix prompt**, and the fix is the same loop from step 4.

### 13.2 U4: the Human applies the update

Precondition: the Human has given Document skills to Jane and Omar; their Skills are in
`Skills/` with `.source.json`.

1. **The AI tool pulls.** Claude Code runs a check for updates (the `skillPrompt.update` prompt
   from `G5`, task **Check for updates**) or the Human pulls by hand. Files in the clone change;
   it rewrites the pack's `version`, `source.json` and the row's `Last updated`.
2. **Huddle notices.** The Skill watch root sees the clone's changes; the watcher turns them
   into `RebuildScope` with the touched Skill folders and the changed pack file.
   `SkillRegistry.RebuildAsync` re-hashes only those folders. A Skill whose `PointerSha256`
   differs from `InstalledSha256` is `UpdateAvailable`, and so are its Pack and the holders'
   cards. **No Agent file and no copy in `Skills/` has changed** (U3).
3. **The Human reviews.** **Review update** opens `UpdateDialog` (`G7`). `PackUpdatePlanner.Plan`
   reads `Current`, `HolderNames` and the Agent files and returns a `PackUpdatePlan`:
   `FileChanges` (shared, no checkboxes, with the affected Agents), `Added` and `Removed`
   Skills, the `consult_when` change, and one `AgentChange` per Agent whose membership
   changes. A Skill whose copy was edited adds the warning and changes the button to **Update
   and replace my edits**.
4. **Files first.** **Update 2** calls `ISkillCopier.CopyAsync` for every changed and every
   added Skill. Per Skill: wait for quiet, hash source, copy to `Skills/.staging/<name>-<random>`,
   hash again, run the S checks on the copy, write `.source.json`, rename the old folder to
   `.trash`, move the staging folder in, delete the trash. One failure stops here and writes
   nothing else; the dialog shows the message.
5. **Then Agents.** For each checked Agent, `PackMembership` rewrites `skills:`,
   `consult_when:` and `_packs:`; `PersonaStore.Update` writes the file and the Agent restarts.
   Unchecked Agents keep their membership. An Agent whose Skill only changed files is
   not restarted: Skill bodies are read live.
6. **Everything settles.** `SkillStore` raises `SkillsChanged` (500 ms); `SkillRegistry`
   rebuilds the touched names, `InstalledSha256` now equals `PointerSha256`, the chips leave
   **Update available**, holders update to **In use by 2**. `SourceLog` writes one `copied` line
   per Skill and one `update-applied` line for the Pack; `InstallFileWriter` refreshes both
   install files.

| Step | Component | Files |
| --- | --- | --- |
| 2 | `RegistryWatcher`, `SkillRegistry`, `SkillFolderHash` | R clone folders, pack file |
| 3 | `PackUpdatePlanner`, `UpdateDialog` | R Agent files |
| 4 | `SkillCopier` | W `Skills/.staging`, `Skills/<name>/`, `.source.json`; `.trash` emptied |
| 5 | `PackMembership`, `PersonaStore`, `PersonaSupervisor` | W Agent `.md` files |
| 6 | `SourceLog`, `InstallFileWriter` | W `log.jsonl`, `InstallSkillPack.md`, `install-context.json` |

---

## 14. Design Notes / Rationale

The product decisions are the vision's 17 ([Decisions so far](Huddle.SkillPacksSpec.md#decisions-so-far)).
These are the design decisions this spec adds, each with the alternative it beat.

| # | Decision | Alternative rejected | Why |
| --- | --- | --- | --- |
| D-1 | Parse SKILLS.md with Markdig, finding tables by their level-2 heading | A line-by-line regex parser | Markdig already ships with the Library and handles escapes, code spans and alignment rows; a hand parser would disagree with how the file renders |
| D-2 | A computer entry needs a 2-to-15-character name followed by `": "` | Splitting on the first colon; a different separator such as `=>` | A drive colon is followed by `\`, never a space, and drive letters are one character. Keeping `NAME: path` keeps the cell readable |
| D-3 | JSON `path` is a string or an object of computer names, with `"*"` for the rest | Always an object; a separate `paths` field | Most Skills have one path. A string stays the common case, and the object form is the same idea as in SKILLS.md |
| D-4 | Reuse `SkillFolderHash` over the whole folder, skipping `.git` | Hash only the top-level `.md` files Agents read | The copy holds the whole folder, so the hash must cover it, or a copy could differ from its source unnoticed. A change in `scripts/` showing **Update available** is accurate |
| D-5 | One shared copy per Skill; `G7` lists file-change holders without checkboxes | A copy per Agent | `Skills/<name>/` is flat and shared today, and `read_skill` reads it live. A per-Agent copy would change `SkillStore`, `read_skill` and every Persona's resolution for little gain |
| D-6 | Watch roots, capped at 32, with a 30-second poll for the rest | One watcher per Skill folder; polling only | 263 catalog Skills could mean 263 watchers. Polling alone makes a new row take up to 30 s to appear |
| D-7 | Do not report a parse failure on a file written in the last 2 s | Requiring the AI tool to write atomically | Huddle cannot enforce what an AI tool does; asking for atomic writes in `InstallSkillPack.md` helps but cannot be relied on |
| D-8 | Copy through a staging folder, hashing source before and after, then swap | Copying in place; locking the source | A half-copied Skill must never be visible to `SkillStore`, and Huddle cannot lock files another process writes |
| D-9 | Catalog ids unique across the Catalogs table; the first row wins | Qualifying references with the row's path | Pack files must work on every computer; an id does, a path does not |
| D-10 | Skill names unique across the whole registry | Namespacing by Source | `Skills/<name>/` and `skills:` lines are flat. Namespacing would change Persona files and the Skill Index |
| D-11 | A Pack is chosen whole; one missing Skill makes it **Not downloaded** | Choosing what is there and leaving the rest | `_packs` records a version; an Agent holding part of a Pack at that version would read as up to date when it is not |
| D-12 | `huddle-packs` reads a context file Huddle writes | Referencing `Huddle.App` to read the hiring pool and Skills | `Huddle.App` is a web app; referencing it would make the command large and slow, and tie it to ASP.NET |
| D-13 | Framework-dependent, beside the app, not on `PATH` | Self-contained; a global tool | The runtime is already a requirement of Huddle, and `InstallSkillPack.md` gives the full command line |
| D-14 | `Team:SkillsRepositoryDir` read only by the move; empty means no rows | A permanent setting on the Sources tab; writing rows with no usable path | After the move the repository is a Source like any other, kept up to date by the AI tool. A row with no path for this computer would be an error, not a pointer |
| D-15 | The Skill Sources folder set on the Sources tab persists in `skill-sources.json` | Config only | The Human changes it while sharing; editing `appsettings.json` is not a UI. Matches `library-roots.json` |
| D-16 | **New** is tracked per Huddle, not in the shared folder | In `hidden.json` beside the hidden items | **New** means "since you last looked here"; another Huddle looking must not clear it |
| D-17 | Huddle edits SKILLS.md only by deleting a row's own lines | Rewriting the table on every change | People and AI tools add their own notes and columns; Huddle must not reformat what it does not own |
| D-18 | A role's Pack id that no row provides is skipped on a card, as today; `R008` warns | **Needs setup** for it | A stock install with no skills repository must still hire a developer. **Needs setup** is for what is listed but not downloaded |
| D-19 | The move drops the later of two catalog entries with the same name: 263 Skills, not 265 | Writing both | `retro` and `test-driven-development` appear twice in `SkillsCatalog.json`; writing both would make a fresh install show `D003` |
| D-20 | `S011` reverses today's rule: a Grantable App Tool in a fetched Skill is an error, and `SkillStore` ignores `tools:` on any folder with a `.source.json` | Today's warning | Vision decision 15. The error stops it at the pointer; ignoring it in `SkillStore` stops a hand-edited copy |
| D-21 | `validate <path>` accepts a pack or catalog file no row points at yet | Requiring the row first | The AI tool validates before it adds the row, so a failed check never reaches SKILLS.md |
| D-22 | Intros end with **Learn more**, an in-app dialog | A link to the User Guide | Huddle serves no documentation; the dialog's text comes from the User Guide section |
| D-23 | A Source id is `host/owner/repo` or `local/name` with no `..`, no leading `/` and no drive (`O001`) | Any string | The id becomes a folder under `sources/`; a free-form id could point outside it |
| D-24 | A folder put straight into `Skills/` with no `.source.json` stays the Human's own Skill, `tools:` included | Treating every folder in `Skills/` as fetched | Huddle cannot tell who wrote a folder; `InstallSkillPack.md` forbids the AI tool to write there. A known limit, in §12 |

---

## Appendix A — Test-first task plan

Every implementation task is preceded by the tests that specify it, and the two are worked **as
vertical pairs**: write one test, see it fail for the right reason, write the least code that
passes, then take the next pair. Never write a task's tests in bulk. Test names follow
`Method_Scenario_Expectation`; every test has a `///` summary, at least one `Assert`, and passes
`TestContext.Current.CancellationToken` (`agents/CSharpPrinciples.md`). Prove a new test can fail
before trusting it (`agents/Testing.md`). A name ending in † is one that [§12](#12-edge-cases-and-failure-modes)
adds; the task that lists it writes it.

Run after every pair:

```powershell
dotnet build Huddle.slnx
dotnet test  Huddle.slnx --
```

Run before a task is marked done: `agents/scripts/Check-All.ps1`. It includes `Check-DocIndex.ps1`,
which fails when a page under `docs/` is added, renamed or deleted without a change to
`docs/Index.md` in the same commit, and `Check-TestDocs.ps1`, which wants a `///` summary on every
added test. Build the **solution**, never one project: the test projects carry their own analyzers.

**Columns.** *Depends on* names tasks that must be done first. *Failing tests first* names the
classes and tests that go red first, taken from the section that owns the behaviour. *Done when*
is the check that closes the task. A task is never closed with a red build, a skipped test or a
suppression that has no reason.

### A.1 Streams and what runs in parallel

| Stream | Letter | Tasks | Needs before it starts |
| --- | --- | --- | --- |
| Decision and contracts | 0 | P0, P1 | Nothing. **Serial**: nothing else starts until P1 is merged |
| Library | L | P2 to P7 | P1. P2, P3 and P4 run in parallel; P5 needs all three; P6 and P7 need P5 and run in parallel |
| Command | C | P8, P9 | P1. P8 runs beside L; P9 pairs 1 to 5 start when P1 and P8 are done, the rest wait for P7 |
| App services | A | P10 to P18, P22 | P1. P10 and P11 start at once; P12 needs L complete; P13 to P18 need P12 (P16 also P10 and P11) |
| UI | U | P19 to P21, P23 to P25 | P1 for the frozen types; each task builds against a `SnapshotBuilder` test helper (P12) so UI work does not wait for the watcher. A UI task closes only after P12 is merged |
| Removals | R | P26 | Every replacement it names |
| Docs | D | P27 to P31 | See each task; P27 and P28 start after P0 |
| Final | F | P32 | All others |

```text
P0 ─► P1 ─┬─► L: P2, P3, P4 ─► P5 ─► P6, P7 ─┐
          ├─► C: P8 ─► P9 (pairs 6+ after P7) ┤
          ├─► A: P10, P11 ───────────────────►┼─► P12 ─► P13, P14, P15, P16, P17, P18
          │                                   │              │
          └─► U: P19, P20, P21 (on SnapshotBuilder) ─► P22, P23, P24, P25 ─► P26 ─► P32
D: P27, P28 (after P0) · P29, P30 (with the UI) · P31 (last)
```

**Shared files.** `Huddle.slnx`, `Directory.Packages.props` and `CLAUDE.md` are shared root files.
Their changes are announced before they are made; the announcement is a step of P1, not an
afterthought.

### A.2 Stream 0 — Decision and contracts

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P0 | **The successor ADR.** Write `docs/adr/0059-skills-and-packs-are-fetched-by-the-humans-ai-tool.md` (numbered 0058 when written; renumbered 0059 on 2026-10-18 because a Theme ADR took 0058). It marks [ADR-0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md) "Partly superseded by 0059": pinning, hashing and review stay; the download goes. It decides, one clause each: (1) Huddle makes no network call for Skills, and builds no `HttpClient` for them; (2) the Human's AI tool fetches, guided by `InstallSkillPack.md`, and records pointers in SKILLS.md; (3) Huddle reads pointers and checks what they lead to (`huddle-packs` and Huddle run the same checks); (4) there is no approval step, the checks are the gate; (5) an Agent uses a copy in `Skills/`, never a clone, and the copy is made before the Agent file changes; (6) only built-in Skills grant App Tools (`S011`); (7) one copy per Skill name, shared by every holder, so a file update reaches all holders at once and membership changes are per Agent (§8.4); (8) the first row wins every tie; (9) Huddle deletes only inside the Skill Sources folder and `Skills/.staging` and `.trash`; (10) the shipped Packs move to the skills repository and the shipped catalog is split by the one-time move, 263 Skills, not 265; (11) the Skill Sources folder is a read-only Library root, which amends [ADR-0027](adr/0027-the-library-sees-only-configured-roots.md); (12) `Team:SkillsRepositoryDir` is a configuration key read once by the move, not a UI setting; (13) the prompts the Human pastes are configuration ([ADR-0007](adr/0007-model-facing-text-is-configuration.md)). Edit ADR-0043's status line in the same commit | None | None. A document: reviewed by the owner of the vision before any other task starts | ADR merged with status `accepted`; `docs/Index.md` has its row and `Check-DocIndex.ps1` passes; 0043's status line points at it |
| P1 | **Projects and frozen contracts** ([§5.3](#53-contracts-fixed-before-any-stream-starts)). **Announce first**: adding `src/Huddle.SkillPacks` and `src/Huddle.Packs` to `Huddle.slnx` and the two rows in the `CLAUDE.md` "Two projects, one build root" table are shared root changes. Then: create both projects (`Directory.Build.props` applies; Markdig is already a `PackageVersion`, so `Directory.Packages.props` should not change; if it must, announce that too); add `ProjectReference`s from `Huddle.App` and `tests/Huddle.Tests`; write the types of §6.1 and §6.2 as compiling records, enums and signatures whose bodies throw `NotImplementedException` (`Finding`, `Severity`, `CheckContext`, `RoleInfo`, `InstalledSkill`, `CheckOptions`, `CheckResult`, `IFileReader`, `Registry`, `RegistryRow`, `PathEntries`, the three file records, `ResolvedRegistry`, `PackEntry`, `SkillEntry`, `ItemState`); write `CheckCatalog` with all codes of §6.3 (R001 to X002), their severities, titles and fix hints, and the `Retired` list; write the App seams `ISkillCopier`, `ISkillAvailability`, `SkillAvailability`, `SkillSetupItem`, `RegistrySnapshot`, `RegistryChange`, `RebuildScope` and a `SkillRegistry` whose members throw. No separate test project: the suite lives in `tests/Huddle.Tests/SkillPacks/` | P0 | `CheckCatalogTests`: `All_Codes_MatchPinnedList`, `Retired_Codes_AreNeverInAll`, `All_Entries_HaveHintAndTitle`, `Codes_AreUniqueAndWellFormed`. A reflection test, `Library_ReferencesOnlyMarkdig` †, fails when `Huddle.SkillPacks` references `Huddle.App`, `Huddle.Contracts` or an ASP.NET assembly | `dotnet build Huddle.slnx` green with both projects; the four tests green; the announcement is in the PR description and in the project's tracker; `Check-All.ps1` green. **Contracts are frozen**: a later change to a name in §5.3 needs the owner's yes |

### A.3 Stream L — The library

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P2 | **Reader and paths** ([§6.2](#62-registry-model-and-file-readers), [§8.1](#81-reading-skillsmd), [§8.2](#82-per-computer-paths)): `RegistryReader`, `Registry`, `RegistryRow`, `LineSpan`, `PathEntries`, `PathEntry`. Also the test helpers `InMemoryFileReader` (an `IFileReader` over a dictionary) and `RegistryTreeBuilder` (writes a Skill Sources tree to a temp folder) | P1 | `RegistryReaderTests`: `Read_BothTables_ReturnsRowsInFileOrder`, `Read_ColumnsInAnyOrder_MapsByHeader`, `Read_PathCellWithBr_SplitsIntoEntries`, `Read_CodeSpanPath_KeepsSpaces`, `Read_RowSpan_CoversOnlyTheRowLines`, `Read_EmptyText_R001` †, `Read_BinaryGarbage_R001AndNoThrow` †, `Read_TableWithoutLocalPath_R002` †, `Read_OverOneMegabyte_R011AndNoRows` †, `Read_SecondPacksHeading_R012`. `PathEntriesTests`: `Resolve_ComputerEntry_BeatsPlainEntry`, `Resolve_OtherComputerOnly_ReturnsNull`, `Resolve_DriveLetterPath_IsNotAComputerEntry`, `Parse_OneLetterComputerLine_IsPlainEntryAndR003` †, `FromJson_ObjectWithStar_IsPlainEntry`, `FromJson_OneLetterComputerKey_Resolves` †, `HasDuplicateComputer_R010`, `HasTwoPlainEntries_R013` | Tests green; the reader never throws on any text (a property-style test feeds 1,000 random strings); `ResolveFull` accepts `/` and `\` |
| P3 | **File schemas** ([§6.2](#62-registry-model-and-file-readers), [§7.2](#72-schemas)): `PackFile.Parse`, `CatalogFile.Parse`, `SourceFile.Parse`, `ParseResult<T>`, `PackSkill`, `CatalogEntry`; the shape findings `P001` to `P003`, `P008`, `P009` (shape part), `D001`, `D002`, `O001`, `O003`; the three JSON Schema files embedded in the library | P1 | `PackFileTests`: `Parse_PackWithUnknownSchemaVersion_IsP001AndNull`, `Parse_PackSkillEmptyPath_IsValidAndNotDownloaded`, `Parse_PackSkillWithPathAndCatalog_IsP009`. `CatalogFileTests`: `Parse_CatalogBadEntry_DropsOnlyThatEntry`. `SourceFileTests`: `Parse_SourceWithoutRefCommitLocalPath_IsValid`, `Parse_StatusWithdrawnWithoutReason_IsO003`. `SchemaFilesTests`: each schema parses as JSON; every example in §7.2 validates against it; every rule the reader enforces is in the schema (the "step" test of §6.4). Unknown keys and a BOM are accepted | Tests green; the three schemas are embedded resources `Agency.Huddle.SkillPacks.Schemas.*.schema.json` |
| P4 | **Move the folder code** ([§6.3](#63-the-checks), [§6.14](#614-code-that-goes)): `git mv` `SkillFolderHash`, `SkillValidator`, `SkillValidation`, `SkillReviewScanner` and their three test files into the library and `tests/Huddle.Tests/SkillPacks/`; `SkillFolderHash` skips `.git`; add `SkillFrontmatter.Parse` and `SkillFolderRules` (200 files, 5 MB, reparse points, Windows names). `Huddle.App` is updated to reference the new namespace and nothing else changes | P1 | `SkillFolderHashTests`: `ComputeFolder_GitFolder_IsIgnored`. `SkillValidatorParityTests`: `SkillFrontmatter_Agrees_WithPersonaFrontmatter`. `SkillFolderRulesTests`: `TooManyFiles_Reported`, `Junction_Reported`, `ReservedDeviceName_Reported`, `CaseOnlyDuplicate_Reported` | The moved tests are green **unchanged** except for the namespace; the new ones green; `git log --follow` shows the rename |
| P5 | **Resolver** ([§6.2](#62-registry-model-and-file-readers), [§8.3](#83-resolving-packs-catalogs-and-skills), [§8.4](#84-noticing-an-update)): `RegistryResolver.Resolve(registry, files, context)`, the resolved records, states, the first-row-wins rules, pointer hash and `InstalledSha256`, the 2-second retry rule | P2, P3, P4 | `RegistryResolverTests`: `Resolve_SameNameDifferentOrigin_FirstRowWins_LaterIsBroken`, `Resolve_PackSkillByCatalog_TakesCatalogPathAndOrigin`, `Resolve_LibraryNeverProducesNewOrInUse`, `Resolve_SameInputTwice_IsIdentical`, `Resolve_PointerHashDiffersFromInstalled_IsUpdateAvailable`, `Resolve_PackWithOneMissingSkill_IsNotDownloaded`, `Resolve_ParseFailureWithinTwoSeconds_NotReportedYet` †, `Resolve_ParseFailureAfterRetry_IsBroken` †, `Read_FiveThousandEntryCatalog_UnderOneSecond` † | Tests green; no allocation of a body of any file in a `ResolvedRegistry` (a memory test sets the §11 budget) |
| P6 | **Checks A: registry, Pack, catalog and Source codes** ([§6.3](#63-the-checks)): `Checks.Run` for `R001` to `R013`, `P004` to `P007`, `D003` to `D005`, `O002` to `O005`, `X002`; `Strict`; `OnlyPath`; ordering; `CheckResult` (the only place states are computed), `RowCheck`, `CheckSummary` | P5 | `RegistryChecksTests`: one raise and one clear test per code (`R003_EmptyPath_Raised`, `R003_FileOnMissingDrive_Broken` †, `R009_NoEntryForThisComputer_NotDownloaded`, `P009_PathAndCatalog_Raised`, `D005_SecondCatalogSameId_FirstWins`). `StrictTests`: `Strict_R009AndS001_BecomeErrors`, `Strict_NothingElseChanges`, `EmptyCatalogPath_NoFinding_EvenStrict`. `OrderingTests`: `Findings_AreSortedByRowThenFileThenCode`, `SameInputs_GiveIdenticalOutput`, `SharedSkill_ReportedOnceOnFirstRow`. `ContextTests`: `X002_DifferentVersions_Warns`, `X002_NullToolVersion_Skipped`, `R008_NoSuggestedPacks_NeverRaised`, `O002_SourceWithoutCommit_RaisedOnlyWhenAnEntryFailsD004`. Also `Run_FileCannotBeRead_FindingNotThrow` † and `SourceId_DotDot_O001AndNothingRead` † | Tests green; `Run` honours the `CancellationToken` (a cancelled token throws, with no partial result) |
| P7 | **Checks B: Skill-folder codes and secrets** ([§6.3](#63-the-checks)): `S001` to `S012`, `X001` with the ten patterns, `OriginIdentity` for `S006`, the S009 licence rule, the mapping of the old scanner flags to `S009` and `S010` | P5 | `SkillFolderChecksTests`: each of S001 to S012 raised and not raised; `S011_ProposeSprint_Raised`, `S011_ReadSkill_NotRaised`, `S011_NameOutsideAppToolNames_NeverApplies`. `SecretScanTests`: one positive and one negative per pattern, `Password_Placeholder_Passes`, `Finding_NeverContainsSecret`, `BinaryFile_Skipped`, `RegexTimeout_CountsAsNoMatch`. `OriginIdentityTests`: `Commit_Ignored_ForS006`, `NonUrl_TrimmedOnly` | Tests green; every regex is `[GeneratedRegex]` with a 1-second timeout |

### A.4 Stream C — The command

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P8 | **Role list and context file** ([§6.7](#67-installskillpackmd-and-the-context-file)): `RoleLister` in `Huddle.App`, and `InstallContext.Parse`, `ToJson` and `CheckContext.From` in the library, with the one `JsonSerializerOptions` | P1 | `RoleListerTests`: `RoleLister_SortedByIdOrdinal_AndDeduplicated`, `Write_Roles_UnionsCustomPoolAndTeamTypes` (the first half). `ContextFileTests`: `RoundTrip_WriteThenRead_Equal`, `OptionalSuggestedPacks_Absent_Reads`, `MissingRequiredField_ReportsWhichOne`, `NewerSchemaVersion_Refused` | Tests green; the type is shared, so the writer and the command cannot disagree on a key |
| P9 | **The `huddle-packs` program and how it ships** ([§6.4](#64-the-huddle-packs-command), [§8.9](#89-shipping-huddle-packs)): `Program.Run`, `ArgumentParser`, `DataLocator`, `ValidateCommand` with path filtering and virtual rows, `RolesCommand`, `InstalledCommand`, `SkillsCommand`, `SchemaCommand`, `FindingWriter`, `HelpText`, `--version`. Order of pairs: parser, locator, context errors, schema, roles, installed (all need P1 and P8 only); then `validate` output, exit codes, JSON, path filter, strict, version mismatch and `skills` (need P7). Last pair: `Huddle.App` references the project and the apphost lands beside the app | P1, P8; pairs after the schema pair wait for P7 | `ArgumentParserTests`, `DataLocatorTests`, `ContextFileTests`, `ValidateOutputTests`, `ValidateExitCodeTests`, `ValidateJsonTests`, `ValidatePathFilterTests`, `ValidateStrictTests`, `VersionMismatchTests`, `ListCommandTests`, `SchemaCommandTests`, `ParityTests` ([§6.4](#64-the-huddle-packs-command) lists every test). Then `AppOutput_ContainsHuddlePacksApphost` † (reads the build output). The edge-case tests of [§12.6](#126-huddle-packs-and-the-context-file) are among them | Tests green; `huddle-packs.exe --version` runs from the app's output folder; if the apphost is not copied, a `Copy` target after build does it |

### A.5 Stream A — App services

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P10 | **Configuration and small stores** ([§8.7](#87-configuration-huddle-reads), [§8.8](#88-writing-skillsmd), [§6.12](#612-removing-and-hiding)): the four `TeamOptions` keys; `SkillSourcesSettings` (`dir`, `seen`, one writer, `Changed`); `HiddenStore`; `SourceLog`; `SkillsMdWriter` (remove a row's lines; retry); `SkillsMdTemplate` | P1 | `SkillSourcesSettingsTests`: `SetDir_MissingFolder_Refused` †, `MarkSeen_KeepsDirInSkillSourcesJson`, `SkillSourcesJson_Unreadable_TreatedAsAbsent` †, `Dir_ConfigurationKey_UsedWhenNoFile`. `HiddenStoreTests`: `Hide_Unhide_RoundTrip`, `MalformedFile_NotOverwritten`, `Write_IsThroughTemporaryFile`. `SourceLogTests`: `Append_NeverRewrites_OneLinePerCall`, `Append_Concurrent_NoInterleave`, `Line_HasAtComputerHuddleEventSubject`. `SkillsMdWriterTests`: `RemoveRow_WritesEveryOtherByteUnchanged_CrlfKept`, `RemoveRow_FileChangedInBetween_Retries` | Tests green; `TeamOptionsTests` pin the four defaults |
| P11 | **Skill store and provenance** ([§6.3](#63-the-checks), [§6.6](#66-skillcopier-and-sourcejson), [§7.2](#72-schemas)): `SkillStore.ScanDisk` skips folders starting with `.`; ignores `tools:` where `.source.json` exists; `SkillProvenance` gains `schemaVersion`, `source`, `origin`, `copiedFrom`; `SkillProvenanceLine` shows the Source and **Source removed**; `SkillOrigin` | P1 | `ScanDisk_DotFolders_AreSkipped` †, `All_FolderWithSourceJson_IgnoresToolsLine` †, `All_FolderWithoutSourceJson_IsYours` †, `Provenance_NewFile_IsReadByOldReaderShape`, `Provenance_OldFile_ReadsWithNewFieldsNull`, `SkillProvenanceLineTests`: a copy whose Source no longer exists shows **Source removed** | Tests green; every existing `SkillStoreTests` still green |
| P12 | **Registry service and watcher** ([§6.5](#65-skillregistry-and-registrywatcher), [§8.5](#85-watching-pointers), [§9](#9-incremental-vs-full-processing), [§10](#10-background-workers--async-components)): `SkillRegistry`, `RegistryWatcher`, watch roots, the 32 cap, polling, scope merging, the `seen` baseline, holders, `BuildCheckContext`, start-up order, DI registration (`RegistryWatcher` before `BuiltinAgentSeeder`); the `SnapshotBuilder` helper for UI tests. Aligns §6.5 start-up step 2.2 with §6.6 (restore `.trash` before the sweep) and §10 with §6.13 (the watcher calls the move) | P5, P6, P7, P8, P10, P11 | `SkillRegistryTests` (the 17 in §6.5), `RegistryWatcherTests`: `WatchRoots_33Roots_CreatesAtMost32AndPollsTheRest` †, `WatchRoots_NestedRoots_Collapse` †, `WatchRoots_NetworkRoot_IsPolled` †, `Watcher_Error_MovesRootToPollingAndFullReread` †, `Poll_PathAppearsLater_TriggersRebuild` †, `Rebuild_Throws_KeepsPreviousSnapshot` †, `Rebuild_BurstOfScopes_MergesIntoOneFollowUp` †, `Rebuild_SkillsMdDeleted_PublishesR001AndNoRows` †, `StartAsync_DirMissing_CreatesIt` †, `StartAsync_SkillSourcesDirOnMissingDrive_EmptySnapshotAndSourcesTabNamesFolder` †. `ServiceCollectionExtensionsTests`: `AddTeamServices_RegistryWatcherStartsBeforePersonaSupervisor` | Tests green; a `FakeTimeProvider` drives the 500 ms, 2 s and 30 s rules with no sleep; a manual run with 50 downloaded Skills meets the §11 numbers |
| P13 | **Copier** ([§6.6](#66-skillcopier-and-sourcejson), [§8.6](#86-reading-and-copying-while-the-ai-tool-writes)): `SkillCopier`, `ISkillCopier` (`CopyAsync`, `EnsureAvailableAsync`), the swap, retries, `.source.json` writing, the start-up repair of `.trash` | P12 | The 21 tests of §6.6, then `CopyAsync_DiskFullDuringCopy_ReportsIoAndRemovesStaging` † and `StartAsync_TrashHoldsSkillAndFolderMissing_RestoresItBeforeSweep` † | Tests green; the copier takes no `HttpClient` (reflection test shared with the move) |
| P14 | **Install files** ([§6.7](#67-installskillpackmd-and-the-context-file)): `InstallFileWriter` (hosted, after the watcher), the front matter, the body from `installSkillPack.body`, appendices A to D with their caps, the context file, coalescing, retry | P12, P8, P15 (prompt key) | `InstallFileWriterTests`, `InstallAppendixTests`, `InstallContextFileTests` and `InstallBodyDefaultTests` (§6.7) | Tests green; registration order test green; the default body is at most 9,600 bytes raw and the rendered fixed part at most 20,480 |
| P15 | **Prompts** ([§6.8](#68-prompts-the-human-pastes), [Appendix C](#appendix-c--default-prompt-texts)): the seven `PromptDefinition`s, `prompts.default.json` (regenerate with `agents/scripts/Regenerate-PromptDefaults.ps1`), `PromptFieldFactory.GroupOrder`, `SkillPromptBuilder`, `PathFormats`, `PathFormat`, `PathFormatChoice` | P1 | `SkillPromptBuilderTests` and `PathFormatsTests` (the 28 in §6.8); `PromptCatalogTests.All_HasExpectedPromptCount` 95 to 102; `PromptDefaultsFileTests`; `Prompt_BodyOverrideMissingValidateCommand_IsRejected` | Tests green; `PromptGoldenTests` do not move |
| P16 | **The move** ([§6.13](#613-the-move)): `SkillSourcesMove`, `IMoveFiles`, `LegacyCatalogReader`, `LegacyPackReader`, `CatalogSplitter`, `MoveStaging`, `MoveNotes`, `MoveReportStore`; the golden files. Announce nothing: all inside `Huddle.App` | P5, P6, P7, P10, P11 | Every class of §6.13 in order: `MoveDetectionTests`, `MoveCrashSafetyTests`, `CatalogSplitTests`, `ShippedPackRowsTests`, `UserPacksMoveTests`, `ImportedSkillsMoveTests`, `MoveBoundaryTests`, `MoveOutputChecksTests`, `MoveReportTests`, `SkillsMdGoldenTests`. Plus `RunAsync_SkillsMdDeletedByHand_RunsAgain` †, `RunAsync_OtherHuddleWinsRace_DiscardsStaging` †, `RunAsync_ExistingCatalogWithPaths_KeepsPathsOnRerun` † | Tests green; `Move_FullFixture_ProducesNoErrorFindings`; the golden `SKILLS.expected.md` reviewed by a person, not just accepted |
| P17 | **Read-only Library root** ([§6.11](#611-the-skill-sources-library-root)): `LibraryRoot.ReadOnly`, `LibraryRootKind.SkillSources`, `LibraryPath.ReadOnly`, resolver, `LibraryProtection`, every writer in `LibraryFileService`, `LibraryRoots.DefaultDocument`, `LibraryHiddenFolders`, `LibraryPanel` caption | P10 | `LibraryRootStoreSkillSourcesTests`, `LibraryPathResolverReadOnlyTests`, `LibraryFileServiceReadOnlyTests`, `LibraryProtectionTests`, `LibraryExplorerSkillSourcesTests`, `LibraryPanelTests` (updated) | Tests green; the review checklist grep of §6.11.3 has every hit either Huddle's own write or guarded |
| P18 | **Hire path and cards' logic** ([§6.10](#610-agent-card-role-form-and-build-a-team) 1 to 3, 8): `ISkillAvailability` on `SkillRegistry`, `SkillSetupCheck`, `SkillSetupText`, `CardResolver` over the registry, `HireService.EnsureSkillsAsync`, `RoleHirer`, `TeamDuplicator`, `PackMembership.Add/Remove/DropRecord` extracted from `TogglePack` | P12, P13 | `CardResolverTests` (the four in §6.10.9), `SkillSetupCheckTests`, `HireServiceStartTests`, `SkillCopierEnsureTests`, `PackMembershipTests`, `CardResolver_WithdrawnSkill_NeedsSetup` | Tests green; existing `Reasons`-reading tests green unchanged |
| P22 | **Removing and hiding** ([§6.12](#612-removing-and-hiding)): the remove-Pack, remove-catalog, remove-Source and unhold flows, the hide commands and snackbars; `RemovePackDialog` (`G6a`) | P10, P12, P18 | `RemovePackTests`, `RemoveCatalogTests`, `RemoveSourceTests` (including `RemoveSource_IdResolvesOutside_Refused` and `RemoveSource_ReparsePointInside_IsUnlinkedTargetKept`), `HiddenStoreTests`, `HideTests`, `SkillsMdWriterTests` | Tests green; nothing outside `{SS}` and `Skills/.staging` or `.trash` is ever deleted (a directory-snapshot test) |

### A.6 Stream U — The user interface

Each UI task builds against `SnapshotBuilder` (P12) and closes only when P12 is merged. A
component takes plain values and injects the registry ([§6.9.1](#691-what-it-is)). Draw the
wireframe row in `docs/engineering/wireframes.md` in the same commit (P29 lists the rows).

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P19 | **Shell and guidance text** ([§6.9.1](#691-what-it-is) to [§6.9.4](#694-state-chips), [§6.9.12](#6912-guidance-text)): `SkillsPanel` with three tabs, `SkillsTab` (virtualised grid), `SkillStateChips`, `SkillStateText`, `SkillProblemText`, `SkillGuide`, the Settings badge | P1, P12 (helper) | `SkillsPanelTests`, `SkillStateChipsTests`, `SkillStateTextTests`, `SkillsTabTests`, `GuidanceTextTests` (intros, button sentences, every `CheckCatalog.All` code composes with its hint and the code last, no "simply", "just" or "easy"), `SkillsTab_FiveThousandRows_RendersWindowOnly` † | Tests green; no colour-only state; every icon-only control has an `aria-label` |
| P20 | **Packs tab** ([§6.9.5](#695-the-bar-the-cards-and-reload), [§6.9.6](#696-the-empty-state-g4d), [§6.9.9](#699-the-new-chip-and-the-snackbar), [§6.9.10](#6910-details-g6)): `PackBar`, `PackCard`, `PackEmptyState`, `PackDetailsDialog`, `SkillArrivalWatcher`, `ReadAgoText`, the New chip and `MarkSeenAsync` on leaving | P19 | `PackCardTests`, `PackBarTests`, `ReadAgoTextTests`, `PackEmptyStateTests`, `PackDetailsDialogTests`, `SkillArrivalWatcherTests`, `SkillsTabSeenTests`, `BrokenCard_ShowsCopyFixPrompt` † | Tests green |
| P21 | **Prompt dialog and in-app help** ([§6.9.7](#697-the-prompt-dialog-g5-g5a-g5b), [§6.9.12](#6912-guidance-text)): `AddWithAiDialog`, `CopyPathMenu`, `PromptClipboard`, `SkillsHelpDialog` (`G8`); `Import skill` opens `G5` | P15, P19, P27 | `AddWithAiDialogTests` (the seven in §6.9.13), `SkillsHelpDialogTests`, `SkillsHelpTextTests` (every help paragraph is found word for word in the User Guide) | Tests green; the dialog writes nothing |
| P23 | **Update card** ([§6.9.8](#698-the-update-card-g7)): `PackUpdatePlanner`, `PackUpdatePlan`, `UpdateDialog` | P13, P18, P19 | `PackUpdatePlannerTests`, `UpdateDialogTests` (the four in §6.9.13) | Tests green; copy-before-Agent order pinned |
| P24 | **Sources tab and the move's snackbar** ([§6.9.3](#693-the-three-tabs), [§6.13](#613-the-move)): `SourcesTab`, `SourceRow`, the Folder block, Skills repository (read only), `MainLayout` shows `MoveReportStore.TakeUnshown()` once | P16, P19, P22 | `SourcesTabTests`, `MoveReportTests` (the three texts, `ReportStore_TakeUnshown_ReturnsOnceThenNull`), `MainLayoutMoveSnackbarTests` † | Tests green |
| P25 | **Agent card, role form, Build a team, warnings** ([§6.10](#610-agent-card-role-form-and-build-a-team) 4 to 7): `PackChip`, copy on **Save**, **Find more Packs for {Role}**, the Skills select and its collapsed list, `RoleFormDialog`, `BuildTeam` (Needs setup, Copy prompt, `CanApprove`, `PrepareHiresAsync`), `MemberWarnings.ForSources` including the Pack-not-in-registry case | P18, P19, P21 | `AgentProfileCardPackTests`, `AgentProfileCardSaveTests`, `AgentProfileCardSkillsTests`, `RoleFormDialogTests`, `BuildTeamSetupTests`, `MemberWarningsTests`, `ForSources_PackNotInRegistry_Warns` †, `AgentProfileCardPackTests.PackNotInRegistry_ShowsWarning` † | Tests green; **Save** copies before `PersonaStore.Update` |

### A.7 Stream R — Removals

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P26 | **Remove the old code** ([§6.14](#614-code-that-goes)): delete `SkillDownloader`, `SkillImporter`, `ImportingSkillInstaller`, `ISkillInstaller`, `SkillsCatalogIndex`, `SkillPackStore`, `SkillImportDialog.razor`, the embedded `TeamTypes\Builtin\SkillPacks\*.json` and `Huddle.App.csproj` line 42, the two `new HttpClient` registrations, the old `AddSingleton` lines and the tests of the same names. Keep line 39 (`SkillsCatalog.json`) and `Build-SkillsCatalog.ps1` for one release. Reword the `TeamPaths` summaries | P16, P18, P22, P25 | `ServiceCollectionExtensionsTests`: `ISkillInstaller_NoLongerRegistered`, `ISkillCopier_ResolvesToSkillCopier`; `NoHttpClient_ForSkills` † (reflection over `Agency.Huddle.App` finds none in the Skills types); the deleted tests are removed in the same commit. The compiler is the test: the solution builds with no reference to a deleted type | `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` green; `grep -rn "SkillPackStore\|SkillsCatalogIndex\|SkillImporter\|SkillDownloader" src tests` returns nothing but the move's legacy readers |

### A.8 Stream D — Documentation

Every page added, renamed or deleted under `docs/` changes `docs/Index.md` in the same commit
(`Check-DocIndex.ps1`). Lint each page with the markdown skill before review.

| # | Task | Depends on | Done when |
| --- | --- | --- | --- |
| P27 | **User Guide** (`docs/Huddle.UserGuide.md`): rewrite "Giving Agents Skills" (near line 1726) and "Importing a Skill" (near line 1782) for SKILLS.md, **Add with your AI tool**, **Not downloaded**, **Update available**, **Needs setup**. Add the subsections "The Skills tab", "The Packs tab" and "The Sources tab", each at most 250 words: they are the text `SkillsHelpDialog` shows and `SkillsHelpTextTests` compares word for word | P0 | The three subsections exist and match `SkillGuide.HelpFor`; no external link; `Check-DocIndex.ps1` passes |
| P28 | **Engineering docs**: `docs/Huddle.EngineeringGuide.md` gains rows for `Team:SkillSourcesDir`, `Team:ComputerName`, `Team:SkillsRepositoryDir`, `Team:HuddlePacksPath` and replaces `SkillPacks/` in the storage table with `SkillSources/`, `InstallSkillPack.md`, `install-context.json`, `skill-sources.json`, plus the seven prompt keys and the new prompt count; `docs/engineering/language.md` (**Pack** says it comes from SKILLS.md; adds **Source**, **Skill pointer**, **Skill Sources folder**, **Catalog**); `code-map.md` (rows for the two projects and folders, removes the deleted types); `known-limits.md` (the move runs on the first Huddle only; the one-letter computer name) | P0; keys with P10; rest with P26 | The keys table matches `TeamOptions` (a doc check reads both); `Check-DocIndex.ps1` passes |
| P29 | **`docs/engineering/wireframes.md`**: add `G4`, `G4a` to `G4f`, `G5`, `G5a`, `G5b`, `G6`, `G6a`, `G7`, `G8` as [§6.9.11](#6911-wireframes-proposed-rows-for-docsengineeringwireframesmd) lists; update the notes of `G2a`, `G3`, `B9b`, `B9c`, `B9e` and `B10`; draw the badges in the Settings figure. Done with the UI task that builds each, not after | P19 to P25 | Every code appears in the page and in the component that draws it; the page's own checks pass |
| P30 | **Manual tests**: new `docs/engineering/manual-tests/skill-packs.md` (SKILLPACKS-01 add with a real AI tool; -02 choose a Pack; -03 update; -04 move on a real data folder; -05 two computers sharing one folder; -06 unplugged drive; -07 Withdrawn); `manual-tests/skills.md` replaces its import steps with a link to the new script; `manual-tests.md` lists it. Run `agents/scripts/Check-ManualTests.ps1` | P24, P25 | The checker passes; a person has run SKILLPACKS-04 on a copy of a real `App_Data` and recorded the result |
| P31 | **Cross-references and corrections**, last: `docs/Index.md` (a row for this specification and the ADR; the vision row becomes "Superseded by …"; the Skill import row points at the new pages); `docs/Huddle.Skills-Specifications.md` (§6.7 and the import notes point here; provenance names a Source); `docs/adr/0043-…md` status; `CLAUDE.md` table; and the **vision's corrections** in `docs/Huddle.SkillPacksSpec.md`: (a) the update card `G7` follows [§8.4](#84-noticing-an-update), where a Skill file change is shared and has no checkbox, and only membership has; (b) the catalog has **263** Skills, not 265, and mattpocock/skills is 37 and obra/superpowers 14, not 38 and 15, with `retro` and `test-driven-development` kept once (lines 20, 108, 936, 956); (c) the **Skills repository** is the configuration key `Team:SkillsRepositoryDir`, read once by the move, not a field on the Sources tab (lines 958 to 963); (d) the intros end with **Learn more**, an in-app dialog, not a link to the User Guide; (e) the move's Source ids are `local/skills` and `local/my-packs`, under `sources/local/`; (f) its status line says "Requirements met by Huddle.SkillPacks-Specifications.md". The two `<!-- … -->` placeholders are gone from this specification | All | `Check-DocIndex.ps1` passes; a search of the vision for "265" finds only the sentence that explains the correction |

### A.9 Stream F — The whole, end to end

| # | Task | Depends on | Failing tests first | Done when |
| --- | --- | --- | --- | --- |
| P32 | **End-to-end and release check.** A test double of the AI tool (`FakeAiTool`) follows `InstallSkillPack.md` by writing files and running `Program.Run`, then the real watcher and registry react. Run the whole of [§13](#13-end-to-end-flow) | All | `SkillPacksEndToEndTests`: `U1_AiToolAddsPack_PackIsNewAndSnackbarShown`, `U2_ChoosePack_CopiesThenChangesAgent`, `U3_PointerChanges_ShowsUpdateAvailable_NothingElseChanges`, `U4_ApplyUpdate_CopiesFilesThenMembership`, `U5_FirstStart_MoveWritesSixCatalogs263Skills_NothingNew`, `U9_HalfWrittenPackFile_NoBrokenFlash`, `U13_WithdrawnSource_BlocksNewHire_HoldersKeepWorking`. Then `HuddlePacksParity`: for the output of the move, the library's `Checks.Run` and `huddle-packs validate` agree | `Check-All.ps1` green; `./test-health.ps1` passes with an empty `DataDir` (the watcher must not throw at process start); SKILLPACKS-01 to -07 run and recorded; the move reviewed against a copy of a real data folder |

### A.10 Order of work for one person

If one person does everything, the order that keeps the build green and the review small is:
P0, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11, P12, P13, P15, P14, P16, P17, P18, P19, P20, P27,
P21, P22, P23, P24, P25, P26, P28, P29, P30, P31, P32. P29 is spread across the UI tasks rather than
done at the end.

## Appendix B — Files touched

Every path under **Changed** and **Deleted** was checked to exist on 2026-10-17. Paths under
**New** are proposed: the names inside `Huddle.SkillPacks`, `Huddle.Packs` and the new
`Huddle.App` folders follow the type names fixed in the brief; the section that owns a
component ([§6.1](#61-huddleskillpacks-library-overview) to [§6.12](#612-removing-and-hiding))
may rename a file, and Appendix A's task for it records the final name. Two root files are
shared and are announced before they change: `Huddle.slnx` and `Directory.Packages.props`
(CLAUDE.md, "Two projects, one build root").

A **Moved** file is deleted at its old path and new at its new one; it is listed under both so a
reviewer sees one rename, not a deletion and an addition.

### New

#### Source

| Path | What it is |
| --- | --- |
| `src/Huddle.SkillPacks/Huddle.SkillPacks.csproj` | Class library `Agency.Huddle.SkillPacks`; references Markdig only |
| `src/Huddle.SkillPacks/IFileReader.cs`, `PhysicalFileReader.cs`, `InstallContext.cs` | The only disk seam, and the context file's type with `Parse` and `ToJson` ([§6.2](#62-registry-model-and-file-readers), [§6.7](#67-installskillpackmd-and-the-context-file)) |
| `src/Huddle.SkillPacks/Reading/RegistryReader.cs`, `Registry.cs`, `RegistryRow.cs`, `PathEntries.cs` | SKILLS.md reader and per-computer paths ([§8.1](#81-reading-skillsmd), [§8.2](#82-per-computer-paths)) |
| `src/Huddle.SkillPacks/Files/PackFile.cs`, `CatalogFile.cs`, `SourceFile.cs`, `ParseResult.cs` | The three file schemas |
| `src/Huddle.SkillPacks/Resolving/RegistryResolver.cs`, `ResolvedRegistry.cs`, `PackEntry.cs`, `SkillEntry.cs`, `ResolvedCatalog.cs`, `ResolvedSource.cs`, `SourceId.cs`, `ItemState.cs` | Rows to Packs, catalogs and Skills, without states ([§8.3](#83-resolving-packs-catalogs-and-skills)) |
| `src/Huddle.SkillPacks/Checking/Checks.cs`, `CheckContext.cs`, `CheckOptions.cs`, `CheckResult.cs`, `Finding.cs`, `Severity.cs`, `CheckCatalog.cs` | `Checks.Run(...)`, its inputs and outputs, and the table of codes and fix hints ([§6.3](#63-the-checks)) |
| `src/Huddle.SkillPacks/Checking/RegistryChecks.cs`, `PackChecks.cs`, `CatalogChecks.cs`, `SkillFolderChecks.cs`, `SourceChecks.cs`, `SecretChecks.cs` | One class per code family: `R`, `P`, `D`, `S`, `O`, `X` |
| `src/Huddle.SkillPacks/Folders/SkillFolderHash.cs`, `SkillValidator.cs`, `SkillValidation.cs`, `SkillReviewScanner.cs` | **Moved** from `src/Huddle.App/Skills/` (see Deleted) |
| `src/Huddle.SkillPacks/Folders/SkillFrontmatter.cs`, `SkillFolderRules.cs` | The library's own `SKILL.md` front-matter parser (`PersonaFrontmatter` is internal to the app) and the folder limits of `S003`, `S004` |
| `src/Huddle.SkillPacks/Schemas/pack.schema.json`, `catalog.schema.json`, `source.schema.json` | Embedded resources printed by `huddle-packs schema` |
| `src/Huddle.Packs/Huddle.Packs.csproj` | Console exe, assembly `huddle-packs`; references `Huddle.SkillPacks` |
| `src/Huddle.Packs/Program.cs`, `ArgumentParser.cs`, `DataLocator.cs`, `FindingWriter.cs` | Entry point, hand-rolled parsing, finding the data and context file, the line and `--json` output ([§6.4](#64-the-huddle-packs-command)) |
| `src/Huddle.Packs/Commands/ValidateCommand.cs`, `RolesCommand.cs`, `InstalledCommand.cs`, `SkillsCommand.cs`, `SchemaCommand.cs`, `HelpText.cs` | One class per command, and the usage text || `src/Huddle.App/SkillSources/SkillRegistry.cs`, `RegistryWatcher.cs` | Snapshot singleton with `Changed`, and the hosted watcher that runs the move first ([§6.5](#65-skillregistry-and-registrywatcher)) |
| `src/Huddle.App/SkillSources/SkillCopier.cs`, `ISkillCopier.cs` | Copies a Skill from a pointer into `Skills/` and writes `.source.json`; the interface the hire path and the UI use ([§6.6](#66-skillcopier-and-sourcejson)) |
| `src/Huddle.App/SkillSources/InstallFileWriter.cs`, `IInstallFileLocation.cs`, `RoleLister.cs` | Writes `InstallSkillPack.md` and `install-context.json`; the union of roles ([§6.7](#67-installskillpackmd-and-the-context-file)) |
| `src/Huddle.App/SkillSources/SkillPromptBuilder.cs`, `PathFormats.cs`, `PathFormatChoice.cs` | Builds the six prompts the Human pastes, and the three path forms ([§6.8](#68-prompts-the-human-pastes)) |
| `src/Huddle.App/SkillSources/SkillSourcesSettings.cs` | Reads and writes `skill-sources.json`; resolves the Skill Sources folder ([§8.7](#87-configuration-huddle-reads)) |
| `src/Huddle.App/SkillSources/SourceLog.cs`, `HiddenStore.cs`, `SkillsMdWriter.cs` | `log.jsonl` appender, `hidden.json`, and the row-removing SKILLS.md writer ([§6.12](#612-removing-and-hiding), [§8.8](#88-writing-skillsmd)) |
| `src/Huddle.App/SkillSources/SkillsMdTemplate.cs` | The fixed template that the move and a first-time create share |
| `src/Huddle.App/SkillSources/Move/SkillSourcesMove.cs`, `IMoveFiles.cs`, `FileMoveFiles.cs`, `MoveReport.cs`, `MoveReportStore.cs` | The move ([§6.13](#613-the-move)) |
| `src/Huddle.App/SkillSources/Move/LegacyCatalogReader.cs`, `LegacyPackReader.cs`, `MoveStaging.cs`, `CatalogSplitter.cs`, `MoveNotes.cs` | Old-format readers, the staging folder, the catalog split and the `NOTES.md` text |
| `src/Huddle.App/Components/Settings/Skills/SkillsTab.razor`, `PacksTab.razor`, `PackBar.razor`, `SourcesTab.razor`, `SourceRow.razor` | `G4`, `G4a` to `G4b`, `G4e`, `G4f`: the three tabs, each with its `SettingsIntro` and **Learn more** ([§6.9](#69-settings--skills-g4g8-and-its-guidance-text)) |
| `src/Huddle.App/Components/Settings/Skills/PackCard.razor`, `PackEmptyState.razor`, `PackDetailsDialog.razor`, `UpdateDialog.razor`, `RemovePackDialog.razor`, `AddWithAiDialog.razor`, `CopyPathMenu.razor`, `SkillStateChips.razor`, `SkillsHelpDialog.razor` | `G4c`, `G4d`, `G6`, `G7`, `G6a`, `G5`, `G5b`, the state chips with their hover text, and the in-app help (`G8`) |
| `src/Huddle.App/Components/Layout/SkillArrivalWatcher.cs` | The "ready" snackbar |
| `src/Huddle.App/Components/Shared/PackChip.razor` | One Pack chip on an Agent card ([§6.10](#610-agent-card-role-form-and-build-a-team)) |
| `src/Huddle.App/Teams/PackMembership.cs`, `SkillSetupCheck.cs`, `SkillSetupText.cs`, `ISkillAvailability.cs` | The pure rules for adding and removing a Pack's Skills, the **Needs setup** check and its sentences, and the registry seam `CardResolver` takes |
| `src/Huddle.App/Components/Settings/Skills/ReadAgoText.cs` | "SKILLS.md read 1 minute ago" |
| `src/Huddle.App/Library/LibraryProtection.cs` | The read-only root guard ([§6.11](#611-the-skill-sources-library-root)) |
| `src/Huddle.App/Skills/Ui/SkillStateText.cs`, `SkillProblemText.cs`, `SkillGuide.cs`, `SkillRow.cs`, `PackUpdatePlanner.cs`, `PromptClipboard.cs` | The guidance sentences for every chip, button and problem, the help text (UI text, not prompts), and the plain types behind the tabs |

#### Tests

| Path | What it pins |
| --- | --- |
| `tests/Huddle.Tests/SkillPacks/Reading/` and `…/Resolving/` (`RegistryReaderTests.cs`, `PathEntriesTests.cs`, `PackFileTests.cs`, `CatalogFileTests.cs`, `SourceFileTests.cs`, `RegistryResolverTests.cs`) | [§8.1](#81-reading-skillsmd) to [§8.3](#83-resolving-packs-catalogs-and-skills) |
| `tests/Huddle.Tests/SkillPacks/Checks/` (the classes of [§6.3](#63-the-checks)) | Every code in the check table |
| `tests/Huddle.Tests/SkillPacks/InMemoryFileReader.cs`, `Fixtures/skills-repo/` | The test seam for `IFileReader`, and a copy of the skills repository's two Packs and `NOTES.md`, so no test reads `E:\Repos\skills` |
| `tests/Huddle.Tests/SkillPacks/SkillFolderHashTests.cs`, `SkillValidatorTests.cs`, `SkillReviewScannerTests.cs` | **Moved** from `tests/Huddle.Tests/Skills/` |
| `tests/Huddle.Tests/SkillPacks/Move/` (`MoveFixture.cs`, `MoveDetectionTests.cs`, `MoveCrashSafetyTests.cs`, `CatalogSplitTests.cs`, `ShippedPackRowsTests.cs`, `UserPacksMoveTests.cs`, `ImportedSkillsMoveTests.cs`, `MoveBoundaryTests.cs`, `MoveOutputChecksTests.cs`, `MoveReportTests.cs`, `SkillsMdGoldenTests.cs`) | [§6.13](#613-the-move) |
| `tests/Huddle.Tests/SkillPacks/Move/Golden/SKILLS.expected.md`, `Golden/dotnet-skills.skillcatalog.expected.json` | The two golden files |
| `tests/Huddle.Tests/SkillPacks/Copier/SkillCopierTests.cs`, `SkillCopierEnsureTests.cs` | Copy, hash agreement, swap, retry ([§8.6](#86-reading-and-copying-while-the-ai-tool-writes)) |
| `tests/Huddle.Tests/SkillPacks/Watching/RegistryWatcherTests.cs`, `SkillRegistryTests.cs`, `SkillRegistrySeenTests.cs`, `SkillRegistryContextTests.cs` | Watch roots, polling, snapshot publishing ([§8.5](#85-watching-pointers)) |
| `tests/Huddle.Tests/SkillPacks/Install/` (`InstallFileWriterTests.cs`, `InstallAppendixTests.cs`, `InstallContextFileTests.cs`, `RoleListerTests.cs`, `InstallBodyDefaultTests.cs`) and `…/Prompts/` (`SkillPromptBuilderTests.cs`, `SkillPromptDefaultsTests.cs`, `PathFormatsTests.cs`) | The install file, its context file, the six prompts and the path forms |
| `tests/Huddle.Tests/SkillPacks/Cli/` (the classes of [§6.4](#64-the-huddle-packs-command)) | `huddle-packs` run in process against a temp data folder |
| `tests/Huddle.Tests/Ui/SkillPacks/` (`SkillsPanelTests.cs`, `SkillsTabTests.cs`, `PackCardTests.cs`, `PackBarTests.cs`, `AddWithAiDialogTests.cs`, `PackDetailsDialogTests.cs`, `UpdateDialogTests.cs`, `SkillArrivalWatcherTests.cs`, `SkillsHelpDialogTests.cs`, `SkillsHelpTextTests.cs`, `GuidanceTextTests.cs`) | `G4` to `G8` and the guidance rules ([§6.9.13](#6913-bunit-tests)) |
| `tests/Huddle.Tests/Library/SkillSourcesRootTests.cs` | The read-only root refuses every write |

#### Other

| Path | What it is |
| --- | --- |
| `docs/Huddle.SkillPacks-Specifications.md` | This specification |
| `docs/adr/0059-skills-and-packs-are-fetched-by-the-humans-ai-tool.md` | The successor to ADR-0043 (task P0), which marks 0043 "Partly superseded" |
| `docs/engineering/manual-tests/skill-packs.md` | Manual test script for `G4` to `G7` and the move on a real data folder |
| `E:\Repos\skills\` (outside this repository) | Holds `packs/msbuild-authoring.skillpack.json`, `packs/code-review-and-quality.skillpack.json` and `NOTES.md`. Already in place; not a file of this change |

### Changed

#### Source

| Path | Change |
| --- | --- |
| `Huddle.slnx` | Adds `Huddle.SkillPacks` and `Huddle.Packs` (**shared root file; announce first**) |
| `Directory.Packages.props` | None expected: Markdig is already a `PackageVersion`. Checked in task P1 |
| `src/Huddle.App/Huddle.App.csproj` | `ProjectReference` to `Huddle.SkillPacks` and `Huddle.Packs`; remove line 42 (`TeamTypes\Builtin\SkillPacks\*.json`); line 39 (`SkillsCatalog.json`) stays one release |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | Lines 149, 153, 235 to 246 and 300 removed; the registrations of [§6.14](#614-code-that-goes) added after line 484 |
| `src/Huddle.App/TeamOptions.cs` | Adds `ComputerName`, `SkillSourcesDir`, `SkillsRepositoryDir`, `HuddlePacksPath` ([§8.7](#87-configuration-huddle-reads)) |
| `src/Huddle.App/Skills/SkillStore.cs` | `ScanDisk` skips folder names that start with `.` (`.staging`, `.trash`); reads `.source.json` in both shapes |
| `src/Huddle.App/Skills/Skill.cs` | Provenance fields carry the Source and origin |
| `src/Huddle.App/Skills/SkillProvenance.cs` | `.source.json` gains the Source and origin; still reads today's shape |
| `src/Huddle.App/Skills/SkillOrigin.cs` | Describes a Skill by Source and origin as well as repository and path |
| `src/Huddle.App/Teams/TeamPaths.cs` | `SkillPacksDir` and `SkillsCatalogFile` summaries say they are read only by the move |
| `src/Huddle.App/Teams/HireService.cs` | `SkillPackStore` becomes `SkillRegistry`; `ISkillInstaller` and lines 142 and 373 per [§6.10](#610-agent-card-role-form-and-build-a-team) |
| `src/Huddle.App/Teams/RoleHirer.cs`, `Teams/TeamDuplicator.cs` | `SkillPackStore` becomes `SkillRegistry` |
| `src/Huddle.App/Teams/CardResolver.cs` | Expands `role.Packs` from the registry; reports **Needs setup** |
| `src/Huddle.App/Teams/MemberWarnings.cs` | Sub-category lookup moves to `SkillRegistry` |
| `src/Huddle.App/Components/Settings/SkillsPanel.razor` | Becomes the host of the three tabs; **Import skill** (line 109) opens `G5` |
| `src/Huddle.App/Components/Settings/SkillNames.cs`, `SettingsTab.cs` | Skills tab badge shows the problem count |
| `src/Huddle.App/Components/Pages/Settings.razor` | Passes the problem count to the Skills row of the section menu (`G2a`) |
| `src/Huddle.App/Components/Pages/BuildTeam.razor` | Lines 30 and 31 inject `SkillRegistry`; `PrepareHiresAsync` sets `skillsBlocked` from not-downloaded Skills; draft cards show **Needs setup** |
| `src/Huddle.App/Components/Teams/RoleFormDialog.razor` | Line 13 injects `SkillRegistry`; the Skill picker marks **Not downloaded** with **Copy prompt** |
| `src/Huddle.App/Components/Shared/AgentProfileCard.razor` | Lines 1609 and 1711 use `SkillRegistry`; Pack chips (391 to 402) and `TogglePack` (1594 to 1656) copy before changing the Agent; **Find more Packs for {Role}**; **Update available** |
| `src/Huddle.App/Components/Shared/SkillProvenanceLine.razor` | Shows the Source, and **Source removed** |
| `src/Huddle.App/Components/Layout/MainLayout.razor` | Shows the move's snackbar once |
| `src/Huddle.App/Library/LibraryRoot.cs`, `LibraryRootKind.cs` | A read-only flag and the Skill Sources root ([§6.11](#611-the-skill-sources-library-root)) |
| `src/Huddle.App/Library/LibraryPathResolver.cs`, `LibraryFileService.cs` | Enforce the read-only flag on every write path |
| `src/Huddle.App/Library/LibraryHiddenFolders.cs` | `.move-*`, `.staging` and `.trash` are hidden |
| `src/Huddle.App/Prompts/PromptCatalog.cs`, `src/Huddle.App/prompts.default.json` | Seven `PromptDefinition`s: `installSkillPack.body`, `skillPrompt.add`, `findForRole`, `update`, `fix`, `fetchSkill`, `needsSetup` |
| `agents/scripts/Build-SkillsCatalog.ps1`, `agents/scripts/README.md` | Stay this release; deleted with the embedded catalog in the next ([§6.13](#613-the-move)) |
| `CLAUDE.md` | The "Two projects, one build root" table gains the two new projects |

#### Tests

| Path | Change |
| --- | --- |
| `tests/Huddle.Tests/Huddle.Tests.csproj` | `ProjectReference` to `Huddle.SkillPacks` and `Huddle.Packs` |
| `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs` | Lines 215, 226 and 252 to 263 change; adds the registration-order test |
| `tests/Huddle.Tests/Teams/TeamPathsTests.cs` | Unchanged constants; summaries reworded |
| `tests/Huddle.Tests/Teams/CardResolverTests.cs` | Pack expansion over `SkillRegistry`; **Needs setup** |
| `tests/Huddle.Tests/Teams/HireServiceTests.cs`, `HireServiceDraftTests.cs`, `HireServicePooledRoleTests.cs`, `HireServiceStartTests.cs` | Constructor and installer changes |
| `tests/Huddle.Tests/Teams/HouseholdEndToEndTests.cs`, `TeamsPhase3EndToEndTests.cs`, `TeamsPhase4EndToEndTests.cs`, `TeamDuplicatorTests.cs` | Use a registry over a temp Skill Sources folder in place of `SkillPackStore` |
| `tests/Huddle.Tests/Agents/RoleHirerTests.cs`, `RoleFixtures.cs`, `ProposalServiceTests.cs` | Same |
| `tests/Huddle.Tests/Skills/SkillStoreTests.cs`, `SkillProvenanceTests.cs`, `SkillOriginTests.cs` | New `.source.json` fields; dot-folder skip |
| `tests/Huddle.Tests/Skills/SkillsCatalogFileTests.cs` | Stays one release; pins the move's input |
| `tests/Huddle.Tests/Ui/SkillProvenanceLineTests.cs`, `AgentProfileCardSetupTests.cs`, `SettingsPanelsTests.cs` | Source line, Pack chips, tabs |
| `tests/Huddle.Tests/Ui/Teams/BuildTeamTests.cs` | **Needs setup** and `skillsBlocked` |
| `tests/Huddle.Tests/Library/LibraryPathResolverTests.cs`, `LibraryRootStoreTests.cs` | The read-only root |
| `tests/Huddle.Tests/Prompts/PromptCatalogTests.cs`, `PromptDefaultsFileTests.cs` | The seven new keys and their defaults |
| `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` | Checked only: the new prompts are not part of a Persona's system prompt, so no golden should move |

#### Docs

| Path | Change |
| --- | --- |
| `docs/Index.md` | A row for the specification and one for the ADR; the vision row becomes "Superseded by …"; the Skill import row points at the new pages (`Check-DocIndex.ps1` fails otherwise) |
| `docs/Huddle.UserGuide.md` | "Giving Agents Skills" (line 1726) and "Importing a Skill" (line 1782) are rewritten: Packs and Skills come from SKILLS.md, **Add with your AI tool** replaces **Import skill**, **Not downloaded**, **Update available**, **Needs setup**. Three new subsections, "The Skills tab", "The Packs tab" and "The Sources tab", are the text `SkillsHelpDialog` shows; nothing in the app links to the guide |
| `docs/Huddle.EngineeringGuide.md` | The `Team:` keys table (near line 275) gains `SkillSourcesDir`, `ComputerName`, `SkillsRepositoryDir`, `HuddlePacksPath`; the storage table (lines 346 to 358) replaces `SkillPacks/` with `SkillSources/`, `InstallSkillPack.md`, `install-context.json`, `skill-sources.json` |
| `docs/Huddle.Skills-Specifications.md` | Cross-references: §6.7 (Skills UI) and the import notes point to this specification; Skill provenance now names a Source |
| `docs/Huddle.SkillPacksSpec.md` | Status line says "Requirements met by Huddle.SkillPacks-Specifications.md"; wording that this specification overrides is listed in its §14 |
| `docs/adr/0043-imported-skills-are-pinned-by-commit-and-hash.md` | Status becomes "Partly superseded by 0059": pinning, hashing and review stay; the download goes |
| `docs/engineering/language.md` | **Pack** (line 127) says it comes from SKILLS.md; adds **Source**, **Skill pointer**, **Skill Sources folder**, **Catalog** (the Skill sense) |
| `docs/engineering/manual-tests/skills.md` | Import steps replaced by a link to `skill-packs.md` |
| `docs/engineering/manual-tests.md` | Lists the new script |
| `docs/engineering/wireframes.md` | Adds `G4`, `G4a`–`G4f`, `G5`, `G5a`, `G5b`, `G6`, `G6a`, `G7` and `G8` once the design is accepted; `G3`'s Skills line is reworded |
| `docs/engineering/code-map.md` | Rows for the two new projects and the new folders; removes the rows for `SkillPackStore`, `SkillsCatalogIndex`, `SkillImporter`, `SkillDownloader`, `ImportingSkillInstaller`, `SkillImportDialog` (lines 300, 320 to 326, 344) |
| `docs/engineering/known-limits.md` | Records the shared-folder limit: the move runs on the first Huddle only |

### Deleted

| Path | Replaced by |
| --- | --- |
| `src/Huddle.App/Skills/SkillDownloader.cs` | Nothing; the AI tool downloads |
| `src/Huddle.App/Skills/SkillImporter.cs` | `SkillCopier` |
| `src/Huddle.App/Skills/SkillsCatalogIndex.cs` | `SkillRegistry` |
| `src/Huddle.App/Skills/SkillFolderHash.cs`, `SkillValidator.cs`, `SkillValidation.cs`, `SkillReviewScanner.cs` | **Moved** to `src/Huddle.SkillPacks/Folders/` |
| `src/Huddle.App/TeamTypes/SkillPackStore.cs` | `SkillRegistry` |
| `src/Huddle.App/TeamTypes/Builtin/SkillPacks/msbuild-authoring.json` | `E:\Repos\skills\packs\msbuild-authoring.skillpack.json` |
| `src/Huddle.App/TeamTypes/Builtin/SkillPacks/code-review-and-quality.json` | `E:\Repos\skills\packs\code-review-and-quality.skillpack.json` |
| `src/Huddle.App/Teams/ImportingSkillInstaller.cs` | The registry check and `SkillCopier` |
| `src/Huddle.App/Components/Settings/SkillImportDialog.razor` | `AddWithAiDialog.razor` (`G5`) |
| `tests/Huddle.Tests/Skills/SkillDownloaderTests.cs`, `SkillImporterTests.cs`, `FakeSkillImporter.cs` | Nothing |
| `tests/Huddle.Tests/Skills/SkillsCatalogIndexTests.cs` | `SkillRegistryTests.cs` |
| `tests/Huddle.Tests/Skills/SkillFolderHashTests.cs`, `SkillValidatorTests.cs`, `SkillReviewScannerTests.cs` | **Moved** to `tests/Huddle.Tests/SkillPacks/` |
| `tests/Huddle.Tests/TeamTypes/SkillPackStoreTests.cs`, `BuiltinPacksTests.cs` | `SkillRegistryTests.cs` and `ShippedPackRowsTests.cs` |
| `tests/Huddle.Tests/Teams/ImportingSkillInstallerTests.cs` | `CardResolverTests.cs` and the `HireService` tests |
| `tests/Huddle.Tests/Ui/SkillImportDialogTests.cs` | `AddWithAiDialogTests.cs` |

**Deleted one release later,** with the embedded catalog: `src/Huddle.App/Skills/SkillsCatalog.json`,
`agents/scripts/Build-SkillsCatalog.ps1`, `tests/Huddle.Tests/Skills/SkillsCatalogFileTests.cs`,
and the move's `LegacyCatalogReader.cs`.

**Never touched:** `src/Huddle.App/Watching/DebouncedFileWatcher.cs` (reused as is),
`src/Huddle.App/HiringPool/hiringpool.json` (roles keep naming Packs by id), every Agent file,
`src/Huddle.Acp`, `src/Huddle.Console`, `src/Huddle.Contracts`, `src/Huddle.MockAdapter`, and
`tests/Huddle.Acp.Tests`.

---

## Appendix C — Default prompt texts

The default wording of the seven prompt keys that [§6.7](#67-installskillpackmd-and-the-context-file)
and [§6.8](#68-prompts-the-human-pastes) define. Each text below goes, character for character,
into a raw string literal in the key's `PromptDefinition.Default` in
`src/Huddle.App/Prompts/PromptCatalog.cs`, and into `src/Huddle.App/prompts.default.json`
(`PromptDefaultsFileTests` pins that the two agree). Rules every default follows:

- `{{name}}` placeholders only, each listed in the key's `Placeholders`. No other `{{` appears,
  so the examples are indented code blocks with single braces, and no text contains a triple
  backtick or a triple quote (both would break a fence or a raw string).
- The text is plain Markdown. `InstallSkillPack.md` puts `installSkillPack.body` after its front
  matter as it is; a prompt the Human pastes is sent as it is.
- A placeholder that can be empty (`{{purposeLine}}`) sits alone on its line;
  `SkillPromptBuilder` removes the gap an empty one leaves ([§6.8](#68-prompts-the-human-pastes)).

| Key | Placeholders | Required |
| --- | --- | --- |
| `installSkillPack.body` | `{{computer}}`, `{{skillSourcesDir}}`, `{{registryPath}}`, `{{validateCommand}}`, `{{checkHints}}` | all five |
| `skillPrompt.add` | `{{installFile}}`, `{{where}}`, `{{purposeLine}}` | `{{installFile}}`, `{{where}}` |
| `skillPrompt.findForRole` | `{{installFile}}`, `{{roleTitle}}`, `{{roleId}}`, `{{purposeLine}}` | `{{installFile}}`, `{{roleId}}` |
| `skillPrompt.update` | `{{installFile}}`, `{{sources}}` | both |
| `skillPrompt.fix` | `{{installFile}}`, `{{table}}`, `{{localPath}}`, `{{state}}`, `{{problems}}` | `{{installFile}}`, `{{localPath}}` |
| `skillPrompt.fetchSkill` | `{{installFile}}`, `{{skillName}}`, `{{catalogId}}`, `{{catalogPath}}`, `{{origin}}` | `{{installFile}}`, `{{skillName}}` |
| `skillPrompt.needsSetup` | `{{installFile}}`, `{{roleTitle}}`, `{{roleId}}`, `{{missing}}` | `{{installFile}}`, `{{roleId}}`, `{{missing}}` |

### `installSkillPack.body`

The 11 fixed sections of `InstallSkillPack.md`, headings and numbers included (section 2 is the
one [§8.6](#86-reading-and-copying-while-the-ai-tool-writes) calls "§2"). Section 10 ends in
`{{checkHints}}`: `InstallFileWriter` replaces it with a Markdown table (code, severity, check, fix
hint) generated from `CheckCatalog.All`, the one source of fix hints ([§6.3](#63-the-checks)), so
the body holds no hint table of its own.

Size, stated for the rendered file. The text with the placeholders left in is at most 9,600
bytes (a test pins it; today about 9,590). The generated table adds about 7.7 KB (46 codes, each with its check and fix hint), and the path values
add about 0.4 KB with ordinary paths and 1.4 KB with 260-character ones. So the fixed part of
`InstallSkillPack.md` (body plus table, appendices excluded) is about 18 KB rendered, and a
second test pins **20,480 bytes (20 KB)** for it with 260-character paths and the full catalog. Section 9 adds a fifth
task, **Fetch Skills**, which the vision's list of four does not name: `skillPrompt.fetchSkill`
and `skillPrompt.needsSetup` point at it.

```text
## 1. What you are doing

You add, update and retire Skills and Skill Packs for one Huddle, a chat app where AI
Agents work for a Human. Huddle does not download, sign in or go online for Skills. You do,
with the Human's own sign-ins, and then you record where things are in files Huddle reads.
Finish by telling the Human what is ready in Huddle (section 11).

A Skill is a folder with a SKILL.md. A Pack is a named set of Skills for some roles. This
computer is "{{computer}}". What this Huddle holds today is in the appendix at the end.

## 2. Rules

- Clone or download wherever suits you, pinned to one commit, and record where.
- Write only in {{skillSourcesDir}} or beside your clone. Never write into Skills/: Huddle
  copies Skills there itself, and a folder you put there would count as the Human's own and
  skip the checks. Never write in Agent files, Huddle settings or any log.jsonl.
- Write each file to a temporary name, then rename it, so Huddle never reads half a file.
- Never run, build, install or import anything a Skill or repository contains. Only read it.
- Never put a token, password, cookie or private key in any file you write.
- Ask the Human before fetching more than 20 Skills or 50 MB, and before using a Source that
  needs sign-in.
- Never delete a clone, a Skill folder or a row you did not add.
- Do not edit this file. Huddle overwrites it.

## 3. Where things go

Suggested places under {{skillSourcesDir}}: packs/<id>.skillpack.json for a Pack;
catalogs/<id>.skillcatalog.json for a catalog; sources/<host>/<owner>/<repo>/ for a Source's
source.json and NOTES.md (a local folder: sources/local/<name>/). A Source id is that path
after sources/: host/owner/repo or local/name, lower-case letters, digits, . and -, with /
between parts; no .., no leading /, no drive letter (else O001). Huddle made sources/local/skills
and sources/local/my-packs from older data; keep using them. A file elsewhere works if its row
points at it. Add one Skill to its Source's catalog (appendix D) rather than starting a new
catalog. Huddle writes log.jsonl itself; never touch it.

## 4. The skill pack and skill catalog files

Both are JSON with "schemaVersion": 1. `huddle-packs schema pack` and `huddle-packs schema
catalog` print the full JSON Schema. A Skill's "path" is a string (absolute, or relative to
the file it is in) or an object keyed by computer name, with an optional "*" for all others:
{ "DESK-01": "E:\\a\\docx", "LAPTOP": "C:\\a\\docx" }. It is "" until the Skill is fetched.

Pack, <id>.skillpack.json. id: lowercase letters, digits, single hyphens; equals the file
name; unique in SKILLS.md. version: the Pack's version, else the short commit. title, source
(host/owner/repo). appliesTo: role ids from appendix A; Huddle offers the Pack on those roles'
cards. consultWhenAdds: up to 200 characters on when to reach for these Skills; Huddle adds it
to the Agent's consult_when line. skills: each has a name and either "path" or "catalog"
(never both), and, with a path, an "origin" URL pinned to a commit.

    {
      "schemaVersion": 1, "id": "document-skills", "version": "dbd4588",
      "title": "Document skills", "source": "github.com/anthropics/skills",
      "appliesTo": ["bookkeeper", "writer"],
      "consultWhenAdds": "a Word, Excel or PDF file has to be made, read or changed",
      "skills": [
        { "name": "docx", "path": "E:\\Clones\\skills\\docx",
          "origin": "https://github.com/anthropics/skills/tree/dbd4588.../docx" },
        { "name": "target-authoring", "catalog": "dotnet-skills" }
      ]
    }

Catalog, <id>.skillcatalog.json: schemaVersion, id (equals the file name), title, source,
skills[]. Each entry has name, description, publisher, origin (pinned to a commit), license,
category, subCategory, requires ([]) and path, as in the Pack example. Names are unique in the
catalog.

## 5. The SKILLS.md tables

The registry is {{registryPath}}. It has two tables under the headings "## Packs" and
"## Catalogs". One row per file, never one per Skill. Columns: Local path (the only required
one), Origin URL, Last updated, Notes. Huddle ignores any other table and column.

- Local path: absolute, or relative to the folder SKILLS.md is in. No ~ and no environment
  variables. A Packs row points at a .skillpack.json, a Catalogs row at a .skillcatalog.json.
- One entry per computer: put each on its own line in the cell, joined with <br>, as
  NAME: path. NAME is 2 to 15 letters, digits or hyphens, then a colon and a space. Example:
  DESK-01: E:\a\x.skillpack.json<br>LAPTOP: C:\a\x.skillpack.json. A plain path is for every
  computer.
- You are on "{{computer}}". Add or change only this computer's entry and leave the others.
- Origin URL: an absolute https, ssh or git URL. Last updated: yyyy-MM-dd, today's date.
  Notes: one short line. Write a literal | as \|.
- Check appendix C first: update an existing row, never add a duplicate. Add new rows at the
  end of their table. Change no other row, keep the note at the top and do not reformat.
- A Pack names a catalog's Skill with "catalog" so the Pack and the catalog share one copy.

## 6. source.json and NOTES.md

Each Source has sources/<id>/source.json (<id> as in section 3; `huddle-packs schema source`):

    { "schemaVersion": 1, "kind": "github", "url": "https://github.com/anthropics/skills",
      "ref": "main", "commit": "dbd4588f...", "localPath": "E:\\Clones\\skills",
      "fetchedAt": "2026-10-17T16:40:00+02:00", "fetchedBy": "Claude Code",
      "signIn": "none", "status": "active" }

ref, commit, localPath are optional. kind: github, git or local. signIn: none or required, never a credential. status: active,
deprecated, withdrawn or moved; the last three need "statusReason", and moved needs
"movedTo". Record a move or deprecation you read about, with the reason.

NOTES.md starts with front matter naming its layout (layout: claude-marketplace, say), then
how you found the Source, its quirks and how to update it. Read it before you touch the Source
again. Keep it under 16 KB.

## 7. Reading a repository

- A Claude Code marketplace (.claude-plugin/marketplace.json): a plugin with a skills[] list is
  one Pack. Plugins carry no version; use the short commit.
- A skills folder with tiers (such as .system and .curated): one catalog, tier in the category.
- One SKILL.md, or a list of links: one catalog entry per Skill.
- Anything else: ask the Human.

## 8. What Huddle can use

Agents read only the .md files directly in a Skill's folder, up to 64 KB each. Sub-folders
are copied but never read. A Skill's tools: line cannot grant App Tools (check S011). Tell the
Human when this or a licence matters.

## 9. Tasks

Add (a link or folder)
1. Read NOTES.md if the Source has a folder under sources/. Read the repository's README and
   manifests (section 7).
2. Fetch it, pinned to a commit. Ask first if it needs sign-in.
3. Write source.json and NOTES.md.
4. Write a Pack for each natural group of Skills, and a catalog for the rest. Ask the Human
   which roles a Pack is for if it is unclear (appendix A).
5. Run the validate command on each file you wrote (section 10).
6. Add or update the SKILLS.md rows. Run validate once with no path.

Find Packs for a role
1. Find the role in appendix A. Prefer what is cheapest: Skills already installed (appendix B)
   and catalog Skills not yet downloaded (appendix D).
2. Search Sources whose NOTES.md fit, and `huddle-packs skills <text>`.
3. Suggest at most three Packs with source, Skills, licence and what you could not check.
   Fetch nothing until the Human chooses. Then do Add, with the role in appliesTo.

Check for updates
1. For each Source: read NOTES.md, fetch, compare with source.json's commit. If it has not
   moved, change nothing.
2. If it moved: re-read the manifest, rewrite only the Packs and catalogs whose Skills changed,
   keep each Pack id, change its version and each origin to the new commit, and update
   source.json (commit, fetchedAt), the rows' Last updated and NOTES.md.
3. Record a deprecation, withdrawal or move in source.json. Delete nothing.
4. Run validate. Report each Source: changed or not.

Fix a problem
1. Read the findings the Human pasted and run validate on the row's path to see the current ones.
2. Fix the cause in that row and the files it points at, and nothing else. A path with no entry
   for this computer: fetch the Skills here and add this computer's entry.
3. Run validate --strict on the path until Errors=0.

Fetch Skills (one Skill, or what a role needs)
1. Find each Skill's catalog entry (appendix D). Fetch exactly the commit in its origin.
2. Set its "path" (this computer's entry if the catalog is shared). Leave the row alone.
3. Run validate --strict on the catalog file until Errors=0.

## 10. Checking your work

Run: {{validateCommand}} [path] --strict

The other commands named here (roles, installed, skills, schema) use the same command line
with validate replaced.

With a path it checks only the rows at or under it; without one, everything. Output is one line
per finding and a last line PACKS-VALIDATE Rows=.. Packs=.. Skills=.. Errors=.. Warnings=..
Repeat until Errors=0. Warnings are allowed; mention them to the Human. Every code validate
prints is in this table, with how to fix it:

{{checkHints}}

## 11. Telling the Human

Write one short message in plain words: what is ready in Huddle, by name; what its details card
will flag (scripts, licences, unread files); anything you could not do and why. Put check codes
in brackets at the end, not at the front.
```

### `skillPrompt.add`

Task **Add from a link or folder**. `{{where}}` is the Human's text, single line.
`{{purposeLine}}` is `What they are for: <text>.` or empty.

```text
Read the file at {{installFile}} first, and follow it. It is Huddle's instructions for adding
Skills and Packs, with this Huddle's roles and Skills in its appendix.

Task: add Packs or Skills from {{where}} (section 9, "Add").
{{purposeLine}}

Fetch them to this computer with my sign-ins, write the Pack or catalog file, source.json and
NOTES.md, and add the SKILLS.md row. Run the validate command from the file with --strict
until it prints Errors=0. Ask me before fetching more than 20 Skills or 50 MB.

When you are done, tell me what is ready in Huddle and what its details card will flag.
```

### `skillPrompt.findForRole`

Task **Find Packs for a role**. `{{purposeLine}}` is `It should get better at: <text>.` or
empty.

```text
Read the file at {{installFile}} first, and follow it. It is Huddle's instructions for adding
Skills and Packs, with this Huddle's roles and Skills in its appendix.

Task: find Packs for the {{roleTitle}} role (id: {{roleId}}) (section 9, "Find Packs for a role").
{{purposeLine}}

Look first at what this Huddle already has (appendix B) and at catalog Skills not yet
downloaded (appendix D). Suggest at most three Packs. For each, say where it is from, which
Skills it holds, the licence, and what you could not check. Ask me before you fetch anything.
After I choose, add them as the file says, with {{roleId}} in appliesTo, and tell me when they
are ready.
```

### `skillPrompt.update`

Task **Check for updates**. `{{sources}}` is `all the Sources in SKILLS.md` or one
`- <source id>` line per checked Source.

```text
Read the file at {{installFile}} first, and follow it. It is Huddle's instructions for adding
Skills and Packs, with this Huddle's roles and Skills in its appendix.

Task: check these Sources for updates (section 9, "Check for updates"):
{{sources}}

Read each Source's NOTES.md before you touch it. If it has not moved, change nothing. If it
has, pull the new commit, rewrite only the files that changed, update source.json, NOTES.md and
the rows' Last updated, and run the validate command until it prints Errors=0. Record a
deprecation or a move in source.json with its reason. Delete nothing.

Then tell me, Source by Source, what changed and what did not.
```

### `skillPrompt.fix`

Task **Fix a problem**, and **Copy fix prompt** on a Pack or Source row. `{{table}}` is `Packs` or
`Catalogs`; `{{state}}` is `Broken` or `Not downloaded`; `{{problems}}` is one finding per line.

```text
Read the file at {{installFile}} first, and follow it. It is Huddle's instructions for adding
Skills and Packs, with this Huddle's roles and Skills in its appendix.

Task: fix the {{table}} row for {{localPath}} in SKILLS.md (section 9, "Fix a problem").
Huddle shows it as {{state}}. Huddle reported:

{{problems}}

Find the cause and fix that row and the files it points at, and nothing else. If this computer
has no entry for a path, fetch the files here and add this computer's entry only. Run the
validate command on the row's path with --strict until it prints Errors=0, then tell me what
you changed.
```

### `skillPrompt.fetchSkill`

**Copy prompt** beside a **Not downloaded** Skill. `{{catalogPath}}` is this computer's path to
the catalog file, or `(no path on this computer)`.

```text
Read the file at {{installFile}} first, and follow it. It is Huddle's instructions for adding
Skills and Packs, with this Huddle's roles and Skills in its appendix.

Task: fetch one Skill to this computer (section 9, "Fetch Skills").
Skill: {{skillName}}
Catalog: {{catalogId}}, at {{catalogPath}}
Origin: {{origin}}

Fetch exactly the commit in the origin, put it in a folder you choose, and set the Skill's
"path" in the catalog file (this computer's entry if the catalog is shared). Do not change the
SKILLS.md row or any other Skill. Run the validate command on the catalog file with --strict
until it prints Errors=0, then tell me when it is ready.
```

### `skillPrompt.needsSetup`

**Copy prompt** on a **Needs setup** draft card in Build a team. `{{missing}}` is one line per
missing item: `Pack "MSBuild Authoring" (msbuild-authoring)` or `Skill "code-review-and-quality"
(catalog addyosmani-agent-skills)`.

```text
Read the file at {{installFile}} first, and follow it. It is Huddle's instructions for adding
Skills and Packs, with this Huddle's roles and Skills in its appendix.

Task: set up what the {{roleTitle}} role (id: {{roleId}}) needs before I can hire it
(section 9, "Fetch Skills"). Missing on this computer:

{{missing}}

Fetch each one here and fill in its path (this computer's entry if the file is shared). Reuse
a catalog entry when one exists (appendix D) and fetch exactly the commit in its origin. Run
the validate command with --strict on each file you change until it prints Errors=0. Then tell
me what is ready; I will return to Huddle and approve the hire.
```
