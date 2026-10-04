# Analyzer decisions

Which compiler and analyzer rules are switched off or turned up in this solution, what was
decided about each, and what the decision rested on. Read it before enabling, disabling or
"cleaning up" a rule, so the answer is a lookup and not a guess.

This is a record of the **state of each rule**, not a log of the work that produced it. A rule is
either [enabled](#enabled) or [denied](#denied), and each section says why and how sure we are.
How a rule got there is in git history, not here.

The enforced house style is [CSharpPrinciples.md](../../agents/CSharpPrinciples.md); the files that
carry the settings are `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` and
`BannedSymbols.txt` at the repository root.

First written 2026-10-03 against SonarAnalyzer.CSharp 10.32.0.713. Update it in the same
commit as any change to those files.

**Where it stands.** The denylist was 282 entries on `main`. It is now 32:

| Denied because | Count |
| --- | --- |
| The rule fires here and is noise, a false positive, or needs a change outside the chat side | 19 |
| Tried, did not fire | 4 |
| Sonar telemetry (`S9999-*`), not code rules | 9 |

Every other default-on Sonar rule is enabled. The goal is still to narrow the 20 and the 4; every entry should have a reason on this page, not a number in a list.
## How a rule gets its severity

| Layer | Where | Effect |
| --- | --- | --- |
| Compiler and `AnalysisLevel=latest-recommended` | `Directory.Build.props` | The `CS` and recommended `CA` rules are warnings, and `TreatWarningsAsErrors` makes a warning a failed build. |
| Code-style rules | `.editorconfig` | `IDE` rules run in the build (`EnforceCodeStyleInBuild`). Only `warning` or `error` fails it. |
| Sonar, default-on | `<NoWarn>` in `Directory.Build.props` | About 326 Sonar rules are on by default. They are switched off **by ID**, because Sonar's categories contain spaces and the `category-*` bulk override cannot match them. |
| Sonar, opted in | `.editorconfig` | A rule leaves the denylist and gets an explicit `dotnet_diagnostic.Sxxxx.severity = warning`. |
| Banned APIs | `BannedSymbols.txt` | `Microsoft.CodeAnalysis.BannedApiAnalyzers` raises `RS0030`. |
| Prose rules no analyzer can express | `agents/scripts/Check-*.ps1` | `Check-Diff.ps1` (null-forgiving `!`, reasonless pragmas), `Check-TestDocs.ps1` (a `///` summary on every added test) and `Check-Analyzers.ps1` (the enabled rules still fire; the config files agree). |

A rule in `NoWarn` stays off even if `.editorconfig` sets a severity for it. **To enable a
rule, remove it from `NoWarn` and add the `.editorconfig` line.** Doing only one of the two
does nothing.

## Why so many rules are denied

The denylist began as a bulk switch-off, not 270 reviewed decisions. The repository's `.editorconfig`
was carried over from the Agency repository without the analyzer behind its `S####` rules, so
those rules sat inert. When the package was added, enabling it would have failed the build on
every default rule at once, so every default rule was denied by ID and a curated list was opted
back in. The list has since been reviewed rule by rule; the sections below state, for each group,
how much judgement went into it.

## What was measured

The baseline was taken on 2026-10-03 with the whole denylist lifted
(`dotnet build Huddle.slnx -p:TreatWarningsAsErrors=false -p:NoWarn=CS1591%3BNU1701`). Hits were
de-duplicated by file and position, because a diagnostic can be printed once per target.

- 273 of the denylisted rules were default-on and checked.
- **237 of the 273 had zero hits** in `src` and `tests` combined.
- The other 36 fired somewhere. Fourteen of those were fixed and enabled; the rest stay denied
  (see [Denied: the rule fires here](#denied-the-rule-fires-here)).

Two cautions on those numbers:

1. They are a baseline from before the rules were enabled, not a current figure. Re-measure before
   relying on them.
2. Overriding `NoWarn` on the command line replaces every `NoWarn` in every project, so the
   per-project `CA` suppressions are lifted too. Read only the `S####` lines from that build.

## Proof: the analyzer probes

Enabling a rule proves nothing by itself: a rule can be on and never fire. Two projects outside
`Huddle.slnx` hold one deliberate violation per rule, and `agents/scripts/Check-Analyzers.ps1`
builds them and compares what fired with what each `// probe: Sxxxx` tag expects.

- `tests/Huddle.AnalyzerProbes` holds the general probes. `tests/Huddle.AnalyzerProbes.Tests` holds the
  xunit ones. **They are two projects because Sonar treats any project that references a test
  framework as test code and skips most rules there.** 32 probes that fail with an `xunit.v3`
  reference passed without it, so the same rule can be live in `src` and silent in
  `tests`. Do not read a clean test project as evidence a rule passes.
- The probe tag is answered only if that rule fires on the tag's own line or the next 8, so one
  probe cannot be satisfied by another's output. A probe that stops firing fails the check: that is an
  analyzer upgrade or a config change breaking a rule silently.
- A probe must compile. The compiler's declaration errors stop the analyzers from running at all, which
  is why a rule that needs invalid C# (`S3464`, the C# 14 identifier rules) cannot be probed.
- `tests/Huddle.AnalyzerProbes/Unprobed.txt` lists every enabled rule with no probe and one of five
  reasons. The check fails if an enabled rule is in neither place, or if a listed rule gets a probe or
  stops being enabled.
- The same script checks the configuration: every `NoWarn` token is a well-formed ID and none repeats,
  no rule is both denylisted and given a severity, every denylisted Sonar rule is named on this page,
  and the "It is now N" count above matches `Directory.Build.props`.

`Check-All.ps1` runs it only when the branch touches the analyzer configuration, because it builds two
projects and takes about a minute.

Where the probes stand: of 295 enabled Sonar rules, 238 have a probe that fires and 57 do not. The 57 are
18 `no-technology`, 6 `compiler-rejects`, 7 `needs-package`, 22 `probe-did-not-fire` and 4
`not-yet-probed`. The `probe-did-not-fire` rules are the interesting ones; nobody has checked whether
the probe or the rule is at fault. The `IDE*` and `RS0030` rules in the first Enabled table are not Sonar
rules and are not tracked by the probes.

## Enabled

Whether an enabled rule actually fires is not argued on this page. It is tested:
[the analyzer probes](#proof-the-analyzer-probes) hold one deliberate violation per rule, and a rule
with no probe is listed in `tests/Huddle.AnalyzerProbes/Unprobed.txt` with the reason. Titles below are
paraphrased; confirm the exact wording on rules.sonarsource.com before quoting one.

### Prose rules that had no enforcement

| Rule | Enforces | Hits when enabled |
| --- | --- | --- |
| `S4462` | No `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` | 0 |
| `S6354` | `TimeProvider`, not `DateTime.Now` or `DateTimeOffset.UtcNow` | 5, fixed |
| `IDE0009` | `this.` on field, property, method and event access | 0 (not covered by the probes, which only track Sonar rules) |
| `IDE0161` | File-scoped namespaces | 0 (not covered by the probes, which only track Sonar rules) |
| `IDE0330` | Lock on `System.Threading.Lock` | 0 (not covered by the probes, which only track Sonar rules) |
| `IDE1006` | Private fields are camelCase with no `_` prefix; private constants stay PascalCase | 0 (not covered by the probes, which only track Sonar rules) |
| `RS0030` | Parameterless `new Random()` is banned (`BannedSymbols.txt`) | 0 |

`IDE0003` is also set to `warning`. It is the "remove `this.`" rule and cannot fire while the
qualification option is `true`; it is set so the pair stays consistent if the option is flipped.

### Security rules that were denied only because they were default-on

All 18 had zero hits.

`S2257`, `S2612`, `S2092`, `S3330`, `S2115`, `S4433`, `S4502`, `S4507`, `S5332`, `S5344`, `S5443`,
`S5445`, `S5659`, `S5693`, `S5753`, `S5766`, `S2077`, `S2245`. Most are probed. The ones that are not
(`S2115`, `S4433`, `S5659`, `S5753`, `S5766` need packages this repository does not reference;
`S2612` and `S5332` did not fire on the probes written for them) are in `Unprobed.txt`, so treat
their coverage as unknown. Enabling a rule that never fires costs nothing, but do not read "zero
hits" as "checked".

The Haiku audit flagged `S2077` as denied while the code runs SQL. A search of `src` for
`CommandText` built by interpolation or concatenation found none, so it was not hiding a live
hole; enabling it makes that permanent.

### Correctness rules

Thirty-two rules for code that does not do what it reads as doing.

| Rule | Guards |
| --- | --- |
| `S1862`, `S1871`, `S3923`, `S1764`, `S1656`, `S2201`, `S2674`, `S2688`, `S2692`, `S2183`, `S2184`, `S2275`, `S2437`, `S3169`, `S3172`, `S4143`, `S4581`, `S1944`, `S1696`, `S2995`, `S2996`, `S3005`, `S5856`, `S2701` | Duplicate branches, self-assignment, ignored results, bad shifts, wrong comparisons, test assertions that cannot fail |
| `S1163`, `S1848`, `S2234`, `S2386`, `S2737`, `S2955`, `S3244`, `S3881` | Throw in `finally`, object created and discarded, arguments in a different order than the parameters, public static mutable field, a `catch` that only rethrows, unconstrained generic compared to `null`, anonymous delegate used to unsubscribe, wrong `IDisposable` pattern |

`S3172` and `S1944` did not fire on their probes (see `Unprobed.txt`). `S1848` and `S1163` overlap
`CA1806` and `CA2219`, which were already on, so they add no new coverage.

### Hazards, types, logging, tests, web and security: 88 rules judged on their titles

These had zero hits on the whole solution, were read on their Sonar title (listed with
`agents/scripts/List-SonarRules.cs`), and guard correctness, security, a house rule or a
technology this repository uses. The grouping is from the titles; no sites were read because there
were none.

| Group | Rules |
| --- | --- |
| Hazards and API misuse | `S3236`, `S3343`, `S4583`, `S3346`, `S5034`, `S3869`, `S3363`, `S1215`, `S3971`, `S3981`, `S3998`, `S3875`, `S1048`, `S2291`, `S2139`, `S3877`, `S3397`, `S3444`, `S3011`, `S3885`, `S2953`, `S3060`, `S2934`, `S3449`, `S2757`, `S2761`, `S2198`, `S3440`, `S3603`, `S4201`, `S3457`, `S3458`, `S4275`, `S2372`, `S2376`, `S4456`, `S3889`, `S3443`, `S2178`, `S3464` |
| Types, enums, overrides and attributes | `S4070`, `S2345`, `S2346`, `S4015`, `S4019`, `S3600`, `S3262`, `S3466`, `S3427`, `S3450`, `S3451`, `S3447`, `S4260`, `S4545`, `S3251`, `S2368`, `S3887`, `S2696`, `S3010`, `S1104`, `S2290` |
| Logging (matches the `{PascalCase}` template rule) | `S6668`, `S6673`, `S6678`, `S6672`, `S6618`, `S6580` |
| Test hygiene | `S2925`, `S3433`, `S2187`, `S3415`, `S2970` |
| Blazor and web | `S6797`, `S6798`, `S6800`, `S6962`, `S6967`, `S6964`, `S6930`, `S6931`, `S6934`, `S6965`, `S6961`, `S6932` |
| Security | `S6377`, `S7039`, `S1313`, `S2857` |

The probes show most of these fire. Twenty-odd did not, on probes built for them (`S2187`, `S2198`,
`S2970`, `S3433`, `S3449`, `S3458`, `S4015`, `S4019`, `S6797`, `S6930`, `S6931`, `S6960`, `S6967`,
`S6968` and others): either the probe was not what the rule looks for or the rule is narrower than
its title. Nobody has checked which. `S3458` was listed here as proven by an earlier hand probe; the
permanent probe could not reproduce it, so the earlier claim should not be relied on.

### Enabled after fixing real hits

Each of these fired on real code, so each is proven. The fix is what the rule asked for, except
the two suppressions, which are recorded as such.

| Rule | What it caught | Fix |
| --- | --- | --- |
| `S1871` | `RoomSessionPool` victim selection and `WikiLinkParser` line counting each had two identical branches | Merged into one condition |
| `S2701` | `LibraryEditorTests`, two assertions of the form `Assert.Equal(false, x)` | `Assert.False(Assert.IsType<bool>(x))` |
| `S6354` | Five uses of the ambient clock | Injected `TimeProvider` |
| `S1643` | `PersonaFrontmatter.SplitFlowListItems` appended to a `string` in a loop, three places | `StringBuilder` |
| `S2123` | Two test counters declared as `void Count() => n++`, read later | `Interlocked.Increment`, which the same file already uses elsewhere |
| `S3241` | Two `SetupEditorModule` test helpers returned a tuple no caller used | Return type changed to `void` |
| `S3237` | `DuplexStream.Position`'s setter never reads `value` | **False positive**: a deliberate `NotSupportedException` setter on a `Stream` override. Narrow `#pragma` with the reason. |
| `S2971` | `TaskStoreMemoryWarningTests.Warnings` calls `ToList()` on a live list | **Looks useless, is not**: the logger appends from other threads and the call is the snapshot. Narrow `#pragma` with the reason; dropping it risked a flaky test. |
| `S1481` | Fourteen unused locals in tests, one in `DotAcpAgentHostFactory` | Unused `ct` locals deleted; unused `out` variables became `out _`; unused service locals became `_ =`. In `DotAcpAgentHostFactory` the call can throw or construct a service, so it became `_ = ...` and is kept. |
| `S1144` | `RoomSession.MaxDescriptionLength`, a test helper `PersonaText`, a test field `Empty`, and `appearanceLogger` | Deleted |
| `S1066` | Nested `if` in `LibraryFileKinds` (the WebP signature check) and two in `SkillCatalogTests` | Merged into one condition |
| `S1117` | Locals named `index`, `open`, `grouping` and `gate` that hid a field | Renamed to `textIndex`, `opening`, `groupingKey` and `openGate` |
| `S2292` | `ManualTimeProvider.UtcNow` wrapped a backing field | Auto-property |
| `S6562` | `new DateTime(2026, 11, 3)` in a test | `DateTimeKind.Unspecified`, the same kind it had before, now stated |
| `S2479` | A raw `0x1F` control character inside the effort cache key in `ModelCatalogProbe` | `string.Concat(profileId, "\u001F", model)`: the same key, written so a reader can see it |
| `S3358` | Eight nested ternaries: assignee resolution in `CreateTaskTool` and `UpdateTaskTool`, the avatar choice in `Appearance` and `TeammateCard`, the page title in `TeamPage`, `DetailKey` in `Tasks`, a note title in `MarkdownRenderer`, a skill's source in `SkillStore` | `if` chains, property-pattern and tuple `switch` expressions |
| `S108` | Forty-one empty blocks: 38 empty `catch` blocks (the `DispatchAsync` helper copied into 18 razor components, plus `AgentConnection` and `TempDataDir`) and one empty `using` in `TeammateLayoutMigrationTests` | A comment saying why swallowing is safe, matching the house rule for general `catch` blocks; the `using` became `File.Create(markerPath).Dispose()`. **Exempt in the ACP subtree** (`src/Huddle.Acp`, `src/Huddle.Console`, `tests/Huddle.Acp.Tests`) by a path-scoped `none` in `.editorconfig`, because four empty blocks there (`AgentProcess.cs` twice, `ConsoleLineReader.cs`, `AgentProcessLauncherTests.cs`) belong to the ACP owner. Delete that section when they are filled. |
| `S1118` | `public partial class Program` had an implicit public constructor | Protected constructor with a doc comment; the class stays `public partial` for `WebApplicationFactory<Program>` |

Two side effects of these fixes are worth knowing:

- `AgentConnection.HandleMessageDeltaAsync` used `this.Agent!.Id, this.Agent!.Name`. It now reads
  `this.Agent ?? throw new InvalidOperationException(...)` once, so the invariant "the handshake set
  the Agent first" is stated instead of asserted with `!`.
- `Tasks.razor`'s `DetailKey` returned a string literal, which `Check-Diff` reads as user-facing
  text with no test; the fallback is a named constant, `NoDetailKey`.

### Enabled with no hits, each proven

Each was proven by a deliberate violation that failed the build with its ID.

| Rule | Guards |
| --- | --- |
| `S1244` | Floating-point equality |
| `S6588` | A hand-built Unix epoch instead of `UnixEpoch` |
| `S6607` | Sorting before filtering (`OrderBy` before `Where`) |
| `S6561` | `DateTime.Now` used for timing or benchmarking |
| `S6608` | `Enumerable` methods on an `IList` where indexing works (the written `[0]` not `.First()` rule) |
| `S6609`, `S6613` | `Enumerable` extension methods on a `Set` or `LinkedList` where the type's own `Min`/`Max` or `First`/`Last` property exists |
| `S6617` | `Any` for a simple equality check where `Contains` works |
| `S1940` | Boolean checks written inverted |
| `S3247` | Duplicate casts |
| `S4635` | `Substring` where a start index would do |
| `S6640` | Unsafe code blocks |
| `S907` | `goto` |
| `S1168` | Returning `null` where an empty array or collection belongs |
| `S1123` | `[Obsolete]` without an explanation |
| `S1116` | Empty statements |
| `S2166` | A class named `...Exception` that does not extend `Exception` |
| `S3993` | A custom attribute without `AttributeUsage` |
| `S927` | An override whose parameter names differ from the base declaration |
| `S1210` | `IComparable` implemented without `Equals` and the comparison operators |

### The rest of the denylist, cleared in one step

Seventy-seven rules were left on the denylist after review and had zero hits on the whole solution,
so enabling them changes nothing today and guards new code. Enabling them did not break the build
and no code changed. They are in five loose groups, judged on their Sonar titles only.

| Group | Rules |
| --- | --- |
| Technology this repository does not use | `S6420`, `S6419`, `S6424`, `S6422` (Azure Functions), `S3597`, `S3598` (WCF), `S4210` (WinForms), `S4428`, `S4159`, `S4277` (MEF), `S4200`, `S4211`, `S3925`, `S3927`, `S3926` (legacy serialization), `S3431` (NUnit), `S6670`, `S6675` (the `Trace` API), `S8380`, `S8381`, `S8367`, `S8368` (C# 14 keyword-escape naming) |
| Overlap with another analyzer (they would report a finding twice, which is moot at zero hits) | `S112` (`CA2201`), `S2629` (`CA2254`), `S3260` (`CA1852`), `S1172` (`IDE0060`), `S3445` (`CA2200`), `S101` (`IDE1006`), `S1155` (`CA1860`), `S1905` (`IDE0004`), `S2223` (`CA2211`), `S2681` (`IDE0011`), `S1699`, `S2306`, `S4220`, `S3903` |
| Previously kept off for a stated reason that did not survive a measurement | `S4036` (agent adapters are spawned by command name: no hit), `S1607` (conditional `Skip`: no hit), `S6610` (the house `StringComparison.Ordinal` calls do not trip it), `S1075` (fixtures hardcode URIs: no hit) |
| Style, naming and design | `S1110`, `S1121`, `S1133`, `S1134`, `S1185`, `S1186`, `S1199`, `S1264`, `S1694`, `S1939`, `S2219`, `S2344`, `S3217`, `S3246`, `S3249`, `S3261`, `S3263`, `S3400`, `S3453`, `S3456`, `S3459`, `S3604`, `S3897`, `S3904`, `S3963`, `S3972`, `S3973`, `S4035`, `S4052`, `S4061`, `S4136`, `S4524`, `S4663`, `S6575`, `S6960`, `S6968`, `S818` |

Most of these are probed; the 22 for technology this repository does not use cannot be (`Unprobed.txt`
says `no-technology`), and `S1121`, `S1172`, `S3217`, `S3604`, `S3972`, `S6968`, `S6960` and a few
others did not fire on their probes. `S3604` was previously listed as proven by hand; the permanent
probe did not reproduce it.

## Denied

### Denied: the rule fires here

Nineteen rules fire on this codebase and stay denied; they are the only denied rules that do. Counts are from the baseline above; a rule
listed under "Enabled after fixing real hits" is no longer in this table. **Reviewed** means
someone read the sites and decided; **Noise** means the decision rests on the diagnostic's message
and its hit count, with the sites not read; **Not enabled** means the rule is not wrong, but a site
is in the ACP effort's subtree (the chat side does not edit it) or the fix is churn for no defect.

| Rule | Message | src | tests | Status and decision |
| --- | --- | --- | --- | --- |
| `S6667` | Logging in a `catch` should pass the exception | 4 | 0 | **Reviewed, keep off.** The sites log an expected cancellation (`RoomSession.cs`) or a bad colour in user data (`AvatarStore.cs`); a stack trace would only be noise. Fixing it would make the logs worse. |
| `S3264` | Unused event | 2 | 0 | **Reviewed, keep off.** False positive: `PersonaStore` raises `PersonaRenamed` and `PersonaRemoved` through `GetInvocationList()`, which the rule does not see. |
| `S1994` | Loop's stop incrementer is not in the stop condition | 1 | 0 | **Reviewed, keep off.** `TaskIdAllocator` uses `for (suffix = 2; ; suffix++)` with a `return` inside; it is deliberate. |
| `S8969` | Redundant null-forgiving `!` | 3 | 32 | **Reviewed, keep off.** Tried on the three source sites: at `PersonaRunner.cs:560` and `RoomSessionPool.cs:170` removing the `!` fails the build with `CS8602`, so the rule is wrong there. Only the `AgentConnection.cs` site was right. The 32 test sites were not tried; a rule that is wrong on two of three cannot be trusted without checking each fix against the compiler. |
| `S8970` | `!` where nullable warnings are disabled | 4 | 0 | **Reviewed, keep off.** False positive on all four sites. They are `= default!` in `.razor` files, where nullable is enabled: removing the `!` fails the build with `CS8625` or `CS8601`. |
| `S3878` | Needless array creation for `params` | 3 | 3 | **Reviewed, keep off.** It contradicts `S3220`, which is already on: `Split([',', ';'])` trips `S3878`, and `Split(',', ';')` trips `S3220`. Both cannot be on for `string.Split`. |
| `S2325` | Method could be `static` | 3 | 0 | **Reviewed, keep off.** All three sites are razor handlers. Making one `static` breaks every `this.Member(...)` call in the markup (`CS0176`) and the generated event lambdas (`CS1662`), and the house rule requires `this.`. `CA1822` already covers ordinary classes. |
| `S6966` | Await the async variant (`CancelAsync`, `DisposeAsync`) | 16 | 923 | Noise. It is a preference for the async overload, not a missing `await`. Almost all hits are tests. |
| `S3267` | Loop could use `Where` | 69 | 2 | Noise. A style preference. |
| `S1135` | `TODO` comment | 2 | 2 | **Reviewed, keep off.** Three of the four hits are false positives: the rule matches `ToDo`, the name of a task status, in `TaskState.cs` and `TestTasks.cs` (`Default: ToDo`). The fourth is a real follow-up note in `GetHelpToolTests.cs`, which a tracker would not want blocked on a build. |
| `S125` | Commented-out code | 7 | 4 | Not enabled: two of the eleven sites are in `Huddle.Acp` and `Huddle.Console`. Plausibly worth a look, since commented-out code rots. |
| `S2094` | Empty record or class | 4 | 0 | Not enabled: three of the four sites (`AssemblyMarker`, `AgentPrompt`, `ElicitationResult`) are in `Huddle.Acp`. |
| `S2365` | Property copies a collection | 4 | 1 | Not enabled: two of the five sites are in `Huddle.Acp` and `Huddle.Acp.Tests`. The others are `Aliases`, `WatchedTaskIds` and `OnlineAgentIds`; not read. |
| `S4144` | Method identical to another | 1 | 8 | Not enabled: one site is in `Huddle.Acp.Tests`. The rest are test methods that share a body. |
| `S1450` | Field should be a local | 1 | 0 | Not enabled: the one site is `ConsoleLineReader` in `src/Huddle.Console`, which the chat side does not edit (`CLAUDE.md`). Revisit with the ACP owner. |
| `S2743` | Static field in a generic type | 0 | 1 | Not enabled: the one site is `ListLogger` in `tests/Huddle.Acp.Tests`. |
| `S3376` | Class name should end in `Exception` | 0 | 1 | Not enabled: the one site is `FakeRpcError`, a file linked into two assemblies (`Huddle.Acp.Tests` and `Huddle.MockAdapter`) and owned by the ACP effort. Renaming it is their change. |
| `S3218` | Member shadows an outer member | 0 | 4 | Not enabled, by choice: four nested test fixtures expose a member named like one on the outer class (`Chat`, `Directory`, `Team`, ...). Renaming ripples through every use, for no defect. |
| `S3398` | Move method inside its class | 0 | 1 | Not enabled, by choice: one shared test helper (`WaitUntilAsync`) used by a single nested class. Moving it is churn. |

The eight **Reviewed** rows are the only ones where the sites were read. For every other row,
treat "Noise" as "not worth the cost at the time", not as "proven harmless". Seven rows wait on the
ACP owner (`S125`, `S2094`, `S2365`, `S4144`, `S1450`, `S2743`, `S3376`); two (`S3218`, `S3398`)
wait on a decision to accept churn.

### Denied: tried, did not fire

| Rule | Title | What was tried |
| --- | --- | --- |
| `S2114` | Collections should not be passed as arguments to their own methods | A probe built to trigger it. |
| `S2328` | `GetHashCode` should not reference mutable fields | A probe built to trigger it. |
| `S3610` | Nullable type comparison should not be redundant | Five shapes of nullable comparison. |
| `S4050` | Operators should be overloaded consistently | `+` without `-`, `Equals` without `==`, and `==` without `Equals`. |

Either the probe was wrong or the rule is narrower than its title; nobody checked which, so all
four stay denied until someone does. They have zero hits, so enabling them would cost nothing, but
a rule known not to fire only gives false cover.

### Denied: Sonar telemetry

Nine `S9999-*` entries are Sonar's own bookkeeping diagnostics, not code rules.

To clear any rule here, follow [Changing a rule](#changing-a-rule).
## Non-Sonar settings

| Setting | Where | Decision and reason |
| --- | --- | --- |
| `CS1591` suppressed | `Directory.Build.props` | Turning on the documentation file (needed so `IDE0005` runs) also turns on `CS1591`. XML docs are a house rule enforced by review, the `PreToolUse` hook and `Check-TestDocs.ps1`, not by the compiler. |
| `CA2007` off | `.editorconfig` | No `SynchronizationContext` to deadlock on in ASP.NET Core or the generic host, so blanket `ConfigureAwait(false)` was judged noise. |
| `IDE0008` off | `.editorconfig` | It conflicts with the `var` preferences. |
| `IDE0058` silent | `.editorconfig` | An unused expression value is common and harmless here. |
| `IDE0005`, `IDE0011` as errors | `.editorconfig` | Cheap, unambiguous, and they cannot silently regress. |
| `CA1062` as warning | `.editorconfig` | Public-API guard; kept at warning because existing call sites were not audited when it was added. |
| `CA1707` off in test projects | `.csproj` | Tests use `Method_Scenario_Expectation` names. |
| `CA1861` off in `Huddle.Acp.Tests` | `.csproj` | Constant arrays in test data. |
| `CA2007` also in `Huddle.App` and `Huddle.Contracts` | `.csproj` | **Redundant** with the `.editorconfig` setting. Safe to remove; not yet removed. |
| `CA1848` off in `Huddle.App` and `Huddle.Contracts` | `.csproj` | Documented allowance in `CSharpPrinciples.md`: direct `logger.LogX` calls are fine there. Leave it. |
| `CA1031` off in `Huddle.App` and `Huddle.Contracts` | `.csproj` | **Open.** With the suppression lifted about 40 broad `catch` sites show up, far more than the four the Haiku audit counted. See [Deferred](#deferred). |

## Deferred

| Rule | Hits | Why it is not enabled |
| --- | --- | --- |
| `S2221` (do not catch `Exception`) | 32 in `src`, mostly `RoomSession.cs` | The sites are supervisor loops that are allowed to catch broadly. Enabling it needs a commented `#pragma` at each. Decide together with the blanket `CA1031` suppression. |
| `IDE0090` (use `new()`) | 112 | Matches the house style, but needs a mechanical bulk fix first. |
| `IDE0290` (primary constructors) | 13 | Cheap to fix, then enable. |
| `S4457`, `CA1307` | 146, 33 | Noisy and do not match a written rule. |

## Inline suppressions audited

A Haiku agent read every inline suppression on 2026-10-03: six in `src` and three in `tests`. All
carry a real justification and a narrow scope. `S127` twice (a deliberate scan advance bounded on
the line above), `CA1031` in `Huddle.MockAdapter`'s entry point, `CS8524` (keeps `CS8509` alive),
`CA1711` for a wire-spec type name, `CA1720` for a JSON Schema type word, `CA1835`, `S3871`,
`CS0067` in tests. Two more were added since, both recorded under
[Enabled after fixing real hits](#enabled-after-fixing-real-hits): `S3237` and `S2971`. The
agent's other claims were spot-checked, not re-derived; its `CA1031` count was wrong, as above.

## Changing a rule

1. Read what the rule is. `dotnet run agents/scripts/List-SonarRules.cs` prints every rule's ID,
   severity and title. Judge it on that, not on its number.
2. Measure: build with the rule enabled and the denylist lifted, and read the hits. Zero hits is
   the cheap case; some hits means reading the sites.
3. Prove it fires. Add a probe to `tests/Huddle.AnalyzerProbes` (or `.Tests` for xunit rules) with a
   `// probe: Sxxxx` tag, or give the rule a reason in `Unprobed.txt`, then run
   `pwsh agents/scripts/Check-Analyzers.ps1`. "No output" does not mean "works".
4. Fix real findings in the same change. Do not suppress to get a build through; the suppression
   rules are in `CSharpPrinciples.md`. If fixing a hit breaks the build, the rule is wrong for that
   site: put it back and record why under
   [Denied: the rule fires here](#denied-the-rule-fires-here).
5. Move the rule between the groups in `Directory.Build.props` and add the `.editorconfig` line.
   **Diff the denylist against `HEAD` afterwards.** A scripted edit once glued two neighbours into
   one token (`S6575S6640`), which would have turned both rules back on at their default severity;
   the build stayed green because neither has hits.
6. Update this page in the same commit, by moving the rule between the sections above. Do not add
   a section for the change.
