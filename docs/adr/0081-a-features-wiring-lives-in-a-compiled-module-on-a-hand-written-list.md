---
status: accepted
date: 2026-11-06
---

# A feature's wiring lives in a compiled module on a hand-written list

A feature's wiring was spread across central files: its registrations in `ServiceCollectionExtensions`, its tools and Work Dir setup in `DotAcpAgentHostFactory`. Of 260 PRs that touched the app between 2026-09-06 and 2026-11-06, 117 (45%) changed at least one of six central files and 54 (21%) changed two or more.

Claude Code's mods prompted the question of whether a mod system would help. This one is about organising Huddle's own code. Nobody outside the repo writes a module.

## Decision

**A feature may declare its wiring in one class that implements `IFeatureModule`, listed by hand in `ModuleCatalog.All`.** A module does three things, each at a point the factory or the composition root already has:

| Member | When it runs | What it may do |
| --- | --- | --- |
| `AddServices` | Once, while the app is composed | Register services, panels and hooks |
| `OfferTools` | For each session, after Skill gating and before `get_help` is built | Return the tools the feature offers |
| `PrepareWorkDir` | For each session, before the Adapter reads the folder's settings | Put files in the Work Dir |

The list's order is the order of what modules contribute, and a module's tools reach the system prompt in that order, so `ToolOfferOrderTests` pins it.

File Changes and Document History are the modules so far. Moving another feature is a separate decision (see Consequences).

### What stays central

- **Guards and the merge gate.** A guard that throws must not approve, and the set of guards should be readable in one place (`PermissionGuardList`).
- **The order of Turn hooks and enrichers.** `ProductionOrder` stays in each pipeline. A module registers a hook, and the pipeline decides when it runs. A hook missing from the order throws.
- **The Reply Gate, the Budget and the Turn lifecycle.** These are the product's rules, not a feature's wiring.
- **`get_help`.** It is built last, from the tools the session was offered.

Modules do not replace the separate contracts (`ITurnHook`, `ITurnEnricher`, `IToolCallGuard`, `IAppTool`, `SidebarPanel`). They differ in their failure rules, so a module uses each one as it is.

## Measured

| | Document History (#438) | File Changes (#441) | Together |
| --- | --- | --- | --- |
| `ServiceCollectionExtensions.cs` | +8 | -15 | 951 to 944 |
| `DotAcpAgentHostFactory.cs` | -7 | -13 | 771 to 751 |
| Central files | +1 | -28 | -27 |
| New product lines elsewhere | about +131 | +54 | about +185 |

The first module paid for the contract and the loops, and the second shrank the central files with no change to the contract. The product as a whole still grew, partly in doc comments. The wiring is easier to find, but there is no evidence yet that fewer PRs touch the central files. That needs more features moved, and time.

Both moves left the registrations, tool order and prompts unchanged: `CompositionSnapshotTests`, `ToolOfferOrderTests` and the prompt goldens hold them. DOCHISTORY-07 and 08 passed on a real Adapter after the move, including the Stop hook sending an Agent back to log (#440).

## Consequences

- **Don't move more features by default.** Re-measure the share of PRs that touch the central files first, a few weeks after this decision, against the figures above. Jobs would be next if that shows a gain.
- **A module does not hold all of a feature.** `PersonaSupervisor` still decides per Persona whether File Changes applies, and the factory still reads its options for the memory block. Options stay in `TeamOptions` and prompts in `PromptCatalog`.
- **Nothing enforces the boundary.** All of this is one assembly, so code outside a module can still reach into a feature. A `Check-*` script or an analyzer would be its own decision.
- **`CompositionSnapshotTests` is temporary.** Every PR that registers a service has to reseed `composition.txt`. Delete both when the migration ends or is abandoned.
- **A disabled feature keeps its services.** Razor components inject them, so `AddServices` registers them whatever the options say, and the checks stay in `OfferTools` and on the panels.

## Rejected

| Alternative | Why not |
| --- | --- |
| Runtime plugins, assembly scanning, manifests or hot reload | Nobody would load them, and each adds what a compiled list does not need. The Turn Hooks decision (D-15 in the AGENTS.md Changes spec) rejected them for the same reason |
| One generic `IHook` or middleware chain | The existing lists fail in different ways, and a guard must never fail by approving |
| Only per-feature `AddX()` methods | Tidies registration, but tools and Work Dir setup stay in the factory, which is the part that needs a per-session contract. Still the fallback if the re-measure shows no gain |
| Moving `RoomSession` wiring into modules | The in-process hooks work (#436) already moved its prompt additions and left it smaller |
