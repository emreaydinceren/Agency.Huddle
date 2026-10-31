---
status: accepted
date: 2026-10-29
---

# A Team's avatar lives in `_team.md` and is a rounded square

A Team had no face of its own: the sidebar and search drew a generic `Groups` icon, the Feed drew
two initials, and the Team page header was plain text. Agents already have a mature avatar stack
([ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md),
[ADR-0047](0047-an-agents-avatar-is-edited-in-its-own-dialog-opened-from-the-avatar.md)). This
record gives a Team an avatar by reusing it, and settles where the value is kept and what it looks
like.

## The problem with `avatars.json`

`AvatarStore` keys `{DataDir}/avatars.json` by Name, and an Agent and the Human already share that
key space. A Team's name is free text, so a Team named like an Agent (or like the Human) would
write to that Agent's entry and replace its avatar, and removing the Team's avatar would remove
the Agent's. Prefixing the key (`team:Growth`) would work only until a hand edit or a rename
cascade (`PersonaRenameCascade`, `HumanRenameCascade`) treated it as a Name.

## The decision

**A Team's avatar is three optional keys in `{DataDir}/Teams/<Team>/_team.md`, and it is drawn as a
rounded square.**

- The keys are `avatarLabel`, `avatarImage` and `avatarBackground`, the three fields of the
  existing `Avatar` record. Each is written only when set, so an unedited Team's file is unchanged
  and an old file without them reads as `Avatar.None`.
- The avatar lives with the rest of the Team's metadata, and the catalog's existing `_team.md`
  watcher refreshes every page that shows it. No new store, no new watcher.
- Image bytes still go through `AvatarStore.WriteImage` and `DeleteImage`, into
  `{DataDir}/avatars/<guid>.<ext>`, and are served by the existing `/agent-avatars` route with the
  same size and magic-byte rules. SVG is still refused.
- On read, `TeamFile` drops an image name that is not a bare file name and a background that is not
  a colour, because `_team.md` is hand-edited, the image name becomes a path to read and delete,
  and the background goes into an inline style.
- **The shape is the only visual difference.** An Agent's or the Human's face is a circle; a Team's
  is a rounded square (the skin's base radius, `var(--mud-default-borderradius)`), so a Team never reads as a person. `AgentAvatar`
  gains an `AvatarShape` parameter that adds the class `avatar-square`, and `TeamAvatar` is a thin
  wrapper that always passes it. Image, then label, then monogram, and the four sizes, are
  unchanged, and the picture's hairline follows the shape.
- **A Team has no image library.** `AvatarEditor` and `AvatarEditorDialog` gain `AllowLibrary`
  (default `true`) and `Shape` (default `Circle`). The Team page passes `false` and
  `RoundedSquare`, so its previews are squares and the library card is not offered.
- **Where it is edited:** the avatar beside the Team page's title is a button that opens the editor
  with the button "Save". Saving writes any pending image, rewrites `_team.md` through
  `ITeamFolders.UpdateTeamFile`, and only then deletes the image it replaced. A Team with no
  readable `_team.md` (it exists only as a Persona label) cannot keep an avatar; the new image is
  removed and the Human is told.
- **Where it is shown:** the Team page header, the sidebar's Team rows, the Feed card's Team chip
  and Team filter, the Team search card, and the Team Room's header. Each reads
  `ITeamCatalog.Find(name)?.Avatar`.
- **A duplicate gets its own copy of the image.** `TeamDuplicator` reads the source's image and
  writes it under a new name. Sharing one file name would let replacing one Team's picture delete
  the other's.

## Consequences

- This amends [ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md): "an Avatar is
  held apart from the Persona" still holds, but `avatars.json` is the store for Agents and the
  Human only. Its rename and remove cascades do not touch a Team's avatar.
- A Team's `_team.md` gains a rewrite when its avatar changes. `UpdateTeamFile` already preserves
  unknown frontmatter lines and the notes, so a hand edit survives.
- At `Size.Small` (24px) `AgentAvatar` draws the monogram or label and never a picture, so the
  sidebar, the Feed chip and the search card show a Team's label or initials and show its picture
  only at 32px and up (the Team page header and the Team Room header use the Transcript size).
- There is no Team deletion in the app, so no delete path removes a Team's image file.
