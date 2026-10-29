---
status: accepted
date: 2026-10-17
---

# Skills and Packs are fetched by the Human's AI tool

> **Partly supersedes** [ADR-0043](0043-imported-skills-are-pinned-by-commit-and-hash.md). Pinning, hashing and the
> checks stay; the download goes. **Amends** [ADR-0027](0027-the-library-sees-only-configured-roots.md) with one
> read-only root. The full design is in [Huddle.SkillPacks-Specifications.md](../Huddle.SkillPacks-Specifications.md).

## The problem

Huddle downloads Skills from public GitHub and ships two Packs and a catalog inside the app. Shipped Packs go stale
between releases and cannot be retired. The downloader reaches only public GitHub, because Huddle has no browser and
no way to sign in. The Human's own AI tool, such as Claude Code, already has their sign-ins and can read any
repository layout.

## The decision

1. **Huddle makes no network call for Skills.** It builds no `HttpClient` for them.
2. **The Human's AI tool fetches.** `InstallSkillPack.md` tells it what Huddle expects, and it records pointers in
   SKILLS.md, the registry in the Skill Sources folder.
3. **Huddle reads pointers and checks what they lead to.** The checks live in the `Huddle.SkillPacks` library;
   Huddle and the `huddle-packs` command run the same code, so their findings cannot drift.
4. **There is no approval step.** The checks are the gate.
5. **An Agent uses a copy in `Skills/`, never a clone.** The copy is made, and checked, before the Agent's file
   changes.
6. **Only built-in Skills grant App Tools.** A fetched Skill whose `tools:` names one is Broken (`S011`).
7. **One copy per Skill name, shared by every holder.** A change to a Skill's files reaches all holders at once;
   membership changes are per Agent.
8. **The first row wins every tie** between two rows, two catalogs or two origins of a name. The loser carries the
   finding.
9. **Huddle deletes only inside the Skill Sources folder and `Skills/.staging` and `Skills/.trash`.** It never deletes
   a clone.
10. **The shipped Packs move to the skills repository, and the shipped catalog is split by a one-time move.** The
    catalog's 265 entries hold 263 distinct Skill names, so the move writes 263 Skills.
11. **The Skill Sources folder is a read-only Library root.** This amends ADR-0027.
12. **`Team:SkillsRepositoryDir` is a configuration key read once by the move**, not a UI setting.
13. **The prompts the Human pastes are configuration** ([ADR-0007](0007-model-facing-text-is-configuration.md)), as is
    the fixed body of `InstallSkillPack.md`.

## Options

- **Keep the downloader and add sign-in.** Huddle would need a browser flow and token storage per host, and still
  could not read arbitrary repository layouts.
- **An agent inside Huddle that imports.** Duplicates what the Human's AI tool already does, with fewer sign-ins.
- **The Human's AI tool fetches; Huddle follows local pointers (chosen).** Huddle stays offline, and the written
  contract plus a shared validator keep what passes in the AI tool working in Huddle.

## Consequences

- `SkillDownloader`, `SkillImporter`, `ImportingSkillInstaller`, `SkillsCatalogIndex`, `SkillPackStore` and the
  shipped Pack files are deleted once their replacements land.
- A first start of this version runs the move. It writes SKILLS.md, six catalog files and the Source notes, and
  changes no Agent file.
- A Skill Sources folder shared between computers needs a per-computer path entry for each clone that lives in a
  different place.
