---
status: accepted
date: 2026-10-08
---

# Team types are data in JSON and Markdown files

A **team type** describes a kind of Team: its roles, the candidates that fill them, its
process and its skill packs. Every team type is a set of files. The built-in types ship
embedded in the app, and the user's own types live in a folder beside the data. Code knows
managers, roles, packs, gates and Requests, but never a role name. The full design is in the
[Teams specification](../engineering/teams-spec.md), under *Data formats* and *Architecture*.

## The problem

The first version of the Teams design put roles in code: a Development Team would have a
developer class, a QA class and a manager class, and a Household would need its own. Every new
kind of Team would then mean a release, and the roles of one type would leak into every
service that touches a Team. The Human wants to add a type, such as a Household or a
research group, by writing files, not by changing the app.

## The decision

**A team type is a folder with a manifest and its candidates and processes.**

```text
TeamTypes/<type>/
  team-type.json            the manifest: roles, manager, retro role, presets, skill packs
  candidates/<role>.md      one candidate per role: who the Teammate is and what it does
  processes/<process>.md    a process template a Team copies into its Process.md
```

- **Built-ins are embedded.** The two built-in types, `development` and `household`, are
  embedded resources under `TeamTypes/Builtin/`, as the SkillCatalog defaults are. A new
  built-in file needs no project change.
- **The user's folder replaces by id.** `{DataDir}/TeamTypes/<id>/` with a manifest of the same
  id replaces the built-in manifest. Candidate and process files resolve the user folder first,
  then the built-in one, so a user can add one file without copying the whole type.
- **Skill packs are files too.** `SkillPacks/*.json` are shared across types.
- **Manifests name tiers, not models.** A role says which tier it needs. The app maps a tier to
  a model through `Team:Teams:Tiers:<adapterId>:<tier>`, and no entry means the adapter's
  default model.
- **A bad type is excluded, not fatal.** `TeamTypeValidator` checks each manifest against the
  Spec's rules and returns a stable rule code. A type that fails is left out of `Types` and
  listed in `TeamTypeCatalog.Problems` with its reason, and loading never throws for bad
  content. The rule codes include `no-manager`, `many-managers`, `duplicate-role`,
  `candidate-missing`, `coverage-uncovered`, `process-missing` and `retro-role-unknown`.
- **The code stays neutral.** `RoleNeutralityTests` scans every `.cs` and `.razor` file under
  `src/Huddle.App` for the role titles and ids of the built-in manifests. A file that
  legitimately names a role is listed in `neutrality-allow.txt`, which may shrink but never
  grow. The Household type is the proof: it runs end to end with no role code.

## Rejected

| Alternative | Why not |
| --- | --- |
| Roles as C# classes or enums | Every new kind of Team becomes a release, and role names spread through the services that act on Teams |
| One database table per team type | Candidates and processes are prose the Human reads and edits, and a file is the form they already take in Teammates and Skills |
| A single file per Team type holding all text inline | A process or candidate is long Markdown. Inline JSON strings are hard to read and to diff |
| A user type that must copy every built-in file | A user who wants one changed candidate would have to copy the whole type, and the copy would drift from the built-in |
| Models named in the manifest | A manifest would have to change whenever a model is retired. A tier name stays valid, and the model is picked in configuration |

## Consequences

- **A team type is changed without a build.** The Human edits a file in `TeamTypes/` and the
  catalog reloads it. A built-in can be overridden by id, and the override is visible in
  `IsUserDefined`.
- **A broken manifest disappears from the offer and says why.** The Problems list is the place
  to look, and no Team can be hired from a type that is in it.
- **The neutrality guard is a test, not a rule of style.** A new role title in app code fails
  `RoleNeutralityTests`. The allow-list has a ceiling, so it cannot quietly grow to cover
  everything.
- **Adding a type can still need code when it needs a new kind of check.** A new gate or a new
  Request kind is code, because the app enforces it. A new role, candidate, process or pack is
  files only.
