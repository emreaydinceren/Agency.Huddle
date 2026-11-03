---
name: brain-check
description: Use in a weekly or monthly Job to check the brain's health: caps, missing descriptions, broken links, changed raw files, misplaced rules, quiet Teams. It changes no file and files Tasks and retro proposals.
---

# Check the brain

This Skill reports on the health of the notes, then files what it found. It
changes no file: it never edits Memory, `AGENTS.md`, outputs or raw files. Its
only writes are Tasks and retro proposals, so it needs no OK from the Human
before it files them.

## Checks

1. **Caps.** Which folders or indexes are over their cap.
2. **Outputs with no description.** List each one.
3. **Broken links.** Links in notes that point at nothing.
4. **Raw files changed since filing.** Call `get_help` and read its **Raw files
   changed since filing** list. Report that list as it is. Never compare files
   yourself.
5. **Rules in Memory and facts in `AGENTS.md`.** A rule belongs in `AGENTS.md`;
   a fact belongs in Memory. List each one in the wrong place.
6. **Quiet Teams and Projects.** Those with no file change in 60 days, leaving
   out `_tasks/_closed/`.

## Filing

- File each finding as a Task, one per finding, with the evidence in its
  description.
- File a rule that should move as a retro proposal for the Team's next retro,
  not as an edit.
- When nothing is wrong, say so in one line and file nothing.

End with a short report: the checks that found something, the Tasks filed and
the retro proposals made.
