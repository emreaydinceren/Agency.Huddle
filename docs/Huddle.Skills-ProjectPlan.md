# Huddle.Skills — Project Plan

Decomposition of [`Huddle.Skills-Specifications.md`](Huddle.Skills-Specifications.md) (the
**Spec**) into atomic, self-contained tasks. Every task is written for a sub-agent with **zero
project context**: it names exact paths, types, signatures and acceptance criteria, and cites
the Spec section that defines it.

**17 deliverables · 90 tasks** across the Spec's three streams. Every implementation task
(`.i`) is preceded by its test task (`.t`). A `.t` task ends **red, for the right reason**; its
`.i` partner ends **green**.

| Stream | Deliverables | Spec | May start |
| --- | --- | --- | --- |
| **S1 — Skills infrastructure** | D1–D7 | §6.1–§6.7 | Now |
| **S2 — Team-building tools** | D8–D13 | §6.8–§6.11 | Now, in parallel with S1 |
| **S3 — Content, Chief of Staff, Greeting** | D14–D17 | §6.12–§6.14 | D14 and D16 now; D15 after D3 |

---

## How to use this document

Work one task at a time, top to bottom within a deliverable. **Never write several `.t` tasks
before their implementations.** Tests written in bulk test imagined behaviour, not actual
behaviour (Spec Appendix A preamble). After every `.i` task, run the whole solution:

```powershell
dotnet build Huddle.slnx
dotnet test  Huddle.slnx --
```

### Dependency graph

```text
 S1   D1 ─▶ D2 ─┬─▶ D4 ─▶ D5 ─▶ D6 ─▶ D7
      D3 ───────┘
 S2   D8 ─▶ D9 ─▶ D10 ─▶ D11 ─▶ D12 ─▶ D13
 S3   D14 (needs D1)      D15 (needs D3; its Reset task needs D6's card changes)
      D16 (independent: wire + runner)                D17 (last: needs everything)
```

**Cross-stream seams (Spec §5.2).**
- `DotAcpAgentHostFactory`'s tool list. D5 (S1) owns it. D10 (S2) adds its tools to the list
  and they are offered to everyone until D5 lands; D5 then filters them.
- `PromptCatalog` and `prompts.default.json`. Tasks in D4, D9, D10 and D16 each add Prompts.
  Whoever merges second rebases and regenerates `prompts.default.json` (procedure below). The
  drift test `PromptDefaultsFileTests` fails loudly if anyone forgets.

---

## Repo-wide conventions (read once, applies to every task)

| Rule | Detail |
| --- | --- |
| Build | `dotnet build Huddle.slnx`: the **solution**, never one project, because test projects carry their own analyzers |
| Test | `dotnet test Huddle.slnx --`: **the trailing `--` is required**, or the run reports "Zero tests ran" and reads as a no-op |
| Warnings | `TreatWarningsAsErrors=true`, `Nullable=enable` (`Directory.Build.props`). Any analyzer complaint fails the build |
| Packages | No new NuGet package is needed anywhere in this plan. If you think one is, stop and ask |
| C# style | `agents/CSharpPrinciples.md` is binding. File-scoped namespaces matching the folder, `using` above the namespace, `this.field` (no `_` prefix), explicit type on the left with `new()` on the right, Allman braces, **CRLF**, four spaces, every class `sealed` unless `static` or `abstract` |
| XML docs | Every class and method takes `///` comments, **including tests**. Never `//` for documentation |
| Test naming | `Method_Scenario_Expectation`. Test classes are `public sealed class FooTests` |
| Cancellation | Every awaited call in a test that accepts a `CancellationToken` receives `TestContext.Current.CancellationToken` (`xUnit1051`) |
| Nullable | **Never** silence with `!` or `= null!`. Prove non-null with a pattern or a guard |
| App Tools | Return a string for every expected failure; never throw for one (`CreateRoomTool.cs` is the model) |
| Model-facing text | Never contains the literal `mcp__team__`. Tool names reach Prompts only through placeholders (`rules.md` row 55) |
| Line endings | The `Write` tool, heredocs and `python3` emit **LF**. After creating any file, check it with `git ls-files --eol -- <path>` (want `w/crlf`, or run it after `git add -N`). Repair with PowerShell: `$t=[IO.File]::ReadAllText($p); $u=$t -replace "`r`n","`n"; [IO.File]::WriteAllText($p, ($u -replace "`n","`r`n"))` |

### Existing `InternalsVisibleTo` (do not duplicate)

| Project | Grants to |
| --- | --- |
| `src/Huddle.App/Huddle.App.csproj` (line 10) | `Huddle.Tests` |
| `src/Huddle.Contracts/Huddle.Contracts.csproj` (line 9) | `Huddle.Tests` |
| `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj` (line 9) | `Huddle.Tests` |

Every new type in this plan lives in `Huddle.App` or `Huddle.Contracts`, and both already
grant `Huddle.Tests`. **Make new types `internal` unless a Razor component or a public
record must expose them** (Razor generates public component types, so a parameter type used by
a component must be public). Unit tests reach the internals through the existing grants.
**No task adds an `InternalsVisibleTo`.** If one seems necessary, the type is in the wrong
project.

### Regenerating `prompts.default.json`

`src/Huddle.App/prompts.default.json` is committed and copied to output. It must equal
`PromptCatalog.All` serialised key → `Default`. Any task that adds a `PromptDefinition` must
regenerate it. Serialise `PromptCatalog.All.ToDictionary(p => p.Key, p => p.Default)` with
`ProtocolJson.Options` plus `WriteIndented = true`, write it CRLF, then confirm
`tests/Huddle.Tests/Prompts/PromptDefaultsFileTests.cs` is green. The failure message of that
test repeats this procedure.

### Test helpers you will reuse

| Helper | Path | Use |
| --- | --- | --- |
| `TempDataDir` | `tests/Huddle.Tests/TempDataDir.cs` | Temp `DataDir`; `Options()` returns `IOptions<TeamOptions>` |
| `FakePromptSource` | `tests/Huddle.Tests/Acp/Fakes/` | Catalog defaults, or `SetOverride(key, text)` |
| `FakeAgentHostFactory` | `tests/Huddle.Tests/Acp/Fakes/FakeAgentHostFactory.cs` | Records `(Persona, AgentId)` calls; exposes `Session` |
| `PipeHostFixture` | `tests/Huddle.Tests/Pipes/PipeHostFixture.cs` | A real pipe server: `StartAsync(settings, ct)` |
| `MockAdapterFixture` | `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs` | Real factory against `mock-acp`; `ToolPrefixTests.GetAppendedSystemPromptAsync` shows how to read the system prompt |
| `TeamWebApplicationFactory` | `tests/Huddle.Tests/Ui/TeamWebApplicationFactory.cs` | In-process app; `TeamsDirPath`, `DbPath`, `PromptsJsonPath` |
| `MudBunitContext` | `tests/Huddle.Tests/Ui/MudBunitContext.cs` | bUnit with MudBlazor; `RenderWithPopovers` |

### Terminology (Spec header, `docs/agencyteam/language.md`)

**Skill**: a named folder of Markdown teaching one kind of work. **Skill Index**: the lines
naming each assigned Skill in the system prompt. **Proposal**: a roster of new Teammates an
Agent asks the Human to create. **Candidate**: one proposed Teammate; never "draft", which is
a defined term for streaming Turn text. **Greeting**: the Chief of Staff's unprompted first
Message; never "welcome", which is the pipe handshake Envelope. **Teammate**, **Persona**,
**Room**, **Message**, **Turn**, **Budget**, **Reply Gate**, **App Tool**, **Prompt**: as in
`language.md`.

### Binding documents

- `docs/agencyteam/rules.md`: **read in full before changing anything in `src/Huddle.App`**
- `docs/agencyteam/traps.md`: **read before touching `src/Huddle.Contracts` or the pipe** (D16)
- `agents/CSharpPrinciples.md`: house style, enforced by the build
- `docs/adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md`: the decision this plan builds

---

# S1 — Skills infrastructure

---

# D1 — Shipped Skill defaults

**Spec §6.1 (SkillCatalog)**, **Spec §7.2 (Skill frontmatter)**, **Spec §8.1 (Validating a
Skill)**, **Spec §5.3 Contract A**.

### Task 1.1.t — Test: the catalog holds `team-building` with its four files (red)

- **Goal:** Pin **Spec §6.1**: shipped Skills are compiled into the assembly.
- **Read first:** **Spec §6.1**, **Spec §5.3 (Contract A)**,
  `src/Huddle.App/Skills/Defaults/team-building/` (the four `.md` files already exist).
- **Deliverable:** Create `tests/Huddle.Tests/Skills/SkillCatalogTests.cs`, namespace
  `Agency.Huddle.Tests.Skills`. Test
  `All_ContainsTeamBuilding_WithItsFourFiles`: `SkillCatalog.All["team-building"]` has exactly
  the keys `SKILL.md`, `onboarding.md`, `roles.md`, `team-patterns.md` (ordinal). Test
  `All_FileText_HasNoCarriageReturns`: every value contains no `'\r'`.
- **Acceptance:** Fails to compile because `SkillCatalog` does not exist. **Red.**

### Task 1.1.i — Implement `SkillCatalog` and embed the defaults

- **Goal:** Implement **Spec §6.1**.
- **Read first:** Task 1.1.t, `src/Huddle.App/Huddle.App.csproj`, **Spec §6.1 (Implementation
  notes)**.
- **Deliverable:**
  1. In `Huddle.App.csproj` add
     `<EmbeddedResource Include="Skills\Defaults\**\*.md" LogicalName="Skills/Defaults/%(RecursiveDir)%(Filename)%(Extension)" />`.
  2. Create `src/Huddle.App/Skills/SkillCatalog.cs`, namespace `Agency.Huddle.App.Skills`:
     `internal static class SkillCatalog` with
     `internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; }`,
     built lazily once. Enumerate `typeof(SkillCatalog).Assembly.GetManifestResourceNames()`,
     keep names starting with `Skills/Defaults/`, normalise `\` to `/`, and split into
     `<skill>/<file>`. Read each as UTF-8, apply `ReplaceLineEndings("\n")`, and store in
     `FrozenDictionary` with `StringComparer.Ordinal` at both levels.
- **Acceptance:** 1.1.t green. `dotnet build` shows 0 warnings.

### Task 1.2.t — Test: the Skill validator (red)

- **Goal:** Pin **Spec §8.1** and the **Spec §7.2** Skill frontmatter table.
- **Read first:** **Spec §7.2 (Skill frontmatter)**, **Spec §8.1**, **Spec §6.5**
  (`SkillGrants.Grantable` is `validate_teammate`, `propose_teammates`),
  `src/Huddle.App/Acp/PersonaFrontmatter.cs` (`Parse`, line 467, is `internal static`).
- **Deliverable:** Create `tests/Huddle.Tests/Skills/SkillValidatorTests.cs`. Call
  `SkillValidator.Validate(string folderName, IReadOnlyDictionary<string,string> files)` →
  `SkillValidation`. Assert:
  - (a) valid input yields no Errors, `Name`, `Description`, and `Tools` = `[validate_teammate]`;
  - (b) no `SKILL.md` is an Error;
  - (c) `name` ≠ folder name is an Error;
  - (d) `name` `Team_Building` fails the regex `\A[a-z0-9]+(?:-[a-z0-9]+)*\z`, so an Error;
  - (e) a blank `description` is an Error;
  - (f) a description of 301 characters is a Warning; 501 is an Error;
  - (g) `tools: [post_message]` is a Warning and the tool is dropped from `Tools`;
  - (h) an unknown key `author:` is an Info;
  - (i) a file over 65,536 bytes is a Warning and is dropped from the returned file list.
- **Acceptance:** Fails to compile. **Red.**

### Task 1.2.i — Implement `SkillValidator` and the Skill records

- **Goal:** Implement **Spec §8.1**.
- **Read first:** Task 1.2.t, **Spec §6.2 (Inputs / outputs)** for the record shapes.
- **Deliverable:** Create in `src/Huddle.App/Skills/`:
  - `Skill.cs`:
    - `internal sealed record Skill(string Name, string Description, IReadOnlyList<string> Tools, IReadOnlyList<string> Files, SkillSource Source, string? FolderPath);`
    - `internal enum SkillSource { Default, Overridden, Yours }`
    - `internal enum SkillIssueSeverity { Info, Warning, Error }`
    - `internal sealed record SkillIssue(string Skill, string Message, SkillIssueSeverity Severity);`
    - `internal sealed record SkillResolution(IReadOnlyList<Skill> Skills, IReadOnlyList<string> Warnings);`
  - `SkillValidation.cs`:
    `internal sealed record SkillValidation(string? Name, string? Description, IReadOnlyList<string> Tools, IReadOnlyList<string> Files, IReadOnlyList<SkillIssue> Issues)`,
    with `bool HasErrors => Issues.Any(i => i.Severity == SkillIssueSeverity.Error)`.
  - `SkillValidator.cs`: an `internal static partial class` with a `[GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]` name rule.
    Parse `SKILL.md` with `PersonaFrontmatter.Parse` and apply §8.1 rules 1–7. The file list
    is `SKILL.md` first, then ordinal order. `Grantable` is referenced from `SkillGrants`
    (D4); until D4 lands, declare it here as
    `internal static readonly FrozenSet<string> Grantable` and move it in Task 4.1.i.
- **Acceptance:** 1.2.t green.

### Task 1.3.t — Test: every shipped Skill is valid (red → green in one step)

- **Goal:** Enforce **Spec §6.1 (Constraints)**: an invalid shipped Skill is a test failure,
  never a runtime warning.
- **Read first:** Tasks 1.1.i and 1.2.i.
- **Deliverable:** In `SkillCatalogTests` add `All_EveryShippedSkill_PassesValidationWithNoErrorsOrWarnings`.
  Show the red by temporarily changing `name:` in a **copy** of `SKILL.md` passed through
  `SkillValidator`; do not edit the shipped file.
- **Acceptance:** Green against the shipped files. The copy-based variant is shown failing in
  your hand-back note.

---

# D2 — `SkillStore`: resolution, overrides, watching

**Spec §6.2**, **Spec §7.1**, **Spec §7.3 (`Acp:SkillsDir`)**, **Spec §9**, **Spec §12
(F-2, F-3, F-6, F-7)**.

### Task 2.1.t — Test: an empty data directory yields the defaults (red)

- **Goal:** Pin **Spec §6.2**: defaults apply with no files on disk.
- **Read first:** **Spec §6.2**, `src/Huddle.App/Prompts/PromptStore.cs` (the pattern to mirror:
  `volatile` frozen snapshot, `Lock writeGate`, watcher, 500 ms debounce),
  `src/Huddle.App/Acp/AcpOptions.cs`, `tests/Huddle.Tests/TempDataDir.cs`.
- **Deliverable:** Create `tests/Huddle.Tests/Skills/SkillStoreTests.cs`. Test
  `All_EmptyDataDir_ReturnsTeamBuildingAsDefault`: `new SkillStore(dir.Options(), NullLogger<SkillStore>.Instance)`,
  then `Get("team-building")` has `Source == SkillSource.Default` and `FolderPath == null`.
  Test `Constructor_MissingSkillsDir_CreatesIt`.
- **Acceptance:** Fails to compile. **Red.**

### Task 2.1.i — Implement `SkillStore` construction and snapshot

- **Goal:** Implement **Spec §6.2** (read side).
- **Read first:** Task 2.1.t, **Spec §6.2 (Internal flow)**, **Spec §7.3**.
- **Deliverable:**
  1. `AcpOptions`: add `public string SkillsDir { get; set; } = "Skills";` with a `///` doc
     ("Relative to DataDir").
  2. Create `src/Huddle.App/Skills/SkillStore.cs`:
     `internal sealed class SkillStore : IDisposable`. Its constructor is
     `(IOptions<TeamOptions> options, ILogger<SkillStore> logger)`.
     - The directory is `Path.Combine(DataDir, Acp.SkillsDir)`, created if missing.
     - `Rebuild()` merges `SkillCatalog.All` with the disk folders and publishes a
       `volatile FrozenDictionary<string, Skill>` (ordinal) plus
       `IReadOnlyList<SkillIssue> Issues`.
     - Members: `IReadOnlyList<Skill> All` (ordinal by name), `Skill? Get(string name)`,
       `IReadOnlyList<SkillIssue> Issues`.
     - Keep the resolved file texts in a private `FrozenDictionary<string, FrozenDictionary<string,string>>`.
  3. Register in `src/Huddle.App/ServiceCollectionExtensions.cs` next to `PromptStore`
     (lines 92–93): `services.AddSingleton<SkillStore>();`.
- **Acceptance:** 2.1.t green.

### Task 2.2.t — Test: per-file overrides and Source classification (red)

- **Goal:** Pin **Spec §6.2 (per-file overlay)** and **Spec §12 F-2, F-3**.
- **Read first:** **Spec §6.2**, **Spec §8.1** (last two lines).
- **Deliverable:** Add to `SkillStoreTests`:
  - `Get_DiskOverridesRolesOnly_RolesFromDiskOthersDefault_SourceOverridden` (write `{Skills}/team-building/roles.md`);
  - `All_FolderWithNoDefault_IsYours`;
  - `All_InvalidYoursSkill_OmittedAndErrorInIssues`;
  - `Get_InvalidOverrideSkillMd_FallsBackToDefaultSkillMd_WarningInIssues`;
  - `All_SubFolderInsideSkill_IgnoredWithInfo`.
- **Acceptance:** Compiles; the new tests fail. **Red.**

### Task 2.2.i — Implement the overlay and fallback

- **Goal:** Implement **Spec §6.2** resolution and **Spec §8.1** fallback.
- **Read first:** Task 2.2.t.
- **Deliverable:** In `Rebuild()`, for each name in defaults ∪ disk, overlay disk files onto
  default files. Classify Source. Run `SkillValidator.Validate`. On Errors: if a default
  exists, re-validate with the default `SKILL.md` and record the Errors as Warnings; else omit
  the Skill and keep the Errors. `FolderPath` is the disk folder, or `null` for a Default with
  no folder.
- **Acceptance:** 2.2.t green; 2.1.t still green.

### Task 2.3.t — Test: `ReadFile` cannot escape the Skill (red)

- **Goal:** Pin **Spec §6.2 (Implementation notes)** and **Spec §12 F-7**.
- **Read first:** **Spec §6.6**, **Spec §12 F-7**.
- **Deliverable:** Theory `ReadFile_NameNotInFiles_ReturnsNull` with inline data `"../../Teams/x.md"`,
  `"..\\SKILL.md"`, `"C:\\Windows\\win.ini"`, `"/etc/passwd"`, `"SKILL"`, `"roles.MD"`.
  Fact `ReadFile_KnownFile_ReturnsResolvedText` (an override's text wins).
- **Acceptance:** **Red.**

### Task 2.3.i — Implement `ReadFile` by lookup only

- **Goal:** Implement **Spec §6.2** safe reads.
- **Read first:** Task 2.3.t.
- **Deliverable:** `internal string? ReadFile(string skill, string file)` looks up the resolved
  dictionary with ordinal comparison and **never** calls `Path.Combine` on `file`.
- **Acceptance:** 2.3.t green.

### Task 2.4.t — Test: watching and `SkillsChanged` (red)

- **Goal:** Pin **Spec §6.2 (watching)**, **Spec §9 (full rebuild, debounced)**, **Spec §12 F-6**.
- **Read first:** `src/Huddle.App/Prompts/PromptStore.cs` lines 149–159 and 499–565 (watcher,
  debounce, retry), `tests/Huddle.Tests/Prompts/` for how `PromptStore` watching is tested.
- **Deliverable:** `SkillsChanged_NewSkillFolderWritten_RaisedAndSkillVisible`. Write a valid
  `{Skills}/notes/SKILL.md`, then await a `TaskCompletionSource` set by `SkillsChanged`, with a
  5 s timeout. Assert `Get("notes")` is non-null and `Source == Yours`.
- **Acceptance:** **Red** (times out).

### Task 2.4.i — Implement the watcher

- **Goal:** Implement **Spec §6.2** watching.
- **Read first:** Task 2.4.t, **Spec §10** (the `SkillStore` row).
- **Deliverable:** `FileSystemWatcher(skillsDir, "*")` with `IncludeSubdirectories = true`,
  `InternalBufferSize = 64 * 1024`, and `NotifyFilter = LastWrite | FileName | DirectoryName`.
  Every event restarts a 500 ms `System.Threading.Timer`; `Error` schedules a rebuild. On the
  timer, rebuild with up to 3 attempts, 20 ms apart, on `IOException`; if all fail, keep the
  previous snapshot and log a Warning. Raise `internal event Action? SkillsChanged` **outside**
  any lock. `Dispose` detaches handlers and disposes the watcher and timer.
- **Acceptance:** 2.4.t green, and stable across 5 consecutive runs.

### Task 2.5.t — Test: Restore default (red)

- **Goal:** Pin **Spec §6.2 (RestoreDefault)** and use case **U10**.
- **Read first:** **Spec §2 U10**, **Spec §6.7 (Constraints)**.
- **Deliverable:** `RestoreDefault_OverriddenSkill_DeletesFolderAndSourceIsDefault`;
  `RestoreDefault_YoursSkill_ThrowsInvalidOperation`; `RestoreDefault_DefaultSkill_NoOp`.
- **Acceptance:** **Red.**

### Task 2.5.i — Implement `RestoreDefault`

- **Goal:** Implement **Spec §6.2**.
- **Read first:** Task 2.5.t.
- **Deliverable:** `internal void RestoreDefault(string name)`. Under `private readonly Lock writeGate = new();`,
  use only `Skill.FolderPath` from the snapshot; `Directory.Delete(path, recursive: true)`;
  `Rebuild()`; raise `SkillsChanged` outside the lock. Throw `InvalidOperationException` for a
  `Yours` Skill (there is no default to restore).
- **Acceptance:** 2.5.t green.

### Task 2.6.t — Test: `Resolve` (red)

- **Goal:** Pin **Spec §6.2 (`Resolve`)** and **Spec §12 F-1**.
- **Read first:** **Spec §6.4 (Internal flow)**.
- **Deliverable:** `Resolve_KnownAndUnknown_SkillsInOrderAndWarningForUnknown`: input
  `["team-building", "nope", "team-building"]` → one Skill; Warnings is
  `["Skill 'nope' does not exist."]`.
- **Acceptance:** **Red.**

### Task 2.6.i — Implement `Resolve`

- **Goal:** Implement **Spec §6.2**.
- **Read first:** Task 2.6.t.
- **Deliverable:** `internal SkillResolution Resolve(IReadOnlyList<string> names)` is pure over
  the current snapshot. It de-duplicates ordinally, keeping first-occurrence order.
- **Acceptance:** 2.6.t green.

---

# D3 — Persona `skills` and `_builtin`

**Spec §6.3**, **Spec §7.2 (Persona frontmatter additions)**, **Spec §14 D-15**.

### Task 3.1.t — Test: `skills` and `_builtin` parse (red)

- **Goal:** Pin **Spec §6.3 (Responsibilities)**.
- **Read first:** `src/Huddle.App/Acp/PersonaFrontmatter.cs` (key constants lines 30–34,
  `TryReadIdentity` lines 93–134, `SplitTeams` line 436), `src/Huddle.App/Acp/PersonaIdentity.cs`,
  `src/Huddle.App/Acp/PersonaEntry.cs`, `tests/Huddle.Tests/Acp/PersonaFrontmatterTests.cs`,
  **Spec §6.3**.
- **Deliverable:** Extend `PersonaFrontmatterTests`:
  - (a) `skills: [a, b]` and the block-list form both yield `Skills == ["a","b"]`;
  - (b) absent `skills` yields an empty list and the Persona is valid;
  - (c) `Skills:` matches case-insensitively;
  - (d) `_builtin: chief-of-staff` yields `Builtin == "chief-of-staff"`;
  - (e) absent `_builtin` yields `null`.
- **Acceptance:** **Red.**

### Task 3.1.i — Read the two keys

- **Goal:** Implement **Spec §6.3**.
- **Read first:** Task 3.1.t.
- **Deliverable:**
  - Add `private const string SkillsKey = "Skills";` and `private const string BuiltinKey = "_builtin";`.
  - Extend `PersonaIdentity` to `(string Name, string Title, string Alias, IReadOnlyList<string> Teams, string? Adapter = null, IReadOnlyList<string>? Skills = null, string? Builtin = null)`.
    Consumers treat a `null` `Skills` as empty.
  - Rename `SplitTeams` to `SplitList` and reuse it for `skills`.
  - Extend `PersonaEntry` with trailing `IReadOnlyList<string>? Skills = null, string? Builtin = null`,
    populated in `PersonaIndex.Build`.
  - **Do not touch the `Persona` record** (Spec §14 D-15).
- **Acceptance:** 3.1.t green; every existing `PersonaFrontmatterTests`, `PersonaIndexTests`
  and `PersonaStoreTests` case is still green.

### Task 3.2.t — Test: `skills` never reaches the job description (red)

- **Goal:** Pin **Spec §6.3** and the **Spec §7.2** "In `list_agents`?" column.
- **Read first:** `PersonaFrontmatter.ComposeJobDescription` (line 62),
  `JobDescriptionExcludedKeys` (line 47).
- **Deliverable:** A Persona with `skills: [team-building]`, `_builtin: chief-of-staff` and
  `consult_when: For research` yields a job description containing `Consult When: For research`
  and containing neither `skills` nor `builtin` (`OrdinalIgnoreCase`).
- **Acceptance:** **Red** (skills is present today).

### Task 3.2.i — Exclude `skills`

- **Goal:** Implement **Spec §6.3**.
- **Read first:** Task 3.2.t.
- **Deliverable:** Add `SkillsKey` to `JobDescriptionExcludedKeys`, and extend that field's
  `///` doc with the reason: which Skill files a Teammate loads is plumbing, like `adapter`.
- **Acceptance:** 3.2.t green.

### Task 3.3.t — Test: `Compose` writes `skills` and `_builtin`, and `WriteListField` (red)

- **Goal:** Pin **Spec §6.3** (Compose) and **Spec §6.7** (`WriteListField`).
- **Read first:** `PersonaFrontmatter.Compose` (lines 155–182), `WriteScalarField` (line 213).
- **Deliverable:**
  - `Compose_WithSkillsAndBuiltin_RoundTripsThroughTryReadIdentity`.
  - `Compose_EmptySkills_OmitsKey`.
  - `WriteListField_AddsReplacesAndRemoves`: add to a file without the key; replace an existing
    flow list; replace a block list; an empty list removes the key; the body is untouched.
- **Acceptance:** **Red.**

### Task 3.3.i — Implement `Compose` additions and `WriteListField`

- **Goal:** Implement **Spec §6.3** and **Spec §6.7**.
- **Read first:** Task 3.3.t.
- **Deliverable:**
  - `Compose` writes `skills: ['a', 'b']` after `adapter` when non-empty, and `_builtin: '<v>'`
    last when non-null, using the existing single-quote escaping.
  - Add `internal static string WriteListField(string personaText, string key, IReadOnlyList<string> values)`,
    modelled on `WriteScalarField`. It writes flow syntax, replaces either list syntax, and
    removes the key for an empty list.
- **Acceptance:** 3.3.t green.

### Task 3.4.t — Regression guard: a Persona refresh never restarts (green on arrival)

- **Goal:** Guard **Spec §14 D-15**.
- **Read first:** `src/Huddle.App/Acp/PersonaSupervisor.cs` (`NeedsRestart`, lines 250–251;
  `OnPersonasChanged`, lines 175–239), `tests/Huddle.Tests/Acp/PersonaSupervisorTests.cs`
  (setup pattern).
- **Deliverable:** `OnPersonasChanged_UnrelatedFileEvent_DoesNotRestartPersonaWithSkills`.
  Start a Persona with `skills: [team-building]`, touch a different Persona file, wait past
  the debounce, and assert `FakeAgentHostFactory.Calls.Count == 1`.
- **Acceptance:** Green. Show it red by temporarily adding a list member to `Persona`, and
  describe that in your hand-back note; do not commit that change.

---

# D4 — Skill Index, `SkillGrants`, `read_skill`

**Spec §6.4**, **Spec §6.5**, **Spec §6.6**, **Spec §14 D-9, D-11, D-12**.

### Task 4.1.t — Test: `SkillGrants.Offer` (red)

- **Goal:** Pin **Spec §6.5** and **Spec §8.2**.
- **Read first:** **Spec §6.5 (Internal flow)**, `src/Huddle.Acp/Abstractions/IAppTool.cs`.
- **Deliverable:** Create `tests/Huddle.Tests/Skills/SkillGrantsTests.cs` with a private
  `sealed class StubTool(string name) : IAppTool`. Table-driven `Offer_*` tests:
  - no Skills → no `read_skill`, `validate_teammate` or `propose_teammates`; every other tool
    is kept;
  - a Skill with `tools: [validate_teammate, propose_teammates]` → both offered, plus
    `read_skill`;
  - a Skill with no tools → `read_skill` only;
  - input order is preserved.
- **Acceptance:** **Red.**

### Task 4.1.i — Implement `SkillGrants`

- **Goal:** Implement **Spec §6.5**.
- **Read first:** Task 4.1.t.
- **Deliverable:** `src/Huddle.App/Skills/SkillGrants.cs`: `internal static class SkillGrants`
  with `internal static readonly FrozenSet<string> Grantable` (`validate_teammate`,
  `propose_teammates`, ordinal; move it here from `SkillValidator`) and
  `internal static IReadOnlyList<IAppTool> Offer(IReadOnlyList<IAppTool> all, IReadOnlyList<Skill> skills)`.
- **Acceptance:** 4.1.t green; `SkillValidatorTests` still green.

### Task 4.2.t — Test: the Skill Index in the system prompt (red)

- **Goal:** Pin **Spec §6.4** and **Spec §14 D-12**.
- **Read first:** `src/Huddle.App/Acp/SystemPromptComposer.cs` (lines 57–79),
  `src/Huddle.App/Prompts/PromptCatalog.cs` (`systemPrompt.*` entries; the
  `PromptDefinition` shape in `PromptDefinition.cs` lines 37–59),
  `tests/Huddle.Tests/Acp/PromptGoldenTests.cs`, `tests/Huddle.Tests/Acp/Golden/`, **Spec §6.4**.
- **Deliverable:** In `PromptGoldenTests` add `SystemPrompt_WithSkills_MatchesGolden`.
  Compose for `new Persona("Nova", "You are Nova.")` with one Skill
  (`team-building`, its real description) and `readSkillTool = "mcp__team__read_skill"`.
  Compare to the new golden `Golden/systemPrompt.skills.txt`. Add
  `SystemPrompt_NoSkills_UnchangedFromExistingGolden`, asserting the new overload with an
  empty Skill list equals `Golden/systemPrompt.txt`.
- **Acceptance:** **Red** (the golden is seeded on first run and the test fails, by design of
  `PromptGoldenTests`).

### Task 4.2.i — Implement `systemPrompt.skills` and the `Compose` overload

- **Goal:** Implement **Spec §6.4**.
- **Read first:** Task 4.2.t, **Spec §6.4** (Prompt default text and the key table), the
  regeneration procedure at the top of this plan.
- **Deliverable:**
  - Add to `PromptCatalog.BuildAll()`: `systemPrompt.skills` (Label "Skills block", Timing
    `NextSession`, Placeholders and RequiredPlaceholders `{{skillIndex}}`, `{{readSkillTool}}`),
    with the default text from **Spec §6.4** verbatim.
  - Add `tool.readSkill.description` (Timing `NextSession`):
    *"Reads one of your Skills: its main file, or a supporting file it names. Give the Skill's
    name, and optionally 'file'. Read a Skill before acting on it."*
  - Add an overload
    `Compose(Persona persona, IPromptSource prompts, string helpToolName, IReadOnlyList<string> toolNames, IReadOnlyList<Skill> skills, string readSkillToolName)`.
    It appends the rendered block after `tools` only when `skills.Count > 0`. `{{skillIndex}}`
    is `- {name}: {description}` lines joined with `\n`. The existing overload calls it with
    an empty list.
  - Regenerate `prompts.default.json`. Review the seeded golden, then commit it.
- **Acceptance:** 4.2.t green; `systemPrompt.txt` and `systemPrompt.unprefixed.txt`
  byte-identical; `PromptDefaultsFileTests` green.

### Task 4.3.t — Test: `read_skill` (red)

- **Goal:** Pin **Spec §6.6** and **Spec §5.3 Contract B**.
- **Read first:** **Spec §6.6**, `src/Huddle.App/Acp/Tools/CreateRoomTool.cs` (tool shape),
  `tests/Huddle.Tests/Acp/Tools/CreateRoomToolTests.cs` (test shape).
- **Deliverable:** Create `tests/Huddle.Tests/Acp/Tools/ReadSkillToolTests.cs`. Use a real
  `PersonaStore` in `TempDataDir` seeded with `Nova.md` holding `skills: [team-building]`,
  a real `SkillStore`, and `new ReadSkillTool(skillStore, personaStore, "Nova", new FakePromptSource())`.
  Tests:
  - held + no `file` → text starts with `[Skill: team-building · file: SKILL.md · also: ` and
    contains no leading `---` frontmatter;
  - `file: "roles.md"` → the roles text;
  - not held → `You do not hold the Skill 'x'. Your Skills: team-building.`;
  - unknown Skill held by name → `The Skill 'x' does not exist.`;
  - unknown file → lists the files;
  - `InputSchema` requires `name`;
  - `Name == "read_skill"`.
- **Acceptance:** **Red.**

### Task 4.3.i — Implement `ReadSkillTool`

- **Goal:** Implement **Spec §6.6**.
- **Read first:** Task 4.3.t.
- **Deliverable:** `src/Huddle.App/Acp/Tools/ReadSkillTool.cs`:
  `internal sealed class ReadSkillTool(SkillStore skills, PersonaStore personas, string callerPersonaName, IPromptSource prompts) : IAppTool`.
  Whether the caller holds the Skill is checked **live** via
  `personas.ResolveByNameOrAlias(callerPersonaName)?.Skills`. Strip frontmatter from
  `SKILL.md` with `PersonaFrontmatter.Parse(...).Body`. `Description` renders
  `tool.readSkill.description`. It never throws for expected failures.
- **Acceptance:** 4.3.t green.

---

# D5 — Wiring: factory, supervisor, health

**Spec §6.4 (Internal flow)**, **Spec §6.5 (Implementation notes)**, **Spec §12 F-1**.

### Task 5.1.t — Test: a Persona with a Skill is offered `read_skill` end to end (red)

- **Goal:** Pin **Spec §6.4** and **Spec §6.5** through the real factory, **Spec §14 D-11**.
- **Read first:** `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs` (lines 77–179, tool list
  104–133), `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`,
  `tests/Huddle.Tests/Conformance/ToolPrefixTests.cs` (`GetAppendedSystemPromptAsync`).
- **Deliverable:** Create `tests/Huddle.Tests/Conformance/SkillIndexConformanceTests.cs`.
  - `StartAsync_PersonaWithTeamBuilding_PromptNamesReadSkillAndTeamBuilding`: the appended
    system prompt contains `mcp__team__read_skill` and `- team-building: `.
  - `StartAsync_PersonaWithoutSkills_PromptHasNoSkillsBlockAndNoReadSkill`.
  - Extend `MockAdapterFixture` only as far as needed to register a `SkillStore` and pass a
    Persona text.
- **Acceptance:** **Red.**

### Task 5.1.i — Wire `SkillStore` and `SkillGrants` into the factory

- **Goal:** Implement **Spec §6.4** and **Spec §6.5** in `DotAcpAgentHostFactory`.
- **Read first:** Task 5.1.t, **Spec §6.4 (Implementation notes)**: keep
  `IAgentHostFactory.CreateAsync`'s signature.
- **Deliverable:**
  - Inject `SkillStore`.
  - Parse Skills from `persona.Text` with `PersonaFrontmatter.TryReadIdentity`, then
    `resolution = skillStore.Resolve(identity.Skills ?? [])`.
  - Add `ActivatorUtilities.CreateInstance<ReadSkillTool>(sp, persona.Name)` to the chat
    tools, then `chatTools = SkillGrants.Offer(chatTools, resolution.Skills)` **before**
    `new GetHelpTool(...)` (line 127).
  - Call the new `Compose` overload with `resolution.Skills` and `toolNamePrefix + "read_skill"`.
- **Acceptance:** 5.1.t green; `ToolPrefixTests`, `PromptDeliveryTests` and all `PromptGoldenTests` green.

### Task 5.2.t — Test: an unknown Skill reports Degraded (red)

- **Goal:** Pin **Spec §12 F-1** and use case **U14**.
- **Read first:** `PersonaSupervisor.StartHostIfMissingAsync` (lines 372–481; see how the
  Adapter warning becomes `Degraded`), `src/Huddle.App/Acp/PersonaHealth.cs`,
  `PersonaSupervisorTests` setup.
- **Deliverable:** `Start_UnknownSkill_ReportsDegradedWithReason`: a Persona with
  `skills: [nonexistent]` ends `Degraded` with Reason `Skill 'nonexistent' does not exist.`,
  and the factory was still called (the Persona started).
- **Acceptance:** **Red.**

### Task 5.2.i — Report Skill warnings

- **Goal:** Implement **Spec §6.4** (supervisor half).
- **Read first:** Task 5.2.t.
- **Deliverable:** Inject `SkillStore` into `PersonaSupervisor`. Before `CreateAsync`, resolve
  the entry's Skills and report each warning exactly as the Adapter warning is reported. If
  there are several, join them with a space into one Reason. Update every
  `new PersonaSupervisor(...)` in `PersonaSupervisorTests` and elsewhere in tests.
- **Acceptance:** 5.2.t green; every `PersonaSupervisorTests` case green.

---

# D6 — Skills UI

**Spec §6.7**, **Spec §2 U7, U8, U10**, **Spec §14 D-8**.

### Task 6.1.t — Test: the Teammate card's Skills picker (red)

- **Goal:** Pin **Spec §6.7** (card row) and **U7**.
- **Read first:** `src/Huddle.App/Components/Shared/TeammateCard.razor` (the Adapter select
  and its `WriteScalarField` call near line 1116; `SaveAsync` lines 1213–1241),
  `tests/Huddle.Tests/Ui/TeammateCardTests.cs` (`NewContext(factory)` lines 1370–1383;
  seeding files lines 1406–1410), **Spec §6.7**.
- **Deliverable:** Add `SkillStore` to the singletons `NewContext` copies. Tests:
  - `SkillsSelect_PickTeamBuilding_TextGainsSkillsField`;
  - `SkillsSelect_HelperText_SaysChangingRestarts`;
  - `SkillsSelect_AssignedSkillMissing_ShowsWarningChip`.
- **Acceptance:** **Red.**

### Task 6.1.i — Implement the picker

- **Goal:** Implement **Spec §6.7** (card).
- **Read first:** Task 6.1.t, `docs/agencyteam/language.md` (Skill).
- **Deliverable:** A `MudSelect<string>` with `MultiSelection="true"` over `SkillStore.All`,
  each option showing its description. On change,
  `this.text = PersonaFrontmatter.WriteListField(this.text, "skills", selected)`. Helper text:
  *"Changing Skills restarts this Teammate."* Subscribe to `SkillsChanged` in
  `OnInitialized`, unsubscribe in `Dispose`, and re-render with `InvokeAsync(StateHasChanged)`.
- **Acceptance:** 6.1.t green; every existing `TeammateCardTests` case green.

### Task 6.2.t — Test: Settings › Skills (red)

- **Goal:** Pin **Spec §6.7** (Settings row), **U8**, **U10**.
- **Read first:** `src/Huddle.App/Components/Settings/SettingsTab.cs`,
  `src/Huddle.App/Components/Pages/Settings.razor` (tab index mapping lines 97–141),
  `src/Huddle.App/Components/Settings/Personas.razor` (layout to copy),
  `tests/Huddle.Tests/Ui/SettingsPageTests.cs`.
- **Deliverable:**
  - `SkillsTab_ListsTeamBuildingAsDefault`;
  - `SkillsTab_OverriddenSkill_ShowsRestoreDefault_ClickRemovesFolder` (the confirm dialog
    names the folder);
  - `SkillsTab_InvalidYoursSkill_ShowsError`;
  - `SettingsRoute_TabSkills_SelectsSkillsTab`.
- **Acceptance:** **Red.**

### Task 6.2.i — Implement the tab

- **Goal:** Implement **Spec §6.7**.
- **Read first:** Task 6.2.t.
- **Deliverable:**
  - Add `Skills` to `SettingsTab` after `Personas`, with a `///` doc, and extend the
    index mapping in `Settings.razor` (index 3).
  - Create `src/Huddle.App/Components/Settings/SkillsPanel.razor`: a read-only table with
    Name, Description, Source, Folder, Files and Issues, plus **Restore default** on
    `Overridden` rows behind a `MudDialog` confirm that names `FolderPath`.
  - Add a caption with the Skills folder path and *"Write a new Skill as a folder here; it
    appears within a second."*
- **Acceptance:** 6.2.t green; every existing `SettingsPageTests` case green.

---

# D7 — S1 documentation

**Spec §14 (Where this spec changes ADR-0021)**, **Spec Appendix A S1-D**.

### Task 7.1 — Update the docs S1 changed

- **Goal:** Record S1 as built, per **Spec §14** and **Spec Appendix A (S1-D)**.
- **Read first:** `docs/adr/0021-a-skill-is-know-how-an-agent-reads-on-demand.md`,
  `docs/AgencyTeam.md` (Configuration and runtime-files tables), `docs/agencyteam/language.md`
  (Skill), `docs/agencyteam/rules.md`, `docs/agencyteam/manual-tests/prompts-settings.md`
  (lines 22 and 196, "24 keys"), **Spec §14 D-9, D-15**.
- **Deliverable:**
  - **ADR-0021:** replace "A tool that some Skill lists is offered only to Personas assigned
    that Skill" with the code-defined `Grantable` set (**Spec §6.5 Constraints**), and set
    `status: accepted`.
  - **`AgencyTeam.md`:** add `Acp:SkillsDir` to the config table and `{DataDir}/Skills/` to the
    runtime files table.
  - **`language.md`:** remove "Proposed, not built" from Skill.
  - **`rules.md`:** two rows. "Grantable tools are defined in code" (D-9), and "Never add a
    list member to the `Persona` record" (D-15), each with its why.
  - **`prompts-settings.md`:** update the key count to `PromptCatalog.All.Count`.
- **Acceptance:** Every link resolves; every edited file is CRLF.

---

# S2 — Team-building tools

---

# D8 — `PersonaStore`: dry-run check and serialised writes

**Spec §6.8 (Implementation notes)**, **Spec §14 D-14**.

### Task 8.1.t — Test: `PersonaStore.Check` reports every problem, writes nothing (red)

- **Goal:** Pin **Spec §6.8** ("Extract, do not copy").
- **Read first:** `src/Huddle.App/Acp/PersonaStore.cs` (`Add` lines 273–310,
  `ValidateCandidate` lines 428–445), `src/Huddle.App/Acp/PersonaIndex.cs` (`Build`, lines
  92–138), `tests/Huddle.Tests/Acp/PersonaStoreTests.cs`.
- **Deliverable:**
  - `Check_TwoNewTextsSameAlias_BothRejectedWithReasons`;
  - `Check_NewTextAliasEqualsExistingName_Rejected`;
  - `Check_ValidText_NoProblems_NoFileWritten`.
  - Signature under test:
    `internal IReadOnlyList<(string Text, string? Problem)> Check(IReadOnlyList<string> texts)`.
- **Acceptance:** **Red.**

### Task 8.1.i — Extract `Check`

- **Goal:** Implement **Spec §6.8** (store half).
- **Read first:** Task 8.1.t.
- **Deliverable:** Build one candidate `PersonaIndex` from the current entries plus every new
  text, each under a synthetic path `{teamsDir}/{Name}.md`, and map each new text's
  rejection. `ValidateCandidate` delegates to the same code.
- **Acceptance:** 8.1.t green; every `PersonaStoreTests` case green.

### Task 8.2.t — Test: concurrent Adds cannot both succeed on a collision (red)

- **Goal:** Pin **Spec §14 D-14**.
- **Read first:** Task 8.1.i, **Spec §6.10 (Implementation notes)**.
- **Deliverable:** `Add_ConcurrentSameAlias_ExactlyOneSucceeds`. Run 20 iterations. In each,
  two `Task.Run` Adds with different Names and the same Alias, released together from a
  `Barrier`. Assert one succeeds, one throws `ChatException`, and no rejected files exist
  afterwards.
- **Acceptance:** **Red** (intermittently fails today; 20 iterations make that reliable).

### Task 8.2.i — Serialise `Add` and `Update`

- **Goal:** Implement **Spec §14 D-14**.
- **Read first:** Task 8.2.t.
- **Deliverable:** Add `private readonly Lock writeGate = new();`. Hold it from the
  exists-check through `File.WriteAllText` in both `Add` and `Update`. **Do not** hold it
  while raising events: `RefreshIndexAndNotify` runs after the lock is released.
- **Acceptance:** 8.2.t green across 5 consecutive runs.

---

# D9 — Candidates and `validate_teammate`

**Spec §6.8**, **Spec §7.2 (Candidate)**, **Spec §8.3**, **Spec §12 F-8, F-9, F-24**,
**Spec §5.3 Contract B**.

### Task 9.1.t — Test: parsing a Candidate from JSON (red)

- **Goal:** Pin the **Spec §7.2 Candidate** table and **Spec §8.3** order 1.
- **Read first:** **Spec §7.2**, **Spec §8.3**, **Spec §14 D-5**.
- **Deliverable:** Create `tests/Huddle.Tests/Teammates/CandidateJsonTests.cs`, namespace
  `Agency.Huddle.Tests.Teammates`. Call
  `CandidateJson.TryParse(JsonNode? node, int index, out Candidate? candidate, List<string> problems)`.
  - valid → Candidate;
  - missing `alias` → `Candidate 1 is missing 'alias'.`;
  - `model` present → `A Candidate cannot set 'model'; the Human sets it on the Teammate card.`,
    and the same for `effort`, `adapter`, `skills`, `_builtin`;
  - `teams` not an array → a problem;
  - an unknown field `color` → `Candidate 1 has an unknown field 'color'.`
- **Acceptance:** **Red.**

### Task 9.1.i — Implement `Candidate` and `CandidateJson`

- **Goal:** Implement **Spec §7.2** (Candidate).
- **Read first:** Task 9.1.t.
- **Deliverable:** In `src/Huddle.App/Teammates/`, namespace `Agency.Huddle.App.Teammates`:
  - `Candidate.cs`:
    `public sealed record Candidate(string Name, string Alias, string Title, string Body, IReadOnlyList<string> Teams, string? ConsultWhen);`
    It is public because `ProposalCard.razor` renders it.
  - `CandidateJson.cs`: an `internal static class`. The field names are `name`, `alias`,
    `title`, `body`, `teams`, `consult_when`.
- **Acceptance:** 9.1.t green.

### Task 9.2.t — Test: `CandidateChecker` (red)

- **Goal:** Pin **Spec §6.8** and every row of **Spec §8.3**, plus **F-8, F-9, F-24**.
- **Read first:** **Spec §6.8 (Internal flow)**, `src/Huddle.Contracts/NameRules.cs`,
  `src/Huddle.App/Data/ITeamDirectory.cs` (`GetHumanAsync` line 9, `FindUserByNameAsync`
  line 49), `src/Huddle.App/Pipes/IAgentGateway.cs` (`bool IsOnline(string agentId)`, line 9).
- **Deliverable:** Create `tests/Huddle.Tests/Teammates/CandidateCheckerTests.cs`, using a real
  `PersonaStore`, a `SqliteTeamDirectory` and a `FakeAgentGateway`. Assert one problem
  string per case, and that **all** problems return together for a Candidate with three faults:
  - Name `Vera.`;
  - blank Title;
  - Body starting with `---`;
  - `teams` containing `a,b`;
  - two Candidates sharing an Alias (both named);
  - an Alias equal to an existing Persona's Name;
  - Name equal to the Human's Name `You`;
  - Name `echo` while an `echo` Agent is connected and has no Persona file;
  - `{Name}.md` already on disk.
- **Acceptance:** **Red.**

### Task 9.2.i — Implement `CandidateChecker`

- **Goal:** Implement **Spec §6.8**.
- **Read first:** Task 9.2.t, **Spec §6.8 (Inputs / outputs)**.
- **Deliverable:**
  - `src/Huddle.App/Teammates/CandidateChecker.cs`:
    `internal sealed class CandidateChecker(PersonaStore personas, ITeamDirectory directory, IAgentGateway gateway)`,
    with `internal Task<CandidateCheck> CheckAsync(IReadOnlyList<Candidate> candidates, CancellationToken ct)`.
  - `public sealed record CandidateCheck(bool IsValid, IReadOnlyList<string> Problems);`
  - Compose each Candidate's text with `PersonaFrontmatter.Compose`, then
    `WriteScalarField(text, "consult_when", …)` when set, then `personas.Check(texts)`.
  - Register it as a singleton.
- **Acceptance:** 9.2.t green.

### Task 9.3.t — Test: `validate_teammate` (red)

- **Goal:** Pin **Spec §5.3 Contract B** for `validate_teammate`.
- **Read first:** Task 9.2.i, `CreateRoomToolTests.cs`.
- **Deliverable:** Create `tests/Huddle.Tests/Acp/Tools/ValidateTeammateToolTests.cs`:
  - `InvokeAsync_Valid_ReturnsValid` (exactly `Valid.`);
  - `InvokeAsync_TwoProblems_ReturnsOneLinePerProblem`;
  - `InvokeAsync_MissingCandidate_ReturnsProblem`;
  - `Name_IsValidateTeammate`.
- **Acceptance:** **Red.**

### Task 9.3.i — Implement `ValidateTeammateTool`

- **Goal:** Implement **Spec §6.8** (tool).
- **Read first:** Task 9.3.t, the regeneration procedure at the top of this plan.
- **Deliverable:**
  - `src/Huddle.App/Acp/Tools/ValidateTeammateTool.cs`:
    `internal sealed class ValidateTeammateTool(CandidateChecker checker, IPromptSource prompts) : IAppTool`.
    `InputSchema` has `candidate` (an object with the six properties; `name`, `alias`,
    `title`, `body` required).
  - Add `tool.validateTeammate.description` (Timing `NextSession`):
    *"Checks one proposed Teammate without creating anything. Returns 'Valid.' or every
    problem, one per line. Free: call it until the Candidate is clean."*
  - Add the tool to `DotAcpAgentHostFactory`'s chat tools. Until D5 lands it is offered to
    everyone; after D5, `SkillGrants` gates it.
  - Regenerate `prompts.default.json`. Update the tool-name lists in `PromptDefaultsTests`
    and `ToolNamesTests`.
- **Acceptance:** 9.3.t green; `PromptDefaultsFileTests`, `PromptDefaultsTests` and
  `ToolNamesTests` green.

---

# D10 — Proposals and `propose_teammates`

**Spec §6.9**, **Spec §8.4**, **Spec §7.3 (`Acp:MaxTeammates`)**, **Spec §12 F-16, F-20**.

### Task 10.1.t — Test: `ProposalStore` (red)

- **Goal:** Pin **Spec §6.9** and **Spec §8.4**.
- **Read first:** **Spec §6.9 (Inputs / outputs)**, **Spec §8.4**,
  `src/Huddle.App/Services/RoomEvents.cs` (event pattern, lines 24–58).
- **Deliverable:** Create `tests/Huddle.Tests/Teammates/ProposalStoreTests.cs`:
  - `TryPut_Empty_Stored`;
  - `TryPut_SameProposer_Replaced`;
  - `TryPut_OtherProposer_RefusedWithExisting`;
  - `TryTake_MatchingId_ReturnsOnceThenNull`;
  - `TryTake_StaleId_ReturnsNull`;
  - `Drop_RemovesAndRaises`;
  - `TryPut_RaisesProposalChangedWithRoomId`.
- **Acceptance:** **Red.**

### Task 10.1.i — Implement `ProposalStore`

- **Goal:** Implement **Spec §6.9**.
- **Read first:** Task 10.1.t.
- **Deliverable:**
  - `src/Huddle.App/Teammates/Proposal.cs`:
    - `public sealed record Proposal(string Id, string RoomId, string ProposerAgentId, string ProposerName, IReadOnlyList<Candidate> Candidates, DateTimeOffset ProposedAt);`
    - `internal enum ProposalPutResult { Stored, Replaced, Refused }`
    - `internal sealed record ProposalPut(ProposalPutResult Result, Proposal? Existing);`
  - `ProposalStore.cs`: `internal sealed class ProposalStore(RoomEvents events)` holding a
    `Dictionary<string, Proposal>` (ordinal) under `private readonly Lock gate = new();`.
    Methods: `Get`, `TryPut`, `TryTake(roomId, proposalId)`, `Drop`.
  - Add `public event Action<string>? ProposalChanged;` and an
    `internal void PublishProposalChanged(string roomId)` to `RoomEvents`. It is raised outside
    the lock.
  - Register `ProposalStore` as a singleton.
- **Acceptance:** 10.1.t green.

### Task 10.2.t — Test: `propose_teammates` (red)

- **Goal:** Pin **Spec §6.9 (Internal flow)**, **Spec §5.3 Contract B**, and use cases **U3, U6**.
- **Read first:** **Spec §6.9**, `src/Huddle.App/Data/ITeamDirectory.cs` (`GetRoomAsync` line
  72, `GetRoomMembersAsync` line 74, `Room.Archived`), `CreateRoomToolTests.cs`.
- **Deliverable:** Create `tests/Huddle.Tests/Acp/Tools/ProposeTeammatesToolTests.cs`. Set
  `MaxTeammates = 8` through `TeamOptions`. Cases:
  - unknown Room → `Unknown room 'x'.`;
  - caller not a Member;
  - Room Archived → `That Room is Archived.`;
  - an invalid Candidate → its problems;
  - five Personas plus four Candidates → `5 Teammates exist and the limit is 8; propose at most 3.`;
  - `MaxTeammates = 0` → no limit;
  - success → text starts `Proposed 2 Teammates (Vera, Quill) in Room`, and
    `ProposalStore.Get(roomId)` holds them;
  - the same caller proposing again → replaced;
  - another caller → `A Proposal from Nova is already waiting in this Room.`
- **Acceptance:** **Red.**

### Task 10.2.i — Implement `ProposeTeammatesTool` and `MaxTeammates`

- **Goal:** Implement **Spec §6.9** and **Spec §7.3**.
- **Read first:** Task 10.2.t, the regeneration procedure at the top of this plan.
- **Deliverable:**
  - `AcpOptions`: add `public int MaxTeammates { get; set; } = 8;` with a `///` doc saying
    zero or less disables it.
  - `src/Huddle.App/Acp/Tools/ProposeTeammatesTool.cs`:
    `internal sealed class ProposeTeammatesTool(ProposalStore proposals, CandidateChecker checker, PersonaStore personas, ITeamDirectory directory, IOptions<TeamOptions> options, TimeProvider clock, string callerAgentId, IPromptSource prompts) : IAppTool`.
    `Proposal.Id` is `Guid.NewGuid().ToString("N")`; `ProposedAt` is `clock.GetUtcNow()`.
    The success text is as in **Spec §6.9**.
  - Add `tool.proposeTeammates.description` (Timing `NextSession`):
    *"Asks the Human to approve new Teammates. Give the id of the Room you are talking in and
    one to four Candidates. Nothing is created until the Human approves; you will be told
    the outcome in that Room."*
  - Add the tool to the factory's chat tools with `agentId`. Regenerate
    `prompts.default.json`. Update `PromptDefaultsTests` and `ToolNamesTests`.
  - `TimeProvider` is already registered (`ServiceCollectionExtensions.cs` line 53); inject it.
- **Acceptance:** 10.2.t green; the prompt and tool-name tests are green.

### Task 10.3.t — Test: archiving or deleting a Room drops its Proposal (red)

- **Goal:** Pin **Spec §12 F-16**.
- **Read first:** `src/Huddle.App/Services/ChatService.cs` (`SetRoomArchivedAsync` line 490,
  `DeleteRoomAsync` line 510), `tests/Huddle.Tests/Services/ChatServiceTests.cs`.
- **Deliverable:** `SetRoomArchived_True_DropsProposal`; `DeleteRoom_DropsProposal`;
  `SetRoomArchived_False_KeepsProposal`.
- **Acceptance:** **Red.**

### Task 10.3.i — Drop on archive and delete

- **Goal:** Implement **Spec §6.9 (Implementation notes)**.
- **Read first:** Task 10.3.t.
- **Deliverable:** Inject `ProposalStore` into `ChatService`. Call `Drop(roomId)` after a
  successful archive (`true`) or delete. Update every `new ChatService(...)` in tests,
  including the `CreateChatService` helpers in `tests/Huddle.Tests/Acp/Tools/`.
- **Acceptance:** 10.3.t green; the full suite green.

---

# D11 — Approve and Decline

**Spec §6.10**, **Spec §8.5**, **Spec §12 F-10, F-11, F-17**, **Spec §14 D-1, D-7, D-13**.

### Task 11.1.t — Functional test: Approve creates all and wakes the proposer (red)

- **Goal:** Pin **Spec §6.10** (happy path) and use case **U2**.
- **Read first:** **Spec §6.10**, **Spec §8.5**, `ChatService.PostAsync` (line 141),
  `ITeamDirectory.GetHumanAsync`.
- **Deliverable:** Create `tests/Huddle.Tests/Teammates/ProposalServiceTests.cs`, using a real
  `PersonaStore`, `SqliteTeamDirectory`, `FileChatStore`, `ChatService` and `ProposalStore` in
  one `TempDataDir`. Test `Approve_ThreeValid_CreatesAllAndPostsHumanMessageMentioningProposer`:
  - three files exist;
  - `Outcome.Kind == Created`;
  - the Room's last Message is from the Human with text
    `Approved. Created Vera, Quill and Iris. @Chief of Staff go ahead.`;
  - the store is empty.
- **Acceptance:** **Red.**

### Task 11.1.i — Implement `ApproveAsync` (happy path)

- **Goal:** Implement **Spec §6.10**.
- **Read first:** Task 11.1.t, **Spec §6.10 (Inputs / outputs)**, and the outcome template
  table.
- **Deliverable:**
  - `src/Huddle.App/Teammates/ProposalService.cs`: an internal sealed class with the
    constructor from **Spec §6.10**, plus
    `internal Task<ProposalOutcome> ApproveAsync(string roomId, string proposalId, CancellationToken ct)`.
  - In `Proposal.cs`:
    - `public enum ProposalOutcomeKind { Created, PartlyCreated, NoneCreated, OverLimit, Declined, Gone }`
    - `public sealed record ProposalOutcome(ProposalOutcomeKind Kind, IReadOnlyList<string> Created, IReadOnlyList<CandidateFailure> Failed, string PostedText);`
    - `public sealed record CandidateFailure(string Name, string Reason);`
  - Serialise creation with an app-wide `private static readonly SemaphoreSlim CreationGate = new(1, 1);`.
  - Name lists read "A", "A and B", "A, B and C".
  - The texts are `const` fields in `ProposalService`, **not** Prompts (**Spec §14 D-13**).
  - Raise `ProposalChanged` after the take.
  - Register it as a singleton.
- **Acceptance:** 11.1.t green.

### Task 11.2.t — Functional test: partial, over-limit, gone, renamed (red)

- **Goal:** Pin **Spec §8.5** invariants, **Spec §14 D-7**, **F-10, F-11, F-17**, and **U5**.
- **Read first:** **Spec §6.10 (Internal flow, Constraints)**.
- **Deliverable:**
  - `Approve_OneCollidesSinceProposing_PartlyCreatedWithReason` (write a colliding Persona
    after proposing);
  - `Approve_OverLimit_CreatesNoneAndSaysBy`;
  - `Approve_SecondCall_GoneAndPostsNothing`;
  - `Approve_ProposerRenamed_MentionsNewName`;
  - `Approve_ProposerDeleted_NoMention`.
- **Acceptance:** **Red.**

### Task 11.2.i — Implement the non-happy paths

- **Goal:** Implement **Spec §6.10** in full.
- **Read first:** Task 11.2.t.
- **Deliverable:**
  - Re-check each Candidate with `CandidateChecker` inside the gate.
  - Check the limit before creating anything.
  - A `null` `TryTake` returns `Gone` without posting.
  - Resolve the proposer's current Name by `ProposerAgentId` through `ITeamDirectory` at post
    time; if it is missing, omit the Mention.
  - Log, but do not throw, when `PostAsync` fails after creation.
- **Acceptance:** 11.2.t green; 11.1.t still green.

### Task 11.3.t — Functional test: Decline (red)

- **Goal:** Pin **Spec §6.10** (Decline) and **U4**.
- **Read first:** The outcome template table in **Spec §6.10**.
- **Deliverable:** `Decline_PostsDeclinedNamesAndMentionsProposer_CreatesNothing`;
  `Decline_SecondCall_Gone`.
- **Acceptance:** **Red.**

### Task 11.3.i — Implement `DeclineAsync`

- **Goal:** Implement **Spec §6.10**.
- **Read first:** Task 11.3.t.
- **Deliverable:** `internal Task<ProposalOutcome> DeclineAsync(string roomId, string proposalId, CancellationToken ct)`.
- **Acceptance:** 11.3.t green.

### Task 11.4.t — Functional test: the posted Message wakes the proposer in a group Room (red → verify)

- **Goal:** Prove **Spec §14 D-1**'s claim that the ordinary Reply Gate wakes the proposer.
- **Read first:** `src/Huddle.App/Services/MentionParser.cs` (longest handle first),
  `src/Huddle.App/Pipes/AgentGateway.cs` line 129 (`MessagePosted` per recipient),
  `src/Huddle.App/Acp/ReplyGate.cs`, `tests/Huddle.Tests/Acp/Tools/FakeAgentGateway.cs`.
- **Deliverable:** `Approve_InGroupRoom_ProposerDeliveryIsMentioned`. Use a four-Member Room
  (the Human, the proposer and two others), and a proposer Name with a space
  (`Chief of Staff`). Assert that the delivery to the proposer carries it in `Mentions`, and
  that `ReplyGate.Decide(...)` returns reply.
- **Acceptance:** Green, or red with a precise finding. If red, adjust only the Mention form in
  `ProposalService` (for example, Mention by Alias) and record the finding in your hand-back.

---

# D12 — The Proposal card

**Spec §6.11**, **Spec §12 F-11, F-16**.

### Task 12.1.t — Test: `ProposalCard` renders and acts (red)

- **Goal:** Pin **Spec §6.11**.
- **Read first:** `src/Huddle.App/Components/Pages/Chat.razor` (the Budget prompt, lines
  107–138; `OnMessagePosted`, lines 310–347), `tests/Huddle.Tests/Ui/MudBunitContext.cs`,
  **Spec §6.11**.
- **Deliverable:** Create `tests/Huddle.Tests/Ui/ProposalCardTests.cs`:
  - `Renders_CandidatesAndHeadroom` (text `This adds 2 Teammates; 5 of 8 exist.`);
  - `Approve_CallsServiceAndCardDisappears`;
  - `OtherTabApproves_CardDisappearsOnProposalChanged`;
  - `ArchivedRoom_RendersNothing`;
  - `Approve_Busy_ButtonsDisabled`.
- **Acceptance:** **Red.**

### Task 12.1.i — Implement `ProposalCard.razor` and wire it into `Chat.razor`

- **Goal:** Implement **Spec §6.11**.
- **Read first:** Task 12.1.t, `docs/agencyteam/language.md` (Proposal, Candidate, Teammate).
- **Deliverable:**
  - `src/Huddle.App/Components/Shared/ProposalCard.razor`, with parameter `[Parameter] public string RoomId`.
  - Show the proposer, a `MudTable` of Candidates, and the Body in a `MudExpansionPanel`.
  - Copy: *"{Proposer} proposes new Teammates. Approve creates these Teammates."*
  - **Approve** / **Decline** buttons, busy-flagged.
  - Subscribe to `RoomEvents.ProposalChanged` and unsubscribe in `Dispose`.
  - Render nothing when there is no Proposal or the Room is Archived.
  - In `Chat.razor`, place `<ProposalCard RoomId="@this.roomId" />` directly above the Budget
    prompt block.
- **Acceptance:** 12.1.t green; every existing Chat UI test green.

---

# D13 — S2 documentation and goldens

**Spec Appendix A (S2-D, S2-T13)**, **Spec §12 F-19**.

### Task 13.1.t — Test: the tool-descriptions golden includes the new tools (red)

- **Goal:** Pin **Spec Appendix A S2-T13**.
- **Read first:** `PromptGoldenTests.cs` (the hand-built tool set, lines 172–196),
  `Golden/toolDescriptions.txt`.
- **Deliverable:** Add `ValidateTeammateTool` and `ProposeTeammatesTool` to the hand-built
  set. Delete `toolDescriptions.txt` so it re-seeds.
- **Acceptance:** **Red** (the seeded golden fails the first run).

### Task 13.1.i — Accept the golden

- **Goal:** Complete **Spec Appendix A S2-I13**.
- **Read first:** Task 13.1.t.
- **Deliverable:** Review the seeded file, confirm it has no `mcp__team__` inside any
  description, and commit it.
- **Acceptance:** 13.1.t green.

### Task 13.2 — Update the docs S2 changed

- **Goal:** Record S2 as built, per **Spec Appendix A (S2-D)**.
- **Read first:** `docs/agencyteam/language.md` (Proposal, Candidate),
  `docs/AgencyTeam.md` (config table), `docs/agencyteam/known-limits.md`,
  `docs/adr/0006-a-room-has-a-budget-for-agent-replies.md`.
- **Deliverable:**
  - Remove "Proposed, not built" from Proposal.
  - Add `Acp:MaxTeammates` to the config table.
  - Add a Known-limits entry: *"A waiting Proposal does not survive a restart, and the
    proposer is not told"* (**Spec §12 F-19**).
  - Add a one-line cross-reference in ADR-0006 that the Proposal reuses the Budget's view-state
    pattern.
- **Acceptance:** Links resolve; files are CRLF.

---

# S3 — Content, the Chief of Staff, the Greeting

---

# D14 — The `team-building` content tests

**Spec §6.13**, **Spec Appendix A S3-T1** (the content exists already: show red against the
pre-§6.13 draft).

### Task 14.1.t — Test: the shipped content honours its contract (red against the old draft)

- **Goal:** Pin **Spec §6.13 (Constraints)**, **Spec §6.5**, **Spec §7.2**.
- **Read first:** **Spec §6.13**, `src/Huddle.App/Skills/Defaults/team-building/*.md`,
  `tests/Huddle.Tests/Skills/SkillCatalogTests.cs` (from D1).
- **Deliverable:** Add to `SkillCatalogTests`:
  - `TeamBuilding_Tools_AreExactlyTheGrantableTwo`;
  - `TeamBuilding_NoFileContainsToolPrefix` (`mcp__`);
  - `TeamBuilding_SkillMd_NamesProposeTeammates_NeverCreateTeammate`;
  - `TeamBuilding_EverySupportingFile_IsNamedInSkillMd`;
  - `TeamBuilding_Description_AtMost300Chars`;
  - `TeamBuilding_SkillMdStep0_KeysOnGreetingTurn`: `SKILL.md` contains "greet" and "has not
    written" (the wording agreed in `Conversation/2026-09-22-1510-…`).
  - Put the assertions in one `private static IReadOnlyList<string> ContentContractViolations(IReadOnlyDictionary<string,string> files)`
    helper. Each test above asserts that the list has no entry of its kind for
    `SkillCatalog.All["team-building"]`. Add one permanent test,
    `ContentContract_PreSpecDraft_ReportsViolations`: it builds an in-test copy whose `SKILL.md`
    frontmatter says `tools: [validate_teammate, create_teammate]` and whose body names
    `create_teammate`, then asserts the helper reports both. That test is the durable proof
    the checks can fail.
- **Acceptance:** `ContentContract_PreSpecDraft_ReportsViolations` is green. Against the shipped
  files, every test is green **except** possibly `…Step0…`, which stays red until the content
  session applies the agreed wording.

### Task 14.1.i — Close any gap in the content

- **Goal:** Complete **Spec §6.13** (S3-I1).
- **Read first:** Task 14.1.t results; `Conversation/` for the content session's latest reply.
- **Deliverable:** If only `…Step0…` is red, apply the agreed step 0 case (a) wording from
  `Conversation/2026-09-22-1510-re-team-building-content-vs-skills-spec.md` to `SKILL.md`.
  **Do not otherwise rewrite the content** (**Spec §6.13 Status**). Preserve CRLF and UTF-8
  without a BOM.
- **Acceptance:** 14.1.t green.

---

# D15 — The built-in Chief of Staff

**Spec §6.12**, **Spec §8.6**, **Spec §12 F-12 to F-15**, **Spec §14 D-3, D-4**, use cases
**U11 to U13, U15**.

### Task 15.1.t — Test: an empty library gets the Chief of Staff (red)

- **Goal:** Pin **Spec §6.12** and **U15**.
- **Read first:** **Spec §6.12**, **Spec §8.6**, `src/Huddle.App/Acp/PersonaStore.cs` (`Add`).
- **Deliverable:** Create `tests/Huddle.Tests/Teammates/BuiltinTeammateSeederTests.cs`. Test
  `Start_EmptyLibrary_WritesChiefOfStaffWithMarkerAndSkill`: after `StartAsync`, the entry
  `Chief of Staff` has Alias `cos`, `Builtin == "chief-of-staff"`, and
  `Skills == ["team-building"]`.
- **Acceptance:** **Red.**

### Task 15.1.i — Implement the seeder and embed the default

- **Goal:** Implement **Spec §6.12**.
- **Read first:** Task 15.1.t.
- **Deliverable:**
  - Create `src/Huddle.App/Builtin/chief-of-staff.md`, embedded with
    `<EmbeddedResource Include="Builtin\*.md" LogicalName="Builtin/%(Filename)%(Extension)" />`.
    It carries full frontmatter (`name: 'Chief of Staff'`, `title: 'Chief of Staff'`,
    `alias: 'cos'`, `skills: ['team-building']`, `_builtin: 'chief-of-staff'`) and a short
    Body in the second person: who it is, that it helps the Human assemble Teammates, and that
    it reads its Skill before acting.
  - Create `src/Huddle.App/Teammates/BuiltinTeammate.cs`: an
    `internal static class BuiltinTeammate` with `const string ChiefOfStaffMarker = "chief-of-staff"`
    and `static string DefaultText { get; }`.
  - Create `BuiltinTeammateSeeder.cs`: `internal sealed class BuiltinTeammateSeeder(PersonaStore personas, ILogger<BuiltinTeammateSeeder> logger) : IHostedService`.
    In `StartAsync`, add the default only if no entry has the marker. `StopAsync` does nothing.
- **Acceptance:** 15.1.t green.

### Task 15.2.t — Test: marker, free names, no reverting (red)

- **Goal:** Pin **Spec §8.6**, **F-13, F-14, F-15**, **U11**.
- **Read first:** **Spec §12**, rows F-12 to F-15.
- **Deliverable:**
  - `Start_MarkerUnderOtherName_WritesNothing` (Alfred carries the marker);
  - `Start_NameTaken_WritesChiefOfStaff2WithAliasCos2`;
  - `Start_EditedChiefOfStaff_NotReverted`;
  - `Start_TwoFilesWithMarker_WritesNothing`;
  - `Start_WriteFails_LogsAndDoesNotThrow`.
- **Acceptance:** **Red.**

### Task 15.2.i — Implement detection and the free-name search

- **Goal:** Implement **Spec §8.6**.
- **Read first:** Task 15.2.t.
- **Deliverable:** `n = 1, 2, …`: Name `Chief of Staff` / `Chief of Staff {n}`, Alias
  `cos` / `cos{n}`. A pair is free when it is not a Name or Alias of any entry and no
  `{Name}.md` exists. Catch `ChatException` and `IOException` around `Add`, log an Error, and
  return.
- **Acceptance:** 15.2.t green.

### Task 15.3.t — Test: the seeder runs before the supervisor (red)

- **Goal:** Pin **Spec §10 (ordering 1)**.
- **Read first:** `src/Huddle.App/ServiceCollectionExtensions.cs` (hosted services, lines
  113–140), `tests/Huddle.Tests/ServiceCollectionExtensionsTests.cs`.
- **Deliverable:** `HostedServices_SeederRegisteredAfterDataInitializerAndBeforeSupervisor`.
  Resolve `IEnumerable<IHostedService>` and assert the index order.
- **Acceptance:** **Red.**

### Task 15.3.i — Register the seeder

- **Goal:** Implement **Spec §6.12 (Implementation notes)**.
- **Read first:** Task 15.3.t.
- **Deliverable:** `services.AddHostedService<BuiltinTeammateSeeder>();` immediately before
  the `PersonaSupervisor` hosted-service line (124).
- **Acceptance:** 15.3.t green; the full suite green.

### Task 15.4.t — Test: Reset to default on the card (red)

- **Goal:** Pin **Spec §6.12** (Reset) and **U13**.
- **Read first:** `TeammateCard.razor` (Remove button, lines 146–159), `TeammateCardTests`.
- **Deliverable:**
  - `BuiltinTeammate_ShowsResetNotRemove`;
  - `Reset_RestoresDefaultsKeepsNameAndAlias` (rename to Alfred, edit the Body, set a Model,
    then Reset: the Body, Title, Teams, Skills, Model and Effort return to default, while the
    Name Alfred and Alias are kept);
  - `NonBuiltin_StillShowsRemove`.
- **Acceptance:** **Red.**

### Task 15.4.i — Implement Reset

- **Goal:** Implement **Spec §6.12**.
- **Read first:** Task 15.4.t, `PersonaStore.Update` (line 333).
- **Deliverable:**
  - Add `internal void ResetToDefault(string currentName)` on `BuiltinTeammateSeeder`, or on a
    small `BuiltinTeammateReset` singleton if the seeder must stay hosted-only. It takes
    `DefaultText`, replaces `name` and `alias` with the entry's values via `WriteScalarField`,
    and calls `personas.Update(currentName, text, model: null, effort: null)`.
  - On the card, when `entry.Builtin == ChiefOfStaffMarker`, replace the Remove control with
    **Reset to default**, using the same inline confirm pattern. The confirm copy is from
    **Spec §6.12**.
- **Acceptance:** 15.4.t green; the Remove tests still green.

---

# D16 — The Greeting

**Spec §6.14**, **Spec §7.2 (`RoomInfo` on the wire)**, **Spec §10 (ordering 3)**, **Spec §12
F-25 to F-31**, **Spec §14 D-16 to D-18**, use case **U16**. Read `docs/agencyteam/traps.md`
first.

### Task 16.1.t — Test: `RoomInfo.IsEmpty` on the wire (red)

- **Goal:** Pin **Spec §7.2 (`RoomInfo`)**: additive, with no version bump.
- **Read first:** `src/Huddle.Contracts/Messages.cs` (line 7 `RoomInfo`, line 75 `Welcome`),
  `src/Huddle.Contracts/ProtocolVersion.cs`, `tests/Huddle.Tests/Contracts/ProtocolJsonTests.cs`,
  `docs/agencyteam/traps.md` lines 56–73.
- **Deliverable:**
  - `Welcome_RoomInfoIsEmpty_SerialisesAsLiteralJson`: the literal contains `"isEmpty":true`.
  - `Welcome_IsEmptyAbsent_DeserialisesFalse`.
  - `ProtocolVersion_StillThree`.
- **Acceptance:** **Red.**

### Task 16.1.i — Add `IsEmpty`

- **Goal:** Implement **Spec §7.2**.
- **Read first:** Task 16.1.t.
- **Deliverable:** Change the record to
  `public sealed record RoomInfo(string Id, string Name, IReadOnlyList<MemberInfo> Members, bool IsEmpty = false);`
  with a `///` doc explaining that absent means "not empty". Update any literal-JSON
  expectations in `ProtocolJsonTests` that serialise a `Welcome`. **Do not** change
  `ProtocolVersion.Current`.
- **Acceptance:** 16.1.t green; `tools/echo-bot.ps1` is unaffected (it ignores unknown fields;
  confirm by reading it).

### Task 16.2.t — Functional test: the server fills `IsEmpty` (red)

- **Goal:** Pin **Spec §6.14** (server half).
- **Read first:** `src/Huddle.App/Pipes/AgentConnection.cs` (lines 195–210),
  `src/Huddle.App/Data/IChatStore.cs`, `src/Huddle.App/Data/FileChatStore.cs`,
  `tests/Huddle.Tests/Pipes/PipeEndToEndTests.cs`, `PipeHostFixture.cs`,
  `tests/Huddle.Tests/Data/FileChatStoreTests.cs`.
- **Deliverable:**
  - `FileChatStoreTests.HasMessagesAsync_NoFile_False`, `…_AfterAppend_True`.
  - `PipeEndToEndTests.Welcome_NewAgentRoom_IsEmptyTrue` and
    `…_AfterOneMessage_ReconnectIsEmptyFalse`.
- **Acceptance:** **Red.**

### Task 16.2.i — Implement `HasMessagesAsync` and fill the field

- **Goal:** Implement **Spec §6.14**.
- **Read first:** Task 16.2.t.
- **Deliverable:**
  - Add `Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default);` to
    `IChatStore`.
  - In `FileChatStore`, return `File.Exists(path) && new FileInfo(path).Length > 0` without
    reading the file.
  - Implement the new member in any test fakes of `IChatStore`.
  - In `AgentConnection`, set `IsEmpty: !await chatStore.HasMessagesAsync(room.Id, ct)` per Room.
- **Acceptance:** 16.2.t green.

### Task 16.3.t — Test: the runner queues exactly one Greeting (red)

- **Goal:** Pin **Spec §6.14 (Internal flow, Constraints)**, **F-29, F-31**.
- **Read first:** `src/Huddle.App/Acp/PersonaRunner.cs` (`StartAsync` lines 119–178,
  `Welcome` handling lines 128–157, `WorkItem` line 1109, `BuildPrompt` line 975, the
  `workItems` channel line 44), `tests/Huddle.Tests/Acp/PersonaRunnerTests.cs`.
- **Deliverable:**
  - `Start_BuiltinWithEmptyHumanRoom_RunsOneGreetingTurn` (the `FakeAgentHostFactory` session
    records one prompt starting with the Room label);
  - `Start_NotBuiltin_NoGreeting`;
  - `Start_HumanRoomNotEmpty_NoGreeting`;
  - `Start_EmptyGroupRoomOnly_NoGreeting`;
  - `Start_FactoryThrows_NoGreetingQueued`.
- **Acceptance:** **Red.**

### Task 16.3.i — Implement the Greeting `WorkItem` and `turn.greeting`

- **Goal:** Implement **Spec §6.14**.
- **Read first:** Task 16.3.t, **Spec §6.14** (the Prompt default text), the regeneration
  procedure at the top of this plan.
- **Deliverable:**
  - Add `internal enum WorkItemKind { Message, Greeting }` and extend `WorkItem` with a
    trailing `WorkItemKind Kind = WorkItemKind.Message`.
  - After `CreateAsync` succeeds (line 157), if
    `PersonaFrontmatter.TryReadIdentity(persona.Text, …)` yields
    `Builtin == BuiltinTeammate.ChiefOfStaffMarker`, and `welcome.Rooms` has a Room with two
    Members, one of them `UserKind.Human`, and `IsEmpty`, write
    `new WorkItem(room.Id, room.Name, string.Empty, string.Empty, [], WorkItemKind.Greeting)`
    to `workItems` once.
  - In `BuildPrompt`, render `turn.greeting` with `{{roomLabel}}` set to the rendered
    `turn.roomLabel` for a Greeting.
  - Add `turn.greeting` to `PromptCatalog` (Timing `Live`, Placeholders and
    RequiredPlaceholders `{{roomLabel}}`) with the **Spec §6.14** default text verbatim.
  - Regenerate `prompts.default.json`.
  - Nothing here is named "Welcome".
- **Acceptance:** 16.3.t green; every `PersonaRunnerTests` case green.

### Task 16.4.t — Test: the Greeting prompt golden (red)

- **Goal:** Pin **Spec §6.14** (Prompt text).
- **Read first:** `PromptGoldenTests.cs` (`turnPromptPlain.txt` pattern).
- **Deliverable:** `TurnPromptGreeting_MatchesGolden` against the new
  `Golden/turnPromptGreeting.txt`.
- **Acceptance:** **Red** (seeded on first run).

### Task 16.4.i — Accept the golden

- **Goal:** Complete **Spec §6.14**.
- **Read first:** Task 16.4.t.
- **Deliverable:** Review the seeded file (it must contain no tool prefix and no Skill name),
  then commit it.
- **Acceptance:** 16.4.t green.

### Task 16.5.t — Functional test: the Greeting posts, or fails cleanly (red)

- **Goal:** Pin **Spec §6.14 (Constraints)**, **F-26, F-27**.
- **Read first:** How `PersonaRunnerTests` drives a fake session to reply, and how a normal
  Turn posts (the `ActiveTurn` record near line 1122).
- **Deliverable:**
  - `Greeting_FakeSessionReplies_PostedAsChiefOfStaffInHumanRoom`;
  - `Greeting_TurnFails_NothingPostedRoomStillEmpty`;
  - `Greeting_HumanMessageArrivesDuring_QueuedAndAnsweredNext`.
- **Acceptance:** **Red**, or green on arrival if 16.3.i already covers it. Record which.

### Task 16.5.i — Close gaps in a Turn with no triggering Message

- **Goal:** Implement **Spec §6.14 (Implementation notes)**: the Draft is keyed by the posted
  Message id, and nothing in `Drafts` changes.
- **Read first:** Task 16.5.t.
- **Deliverable:** Fix only what 16.5.t exposes: for example, a null-sender assumption in the
  post path, or a catch-up buffer read for a WorkItem with no Message.
- **Acceptance:** 16.5.t green; the full suite green.

---

# D17 — S3 health check, manual tests, final docs

**Spec Appendix A (S3-T11, S3-T12, S3-D)**, **Spec §2 U15, U16**.

### Task 17.1 — Health check on an empty data directory

- **Goal:** Verify **Spec Appendix A S3-T11**: the seeder must not break process start.
- **Read first:** `test-health.ps1`, `docs/AgencyTeam.md` (Build, test, run).
- **Deliverable:** Run `dotnet build Huddle.slnx`, then `./test-health.ps1` twice: once as
  is, and once with a fresh empty `DataDir` passed as the script's configuration.
- **Acceptance:** Both print `PASS` and exit 0.

### Task 17.2 — Write `manual-tests/skills.md`

- **Goal:** Author the paid manual tests of **Spec Appendix A S3-T12**.
- **Read first:** `docs/agencyteam/manual-tests.md` (section 0: cost guard, Haiku / low
  Effort, outcomes), `docs/agencyteam/manual-tests/adapters.md` (the entry format),
  `docs/agencyteam/manual-tests/common.md` (oracles), `docs/agencyteam/manual-tests/planning.md`,
  **Spec §2 U1–U6, U16**.
- **Deliverable:** Create `docs/agencyteam/manual-tests/skills.md` with **SKILLS-01** to
  **SKILLS-05** in the house format: Free/💰 line, italic purpose, Before you start, Steps,
  Pass if, Fail if, Inconclusive if. SKILLS-05 includes "restart does not greet twice". Add a
  "Not a defect" note: Catch-up is per Room, so a private Advisor cannot know what happened in
  a scenario Room. Register the tests in `planning.md` and add rows in
  `manual-tests/tracker.md`.
- **Acceptance:** The file follows `adapters.md`'s format exactly; counts in `planning.md` add up.

### Task 17.3 — Final documentation

- **Goal:** Record S3 and the feature as delivered, per **Spec Appendix A (S3-D)**.
- **Read first:** `docs/agencyteam/roadmap.md` (table at lines 26–41), `docs/agencyteam/decisions.md`
  (entry format), `docs/AgencyTeam.md` (the "Applies to the repo as of" paragraph and the
  `welcome` JSON sample), `docs/agencyteam/language.md` (Greeting).
- **Deliverable:**
  - Add roadmap item 16, "Skills and the Chief of Staff — DELIVERED {date}", with a row in the
    table.
  - Add a dated `decisions.md` entry summarising D-1 to D-19 and what each rejected.
  - Update the hub's test count and the `welcome` sample, adding `"isEmpty"`.
  - Remove "Proposed, not built" from Greeting.
  - Set the Spec's header **Status** to *Delivered*.
- **Acceptance:** Links resolve; files are CRLF; `dotnet test Huddle.slnx --` green with the
  count quoted in the hub.
