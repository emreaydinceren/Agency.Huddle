# Rooms and chat

A **Room** is a conversation between you and one or more Agents. This page shows what you can do in
one. For step-by-step help, see the [User Guide](../../Huddle.UserGuide.md#chatting-in-rooms).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Decide who answers

| Where you are | Who replies |
| --- | --- |
| A one-to-one Room | The Agent answers everything |
| A group Room | An Agent answers only when mentioned |
| A group Room, using `@all` | Every Agent in the Room |
| A Room an Agent follows | That Agent can be woken without a mention |

Add someone with `/invite @alias` or the **Invite agent** button. When a Message wakes nobody, the
Room says why instead of staying silent.

## Point at things without copying them

- **`@`** picks an Agent, a Room, a Team or a Library file. Type `@room:` or `@lib:` to narrow it.
- **`#`** picks a Task.
- **Copy link** on a Room makes the same kind of reference.

A sent reference remembers what it pointed at, so it later shows a **changed** or **(unavailable)**
chip when the target moves on.

## Attach files

Use the paperclip, paste, or drag and drop. Each file is saved to the Library straight away, and an
Agent can open it from the path in your Message. A Library image you name reaches the Agent as an
image, so it can see it.

Limits: 25 MiB per file and 10 files per Message. SVGs and programs are refused.

## Watch an Agent work

- **Replies stream** as they are written, and **Stop** cancels a Turn.
- **Tool calls appear as rows** with the file and line. An edit row opens an **Edit preview**.
- **A failed Turn says so** in the Room.
- **Spend appears on the card**, per currency.
- **Context fill appears in the Members panel**, as a percentage and a bar that turns amber at 75%
  and red at 90%.

## Answer an Agent's questions

An Agent can ask you a question with options, drawn as a card in the Room. Your answer comes back to
it as a Message. Questions, retry dialogs and forms from its tools use the same kind of card. A question
with option buttons is still waiting if Huddle restarts.

## Keep long chats under control

- **Budget.** After a set number of Agent replies without you, a Room pauses and offers **Continue**
  or **Leave paused**. Your next Message resets it, and Messages that were waiting are answered.
- **Sessions.** Each Agent keeps a separate conversation per Room and catches up from the transcript
  when it needs to. **Settings > Sessions** changes how much it rereads.
- **Start afresh.** Gives an Agent a new session in this Room and shows **See what it reread**, the
  exact text it was sent.

## Pick up where you left off

- **Nothing you sent is lost.** A Message an Agent hadn't answered yet survives Huddle closing or crashing. On
  the next start the Agent answers it, and the Room says "Picked up after a restart."
- **Waiting is visible.** A row such as "Waiting for Nova: 2 Messages" has **Don't answer** to call them off.
- **Quick Messages get one reply.** In an Agent's one-to-one Room, several Messages sent while it is busy are
  answered together.
- **Agents can come back.** An Agent that says it will check something later sets a **Follow-up**. The Room
  shows it with **Cancel**, and at that time the Agent picks the conversation up again.

## Work in Team Rooms

Every Team has a **Team Room** holding you and all its members. It is created with the first member.
A member's **Message** button opens that Team's own one-to-one Room, labelled with the Team's icon and name
and listed under **All**.

## Good to know

- **Archive** hides a Room, and it still works. **Delete** permanently removes the transcript, after
  a confirmation.
- Half-written Messages are kept per Room.
- Team Rooms can't be renamed.
- The Chief of Staff can search every Room.

## In more detail

### How a Message is handled

1. Huddle appends your Message to the Room's transcript, which is never rewritten.
2. Each Agent who should answer takes a Turn. An Agent runs one Turn at a time across all its Rooms.
3. At the start of the Turn, the Agent hears which files changed in its folders. See
   [Second Brain and Memory](second-brain-and-memory.md#file-changes).

Agents can also reply to each other, which is why Rooms have a Budget.

### Messages that wait

A Message waits when its Agent is busy, offline, or in a Room whose Budget is spent. Waiting Messages are kept,
so they survive Huddle closing, crashing or the computer restarting.

| Situation | What happens |
| --- | --- |
| Messages are waiting | The Room shows "Waiting for Nova: 2 Messages". **Don't answer** calls them off before they cost anything |
| An Agent is already answering | Its **Stop** button also calls off what waits behind that Turn |
| Huddle starts again | Each Agent answers its waiting Messages once it is online. The Room says "Picked up after a restart." and lists what is waiting and for whom |
| A Turn was cut off half way | It runs once more, and the Agent is told to check what it already did. A reply is never posted twice |
| A Message is older than a day | It isn't picked up, and the Room says so. Team:TurnQueue:ResumeWithin changes the limit |
| You had stopped a Turn | It stays stopped |
| The Room's Budget is spent | Waiting Messages are answered when you press **Continue** or type in the Room |

A Question an Agent asked with option buttons is still waiting after a restart.

### Quick Messages get one reply

In an Agent's one-to-one Room, Messages you send while the Agent is still busy are answered together, in one
reply, instead of one reply each. This also applies to Messages picked up after a restart. A Room with more
Agents answers each Mention on its own. A command such as @nova /compact ends the group, so Messages before
and after it are separate Turns.

### Follow-ups

When an Agent says it will come back to something ("I'll look at the build in an hour"), it can set a
**Follow-up**. The Room shows a row such as "Nova will follow up Mon 16 Nov, 15:40: Check the build" with
**Cancel**. At that time Huddle mentions the Agent in that Room and it carries on the conversation.

- Follow-ups are also listed under **Scheduled**, where **Delete job...** cancels one.
- An Agent can hold 5 Follow-ups at a time. Team:Jobs:MaxFollowUpsPerAgent changes that.
- A Follow-up due while its Room is paused waits until you type or press **Continue**.
- One that is more than a day late is dropped, and the Feed says it did not run.

### Sessions and catch-up

| Rule | Default |
| --- | --- |
| A session closes after | 30 minutes idle |
| Sessions open per Agent | 3 at most |
| A catch-up rereads up to | 20 Messages and an estimated 100,000 tokens |
| When over the limit | The Agent's own older Messages go first, and its newest 5 stay |

A closed session resumes where the Adapter supports it, and otherwise starts fresh with a catch-up.
**Settings > Sessions** changes the Message and token limits, from the next fresh session and without a
restart.

**Start afresh** applies between Turns only. Huddle offers it only for an Adapter with one session per Room.

### Using the reference picker

The `@` list opens at the start of a line, after a space or after an opening bracket, so an email address
doesn't trigger it. A bare `@` lists the Room's Members first.

| Key | What it does |
| --- | --- |
| Up and Down | Move the highlight, wrapping at the ends |
| Enter or Tab | Pick the highlighted row |
| Esc | Close the list and keep your text |
| Ctrl+Z, after a pick | Undo the pick and restore what you typed |

Enter never sends while the list is open. The **Send** button always sends your text as typed.

| You pick | The Message gets |
| --- | --- |
| An Agent | `@` and its name, addressed as if you had typed it |
| A Task | Its id, shown as a link with a status dot |
| A Room or a Team | A link |
| A Library file | Its path, the same text an attached file produces |

An Agent outside the Room is shown in grey and marked **Not in this Room**, so invite it first.

| Chip | Means |
| --- | --- |
| **changed** | The item was renamed or retitled. The Message keeps the old name |
| **(unavailable)** | The item is gone, such as a deleted Room or a moved file |
| None | The item is unchanged. An archived Room still exists, so it shows no chip either |

A Task id or path that names nothing when you send stays plain text. Agents are told what a reference is
so they can read it. Set `Team:References:ExpandInPrompt` to false to stop that.

### Attaching files

| Case | What happens |
| --- | --- |
| A file over the limit, or one too many | An error notice appears and no chip is added |
| An SVG or a program | The chip stays in the error colour with the reason. Huddle checks contents, not the name |
| You remove a chip before sending | The saved file is deleted and nothing is posted |
| You send while a file is uploading | Send waits, and your text stays in the box |
| You reload with chips pending | The chips are lost. Files already saved stay in the Room's folder |

- **Where files land.** A Team Room uses the Team's `raw/` folder. Other Rooms use the **Raw** root, one
  folder per Room.
- **Who is told.** The Message carrying the path is the only notice. File Changes doesn't report uploads.
- **Without a Library.** The paperclip, paste and drop appear only when the Library is on.

### Images and PDFs

A Library image you name reaches the Agent as an image. A PDF goes as a document only to an Adapter that
can't read files and accepts embedded documents, with limits of 2 PDFs and 10 MiB per Turn. Claude can read
files, so for Claude a PDF stays a path and it opens the file itself.

### Reading the conversation

- Your Messages sit on the right in a tinted bubble. An Agent's text sits beside its avatar with no card.
- A separator marks each day, and consecutive Messages from one Agent share a name and avatar.
- A Task change that wakes an Agent shows as one slim line. Select it to see the changes.
- The composer starts at 3 lines and grows to 18.

Tool-call rows show the last six calls with **Waiting**, **Running**, **Failed** or done. They aren't saved,
and disappear when the reply finishes or the app restarts.

### When an Agent says nothing

An Agent with nothing to add replies `NO_REPLY`, and Huddle posts nothing. Every Agent is also told that a
promise of later work needs a way back, such as a Task, a Job, a Follow-up or a Mention. Otherwise the work is done now.

### Renaming, archiving and deleting

| Action | Result |
| --- | --- |
| **Rename** | A name you type is kept even when members change |
| **Archive** | The Room moves to the **Rooms** tab of the **Archived** page. It still works and can still be woken, and its files stay |
| **Delete** | The transcript is permanently deleted, and the Room's **Raw** files go to the Recycle Bin |

> [!WARNING]
> Deleting a Room can't be undone, because the transcript skips the Recycle Bin. Archive it if you might want
> it later.

### Team Rooms in depth

- **New Rooms.** Use **New room** in a Team's More (⋮) menu. Its members are listed first, and you can still
  select an Agent from another Team.
- **The Team Room.** A later joiner is invited automatically. Leaving a Team doesn't remove an Agent from
  the Room.
- **Two one-to-ones.** A Team's one-to-one is separate from the one under **Rooms**, so they don't mix. It reads
  like "Rhea · Development Team", with the Team's icon.
- **Digests and retros.** These post in the Team Room, or in the Agent's one-to-one if the Team Room is
  archived.
- **When a Team goes away,** its Rooms are archived with their history.

### If something goes wrong

| Symptom | Cause and fix |
| --- | --- |
| `@nova /compact` got an ordinary reply | The Agent doesn't offer that command, wasn't online yet, or another Agent wrote it |
| `/compact` says *Unknown command* | Write the mention first: `@nova /compact` |
| Nobody replied | The Room says why. In a group Room, mention an Agent or use `@all` |
| A Message was not answered after a restart | It was more than a day old, and the Room says so. Send it again |
| "Waiting for Nova" stays on screen | Nova is offline or busy, or the Room's Budget is spent. Press **Continue**, or **Don't answer** to drop them |
| Agents stopped replying to each other | The Room reached its Budget. Press **Continue** or post a Message |
