# C# Principles

House style for every C# file in this solution (`.cs`, `.csx`, `.razor`, `.cshtml`),
written for coding agents. The build is the linter here: `TreatWarningsAsErrors` turns
every analyzer complaint into a failed build, so the rules below are not advice — they
are the cheapest way to get a green `dotnet build` on the first attempt.

## Global Build Config & Centralized Package Management

Two files at the **repository root** — not under `src/` — split this between them:

| File | Owns |
| --- | --- |
| `Directory.Packages.props` | **Package versions.** One `<PackageVersion>` per dependency. Central package management is on, so a `Version` attribute on a `PackageReference` is a build error. The one exception in this file is `<GlobalPackageReference>` (the Sonar analyzer), which carries its own `Version` by design. |
| `Directory.Build.props` | **Compiler settings** — `TreatWarningsAsErrors=true`, `Nullable=enable`, `ImplicitUsings=enable`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`, and the Sonar `NoWarn` denylist. It switches central package management on but holds no versions itself. |

All code must be warning-free and null-safe.

When adding or updating a dependency:

1. Add or update the `<PackageVersion>` entry in `Directory.Packages.props`
2. Reference it in the `.csproj` with `<PackageReference Include="..." />` and **no** `Version` attribute
3. This keeps one version per package across the solution and makes an upgrade a single-point change

## What the build enforces

Five analyzer layers run on every `dotnet build`, and anything at `warning` or above
fails it. Knowing which layer a diagnostic comes from tells you where its rule lives.

| Prefix | Source | Where the severity is set |
| --- | --- | --- |
| `CS` | The compiler, including every nullable-flow warning (`CS86xx`) | Always on |
| `IDE` | Code-style rules from `.editorconfig`, run in the build by `EnforceCodeStyleInBuild` | `.editorconfig` — only rules at `warning`/`error` fail the build; `suggestion` ones do not |
| `CA` | .NET code-quality analyzers at `AnalysisLevel=latest-recommended` | The SDK's recommended set, plus overrides in `.editorconfig` and per-project `NoWarn` |
| `S` | SonarAnalyzer.CSharp, a curated subset | Opted in by ID in `.editorconfig`; everything else denylisted in `Directory.Build.props` |
| `xUnit` | xunit.v3 analyzers, test projects only | Package defaults |

Per-project exceptions you can rely on: `Huddle.App` and `Huddle.Contracts` suppress
`CA1848` (LoggerMessage) and `CA1031`; the test projects suppress `CA1707` (underscores
in identifiers); `Huddle.Acp.Tests` also suppresses `CA1861`. Everywhere else, those
rules are live.

## Rules that fail the build most often

Each row is a real diagnostic from the layers above, with the idiom that satisfies it.
Write the idiom the first time and the build stays green.

### Layout and hygiene

| Rule | Write this |
| --- | --- |
| `IDE0011` (error) | Braces on every `if`/`else`/`for`/`foreach`/`while`, even single statements. |
| `IDE0005` (error) | No unused `using` directives. `ImplicitUsings` is on, so `System`, `System.Linq`, `System.Threading.Tasks`, `System.Collections.Generic`, `System.IO`, `System.Net.Http` and `System.Threading` are already imported — adding them again is an unused using. |
| `IDE0060` | No unused parameters. Remove it, or name it `_` when a delegate shape forces it. |
| `IDE0059` | No assignments that are never read. Use a discard: `_ = await SomethingAsync(ct);` |
| `CA1822` | A member that touches no instance state must be `static`. Also mark lambdas and local functions `static` when they capture nothing. |
| `CA1852` | Every `internal class` that nothing inherits from must be `sealed`. Make every class `sealed` unless it is `abstract` or `static`. |
| `CA1805` | Do not initialise fields to their default (`= null`, `= 0`, `= false`). |
| `S4487` / `S1854` / `S2933` | No unread private fields, no dead stores, and a field assigned only in the constructor is `readonly`. |
| `S3626` / `S1125` | No trailing `return;` / `continue;` that changes nothing; no `== true` / `== false` on a non-nullable `bool`. |

### Nullable reference types

- **Never silence with `!`.** Prove non-null with a pattern (`if (x is not null)`), a guard, or a `TryGet` with `[MaybeNullWhen(false)]`. A `!` is a review finding.
- **Lookups return `T?`**, and callers branch on it. A method that "cannot fail" but returns `null` on a miss is lying to the compiler.
- **Constructor or `required`.** Every non-nullable property is assigned in the constructor, is `required`, or has an initialiser. `= null!` is the same lie as `!`.

### Public-API guards (`CA1062`, `CA1510`–`CA1513`, `CA2208`, `S3928`)

```csharp
public void Register(Persona persona, string roomId, int budget)
{
    ArgumentNullException.ThrowIfNull(persona);
    ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
    ArgumentOutOfRangeException.ThrowIfNegative(budget);
    // ...
}
```

- Every **public method on a public type** that dereferences a reference-type parameter guards it with a throw helper first — nullable annotations alone do not satisfy `CA1062`. `internal` and `private` members are exempt: validate at the boundary, trust internally.
- Use the **throw helpers**, never `if (x is null) throw new ArgumentNullException(...)`.
- When you do construct an argument exception, `paramName` must be `nameof(aRealParameter)`, and the argument order is `(message, paramName)` for `ArgumentException` but `(paramName, message)` for `ArgumentNullException` and `ArgumentOutOfRangeException`.
- Guard `ObjectDisposedException.ThrowIf(this.disposed, this)` at the top of members on disposable types.

### Strings, culture and comparison (`CA1304`, `CA1305`, `CA1309`–`CA1311`, `CA1862`, `CA2251`, `CA1847`, `CA1865`)

| Instead of | Write |
| --- | --- |
| `string.Compare(a, b)`, or `a == b` when case should not matter | `string.Equals(a, b, StringComparison.Ordinal)` (or `OrdinalIgnoreCase`) |
| `s.StartsWith("x")`, `s.EndsWith("x")`, `s.IndexOf("x")` | Same call with `StringComparison.Ordinal` as the last argument |
| `s.Contains("x")` / `s.IndexOf("x")` for a **single character** | `s.Contains('x')` / `s.IndexOf('x')` — the `char` overload |
| `s.ToLower()`, `s.ToUpper()` | `s.ToUpperInvariant()`, or better, a case-insensitive comparison |
| `int.Parse(s)`, `value.ToString()`, `$"{value}"` for numbers or dates in machine-facing text | `int.Parse(s, CultureInfo.InvariantCulture)`, `value.ToString(CultureInfo.InvariantCulture)`, `string.Create(CultureInfo.InvariantCulture, $"...")` |
| `new Dictionary<string, T>()`, `.Distinct()`, `.GroupBy(x => x.Key)` keyed by a string | Pass `StringComparer.Ordinal` explicitly |

Ordinal is the default for identifiers, keys, paths and protocol text. Culture-aware
comparison is for text shown to a human, and only when sorting or searching what they typed.

### Collections and LINQ (`CA1826`–`CA1829`, `CA1859`, `CA1860`, `CA1861`, `CA1869`)

- On a `List<T>`, array or `IReadOnlyCollection<T>`, use `.Count` / `.Length` and `[0]`, not `.Count()`, `.First()` or `.Last()`.
- Use `count > 0` / `count != 0`, not `.Any()`, when the receiver is a collection rather than a lazy sequence.
- **Public API returns `IReadOnlyList<T>`; private helpers return the concrete type.** `CA1859` fires when a private member's declared type is an interface but every return is a `List<T>` — narrow the private signature, keep the public one abstract.
- Hoist constant arrays passed as arguments into a `private static readonly` field. Prefer collection expressions (`[]`, `[a, b]`) and `ReadOnlySpan<T>` for constant data.
- `JsonSerializerOptions` is created once, in a `private static readonly` field, and reused.

### Async, cancellation and disposal (`CA1068`, `CA2016`, `S8949`, `CA2012`, `CA1001`, `CA1816`, `S2930`, `S2997`, `S3168`)

- `CancellationToken` is the **last parameter**, and every awaited call that accepts one receives it. Passing `default` or `CancellationToken.None` from inside a method that has a token is a build error.
- `async Task` or `async ValueTask`, never `async void` (event handlers excepted). Never `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`.
- A `ValueTask` is awaited exactly once, never stored or awaited twice.
- A type owning an `IDisposable` field implements `IDisposable` or `IAsyncDisposable`; a `Dispose` on a non-sealed type ends with `GC.SuppressFinalize(this)`. A disposable created in a `using` is never returned.
- Prefer `await using` for anything async-disposable and the simple `using FileStream stream = new(path, FileMode.Open);` statement form (no braces) otherwise.
- Lock on a `private readonly Lock gate = new();` field (`System.Threading.Lock`), never on `this`, a `string`, a type, or a reassignable field.

### Exceptions (`CA2201`, `CA2200`, `S2486`, `S3984`, `S3871`)

- Throw `InvalidOperationException`, the `ArgumentException` family, `KeyNotFoundException`, `NotSupportedException` or a domain exception — never `Exception`, `SystemException`, `NullReferenceException`, `IndexOutOfRangeException` or `OutOfMemoryException`.
- Rethrow with `throw;`, never `throw ex;`.
- Catch the specific type you handle. Where a general `catch` is unavoidable (a supervisor loop that must not die), the block **must contain a comment or a log call** saying why swallowing is safe; an empty `catch` fails the build.
- Custom exception types are `public`, even in test projects — a test-only fake needs a scoped `#pragma` (see suppressions below).

### Logging (`CA1848`, `CA2254`, `CA1727`, `CA1873`, `S6674`, `S6677`)

```csharp
[LoggerMessage(Level = LogLevel.Warning, Message = "Persona '{PersonaName}' stopped in room {RoomId}.")]
private static partial void LogPersonaStopped(ILogger logger, string personaName, string roomId);
```

- In `Huddle.Acp` and `Huddle.Console`, log through `[LoggerMessage]` partial methods on a `partial class`, as above.
- In `Huddle.App` and `Huddle.Contracts`, direct `this.logger.LogWarning(...)` calls are allowed.
- Everywhere: the message is a **constant template with `{PascalCase}` placeholders**, never an interpolated string; placeholders are unique within a template; arguments are cheap to compute (no `string.Join`, no serialisation) unless the level is checked first.

### Naming (`CA1707`–`CA1725`, `CA1200`)

- `PascalCase` for types, members, constants and enum values; `camelCase` for locals, parameters and private fields (no `_` prefix — see conventions below); interfaces `IFoo`; generic parameters `TFoo`.
- **No underscores** in production identifiers. Tests are exempt and use `Method_Scenario_Expectation`.
- Suffix rules: collections end in `Collection` (or use `List`/`Dictionary` types), exceptions in `Exception`, attributes in `Attribute`, event args in `EventArgs`; nothing else ends in `Stream`, `Queue`, `Stack`, `Delegate`, `Impl`, `Ex` or `Flag`.
- Do not use type names as identifiers (`obj`, `str`, `guid`, `int`), and do not name a public parameter or member after a C# or VB keyword (`event`, `default`, `error`, `date`, `set`, `end`, `next`, `step`).
- An interface implementation keeps the interface's parameter names.
- Use `nameof(x)`, never the string `"x"`; XML `cref` values have no `T:`/`M:` prefix.

### Tests (xunit.v3 analyzers, `S2699`)

```csharp
/// <summary>A turn in a room with no budget left is rejected without reaching the agent.</summary>
[Fact]
public async Task PostAsync_BudgetExhausted_DoesNotCallAgent()
{
    CancellationToken ct = TestContext.Current.CancellationToken;
    RecordingAgent agent = new();
    Room room = new(agent, budget: 0);

    await room.PostAsync("hello", ct);

    Assert.Empty(agent.Calls);
}
```

- Every test method has a `///` summary, a `[Fact]` or `[Theory]`, and **at least one `Assert`**.
- Every awaited call that accepts a `CancellationToken` receives `TestContext.Current.CancellationToken` (`xUnit1051`). No `ConfigureAwait(false)` and no blocking inside a test.
- Use the specific assertion: `Assert.Empty` / `Assert.Single` / `Assert.Contains` / `Assert.Null`, not `Assert.Equal(0, x.Count)` or `Assert.True(x.Contains(y))`. `Assert.ThrowsAsync<T>` for async throws.
- Test classes are `public sealed class FooTests`; a class holding `[GeneratedRegex]` members is also `partial`.

## Conventions the codebase settled on

These do not fail the build, but the code is consistent about them and reviewers expect it.

- **File-scoped namespaces** matching the folder path. `Agency.Huddle.App.Hooks` lives in `src/Huddle.App/Hooks/`.
- **`using` directives go above the namespace** (`.editorconfig`: `outside_namespace`). About half the older files place them below; do not churn those, but write new files the configured way.
- **Fields are plain `camelCase` and accessed as `this.field`.** There are zero `_field` names in the repo; `this.` qualification is the house way of telling a field from a local.
- **Primary constructors** for `sealed` classes whose dependencies are only stored: `internal sealed class HookStore(IFileSystem fileSystem, TimeProvider clock)`.
- **Explicit type on the left, target-typed `new()` on the right.** Write `StringBuilder stringBuilder = new();`, not `StringBuilder stringBuilder = new StringBuilder();` and not `var stringBuilder = new StringBuilder();`. The type is named once, where the reader looks for it. Use `var` only where the type is already spelled on the right (a cast, an `as`, a generic factory such as `Enumerable.Empty<T>()`) or where naming it would be pure noise, such as `foreach (var item in items)` and LINQ results.
- **Allman braces, CRLF, four spaces.** Expression bodies for properties, accessors, indexers and lambdas; block bodies for methods, constructors and local functions.
- **Modern syntax by default:** `is not null`, property and extended patterns, `switch` expressions, `new()` when the type is apparent, collection expressions `[]`, `^1` and ranges, raw string literals `"""` for multi-line text, `u8` for UTF-8 constants.
- **Modifier order** `public/private/protected/internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, required, volatile, async`. Accessibility is always explicit outside interfaces.
- **Regexes are source-generated:** `[GeneratedRegex(pattern, RegexOptions.CultureInvariant)]` on a `private static partial` method. A runtime-constructed `Regex` must be given a match timeout.

## C# Conventions

- Always use XML doc comments (`///`) for all class and method comments — never plain `//` comments for documentation. Use `<summary>`, `<param>`, `<returns>`, and `<see cref="..."/>` tags as appropriate. This applies to test projects as well as production code. The compiler does not check this (`CS1591` is suppressed) — review and the `PreToolUse` hook do.
- Do NOT use `yield return` inside try-catch blocks — this does not compile in C#
- Do NOT instantiate abstract classes directly; use interfaces or concrete implementations
- Sealed classes cannot be mocked — use functional/integration tests or extract an interface
- Always verify builds pass (`dotnet build Huddle.slnx`) after code changes before declaring success — build the **solution**, not one project, because the test projects carry their own analyzers

## Suppressing a rule

Fix the code first. A suppression is for the rare case where the rule is wrong for one spot
and the code is right, and it always carries the reason.

```csharp
#pragma warning disable S127 // Deliberate scan-advance inside the parser loop; the bounds check is on the line above.
index += consumed;
#pragma warning restore S127
```

- **Narrowest scope wins:** a `#pragma disable`/`restore` pair around the lines, or a `[SuppressMessage(Category, "CAxxxx:Title", Justification = "...")]` on one member or type. The justification names the constraint, not the rule.
- **Never add a `NoWarn`** to a `.csproj`, `Directory.Build.props` or `.editorconfig` for a single call site. Those are shared root files; changing what the whole solution enforces is announced first.
- Never suppress a nullable warning; fix the flow instead.

## Principles for C# Coding

- **`record` for data, `sealed class` for behavior.** Records give value equality and non-destructive `with` copies. Sealing blocks accidental inheritance — inherit only when you've explicitly designed for it. Sealing also satisfies `CA1852` and exempts you from `CA1816`.
- **Immutable by default.** `init`-only properties, `readonly` fields, return `IReadOnlyList<T>` / `IReadOnlyDictionary<TK,TV>` from public APIs. Never hand out references to internal mutable state.
- **Push side effects to the edges.** Pure functions in the middle, I/O and mutation at the boundary — the functional core / imperative shell pattern. Same idea you'd lean on in Haskell or Elm, just not enforced by the compiler, so you have to enforce it by discipline.
- **Constructor injection, always.** No service locators, no static singletons for behavior, no `new SomeService()` inside domain code. If a class has no constructor parameters and isn't a pure record, be suspicious.
- **Inject ambient dependencies too.** `TimeProvider`, `IRandom`, `IFileSystem`, message bus, HTTP client. `DateTime.Now` and `new Random()` produce flaky tests and time-bombed code.
- **Make illegal states unrepresentable.** `enum OrderStatus`, not `string status`. Wrap meaningful primitives — `record CustomerId(Guid Value)` beats raw `Guid` passed through twelve methods. The compiler catches the misuse you'd otherwise debug in production.
- **Validate at the boundary, trust internally.** Parse incoming data into domain types once, at the edge. Internal code assumes invariants hold. Don't re-check `if (x == null)` ten layers deep.
- **`Result<T>` (or similar) for expected failure; exceptions for the actually exceptional.** Business validation isn't exceptional. Control flow hidden inside exceptions is invisible to readers and to the compiler. `throw` is for "this should never happen."
- **Async hygiene.** `async Task`, not `async void` (except event handlers). Never `.Result` or `.Wait()` — sync-over-async deadlocks are a rite of passage you should skip. `CancellationToken` on anything that can wait. `ConfigureAwait(false)` in library code (CA2007 is set to `none` in `.editorconfig` — no SynchronizationContext in our ASP.NET Core/generic-host targets — but it's still a reasonable defensive habit; revisit if hosting scenarios broaden pre-1.0).
- **Don't catch `Exception`.** Catch what you actually handle; otherwise let it propagate. Swallowed exceptions are how production bugs go silent for months.
- **Materialize LINQ at boundaries.** `IEnumerable<T>` is a recipe, not a result. Multiple enumeration and lazy side effects will bite. `.ToList()` / `.ToArray()` when crossing a layer.
- **Tests are design feedback.** If something is hard to test, the design is wrong — fix the design, don't add test-only hooks. Hard-to-mock dependency wants to be an interface. Hard-to-reach branch is usually dead code or a missing abstraction.
