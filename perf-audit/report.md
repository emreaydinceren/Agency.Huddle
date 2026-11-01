# String allocation audit: report

Branch `perf/string-allocations`, 2026-10-31. Every measurement is a Release build of `Huddle.App` running real,
paid Claude agents. LoadDriver asked Grace, every 15 s, to create, list and update Tasks, read and search
Library notes, and edit a note. It also loaded `/tasks`, `/tasks/item/PLAT-0012`, `/library` and
`/teams/Platform` about four times a second. Browser circuits were open on the Room, the Tasks board and the
Library throughout. How to repeat it: [README.md](README.md).

## 1. Summary

| Measure (90 s window) | Before | After |
| --- | --- | --- |
| Allocation rate | 17.7 MB/s | 18.2 MB/s |
| Gen0 GCs | 0.26 /s | 0.25 /s |
| Time paused for GC | 3.5 ms/s | 3.9 ms/s |
| `System.String` + `System.Char[]` share of sampled allocations | 16.9% | 16.6% |
| `SkillArrivalWatcher.Usable` string bytes (the fixed site) | 7.5 MB | 4.2 MB (−44%) |

The fixed site dropped as the benchmark predicted. The end-to-end numbers did not move outside run-to-run
noise: replies from a real model differ in length between runs, and the two runs served 339 and 344 page
loads in the counters window. The headline finding is that **this app has little string waste of its own on
its hot paths**:

- GC costs about 0.4% of wall time (3.5 to 3.9 ms per second).
- Strings are 17% of what is allocated, and about 71% of those come from Blazor rendering MudBlazor
  components, which is third-party code.
- The larger app-owned costs are repeated work, not string handling: loading every Room twice per page,
  N+1 member queries, and re-parsing Persona files on each Task render. Each is a design decision, listed
  in section 5.

The static pass agrees. With the recipe's analyzer rules raised to warning, the solution has zero hits, and
the 435 grep hits in `candidates.md` sit on cold paths.

## 2. Fixes applied

| ID | Location | Fix | Allocated | Mean | Commit |
| --- | --- | --- | --- | --- | --- |
| S1 | `src/Huddle.App/SkillSources/SkillRegistry.cs` `DisplayState(SkillEntry)` | Read the computed `SkillEntry.StateKey` once instead of twice per Skill | 10.94 KB → 5.47 KB (−50%) per 100 Skills | 3.727 µs → 2.559 µs (−31%) | `5c5da737` |

S1 adds the characterization test `SkillDisplayState_NewThenAvailableThenInUse`. A deliberate mutation made it
fail: looking up the New overlay by the bare name gave `[Available, Available, InUse]`. The solution builds with
0 warnings, and all 14,131 tests pass (35 skipped).

The other commits on the branch:

- `83fe235f` adds the measurement tools, `tools/AllocReport`, `tools/LoadDriver` and `tools/Benchmarks`, plus
  BenchmarkDotNet and TraceEvent in `Directory.Packages.props`. None of the tools is in `Huddle.slnx`.
- `4734a5c7` raises CA1830, CA1834, CA1845, CA1846, CA1858 and CA1870 to `warning`. They have zero hits, each
  failed a deliberate probe, and they are recorded in `docs/engineering/analyzer-decisions.md`. The other
  string rules in the recipe (CA1847, CA1862, CA1865 to CA1867, CA2249) were already build errors. CA1848 and
  CA2254 are already met: `[LoggerMessage]` in the ACP subtree, and constant templates elsewhere.

## 3. Rejected fixes

| ID | Why |
| --- | --- |
| N1 `RoomNavRow` href and class strings | Caching in `OnParametersSet` saves nothing on a prerender, where each row renders once. The strings are the rendered output. |
| Caching `StateKey` on `SkillEntry` (an alternative to S1) | A stored key goes stale under `with { Name = ... }`, which tests use. A lazily cached key breaks the record's generated `Equals`. |
| Zero-allocation S1 (a span lookup through `GetAlternateLookup`) | It needs `CheckResult.StateOf` and `RegistrySnapshot.NewKeys` to change from public signatures to concrete types, which the recipe says to ask about first, for a further 5.47 KB per 100 Skills. |

## 4. Not hot: left alone

- **All 435 static hits in [candidates.md](candidates.md)** except S1: 144 `Split`, 96 `ToLower`/`ToUpper`,
  62 `string.Join(... Select)`, 45 `Trim()` chains, 26 `string.Format`, 26 `Encoding.GetBytes`, 19
  `Replace().Replace()` chains, 10 `+=`, 5 `Substring`, 1 interpolated log call, 1 static `Regex` call. None
  of their methods appears among the trace's app frames above 0.5%.
- **T1** `TabTrail.Build` (0.9%), **L1** `TeamFolderProvisioner.TryReadTeamFile` (0.9%) and **K1**
  `MarkdownRenderer.Render` (0.6%): below the 1% stop rule.
- **M1** `MenuDialogs.ActivatorLabel` (1.0%): the string is the aria-label.
- **MudBlazor's own string building** (about 71%): third-party.

## 5. Needs my decision

1. **R1: two full Room loads per page render.** `TeamsNav.LoadRoomsAsync` and `TabSync.ReconcileAsync` each
   call `SqliteTeamDirectory.GetRoomsAsync` on every page render. Together that is 5.4% of string allocations,
   plus a SQLite round trip each. A per-circuit or short-lived Rooms cache would remove one or both, but it
   changes how fresh the sidebar is. That is a behaviour decision.
2. **R2: N+1 member queries.** `RoomNavRow.OnParametersSetAsync` runs `GetRoomMembersAsync` once per row,
   which is 1.8% and one query per Room per render. One batched query in the parent would fix it, but it
   changes the component's contract.
3. **P1: Persona re-parse on each Task render.** `ProofContextBuilder.CoverageFor` calls
   `PersonaFrontmatter.TryReadIdentity` for each name, on each `TaskReadView.Refresh` (1.3%). Keeping the parsed
   role in `PersonaIndex`/`PersonaEntry` fixes it, but `PersonaEntry` is a public positional record used across
   the app and its tests.
4. **ACP subtree.** I did not edit it, per your scope choice. No `Huddle.Acp` or `Huddle.Console` frame
   appears in the trace's top string sites under this workload. It sees one prompt every 15 s, so that is
   not evidence that it is clean under heavy streaming.
5. **Tooling is not in the solution.** `tools/*` is kept out of `Huddle.slnx`, so CI does not build it and it
   can rot. Adding it costs a BenchmarkDotNet restore on every CI run.

## Costs

The workload spent real money: two measured runs (before and after), 11 Grace Turns each on her default model,
each run capped at 600,000 tokens by `Acp:TokenBudget`. One more start on the real adapter built the run
template and sent no prompts. The first two baseline attempts ran on the free `mock-acp` and were thrown away,
because the mock echoes its prompt: two Agents in one Room mentioned each other forever.
