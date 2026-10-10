---
status: accepted
date: 2026-10-10
---

# The avatar library ships with the app and is the default

> **Amends [ADR-0044](0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md).** Everything
> that ADR decided about *how* the library is read stands: a read-only folder, read when the dialog
> opens and dropped when it closes, and a pick that copies. What changes is *where it comes from* and
> *what happens when nothing is configured*. The full design is in the
> [Avatar library specification](../Huddle.AvatarLibrary-Specifications.md).

## The defect

ADR-0044 left the library outside the repository, in a folder named by `Team:AvatarLibrary:Dir`, and
made an unset `Dir` mean *no library*. The 324 pictures lived in one place on the author's machine
(`E:\Projects\Software Factories\avatars`). So a fresh clone, a second checkout, the CI runner and an
installed copy all showed an avatar editor with no **Choose from library** button, until somebody
found the setting and set it by hand. That is what happened on the first run of a seeded copy:
`run.ps1 -seed` started the app, and the button was simply not there, with nothing to say why.

## The decision

**The library is part of the repository and ships with the app. It is the library when `Dir` is unset.**

`src/Huddle.App/AvatarLibrary` holds the 324 pictures, `master.json` and its `master.schema.md`.
`Huddle.App.csproj` copies the folder beside the application on build and on publish, and
`AvatarLibraryLocator.BundledDir` finds it from the application's base directory. `Dir` still works,
and still **replaces** the bundled library; it is never backed by it.

### Decisions

| # | Decision | The alternative it beat |
| --- | --- | --- |
| D-1 | **Commit the pictures to the repository** (35 MB, 324 files). | An install-time download, which needs hosting and a network and leaves the picker silently absent when offline. Git LFS, which needs LFS on every clone, on the CI runner and on the GitHub mirror. A NuGet package or a separate repository, which moves the same files and adds a version to keep in step. |
| D-2 | **A `Content` item, copied beside the app, found from `AppContext.BaseDirectory`.** | A path relative to the working directory, which differs between `run.ps1`, `dotnet run`, the tests and an installed copy. The content root, which differs between a build and a publish. Embedding the pictures as resources, which puts 35 MB into the assembly and cannot be served as static files. |
| D-3 | **A configured `Dir` replaces the bundled library and never falls back to it.** A set path that is not a library means no library. | Falling back when the set path is bad. It would remove the one way to switch the shipped library off, and a typo in a path would silently show a different library from the one named. |
| D-4 | **`master.json` and the schema note are copied unchanged**, apart from line endings. | Trimming the index to the five fields Huddle reads. It would lose where each picture came from (grid, crop box) and the schema note, to save 249 KB. |
| D-5 | **The pictures are not recompressed.** | Running an optimiser over them. It adds a tool and a change to every file for an unmeasured saving, and the library was already resized once. |
| D-6 | **Tests never touch the folder.** A test that renames or edits library files works on a copy, and `BundledAvatarLibraryTests` pins the count, the size and dimensions of every picture, and that no file is orphaned. | Editing the bundled folder in place, which would change what ships. |

## Consequences

- The repository grows by 35 MB, and so does every clone and the GitHub mirror, for good: history
  cannot shed it. The pictures are binary, so a changed picture is a whole new blob. If that becomes a
  cost, D-1 is the decision to revisit (LFS, or a download at install), and this ADR would be
  superseded.
- A fresh checkout, a seeded run and an installed copy all have the picker with no setting. The
  tests that said "unset means no library" now say "unset means the bundled library", and the
  no-library case is a `Dir` that names a folder that does not exist.
- The originals and the grid images the pictures were cut from stay outside the repository. Only what
  Huddle reads, plus the schema note, came in.
- A library created after start-up still needs a restart to be served; that has not changed.
