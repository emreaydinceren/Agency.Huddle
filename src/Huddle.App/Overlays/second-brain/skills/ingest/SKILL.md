---
name: ingest
description: Use when the Human asks you to ingest new raw files, and you propose Memory entries, Tasks and outputs from them, each with a Based-on link, for the Human to approve.
---

# Ingest new raw files

Raw files are the originals the Human or an Agent put in a `raw/` folder. They
are read-only for you: never edit, move or delete one. This Skill turns new raw
files into notes the Team can use, and writes nothing until the Human agrees.

## Steps

1. Find the raw files that are new. Ask the Human which folder or files to
   ingest when it is not clear; do not guess.
2. Read each new file. Skip a file you cannot read and say which one.
3. Propose, grouped by destination:
   - **Memory** entries for facts and rules worth keeping.
   - **Tasks** for work the file asks for.
   - **Outputs** for a summary or a note the Team should keep.
4. Give every proposal a Based-on link to the raw file it came from. A proposal
   with no raw file behind it does not belong in the list.
5. Show the whole list and wait for the Human's OK. Nothing is written yet.
6. Write only what the Human approved. Leave out what they declined; do not
   propose it again in the same session.
7. Tell the Human what you wrote and where.
