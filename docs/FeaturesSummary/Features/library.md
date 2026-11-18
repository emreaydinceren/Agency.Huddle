# Library

The **Library** is a file browser and Markdown editor over your Huddle data, and you can use it as a
linked notebook. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#keeping-notes-in-the-library).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Browse your files

Select **Library** in the Activity bar and the folders and files of every root appear in the left sidebar, with a
search box, **New note** and **New folder** above them. Select a file and its page opens in a tab, showing only
the document, so the page keeps all its room for reading and writing.

The built-in roots are:

| Root | Holds |
| --- | --- |
| **Teams** | Team and Project folders |
| **Agents** | Agent files and their working folders |
| **Raw** | Files attached in chats outside a Team |
| **Archive** | Archived Teams and Projects, read-only |
| **Skill Sources** | Where Skills come from, read-only |
| **Prompts** | Your changes to the text Huddle sends to models |
| **Memory backups** | What each Memory Tidy pass changed, read-only |

Pin any other folder in **Settings > Library**. A Team's or Project's page has a **Library** tab that shows just its
own folder.

## Write and read notes

- **Three modes.** Markdown opens in **Read**, **Edit** or **Split**. `Ctrl+S` saves.
- **A formatting toolbar** with shortcuts for bold, italic, headings, code and lists.
- **Safe saves.** Huddle keeps your encoding and line endings, and asks what to do if the file
  changed on disk.
- **Other files.** Images preview, a PDF offers **Open in default app**, and other text is read-only.

## Link your notes together

Huddle reads links the way Obsidian does: `[[Note]]`, `[[folder/Note]]`, `[[Note|text]]` and
`[[Note#Heading]]`. Typing `[[` suggests notes, and a link to a missing note offers to create it.

The Right sidebar shows each note's:

| Panel | What it shows |
| --- | --- |
| **Outline** | The note's headings |
| **Linked documents** | What this note links to |
| **Backlinks** | Notes and open Tasks that link here |
| **History** | Who created, edited, reviewed or objected, and when |
| **Tags** | This note's tags, and a tag tree with counts |

## Reorganise without breaking links

Drag and drop, or use **Move…**, and every `[[link]]` to the file is rewritten. Renames carry a
note's history with them.

## Use Obsidian-style metadata

A note's front matter appears as a typed **Properties** block. Links in it count as backlinks, and
aliases are searchable. Tags work like Obsidian's too: inline `#tag` plus front matter `tags`, nested
on `/`. Huddle reads these and writes nothing back.

## Good to know

- Delete sends a file to the Recycle Bin.
- Roots, Teams, Projects and Agent folders are protected from moving.

## In more detail

### Managing roots

- **Pin a folder** in **Settings > Library**, which can also hide the built-in roots.
- **Where it's saved.** Pinned folders are in `library-roots.json`, created the first time you add, remove or
  hide a root. Hand edits to it are read only at startup.
- **Start over.** **Reset pinned folders** deletes the file and returns to the configured list.
- **Hidden folders.** Task folders (`_tasks`) don't show in the **Teams** root.

### How files open

| File | Behaviour |
| --- | --- |
| Markdown | **Read**, **Edit** and **Split** |
| Other text and code | Read-only |
| Images | Preview |
| PDF | A card with **Open in default app** |
| Anything over 2 MiB | Read-only |

If a file changed on disk since you opened it, Huddle offers **Reload** or **Keep mine**. Saving an Agent's
definition file restarts that Agent. Agents hear about your saves at their next Turn if the file is in a folder
they watch.

### Organising actions

Open the More (⋮) menu on any item, or right-click it.

| Action | What it does |
| --- | --- |
| **New note** | Creates an empty file, adding `.md` if you give no extension |
| **New folder** | Creates the folder. Inside a Team folder, this is a new Project |
| **Rename** or **Move…** | Moves the item and rewrites `[[links]]` across the root. The dialog says how many will change and lists any notes it couldn't update. Moves stay within one root |
| **Copy path** | Copies the full path, which you can paste into chat |
| **Delete** | Asks first, then sends the item to the Recycle Bin |

File names can't contain `/ \ : < > " | ? *`, end in a dot or space, or use a device name such as `CON` or
`NUL`. Folders that hold Tasks can't be moved.

The Library's search box matches file names only, with at most 200 results. To search inside files, use
[Global Search](finding-your-way.md#search-everything).

> [!NOTE]
> A `memory/` folder isn't protected the way Team and Project folders are. If you rename or delete it, Agents
> lose the index.

### Properties and tags

Each Property has a type: text, list, number, checkbox, date, date and time, aliases, tags, unsupported or
mismatch. Huddle takes the type from the root's `.obsidian/types.json`, then its defaults, then the value
itself. JSON front matter works too.

Tags are case-insensitive, and nesting on `/` builds the tree in the **Tags** panel.

### Document history

A note's history shows as a byline under its heading and a **History** timeline in the Right sidebar.

| Who | How it's recorded |
| --- | --- |
| You | Saves, creates, renames and moves log themselves. A file you attach in a chat or to a Task is logged as created when it is saved to the Library |
| Agents | They record entries with a tool, and are reminded when they create or edit without one |
| Reviewers | Only an Agent whose role verifies is reminded about reads |

An edit made in another editor leaves no history entry. If you remove an attachment before sending it, its
history goes too, so a later file of the same name starts clean. A review an Agent makes in goldfish mode reads
"Goldfish review by" its name.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| Delete says the Recycle Bin isn't available | The file is on a network or removable drive. Delete it in your file manager |
| A note won't let you edit | It's over 2 MiB, or isn't Markdown |
| A folder can't be moved | It's a root, Team, Project or Agent folder, or it holds Tasks |
