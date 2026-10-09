---
status: accepted
date: 2026-10-08
---

# An avatar library is a read-only source, and a pick copies

> **Builds on [ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md).** Nothing
> that ADR decided changes. `Avatar`, `AvatarStore` and `avatars.json` are untouched, and a picked
> face goes through the same path an upload does. The full design, with the use cases and the
> mock-ups, is in the [Avatar library specification](../Huddle.AvatarLibrary-Specifications.md).

## The defect

An Avatar can only be an image the Human already has on disk. Giving a Teammate a face therefore
means finding a suitable picture somewhere, cropping it square, shrinking it under the 512 000-byte
cap and then uploading it. [ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md)
refuses an image decoder, so the app cannot do any of that work itself. The feature exists, but
choosing a face is a chore, and most Teammates keep their initials.

## The decision

**Huddle reads a library folder that the Human maintains. A pick copies the chosen file into the
Avatar store through the same path an upload uses.**

The library is a folder named by `Team:AvatarLibrary:Dir`. It holds 324 prepared PNGs and a
`master.json` index. Huddle reads five fields from the index and nothing else. The dialog reads the
index when it opens and drops it when it closes. Thumbnails are served by a static-file mount, so
the server never holds the pixels. The one picked file is read on demand and handed to the
`avatarPendingBytes` and `WriteImage` path that an upload already feeds.

The Teammate therefore owns its copy. Moving or deleting the library afterwards does not blank
anybody's face, and nothing downstream of the picker changes.

### The nine decisions

Each decision below beat a named alternative. The alternative is kept here so that a later reader
who proposes it again can see why it was not taken.

| # | Decision | The alternative it beat |
| --- | --- | --- |
| D-1 | A pick **copies** the file into the Avatar store. | Putting a library file name in `avatars.json`. That needs a second image namespace in `TeammateAvatar`, and a moved library would blank every Teammate. |
| D-2 | A **second static mount** serves the thumbnails at `/avatar-library`. | A `data:` URI per thumbnail, which ADR-0019 rejected for payload size and missing caching, or a custom endpoint. |
| D-3 | The picker is a **modal `MudDialog`**. | A drawer or sliding panel, which the product owner rejected, or an inline expander inside the card. |
| D-4 | **No visible text on a picture.** The accessible name is `aria-label` only. | Captions or tooltips, and `title`, which is a tooltip. |
| D-5 | The chip clouds are **static**: nothing is hidden, disabled or recounted. | Faceted chips that vanish when they would give zero results, which the product owner rejected. |
| D-6 | Professions combine by **OR**, tags by **AND**, and the four controls by **AND**. | AND everywhere, which makes two professions always empty. OR for tags, which widens the result when the Human means to narrow it. |
| D-7 | The library is **resized in advance**, and the app only validates. | An image decoder in the app, which ADR-0019 refused. |
| D-8 | The index is **loaded on open and dropped on close**, with no cache and no watcher. | A singleton snapshot with change detection, as `AvatarStore` does. That reopens faster, but the index would stay resident for the life of the process, which was ruled out. |
| D-9 | The setting is a **nested options object**, `Team:AvatarLibrary:Dir`. | A flat `Team:AvatarLibraryDir`, which breaks the pattern that `Library`, `Teams` and `Tasks` follow. |

## Consequences

- **The app never resizes or decodes a library image.** An image that is too large or is not a real
  PNG, JPEG or WebP is skipped with a single warning when the dialog opens. The Human fixes the
  file; the app does not fix it for them.
- **A library created after start-up is not served until a restart,** because the static mount is
  added only when the folder exists at start-up. Recorded in
  [Known limits](../engineering/known-limits.md).
- **Library images are served unauthenticated,** as other avatars already are. The library holds
  illustrations, not personal data, and the same-origin reasoning in ADR-0019 applies unchanged.
- **Every picked face is a copy.** Disk use grows by one image per pick. That is accepted, because
  it is what makes a Teammate independent of the library.
