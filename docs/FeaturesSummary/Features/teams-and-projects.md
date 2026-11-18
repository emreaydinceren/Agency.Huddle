# Teams and Projects

A **Team** is a group of Agents with a shared folder, purpose and way of working. A **Project** is a
sub-folder inside it. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#organising-teams-and-projects).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Build a Team in four steps

Open **Teams**, then **Build a team**:

1. **Team type.** Pick a template, or start blank.
2. **Team details.** Name, purpose and Library folder.
3. **Hire.** Each role's **Add** drafts an Agent you can re-roll, edit or remove. You can also write
   your own roles.
4. **Approve & hire.** Huddle creates the Team and its process, and hires the drafts.

Hiring a typed Team starts a paid Turn. The manager proposes a starter Task in your one-to-one Room,
and creates it only after you say yes.

## Start from a template

Huddle ships more than two dozen team types, with **Development Team** and **Household** first. You
can add your own, and **Settings > Team types** lists them all. A single hiring pool holds every
job, filtered by two tag clouds.

**Household** runs a family's home, with roles such as School Liaison, Meal Planner, Home Keeper and
Finance Tracker. Every spend or signature waits for you. Only the Household Manager is required.

## Use a Team's page

| Tab | What's there |
| --- | --- |
| **Members** | Role cards and the roster |
| **Files** | The Library, scoped to this Team |
| **Tasks** | A List or Board with one swimlane per Project |
| **Process** | The Team's rules, gates and Needs You level, on a Team with a `_team.md` file |
| **Details** | Type, name, purpose, Library folder and archive |

A Project's page has **Files** and **Tasks**, and its own **Spent** total. Any ordinary sub-folder of a Team folder is a
Project.

In the **Teams** sidebar, the open Team's card has a selector to switch to another Team. Below it, the Team and its
Projects are a selectable list, each Project with a square avatar and its number of open Tasks. Pick one to see its
Rooms, Tasks and Library link below the list. A Room can belong to one Project, and **No project** shows what belongs
to none.

## Other things you can do

- **Duplicate** a Team's roster into a new Team, hiring each member afresh.
- **Add a shortcut.** Each Team can have an avatar tile in the Activity bar, opening its Team and Project
  list with their Rooms, Tasks and Library. Tiles are on by default.
- **Give a Team an avatar**, drawn as a rounded square so it never reads as a person.
- **See the Team's spend** as a total per currency, and a Project's own spend on its page.
- **Choose how much waits for you** with the Needs You level, on the Team's **Process** tab or a Project's
  **Needs You level...** action. See [Needs you and the Human gate](needs-you-and-human-gate.md).
- **Connect a workspace**, such as a code repository, so members know where the real work lives.
- **Rename** a Team or a Project with **Rename team...** or **Rename project...**, on its page or in its sidebar menu.
  A confirmation lists what can go wrong. The folder, Rooms, Tasks, Views and links move with it, the Team Room gets
  one line saying so, an Agent that may know the old name is told once, and the old name still reaches it.
- **Archive** a Team or a Project, from its row's More (⋮) menu in the sidebar or from its page. The folder moves unchanged into the read-only **Archive**, and
  **Restore** on the **Archived** page brings it back. See [Second Brain and Memory](second-brain-and-memory.md).

## Good to know

- Teams can't be deleted in the app. They can be archived.
- By default, a Team holds up to 8 Agents and Huddle runs up to 16 live processes.
- A role that needs a connector you haven't set up shows **Not connected yet**, which never blocks the hire.

## In more detail

### What a Team is made of

A Team is a folder, `Teams/<Team>/`, plus each member's `teams:` line. Huddle creates `raw/`, `memory/` and
`outputs/` inside it and writes `_team.md`. A Team made from a type also gets the type's process as
`AGENTS.md`.

Team names can't contain `,` `;` `[` `]` or start with `_` or `.`, and follow the Library's file-name rules.
They are unique regardless of case.

### Each step of Build a team

| Step | Details |
| --- | --- |
| **Team type** | Cards show what each type does, how many roles it has besides the manager, and **You have one** if you already run it. **Blank Team** has no roles or process |
| **Team details** | **Library folder** becomes the Team's workspace, and stays empty for a Team with none. A type that needs one, such as **Development Team**, asks before you can go on. Choose a **Process** if the type offers more than one |
| **Hire** | Suggested roles come first, then your own, then the hiring pool. **Add** again drafts a second Agent in that role. A dashed avatar ring means not hired yet |
| **Approve & hire** | Off until the details are complete and the roster has at least one Agent. A hint says which is missing |

- **Your own roles.** The **Add** (+) card takes a title, what it does, instructions and model strength. They
  are saved in `custom-roles.json` for next time.
- **Skills.** Approving copies the Skills the roster needs. If one can't be copied, approval stops with
  "Skills not ready: …" and nothing is created. **Edit** on a draft lets you untick a Skill that isn't on this
  computer.
- **Cost.** The manager takes a paid Turn on its onboarding steps once it's running, so set each draft's
  **Model** before you approve.

### Changing members

On the **Members** tab, nothing changes until **Save changes**:

| Control | What it does |
| --- | --- |
| **Add** on a role | Puts a new draft on the roster |
| **Edit** on a member | Opens their Agent card, which saves at once |
| **Message** | Opens this Team's one-to-one, once the member has connected at least once |
| **Remove** | **Remove from team** keeps the Agent. **Delete agent** deletes it, and Huddle asks first when saving |
| **Manager** | Picks from members and drafts in a manager role |

Taking an Agent off a Team restarts it, and it loses its conversation memory. To add an Agent who already
exists, add the Team to the `teams:` line on its card.

> [!CAUTION]
> Close any open Agent Edit card before changing members. A card saves its whole text, so saving one that was
> open during the change puts the old `teams:` line back.

### Projects

Use **New project** in a Team's More (⋮) menu or its **Files** tab, or create a folder directly inside the
Team folder. The names `memory`, `raw` and `outputs` are excluded, along with any name starting with `_` or
`.`. A Project has no members of its own, so the Team works it, and there is no separate record.

To start a chat inside a Project, select the Project in the sidebar and press the "+" on its **ROOMS** section. The Room belongs to
that Project from then on, is listed under ROOMS when that Project is selected, and can't be moved. A Room whose Project is later archived appears in
the Team's own Rooms instead.

A Project's **Spent** total adds up the Turns that ran in the Project's Rooms, whoever ran them, one amount per
currency. A Turn in a Room outside the Project counts toward none, and deleting a Room keeps what it spent. The total is
blank until something is spent there.

**No project** in the sidebar narrows the Rooms, Tasks and Library link to what belongs to no Project. It appears once
the Team has a Project.

### Renaming a Team or a Project

Choose **Rename team...** or **Rename project...** from the More (⋮) menu on its sidebar row, or **Rename** on a Team's
**Details** tab. Huddle asks for the new name, checked by the same rules as a new one, then shows a confirmation before
changing anything. Only you can rename: Agents have no way to.

The confirmation lists what can go wrong, with your Team's numbers:

- **Members restart.** Each starts a new conversation, keeping a summary of recent Messages but losing its working
  context. Any member in the middle of a Turn is named, and loses that Turn's work. Renaming a Project restarts no one.
- **Agents are told on their next Turn,** not at once, so until then one may use the old name. An Agent that writes a
  file to the old folder brings it back as an empty Team.
- **Agents' own notes keep the old name** until each Agent updates them.
- **Links outside Huddle break,** such as bookmarks and scripts that use the old folder.
- **It can't be undone in one step.** The old name stays reserved, and renaming back is another rename.

Once you confirm, the folder, Rooms, Tasks, Requests, saved Views and links inside Huddle move with it, and the Team Room
gets one line saying so. The old name still reaches the right place: a link or Reference to it opens the renamed Team, an
Agent that reads by it is forwarded and told, and one that tries to write by it is refused with the new name. A Project is
renamed in place inside its Team.

If Huddle stops partway, such as when it is closed during a rename, the rename finishes at the next start. If a step
keeps failing, the Feed shows a notice listing what is left, and Huddle retries at the next start.

### Archiving a Team or a Project

Archiving a Team first tells you how many open Tasks it would hide and which Agents would be left with no Team. A Project
is archived at once, since nothing is lost. The Team's Rooms are archived with it, and **Restore** on the **Archived**
page brings back members and Rooms too. The page has **Teams**, **Projects** and **Rooms** tabs, and shows where each Room
belongs. If something now has the same name, Huddle refuses and asks you to rename one of them first. An archived Team's
Projects come back with it.

### Team types

Your own types live in the `TeamTypes` folder under your data folder. A folder with the same id replaces the
built-in type. **Settings > Team types** shows each type's name, id, version, source and role count.
**Reload** rereads them, and **Problems** lists each rejected type with its file and reason.

Team types ship process templates such as a lightweight flow, spec-driven, sprints and maintenance for a
Development Team.

### Duplicating a Team

The dialog asks for a name and a workspace folder. The copy has the same type, and each member is hired
afresh under a new name. No memory, files or Tasks are copied. Members whose role no longer exists are listed
as skipped.

### Editing a Team

On **Details**, you can change **What it's for** and the **Library folder**. The type and name are fixed.
Changes to the Tasks tab's filters last for the current visit only, and stay inside the Team or Project.

### Hire limits

| Setting | Default | Limits |
| --- | --- | --- |
| `Team:Acp:MaxAgentsPerTeam` | 8 | Agents in one Team |
| `Team:Acp:MaxLiveProcesses` | 16 | Agent processes at once, across every Team |

A hire past either limit is refused with a plain sentence. A value of zero turns a limit off.

### Household in depth

| Role | Owns |
| --- | --- |
| School Liaison | School emails, events, the weekly digest and draft replies. Needs `email:read` |
| Meal Planner | The menu around allergies, the pantry list and the grocery list |
| Home Keeper | The upkeep calendar, contractors and warranties |
| Health Admin | Appointments, vaccinations, prescriptions and forms. Needs `email:read` |
| Schedule Keeper | The family calendar and renewals |
| Finance Tracker | Bills, subscriptions and spending against the budget |
| Comparison Shopper | Quotes and prices for a big purchase or renewal |
| Retro Agent | The monthly retro. Without one, the manager runs it |

The default process has these family rules:

- **One owner per job,** listed in a "who owns what" note in Team Memory.
- **One Task per occurrence.** Huddle has no repeating Tasks, so the owner opens the next a week before it's due.
- **Every spend and signature waits for you,** however small the amount.
- **Notes say where a document is kept, never its number.**
- **A weekly check-in,** counted from the day you adopt the process, and a retro every 28 days.

To connect something, list it in `connectors.json` in your data folder, such as
`{"connected": ["email:read"]}`, then select **I connected it** on the card. Huddle doesn't connect anything
itself.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| **Approve & hire** is off | The details aren't complete or the roster is empty. The hint says which |
| A hire says the Team is full | A [hire limit](#hire-limits) was reached |
| The Chief of Staff says a role cannot be hired yet | A Skill or Pack the role needs isn't ready. Set it up, or ask for a role it lists as ready |
| A Team still appears after you removed everyone | Removing members never deletes the folder. It shows **No members** |
| A rename was refused | The name breaks the name rules, is taken, or is an old name that stays reserved. Pick another |
| The Feed says a rename is stuck | A step could not finish, such as a file held open. Close what holds it, and the rename finishes at the next start |
| Restore was refused | Something now has that name. Rename one of them, then restore |
| A membership change was undone | An Edit card open during the change saved its old text. Add the member again |
| Your Team type is missing | See **Settings > Team types > Problems**, fix the file, then select **Reload** |
