# Skill Packs: user guide and plan (envisioned)

This page describes, from the Human's side, how Skills and Skill Packs will reach Huddle once
your own AI tool (Claude Code, Codex or another) fetches them and Huddle only follows the
pointers it is given. The second half plans the pieces that make it work. It is for whoever
turns this vision into requirements and a specification, and for reviewers who want to see the
feature before it is designed.

> [!NOTE]
> **Status: envisioned 2026-10-17. Requirements met by [Huddle.SkillPacks-Specifications.md](Huddle.SkillPacks-Specifications.md)**,
> built 2026-10-18. Where this page and the specification differ, the specification wins; the corrections
> it made are folded in here (the update card, the catalog's count, the Skills repository, **Learn more**
> and the move's Source ids). What Packs and Skills do now is in the
> [User Guide](Huddle.UserGuide.md#giving-teammates-skills).

This page does not cover writing your own Skill, or Theme style packs, which are a different
feature that shares the word.

## Executive summary

**The problem.** Huddle ships two Packs and a catalog of 263 Skills inside the app, so they go
stale between releases and cannot be retired. Huddle downloads Skills itself, only from public
GitHub, because it has no browser and no way to sign in anywhere. And adding a Pack means
writing JSON by hand.

**The answer.** Huddle stops fetching Skills altogether. Your AI tool, which already has your
sign-ins, clones or downloads them **wherever it likes** and records a **pointer**, a local
path, in a registry file, **SKILLS.md**. Huddle reads the pointers, checks what they point at,
and offers the result to your Teammates.

| Piece | What it is |
| --- | --- |
| **`InstallSkillPack.md`** | A file Huddle writes that tells any AI tool everything Huddle expects, with an appendix about this Huddle. You paste its path, with a link, into your AI tool |
| **SKILLS.md** | Two tables, Packs and Catalogs. Each row points at one file, a `.skillpack.json` or a `.skillcatalog.json`, which lists its Skills and their paths. A row has a required **Local path** and optional **Origin URL**, **Last updated** and **Notes** |
| **`huddle-packs validate`** | A standalone command that runs the same checks Huddle runs. Your AI tool runs it until it passes |
| **Source notes** | One `NOTES.md` per Source, kept by the AI tool, so the next update starts from what was learned last time |

**What it means for you.**

- **No approval step.** A Pack or Skill that passes the checks is usable as soon as its row
  appears. The checks, not a click, keep out what Huddle cannot use safely.
- **Teammates do not change behind your back.** Taking a Pack copies its Skills into `Skills/`.
  When the files at a pointer change, cards show **Update available** and you choose when to
  apply it.
- **Huddle never goes online for Skills,** never signs in, and never deletes a clone it did not
  make.
- **Moving to it is automatic.** On first start, today's Packs become `.skillpack.json` files,
  the catalog becomes six `.skillcatalog.json` files, one per repository, and every imported
  Skill is pointed at. No Teammate restarts.

**What it means for the code.** The downloader, the importer and the hire-time installer go.
A registry reader, one shared library of checks, the `huddle-packs` command and the
`InstallSkillPack.md` writer come in. [ADR-0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md)
is partly superseded and needs a successor ADR.

## Contents

- [Executive summary](#executive-summary)
- [Decisions so far](#decisions-so-far)
- **The user guide**
  - [What changes from today](#what-changes-from-today)
  - [Words used on this page](#words-used-on-this-page)
  - [Who does what](#who-does-what)
  - [The Skills page at a glance](#the-skills-page-at-a-glance)
  - [Discovering and adding Packs and Skills](#discovering-and-adding-packs-and-skills)
  - [Choosing a Pack or Skill for a Teammate](#choosing-a-pack-or-skill-for-a-teammate)
  - [Keeping Packs and Skills up to date](#keeping-packs-and-skills-up-to-date)
  - [Deprecated, withdrawn and moved Sources](#deprecated-withdrawn-and-moved-sources)
  - [Removing a Pack, a Skill or a Source](#removing-a-pack-a-skill-or-a-source)
  - [The Skill Sources folder and Source notes](#the-skill-sources-folder-and-source-notes)
  - [Sharing with another Huddle](#sharing-with-another-huddle)
  - [What changes on disk](#what-changes-on-disk)
- **The plan**
  - [Plan: the InstallSkillPack.md file](#plan-the-installskillpackmd-file)
  - [Plan: the validate command](#plan-the-validate-command)
  - [Plan: moving today's Packs and Skills](#plan-moving-todays-packs-and-skills)
- [Notes](#notes)
- [Open questions for requirements](#open-questions-for-requirements)

## Decisions so far

Settled while envisioning, 2026-10-17. Requirements start from these.

| # | Decision |
| --- | --- |
| 1 | Huddle has no import agent of its own. The Human's own AI tool does the work, guided by `InstallSkillPack.md` |
| 2 | **Huddle makes no network calls for Skills at all**, in a Pack or on their own. Signing in, cloning and downloading are the AI tool's job |
| 3 | **Where a Skill is cloned or downloaded does not matter.** SKILLS.md holds a pointer, a local path, and Huddle follows it |
| 4 | SKILLS.md is the registry: a Packs table and a Catalogs table, with **one row per file, not per Skill**. A Packs row points at a `.skillpack.json`, a Catalogs row at a `.skillcatalog.json`, and that file lists its Skills and their paths. Each row has a required **Local path** and optional **Origin URL**, **Last updated** and **Notes**. A note at its top recommends against editing it by hand |
| 5 | **No approval step.** Everything listed that passes the checks is usable |
| 6 | `SkillsCatalog.json` is split into one `.skillcatalog.json` per repository, six files, as if an AI tool had written them, each a row in the Catalogs table like any other catalog |
| 7 | The validate command is a standalone executable, `huddle-packs`. A mode of the Huddle executable is acceptable if it proves simpler |
| 8 | **The two Packs that ship today move out of Huddle** into a skills repository, kept at `E:\Repos\skills` on this computer. Huddle stops embedding them; SKILLS.md points at the repository like any other Source, and an AI tool updates it like any other |
| 9 | One `NOTES.md` per Source |
| 10 | The Skill Sources folder is a read-only Library root |
| 11 | Removing a Source deletes its notes |
| 12 | The Skill Sources folder can be shared between Huddles |
| 13 | `InstallSkillPack.md` lists this Huddle's roles, and its installed Skills so the AI tool reuses them and avoids name clashes |
| 14 | **A path can be given per computer.** Wherever a path goes, it may hold one entry per computer, so a shared folder works on computers that cloned to different places |
| 15 | **Only Huddle's built-in Skills grant App Tools.** A Pack or a fetched Skill never can (check `S011`) |
| 16 | **A Pack can name a catalog's Skill** instead of giving its own path, so one Skill in a catalog and in a Pack stays one copy |
| 17 | **The Skills section carries very descriptive guidance text**: an intro on every tab, an explanation for every state and button, and problems that say how to fix them (see [Guidance text on every tab](#guidance-text-on-every-tab)) |

## What changes from today

| Today | Envisioned |
| --- | --- |
| Two Packs ship inside the app, in `TeamTypes/Builtin/SkillPacks/`, and change only with a Huddle release | Packs are rows in SKILLS.md. The two shipped Packs become rows on first run (see [the move](#plan-moving-todays-packs-and-skills)) |
| A catalog of 263 Skills ships inside the app as `SkillsCatalog.json` | It becomes six `.skillcatalog.json` files, one per repository, each a row in the Catalogs table. Its Skills show as **Not downloaded** until your AI tool fetches them |
| Huddle downloads Skills from public GitHub: on **Import skill**, and during **Approve & hire** | Huddle downloads nothing. Both places build a prompt for your AI tool, which can reach anything you can sign in to |
| To add a Pack you write a JSON file in `SkillPacks/` by hand | You paste a prompt into your AI tool. It fetches, writes the Pack file, checks it and adds the row |
| A Pack never says it is out of date; `_packs` records a version nobody compares | When the files at a pointer differ from what Teammates use, cards show **Update available** |
| A Pack cannot be retired | A Source can mark a Pack **Deprecated** or **Withdrawn**, with a reason |
| Nothing records how a Pack was found | Every Source has a `NOTES.md` the AI tool keeps, and a `source.json` |

## Words used on this page

| Word | Meaning |
| --- | --- |
| **Pack** | A named, versioned set of Skills suited to some roles, which a Teammate's card adds or removes as one chip; it can also add a phrase to the Teammate's `consult_when` line ([language.md](engineering/language.md) owns the term) |
| **Skill pack file** | `<id>.skillpack.json`: one Pack, with its roles, its `consult_when` phrase, and every Skill in it with its `path` |
| **Skill catalog file** | `<id>.skillcatalog.json`: any number of Skills from one Source, each with its name, description, publisher, licence, category and `path`, if it has been fetched |
| **Pointer** | A local path, absolute or relative to the file it is written in. A SKILLS.md row points at a skill pack or skill catalog file; inside those files, each Skill's `path` points at its folder. A path may hold [one entry per computer](#one-path-per-computer). Huddle follows pointers and nothing else |
| **Source** | One place Skills come from, such as a GitHub repository, another Git server, a local folder, or the skills repository that now holds the Packs Huddle used to ship. Each pack and catalog file names its Source |
| **Skill Sources folder** | `App_Data/SkillSources/`: SKILLS.md, the pack and catalog files the AI tool writes there, and each Source's notes. It can be shared |
| **SKILLS.md** | The registry: a Packs table and a Catalogs table, one row per file |
| **`InstallSkillPack.md`** | `App_Data/InstallSkillPack.md`, written by Huddle: what Huddle expects from your AI tool, step by step |
| **Validate command** | `huddle-packs validate`, the same checks Huddle runs, as a command your AI tool can run |

## Who does what

| Your AI tool | Huddle |
| --- | --- |
| Signs in wherever it needs to, with your credentials | Never signs in, never goes online for Skills |
| Reads `InstallSkillPack.md`, the repository's manifests and each `SKILL.md` | Writes `InstallSkillPack.md`, with an appendix of your roles, Skills and Sources |
| Clones or downloads the Skills, pinned to one commit, to any local path | Does not care where; follows the pointer |
| Writes the skill pack or skill catalog file, `source.json` and `NOTES.md` | Reads them; shows as **Broken** what fails the schema |
| Runs the validate command until it passes | Runs the same checks whenever SKILLS.md or a file it points at changes |
| Adds or updates the row in SKILLS.md, then tells you | Offers the Pack or Skill on Teammate cards as soon as it passes |
| Checks for updates when you ask | Notices when the files at a pointer differ from Teammates' copies, and shows **Update available** |

## The Skills page at a glance

Settings › Skills gains three tabs. **Add with your AI tool** opens the dialog that gives you
a prompt to paste.

```text
┌─ Settings ──────────────────────────────────────────────────────────────────────┐
│ ┌────────────┐ ┌ Skills ──────────────────────────────────────────────────────┐ │
│ │ Profile    │ │ [ Skills ]  [ Packs ]  [ Sources ]                       G4a │ │
│ │ Prompts    │ ├──────────────────────────────────────────────────────────────┤ │
│ │ Appearance │ │ Packs   [Add with your AI tool] [Check for updates] [Reload] │ │
│ │ Personas   │ │ 3 Sources · 1 new · 1 problem · SKILLS.md read 1 minute ago  │ │
│ │▌Skills  1  │ │                                                          G4b │ │
│ │ Team types │ │ ┌ Document skills         dbd4588 ──────────────────── New ┐ │ │
│ │ Library    │ │ │ anthropics/skills · 4 Skills · updated 2026-10-17        │ │ │
│ └────────────┘ │ │ bookkeeper, business-manager, writer, +2           [ ⋯ ] │ │ │
│      G2a       │ └──────────────────────────────────────────────────────────┘ │ │
│                │ ┌ MSBuild Authoring         1.0.0 ──────────── In use by 2 ┐ │ │
│                │ │ repos/skills · 1 Skill · developer, release-engineer     │ │ │
│                │ │ Update available                                   [ ⋯ ] │ │ │
│                │ └──────────────────────────────────────────────────────────┘ │ │
│                │ ┌ Code Review and Quality   1.0.0 ───────── Not downloaded ┐ │ │
│                │ │ repos/skills · 1 Skill · reviewer                        │ │ │
│                │ │ code-review-and-quality has no folder[ Copy fix prompt ] │ │ │
│                │ └──────────────────────────────────────────────────────────┘ │ │
│                │                                                          G4c │ │
│                └──────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────┘
```

| Code (proposed) | Container | What it holds |
| --- | --- | --- |
| `G4` | Skills section | The existing `SkillsPanel` (`G3`), split into tabs. The Skills row in the section menu (`G2a`) shows how many Packs or Skills have a problem |
| `G4a` | Skills tab rail | **Skills** (today's list, now with every Skill from the catalogs, marked **Not downloaded** until fetched), **Packs**, **Sources** |
| `G4b` | Pack bar | **Add with your AI tool** and **Check for updates** (both open `G5`), **Reload** (reads SKILLS.md and its pointers again), and a status line |
| `G4c` | Pack card | Title, version or short commit, Source, Skill count, the roles it suits, its **Last updated** from SKILLS.md, and its state chip. **⋯** opens Details (`G6`), Hide, Remove and Show in Library. A card with a problem has **Copy fix prompt** |

A Pack or Skill shows one of these states:

| State | Meaning |
| --- | --- |
| **New** | Its row appeared since you last opened this tab. It is usable already; the chip only draws your eye |
| **Available** | Passes the checks; no Teammate holds it |
| **In use by N** | N Teammates carry it, in `_packs` for a Pack or `skills:` for a Skill |
| **Update available** | The files at its pointer differ from the copies its Teammates use |
| **Not downloaded** | The file the row points at is fine, but a Skill's `path` in it is empty, leads nowhere, or has no entry for this computer. Every Skill from the shipped catalog starts here |
| **Deprecated** | Its Source still lists it but advises against it, with a reason |
| **Withdrawn** | Its Source pulled it. New hires cannot take it; Teammates who hold it are flagged |
| **Broken** | The pointer leads nowhere, or a check fails. The card names the check and the file |

### Guidance text on every tab

This model is new to most people: Huddle lists Skills it does not fetch, and someone else's
tool does the fetching. So the Skills section explains itself in **very descriptive guidance
text**, not just labels. A Human who has never read this page should be able to work out what
each tab is, where its contents come from, what Huddle will and will not do, and what to do
next, from the screen alone.

The rules:

| Rule | Why |
| --- | --- |
| **Every tab opens with a `SettingsIntro`** of three to five short sentences, using the drafts below as a starting point | The intro is the one place everyone reads; it carries the model |
| **Define the tab's word in its first sentence**: Skill, Pack or Source | People arrive from a Teammate card without knowing the terms |
| **Say what Huddle does not do**: it does not download, sign in or delete a clone it did not make | Otherwise people wait for a download that never starts |
| **Say what happens next** after each action: a copied prompt goes to your AI tool, and a new row is usable once it passes the checks | Nothing visible happens in Huddle between the copy and the new row |
| **Every state chip and every button has a one-sentence explanation**, shown on hover, on keyboard focus and in Details (`G6`). The chip text uses the [states table](#the-skills-page-at-a-glance) meanings | A chip such as **Not downloaded** means nothing without its cause |
| **Each task in the prompt dialog (`G5a`) has a line under it** saying what the AI tool will do | People pick the right task without reading the prompt |
| **Problems say which check failed, in which file, and what fixes it**, with the code (`S004`) last, for whoever reads the validator output | A code alone means nothing to most people |
| **Every intro ends with Learn more**, which opens the matching User Guide section in an in-app dialog, not a link out of Huddle | Detail lives in one place, and reading it does not leave the page |
| **Nothing essential lives only in a tooltip** | Tooltips are hard to reach on touch screens and easy to miss |

This is UI text, so it lives in the components, not in Settings › Prompts.
[ADR-0007](adr/0007-model-facing-text-is-configuration.md) covers only text a model reads.

Draft intros:

| Tab | Intro (draft) |
| --- | --- |
| **Skills** | A Skill is a folder of instructions a Teammate reads when its work calls for it. This list shows every Skill Huddle knows about: built in, fetched by your AI tool, or listed in a catalog but not on this computer yet. A Skill marked **Not downloaded** has no folder here yet; **Copy prompt** gives your AI tool what it needs to fetch it. Huddle never downloads Skills itself. Giving a Teammate a Skill copies it to them, so later changes reach them only when you apply an update. |
| **Packs** | A Pack is a named set of Skills for some roles, such as MSBuild Authoring for developers. Adding a Pack to a Teammate gives them all its Skills at once, and tells them when to use it. Packs come from SKILLS.md, a list your AI tool keeps. To add one, select **Add with your AI tool**, say where it is, and paste the prompt into Claude Code, Codex or a similar tool. Whatever passes Huddle's checks is ready to use; there is no approval step. |
| **Sources** | A Source is one place Skills come from: a GitHub repository, another Git server or a folder on this computer. Your AI tool keeps notes for each Source on how it found and fetched it, so the next update starts from what it learned. Everything here lives in the Skill Sources folder, which you can share with another Huddle. Removing a Source removes its rows and notes, but never the clone itself. Your AI tool reads the install file first; **Copy path** copies its location. |

## Discovering and adding Packs and Skills

This section is the whole experience, from noticing you need something to a Teammate using it.

### Where you start

You never need to know where `InstallSkillPack.md` is. Every place that needs your AI tool
gives you a ready prompt, and the prompt carries the path.

| Where you are | What you see | What it does |
| --- | --- | --- |
| Settings › Skills › Packs, while SKILLS.md lists no Packs | The empty state below | **Add with your AI tool** opens `G5` |
| The Pack bar (`G4b`) | **Add with your AI tool** | Opens `G5` on **Add from a link or folder** |
| Settings › Skills › Skills tab | **Import skill** | Opens `G5` on **Add from a link or folder** |
| The Pack bar | **Check for updates** | Opens `G5` on **Check for updates** |
| A Teammate's card, under its Pack chips | **Find more Packs for Developer** | Opens `G5` on **Find Packs for a role**, with the role filled in |
| A Teammate's card, the role form or a draft's Skills list | A Skill marked **Not downloaded**, with **Copy prompt** | Copies a prompt to fetch that one Skill |
| A draft card in Build a team | **Needs setup: Pack "MSBuild Authoring" not downloaded** and **Copy prompt** | Copies a prompt to fetch what the role needs |
| A **Not downloaded** or **Broken** card | **Copy fix prompt** | Copies a prompt that names the row and the failed checks |
| The Sources tab | **Copy path ▾** beside "Install file" | Copies the path of `InstallSkillPack.md` in the format you pick |
| The Library, root **Skill Sources** | SKILLS.md, the pack and catalog files, every Source's notes | Read only |

```text
┌ Packs ──────────────────────────────────────────────────────── G4d ┐
│ No Packs yet.                                                      │
│                                                                    │
│ Huddle does not download Skills; your AI tool does. It signs in    │
│ where it needs to, clones or downloads the Skills wherever it      │
│ likes, and adds a pointer to SKILLS.md. Huddle picks it up.        │
│                                                                    │
│ 1. Select Add with your AI tool and say where the Skills are.      │
│ 2. Paste the prompt into Claude Code, Codex or a similar tool.     │
│ 3. Come back here; what it added is ready to use.                  │
│                                                                    │
│ [ Add with your AI tool ]   [ Copy path ▾ ]           Learn more   │
└────────────────────────────────────────────────────────────────────┘
```

| Code (proposed) | Container | What it holds |
| --- | --- | --- |
| `G4d` | Packs empty state | Shown while SKILLS.md lists no Packs: a short explanation, three numbered steps, **Add with your AI tool**, **Copy path ▾** and **Learn more**, which opens this part of the User Guide in an in-app dialog |

### The prompt dialog

```text
┌ Add with your AI tool ───────────────────────────────────────── G5 ┐
│ What do you want to do?                                        G5a │
│ (•) Add from a link or folder                                      │
│ ( ) Find Packs for a role                                          │
│ ( ) Check for updates                                              │
│ ( ) Fix a problem                                                  │
│                                                                    │
│ Where are the Skills?                                              │
│ ┌────────────────────────────────────────────────────────────────┐ │
│ │ https://github.com/anthropics/skills                           │ │
│ └────────────────────────────────────────────────────────────────┘ │
│ What are they for? (optional)                                      │
│ ┌────────────────────────────────────────────────────────────────┐ │
│ │ Word and Excel files for my bookkeeper                         │ │
│ └────────────────────────────────────────────────────────────────┘ │
│                                                                    │
│ 1. Copy this prompt                                                │
│ ┌────────────────────────────────────────────────────────────────┐ │
│ │ Read E:\Huddle\App_Data\InstallSkillPack.md and follow it to   │ │
│ │ add Packs or Skills from https://github.com/anthropics/skills. │ │
│ │ What they are for: Word and Excel files for my bookkeeper.     │ │
│ └────────────────────────────────────────────────────────────────┘ │
│                                   [ Copy path ▾ ]  [ Copy prompt ] │
│                                                G5b                 │
│ 2. Paste it into Claude Code, Codex or another AI tool that can    │
│    read files and run commands on this computer.                   │
│ 3. Come back here. It appears on this tab as soon as it is ready.  │
│                                                          [ Close ] │
└────────────────────────────────────────────────────────────────────┘
```

| Code (proposed) | Container | What it holds |
| --- | --- | --- |
| `G5` | Prompt dialog | A form dialog (`O2`): a choice of four tasks, the fields that task needs, the built prompt in a read-only box, **Copy path ▾** and **Copy prompt**, and three numbered steps. It writes nothing |
| `G5a` | Task choice | **Add from a link or folder**, **Find Packs for a role**, **Check for updates**, **Fix a problem** |
| `G5b` | Copy path menu | **Windows path** (`E:\Huddle\App_Data\InstallSkillPack.md`), **WSL path** (`/mnt/e/Huddle/App_Data/InstallSkillPack.md`), **Forward slashes** (`E:/Huddle/App_Data/InstallSkillPack.md`). The prompt box follows the last choice, so a tool running in WSL or Git Bash gets a path it can open |

Each task fills the prompt from a template. The templates are model-facing text, so they live
in Settings › Prompts like every other prompt
([ADR-0007](adr/0007-model-facing-text-is-configuration.md)):

| Task | Fields | Prompt it builds |
| --- | --- | --- |
| **Add from a link or folder** | Where are the Skills? (required); What are they for? | `Read {{installFile}} and follow it to add Packs or Skills from {{where}}.` and, when given, `What they are for: {{purpose}}.` |
| **Find Packs for a role** | Role (a picker of your roles); What should it get better at? | `Read {{installFile}} and follow it to find Packs for the {{roleTitle}} role ({{roleId}}). {{purpose}} Suggest at most three, and ask me before fetching.` |
| **Check for updates** | Sources (all, or a checklist) | `Read {{installFile}} and follow it to check {{sources}} for updates.` |
| **Fix a problem** | A row with a problem (a picker) | `Read {{installFile}} and follow it to fix the row for {{localPath}} in SKILLS.md. Huddle reported: {{problems}}` |

### Walking through it

> **Example.** Emre wants the document Skills from `https://github.com/anthropics/skills` for
> his bookkeeper, and uses Claude Code.

1. On Settings › Skills › Packs, select **Add with your AI tool**.
2. Paste `https://github.com/anthropics/skills` into **Where are the Skills?** and type "Word and
   Excel files for my bookkeeper". The prompt box fills in.
3. Select **Copy prompt**, switch to Claude Code, paste, and send.
4. Claude Code reads `InstallSkillPack.md`, clones the repository to `E:\Clones\anthropics-skills`
   at commit `dbd4588`, and asks which roles the Pack is for. It writes the Pack file, runs
   `huddle-packs validate --strict` until it reports `Errors=0`, adds the row to SKILLS.md,
   and tells Emre the Pack is ready in Huddle.
5. Huddle notices the new row within a second. A snackbar says **Document skills is ready ·
   Show**, and the card wears **New**.
6. On his bookkeeper's card Emre selects the **Document skills** chip. The Teammate restarts
   with the four Skills.

If the AI tool stops halfway, nothing appears in Huddle until a row is in SKILLS.md. If a row
fails a check, its card shows **Broken** with **Copy fix prompt**.

### What your AI tool leaves behind

For the example above:

```text
E:\Clones\anthropics-skills\                   the clone; anywhere the AI tool chose
└── skills\docx\, xlsx\, pptx\, pdf\ …          Huddle only reads these

E:\Huddle\App_Data\SkillSources\
├── SKILLS.md                                   one new row in the Packs table
├── packs\document-skills.skillpack.json        the Pack, in Huddle's schema
└── sources\github.com\anthropics\skills\
    ├── source.json                             where it came from, the commit, when
    └── NOTES.md                                how it found and read the repository
```

`InstallSkillPack.md` suggests keeping pack and catalog files in the Skill Sources folder rather
than in the clone, because a clone is a poor place for files its repository does not own. It
is a suggestion: a pointer to a file anywhere works the same.

The skill pack file follows Huddle's schema. It lists every Skill in the Pack with its `path`,
absolute or relative to the file:

```json
{
  "schemaVersion": 1,
  "id": "document-skills",
  "version": "dbd4588",
  "title": "Document skills",
  "source": "github.com/anthropics/skills",
  "appliesTo": ["bookkeeper", "business-manager", "writer", "legal-drafter", "analyst"],
  "consultWhenAdds": "a Word, Excel, PowerPoint or PDF file has to be made, read or changed",
  "skills": [
    { "name": "docx", "path": "E:\\Clones\\anthropics-skills\\skills\\docx", "origin": "https://github.com/anthropics/skills/tree/dbd4588f9e1033efb41dad4bef2f7947c8993d44/skills/docx" },
    { "name": "xlsx", "path": "E:\\Clones\\anthropics-skills\\skills\\xlsx", "origin": "https://github.com/anthropics/skills/tree/dbd4588f9e1033efb41dad4bef2f7947c8993d44/skills/xlsx" },
    { "name": "pptx", "path": "E:\\Clones\\anthropics-skills\\skills\\pptx", "origin": "https://github.com/anthropics/skills/tree/dbd4588f9e1033efb41dad4bef2f7947c8993d44/skills/pptx" },
    { "name": "pdf",  "path": "E:\\Clones\\anthropics-skills\\skills\\pdf",  "origin": "https://github.com/anthropics/skills/tree/dbd4588f9e1033efb41dad4bef2f7947c8993d44/skills/pdf" }
  ]
}
```

A Skill in a Pack gives either its own `path`, as above, or a `catalog`:

```json
{ "name": "target-authoring", "catalog": "dotnet-skills" }
```

That names a Skill in a catalog listed in SKILLS.md, here `dotnet-skills.skillcatalog.json`,
and the Pack uses that entry's path and origin. A Skill that is already in a catalog should be
named this way, so the catalog and the Pack share one copy and one update.

### SKILLS.md

SKILLS.md has two tables, **Packs** and **Catalogs**, with **one row per file, never one per
Skill**. Huddle reads only those two tables; the rest of the file is for people.

```markdown
# Skills registry

> We do not recommend editing this file by hand. Your AI tool adds and updates its rows by
> following InstallSkillPack.md, and a mistake here makes a Pack or Skill show as Broken in
> Huddle.

Every Pack and Skill this Huddle knows about. Each row points at a file on this computer.

## Packs

| Local path | Origin URL | Last updated | Notes |
| --- | --- | --- | --- |
| E:\Repos\skills\packs\msbuild-authoring.skillpack.json | | 2026-10-17 | Moved out of Huddle into the skills repository |
| E:\Repos\skills\packs\code-review-and-quality.skillpack.json | | 2026-10-17 | Moved out of Huddle into the skills repository |
| packs/document-skills.skillpack.json | https://github.com/anthropics/skills | 2026-10-17 | Added by Claude Code at dbd4588 for the bookkeeper |

## Catalogs

| Local path | Origin URL | Last updated | Notes |
| --- | --- | --- | --- |
| catalogs/dotnet-skills.skillcatalog.json | https://github.com/dotnet/skills | 2026-10-17 | From SkillsCatalog.json; 106 Skills, 1 downloaded |
| catalogs/garrytan-gstack.skillcatalog.json | https://github.com/garrytan/gstack | 2026-10-17 | From SkillsCatalog.json; 61 Skills |
| catalogs/mattpocock-skills.skillcatalog.json | https://github.com/mattpocock/skills | 2026-10-17 | From SkillsCatalog.json; 37 Skills |
| catalogs/addyosmani-agent-skills.skillcatalog.json | https://github.com/addyosmani/agent-skills | 2026-10-17 | From SkillsCatalog.json; 25 Skills, 1 downloaded |
| catalogs/anthropics-skills.skillcatalog.json | https://github.com/anthropics/skills | 2026-10-17 | From SkillsCatalog.json; 20 Skills |
| catalogs/obra-superpowers.skillcatalog.json | https://github.com/obra/superpowers | 2026-10-17 | From SkillsCatalog.json; 14 Skills |
| E:\Clones\team-skills\huddle.skillcatalog.json | https://git.example.com/team/skills | 2026-10-17 | Our team's own Skills, cloned by Codex |
```

| Column | Required | What it holds |
| --- | --- | --- |
| **Local path** | Yes | The pointer. In the Packs table, a `.skillpack.json`; in the Catalogs table, a `.skillcatalog.json`. Absolute, relative to the folder SKILLS.md is in, or [one per computer](#one-path-per-computer). Two rows that lead to the same file on this computer are a problem, and Huddle uses the first |
| **Origin URL** | No | Where it came from, for people and for the next update. Huddle never opens it |
| **Last updated** | No | The date your AI tool last wrote the row, as `yyyy-MM-dd`. Huddle shows it on the card; it never decides anything by it |
| **Notes** | No | One short line for people. Longer notes belong in the Source's `NOTES.md` |

A Pack's or catalog's title, version, roles and Skills come from its file, not from the table,
so a row stays a pointer and cannot disagree with the file. Huddle reads the columns by their
header names, so their order does not matter, and it ignores any extra column.

### One path per computer

A path is a path on one computer. Wherever a path goes, in a SKILLS.md row's **Local path** or
a Skill's `path` in a pack or catalog file, it can hold one entry per computer instead of one
for all:

```markdown
| Local path | Origin URL | Last updated | Notes |
| --- | --- | --- | --- |
| DESK-01: E:\Clones\team-skills\huddle.skillcatalog.json<br>LAPTOP: C:\src\team-skills\huddle.skillcatalog.json | https://git.example.com/team/skills | 2026-10-17 | Our team's own Skills |
```

```json
"path": { "DESK-01": "E:\\Clones\\anthropics-skills\\skills\\docx", "LAPTOP": "C:\\src\\anthropics-skills\\skills\\docx" }
```

- **The computer's name** is its Windows computer name. The Sources tab shows it ("This
  computer: DESK-01"), and `InstallSkillPack.md` carries it in its front matter.
- **Huddle uses this computer's entry.** A plain path is for every computer; a relative path
  is relative to the file it is in, so it works on every computer that shares the folder.
- **No entry for this computer** makes the row or the Skill **Not downloaded** here, and only
  here. **Copy fix prompt** asks this computer's AI tool to fetch it and add its own entry.
- **Your AI tool adds or updates only its own computer's entry** and leaves the others alone.

### Adding a single Skill

A Skill you want on its own goes the same way: **Import skill**, or **Copy prompt** beside a
**Not downloaded** Skill. Your AI tool fetches it and adds it to a **skill catalog file**: the
catalog for its Source if there is one, otherwise a new one with its own Catalogs row. A
catalog lists any number of Skills, each with its `path`:

```json
{
  "schemaVersion": 1,
  "id": "addyosmani-agent-skills",
  "title": "Addy Osmani's agent skills",
  "source": "github.com/addyosmani/agent-skills",
  "skills": [
    {
      "name": "api-and-interface-design",
      "description": "Guides stable API and interface design. Use when designing APIs, module boundaries, or any public interface.",
      "publisher": "Addy Osmani",
      "origin": "https://github.com/addyosmani/agent-skills/tree/1401c8b8030e023baeebb31781a6653fe8e93026/skills/api-and-interface-design",
      "license": "MIT",
      "category": "Architecture & Design",
      "subCategory": "API & Interface Design",
      "requires": [],
      "path": "E:\\Clones\\agent-skills\\skills\\api-and-interface-design"
    },
    {
      "name": "code-review-and-quality",
      "description": "…",
      "publisher": "Addy Osmani",
      "origin": "https://github.com/addyosmani/agent-skills/tree/1401c8b8030e023baeebb31781a6653fe8e93026/skills/code-review-and-quality",
      "license": "MIT",
      "category": "…",
      "subCategory": "…",
      "requires": [],
      "path": ""
    }
  ]
}
```

| Field | What it holds |
| --- | --- |
| `id`, `title`, `source` | The catalog's id, which is also its file name (`<id>.skillcatalog.json`); a title for people; and the Source it describes |
| `skills[]` | One entry per Skill: `name`, `description`, `publisher`, `origin` pinned to a commit, `license`, `category`, `subCategory` and `requires`, the same fields as today's catalog |
| `path` | Where the Skill's folder is, absolute or relative to the catalog file. Empty until someone fetches it |

A Skill whose `path` is empty, or leads nowhere, is one Huddle knows about but nobody has
fetched: it shows in every Skills list as **Not downloaded**, with **Copy prompt**. That is how
the shipped catalog appears after [the move](#plan-moving-todays-packs-and-skills). Fetching a
Skill means the AI tool fills in its `path`; the catalog's row in SKILLS.md stays as it is.

Once its `path` leads to a folder, the Skill can be picked in a Teammate's Skills list, the
role form and a draft's Skills list. `category` and `subCategory` feed the same overlap warning
a Teammate card shows today.
### Pack details

```text
┌ Document skills ─────────────────────────────────────────────── G6 ┐
│ anthropics/skills @ dbd4588 · updated 2026-10-17                   │
│ Notes: Added by Claude Code at dbd4588 for the bookkeeper          │
│ For: bookkeeper, business-manager, writer, legal-drafter, analyst  │
│ Consult when: "a Word, Excel, PowerPoint or PDF file has to be     │
│ made, read or changed"                                             │
│ ┌──────────┬──────────────┬───────┬─────────────────────────────┐  │
│ │ Skill    │ Licence      │ Files │ Warnings                    │  │
│ ├──────────┼──────────────┼───────┼─────────────────────────────┤  │
│ │ docx     │ Proprietary  │ 14    │ Scripts, unread files       │  │
│ │ xlsx     │ Proprietary  │ 9     │ Scripts, unread files       │  │
│ │ pptx     │ Proprietary  │ 22    │ Scripts, unread files       │  │
│ │ pdf      │ Proprietary  │ 11    │ Scripts, unread files       │  │
│ └──────────┴──────────────┴───────┴─────────────────────────────┘  │
│ Notes from your AI tool                             [ Show notes ] │
│                                                                    │
│                       [ Remove ]   [ Show in Library ]   [ Close ] │
└────────────────────────────────────────────────────────────────────┘
```

| Code (proposed) | Container | What it holds |
| --- | --- | --- |
| `G6` | Details card | Opened from a card's **⋯** › **Details**. The Source and commit, the SKILLS.md row's Last updated and Notes, the roles, the `consult_when` phrase, one row per Skill with licence, file count and the validate warnings, and the Source notes. **Show in Library** opens the Pack file; **Remove** is as in [Removing](#removing-a-pack-a-skill-or-a-source) |

**Unread files** means the Skill has files a Teammate never sees: Teammates read only the
`.md` files directly in a Skill's folder, up to 64 KB each, so `scripts/` and `references/` are
ignored. A Skill that depends on them may work less well in Huddle than in the tool it was
written for.

## Choosing a Pack or Skill for a Teammate

1. On a Teammate's card, or on a draft in Build a team, select a Pack's chip, or pick a Skill
   in the Skills list. Only what passes the checks and is downloaded can be chosen.
2. Huddle copies each Skill folder from where its `path` leads into `Skills/`, with a
   `.source.json` naming the Source, origin, commit and the hash of the copy. This is a copy on
   your disk, not a download.
3. The Teammate's `skills:`, `consult_when:` and `_packs:` lines change as they do today, and
   the Teammate restarts.

A Teammate uses the copy in `Skills/`, never the clone, so your AI tool can pull a new version
without changing any Teammate until you apply it.

**Build a team** works the same way. A role that needs a Pack or Skill that is not downloaded
shows **Needs setup** on its draft card with **Copy prompt**, and **Approve & hire** waits
until it is downloaded.

## Keeping Packs and Skills up to date

Huddle cannot look for updates itself, because that would mean going online. Your AI tool
does it when you ask.

1. Select **Check for updates**, pick the Sources, and copy the prompt.
2. Paste it into your AI tool. For each Source that moved on, it pulls the new commit, rewrites
   the pack and catalog files and `source.json`, updates the rows' Last updated, and runs the
   validate command.
3. Huddle compares the files at each pointer with the copies its Teammates use. Where they
   differ, the card and every Teammate card that holds it show **Update available**.

```text
┌ Update: MSBuild Authoring  1.0.0 → 1.1.0 ────────────────────── G7 ┐
│ repos/skills · updated 2026-10-17                                  │
│                                                                    │
│ Skill files that change for everyone                               │
│   ~ target-authoring   a660de8 → 4c1e9a2   3 files changed         │
│     Used by Jane and Omar. Their copy changes; no restart.         │
│                                                                    │
│ Skills added or removed                                            │
│   + incremental-build      dotnet/skills @ 4c1e9a2   MIT           │
│                                                                    │
│ Consult when adds                                                  │
│   ".csproj, .props or .targets changes, build failures"            │
│   → "… build failures, slow incremental builds"                    │
│                                                                    │
│ Take the new list (restarts each one ticked)                       │
│   [x] Jane   Developer                                             │
│   [x] Omar   Release engineer                                      │
│                                                                    │
│                                         [ Not now ]   [ Update 2 ] │
└────────────────────────────────────────────────────────────────────┘
```

| Code (proposed) | Container | What it holds |
| --- | --- | --- |
| `G7` | Update card | The version change; **Skill files that change for everyone** (`~`, with how many files and who uses it), shared by every holder and with no checkbox; Skills added (`+`) or removed (`−`); the `consult_when` change; and a checkbox per Teammate for the **membership** change only. Huddle works it out on disk ([the specification](Huddle.SkillPacks-Specifications.md) §8.4) |

- **Update N** first copies the changed Skill files into `Skills/` for every holder, which needs no
  restart, then rewrites each ticked Teammate's `skills:`, `consult_when:` and `_packs:` lines and
  restarts those Teammates. With nobody ticked the button reads **Update files**.
- **Not now** changes nothing. Unchecked Teammates keep their copies and keep showing
  **Update available**.

> [!TIP]
> Most AI tools can run a prompt on a schedule. Save the update prompt as a scheduled task, and
> updates are waiting in Huddle without you having to ask.

## Deprecated, withdrawn and moved Sources

Your AI tool records what it learns about a Source in `source.json`, for example from a README
that says the repository is deprecated. Huddle never strips a Pack or Skill from your Teammates
on its own; it tells you and lets you choose.

| `source.json` says | What you see | What Huddle does |
| --- | --- | --- |
| **Deprecated**, with a reason | A grey chip on the card and on Teammate cards, with the reason on hover | Nothing else. You can still choose it |
| **Withdrawn**, with a reason | A red chip and a warning on every Teammate card that holds it | New hires cannot choose it. **Remove from Teammates** is one click on the card |
| **Moved** to another place | "This Source moved to openai/plugins" on the Source row | Offers **Copy prompt** to add the new Source. Teammates keep their copies until you update them |

> **Example.** After a **Check for updates**, Emre's `openai/skills` Source shows "Deprecated:
> moved to openai/plugins (from the repository's README, seen 2026-10-17)". He copies the
> prompt into Codex, which adds two Packs from `openai/plugins`, and he updates the Teammates
> who held the old ones.

## Removing a Pack, a Skill or a Source

Huddle deletes only what lives in the Skill Sources folder. It never deletes a clone, wherever
it is: delete it yourself, or ask your AI tool.

| You remove | From | What happens |
| --- | --- | --- |
| A Pack | Its card's **⋯** › **Remove** | Removes its row from SKILLS.md, and its `.skillpack.json` if that is in the Skill Sources folder. If Teammates hold it, Huddle asks first: **Keep their Skills** leaves their `skills:` lists and copies alone and only drops `_packs`; **Remove from Teammates too** also takes off the Skills that no other Pack or role gave them |
| A catalog | Sources tab › the catalog › **Remove** | Removes its row from SKILLS.md, and its `.skillcatalog.json` if that is in the Skill Sources folder. Teammates keep their copies of its Skills |
| One Skill from a catalog | Its row in a Skills list › **Hide** | Huddle never edits a catalog file, so it hides the Skill instead: it stops showing in Skills lists. **Show hidden** brings it back. Ask your AI tool to take it out of the catalog for good |
| A Source | Sources tab › its row › **Remove** | Asks **Remove Source?** first. Deletes `sources/<source>/` (its `NOTES.md`, `source.json` and `log.jsonl`), every row whose file names that Source, and those files if they are in the Skill Sources folder. Teammates keep their copies in `Skills/`, whose provenance line then shows **Source removed** |

> [!CAUTION]
> Removing a Source deletes its notes for good. If the Skill Sources folder is shared, they
> are deleted for every Huddle that uses that folder.

## The Skill Sources folder and Source notes

```text
App_Data\
├── InstallSkillPack.md                 written by this Huddle; never shared
├── Skills\                             the copies Teammates use, as today
└── SkillSources\                       can be shared; a read-only Library root
    ├── SKILLS.md                       the registry of pointers
    ├── packs\                          *.skillpack.json, if the AI tool keeps them here
    ├── catalogs\                       *.skillcatalog.json; the six from the shipped catalog are here
    └── sources\
        └── github.com\anthropics\skills\
            ├── source.json             written by your AI tool: URL, commit, fetched at, status
            ├── NOTES.md                written by your AI tool: how it found it, layout, how to update
            └── log.jsonl               written by Huddle, append-only: what it noticed and did
```

`source.json` is short and fixed; `InstallSkillPack.md` gives its schema:

```json
{
  "schemaVersion": 1,
  "kind": "github",
  "url": "https://github.com/anthropics/skills",
  "ref": "main",
  "commit": "dbd4588f9e1033efb41dad4bef2f7947c8993d44",
  "localPath": "E:\\Clones\\anthropics-skills",
  "fetchedAt": "2026-10-17T16:40:00+02:00",
  "fetchedBy": "Claude Code",
  "signIn": "none",
  "status": "active"
}
```

`kind` is one of `github`, `git` or `local`. `localPath` is where
the AI tool put the clone, for its own next visit; Huddle does not use it. `signIn` is `none`
or `required`, never a credential. `status` is `active`, `deprecated`, `withdrawn` or `moved`;
the last three need `statusReason`, and `moved` needs `movedTo`.

`NOTES.md` is for the next AI tool that visits, and for you. `InstallSkillPack.md` asks your AI
tool to read it before it touches the Source again, and to keep it under 16 KB.

```markdown
---
layout: claude-marketplace
manifest: .claude-plugin/marketplace.json
packsFrom: plugins[].skills
---
# anthropics/skills

**Found:** Emre pasted the URL on 2026-10-17 while setting up Packs for the bookkeeper.
Cloned by Claude Code to E:\Clones\anthropics-skills; public, no sign-in needed.

**Layout:** a Claude Code marketplace. Each plugin is an explicit Skill list, so each one maps to
a Pack. Plugins carry no version of their own; use the short commit.

**Quirks**
- The repository has no licence. Each Skill has a LICENSE.txt and a `license:` line;
  docx, xlsx, pptx and pdf are Proprietary.
- The document Skills ship `scripts/`, which Teammates never read.

**To update:** pull, reread the manifest, compare each plugin's `skills[]` with our Packs, and
rewrite only the Packs whose folders changed.
```

### Reading it in the Library

The Skill Sources folder is a **read-only Library root** named **Skill Sources**. Open the
Library, pick that root, and you see SKILLS.md first, then the pack and catalog files and every
Source's notes, with search and the outline. You cannot edit anything there; ask your AI tool.

A clone outside the Skill Sources folder is not in that root, because the Library shows only
the folders it is given ([ADR-0027](adr/0027-the-library-sees-only-configured-roots.md)). To
browse one, pin its folder on Settings › Library.

## Sharing with another Huddle

Point the Skill Sources folder at a synced folder or a Git checkout on Settings › Skills ›
Sources › **Folder**, and every Huddle that uses it sees the same SKILLS.md, pack and catalog
files, and notes.

| Shared, in the Skill Sources folder | Not shared, stays in each Huddle |
| --- | --- |
| SKILLS.md, the pack and catalog files kept there, each Source's `source.json`, `NOTES.md` and `log.jsonl` | `InstallSkillPack.md` and its appendix |
| Hidden Packs, deprecation and withdrawal states | The copies in `Skills/`, and which Teammates hold what |

A path is a path on one computer. A relative path into the shared folder works everywhere.
For a clone elsewhere, each computer adds its own entry to the path, as in
[One path per computer](#one-path-per-computer). On a computer with no entry, the row shows
**Not downloaded**, and **Copy fix prompt** asks that computer's AI tool to fetch it and add
its entry.

> [!NOTE]
> A Skill that someone copied into the shared folder is readable by everyone who can read it.
> Check its licence before you share it, especially one from a private repository.

Two Huddles writing the same file at once follow the Library's rule: the last write wins
([ADR-0029](adr/0029-between-the-human-and-an-agent-the-last-write-wins.md)). `log.jsonl` names
the Huddle that wrote each line.

## What changes on disk

| Action | Who | Files touched | Effect on Teammates |
| --- | --- | --- | --- |
| Huddle starts, or your roles, Skills or SKILLS.md change | Huddle | `App_Data/InstallSkillPack.md` | None |
| First start of this version | Huddle | The Skill Sources folder, with SKILLS.md (including rows pointing at the two Packs in the skills repository), six catalog files and your imported Skills | None; see [the move](#plan-moving-todays-packs-and-skills) |
| Add or update a Pack or Skill | Your AI tool | Its clone, wherever it is; its pack or catalog file; `sources/<source>/`; one row in SKILLS.md | None until you choose it or apply the update |
| Choose a Pack or Skill on a Teammate | Huddle | `Skills/<name>/` copied from the pointer, with `.source.json`; the Teammate's `<Name>.md` | Restarts |
| Apply an update | Huddle | `Skills/<name>/` for new or changed Skills; each checked Teammate's `<Name>.md` | Each checked Teammate restarts |
| Remove a Pack or a catalog | Huddle | The SKILLS.md row, and its file if it is in the Skill Sources folder; optionally each holder's `<Name>.md` | Holders restart if their files changed |
| Remove a Source | Huddle | `sources/<source>/`, the rows and files that name it, inside the Skill Sources folder | None; copies in `Skills/` stay; clones stay |

## Plan: the InstallSkillPack.md file

`InstallSkillPack.md` is the only thing an AI tool needs to read to work for this Huddle. It is
a contract: everything the validate command checks is written down in it first.

### Where it is and when it is written

| Property | Plan |
| --- | --- |
| **Path** | `App_Data/InstallSkillPack.md`, beside the Skill Sources folder, never inside it, because its appendix belongs to one Huddle and that folder can be shared |
| **Written** | At start-up, and again, at most once a second, when your roles, installed Skills or SKILLS.md change. Written to a temporary file and moved into place, so an AI tool never reads half a file |
| **Who may change it** | The fixed sections come from Settings › Prompts (key `installSkillPack.body`), so you can reword them and **Reset all** restores them. The front matter and the appendix are generated; a hand edit is overwritten on the next write |
| **Shown** | Its path on the Sources tab with **Copy path ▾**, inside every prompt `G5` builds, and in `huddle-packs --help` |
| **Size** | The fixed sections stay under 12 KB. The appendix lists up to 200 roles and 300 installed Skills, and says how many more there are; the AI tool asks `huddle-packs roles`, `installed` or `skills` for the rest |

### What it holds

It opens with front matter an AI tool can read without parsing prose:

```yaml
---
huddle: 2.4.0
schemaVersion: 1
dataDir: E:\Huddle\App_Data
skillSourcesDir: E:\Huddle\App_Data\SkillSources
registry: E:\Huddle\App_Data\SkillSources\SKILLS.md
validate: '"C:\Program Files\Huddle\huddle-packs.exe" validate --data-dir "E:\Huddle\App_Data"'
computer: DESK-01
written: 2026-10-17T16:25:00+02:00
---
```

Then fixed sections, in this order, under these headings, so a prompt or a person can point at
one by name:

| # | Heading | What it tells the AI tool |
| --- | --- | --- |
| 1 | **What you are doing** | You add or update Skills and Packs for one Huddle. Huddle does not download; you do. Finish by telling the Human what is ready in Huddle |
| 2 | **Rules** | Clone or download wherever suits you, and record where. Write Huddle's files only in the Skill Sources folder or beside your clone, never in `Skills/`, Teammate files or settings. Never run, build or install anything a Skill contains. Never store a token, password or cookie in any file. Ask the Human before fetching more than 20 Skills or 50 MB, and before using a Source that needs sign-in |
| 3 | **Where things go** | Suggested places: `*.skillpack.json` in `packs/`, `*.skillcatalog.json` in `catalogs/`, notes in `sources/<host>/<owner>/<repo>/`. Add a single Skill to its Source's catalog rather than starting a new one. Never touch `log.jsonl` |
| 4 | **The skill pack and skill catalog files** | Every field of both schemas, with the examples on this page; that every Skill in either carries a `path`, empty until fetched; how managers use `appliesTo` and `consultWhenAdds`; `id` is lowercase with hyphens, matches the file name, and is unique across SKILLS.md |
| 5 | **The SKILLS.md tables** | The Packs and Catalogs tables, one row per file and never one per Skill, their four columns, that **Local path** is the only required one and may be absolute, relative to SKILLS.md, or one entry per computer; to add or update only this computer's entry (from the front matter's `computer`); the date format; and to touch only your own rows. Name a catalog's Skill in a Pack with `catalog` rather than copying its path |
| 6 | **source.json and NOTES.md** | Both schemas; read `NOTES.md` first on every visit; keep it under 16 KB; record a move or a deprecation in `source.json` with a reason |
| 7 | **Reading a repository** | Known layouts: a Claude Code `marketplace.json` (a plugin with `skills[]` is a Pack); a skills folder with tiers such as OpenAI's `.system` and `.curated`; a single `SKILL.md`; a list of links. For anything else, describe it in `NOTES.md` and ask the Human |
| 8 | **What Huddle can use** | Teammates read only the `.md` files directly in a Skill's folder, up to 64 KB each; a Skill's `tools:` line cannot grant App Tools; licences are per Skill. Say so to the Human when it matters |
| 9 | **Tasks** | One subsection per task in `G5`: Add, Find for a role, Check for updates, Fix. Each is a numbered list of steps ending in the validate command |
| 10 | **Checking your work** | The validate command line from the front matter, with `--strict` for what you just added; what each check code means and how to fix it; repeat until `Errors=0` |
| 11 | **Telling the Human** | One short message: what is ready, what its details card will flag, and anything you could not do |

And a generated appendix:

| Appendix | What it lists |
| --- | --- |
| **A. Roles** | Every role a Pack's `appliesTo` may name: id, title and its one-line description, from the hiring pool, your own roles (`custom-roles.json`) and installed team types |
| **B. Installed Skills** | Every Skill in `Skills/`, built-in or not, with its origin. Not as examples: so the AI tool reuses a Skill this Huddle already has instead of fetching it again, and does not pick a name that clashes with one |
| **C. SKILLS.md today** | Every row with its state in this Huddle, and every Source with its `status` |
| **D. Where to look** | Every catalog in SKILLS.md with how many Skills it lists and how many are downloaded, the six shipped catalogs included. A catalog Skill that is not downloaded is the cheapest thing to offer, because its origin is already pinned. Used by **Find Packs for a role**, with `huddle-packs skills <text>` to search |

## Plan: the validate command

`huddle-packs` is a standalone command-line program that ships with Huddle. Its `validate`
command checks Packs and Skills alike, with the same code Huddle runs whenever SKILLS.md or a
file it points at changes, so what passes in your AI tool works in Huddle. If shipping it as a
mode of the Huddle executable proves simpler, that mode takes the same commands and prints the
same output.

### How it behaves

- **Read only and offline.** It never writes a file and never opens a network connection.
- **Runs without Huddle,** and while Huddle is running on the same folder.
- **One line per finding,** then one summary line that is always last, so an AI tool can read
  the result without parsing prose.

| Command | What it does |
| --- | --- |
| `huddle-packs validate [<path>]` | With no path, checks all of SKILLS.md and everything its rows point at. With a path, checks only the rows that point at or under it |
| `huddle-packs roles` | Prints every role id a Pack may name, one per line, with its title |
| `huddle-packs installed` | Prints every Skill in `Skills/` with its origin and hash |
| `huddle-packs skills [<text>]` | Prints every Skill in every catalog and Pack, or those whose name, description or category contains the text, with whether it is downloaded |
| `huddle-packs schema pack` / `catalog` / `source` | Prints the JSON Schema of a skill pack file, a skill catalog file or `source.json` |
| `huddle-packs --version` | Prints its version and the `schemaVersion` it checks |

| Option | Default | Effect |
| --- | --- | --- |
| `--data-dir <path>` | The `HUDDLE_DATA_DIR` environment variable, else the default `App_Data` | Where to find SKILLS.md, `Skills/` and the roles |
| `--json` | off | Prints one JSON object instead of lines |
| `--quiet` | off | Prints errors and the summary only |
| `--strict` | off | Reports `S001` (a Skill folder not downloaded) as an error. `InstallSkillPack.md` tells your AI tool to use it on what it just added |

| Exit code | Meaning |
| --- | --- |
| `0` | No errors. Warnings may be present |
| `1` | One or more errors |
| `2` | It could not run: a bad option, or a data folder it cannot read |

### What it prints

```text
huddle-packs validate --strict --data-dir "E:\Huddle\App_Data" "E:\Huddle\App_Data\SkillSources\packs\document-skills.skillpack.json"
```

```text
ERROR  P005  packs/document-skills.skillpack.json     appliesTo: "accountant" is not a role in this Huddle; see huddle-packs roles
ERROR  S001  E:\Clones\anthropics-skills\skills\pdf   path leads to no folder
WARN   S008  E:\Clones\anthropics-skills\skills\docx  scripts/ and 9 other files are never read by Teammates
WARN   S009  E:\Clones\anthropics-skills\skills\docx  licence is Proprietary (LICENSE.txt)
WARN   S010  E:\Clones\anthropics-skills\skills\docx  ships scripts: scripts/office/unpack.py and 3 more
OK           SKILLS.md                                Packs row 3 points at packs/document-skills.skillpack.json
PACKS-VALIDATE Rows=1 Packs=1 Skills=4 Errors=2 Warnings=11
```

With `--json`:

```json
{
  "summary": { "rows": 1, "packs": 1, "skills": 4, "errors": 2, "warnings": 11 },
  "findings": [
    { "severity": "error", "code": "P005", "file": "packs/document-skills.skillpack.json",
      "message": "appliesTo: \"accountant\" is not a role in this Huddle; see huddle-packs roles" }
  ]
}
```

### What it checks

An error makes the row **Broken** in Huddle, except `S001`, which makes it **Not downloaded**.
Warnings appear on the details card. Codes never change meaning; a retired code is not reused.

| Code | Severity | Check |
| --- | --- | --- |
| `R001` | Error | SKILLS.md exists and has a `Packs` table, a `Catalogs` table, or both, each under its own heading |
| `R002` | Error | Each table has a `Local path` column |
| `R003` | Error | Each local path, absolute or relative to SKILLS.md's folder, leads to something that exists |
| `R004` | Error | No two rows have the same local path (Huddle uses the first) |
| `R005` | Error | A Packs row points at a `*.skillpack.json` and a Catalogs row at a `*.skillcatalog.json` |
| `R006` | Warning | `Last updated` is a `yyyy-MM-dd` date |
| `R007` | Warning | `Origin URL` is an absolute `https`, `ssh` or `git` URL |
| `R008` | Warning | A role in your hiring pool suggests a Pack id that no row provides |
| `R009` | Warning | A path has no entry for this computer and no plain path, so it is **Not downloaded** here |
| `R010` | Error | A per-computer path names the same computer twice |
| `P001` | Error | The Pack file is JSON with a `schemaVersion` this Huddle knows |
| `P002` | Error | `id`, `version`, `title`, `source` and `skills` are present |
| `P003` | Error | `id` is lowercase letters, digits and single hyphens, and the file is `<id>.skillpack.json` |
| `P004` | Error | No other Pack in SKILLS.md has the same `id` |
| `P005` | Error | Every `appliesTo` entry is a role in this Huddle |
| `P006` | Warning | `appliesTo` is empty, so no Teammate card will offer the Pack |
| `P007` | Warning | `consultWhenAdds` is empty or longer than 200 characters |
| `P008` | Error | No Skill name appears twice in one Pack |
| `P009` | Error | A Skill given by `catalog` names a catalog in SKILLS.md that lists a Skill of that name, and gives no `path` of its own |
| `D001` | Error | A catalog file is JSON with a known `schemaVersion`, an `id` matching its file name `<id>.skillcatalog.json`, a `source` and a `skills` list |
| `D002` | Error | Every catalog entry has `name`, `description` and `origin`; its `name` follows the Skill name rule and appears only once in the catalog |
| `D003` | Error | No two catalogs or Packs give the same Skill name different origins |
| `D004` | Warning | `origin` points at a commit, not a branch, so the Skill is pinned |
| `S001` | Warning | A Skill's `path` is filled in and leads to a folder. Shown as **Not downloaded** otherwise; an error with `--strict` |
| `S002` | Error | The folder has a `SKILL.md` whose `name` follows the Skill name rule and equals the folder name, with a `description` of at most 500 characters: the same rules as today's Skills |
| `S003` | Error | At most 200 files and 5 MB per Skill folder |
| `S004` | Error | No symbolic link or junction inside the folder, no name Windows cannot store, and no two names that differ only in case |
| `S005` | Error | The name is not one of Huddle's built-in Skills |
| `S006` | Error | The name is not taken in `Skills/` by a Skill from a different origin |
| `S007` | Warning | A `.md` file over 64 KB, which Teammates skip |
| `S008` | Warning | Files in sub-folders or not ending in `.md`, which Teammates never read |
| `S009` | Warning | The licence: none found, proprietary, or not the same as the Source's |
| `S010` | Warning | Scripts, shell commands, network calls or paths outside the Work Dir, the same flags as today's Skill review |
| `S011` | Error | `SKILL.md`'s `tools:` names an App Tool such as `propose_sprint`. Only Huddle's built-in Skills grant App Tools; a Pack or a fetched Skill never can |
| `O001` | Error | The Source named by a pack or catalog file has a `sources/<source>/source.json` that matches its schema, with a known `kind` |
| `O002` | Warning | A `github` or `git` Source has no `commit`, so it is not pinned |
| `O003` | Error | A `deprecated`, `withdrawn` or `moved` status has its reason, and `moved` has `movedTo` |
| `O004` | Warning | `NOTES.md` is missing or over 16 KB |
| `O005` | Warning | `NOTES.md` has no front matter naming its `layout` |
| `X001` | Error | No pack file, catalog file, note or Skill folder holds what looks like a secret: a token prefix such as `ghp_` or `github_pat_`, a private key header, or a `password=` line |

## Plan: moving today's Packs and Skills

The move happens once, the first time a Huddle with this feature starts. It writes rows and
files exactly as an AI tool following `InstallSkillPack.md` would have, so from then on there is
one way in. No Teammate restarts, no Teammate file changes, and nothing is downloaded.

### What exists today

| Where | What | Who reads it |
| --- | --- | --- |
| `TeamTypes/Builtin/SkillPacks/msbuild-authoring.json` (embedded) | MSBuild Authoring 1.0.0: `target-authoring` from `dotnet/skills`, path `plugins/dotnet-msbuild/skills/target-authoring`; for developer and release-engineer | `SkillPackStore` |
| `TeamTypes/Builtin/SkillPacks/code-review-and-quality.json` (embedded) | Code Review and Quality 1.0.0: `code-review-and-quality` from `addyosmani/agent-skills`, path `skills/code-review-and-quality`; for reviewer | `SkillPackStore` |
| `App_Data/SkillPacks/*.json` | Your own Packs; one with a built-in's id replaces it | `SkillPackStore` |
| `Skills/SkillsCatalog.json` (embedded) | 263 Skills from six repositories (this page first said 265; `retro` and `test-driven-development` are each in two repositories and are kept once), each pinned to a commit, with licence, category and sub-category: dotnet/skills (106), garrytan/gstack (61), mattpocock/skills (37), addyosmani/agent-skills (25), anthropics/skills (20) and obra/superpowers (14) | `SkillsCatalogIndex`, used by Import skill, the role form, Build a team, the Teammate card and `ImportingSkillInstaller` |
| `hiringpool.json` | `developer` and `release-engineer` suggest `msbuild-authoring`; `reviewer` suggests `code-review-and-quality` | `CardResolver` |
| Each Teammate's `<Name>.md` | `_packs: ['msbuild-authoring@1.0.0']` and the Skills the Pack added | `PersonaFrontmatter`, `TeammateCard` |
| `App_Data/Skills/<name>/.source.json` | Skills already imported, from the catalog or a GitHub link, with repository, path, commit and hash | `SkillStore` |

The old Pack schema names a Skill by `name`, `source` (`owner/repo`) and `path`, with no
commit. The catalog already pins both Pack Skills: `target-authoring` at `a660de8` and
`code-review-and-quality` at `1401c8b`, both MIT.

### What the move does

1. **Detect.** If `SkillSources/SKILLS.md` does not exist, the move runs, before any Teammate
   starts. If it exists, the move is skipped, so running it twice is safe.
2. **Split the catalog.** For each of the six repositories, write
   `sources/github.com/<owner>/<repo>/source.json` (`kind: github`, `fetchedBy: "Huddle
   catalog"`, no single `commit`, because entries from one repository can be pinned to
   different commits), a `NOTES.md` saying the Source came from the catalog shipped with this
   Huddle version, and one catalog file, such as `catalogs/dotnet-skills.skillcatalog.json`,
   holding every entry from that repository with its fields, its tree URL at the pinned commit
   as `origin`, and an empty `path`. That is six files and six Catalogs rows, noted "From
   SkillsCatalog.json"; all 263 Skills show as **Not downloaded** until someone asks.

3. **Point at the skills repository.** The two Packs Huddle shipped no longer live in Huddle.
   They are kept as `.skillpack.json` files in a skills repository, at `E:\Repos\skills` on this
   computer, with its notes in `sources/local/skills/` (the Source `local/skills`). Each of their Skills names its catalog entry
   instead of giving a path, such as `{ "name": "target-authoring", "catalog": "dotnet-skills" }`,
   so the Pack and the catalog share one copy. The move adds a Packs row for each, with this
   computer's entry pointing into the folder the configuration key `Team:SkillsRepositoryDir` names,
   read once, by the move (`E:\Repos\skills` here); the Sources tab only shows it. On a computer where that key is not set or its folder does not hold them,
   the rows are **Not downloaded**, with **Copy prompt** to clone the repository.
4. **Bring your own Packs.** Each valid file in `App_Data/SkillPacks/` becomes a `.skillpack.json` in
   `packs/`, in the Source `local/my-packs`, under `sources/local/my-packs/`. A file whose id matched a shipped Pack
   replaced it before, so the move keeps yours and hides the shipped one. A file that fails to
   load today is listed in the move's report and not moved. `App_Data/SkillPacks/` is left as
   it is, so an older Huddle still finds it.
5. **Point at what is already on disk.** Each folder in `Skills/` with a valid `.source.json`
   was downloaded by today's importer. Huddle copies it, without `.source.json`, to
   `SkillSources/fetched/<name>/` and points the matching catalog entry's `path`, and any Pack Skill's `path`, at that copy. A
   Skill from outside the catalog is added to a new catalog file for its repository, with its
   own Source. Built-in Skills, and Skills you wrote yourself, have no `.source.json` and stay
   where they are, out of SKILLS.md.
6. **Write SKILLS.md,** with the "We do not recommend editing this file by hand" note at the
   top, a Packs row per moved Pack ("Shipped with Huddle; moved from the old format" or "Your
   own Pack; moved from SkillPacks/") and a Catalogs row per catalog file.
7. **Leave Teammates alone.** The files in `fetched/` are the ones Teammates already use, so no
   card shows **Update available**. `_packs` keeps its ids and versions
   (`msbuild-authoring@1.0.0` still matches `1.0.0`) and `skills:` keeps its names. No
   `<Name>.md` is rewritten and nobody restarts. A Teammate whose `_packs` names a Pack the move
   could not bring over shows a warning on its card.
8. **Keep the hiring pool working.** Roles keep suggesting Packs and Skills by name.
   `CardResolver` now looks them up in SKILLS.md; one that is not downloaded makes the draft
   card **Needs setup** with **Copy prompt**.
9. **Report.** Append each step to the Sources' `log.jsonl` files, and show one snackbar:
   **Packs and Skills moved to SKILLS.md · Show**, which opens the Packs tab.

### What changes in the code

| Part | Today | After the move |
| --- | --- | --- |
| `SkillPackStore` | Reads embedded files and `App_Data/SkillPacks/` | Reads the Packs table and follows its pointers. The two embedded Pack files are deleted from Huddle; they live in the skills repository |
| `SkillsCatalogIndex` | Reads the embedded `SkillsCatalog.json` | Reads the Catalogs table and its catalog files; the embedded catalog only seeds the move |
| `SkillDownloader`, `SkillImporter` and their GitHub `HttpClient` registrations | Download a Skill archive from GitHub, pinned to a commit | Removed. Huddle opens no network connection for Skills |
| `SkillImportDialog` (Settings › Skills › **Import skill**) | Previews and installs from the catalog or a GitHub link | Opens the prompt dialog `G5` |
| `ImportingSkillInstaller` | Downloads missing catalog Skills during **Approve & hire** | Removed. A roster that needs something not downloaded is **Needs setup** |
| `SkillReviewScanner`, `SkillValidator`, `SkillFolderHash`, `SkillProvenance` | Run on a downloaded archive | Kept, and moved into the shared checks, so the same rules run on whatever a pointer leads to |
| New: the checks | None | One library holding every check above, used by Huddle and by `huddle-packs` |
| New: `huddle-packs` | None | A standalone console program in the chat surface's subtree. Adding it to `Huddle.slnx` is a shared root change and is announced first |
| New: `InstallSkillPack.md` writer | None | Builds the file from Settings › Prompts and the appendix, at start-up and on change |
| New: the registry watcher | None | Watches SKILLS.md and every folder its rows point at, and re-runs the checks for what changed |
| `TeammateCard.TogglePack`, the Skills picker, the role form | Change Skill names, then rely on hiring to install | Copy the Skill folders into `Skills/` before they change the Teammate; offer **Copy prompt** for anything not downloaded |
| `CardResolver` | Skips a Pack id it cannot find | Reports **Needs setup** for a Pack or Skill that is missing or not downloaded |
| `PersonaFrontmatter`, `HireService.MergeConsultWhen` | Read and write `_packs`, merge `consult_when` | Unchanged |
| The Library | Teams, Teammates and pinned roots | Adds the read-only **Skill Sources** root |

[ADR-0043](adr/0043-imported-skills-are-pinned-by-commit-and-hash.md) is partly superseded:
pinning, hashing, the review flags and the safe copy stay; the download moves to your AI tool.
That change needs its own ADR before it is built.

## Notes

- **No AI tool at all.** You can clone a repository yourself, write the pack or catalog file and
  the SKILLS.md row by hand, and run the validate command. It appears like any other.
- **Offline.** Nothing in Huddle needs the network, for Skills or for anything to do with
  them. Only fetching, which your AI tool does, needs it.
- **A row Huddle cannot use** (a pointer that leads nowhere, a file that fails its schema) shows
  as a **Broken** card with the reason. The rest of SKILLS.md still loads.
- **A clone you move or delete** turns its rows **Not downloaded** or **Broken**. Teammates keep
  working from their copies in `Skills/`.
- **An AI tool that wrote somewhere it should not** is not something Huddle can see or undo.
  Huddle reads only what SKILLS.md points at.

> [!WARNING]
> Your AI tool runs with its own permissions and your sign-ins, not Huddle's.
> `InstallSkillPack.md` tells it where to write and what it must never run, but only your AI
> tool's permission prompts enforce that on your computer. With no approval step, the checks
> are what stand between a fetched Skill and your Teammates: Huddle refuses anything that fails
> them, never runs anything in a Skill, and copies a Skill to a Teammate only when you choose it.

## Open questions for requirements

None open right now. Every question raised while envisioning is answered in
[Decisions so far](#decisions-so-far). The spec will raise its own, starting with the
mechanical ones: how SKILLS.md is parsed, what is hashed to notice an update, what is watched,
how a copy is made while an AI tool is still writing, how `huddle-packs` ships, and the new
`Team:` configuration keys and prompt keys.
