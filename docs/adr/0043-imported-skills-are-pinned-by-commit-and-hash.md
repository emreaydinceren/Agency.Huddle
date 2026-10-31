---
status: partly superseded
date: 2026-10-08
superseded-by: 0059
superseded-on: 2026-10-18
---

# Imported skills are pinned by commit and hash

> **Partly superseded by [ADR-0059](0059-skills-and-packs-are-fetched-by-the-humans-ai-tool.md):** pinning,
> hashing and the review checks stay; the download goes. The Human's AI tool fetches, and Huddle follows local
> pointers. Built on 2026-10-18: the importer, the downloader and the built-in Packs are gone from the code.

A Skill can be imported from a public GitHub repository, from the built-in catalog or from a
URL the Human pastes. The import is pinned to one **commit**, the files are recorded by a
**SHA-256 hash** in a `.source.json` beside them, and the Human sees what the Skill contains
before it is installed. The original Teams
specification, which held the full design, has been retired; Skill Packs now carry it in the
[Skill Packs specification](../Huddle.SkillPacks-Specifications.md).

## The problem

A Skill is instructions, and sometimes scripts, that an Agent will follow. Fetching one from the
internet raises three problems: the repository can change after the Human looked at it, an
archive can carry files that escape the folder or cannot exist on Windows, and the Human cannot
tell later whether the folder on disk is still what was imported or has been edited.

## The decision

**The download is narrow and bounded.** `SkillDownloader` accepts only `https://github.com/
<owner>/<repo>` and fetches `codeload.github.com/<owner>/<repo>/zip/<commit>` for a 40-character
hexadecimal commit, so the hosts are fixed and nothing but a commit is fetched. It enforces a
download limit (50 MB, checked against the header and again while reading), at most 200 files
and 5 MB for the Skill folder. It rejects the whole archive for a path that is rooted, contains
`:` or `..` (zip-slip), a symbolic link, a name Windows cannot store (reserved device names,
forbidden characters, trailing dot or space) and two names that differ only in case.

**A branch is resolved once, to a commit.** `SkillImporter` takes a GitHub `tree` URL, validates
the owner, repository and reference, asks the GitHub API for the commit the reference points to
and pins that commit. A catalog entry already names its commit.

**The Human reviews before installing.** `SkillReviewScanner` flags a script file, a shell
command, a network call, a path outside the Work Dir and a missing licence. The preview shows
the flags, the validation problems and any difference between the catalog entry and the
`SKILL.md`. A Skill with validation problems or no `SKILL.md` cannot be installed. **A missing
licence is a warning, never a block.**

**Install is safe to repeat and safe to fail.** `Install`:

- refuses a name that is a built-in Skill, so an import cannot replace one;
- treats the same import twice (same repository, path and commit) as a success that changes
  nothing, and refuses a folder of that name that came from somewhere else;
- writes the files and the provenance into a temporary folder in the data directory, and moves
  it into place, so a half-written Skill never appears in the Skills folder;
- removes temporary folders older than an hour left by an earlier crash;
- runs under one lock.

**The provenance records what was imported.** `SkillProvenance` is the `.source.json`: the
repository, path, commit, the SHA-256 of the files (`SkillFolderHash`), publisher, licence and
import time. `SkillStore` reads it into `Skill.Provenance`, and sets `Skill.ModifiedLocally`
when the folder's current hash no longer equals the recorded one. A Skill with no valid record is
simply not an imported Skill.

**Hiring imports what a Team needs, but asks for consent for scripts.** `ImportingSkillInstaller`
installs a missing Skill from the catalog when its only flag is a missing licence. A Skill whose
review shows anything more is not installed, and is returned so Start Team stops and the Human
imports it from Settings, where the flags are shown.

## Rejected

| Alternative | Why not |
| --- | --- |
| Follow a branch and update on pull | The Human reviewed one set of files, and a branch can change under them |
| Use a Git client | A zip of one commit from a fixed host needs no tool and no credentials |
| Block a Skill with no licence | Many useful Skills declare none. The Human sees the warning and decides |
| Install into the Skills folder and clean up on failure | A reader of that folder could see a half-written Skill |
| Re-hash only on import | The point of the hash is to show later that the folder was edited |

## Consequences

- **What runs is what was reviewed,** at a commit and a hash, and an edit shows as modified.
- **A hostile archive is rejected before anything is written.**
- **Importing again is harmless,** and a name clash with a built-in or another source is an error.
- **Hiring cannot smuggle in a script.** Anything beyond a missing licence waits for the Human.
- **The review is a scan, not a sandbox.** It finds what is easy to see; the Human still decides.

Code: `src/Huddle.App/Skills/SkillDownloader.cs`, `SkillImporter.cs`, `SkillProvenance.cs`,
`SkillFolderHash.cs`, `SkillReviewScanner.cs`, `SkillStore.cs`, and
`src/Huddle.App/Teams/ImportingSkillInstaller.cs`.
