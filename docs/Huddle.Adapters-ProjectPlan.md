# Huddle.Adapters — Project Plan

Decomposition of [`Huddle.Adapters-Specifications.md`](Huddle.Adapters-Specifications.md) into
atomic, self-contained tasks. Every task is written for a sub-agent with **zero project
context**: it names exact paths, types, signatures and acceptance criteria, and cites the spec
section that defines it.

**13 deliverables · 47 tasks.** *(46 as first written; Task 12.1 split into 12.1a and 12.1b on
2026-09-18 — see D12.)* Every implementation task (`.i`) is preceded by its test task
(`.t`). Tests are written first and must fail **for the right reason** before implementation
begins.

---

## How to use this document

Work one task at a time, top to bottom within a deliverable. A `.t` task ends **red**; its `.i`
partner ends **green**. Never write several `.t` tasks before their implementations — the spec's
§15 preamble explains why (tests written in bulk test imagined behaviour, not actual behaviour).

**Spec §15.10** gives the dependency graph. D0, D1, D3, D8 and D9 may start in parallel.

---

## Repo-wide conventions (read once, applies to every task)

| Rule | Detail |
| --- | --- |
| Build | `dotnet build Huddle.slnx` — the **solution**, never one project, because test projects carry their own analyzers |
| Test | `dotnet test Huddle.slnx --` — **the trailing `--` is required** or the run reports "Zero tests ran" and reads as a no-op |
| Warnings | `TreatWarningsAsErrors=true`, `Nullable=enable` (`Directory.Build.props`). Any analyzer complaint fails the build |
| Packages | Central package management. A `Version` attribute on a `PackageReference` is a **build error**. Versions live only in `Directory.Packages.props` |
| C# style | `agents/CSharpPrinciples.md` is binding. File-scoped namespaces, `this.field` (no `_` prefix), explicit type left / `new()` right, Allman braces, CRLF, four spaces |
| XML docs | Every class and method takes `///` comments, **including in test projects**. Never `//` for documentation |
| Test naming | `Method_Scenario_Expectation`. Underscores are allowed in tests only (`CA1707` is suppressed in test projects) |
| Cancellation | Every awaited call inside a test that accepts a `CancellationToken` receives `TestContext.Current.CancellationToken` (`xUnit1051`) |
| Nullable | **Never** silence with `!`. Prove non-null with a pattern or a guard |

### Existing `InternalsVisibleTo` (do not duplicate)

| Project | Grants to |
| --- | --- |
| `src/Huddle.App/Huddle.App.csproj` | `Huddle.Tests` |
| `src/Huddle.Acp/Huddle.Acp.csproj` | `Huddle.Acp.Tests` |
| `src/Huddle.Contracts/Huddle.Contracts.csproj` | `Huddle.Tests` |
| `src/Huddle.Console/Huddle.Console.csproj` | `Huddle.Acp.Tests` |

`IAgentHostFactory`, `IModelCatalog`, `AdapterCatalog`, `AdapterProfileResolver`,
`PersonaFrontmatter`, `PersonaIndex` and `DotAcpAgentHostFactory` are all `internal` to
`Huddle.App` and are reachable from `Huddle.Tests` through the grant above. **No new
`InternalsVisibleTo` is needed for those.** Task 0.2 adds one new grant, and only one.

### Terminology (Spec §16)

**Adapter** — an ACP agent a Persona's session runs on. **Adapter Profile** — one configured
Adapter. **Model**, **Effort**, **Persona**, **Turn**, **Room** — as defined in
`docs/agencyteam/language.md`.

### Binding documents

- `docs/agencyteam/rules.md` — **read in full before changing anything in `src/Huddle.App`**
- `docs/agencyteam/traps.md` — **read before touching `src/Huddle.Acp` or the wire protocol**
- `agents/CSharpPrinciples.md` — house style, enforced by the build

---

# D0 — Mock adapter

**Spec §6.10.** Removes Agency.NET's delivery date from the critical path and creates the
conformance tier (Spec §15.1, Tier 3).

> **Announce before starting.** Tasks 0.1 and 0.2 add a project to `Huddle.slnx`, a shared root
> file, and link source out of the ACP effort's test tree. `CLAUDE.md` requires both to be
> announced rather than merged silently.

### Task 0.1 — Scaffold `Huddle.MockAdapter`

- **Goal:** Create the console project that hosts the mock ACP agent, per **Spec §6.10 (Two
  modes)**.
- **Read first:** `Huddle.slnx`, `src/Huddle.Console/Huddle.Console.csproj` (the nearest
  precedent for an exe in this solution), `Directory.Build.props`, `Directory.Packages.props`,
  **Spec §6.10**, **Spec §5.2**.
- **Deliverable:**
  - Create `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj`: `Microsoft.NET.Sdk`,
    `<OutputType>Exe</OutputType>`, `<TargetFramework>net10.0</TargetFramework>`,
    `<RootNamespace>Agency.Huddle.MockAdapter</RootNamespace>`,
    `<AssemblyName>mock-acp</AssemblyName>`. **No `Version` attribute on any
    `PackageReference`.**
  - Add `<InternalsVisibleTo Include="Huddle.Tests" />` inside an `<ItemGroup>`.
  - Register in `Huddle.slnx` under the existing `<Folder Name="/src/">` element as
    `<Project Path="src/Huddle.MockAdapter/Huddle.MockAdapter.csproj" />`.
  - Add a placeholder `Program.cs` with `internal static class Program` and
    `internal static Task<int> Main(string[] args)` returning `Task.FromResult(0)`.
- **Acceptance:** `dotnet build Huddle.slnx` succeeds with zero warnings. `dotnet test
  Huddle.slnx --` is still green (no tests added yet).

### Task 0.2 — Link the ACP fake's source

- **Goal:** Reuse `FakeAcpAgent` without moving it or widening its visibility, per **Spec §6.10
  (Placement, and why the source is linked rather than moved)**.
- **Read first:** `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs`,
  `tests/Huddle.Acp.Tests/Fakes/PromptContext.cs`,
  `tests/Huddle.Acp.Tests/Fakes/FakeRpcError.cs`, **Spec §6.10**, **Spec §14 (Why the mock's
  source is linked rather than moved)**.
- **Deliverable:** In `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj` add:

  ```xml
  <ItemGroup>
    <Compile Include="..\..\tests\Huddle.Acp.Tests\Fakes\FakeAcpAgent.cs" Link="Fakes\FakeAcpAgent.cs" />
    <Compile Include="..\..\tests\Huddle.Acp.Tests\Fakes\PromptContext.cs" Link="Fakes\PromptContext.cs" />
    <Compile Include="..\..\tests\Huddle.Acp.Tests\Fakes\FakeRpcError.cs" Link="Fakes\FakeRpcError.cs" />
  </ItemGroup>
  ```

  If the compiler demands further files from that folder, link those too and **record each one
  in the csproj with a comment naming why**. Do **not** move, copy or modify any file under
  `tests/Huddle.Acp.Tests/`. Do **not** change any type's accessibility — a linked file compiles
  into this assembly as its own `internal`.
  The linked sources have a `namespace Agency.Huddle.Acp.Tests.Fakes;` declaration; leave it, and
  add `using Agency.Huddle.Acp.Tests.Fakes;` where the mock's own code needs the types.
- **Acceptance:** `dotnet build Huddle.slnx` succeeds. `git status` shows **no modification**
  under `tests/Huddle.Acp.Tests/`.

### Task 0.3.t — Test: a duplex stream over two half-streams (red)

- **Goal:** Pin the adapter that lets `FakeAcpAgent`, which takes **one** `Stream`, run over a
  process's separate stdin and stdout, per **Spec §6.10 (Two modes)**.
- **Read first:** `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs` (its constructor is
  `internal FakeAcpAgent(Stream stream)` and it both reads and writes that one stream),
  **Spec §6.10**.
- **Deliverable:** Add `tests/Huddle.Tests/MockAdapter/DuplexStreamTests.cs`, namespace
  `Agency.Huddle.Tests.MockAdapter`, `public sealed class DuplexStreamTests`. Assert:
  (a) bytes written to the duplex arrive on the supplied **output** stream;
  (b) bytes placed on the supplied **input** stream are read back from the duplex;
  (c) `CanRead` and `CanWrite` are both `true`, `CanSeek` is `false`;
  (d) `Dispose` disposes neither half when constructed with `leaveOpen: true`.
  Reference the target type as `Agency.Huddle.MockAdapter.DuplexStream`.
- **Acceptance:** Compiles and fails — `DuplexStream` does not exist. **Red.**

### Task 0.3.i — Implement `DuplexStream`

- **Goal:** Implement the type pinned by 0.3.t, per **Spec §6.10**.
- **Read first:** the tests from 0.3.t, `agents/CSharpPrinciples.md` (disposal rules).
- **Deliverable:** Add `src/Huddle.MockAdapter/DuplexStream.cs`:

  ```csharp
  namespace Agency.Huddle.MockAdapter;

  /// <summary>Presents a separate input and output stream as one duplex <see cref="Stream"/>.</summary>
  internal sealed class DuplexStream : Stream
  {
      internal DuplexStream(Stream input, Stream output, bool leaveOpen = false);
  }
  ```

  Override `CanRead`, `CanWrite`, `CanSeek`, `Read`, `ReadAsync`, `Write`, `WriteAsync`, `Flush`,
  `FlushAsync`. `Length` and `Position` throw `NotSupportedException`; `Seek` and `SetLength`
  throw `NotSupportedException`. Guard every member with
  `ObjectDisposedException.ThrowIf(this.disposed, this)`. The class is `sealed`, so no
  `GC.SuppressFinalize` is required.
- **Acceptance:** 0.3.t green. `dotnet build Huddle.slnx` clean.

### Task 0.4.t — Test: the mock answers a full session in-proc (red)

- **Goal:** Prove the mock is driveable over a stream pair and records what it received — the
  oracle **Spec §6.10 (What already exists)** and **Spec §15.8** depend on.
- **Read first:** `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs` (note `Received`,
  `WaitForAsync(string method, TimeSpan)`, and the four settable handlers `OnInitialize`,
  `OnNewSession`, `OnSetConfigOption`, `OnPrompt`), `Directory.Packages.props` (`Nerdbank.Streams`
  **2.14.354** is already declared), **Spec §6.10**, **Spec §15.2**.
- **Deliverable:** Add `<PackageReference Include="Nerdbank.Streams" />` to
  `tests/Huddle.Tests/Huddle.Tests.csproj` (**no `Version` attribute**) and a `ProjectReference`
  to `src/Huddle.MockAdapter/Huddle.MockAdapter.csproj`.
  Add `tests/Huddle.Tests/MockAdapter/MockAdapterTests.cs`, namespace
  `Agency.Huddle.Tests.MockAdapter`, `public sealed class MockAdapterTests`. Using
  `FullDuplexStream.CreatePair()`, drive a `FakeAcpAgent` and assert:
  (a) `initialize` returns a result and appears in `Received`;
  (b) `session/new` returns a session id and appears in `Received`;
  (c) a `session/prompt` produces **more than one** `agent_message_chunk` notification before its
      result (**Spec §15.2, T-0c**);
  (d) `Received` holds the three methods **in order**.
  **Before writing this test, confirm `AcpReferenceTests` in `tests/Huddle.Tests/Acp/` still
  passes** — it asserts something about that project's references, and a new `ProjectReference`
  may violate it. If it does, stop and report rather than editing that test.
- **Acceptance:** Compiles and fails — the default prompt handler emits a single chunk, or the
  project reference is missing. **Red.**

### Task 0.4.i — Chunked echo as the default prompt handler

- **Goal:** Make the default behaviour prove streaming, per **Spec §6.10 (Behaviour)**.
- **Read first:** the tests from 0.4.t, `tests/Huddle.Acp.Tests/Fakes/PromptContext.cs`
  (`SendTextChunkAsync`), **Spec §6.10**.
- **Deliverable:** Add `src/Huddle.MockAdapter/MockBehaviour.cs`, namespace
  `Agency.Huddle.MockAdapter`, `internal static class MockBehaviour`, with:

  ```csharp
  /// <summary>Echoes the prompt back as several chunks, then ends the turn.</summary>
  internal static Task<string> ChunkedEchoAsync(PromptContext context);
  ```

  It splits the prompt text on whitespace into at least three groups and calls
  `context.SendTextChunkAsync` once per group, then returns `"end_turn"`. Do **not** modify the
  linked `FakeAcpAgent`; assign the handler from the caller
  (`agent.OnPrompt = MockBehaviour.ChunkedEchoAsync`).
- **Acceptance:** 0.4.t green.

### Task 0.5.t — Test: stdout carries protocol bytes only (red)

- **Goal:** Pin **Spec §12 (E-16)** — a stray write to stdout corrupts the JSON-RPC stream.
- **Read first:** **Spec §6.10 (Constraints)**, **Spec §12 (E-16)**.
- **Deliverable:** Extend `tests/Huddle.Tests/MockAdapter/MockAdapterTests.cs`. Launch
  `mock-acp` as a **real child process** via `System.Diagnostics.Process` with all three streams
  redirected, send `initialize`, and assert:
  (a) every line read from stdout parses as JSON;
  (b) any diagnostic the mock emits appears on **stderr**, never stdout.
  Locate the executable by walking up from `AppContext.BaseDirectory` to the directory containing
  `Huddle.slnx` — the same technique `tests/Huddle.Acp.Tests/E2E/E2E.cs` uses — then into the
  mock's build output. This is the **only** process-spawning test in D0; keep it to one.
- **Acceptance:** Compiles and fails — `Program.Main` is still the placeholder. **Red.**

### Task 0.5.i — Implement `Program.Main`

- **Goal:** Run the mock as a process over stdin/stdout, per **Spec §6.10 (Two modes)**.
- **Read first:** the tests from 0.5.t, `src/Huddle.MockAdapter/DuplexStream.cs`,
  **Spec §6.10 (Constraints)**.
- **Deliverable:** Replace `src/Huddle.MockAdapter/Program.cs`:
  - open `Console.OpenStandardInput()` and `Console.OpenStandardOutput()`, wrap them in a
    `DuplexStream`, construct a `FakeAcpAgent` over it, set
    `agent.OnPrompt = MockBehaviour.ChunkedEchoAsync`, and `await agent.RunAsync(ct)`;
  - cancel on `Console.CancelKeyPress` and on `AppDomain.CurrentDomain.ProcessExit`;
  - **every** diagnostic goes to `Console.Error`. Add a `// stdout is protocol-only` comment at
    the single place stdout is obtained, naming **Spec §12 (E-16)**.
  - Accept an optional `--script <path>` argument, parsed but unused in V1, so the V2 script
    format in **Spec §6.10 (V1 / V2)** has a reserved surface.
- **Acceptance:** 0.5.t green. Running `mock-acp` by hand and typing an `initialize` frame
  returns a JSON result on stdout.

---

# D1 — Adapter profile and catalog

**Spec §6.1, §6.2, §7.4.** Tier 1 throughout — no process, no UI.

### Task 1.1.t — Test: an absent `Adapters` list synthesises one profile (red)

- **Goal:** Pin **Spec §6.1 (Internal flow)** and **Spec §4 (P6)** — a stock installation behaves
  exactly as it does today.
- **Read first:** `src/Huddle.App/Acp/AcpOptions.cs` (existing `Command` = `"node"`,
  `AdapterPath`, `Args`), `src/Huddle.App/TeamOptions.cs`, **Spec §6.1**, **Spec §7.4**,
  **Spec §4 (P6)**.
- **Deliverable:** Add `tests/Huddle.Tests/Acp/AdapterCatalogTests.cs`, namespace
  `Agency.Huddle.Tests.Acp`, `public sealed class AdapterCatalogTests`. Assert:
  (a) with `AcpOptions.Adapters` null, `Profiles` has exactly one entry with `Id == "claude"`,
      `DisplayName == "Claude"`, `UsesToolNamePrefix == true`, and `Command`, `Args`,
      `AdapterPath` copied from `AcpOptions`;
  (b) with `Adapters` empty, the same;
  (c) with two configured profiles, `Profiles` preserves **configuration order** and `Default` is
      the **first**;
  (d) `Find` matches `OrdinalIgnoreCase` and returns `null` for an unknown id.
- **Acceptance:** Compiles and fails — the types do not exist. **Red.**

### Task 1.1.i — Implement `AdapterProfile`, `AdapterProfileOptions` and `AdapterCatalog`

- **Goal:** Implement **Spec §6.1**.
- **Read first:** the tests from 1.1.t, `src/Huddle.App/Acp/AcpOptions.cs`,
  `docs/agencyteam/rules.md` (the row *"Collection options need no initialiser"*).
- **Deliverable:** Three additions in `namespace Agency.Huddle.App.Acp`:

  1. `src/Huddle.App/Acp/AdapterProfile.cs` — **public**, because `TeammateCard` binds it to a
     Razor `[Parameter]` and `rules.md` forbids an `internal` type there:

     ```csharp
     public sealed record AdapterProfile(
         string Id,
         string DisplayName,
         string? Description,
         string Command,
         IReadOnlyList<string>? Args,
         string? AdapterPath,
         bool UsesToolNamePrefix);
     ```

  2. `src/Huddle.App/Acp/AdapterProfileOptions.cs` — **public**, mutable, for
     `ConfigurationBinder`, which cannot reliably bind a positional record:

     ```csharp
     public sealed class AdapterProfileOptions
     {
         public string Id { get; set; } = string.Empty;
         public string? DisplayName { get; set; }
         public string? Description { get; set; }
         public string Command { get; set; } = string.Empty;
         public IReadOnlyList<string>? Args { get; set; }   // NO initialiser - see rules.md
         public string? AdapterPath { get; set; }
         public bool UsesToolNamePrefix { get; set; } = true;
     }
     ```

  3. `src/Huddle.App/Acp/AdapterCatalog.cs` — **internal sealed class**, constructed from
     `IOptions<TeamOptions>`, exposing
     `internal IReadOnlyList<AdapterProfile> Profiles { get; }`,
     `internal AdapterProfile Default => this.Profiles[0];` and
     `internal AdapterProfile? Find(string? id)`.
     When `options.Value.Acp.Adapters` is null or empty, synthesise exactly one profile as
     specified in **Spec §6.1**. `DisplayName` falls back to `Id` when unset.

  Add `public IReadOnlyList<AdapterProfileOptions>? Adapters { get; set; }` to `AcpOptions` with
  an XML doc naming **Spec §7.4**, and **no initialiser**.
  Register in `src/Huddle.App/ServiceCollectionExtensions.cs` near line 73 with
  `services.AddSingleton<AdapterCatalog>();`.
- **Acceptance:** 1.1.t green. `dotnet build Huddle.slnx` clean.

### Task 1.2.t — Test: a blank `Command` fails at startup (red)

- **Goal:** Pin **Spec §6.1 (Constraints)** — a misconfigured profile is a startup error, not a
  first-Turn error.
- **Read first:** `src/Huddle.App/ServiceCollectionExtensions.cs` lines 28–32 (the
  `Team:Acp:PersonaDir` rename guard, the precedent for fail-fast configuration), **Spec §6.1**.
- **Deliverable:** Extend `AdapterCatalogTests`: a configured profile with a blank or whitespace
  `Command` makes the constructor throw `InvalidOperationException` whose message contains the
  offending `Id`. Use `Assert.Throws<InvalidOperationException>` and assert on the message.
- **Acceptance:** Compiles and fails. **Red.**

### Task 1.2.i — Validate profiles in the constructor

- **Goal:** Implement **Spec §6.1 (Constraints)**.
- **Read first:** the test from 1.2.t, `agents/CSharpPrinciples.md` (exception rules — throw
  `InvalidOperationException`, never `Exception`).
- **Deliverable:** In `AdapterCatalog`'s constructor, after projecting the options, throw
  `InvalidOperationException` naming the `Id` when `Command` is null or whitespace, or when two
  profiles share an `Id` (`OrdinalIgnoreCase`).
- **Acceptance:** 1.2.t green.

### Task 1.3.t — Test: resolution never fails (red)

- **Goal:** Pin **Spec §6.2** and **Spec §4 (P4 — degrade, never reject)**.
- **Read first:** **Spec §6.2 (Internal flow)** — the three-row table is the specification.
- **Deliverable:** Add `tests/Huddle.Tests/Acp/AdapterProfileResolverTests.cs`, namespace
  `Agency.Huddle.Tests.Acp`, `public sealed class AdapterProfileResolverTests`. Assert:
  (a) `null` → the default profile, `Warning` is `null`;
  (b) whitespace → the default profile, `Warning` is `null`;
  (c) a matching id, differing only in case → that profile, `Warning` is `null`;
  (d) an unknown id → **the default profile**, and `Warning` is non-null and contains **both**
      the unknown id and the default profile's `Id`.
- **Acceptance:** Compiles and fails. **Red.**

### Task 1.3.i — Implement `AdapterProfileResolver`

- **Goal:** Implement **Spec §6.2**.
- **Read first:** the tests from 1.3.t, **Spec §6.2 (Implementation notes)**.
- **Deliverable:** Add `src/Huddle.App/Acp/AdapterProfileResolver.cs`:

  ```csharp
  namespace Agency.Huddle.App.Acp;

  /// <summary>Turns a Persona's Adapter id into a profile. Never fails; see Spec §6.2.</summary>
  internal sealed class AdapterProfileResolver(AdapterCatalog catalog)
  {
      internal (AdapterProfile Profile, string? Warning) Resolve(string? adapterId);
  }
  ```

  Pure and synchronous — it must **not** touch the filesystem (**Spec §6.2 (Constraints)**).
  Use a primary constructor, per the house convention for a `sealed` class whose dependency is
  only stored. Register with `services.AddSingleton<AdapterProfileResolver>();`.
- **Acceptance:** 1.3.t green.

---

# D2 — Process options retarget

**Spec §6.3.** Closes **Spec §12 (E-7)**, rated high.

### Task 2.1.t — Test: precedence survives, and an unprefixed profile never reaches the locator (red)

- **Goal:** Pin **Spec §6.3 (Internal flow)** and **Spec §12 (E-7)**.
- **Read first:** `src/Huddle.App/Acp/AgentProcessOptionsFactory.cs` (current signature
  `TryCreate(AcpOptions options, string workDir, string probeStart)` and its four-step
  precedence), `src/Huddle.App/Acp/AdapterLocator.cs`,
  `tests/Huddle.Tests/Acp/AgentProcessOptionsFactoryTests.cs`, **Spec §6.3**.
- **Deliverable:** Rewrite `AgentProcessOptionsFactoryTests` against the new signature. Assert:
  (a) `Args` non-empty wins;
  (b) otherwise `AdapterPath` wins;
  (c) otherwise `AdapterLocator.Locate` is consulted;
  (d) otherwise `null` is returned — **not** an exception;
  (e) **a profile with `UsesToolNamePrefix == false` and no `Args`/`AdapterPath` returns `null`
      without consulting `AdapterLocator`** (E-7). Prove (e) by pointing `probeStart` at a
      temporary directory tree that *does* contain the Node adapter's relative path and asserting
      the result is still `null`.
- **Acceptance:** Compiles and fails. **Red.**

### Task 2.1.i — Retarget to `AdapterProfile`

- **Goal:** Implement **Spec §6.3**.
- **Read first:** the tests from 2.1.t, both call sites —
  `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs:56` and
  `src/Huddle.App/Acp/ModelCatalogProbe.cs:187`.
- **Deliverable:** Change the signature to
  `internal static AgentProcessOptions? TryCreate(AdapterProfile profile, string workDir, string probeStart)`.
  Keep the four-step precedence, reading `profile.Args`, `profile.AdapterPath`, `profile.Command`.
  Gate step 3 on `profile.UsesToolNamePrefix`, with a comment quoting **Spec §6.3
  (Implementation notes)**: *a profile that does not take the `mcp__` prefix is not the Node
  adapter, and the locator knows only how to find the Node adapter.* Update both call sites to
  pass a profile (temporarily `catalog.Default` where a resolver is not yet wired — D5 and D6
  replace that).
- **Acceptance:** 2.1.t green. `dotnet build Huddle.slnx` clean.

---

# D3 — Persona frontmatter

**Spec §7.2.** Closes **Spec §12 (E-5)** and **(E-6)**, both rated high.

### Task 3.1.t — Test: `adapter:` parses and is optional (red)

- **Goal:** Pin **Spec §7.2 (The frontmatter key)**.
- **Read first:** `src/Huddle.App/Acp/PersonaFrontmatter.cs` (the four `private const` key names
  at lines 30–33, `TryReadIdentity` at line 88), `src/Huddle.App/Acp/PersonaIdentity.cs`,
  `tests/Huddle.Tests/Acp/PersonaFrontmatterTests.cs`, **Spec §7.2**.
- **Deliverable:** Extend `PersonaFrontmatterTests`. Assert:
  (a) `adapter: agency` yields `PersonaIdentity.Adapter == "agency"`;
  (b) an absent `adapter:` yields `null` and the file is still a **valid** Persona;
  (c) `adapter:` with a blank value yields `null`, not `""`;
  (d) the key matches `OrdinalIgnoreCase` (`Adapter:` works).
- **Acceptance:** Compiles and fails. **Red.**

### Task 3.1.i — Read the fifth structural key

- **Goal:** Implement **Spec §7.2**.
- **Read first:** the tests from 3.1.t, `src/Huddle.App/Acp/PersonaFrontmatter.cs`.
- **Deliverable:** Add `private const string AdapterKey = "Adapter";` beside the four existing
  key constants. Extend `PersonaIdentity` to
  `public sealed record PersonaIdentity(string Name, string Title, string Alias, IReadOnlyList<string> Teams, string? Adapter = null);`
  — the trailing optional parameter keeps every existing positional construction compiling.
  Read the key in `TryReadIdentity`, normalising blank to `null`. It is **optional**: a file
  without it must remain valid.
- **Acceptance:** 3.1.t green; every pre-existing `PersonaFrontmatterTests` and
  `PersonaIndexTests` case still green.

### Task 3.2.t — Test: `adapter` never reaches the job description (red)

- **Goal:** Pin **Spec §12 (E-6)** — plumbing must not become model-facing text.
- **Read first:** `src/Huddle.App/Acp/PersonaFrontmatter.cs` — `ComposeJobDescription` at line 57
  and `JobDescriptionExcludedKeys` at line 43 (today it contains **only** `Name`), **Spec §7.2**,
  **Spec §12 (E-6)**.
- **Deliverable:** Extend `PersonaFrontmatterTests`: a Persona whose frontmatter contains
  `adapter: agency` produces a job description containing neither `"adapter"` nor `"Adapter"`
  (`StringComparison.OrdinalIgnoreCase`), while still containing an unrelated custom key such as
  `role:`.
- **Acceptance:** Compiles and fails. **Red.**

### Task 3.2.i — Exclude `adapter` from the job description

- **Goal:** Implement **Spec §7.2**, consequence 1.
- **Read first:** the test from 3.2.t.
- **Deliverable:** Add `AdapterKey` to `JobDescriptionExcludedKeys`. Update that field's XML doc
  to say **why** — `mcp__team__list_agents` describes a Teammate to other Agents, and which
  Adapter runs it is not something any Agent can act on.
- **Acceptance:** 3.2.t green.

### Task 3.3.t — Test: a Create-time `adapter` survives (red)

- **Goal:** Pin **Spec §12 (E-5)** — `Compose` emits only identity keys today, so a value written
  at Create time is destroyed.
- **Read first:** `src/Huddle.App/Acp/PersonaFrontmatter.cs` — `Compose` at line 142 and
  `WriteScalarField` at line 195; `src/Huddle.App/Acp/PersonaStore.cs` — `Add` at line 280 and
  `Update` at line 328, **Spec §7.2**.
- **Deliverable:** Extend `PersonaFrontmatterTests`: `Compose(identity, body)` with
  `identity.Adapter == "agency"` emits an `adapter:` line, and feeding that output back through
  `TryReadIdentity` round-trips the value. Add a second case asserting `Compose` with
  `Adapter == null` emits **no** `adapter:` line.
- **Acceptance:** Compiles and fails. **Red.**

### Task 3.3.i — Emit `adapter` on the write path

- **Goal:** Implement **Spec §7.2**, consequence 2.
- **Read first:** the tests from 3.3.t.
- **Deliverable:** Extend `Compose` to emit `adapter: {value}` after the four identity keys when
  `identity.Adapter` is non-null, and to omit the line entirely when it is null. Keep the emitted
  key order stable — `name`, `title`, `alias`, `teams`, `adapter` — because
  `PromptGoldenTests` and any file round-trip depend on it.
- **Acceptance:** 3.3.t green.

---

# D4 — Persona record and store

**Spec §7.3.** Delivers **Spec §1.3 (O-2)** for free through record equality.

### Task 4.1.t — Test: the Adapter reaches `Persona`, and a change forces a restart (red)

- **Goal:** Pin **Spec §7.3** and **Spec §1.3 (O-2)**.
- **Read first:** `src/Huddle.App/Acp/Persona.cs`, `src/Huddle.App/Acp/PersonaEntry.cs`,
  `src/Huddle.App/Acp/PersonaIndex.cs` (`Build` at line 92, the `PersonaEntry` projection at line
  122), `src/Huddle.App/Acp/PersonaStore.cs` (`Get` at line 205 — the only file↔DB join),
  `src/Huddle.App/Acp/PersonaSupervisor.cs` (`NeedsRestart` at line 246), **Spec §7.3**.
- **Deliverable:**
  - Extend `tests/Huddle.Tests/Acp/PersonaStoreTests.cs`: a file whose frontmatter carries
    `adapter: agency` yields `store.Get(name)!.Adapter == "agency"`; a file without one yields
    `null`; `Update` with unchanged text preserves a hand-written `adapter:` line (because
    `Update` writes raw text through).
  - Extend `tests/Huddle.Tests/Acp/PersonaSupervisorTests.cs`: two `Persona` records differing
    **only** in `Adapter` are unequal, so `NeedsRestart` returns `true`. Reach `NeedsRestart`
    through its existing test seam; if it is `private static`, assert the equality directly on
    the records and add a comment citing **Spec §7.3 (Why this gets restart-on-change for free)**.
- **Acceptance:** Compiles and fails. **Red.**

### Task 4.1.i — Add `Adapter` to `Persona`, `PersonaEntry` and the index

- **Goal:** Implement **Spec §7.3**.
- **Read first:** the tests from 4.1.t.
- **Deliverable:**
  - `Persona` →
    `public sealed record Persona(string Name, string Text, string? Model = null, string? Effort = null, string? Adapter = null);`
  - `PersonaEntry` → append `string? Adapter = null`.
  - `PersonaIndex.Build` → pass `file.Identity.Adapter` into the `PersonaEntry` projection.
  - `PersonaStore.Get` → `new Persona(entry.Name, entry.Text, this.models.Get(entry.Name), this.efforts.Get(entry.Name), entry.Adapter)`.
  - `PersonaStore.Add` and `Update` → carry the value through so their returned `Persona` matches
    what `Get` would return.
  - **Do not** add a store, a table or a migration, and **do not** touch
    `src/Huddle.App/Acp/PersonaRenameCascade.cs` — **Spec §7.1** is explicit that this deliverable
    adds no per-Persona store.
- **Acceptance:** 4.1.t green. `dotnet test Huddle.slnx --` fully green — in particular every
  pre-existing `PersonaRenameCascadeTests` case, unmodified.

---

# D5 — Host factory and tool prefix

**Spec §6.4.** Delivers **Spec §1.3 (O-5)**. Closes **Spec §12 (E-15)**.

### Task 5.1.t — Test: a second golden for the unprefixed profile (red)

- **Goal:** Pin **Spec §6.4 (Implementation notes)** and **Spec §4 (P5, P6)** — the prefix follows
  the Adapter, and the prefixed prompt is byte-identical to today.
- **Read first:** `tests/Huddle.Tests/Acp/PromptGoldenTests.cs` (the seven-name `ToolNames` array
  at lines 56–62 and the `Compose` call at line 71), `tests/Huddle.Tests/Acp/Golden/systemPrompt.txt`,
  `src/Huddle.App/Acp/SystemPromptComposer.cs`, `docs/agencyteam/rules.md` (the rows *"App Tool
  names must be spelled `mcp__team__*`"* and *"A Prompt's text never contains `mcp__team__`"*),
  **Spec §6.4**, **Spec §16 (`rules.md` — one row widens)**.
- **Deliverable:** Extend `PromptGoldenTests` with a second case that composes the prompt from
  **bare** tool names (`get_help`, `list_agents`, …) and a `helpToolName` of `get_help`, pinned
  against a new golden `tests/Huddle.Tests/Acp/Golden/systemPrompt.unprefixed.txt`. Assert the
  new golden contains no `mcp__`. **The existing prefixed golden must not change** — if it does,
  stop and report, because that is **Spec §4 (P6)** breaking.
- **Acceptance:** Compiles and fails — the new golden file does not exist. **Red.**

### Task 5.1.i — Derive the prefix from the profile

- **Goal:** Implement **Spec §6.4**.
- **Read first:** the tests from 5.1.t, `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs` — the
  `private const string ToolServerName = "team";` at line 31 and its five uses at lines 83, 89,
  90, 92, 95, 126.
- **Deliverable:** In `DotAcpAgentHostFactory.CreateAsync`:
  - inject `AdapterProfileResolver` and call
    `var (profile, warning) = this.resolver.Resolve(persona.Adapter);` as the **first**
    statement;
  - pass `profile` to `AgentProcessOptionsFactory.TryCreate`, and extend the
    `InvalidOperationException` message to name `profile.Id` alongside `tools/acp/install.ps1`;
  - replace line 83 with
    `var toolNamePrefix = profile.UsesToolNamePrefix ? $"mcp__{ToolServerName}__" : string.Empty;`
  - leave `ToolServerName` a single `private const` and keep passing it unchanged to
    `AppToolServer` — **the MCP server name does not change**, only whether model-facing names
    carry it (**Spec §6.4**);
  - log `warning` when non-null. **Do not** change `IAgentHostFactory`'s signature — D7 surfaces
    the warning (**Spec §8.2**);
  - keep `GetHelpTool` constructed from the other tools and **last** (`rules.md`).
- **Acceptance:** 5.1.t green. `dotnet build Huddle.slnx` clean.

---

# D6 — Model catalogue dispatch

**Spec §6.5.** Delivers **Spec §1.3 (O-3)**.

### Task 6.1.t — Test: catalogues are cached per adapter (red)

- **Goal:** Pin **Spec §6.5 (Internal flow)** — the cache keys change; the mechanism does not.
- **Read first:** `src/Huddle.App/Acp/IModelCatalog.cs`, `src/Huddle.App/Acp/ModelCatalogProbe.cs`
  (the `cached` field, `effortCache`, `DefaultModelKey`, the shared `SemaphoreSlim gate`, the
  20-second `ProbeTimeout`), `tests/Huddle.Tests/Acp/Fakes/FakeModelCatalog.cs`,
  `docs/agencyteam/rules.md` row *"No test may reach the real `ModelCatalogProbe`"*,
  **Spec §6.5**.
- **Deliverable:**
  - Extend `FakeModelCatalog` to the new two-argument signatures and add
    `public List<string?> AdaptersProbed { get; } = [];` recording every `adapterId` seen, plus
    `public Dictionary<string, IReadOnlyList<AgentModelOption>> ModelsByAdapter { get; } = [];`.
  - Add `tests/Huddle.Tests/Acp/ModelCatalogCacheTests.cs` asserting, against a **fake process
    layer and never the real probe**: a second `GetAsync` for the same adapter does not re-probe;
    a `GetAsync` for a *different* adapter does; effort results are keyed per
    `(adapter, model)`; and **a failed probe is never cached**, so a retry probes again.
  - **This task must not spawn a process.** If the current `ModelCatalogProbe` cannot be tested
    without one, extract the spawn behind an internal seam and say so in the task report.
- **Acceptance:** Compiles and fails. **Red.**

### Task 6.1.i — Make the probe profile-aware

- **Goal:** Implement **Spec §6.5**.
- **Read first:** the tests from 6.1.t, every `IModelCatalog` call site —
  `src/Huddle.App/Components/Shared/TeammateCard.razor` lines 836–873, and
  `src/Huddle.App/ServiceCollectionExtensions.cs:95`.
- **Deliverable:**
  - `IModelCatalog` becomes:

    ```csharp
    ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(string? adapterId, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? adapterId, string? model, CancellationToken cancellationToken);
    ```

  - `ModelCatalogProbe` resolves the profile through `AdapterProfileResolver`, passes it to
    `AgentProcessOptionsFactory.TryCreate`, and re-keys both caches:
    models on the adapter id; efforts on `$"{adapterId}\u001f{model}"` — a unit separator,
    because an adapter id and a model id can both contain ordinary punctuation.
  - Keep the `SemaphoreSlim gate` **single and shared** across adapters, with a comment citing
    **Spec §6.5 (Implementation notes)**. Keep the 20-second timeout, `WithoutAdapterDefault`,
    and the Work-Dir-root probe cwd unchanged.
- **Acceptance:** 6.1.t green. `TeammatesPage_DoesNotProbeForModelsOnAPlainLoad` still green.

---

# D7 — Supervisor degradation

**Spec §8.2.** Closes **Spec §12 (E-1)**.

### Task 7.1.t — Test: an unknown Adapter degrades but still starts (red)

- **Goal:** Pin **Spec §8.2** and **Spec §4 (P4)**.
- **Read first:** `src/Huddle.App/Acp/PersonaSupervisor.cs` (lines 110–115 and 380–384, the two
  `Acp.Enabled` gates; `this.health.Report(...)` usage), `src/Huddle.App/Acp/PersonaHealth.cs`,
  `tests/Huddle.Tests/Acp/PersonaSupervisorTests.cs` and its `FakeAgentHostFactory` usage,
  **Spec §8.2**, **Spec §6.2**.
- **Deliverable:** Extend `PersonaSupervisorTests`: a Persona whose `Adapter` names an
  unconfigured id causes `PersonaHealth` to report `PersonaState.Degraded` with a message
  containing the unknown id, **and** a runner is still created (assert via
  `FakeAgentHostFactory.Calls`).
- **Acceptance:** Compiles and fails. **Red.**

### Task 7.1.i — Report the resolver warning from the supervisor

- **Goal:** Implement **Spec §8.2**.
- **Read first:** the test from 7.1.t, **Spec §8.2 (Why there and not in the factory)**.
- **Deliverable:** Inject `AdapterProfileResolver` into `PersonaSupervisor` and, inside
  `StartHostIfMissingAsync` **after** the `Acp.Enabled` gate and **before** constructing the
  runner, call `Resolve` and report `Degraded` when `Warning` is non-null. Add an XML comment
  citing **Spec §8.2** and stating why the resolver is called twice: it is pure, so two calls
  cost nothing and `IAgentHostFactory`'s signature stays frozen.
- **Acceptance:** 7.1.t green; every pre-existing `PersonaSupervisorTests` case still green.

---

# D8 — Teammate card

**Spec §6.6.** Delivers **Spec §1.3 (O-1)**. Closes **Spec §12 (E-2)** and **(E-4)**.

### Task 8.1.t — Test: one profile renders no Adapter select (red)

- **Goal:** Pin **Spec §6.6 (Implementation notes)** and **Spec §4 (P6)**.
- **Read first:** `src/Huddle.App/Components/Shared/TeammateCard.razor` (the Model `MudSelect` at
  line 240 and the Effort one at 266), `tests/Huddle.Tests/Ui/TeammateCardTests.cs`,
  `tests/Huddle.Tests/Ui/MudBunitContext.cs`, `docs/agencyteam/rules.md` (the rows about
  `Disabled` vs `ReadOnly` and about a `string` `[Parameter]` needing a leading `@`),
  **Spec §6.6**.
- **Deliverable:** Extend `TeammateCardTests`: with an `AdapterCatalog` holding one profile, the
  rendered markup contains **no** select labelled `Adapter`; with two profiles it contains one.
  Use the existing bUnit fixture; register a real `AdapterCatalog` built over in-memory options.
- **Acceptance:** Compiles and fails. **Red.**

### Task 8.1.i — Render the Adapter select

- **Goal:** Implement **Spec §6.6**.
- **Read first:** the tests from 8.1.t, the existing Model select markup.
- **Deliverable:** Inject `AdapterCatalog`. Above the Model select, render a `MudSelect<string>`
  labelled `Adapter`, bound to a new `private string? adapter;` field, with `ValueChanged`
  pointing at `OnAdapterChangedAsync`, wrapped in
  `@if (this.AdapterChoices.Count > 1) { ... }`. Add `AdapterChoices`, which synthesises an entry
  for a stored-but-unconfigured id exactly as `ModelChoices` does, and `AdapterHelperText` with
  the three states in **Spec §6.6 (Helper text)**. Seed `this.adapter` from a new
  `[Parameter] public string? Adapter { get; set; }` in `OnInitializedAsync` and in
  `BeginEditAsync`, and include it in the values passed to `PersonaStore.Add` / `Update`.
  Remember `rules.md`: a `string` parameter passed from `Teammates.razor` needs a **leading `@`**.
- **Acceptance:** 8.1.t green.

### Task 8.2.t — Test: changing the Adapter resets Model and Effort with a note (red)

- **Goal:** Pin **Spec §6.6 (Internal flow)** and **Spec §17 (D-6)**.
- **Read first:** `TeammateCard.razor` `OnModelChangedAsync` (lines 808–823) and the
  `effortResetByModelChange` note markup at line 255, **Spec §6.6**, **Spec §8.3**.
- **Deliverable:** Extend `TeammateCardTests`: selecting a different Adapter clears both the
  Model and the Effort selection, and renders a note with `role="status"` (never `role="alert"`)
  whose text says the Adapter change reset them. Assert the probe for the **new** adapter ran, via
  `FakeModelCatalog.AdaptersProbed`.
- **Acceptance:** Compiles and fails. **Red.**

### Task 8.2.i — Implement `OnAdapterChangedAsync`

- **Goal:** Implement **Spec §6.6** and **Spec §8.3**.
- **Read first:** the tests from 8.2.t, `docs/agencyteam/rules.md` (the row *"A handler that
  awaits two probes in a row must `StateHasChanged()` between them"* — this is issue #39).
- **Deliverable:** Add:

  ```csharp
  private async Task OnAdapterChangedAsync(string value)
  {
      this.adapter = string.IsNullOrWhiteSpace(value) ? null : value;
      this.adapterResetModelAndEffort = this.model is not null || this.effort is not null;
      this.model = null;
      this.effort = null;
      this.effortResetByModelChange = false;

      await this.LoadModelsAsync();
      Task efforts = this.LoadEffortsAsync();
      this.StateHasChanged();          // MANDATORY - rules.md / issue #39
      await efforts;
  }
  ```

  Add `private bool adapterResetModelAndEffort;` and the `role="status"` note beside the existing
  reset note. Pass `this.adapter` to both `LoadModelsAsync` and `LoadEffortsAsync`.
- **Acceptance:** 8.2.t green.

### Task 8.3.t — Test: a stale model probe cannot overwrite a newer one (red)

- **Goal:** Pin **Spec §12 (E-4)** — a new defect class this feature introduces.
- **Read first:** `TeammateCard.razor` `LoadEffortsAsync` (lines 854–873) and its
  `effortProbeGeneration` counter — **`LoadModelsAsync` has no equivalent**; **Spec §8.3
  (Generation counters)**.
- **Deliverable:** Extend `TeammateCardTests`: using `FakeModelCatalog`'s gating hooks, start a
  slow probe for adapter A, switch to adapter B whose probe completes first, then release A.
  Assert the rendered Model options are **B's**.
- **Acceptance:** Compiles and fails. **Red.**

### Task 8.3.i — Add a generation counter to `LoadModelsAsync`

- **Goal:** Implement **Spec §8.3**.
- **Read first:** the test from 8.3.t, `LoadEffortsAsync` as the exact precedent.
- **Deliverable:** Add `private int modelProbeGeneration;` and mirror `LoadEffortsAsync`'s
  structure: capture `var generation = ++this.modelProbeGeneration;` on entry and assign
  `availableModels` and clear `modelsLoading` only when `generation == this.modelProbeGeneration`.
- **Acceptance:** 8.3.t green.

### Task 8.4.t / 8.4.i — Regression guard: a plain load still probes zero times

- **Goal:** Preserve `docs/agencyteam/rules.md` row *"No test may reach the real
  `ModelCatalogProbe`"* and the probe-on-open-only rule.
- **Read first:** `tests/Huddle.Tests/Ui/TeammatesPageTests.cs` —
  `TeammatesPage_DoesNotProbeForModelsOnAPlainLoad`.
- **Deliverable:** Confirm that test still passes unmodified and extend it to assert
  `FakeModelCatalog.AdaptersProbed` is **empty** after a plain GET. No implementation change is
  expected; if one is needed, the cascade is probing too eagerly and that is the defect.
- **Acceptance:** Green with no production change.

---

# D9 — `Huddle.Acp` deltas

**Spec §6.8, §6.9.** Independent of D1–D8; may run in parallel.

> **Read `docs/agencyteam/traps.md` in full before starting** — it is binding for
> `src/Huddle.Acp` and the wire protocol.

### Task 9.1.t — Test: disposal sends `session/close` exactly once (red)

- **Goal:** Pin **Spec §6.8**.
- **Read first:** `src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs` (`DisposeAsync` at line 183 —
  today it makes **no wire call**), `tests/Huddle.Acp.Tests/DotAcp/DotAcpAgentSessionTests.cs`,
  `tests/Huddle.Acp.Tests/Fakes/FakeAcpAgent.cs`, **Spec §6.8**.
- **Deliverable:** Extend `DotAcpAgentSessionTests`. Assert:
  (a) disposal sends one `session/close` carrying the session id, observed through the fake
      agent's `Received`;
  (b) a `session/close` that throws (`IOException`) still lets disposal **complete**;
  (c) a second `DisposeAsync` sends nothing;
  (d) disposal completes within a short bound when the peer never answers.
- **Acceptance:** Compiles and fails. **Red.**

### Task 9.1.i — Send `session/close` on dispose

- **Goal:** Implement **Spec §6.8**.
- **Read first:** the tests from 9.1.t, `agents/CSharpPrinciples.md` (exception rules — a general
  `catch` must carry a comment or a log call explaining why swallowing is safe).
- **Deliverable:** Change `DisposeAsync` to `public async ValueTask DisposeAsync()`. Keep the
  existing `disposed` flag as the idempotency guard. Before completing the channel, send
  `session/close` on a best-effort basis, bounded by a 2-second `CancellationTokenSource`, and
  catch `IOException`, `ObjectDisposedException` and `OperationCanceledException` with a comment
  citing **Spec §6.8**: *this is a courtesy to the agent, never a condition of our own teardown.*
- **Acceptance:** 9.1.t green; the whole `Huddle.Acp.Tests` suite green.

### Task 9.2.t / 9.2.i — `WWW-Authenticate: Bearer` on the 401

- **Goal:** Implement **Spec §6.9**.
- **Read first:** `src/Huddle.Acp/Tools/AppToolServer.cs` lines 179–188 (the 401 block),
  `tests/Huddle.Acp.Tests/Tools/AppToolServerTests.cs`
  (`ToolsList_WithTokenConfigured_NoAuthorizationHeader_Returns401` at line 275 and its
  wrong-token sibling at line 305), **Spec §6.9**.
- **Deliverable:** **Test first:** extend both existing 401 tests to assert the response carries
  `WWW-Authenticate: Bearer` and that its value contains **no** `resource_metadata`.
  **Then implement:** add `context.Response.Headers.WWWAuthenticate = "Bearer";` immediately
  before `return;` in the 401 block, with a comment citing **Spec §6.9** on why the challenge is
  bare — a parameterised one would key MCP's OAuth discovery path.
- **Acceptance:** Both tests green; no other `AppToolServerTests` case changes.

---

# D10 — Conformance suite (Tier 3)

**Spec §15.8.** The tier that was previously missing: each task drives a **real Persona through a
real ACP peer**. Requires D0; each individual task also requires the deliverable it exercises.

### Task 10.1 — Conformance fixture

- **Goal:** Provide the harness **Spec §15.8** needs: a Persona wired to an in-proc mock peer.
- **Read first:** `tests/Huddle.Tests/Pipes/PipeHostFixture.cs` (note
  `RemovePersonaSupervisorHostedService` at line 137 and the in-memory config at lines 122–123),
  `tests/Huddle.Tests/Acp/Fakes/FakeAgentHostFactory.cs`, `src/Huddle.MockAdapter/DuplexStream.cs`,
  **Spec §15.8**, **Spec §6.10**.
- **Deliverable:** Add `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs`, namespace
  `Agency.Huddle.Tests.Conformance`. It must:
  - create a `FullDuplexStream.CreatePair()`, run a `FakeAcpAgent` on one half and hand the other
    to a `DotAcpAgentHost`-equivalent path;
  - expose `internal FakeAcpAgent Agent { get; }` so tests can read `Received` and set handlers;
  - start a `PersonaRunner` for a named Persona with `Team:Acp:Enabled = "true"`;
  - implement `IAsyncDisposable` and tear everything down deterministically.
  **This fixture must not spawn a process** — process mode belongs to Task 10.6 alone.
- **Acceptance:** A smoke test using the fixture starts a Persona and observes `initialize` in
  `Agent.Received`. Green.

### Task 10.2 — The composed prompt actually arrives

- **Goal:** **Spec §15.8 (T-22)** — prove §6.4 end to end, not just the golden.
- **Read first:** `src/Huddle.App/Acp/SystemPromptComposer.cs`, **Spec §6.4**, **Spec §15.8**.
- **Deliverable:** Add `tests/Huddle.Tests/Conformance/PromptDeliveryTests.cs`. Assert the
  `session/new` recorded in `Agent.Received` carries the Persona's text inside its `_meta`
  payload.
- **Acceptance:** Green.

### Task 10.3 — The prefix plumbing is real

- **Goal:** **Spec §15.8 (T-23)** and **Spec §1.3 (O-5)**. The spec calls this the strongest test
  in the suite: it is the defect that makes a Persona look broken rather than misconfigured.
- **Read first:** `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs`, **Spec §6.4**, **Spec §15.8**.
- **Deliverable:** Add `tests/Huddle.Tests/Conformance/ToolPrefixTests.cs`. With a profile whose
  `UsesToolNamePrefix` is `false`, assert the received prompt names `get_help` and contains no
  `mcp__`; with `true`, assert it names `mcp__team__get_help`.
- **Acceptance:** Green.

### Task 10.4 — The tool server reaches the peer, authenticated

- **Goal:** **Spec §15.8 (T-24)**.
- **Read first:** `src/Huddle.Acp/Tools/AppToolServer.cs`, **Spec §6.4**, **Spec §15.8**.
- **Deliverable:** Add `tests/Huddle.Tests/Conformance/ToolServerHandshakeTests.cs`. Assert the
  received `session/new` carries exactly one `mcpServers` entry, with a `127.0.0.1` URL ending
  `/mcp` and an `Authorization` header whose value starts `Bearer `. **Do not assert the token's
  value** — it is minted per session and must never be pinned.
- **Acceptance:** Green.

### Task 10.5 — Streaming, cancel, close and restart

- **Goal:** **Spec §15.8 (T-21, T-25, T-26, T-27, T-28)**.
- **Read first:** `src/Huddle.App/Acp/PersonaRunner.cs` (the event reader at lines 543–580 and the
  `StopTurn` branch), `src/Huddle.Contracts/Messages.cs`, `docs/agencyteam/rules.md` (the row *"A
  Turn the Human stopped is not a failure"*), **Spec §15.8**.
- **Deliverable:** Add `tests/Huddle.Tests/Conformance/TurnLifecycleTests.cs` with four tests:
  (a) a Turn produces several `MessageDelta` envelopes before `TurnCompleted`, and a Message
      lands in the Transcript;
  (b) a Stop sends `session/cancel` to the peer and the Turn ends **stopped, not failed** — no
      health state raised, the consecutive-failure streak unbroken;
  (c) disposal is observed by the peer as `session/close`;
  (d) changing the Persona's `Adapter` restarts it, and the **new** peer receives the new prompt.
- **Acceptance:** All four green.

### Task 10.6 — One process-mode conformance test

- **Goal:** **Spec §15.8 (T-30)** — the only test that exercises `AgentProcessLauncher`.
- **Read first:** `src/Huddle.Acp/Hosting/AgentProcessLauncher.cs`,
  `src/Huddle.App/Acp/AgentProcessOptionsFactory.cs`, **Spec §15.8**.
- **Deliverable:** Add `tests/Huddle.Tests/Conformance/ProcessModeTests.cs` with **exactly one**
  test: a Persona on an Adapter Profile whose `Command` is the built `mock-acp` executable
  completes one Turn. Locate the executable by walking up to `Huddle.slnx`. Keep it to one test —
  every other conformance test is in-proc, per **Spec §6.10 (Two modes)**.
- **Acceptance:** Green, and the suite's total wall-clock rise is under two seconds.

---

# D11 — Documentation

**Spec §16.** Binding documents; each change is announced.

### Task 11.1 — Widen `language.md`

- **Goal:** Apply **Spec §16** to the binding glossary.
- **Read first:** `docs/agencyteam/language.md` (the **Adapter**, **Model** and **Effort**
  entries), `docs/agencyteam/CONTEXT.md` (the note on not copying the glossary), **Spec §16**.
- **Deliverable:** Replace the **Adapter** entry and the **Effort** entry with the wording in
  **Spec §16**; add the new **Adapter Profile** entry; **leave the Model entry unchanged** and add
  a one-line note recording that roadmap item 12 predicted it would break and it did not.
- **Acceptance:** `docs/agencyteam/language.md` contains no claim that the Adapter is a single
  package. No other file duplicates the glossary.

### Task 11.2 — Widen one `rules.md` row

- **Goal:** Apply **Spec §16 (`rules.md` — one row widens)**.
- **Read first:** `docs/agencyteam/rules.md` (the row *"App Tool names must be spelled
  `mcp__team__*` in the system prompt"*), **Spec §16**, **Spec §4 (P5)**.
- **Deliverable:** Replace that row with the wording in **Spec §16**. **Do not** weaken the
  sibling row *"A Prompt's text never contains `mcp__team__`; the prefix is filled in by code"* —
  it still holds, and now holds per Adapter.
- **Acceptance:** The row names the Adapter Profile as the source of the prefix and cites the
  golden test that pins both forms.

### Task 11.3 — Rewrite roadmap item 12 and draft the ADRs

- **Goal:** Record that this specification supersedes the written plan, per **Spec §17 (Candidate
  ADRs)**.
- **Read first:** `docs/agencyteam/roadmap.md` §12 and the Ordering section,
  `docs/adr/0011-a-rename-moves-the-teammate-not-its-history.md` (for house ADR shape),
  **Spec §14**, **Spec §17**.
- **Deliverable:** Rewrite roadmap item 12 to record what was delivered and, in the house style of
  that file, what the original entry got wrong: there is no second `IAgentHostFactory`, no second
  `IModelCatalog`, the **Model** definition survived and **Effort**'s did not, and the `ToolKind`
  ask rested on a false premise. Draft the three ADRs named in **Spec §17**.
- **Acceptance:** Item 12 no longer describes an in-process bridge. Each ADR states its decision,
  its alternatives and its consequences.

---

# D12 — Live (Tier 4)

**Spec §15.9.** The only Agency-dependent work.

> **Status, 2026-09-18.** Task 12.1 as originally written was **not achievable**, and was closed by
> its own second branch — the written report at
> [`docs/Huddle.Adapters-LiveFindings.md`](Huddle.Adapters-LiveFindings.md). It is replaced below by
> 12.1a and 12.1b. Task 12.2's steps are written but **have never been executed**, so its acceptance
> is unmet. See [the handoff](Huddle.Adapters-Handoff.md) for what to pick up first.

### ~~Task 12.1 — Re-point at `agency-acp` and re-run D10 unchanged~~ — superseded

Kept for the record. It said *"Change no test"* and assumed re-pointing was a configuration change.
It is not: every D10 test but `ProcessModeTests` is wired to `MockAdapterFixture`'s in-proc
launcher, and — the part nobody saw until Phase 7 — five of the six conformance files assert
through `FakeAcpAgent.Received`, which no real Adapter can provide. See **Spec §15.9's amendment**
and the D-12 amendment in **Spec §17**.

### Task 12.1a — Make the portable half of D10 actually portable

- **Goal:** **Spec §15.9 (T-31a)**. Close **D-4** in
  [Live findings](Huddle.Adapters-LiveFindings.md).
- **Read first:** `tests/Huddle.Tests/Conformance/MockAdapterFixture.cs` (202 lines; the launcher
  substitution is lines 150-152 and the `Agent` oracle is returned at 178), every file in
  `tests/Huddle.Tests/Conformance/`, **Spec §15.8** (the `portable` / `mock-only` / `split`
  column), `docs/agencyteam/testing.md`.
- **Deliverable:** Two changes, in this order.
  1. **Classify every existing conformance assertion** against Spec §15.8's column. Where a test is
     marked **split** (T-26, T-28), separate it into two test methods — a portable one and a
     mock-only one — rather than leaving one method that is half portable. Name the mock-only ones
     so the constraint is visible at the call site, e.g. a `MockOnly_` prefix or an xUnit trait;
     pick one and apply it to all of them.
  2. **Parameterise `MockAdapterFixture` over its launcher.** Add a factory path that does **not**
     `RemoveAll<IAgentProcessLauncher>()` and instead configures an Adapter Profile whose `Command`
     is a supplied executable. The existing in-proc path must stay the default and must stay
     byte-identical in behaviour — every currently-green conformance test passes unmodified.
  - **Do not** try to give a real Adapter a `Received` equivalent. There is no wire call for it and
    inventing one would make the mock a second implementation of Huddle behaviour (**Spec §4, P8**).
- **Acceptance:** `dotnet test Huddle.slnx --` fully green with no behaviour change on the in-proc
  path. Every conformance test is unambiguously portable or mock-only, and that is readable from
  the test itself rather than from this document.

### Task 12.1b — Run the portable half against `agency-acp`

- **Goal:** **Spec §15.9 (T-31b)**.
- **Read first:** Task 12.1a's output, [Live findings](Huddle.Adapters-LiveFindings.md),
  **Spec §12 (E-18)**.
- **Prerequisite:** Agency has **published**. Take the Adapter from **nuget.org** —
  `AgencyDotNet.Acp 0.1.198-ga453511f0e` — never from a local build or a drop folder; the handoff's
  traps cover how to run the feed artifact and why a drop must not stand in for it.
- **Deliverable:** Configure an Adapter Profile whose `Command` is the published `agency-acp` and
  whose `EnvironmentOverrides` carry whatever its configuration needs, then run the **portable**
  conformance tests against it. The mock-only tests stay on the mock — that is correct, not a gap.
  Any failure is a genuine mock-vs-contract divergence: record it in Live findings, do not paper
  over it.
- **Acceptance:** The portable set green against `agency-acp`, or each divergence named in writing.

### Task 12.2 — Manual checklist

- **Goal:** **Spec §15.9 (T-32, T-33)** — the half of the milestone no automated test contains.
- **Read first:** `docs/agencyteam/manual-tests.md`, **Spec §15.9**.
- **Deliverable:** Add checklist steps: two Personas in one Room on different Adapters; live
  chunked text in the browser; Stop leaves both resumable; and — separately, because no test can
  settle it — whether a real local model actually calls `get_help` unprompted.
- **Acceptance:** Steps added, executed, and their outcomes recorded in the file.
- **Status, 2026-09-18 — half done.** The steps exist as
  `docs/agencyteam/manual-tests/adapters.md` (area 15, four tests: ADAPTERS-01 to -04). **None has
  been executed** — all four Tracker rows are `Active` with empty result columns, so the acceptance
  above is **not met**.
  - **ADAPTERS-01 and -02 are free and runnable today** — `mock-acp` plus `node`, no GPU, no money.
    These two *are* the milestone's Huddle half. Run them first.
  - **ADAPTERS-03** is paid (💰) and needs a Claude subscription.
  - **ADAPTERS-04** was blocked by D-1 and is unblocked the moment Agency publishes. Its blocking
    note in the area file and its Tracker note must both be cleared when that happens — the note
    names a version, so it will read as stale rather than wrong if it is missed.

---

## Sequencing summary

| Deliverable | Depends on | May start |
| --- | --- | --- |
| D0 Mock adapter | — | **now** |
| D1 Adapter profile | — | **now** |
| D2 Process options | D1 | after D1 |
| D3 Frontmatter | — | **now** |
| D4 Persona record | D3 | after D3 |
| D5 Factory + prefix | D1, D2 | after D2 |
| D6 Catalogue dispatch | D1, D2 | after D2 |
| D7 Supervisor | D1, D4 | after D4 |
| D8 Card | D1, D6 | after D6 |
| D9 `Huddle.Acp` | — | **now** |
| D10 Conformance | D0 + the deliverable each test exercises | after D5 |
| D11 Docs | D5 | after D5 |
| D12 Live | 12.1a: all (no Agency). 12.1b: + a **published** `agency-acp`. 12.2: ADAPTERS-01/02 need nothing | 12.1a and ADAPTERS-01/02 **now**; the rest on publication |

**Critical path:** D1 → D2 → D5 → D10 → D12. **D5 is the long pole** — it changes the prompt every
Persona receives, and its acceptance includes *the existing golden must not change*.

**Nothing but D12 waits on Agency.NET** (**Spec §1.3, O-8**).
