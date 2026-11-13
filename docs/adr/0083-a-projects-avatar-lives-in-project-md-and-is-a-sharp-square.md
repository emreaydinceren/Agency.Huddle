---
status: accepted
date: 2026-11-12
---

# A Project's avatar lives in `_project.md` and is a sharp square

A Project had no face of its own. The sidebar drew the same Project icon on every Project row, so
a Team with five Projects showed five identical marks, and the Project page header was plain text.
A Team already has an avatar
([ADR-0069](0069-a-teams-avatar-lives-in-team-md-and-is-a-rounded-square.md)). This record gives a
Project one by reusing the same stack, and settles where it is kept and how it differs from a
Team's.

## The decision

**A Project's avatar is two optional keys in `{DataDir}/Teams/<Team>/<Project>/_project.md`, it is
initials or a short label and never a picture, and it is drawn as a sharp square a size below its
Team's.**

- **Where it is kept.** The keys are `avatarLabel` and `avatarBackground`. The file lives in the
  Project's folder, so it travels with the folder when the Project is archived and restored, and a
  new Project that takes an archived one's name does not inherit its face. `ProjectFile` keeps any
  other frontmatter line and the notes, so a hand edit survives a rewrite; the rewrite does tidy the
  frontmatter (avatar keys first, blank lines inside it dropped). A Project without the file reads
  as `Avatar.None` and draws the initials of its name. Nothing is written until the Human saves an
  avatar that differs from the stored one, so pressing Save without a change creates no file.
- **No new store and no new watcher.** `TeamCatalog` reads every Project folder's `_project.md`
  with its snapshot and adds the file name to the `_team.md` watcher's filters, accepting it one
  level deeper. `TeamSummary.ProjectAvatar(name)` is the one lookup. The Task store ignores the
  file: it is not under `_tasks`.
- **No picture.** A Project's avatar is initials or a label, which is where an emoji goes.
  `AvatarEditor` and `AvatarEditorDialog` gain `AllowImages` (default `true`); `false` removes both
  image cards, opens an avatar with nothing stored on Initials, and never hands an image back.
  `ProjectFile` does not read an `avatarImage` line, and `ITeamFolders.UpdateProjectAvatar` drops
  an image before writing. So no image file is ever written for a Project, and archiving or
  removing one leaves nothing behind in `{DataDir}/avatars`.
- **The shape.** An Agent or the Human is a circle and a Team a rounded square; a Project is a
  square with sharp corners, so it never reads as its Team. `AvatarShape` gains `Square`, which
  `AgentAvatar` draws with `avatar-square avatar-sharp` (`border-radius: 0`), and `ProjectAvatar`
  is a thin wrapper that always passes it.
- **The size.** A Project's avatar is drawn a size below its Team's wherever the two meet: 24px in
  the sidebar's Project rows under the Team card's 40px avatar, and 24px in the Project page header
  where the Team page's is 32px.
- **Where it is edited:** the avatar before the Project page's title is a button, and the header
  has an "Edit avatar…" action before Archive. Both open the editor with the button "Save". A
  Project always has a folder, so unlike a Team without `_team.md` it always has somewhere to keep
  its avatar.
- **The Project icon goes from the sidebar.** Each Project row shows its avatar in the 24px slot
  the icon held. The icon stays on the Archived page's Projects tab: an archived Project is not in
  the catalog, so it has no avatar there.

## Consequences

- `_project.md` shows in the Project's Library as a file, as `_team.md` does in a Team's.
- A Project rename, if one is added, needs nothing extra: the file is inside the folder that moves.
- `AvatarShape` has three values; a caller still never passes one by hand, but through
  `AgentAvatar`, `TeamAvatar` or `ProjectAvatar`.
