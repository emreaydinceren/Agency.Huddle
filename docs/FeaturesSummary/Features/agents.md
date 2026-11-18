# Agents

An **Agent** is an AI teammate with a name, a role and instructions you control. This page shows
what you can do with Agents. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#working-with-agents).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Create and shape an Agent

- **Add one in a click.** **New agent** on the **Agents** page starts with an unused first name and
  a random avatar colour, and you change whatever you like.
- **Write its instructions in plain Markdown.** Each Agent is a file you can edit in Huddle or in
  any text editor. Huddle notices a hand edit within half a second.
- **See what went wrong.** A file Huddle can't read is listed under **Files that didn't load**.
- **Rename safely.** A rename carries the Agent's avatar, Tasks and saved Views with it.

## Choose how it thinks and how much it may do

Each Agent has its own settings, on its card:

| Setting | What it decides |
| --- | --- |
| **Adapter** | The AI tool the Agent runs on. Claude is the default |
| **Model** | Which model answers |
| **Effort** | How hard the model works on each reply |
| **Work mode** | How much the Agent may do before asking you. For Claude: **Agent default**, **Manual** or **Accept edits** |

Changing the Model or Effort restarts the Agent. Changing the Adapter also resets the other three
settings, and the card tells you so.

## Give it a browser and your accounts

- **Every Agent has its own browser.** It is on from the start, with nothing to set up. The Agent reads pages
  freely, and every click, keystroke, upload or script asks you first. Sign-ins stay with that Agent.
- **Your account connectors start off.** Turn them on for one Agent at a time, on its card, and each use asks you
  first.

Both switches are on the Agent's card. For the rules, see [Browsing the web](#browsing-the-web) and
[Using your account connectors](#using-your-account-connectors).

## Give it a face

- **Initials** or **a short label** of up to six characters, with an emoji picker, on a background
  colour you choose.
- **A ready-made picture.** 126 pixel-art people ship with Huddle, and you can filter them by skin
  tone and tag (hair, glasses, top colour).
- **Your own image.** PNG, JPEG or WebP, up to 512 KB.

## Look after a running Agent

- **See its status** on every card: online, starting, working, error or offline.
- **See exactly what it reads.** **What this Agent reads** on its card lists each part of its next
  prompt, where that part comes from and where to change it.
- **Free up its memory.** Send `@nova /compact` to shrink the Agent's conversation, replacing `nova`
  with its alias. It reports how many tokens it saved.
- **Start over in one Room.** **Start afresh** on a Room header's avatar gives the Agent a new
  session there, and every Message stays.
- **Spare yourself repeats.** An Agent that reports to you keeps a short list of what it told you, so it can
  say "no change" instead of telling you again. See
  [Keeping a list of what it told you](#keeping-a-list-of-what-it-told-you).
- **Restart it.** **Restart** clears its Room conversations, and it catches up from the transcripts
  on its next Turn.

## Meet the Chief of Staff

The Chief of Staff is a built-in Agent that helps you assemble a Team and acts as your front door.
Tell it something quickly and it files it:

| You say | It becomes |
| --- | --- |
| A fact about a Team | A note in that Team's Memory |
| Something dated, or an idea you haven't decided on | A Task |
| A fact for everyone | Huddle-wide Memory |
| A rule | A proposed line in `AGENTS.md` |
| Something it can't place | One question back to you |

It proposes new Agents, and nothing is created until you approve. It starts on Opus at medium effort, which you
can change on its card, and it can read every Room and a Team manager's list of what the manager already told
you. **Reset to default** restores its shipped version.

## Set up your own profile

**Settings > Profile** holds your name, alias, title, description and avatar. Agents can @mention
you by name or alias, and you appear on the same kind of card they do.

## Good to know

- **Remove** deletes the Agent's definition file and its browser sign-ins, and nothing else.
- An Agent can belong to many Teams, or to none.
- More Adapters are designed but not built yet. See
  [What Huddle doesn't do yet](../Features-&-Capabilities.md#what-huddle-doesnt-do-yet).

## In more detail

### Naming rules

Names and aliases are at most 64 characters and start with a letter or digit. They may contain letters,
digits, `_`, `-` and single spaces, with no dots. They must be unique across all Agents, and an Agent's
can't match yours.

### What each change does to a running Agent

| You change | What happens | What stays |
| --- | --- | --- |
| Instructions, Model, Effort, Work mode, Adapter, Skills or Teams | The Agent restarts and forgets every conversation. On its next Turn in each Room it catches up from the transcript | Rooms, transcripts and personal Memory |
| The avatar | Nothing restarts | Everything |
| Account connectors, on | The Agent restarts, and the change applies on its next Turn | Rooms, transcripts and personal Memory |
| Account connectors, off, or the browser switch | Applies at once, with no restart and no question | Everything |
| The name | The folder, avatar and session records move. Tasks and saved Views are rewritten, and Rooms named after the Agent are renamed. The Agent's account connectors switch off | Messages already posted keep the old name. Rooms you named yourself are left alone |
| **Restart** | Every Room starts a fresh conversation, with catch-up | Rooms, transcripts and personal Memory |
| **Remove**, after a confirmation | The definition file, Model, Effort, Work mode, avatar and browser profile, with every site it was signed in to, are deleted, and the Agent goes offline | Rooms, transcripts and its `work/` folder. Tasks keep its name but no longer wake anyone |

An Edit card has no **Teams** field. To add an Agent to a Team, edit the `teams:` line in the card's text.
To take it off, use **Remove from team** on the Team's **Members** tab.

### Status badges

| Badge | Means |
| --- | --- |
| Green circle with a check | Online and ready |
| Amber circle that pulses | Starting up |
| Blue circle that pulses | Working now |
| Red circle with a dash | Something is wrong. Open the Agent to see why |
| Hollow grey ring | Offline, or asleep with nothing to do |

Hover a badge to see the reason.

### Work modes

| Choice (Claude) | What it does |
| --- | --- |
| **Agent default** | Huddle sends nothing, and the agent starts in its own mode, which is Manual |
| **Manual** | The agent asks before it makes changes |
| **Accept edits** | The agent edits files without asking |

**Plan**, **Auto** and **Bypass permissions** are hidden, and `Team:Acp:HiddenModes` controls that list. An
Adapter with no modes shows no **Work mode** choice.

> [!NOTE]
> Work mode changes what the agent asks, not who answers. Huddle approves the agent's permission requests
> itself, so nothing here asks you before an ordinary tool runs. The two exceptions are the Agent's browser
> and your account connectors, which ask you on a card. A tool call that writes inside the agent's own
> `~/.claude` folder is always refused, though a shell command that redirects into it isn't caught.

### Browsing the web

Every Agent, including a new one, has a browser of its own. It lets the Agent read pages that need JavaScript and
work inside a site you signed in to for it.

- **Turn it off or on.** Open the Agent and find **Browser** under **Account connectors**. The switch acts at
  once, with no question and no **Save**. Duplicating a Team keeps a member's browser off in the copy, and
  **Reset to default** on the Chief of Staff turns it on.
- **The window.** The first time the Agent browses, a browser window opens on this computer. It is the Agent's own,
  never your everyday browser's profile, and you can watch it work.
- **Signing in.** When a site needs a login, the Agent stops and says which site. Type the password or code
  yourself in that window, then tell the Agent it is done. Never give an Agent a password or a code. The sign-in
  stays with that Agent and is not seen by your others.
- **What asks you.** Reading a page runs at once. Every click, typing, key press, form step, upload, dialog answer
  or script asks first, on a card in the Room that names the Agent and shows what it will do, such as the exact
  text to type. Choose **Allow once** or **Refuse**. There is no "always allow". After a refusal the Agent says
  what it wanted to do and does not retry.
- **Ten minutes.** A card nobody answers within ten minutes is refused, and so is every further step in that Turn.
- **Removing an Agent** deletes its browser profile and every sign-in in it. Renaming keeps them.
- **Work modes.** A Work mode that never asks before a tool runs (**Bypass permissions** or **Auto**) can't be
  combined with the browser. The switch shows off and says why.
- **Where it isn't available.** The switch is plain text for an Adapter other than Claude, and where no Edge or
  Chrome is installed. On Linux with no display the browser has no window, so you can't sign in for the Agent.
- **One Room at a time.** An Agent that browses in two Rooms at once can use the browser in only one. The other
  gets "Browser is already in use" until the first finishes.

The Feed records each time you turn the browser on or off.

### Using your account connectors

Your claude.ai account connectors (mail, calendar, files) are off for every Agent, the Chief of Staff included,
and an Agent can't turn them on for itself.

1. Open the Agent and find **Account connectors** under **Work mode**.
2. Turn the switch on. Huddle asks "Give {Name} access to all your connected accounts?" Choose **Give access**,
   or **Cancel** to leave it off.
3. The Agent restarts, and the change applies on its next Turn. The line under the switch says "On since" and the
   date.

Each use asks you first, on a card in the Room that names the Agent and the tool and shows what it asked for, as
text Huddle has not checked and that may come from an email it read. Choose **Allow once** or **Refuse**. There
is no "always allow". If you don't answer within ten minutes, press **Dismiss** or the Turn stops, the call is
refused, and so is every further connector call in that Turn.

Turning the switch off applies at once. Renaming an Agent, or using **Reset to default** on the Chief of Staff,
turns it off, so switch it back on afterwards. The switch appears only for the Claude Adapter. A Work mode that
never asks before a tool runs can't be combined with connectors: the switch stays off and says why. The Feed
records each time you turn it on or off.

### Keeping a list of what it told you

An Agent that reports to you keeps a file called `reported.md` in its own Memory, with one line for each thing it
told you: the date, the Room and a few words of what it said. Before it reports, it reads the file. If nothing has
changed, it says "no change since" a date in one line instead of repeating the item.

- **Old lines drop out.** Lines older than 14 days are removed, and the oldest go first if the file grows too big.
- **You can read and edit it** in the Library, like any Memory file.
- **The Chief of Staff reads a manager's file** before it summarises a digest, so it leaves out what the manager
  already told you. It reads no other file of that manager.
- **An Agent that never reports to you has none.** One that only does Task work for a manager gets no file.

The Agent keeps the file itself, so a report it forgot to note can come again.
### Choosing a model

Team types name a tier for each role (strongest, balanced or fast), and Huddle maps it to a model through
`Team:Teams:Tiers:<adapter>:<tier>`. With no entry, the Adapter's default model is used, and Build a team and an approved Proposal store that default model on the hire.

Haiku is the cheapest model. It suits Agents that answer questions, file what you tell them and do short
jobs. Choose a larger model for an Agent that manages a Team or runs long, many-step Tasks. The Chief of Staff is not hired for a role: it starts on Opus at medium effort, and a Model or Effort you choose
on its card is kept. On Haiku you
may notice:

| What you see | Why |
| --- | --- |
| A Huddle tool call is refused, then works on retry | Haiku loads a tool's description only when it first needs it. Each retry is one more call on your bill |
| A fact went nowhere although it said **Done** | A small model can report a refused call as done. Check the Feed or the Team's `memory/` folder |
| No offer to change `AGENTS.md` | Only an Agent with the `retrospective` Skill can propose a rule. Otherwise it tells you, and you add the line |

### Avatar details

- **Short labels.** Small avatars in rows and the sidebar show a label's first three characters.
- **Colours.** A colour can be changed to another swatch but not cleared. An image covers it, and the colour
  comes back if you return to initials or a label.
- **Saving.** While you create or edit an Agent, **Apply** holds the avatar until you save the card. On a
  viewed Agent, **Save** writes it at once.
- **Filtering the library.** Filter by skin tone, by text and by tags. Two tags show only pictures with both.
- **Your own library.** Set `Team:AvatarLibrary:Dir` to a folder with a `master.json` and pictures. A folder
  that doesn't exist turns the library off, and uploading still works.

### Agents hired from a role

These Agents have a **Complete setup** panel on their card:

- **Role**, with the version they were hired as.
- **Packs**, as chips that add or remove a named set of Skills.
- **Warnings**, such as a Reviewer on the same vendor as the Developer, too many Skills, or Skills that
  overlap. A warning stops nothing.

**Spent since start** counts the current run. **Total spent** is lasting, and shows on any Agent with stored
spend.

### `/compact` rules

- **Mention first.** A bare `/compact` is refused with *Unknown command*, even in a one-to-one Room.
- **Add guidance if you like:** `@nova /compact keep the decisions and the open questions`.
- **Only you can run it**, and only commands listed on the Agent's card work.
- **Details can be lost.** The Agent keeps a summary, so put lasting facts in Memory.
- **It costs a little.** A compaction is a model call that **Spent since start** doesn't show.
- **Just after a start,** it may be treated as an ordinary Message. Try again a moment later.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| An Agent forgot your conversation | It restarted after an edit, a membership change or **Restart**. Put lasting facts in Memory |
| An Agent file doesn't appear | See **Files that didn't load**. Common causes are a missing `name`, `title` or `alias`, a duplicate alias, or a `name` that doesn't match the file |
| All Agents show **Offline** | The agent host is off, for example with `--Team:Acp:Enabled=false` |
| **Offline** with *Claude needs you to sign in again* | Run `claude` in a terminal, sign in, and send the Message again |
| A browser step or connector call was refused | You chose **Refuse**, or nobody answered within ten minutes. Ask the Agent to try again and answer the card |
| The Agent says it has no browser, or the switch is plain text | **Browser** is off, the Agent isn't on the Claude Adapter, its Work mode never asks, or no Edge or Chrome is installed |
| **Browser is already in use** | The Agent is browsing in another Room. Wait for that Turn to end |
| The **Account connectors** switch won't stay on | Its Work mode never asks before a tool runs. Choose another Work mode first |
| A **Notice** says **Auto mode unavailable** | Your own Claude settings ask for auto mode, and the Agent's model doesn't have it. Choose the current **Haiku**, remove the setting, or set a **Work mode** |
