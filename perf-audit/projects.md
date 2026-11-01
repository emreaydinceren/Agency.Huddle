# Projects, target frameworks and language versions

Every project inherits `TargetFramework` `net10.0` and `LangVersion` `latest` (C# 14) from the root
`Directory.Build.props`; only `Huddle.MockAdapter` restates `net10.0`. So every API in the recipe, including
the .NET 9 ones (`MemoryExtensions.Split` enumeration, `GetAlternateLookup`, `SearchValues<string>`), is
available everywhere.

| Project | Owner | TFM | LangVersion |
| --- | --- | --- | --- |
| `src/Huddle.App` (Web SDK) | chat | net10.0 | latest |
| `src/Huddle.Contracts` | chat | net10.0 | latest |
| `src/Huddle.MockAdapter` | chat | net10.0 | latest |
| `src/Huddle.Packs` | chat | net10.0 | latest |
| `src/Huddle.SkillPacks` | chat | net10.0 | latest |
| `src/Huddle.Seeder` | chat | net10.0 | latest |
| `src/Huddle.Acp` | ACP (not edited) | net10.0 | latest |
| `src/Huddle.Console` | ACP (not edited) | net10.0 | latest |
| `tests/*` (14 in the solution, 4 analyzer probes outside it) | per subtree | net10.0 | latest |
| `tools/AllocReport`, `tools/LoadDriver`, `tools/Benchmarks` (new, not in `Huddle.slnx`) | audit | net10.0 | latest |

## Baseline before any change (2026-10-31, `main` at `432bccc6`)

- `dotnet build Huddle.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test Huddle.slnx -c Release --no-build --`: 14,130 total, 14,095 passed, 35 skipped, 0 failed.
