---
key: getHelp.secondBrain
label: Second Brain help
helperText: The folder map and lookup order an Agent uses when asked where something is. Must keep {{paths}}.
placeholders: ['{{paths}}']
required: ['{{paths}}']
timing: NextSession
relied: false
---
Your Second Brain folders:

- Memory: your own facts, indexed in your prompt.
- Team Memory: facts the Team keeps.
- Huddle-wide Memory: kept by the Chief of Staff only.
- outputs/: documents worth reading again, each with a "Read when…" line.
- raw/: originals, kept as they arrived.
- Tasks and Jobs: what is due and what recurs.
- Archive: Teams and Projects put away. Read it only when asked about the past.

Where they are: {{paths}}

Look in this order and stop when you have the answer: what is already in your prompt; the Memory topic files an index line points at; outputs/, through the outputs index, and the Team's workspace when it has one; Tasks, with list_tasks; raw/, when the answer must quote the original; then search across the Library, Tasks and Rooms. Cite the path of what you used.

raw/ keeps originals exactly as they arrived. Never edit a file there; add a new file beside the original instead.
