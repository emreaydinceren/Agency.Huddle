---
name: dotnet11-breaking-changes
description: >
  .NET 11 / C# 15 breaking and behavioural changes and the one new C# 15 feature that matters here (collection
  expression arguments, `[with(StringComparer.Ordinal), ..]`), filtered for Huddle. USE FOR: a build error or
  new warning that started with the 11.0 SDK, a test whose behaviour changed under net11.0, reviewing code that
  touches BackgroundService/hosted services, span collection expressions, `nameof` inside attributes,
  NamedPipeClientStream constructors, compression/zip/tar, or Blazor Virtualize, and writing a string-keyed
  HashSet/Dictionary with a collection expression. DO NOT USE FOR: the TFM upgrade itself (done: every project
  is net11.0 on SDK 11.0.100) or EF Core, Cosmos, SqlClient, NativeAOT and OpenAPI changes (Huddle uses none).
license: MIT
---

# .NET 11 changes that can bite Huddle

Adapted from `dotnet/skills` (`plugins/dotnet-upgrade/skills/migrate-dotnet10-to-dotnet11`, MIT, see
`../LICENSE-dotnet-skills`). The `references/` files are upstream's, unedited.

> **Coverage gap.** Upstream wrote these references during the previews and says they cover breaking changes
> "through Preview 3". Anything announced later is missing. When a symptom matches nothing here, check
> [the .NET 11 compatibility page](https://learn.microsoft.com/dotnet/core/compatibility/11) (the
> `microsoft-docs` skill can fetch it) before concluding it is a Huddle bug.

## Where to look

| Symptom or area | Reference |
| --- | --- |
| A `CS` error after the SDK moved to 11 | `references/csharp-compiler-dotnet10to11.md` |
| Hosted services, compression, `MemoryStream`, `Environment.TickCount`, pipes, `SYSLIB` obsoletions | `references/core-libraries-dotnet10to11.md` |
| `dotnet publish --self-contained`, PackAsTool, SDK warnings | `references/sdk-msbuild-dotnet10to11.md` |
| Blazor `Virtualize`, OpenAPI | `references/aspnetcore-dotnet10to11.md` |

Load only the row you need.

## Checked against this repo (2026-11-13)

| Change | Huddle exposure |
| --- | --- |
| **Unhandled `BackgroundService` exceptions stop the host** | High in principle: `src/` has about 40 files deriving from or naming `BackgroundService`, and no `BackgroundServiceExceptionBehavior` setting. A throw that escapes `ExecuteAsync` takes the whole app down. House rule `HUD0002` keeps lifetimes in a `StopSignal`. A supervisor loop that must survive catches the specific failure, or catches broadly with the log-and-reason comment `CSharpPrinciples.md` requires. Never an empty `catch`. |
| **`nameof(this.P)` inside an attribute is an error** | `IDE0009` makes `this.` habitual, but the error applies only inside attribute arguments. `nameof(this.Changed)` in a method body (as in `PersonaHealth`, `PersonaRunner`, `PersonaStore`) is fine. In an attribute write `nameof(P)`. |
| **Span collection expressions are `declaration-block` scoped** | Shows up as a new ref-safety error when `Span<T> x = [..]` built in a loop or block is assigned to an outer `scoped` span. Fix: an array, or build it in the outer scope. |
| **`with(...)` as a collection element means constructor arguments** | Only bites a method literally named `with`; escape it as `@with(...)`. |
| **`(X.Y) when` in a switch arm is now a pattern plus guard** | Rare; re-read the arm if a switch expression changes meaning. |
| **`NamedPipeClientStream(…, isConnected)` obsoleted (SYSLIB0063)** | Huddle connects through `PipeClientConnector` and builds green, so it does not use that overload. Do not add it. |
| **Deflate/GZip write headers for empty payloads; Zip validates CRC32; Tar validates checksums and writes HardLink entries** | No `System.IO.Compression` or `System.Formats.Tar` use in `src/` today. Re-check if an export or backup feature adds it. |
| **`Virtualize` default `OverscanCount` 3 → 15** | No `<Virtualize>` in `src/` today. If you add one over a long feed, set `OverscanCount` on purpose. |
| **Minimum hardware: x86-64-v2, Arm64 LSE** | Matters for the installer's supported-machine statement, not the code. |

## New in C# 15: collection expression arguments

`with(...)` as the **first** element passes constructor or factory arguments. It lets the house rule "string keys
take `StringComparer.Ordinal` explicitly" and the rule "prefer collection expressions" hold at once:

```csharp
HashSet<string> seen = [with(StringComparer.Ordinal), .. names];
Dictionary<string, Persona> byName = [with(StringComparer.OrdinalIgnoreCase)];
List<Turn> turns = [with(capacity: source.Count), .. source];
```

Prefer it over `new HashSet<string>(names, StringComparer.Ordinal)` in new code. Do not churn existing
constructions for it. Confirm the analyzers accept it on the first use (`IDE0028`/`IDE0300`-family
suggestions may not yet know the form); the build is the judge.

## Verify

Build and test the solution, trailing `--` included (`CLAUDE.md`). A behavioural change is pinned by a test that
fails on the old behaviour (`agents/Testing.md`).
