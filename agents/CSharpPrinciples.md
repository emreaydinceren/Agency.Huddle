# C# Principles

## Global Build Config & Centralized Package Management

Two files at the **repository root** — not under `src/` — split this between them:

| File | Owns |
| --- | --- |
| `Directory.Packages.props` | **Package versions.** One `<PackageVersion>` per dependency. Central package management is on, so a `Version` attribute on a `PackageReference` is a build error. The one exception in this file is `<GlobalPackageReference>` (the Sonar analyzer), which carries its own `Version` by design. |
| `Directory.Build.props` | **Compiler settings** — `TreatWarningsAsErrors=true`, `Nullable=enable`, `AnalysisLevel=latest-recommended`, and the `NoWarn` denylists. It switches central package management on but holds no versions itself. |

All code must be warning-free and null-safe.

When adding or updating a dependency:

1. Add or update the `<PackageVersion>` entry in `Directory.Packages.props`
2. Reference it in the `.csproj` with `<PackageReference Include="..." />` and **no** `Version` attribute
3. This keeps one version per package across both solutions and makes an upgrade a single-point change

## C# Conventions

- Always use XML doc comments (`///`) for all class and method comments — never plain `//` comments for documentation. Use `<summary>`, `<param>`, `<returns>`, and `<see cref="..."/>` tags as appropriate. This applies to test projects as well as production code.
- Do NOT use `yield return` inside try-catch blocks — this does not compile in C#
- Do NOT instantiate abstract classes directly; use interfaces or concrete implementations
- Sealed classes cannot be mocked — use functional/integration tests or extract an interface
- Always verify builds pass (`dotnet build`) after code changes before declaring success

## Principles for C# Coding

- **`record` for data, `sealed class` for behavior.** Records give value equality and non-destructive `with` copies. Sealing blocks accidental inheritance — inherit only when you've explicitly designed for it.
- **Immutable by default.** `init`-only properties, `readonly` fields, return `IReadOnlyList<T>` / `IReadOnlyDictionary<TK,TV>` from public APIs. Never hand out references to internal mutable state.
- **Push side effects to the edges.** Pure functions in the middle, I/O and mutation at the boundary — the functional core / imperative shell pattern. Same idea you'd lean on in Haskell or Elm, just not enforced by the compiler, so you have to enforce it bydiscipline.
- **Constructor injection, always.** No service locators, no static singletons for behavior, no `new SomeService()` inside domain code. If a class has no constructor parameters and isn't a pure record, be suspicious.
- **Inject ambient dependencies too.** `TimeProvider`, `IRandom`, `IFileSystem`, message bus, HTTP client. `DateTime.Now` and `new Random()` produce flaky tests and time-bombed code.
- **Make illegal states unrepresentable.** `enum OrderStatus`, not `string status`. Wrap meaningful primitives — `record CustomerId(Guid Value)` beats raw `Guid` passed through twelve methods. The compiler catches the misuse you'd otherwise debug in production.
- **Validate at the boundary, trust internally.** Parse incoming data into domain types once, at the edge. Internal code assumes invariants hold. Don't re-check `if (x == null)` ten layers deep.
- **`Result<T>` (or similar) for expected failure; exceptions for the actually exceptional.** Business validation isn't exceptional. Control flow hidden inside exceptions is invisible to readers and to the compiler. `throw` is for "this should never happen."
- **Async hygiene.** `async Task`, not `async void` (except event handlers). Never `.Result` or `.Wait()` — sync-over-async deadlocks are a rite of passage you should skip. `CancellationToken` on anything that can wait. `ConfigureAwait(false)` in library code (CA2007 is set to `none` in `.editorconfig` — no SynchronizationContext in our ASP.NET Core/generic-host targets — but it's still a reasonable defensive habit; revisit if hosting scenarios broaden pre-1.0).
- **Don't catch `Exception`.** Catch what you actually handle; otherwise let it propagate. Swallowed exceptions are how production bugs go silent for months.
- **Materialize LINQ at boundaries.** `IEnumerable<T>` is a recipe, not a result. Multiple enumeration and lazy side effects will bite. `.ToList()` / `.ToArray()` when crossing a layer.
- **Tests are design feedback.** If something is hard to test, the design is wrong — fix the design, don't add test-only hooks. Hard-to-mock dependency wants to be an interface. Hard-to-reach branch is usually dead code or a missing abstraction.