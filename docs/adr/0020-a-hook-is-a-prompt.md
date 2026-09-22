---
status: accepted
date: 2026-09-22
---

# A Hook is a Prompt

> **Renames [ADR-0007](0007-model-facing-text-is-configuration.md).** That decision stands in
> full — a named piece of model-facing text is a template, not an event; it carries a
> `Timing`; its defaults live in code and the shipped file is generated from them; its text
> never contains `mcp__team__`. Only the name changes, everywhere it was used: type names,
> file and folder names, the `{DataDir}/hooks.json` override file, the `/settings/hooks`
> route, and the Settings tab label.

## The word did exactly what it was warned it would do

ADR-0007 spent the word deliberately: *"the settings panel is named for it, but nothing here
executes, nothing subscribes... This spends the word. If executable extension points are ever
wanted at these same sites, they will need a different name, because 'hook' will already mean
a piece of text."*

That bet did not pay off. "Hook" is a term of art for an executable extension point — a git
hook, a React hook, a webhook, a `PreToolUse` hook in this very toolchain's own configuration.
A reader meeting `HookStore` or the Settings > Hooks tab for the first time reasonably guesses
it wires up behaviour, not that it holds editable wording. That guess is exactly backwards, and
it is the kind of misunderstanding a name is supposed to prevent rather than invite.

## Prompt is the accurate word, and it was already half-used

The type doing the actual rendering was always `SystemPromptComposer`, the golden files were
always `PromptGoldenTests`, and ADR-0007 itself, in the same paragraph that coined "Hook",
called the underlying thing "model-facing text" and "a prompt" in lowercase prose throughout.
"Prompt" was never a foreign word here — it names the thing correctly and stops fighting the
vocabulary the rest of the codebase already used for it.

## What actually changed, and what did not

Every `Hook`-prefixed type, file, folder and namespace became its `Prompt`-prefixed
equivalent — `HookCatalog` → `PromptCatalog`, `HookStore` → `PromptStore`, `IHookSource` →
`IPromptSource`, and so on, one for one. `{DataDir}/hooks.json` became `{DataDir}/prompts.json`
and `hooks.default.json` became `prompts.default.json`; the Settings route moved from
`/settings/hooks` to `/settings/prompts`. This was a full rename, including the persisted file
names, with no backward-compatible alias: the application has no shipped installs to break, and
`App_Data/` — where the override file lives — is entirely gitignored, so no tracked data was
ever at stake, only a developer's local, disposable override file.

None of ADR-0007's design survives this ADR by accident — it survives because nothing about it
was wrong. `PromptTiming`, the never-restarts-a-session rule, the advisory-only validator, the
generated-from-code default file: all unchanged, under a name a reader no longer has to unlearn.

## What this frees back up

"Hook" is available again for the thing ADR-0007 described and declined to build: a real
executable extension point at one of these sites — a block that shells out, a plugin. If that
is ever wanted, it can be named what it is without colliding with this feature.
