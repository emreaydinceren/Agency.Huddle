---
name: system-text-json-net11
description: >
  The three System.Text.Json APIs new in .NET 11: JsonNamingPolicy.PascalCase, the generic
  JsonSerializerOptions.GetTypeInfo<T>() and the no-throw TryGetTypeInfo<T>(out JsonTypeInfo<T>?).
  USE FOR: Huddle code under src/ or tests/ that wants PascalCase JSON names or dictionary keys without a custom
  JsonNamingPolicy, typed JsonTypeInfo<T> without a cast, or a probe for resolvable metadata without try/catch.
  DO NOT USE FOR: changing the casing of a wire format or a file already on disk (ACP JSON-RPC, tool-call logs,
  front matter stores): that is a compatibility change, not an API choice.
license: MIT
---

# System.Text.Json: what .NET 11 adds

Adapted from `dotnet/skills` (`plugins/dotnet11/skills/system-text-json-net11`, MIT, see
`../LICENSE-dotnet-skills`). The repo already targets `net11.0` on SDK `11.0.100` (`global.json`), so upstream's
SDK-install step is dropped.

| Want | Write | Never write |
| --- | --- | --- |
| PascalCase property names | `PropertyNamingPolicy = JsonNamingPolicy.PascalCase` | a `JsonNamingPolicy` subclass, or `[JsonPropertyName]` on every member just to fix casing |
| PascalCase dictionary keys | `DictionaryKeyPolicy = JsonNamingPolicy.PascalCase` (separate from the property policy) | pre-transforming the dictionary |
| Typed metadata | `JsonTypeInfo<T> info = options.GetTypeInfo<T>();` | `(JsonTypeInfo<T>)options.GetTypeInfo(typeof(T))` |
| "Is metadata for `T` resolvable?" | `if (options.TryGetTypeInfo<T>(out JsonTypeInfo<T>? info))` | `try { options.GetTypeInfo<T>(); } catch (NotSupportedException) { }` |

## Huddle facts

- Huddle's stores and logs use `JsonNamingPolicy.CamelCase` (`ToolCallLogWriter`, `SkillProvenance`, the turn
  hooks, `DocHistoryStore`'s enum converter). PascalCase is for a **new** format that needs it, never a reason to
  re-case an existing one: files on disk and the ACP wire would stop round-tripping.
- Options live in a `private static readonly JsonSerializerOptions` field (`CA1869`); a `GetTypeInfo<T>()` result
  you reuse belongs in a static field too, not a per-call lookup.
- `GetTypeInfo<T>()` throws `NotSupportedException` (`NoMetadataForType`) when the options have no
  `TypeInfoResolver`. Options built in a normal project get the reflection resolver on first use, but calling
  `GetTypeInfo<T>()` on a fresh options instance before any serialization needs an explicit
  `TypeInfoResolver = new DefaultJsonTypeInfoResolver()` (or a source-generated `JsonSerializerContext`).
- `TryGetTypeInfo<T>`: branch on the bool. If the compiler still sees `info` as maybe-null inside the `true`
  branch, add `&& info is not null` as upstream's sample does; never `!`.
- File-based apps (`dotnet run app.cs`) disable reflection serialization; set a resolver there. The repo's
  `agents/scripts/*.cs` tools are file-based, so a JSON call added to one of them needs it.

## Example

```csharp
private static readonly JsonSerializerOptions ExportOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.PascalCase,
    DictionaryKeyPolicy = JsonNamingPolicy.PascalCase,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
};

private static readonly JsonTypeInfo<ExportRecord> ExportTypeInfo = ExportOptions.GetTypeInfo<ExportRecord>();

internal static string Serialize(ExportRecord value) => JsonSerializer.Serialize(value, ExportTypeInfo);
```

Pin the output casing with a test that asserts the literal JSON (`"Name":`), so a later policy change fails.

## More

- [JsonNamingPolicy](https://learn.microsoft.com/dotnet/api/system.text.json.jsonnamingpolicy)
- [JsonSerializerOptions.GetTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions.gettypeinfo)
