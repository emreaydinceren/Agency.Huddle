---
status: accepted
date: 2026-10-10
---

# A hiring pool holds the jobs and a team type lists them

> **Amends [ADR-0038](0038-team-types-are-data-in-json-and-markdown-files.md).** A team type is still
> data, a manifest plus files, and code still knows no role name. What changes is *where a role's
> description lives*: in one global **hiring pool**, not inside each type. The full design is in the
> [Hiring Pool specification](../Huddle.HiringPool-Specifications.md).

## The defect

ADR-0038 nested every role in its type: a `candidates/` folder and a `roles` list per type. That was
right for two types and wrong for twenty-six. An **Analyst** is the same job in a Data team and a
Research team, and a **Bookkeeper** suits a Small Business and a Tax Season team. Nested, each
copy drifts, a Human cannot browse what is hireable beyond the open type, and the hiring pool could
offer only one type's roles plus a **Custom role**. Presets (Small, Full) papered over the size of
the list instead of letting a Human find a job.

## The decision

**A job lives once, in `hiringpool.json` and a persona file. A team type lists the jobs it uses and
keeps only what is true of the role in that type.**

| Lives in the pool (`HiringPool/`) | Lives in the type (`team-type.json`) |
| --- | --- |
| title, description, tags | the role id and the job it names |
| suggested skills (names) and installable `skillRefs` | manager, required, limit (`maxPerTeam`) |
| the team types it suits | the coverage steps the role covers |
| model tier, connectors it needs, suggested names, packs | `differentVendorFrom` |
| avatars that suit it | |
| the persona, one Markdown file | |

The pool also names each team type (name and description). The hiring pool screen filters by two
chip clouds, **team types** (any) and **tags plus skill names** (all), as the avatar picker does.

### Decisions

| # | Decision | The alternative it beat |
| --- | --- | --- |
| D-1 | **A type's role is a reference to a job**, composed into today's `RoleSpec` when the catalog loads. | Changing every consumer (about twenty) to look up a job. Composing keeps `type.Roles.First(r => r.Id == …)` working and the stored `_role: type/role@version` valid. |
| D-2 | **The role id stays the id the role always had**, and equals the job id unless the manifest says otherwise. | Re-keying hires by job id, which would orphan every saved Teammate. |
| D-3 | **Every pool job the type does not list is offered as an optional, pooled role** (not manager, not required, limit one, covers nothing). | Hiring only the type's own jobs. It would make the pool a menu of one, and Custom role the only way out. |
| D-4 | **The persona is a Markdown file per job**, in the candidate shape. | Prose inside the JSON, which ADR-0038 already rejected. |
| D-5 | **No presets.** Two clouds find a job; a preset hid the list behind a name. | Keeping Small and Full for the two original types only. Two ways to hire is worse than one. |
| D-6 | **A user pool is merged by id on top of the shipped one.** A user job that is rejected leaves the shipped job in place, and a user persona that does not parse falls back to the shipped persona. Entries with no id are reported and skipped; loading never throws. | Replacing the file as the skills catalog does, which would let one user job hide the shipped pool. Validating after the merge, which let one bad override take a shipped job, and every type that used it, away. |
| D-7 | **Tags and skill names come from a controlled vocabulary**, pinned by a test. | Free text, which turns the second cloud into a thousand near-duplicate chips. |

## What does not change

- A type is a folder with a `team-type.json`, processes and, for a user type, its own files.
  A manifest with inline `title` and `candidate` and no `job` still loads, so a user's type keeps
  working. A copy of a built-in type that Custom role saved before the pool existed names candidate
  files that moved into the pool; a role in it whose candidate file is gone and whose id is a pool
  job is composed from that job, and the user's own custom roles keep their candidate files.
- A role that names a job reads its persona from the pool (whose user folder overrides a shipped
  persona), not from a `candidates` folder of the type.
- Code names no role. `RoleNeutralityTests` now reads job titles from the pool.
- A role whose output leaves the team (an email, a filing, a payment, a signature) is a drafting
  role, and its persona names the Human who sends or approves.

## Consequences

- Adding a team type is a manifest, a process and the jobs it needs. No C#.
- `TeamTypeCatalog` depends on `HiringPoolCatalog`; a type that names an unknown job is rejected
  with the job id in the problem.
- `UserCandidateWriter` still writes a custom role into the user's copy of the type. Writing it to
  the user pool, so it appears in the clouds, is the next step.
