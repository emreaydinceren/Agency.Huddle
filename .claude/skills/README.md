# Project skills

Claude Code loads each `<name>/SKILL.md` here as a project skill. The ones below come from Microsoft's
[dotnet/skills](https://github.com/dotnet/skills) (MIT, `LICENSE-dotnet-skills`), taken from the local clone at
`E:\GitHub\dotnet-skills` at commit `2877c712` (2026-11-11).

| Skill | Upstream | How it was brought in |
| --- | --- | --- |
| `process-api-net11` | `plugins/dotnet11/skills/process-api-net11` | Rewritten to house style; maps the new APIs onto Huddle's four process call sites |
| `system-text-json-net11` | `plugins/dotnet11/skills/system-text-json-net11` | Trimmed (SDK install step dropped); Huddle's camelCase stores noted |
| `dotnet11-breaking-changes` | `plugins/dotnet-upgrade/skills/migrate-dotnet10-to-dotnet11` | The migration is done, so the workflow became a checked-against-the-repo table; four `references/` copied unedited |
| `msbuild` | `plugins/dotnet/skills/msbuild` | Copied unedited, with a Huddle preface box |
| `blazor-prerender-interop` | `plugins/dotnet-blazor/skills/support-prerendering`, `use-js-interop` | Merged and cut to this app's InteractiveServer + prerender setup; records the owner's move from `window.*` globals to JS modules |

Left out on purpose: MAUI, WinForms, EF Core, Aspire/AI, NuGet publishing, test-framework migrations to MSTest,
and the other Blazor skills (project scaffolding, auth, forms, Ignite UI), which do not apply or which the
`agents/` guides already cover. The `dotnet-test` skills (`run-tests`, `filter-syntax`) duplicate
`agents/Testing.md` for this repo's MTP + xUnit v3 setup. The 21 standalone `dotnet-msbuild` skills cover the
same topics as the `msbuild` skill's `references/`, at greater length. They were not copied: if a build
investigation needs more depth, read them from the clone.

To refresh: pull the clone, diff the upstream folder against the copy, and re-apply the Huddle edits by hand.
