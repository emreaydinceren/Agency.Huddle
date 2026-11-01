# Findings, ranked

Source: `alloc-before.md`, a 90 s `gc-verbose` trace under the real-agent workload (see [README.md](README.md)).
Shares are of the 264 MB of sampled `System.String` + `System.Char[]`, from 1,561 MB of sampled allocations.

**Where the string bytes go.** About 71% (188 MB) has `GatewaySessionRequirement.InvokeAsync`, the request
middleware, as its nearest app frame. Under it is Blazor's server render of MudBlazor components:
`MudThemeProvider.GenerateTheme`, the `MudIcon`/`MudIconButton` and `MudPopover` class strings, `MudMenu` and
`MudTooltip`. That is third-party code, and every page render pays it. App-owned sites total under 15%.

| ID | Location | Pattern | Fix (section) | % of string allocs | Effort | Risk | TFM needed | Outcome |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R1 | `Data/SqliteTeamDirectory.cs:631` `ReadRoom` | Row strings, plus a `created` text column parsed per row | none: the strings are the data | 5.4% | — | — | — | Not a string fix. The cost is call frequency: `TeamsNav` and `TabSync` each load every Room on every page render. **Needs decision** |
| S1 | `SkillSources/SkillRegistry.cs:94` `DisplayState(SkillEntry)` | Computed `$"skill:{Name}"` built twice per Skill, on every page render (`SkillArrivalWatcher.Usable`) | read the key once per Skill | 2.8% | S | Low | any | **Fixed**, `5c5da737` |
| R2 | `Data/SqliteTeamDirectory.cs:616` `ReadUser` | Row strings; `kind` read as text then switched | none worth it: `kind` is a 5-char string a row | 1.8% | — | — | — | Not a string fix. `RoomNavRow.OnParametersSetAsync` queries members once per row (N+1). **Needs decision** |
| P1 | `Tasks/HumanGate/ProofContextBuilder.cs:55` `CoverageFor` | `PersonaFrontmatter.TryReadIdentity` (Split per line) re-parses Persona text on every Task render | cache the role in `PersonaIndex` | 1.3% | M | Medium | any | **Needs decision**: changes the public `PersonaEntry` record |
| N1 | `Components/Shared/RoomNavRow.razor:39,108` | `$"/rooms/{Id}"` and the class string built in markup | 3.17 compute in `OnParametersSet` | 1.2% | S | Low | any | **Rejected**: on a prerender each row renders once, so caching saves nothing; the strings are the output |
| M1 | `Components/Shared/Dialogs/MenuDialogs.cs:52` `ActivatorLabel` | `$"Actions for {target}"` | none | 1.0% | — | — | — | Not a fix: the aria-label string is the output |
| T1 | `Tabs/TabTrail.cs:14` `Build` | `Split('/')` + `UnescapeDataString` per top-bar render | 3.5 span split | 0.9% | S | Low | net9 | Below 1%; left alone |
| L1 | `Library/TeamFolderProvisioner` `TryReadTeamFile` | `char[]` from reading `_team.md` on each `TeamPage.Features()` render | — | 0.9% | — | — | — | Below 1%; file I/O per render, noted |
| K1 | `Services/MarkdownRenderer.Render` | `char[]` | — | 0.6% | — | — | — | Below 1%; left alone |

The recipe's stop rule is at 1%. Everything below S1 either has no string-level fix, because the string is the
data or the output, or its real fix is a design change listed under "Needs my decision" in
[report.md](report.md).
