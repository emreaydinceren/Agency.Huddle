# Analyzer decisions

Which compiler and analyzer rules are switched off or turned up in this solution, what was
decided about each group, and what the decision rested on. Read it before enabling,
disabling or "cleaning up" a rule, so the answer is a lookup and not a guess.

This is a record. The enforced house style is [CSharpPrinciples.md](../../agents/CSharpPrinciples.md);
the files that carry the settings are `Directory.Build.props`, `Directory.Packages.props`,
`.editorconfig` and `BannedSymbols.txt` at the repository root.

First written 2026-10-03 against SonarAnalyzer.CSharp 10.32.0.713. Update it in the same
commit as any change to those files.

**Where it stands.** The denylist was 282 entries on `main`. It is now 136: 29 rules that fire
here and are noise or reviewed false positives, 98 that were read and denied for a stated
reason, and 9 telemetry entries. The goal is to keep narrowing it; every entry should end up
with a reason on this page, not a number in a list.

## How a rule gets its severity

| Layer | Where | Effect |
| --- | --- | --- |
| Compiler and `AnalysisLevel=latest-recommended` | `Directory.Build.props` | The `CS` and recommended `CA` rules are warnings, and `TreatWarningsAsErrors` makes a warning a failed build. |
| Code-style rules | `.editorconfig` | `IDE` rules run in the build (`EnforceCodeStyleInBuild`). Only `warning` or `error` fails it. |
| Sonar, default-on | `<NoWarn>` in `Directory.Build.props` | About 326 Sonar rules are on by default. They are switched off **by ID**, because Sonar's categories contain spaces and the `category-*` bulk override cannot match them. |
| Sonar, opted in | `.editorconfig` | A rule leaves the denylist and gets an explicit `dotnet_diagnostic.Sxxxx.severity = warning`. |
| Banned APIs | `BannedSymbols.txt` | `Microsoft.CodeAnalysis.BannedApiAnalyzers` raises `RS0030`. |
| Prose rules no analyzer can express | `agents/scripts/Check-*.ps1` | `Check-Diff.ps1` (null-forgiving `!`, reasonless pragmas) and `Check-TestDocs.ps1` (a `///` summary on every added test). |

A rule in `NoWarn` stays off even if `.editorconfig` sets a severity for it. **To enable a
rule, remove it from `NoWarn` and add the `.editorconfig` line.** Doing only one of the two
does nothing.

## Why so many rules are denied

The denylist is a bulk switch-off, not 270 reviewed decisions. The repository's `.editorconfig`
was carried over from the Agency repository without the analyzer behind its `S####` rules, so
those rules sat inert. When the package was added, enabling it would have failed the build on
every default rule at once, so every default rule was denied by ID and a curated list was opted
back in. The denylist was never reviewed rule by rule.

That is why it is now split into three groups in `Directory.Build.props`, and why the next
sections state, for each, how much judgement actually went into it.

## What was measured

On 2026-10-03 the solution was built with the whole denylist lifted
(`dotnet build Huddle.slnx -p:TreatWarningsAsErrors=false -p:NoWarn=CS1591%3BNU1701`). Hits were
de-duplicated by file and position, because a diagnostic can be printed once per target.

- 273 of the denylisted rules were default-on and checked.
- **237 of the 273 had zero hits** in `src` and `tests` combined.
- The other 36 fired somewhere. The two outcomes are in [Enabled](#enabled) and
  [Denied on purpose](#denied-on-purpose).

Two cautions on those numbers:

1. The count is from the tree *before* the changes in the section below, so it is a baseline,
   not a current figure. Re-measure before relying on it.
2. Overriding `NoWarn` on the command line replaces every `NoWarn` in every project, so the
   per-project `CA` suppressions are lifted too. Read only the `S####` lines from that build.

## Enabled

**Proven** means a scratch file with a deliberate violation was built and failed with that rule's
ID, and the scratch file was deleted; or the rule fired on real code. A rule that merely produced
no output on the real code is **unproven**: it may be working, or it may never fire. Each table
below says which. Titles are paraphrased; confirm the exact wording on rules.sonarsource.com
before quoting one.

### Prose rules that had no enforcement

| Rule | Enforces | Hits when enabled |
| --- | --- | --- |
| `S4462` | No `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` | 0, proven with `task.Result` |
| `S6354` | `TimeProvider`, not `DateTime.Now` or `DateTimeOffset.UtcNow` | 5, fixed (proven by real hits) |
| `IDE0009` | `this.` on field, property, method and event access | 0, **unproven** |
| `IDE0161` | File-scoped namespaces | 0, **unproven** |
| `IDE0330` | Lock on `System.Threading.Lock` | 0, **unproven** |
| `IDE1006` | Private fields are camelCase with no `_` prefix; private constants stay PascalCase | 0, **unproven** |
| `RS0030` | Parameterless `new Random()` is banned (`BannedSymbols.txt`) | 0, proven |

`IDE0003` is also set to `warning`. It is the "remove `this.`" rule and cannot fire while the
qualification option is `true`; it is set so the pair stays consistent if the option is flipped.

### Security rules that were denied only because they were default-on

All 18 had zero hits.

| Status | Rules |
| --- | --- |
| Proven | `S2077` (a concatenated `CommandText`), `S2245` (a seeded `new Random(1)`) |
| **Unproven** | `S2257`, `S2612`, `S2092`, `S3330`, `S2115`, `S4433`, `S4502`, `S4507`, `S5332`, `S5344`, `S5443`, `S5445`, `S5659`, `S5693`, `S5753`, `S5766` |

The unproven ones need specific ASP.NET, JWT, cookie, LDAP or cryptography APIs, and none of the
code here uses them, so no probe was written. One probe was tried: `S5332` (clear-text
protocols) did not fire on a `new Uri("http://...")` literal, so treat that rule's coverage as
unknown. Enabling an unproven rule costs nothing, but do not read "zero hits" as "checked".

The Haiku audit flagged `S2077` as denied while the code runs SQL. A search of `src` for
`CommandText` built by interpolation or concatenation found none, so it was not hiding a live
hole; enabling it makes that permanent.

### Correctness rules

Twenty-four rules for code that does not do what it reads as doing: `S1862`, `S1871`, `S3923`,
`S1764`, `S1656`, `S2201`, `S2674`, `S2688`, `S2692`, `S2183`, `S2184`, `S2275`, `S2437`, `S3169`,
`S3172`, `S4143`, `S4581`, `S1944`, `S1696`, `S2995`, `S2996`, `S3005`, `S5856`, `S2701`.

Twenty-one are proven. **Unproven:** `S2184` (not probed), `S3172` (did not fire on `a = a - b`
for two `Action`s) and `S1944` (not probed).

Two had hits and were fixed rather than suppressed:

| Rule | Site | Fix |
| --- | --- | --- |
| `S1871` | `RoomSessionPool` victim selection | Two identical branches merged into one condition |
| `S1871` | `WikiLinkParser` line counting | Same |
| `S2701` | `LibraryEditorTests`, two assertions | `Assert.Equal(false, x)` became `Assert.False(Assert.IsType<bool>(x))` |

### Sixth batch

Eight more zero-hit rules, each proven to fire: `S1163` (throw in `finally`), `S1848` (object
created and discarded), `S2234` (arguments in a different order than the parameters), `S2386`
(public static mutable field), `S2737` (a `catch` that only rethrows), `S2955` (unconstrained
generic compared to `null`), `S3244` (anonymous delegate used to unsubscribe), `S3881` (wrong
`IDisposable` pattern).

`S1848` and `S1163` overlap `CA1806` and `CA2219`, which were already on, so they add no new
coverage.

**Tried and put back:** `S2114` and `S2328` did not fire on a probe built to trigger them. Either
the probe was wrong or the rule is narrower than its title; nobody checked which, so both stay
denied until someone does.

### Seventh batch

The 189 rules that were denied only because they were default-on were each read on their Sonar
title (listed with `agents/scripts/List-SonarRules.cs`). Eighty-eight guard correctness, security,
a house rule or a technology this repository uses, and were enabled. All had zero hits on the
whole solution. The grouping is mine, from the titles; the sites were not read because there were
none to read.

| Group | Rules |
| --- | --- |
| Hazards and API misuse | `S3236`, `S3343`, `S4583`, `S3346`, `S5034`, `S3869`, `S3363`, `S1215`, `S3971`, `S3981`, `S3998`, `S3875`, `S1048`, `S2291`, `S2139`, `S3877`, `S3397`, `S3444`, `S3011`, `S3885`, `S2953`, `S3060`, `S2934`, `S3449`, `S2757`, `S2761`, `S2198`, `S3440`, `S3603`, `S4201`, `S3457`, `S3458`, `S4275`, `S2372`, `S2376`, `S4456`, `S3889`, `S3443`, `S2178`, `S3464` |
| Types, enums, overrides and attributes | `S4070`, `S2345`, `S2346`, `S4015`, `S4019`, `S3600`, `S3262`, `S3466`, `S3427`, `S3450`, `S3451`, `S3447`, `S4260`, `S4545`, `S3251`, `S2368`, `S3887`, `S2696`, `S3010`, `S1104`, `S2290` |
| Logging (matches the `{PascalCase}` template rule) | `S6668`, `S6673`, `S6678`, `S6672`, `S6618`, `S6580` |
| Test hygiene | `S2925`, `S3433`, `S2187`, `S3415`, `S2970` |
| Blazor and web | `S6797`, `S6798`, `S6800`, `S6962`, `S6967`, `S6964`, `S6930`, `S6931`, `S6934`, `S6965`, `S6961`, `S6932` |
| Security | `S6377`, `S7039`, `S1313`, `S2857` |

**Proven (41)**, by a deliberate violation that failed the build with the rule's ID: `S1048`,
`S1104`, `S1215`, `S1313`, `S2139`, `S2178`, `S2290`, `S2291`, `S2346`, `S2368`, `S2376`,
`S2696`, `S2757`, `S2761`, `S2953`, `S3010`, `S3011`, `S3060`, `S3262`, `S3343`, `S3427`,
`S3440`, `S3443`, `S3447`, `S3458`, `S3466`, `S3600`, `S3603`, `S3869`, `S3875`, `S3885`,
`S3981`, `S4070`, `S4201`, `S4456`, `S4545`, `S5034`, `S6580`, `S6618`, `S6672`, `S6678`.

**Unproven (47).** They produced no output on the real code and were not (or could not be)
triggered by a probe: `S2187`, `S2198`, `S2345`, `S2372`, `S2857`, `S2925`, `S2934`, `S2970`,
`S3236`, `S3251`, `S3346`, `S3363`, `S3397`, `S3415`, `S3433`, `S3444`, `S3449`, `S3450`,
`S3451`, `S3457`, `S3464`, `S3877`, `S3887`, `S3889`, `S3971`, `S3998`, `S4015`, `S4019`,
`S4260`, `S4275`, `S4583`, `S6377`, `S6668`, `S6673`, `S6797`, `S6798`, `S6800`, `S6930`,
`S6931`, `S6932`, `S6934`, `S6961`, `S6962`, `S6964`, `S6965`, `S6967`, `S7039`.

Some of the unproven rules were probed and did not fire: `S2372` (a getter that throws
`InvalidOperationException`), `S2345`, `S3346` (`Debug.Assert(x++ > 0)`), `S3236`, `S3998`
(`lock (typeof(T))`), `S3887`, `S4275`, `S2198`, `S3450`, `S3451`. Either the probe was not
what the rule looks for or the rule is narrower than its title. Nobody checked which. The test
and web rules need a test or web project, and were not probed at all.

A rule that was enabled and never fires is harmless but gives a false sense of cover. If one of
these matters to you, write a probe for it first.

### Eighth batch

Ten denied rules were picked for value and enabled to see what the build said. Eight stay on; two
were put back after the build disproved them.

| Rule | What it caught | Outcome |
| --- | --- | --- |
| `S1643` | `PersonaFrontmatter.SplitFlowListItems` appended to a `string` in a loop, three places | Fixed with a `StringBuilder`. Proven by real hits. |
| `S2123` | Two test counters declared as `void Count() => n++`, read later | Not a bug; the increment was the local function's result. Changed to `Interlocked.Increment`, which the same file already uses elsewhere. Proven by real hits. |
| `S3241` | Two `SetupEditorModule` test helpers returned a tuple no caller used | Return type changed to `void`. Proven by real hits. |
| `S3237` | `DuplexStream.Position`'s setter never reads `value` | A deliberate `NotSupportedException` setter on a `Stream` override, so a false positive. Narrow `#pragma` with the reason. Proven by a real hit. |
| `S2971` | `TaskStoreMemoryWarningTests.Warnings` calls `ToList()` on a live list | Looks useless, but the logger appends from other threads and the call is the snapshot. Narrow `#pragma` with the reason; dropping it risked a flaky test. Proven by a real hit. |
| `S1244`, `S6588`, `S6607` | Floating-point equality, a hand-built Unix epoch, sort before filter | Zero hits. Each proven by a deliberate violation. |
| `S8969`, `S8970` | Redundant `!` | **Put back.** See [Denied on purpose](#denied-on-purpose): removing the `!` broke the build at two of three `S8969` sites and all four `S8970` sites. |

One thing the batch also changed: `AgentConnection.HandleMessageDeltaAsync` used `this.Agent!.Id,
this.Agent!.Name`. It now reads `this.Agent ?? throw new InvalidOperationException(...)` once, so
the invariant "the handshake set the Agent first" is stated instead of asserted with `!`, and a
broken one fails with a clear exception.

## Denied on purpose

Twenty-nine rules fire on this codebase and stay denied. (Thirty-four did at the baseline; five of
them were fixed and enabled, see [Eighth batch](#eighth-batch).) `src` and `tests` are hit counts
from the baseline above. **Reviewed** means someone read the sites and decided; **Noise** means
the decision rests on the diagnostic's message and its hit count, with the sites not read.

| Rule | Message | src | tests | Status and decision |
| --- | --- | --- | --- | --- |
| `S6667` | Logging in a `catch` should pass the exception | 4 | 0 | **Reviewed, keep off.** The sites log an expected cancellation (`RoomSession.cs`) or a bad colour in user data (`AvatarStore.cs`); a stack trace would only be noise. Fixing it would make the logs worse. |
| `S3264` | Unused event | 2 | 0 | **Reviewed, keep off.** False positive: `PersonaStore` raises `PersonaRenamed` and `PersonaRemoved` through `GetInvocationList()`, which the rule does not see. |
| `S1994` | Loop's stop incrementer is not in the stop condition | 1 | 0 | **Reviewed, keep off.** `TaskIdAllocator` uses `for (suffix = 2; ; suffix++)` with a `return` inside; it is deliberate. |
| `S6966` | Await the async variant (`CancelAsync`, `DisposeAsync`) | 16 | 923 | Noise. It is a preference for the async overload, not a missing `await`. Almost all hits are tests. |
| `S3267` | Loop could use `Where` | 69 | 2 | Noise. A style preference. |
| `S108` | Empty block | 41 | 4 | Noise. |
| `S125` | Commented-out code | 7 | 4 | Noise. Plausibly worth a look, since commented-out code rots. Not reviewed. |
| `S3358` | Nested ternary | 8 | 0 | Noise. Style. |
| `S8969` | Redundant null-forgiving `!` | 3 | 32 | **Reviewed, keep off.** Tried on the three source sites: at `PersonaRunner.cs:560` and `RoomSessionPool.cs:170` removing the `!` fails the build with `CS8602`, so the rule is wrong there. Only the `AgentConnection.cs` site was right. The 32 test sites were not tried; a rule that is wrong on two of three cannot be trusted without checking each fix against the compiler. |
| `S8970` | `!` where nullable warnings are disabled | 4 | 0 | **Reviewed, keep off.** False positive on all four sites. They are `= default!` in `.razor` files, where nullable is enabled: removing the `!` fails the build with `CS8625` or `CS8601`. |
| `S2094` | Empty record or class | 4 | 0 | Noise. Sites include `AssemblyMarker`, `AgentPrompt`, `ElicitationResult` and `LibraryImageResult`; not read. |
| `S2365` | Property copies a collection | 4 | 1 | Noise. Sites are `Sinks`, `Aliases`, `WatchedTaskIds`, `OnlineAgentIds`; not read beyond the names. |
| `S3878` | Needless array creation for `params` | 3 | 3 | Noise. |
| `S4144` | Method identical to another | 1 | 8 | Noise. |
| `S1481` | Unused local | 1 | 13 | Noise. `IDE0059` is the rule for dead stores. |
| `S1144` | Unused private member | 1 | 3 | Noise. Not read. |
| `S1066` | Mergeable `if` | 1 | 2 | Noise. |
| `S1117` | Local hides a field | 3 | 1 | Noise. |
| `S1118` | Utility class needs a `protected` constructor or `static` | 1 | 0 | Noise. |
| `S1135` | `TODO` comment | 2 | 2 | Noise. |
| `S1450` | Field should be a local | 1 | 0 | Noise. |
| `S2292` | Use an auto-property | 0 | 1 | Noise. |
| `S2325` | Method could be `static` | 3 | 0 | Noise. `CA1822` already covers this for members. |
| `S2479` | Control character in a literal | 1 | 0 | Noise. `ModelCatalogProbe` uses a raw `\u001F`. |
| `S2743` | Static field in a generic type | 0 | 1 | Noise. Test code (`ListLogger`). |
| `S3218` | Member shadows an outer member | 0 | 4 | Noise. |
| `S3376` | Class name should end in `Exception` | 0 | 1 | Noise. Test code. |
| `S3398` | Move method inside its class | 0 | 1 | Noise. |
| `S6562` | Provide `DateTimeKind` | 0 | 1 | Noise. |

The five **Reviewed** rows are the only ones where the sites were read. For every other row,
treat "Noise" as "not worth the cost at the time", not as "proven harmless".

## Denied after review, zero hits

The 101 rules of the original 189 that stayed denied after the seventh batch (three of them,
`S1244`, `S6588` and `S6607`, were enabled in the eighth, leaving 98), in five groups, each its own `<NoWarn>`
element in `Directory.Build.props` with the reason in a comment above it. They have zero hits, so
the reason is never "it would break the build"; it is what the rule is. Judged on the Sonar
title only.

| Group | Count | Rules | Why |
| --- | --- | --- | --- |
| Technology this repository does not use | 22 | `S6420`, `S6419`, `S6424`, `S6422` (Azure Functions), `S3597`, `S3598` (WCF), `S4210` (WinForms), `S4428`, `S4159`, `S4277` (MEF), `S4200`, `S4211`, `S3925`, `S3927`, `S3926` (legacy serialization), `S3431` (NUnit), `S6670`, `S6675` (the `Trace` API), `S8380`, `S8381`, `S8367`, `S8368` (C# 14 keyword-escape naming) | Nothing here to guard. The C# 14 ones are also reported by the compiler. |
| Another rule already covers it | 14 | `S112` (`CA2201`), `S2629` (`CA2254`), `S3260` (`CA1852`), `S1172` (`IDE0060`), `S3445` (`CA2200`), `S101` (`IDE1006`), `S1155` (`CA1860`), `S1905` (`IDE0004`), `S2223` (`CA2211`), `S2681` (`IDE0011`), `S1699`, `S2306`, `S4220`, `S3903` | Enabling it would print each finding twice. The mapping is by title, not tested. |
| Kept for a stated reason | 4 | `S4036`, `S1607`, `S6610`, `S1075` | See below. |
| Tried, did not fire | 2 | `S2114`, `S2328` | See [Sixth batch](#sixth-batch). |
| Style, naming, design opinion or micro-performance | 56 | `S1110`, `S1116`, `S1121`, `S1123`, `S1133`, `S1134`, `S1168`, `S1185`, `S1186`, `S1199`, `S1210`, `S1264`, `S1694`, `S1939`, `S1940`, `S2166`, `S2219`, `S2344`, `S3217`, `S3246`, `S3247`, `S3249`, `S3261`, `S3263`, `S3400`, `S3453`, `S3456`, `S3459`, `S3604`, `S3610`, `S3897`, `S3904`, `S3963`, `S3972`, `S3973`, `S3993`, `S4035`, `S4050`, `S4052`, `S4061`, `S4136`, `S4524`, `S4635`, `S4663`, `S6561`, `S6575`, `S6608`, `S6609`, `S6613`, `S6617`, `S6640`, `S6960`, `S6968`, `S818`, `S907`, `S927` | Taste. None guards correctness or security. |

The four **kept for a stated reason** rest on reasoning, not on a test:

- `S4036` (OS commands should not rely on `PATH` resolution): the app spawns agent adapters by
  command name. Believed deliberate; not checked against the spawn code.
- `S1607` (tests should not be ignored): conditional `Skip` is used for the live-agent tests, and
  the CI quarantine may use it too. Not checked whether Sonar flags a conditional skip.
- `S6610` (use the `char` overload of `StartsWith`): the house rule is
  `StringComparison.Ordinal`. Whether the two actually conflict was not tested.
- `S1075` (URIs should not be hardcoded): fixtures hardcode URIs legitimately. Zero hits today,
  which is mildly surprising; not investigated.

Nine `S9999-*` entries in the last group are Sonar's own telemetry diagnostics, not code rules.

Nothing in these groups is urgent. The "style" group is the only one where a different taste
would change the answer, and the `S6561` and `S6575` date rules are the ones closest to
correctness. To clear a rule:

1. Read its title (`dotnet run agents/scripts/List-SonarRules.cs`) and its page on
   rules.sonarsource.com.
2. Decide whether it guards correctness, security or a house rule, or is style.
3. If it is worth having, move it to `.editorconfig` as a warning and prove it fires.
4. If it is not, leave it and put the reason in the group's comment and in the table above.

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
| `CA1031` off in `Huddle.App` and `Huddle.Contracts` | `.csproj` | **Open.** With the suppression lifted about 40 broad `catch` sites show up, far more than the four the Haiku audit counted. See below. |

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
`CS0067` in tests. The agent's other claims were spot-checked, not re-derived; its `CA1031` count
was wrong, as above.

## Changing a rule

1. Read what the rule is. `dotnet run agents/scripts/List-SonarRules.cs` prints every rule's ID,
   severity and title. Judge it on that, not on its number.
2. Measure: build with the rule enabled and the denylist lifted, and read the hits. Zero hits is
   the cheap case; some hits means reading the sites.
3. Prove it fires. Write a scratch file with a deliberate violation, build, expect the rule's
   ID, delete the file. "No output" does not mean "works".
4. Fix real findings in the same change. Do not suppress to get a build through; the suppression
   rules are in `CSharpPrinciples.md`.
5. Move the rule between the groups in `Directory.Build.props` and add the `.editorconfig` line.
6. Update this page in the same commit.
