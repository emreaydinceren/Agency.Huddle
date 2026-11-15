# Skills and Skill Packs

A **Skill** is a folder of Markdown instructions an Agent reads when it needs them. A **Pack** is a named
set of Skills for a role. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#giving-agents-skills).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Use the built-in Skills

| Skill | What it's for |
| --- | --- |
| `team-building` | Assembling a Team |
| `team-rollup` | Summarising across Teams |
| `team-management` | Managing a Team |
| `request-confirmation` | Confirming a Request your Team filed |
| `retrospective` | Running a retro |
| `ingest` | Proposing facts, Tasks and documents from new originals |
| `wrap-up` | Proposing what a session produced |
| `brain-check` | Finding drift and filing it as Tasks |
| `browser-check` | Driving a web page in a browser to verify a Task |

Assign a Skill on an Agent's card. You can override a built-in Skill file by file and restore it later.

## Write your own

Create `Skills/<name>/SKILL.md`, then assign it to an Agent.

## Add Skills from elsewhere

Huddle never downloads Skills itself. Your AI tool, such as Claude Code or Codex, fetches them and adds a
pointer to Huddle's `SKILLS.md` list. **Copy prompt** gives the tool what it needs.

Huddle then checks every file and shows each Skill's state in **Settings > Skills**, such as **Not
downloaded**, **Update available** or **Broken**. Whatever passes the checks is ready to use at once.

The same page has three tabs: **Skills**, **Packs** and **Sources**. Choosing a Pack gives an Agent all
its Skills with one chip.

## Good to know

Every entry in the shipped catalog but one holds a hash of its Skill's folder, so a fetched Skill whose
folder differs from the pinned commit shows as altered. Line endings do not count, so a clone made by Git
for Windows with its usual conversion still matches; a binary file is compared as it is. The exception is
`garrytan/gstack/gstack`: it is the whole repository, which holds a symlink, so it has no hash and nothing
is compared for it. An install that already split the catalog into its
Sources keeps the entries it split then, which have no hash, so that comparison stays off there until
those catalog files are split again.

## In more detail

### What assigning a Skill does

Huddle adds the Skill to the Agent's file and restarts it. The Agent's prompt then lists each Skill's name and
description, and it reads the full text when its work calls for it. This keeps its main instructions short.

### Writing a Skill

Skills have no editor in the app. Create a folder under `Skills/` in your data folder:

```text
App_Data/Skills/release-notes/
├── SKILL.md
└── examples.md
```

```markdown
---
name: release-notes
description: How to write release notes for the Business team's product updates.
---
Start with what the user can now do. One bullet per change.
```

| Rule | Detail |
| --- | --- |
| `name` | Lower-case words joined by `-`, equal to the folder name |
| `description` | Required. Keep it under 300 characters, because over 500 is an error |
| Files | Only `.md` files directly in the folder count, up to 64 KB each. Sub-folders are ignored |

Huddle picks up a new or changed Skill within a second. Editing one restarts nobody, and Agents read the current
text next time they open it.

### Changing a built-in Skill

Create `Skills/<name>/` and put a file of the same name in it, such as `onboarding.md`. Your file replaces that one
file, and the others still come from the built-in version.

**Restore default**, in the Skill's **⋯** menu, asks first and then deletes your whole `Skills/<name>/` folder,
including any extra files you added.

### What each state means

| You see | It means | What to do |
| --- | --- | --- |
| **Not downloaded** | A catalog or Pack lists the Skill, and it has no folder here | Select **Copy prompt** and paste it into your AI tool |
| **Update available** | Your AI tool's files differ from the copies Agents use | Select **Review update** to see what changed and apply it |
| **Needs setup** | A hire card or draft names a Pack or Skill that isn't ready | Select **Copy prompt** on the card, and it turns ready when the tool finishes |
| **Broken** | A check failed, or a pointer leads nowhere | Select **Copy fix prompt** and paste it into your AI tool |

### Adding Skills step by step

1. Open **Settings > Skills**, then select **Add with your AI tool** on the **Packs** tab.
2. Say where the Skills are, and select **Copy prompt**.
3. Paste the prompt into your AI tool. When it finishes, a message says what's ready, and **Show** takes you there.

**Import skill** on the **Skills** tab does the same for a single Skill from a link or folder. Giving an Agent a Skill
copies it to `Skills/`, so later changes reach the Agent only when you apply an update.

### Provenance

An installed Skill keeps a `.source.json` file recording where it came from. Its provenance line shows the Source,
repository and licence, and a **modified locally** chip if the folder changed since the copy. Your own Skills show
"Yours", and shipped ones show "Built in".

### The three tabs

| Tab | What's there |
| --- | --- |
| **Skills** | Every Skill with its description, origin, state and the Agents using it. Search by name, filter by **State**, or turn on **Show hidden**. **Reload** rereads everything, and **Problems** lists what failed |
| **Packs** | A card per Pack with its version, state, Source, Skill count and roles. **Details** shows each Skill's licence and warnings. **Check for updates** opens a prompt for your AI tool. **Hide** leaves a Pack out of lists, and Agents keep it |
| **Sources** | One row per Source and catalog. A Source can be a GitHub repository, another Git server or a local folder. **Change…** picks another Skill Sources folder, which you can share with another Huddle |

Removing a Source removes its notes and rows, never the clone. Withdrawn Skills can't go to new hires, and Agents who
already have them keep working and are flagged.

### Choosing how many Skills

An Agent's card warns when it has more Skills than advised, or when two share a catalog sub-category. A warning stops
nothing. See [Agents](agents.md#agents-hired-from-a-role).

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| A new Skill doesn't show | Check **Problems** on the **Skills** tab. The `name` must match the folder name |
| An import says the Skill already exists | The name belongs to a built-in Skill or another source. Nothing was changed |
| **Approve & hire** says "Skills not ready" | A Skill can't be copied. Use **Copy prompt** on the card, or untick the Skill on the draft |
