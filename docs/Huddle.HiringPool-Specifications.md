# Huddle.HiringPool — Design Specification

**Date:** 2026-10-10 · **Status:** Built 2026-10-10 · **Decision record:**
[ADR-0049](adr/0049-a-hiring-pool-holds-the-jobs-and-a-team-type-lists-them.md) (Accepted) ·
**Amends:** [ADR-0038](adr/0038-team-types-are-data-in-json-and-markdown-files.md) ·
**Vocabulary:** [language.md](engineering/language.md) (**Team type**, **Hiring pool**, **Role**,
**Candidate**)

This is the design for the hiring pool: one global catalog of jobs that every team type draws its
roles from, and the screen that browses it with two tag clouds. It is written for the engineers or
agents who change it.

> [!IMPORTANT]
> [rules.md](engineering/rules.md) is binding before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file.

---

## 1. Goal

A Human setting up a Team should find the job they need without knowing which team type "owns" it.
An **Analyst** is hireable in a Data team and a Research team. A **Bookkeeper** suits a Small
Business and a Tax Season team. The pool lists each job once, says which types it suits, and lets the
Human filter by type, by tag and by skill.

## 2. The files

All under `src/Huddle.App/HiringPool/`, embedded in the assembly:

```text
hiringpool.json        the team type names and every job
personas/<job-id>.md   one persona per job
```

A **user pool** at `{DataDir}/HiringPool/hiringpool.json` (and `personas/`) is merged by id on top:
a user job replaces a shipped job of the same id, and never hides the rest. Personas resolve the
user folder first.

### 2.1 `hiringpool.json`

```json
{
  "schemaVersion": 1,
  "teamTypes": [ { "id": "small-business", "name": "Small Business", "description": "…" } ],
  "jobs": [
    {
      "id": "bookkeeper",
      "title": "Bookkeeper",
      "description": "One sentence.",
      "tags": [ "accounting", "finance" ],
      "skills": [ "budget-modeling", "data-cleaning" ],
      "skillRefs": [ { "name": "…", "source": "builtin", "path": "…" } ],
      "teamTypes": [ "small-business", "tax-season" ],
      "persona": "personas/bookkeeper.md",
      "model": { "tier": "balanced" },
      "requires": [ "email:read" ],
      "suggestedNames": [ "Ann", "Raj", "Eve" ],
      "packs": [ "msbuild-authoring" ],
      "avatars": [ "accountant_fair_m_01.png" ]
    }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `teamTypes[]` | The type's id, display name and description. The pool is the source of a type's name and description; a manifest `displayName` is the fallback for a user type the pool does not name. |
| `id` | Lower-case letters, digits and hyphens. It is the role id the job takes in a type. |
| `tags`, `skills` | What the second cloud filters by. `skills` are names only. |
| `skillRefs` | The installable skills a hire carries (`name`, `source`, `path`), unchanged from ADR-0038. |
| `teamTypes` | The types the job suits. It drives the first cloud. |
| `persona` | The persona file below the pool folder; `personas/<id>.md` when omitted. |
| `model`, `requires`, `suggestedNames`, `packs` | As on a role in ADR-0038, now owned by the job. |
| `avatars` | Avatar library files that suit the job, best first. A suggestion for the avatar picker. |

### 2.2 A persona

The existing candidate shape: frontmatter with `title` and `consult_when`, then the stance. A
persona never has a `name` key (hiring supplies the name). A job whose persona is missing or does
not parse is left out, and the problem names the job.

### 2.3 A team type's roles

```json
{ "id": "bookkeeper", "job": "bookkeeper", "required": false, "maxPerTeam": 1, "covers": [ "generate" ],
  "model": { "differentVendorFrom": [ "business-manager" ] } }
```

`job` defaults to `id`. The type owns the manager flag, `required`, `maxPerTeam`, `covers` and
`differentVendorFrom`; the job owns the rest. A role with an inline `title` and `candidate` and no
`job` is the old shape and still loads.

## 3. Composition

`TeamTypeCatalog` composes each job role into the `RoleSpec` every consumer already reads, so no
consumer changes. After validation it appends every pool job the type does not list as an optional
**pooled** role (`Pooled = true`): not a manager, not required, limit one, covering nothing. A
pooled role can be hired like any other, and `TeamCheckBar`, `HireService` and the rest see a plain
role. A type that names an unknown job is rejected (`job-unknown`).

## 4. The screen

`HiringPool.razor` shows one card per job and two clouds (`ChipCloud`, the avatar picker's cloud as
a component):

| Cloud | Chips | Selecting several |
| --- | --- | --- |
| Team types | every type of the catalog | **any** (OR); none selected means every type |
| Tags and skills | every tag and skill name of the cards, most used first (40, then "Show all") | **all** (AND) |

The clouds combine with AND. The first cloud opens on the Team's own type; a role the open type
lists itself always suits it. Clouds are static: they do not hide or recount chips (as in the avatar
picker). **Clear all** resets both. A pool with no team types and no terms shows no clouds.

## 5. Vocabulary

Tags and suggested skill names come from a controlled list, pinned by `BuiltinTeamTypesTests`, so
the cloud stays coherent. Tags: planning, research, writing, editing, verification, analysis, data,
code, docs, design, finance, tax, budgeting, accounting, legal, compliance, health, care, home,
family, education, career, sales, marketing, support, community, events, travel, email,
scheduling, tracking, triage, forms, purchasing, security, operations. Add a word to the test when
the pool truly needs one.

## 6. Safety stance

A job whose output reaches someone outside the team (an email, a filing, a payment, a signature) is
a **drafting** role, and its persona names the Human who sends or approves it. Reviewers and
checkers are read-only in stance and use `differentVendorFrom` against their maker. No persona
gives legal, medical, tax or investment advice; they prepare material for a professional or the
Human to act on.

## 7. Built-in types

26 types ship: Development Team, Household, Small Business, Content, Research, Data, Product, Ops,
Legal, Life Admin, Learning, Events, Job Search, Sales Outreach, Docs, Open Source Maintainers,
Grant Proposal, Tax Season, Support Desk, Founder's Office, Home Project, Book Project, Caregiving,
Community Org, Relocation and Home Lab, built from 97 jobs. Each type is a manifest and one
default process; adding a type needs no C#.

## 8. Not built yet

- Saving a **Custom role** to the user pool (it is still written into the user's copy of the type,
  so it does not appear in the clouds).
- Using a job's `avatars` to pick the avatar of a hire (the field is data only).
- Refactoring the avatar picker onto `ChipCloud`.
- Avatars for the jobs the library covers weakly (Lawyer, Compliance Officer, Recruiter, Sales
  Representative, Event Planner, Real Estate Agent and others).
