---
layout: huddle-skillpacks
manifest: packs/*.skillpack.json
packsFrom: packs/
---
# skills

**What this is:** the Skill Packs Agency.Huddle used to ship inside the app, moved out on
2026-10-17 so they can be updated without a Huddle release. Each file in `packs/` is one Pack
in Huddle's `.skillpack.json` schema (`schemaVersion: 1`); the design is
`docs/Huddle.SkillPacksSpec.md` in the Huddle repository.

**Where it lives:** `E:\Repos\skills` on this computer. SKILLS.md in Huddle's Skill Sources
folder points at the files in `packs/`; another computer adds its own entry to those paths.

**Packs**

| Pack | Version | For | Skills |
| --- | --- | --- | --- |
| `msbuild-authoring` | 1.0.0 | developer, release-engineer | `target-authoring`, from the `dotnet-skills` catalog |
| `code-review-and-quality` | 1.0.0 | reviewer | `code-review-and-quality`, from the `addyosmani-agent-skills` catalog |

**Quirks**
- The Packs hold no Skill files of their own. Each Skill names its catalog entry
  (`"catalog": "dotnet-skills"`), so the Skill's path, origin and commit come from that
  catalog and the Pack and the catalog share one copy.
- Version stays `1.0.0`, matching the `_packs: ['msbuild-authoring@1.0.0']` lines Agents
  already carry, so moving here shows no update.
- The catalogs these Packs name (`dotnet-skills`, `addyosmani-agent-skills`) must be listed in
  SKILLS.md, or the Packs show as Not downloaded.

**To update:** edit or add a `packs/*.skillpack.json`, bump `version`, run
`huddle-packs validate --strict` on the file, then update the row's Last updated in SKILLS.md.