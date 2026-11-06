# Feed and notifications

See what your Agents and Huddle have been doing without opening every Room. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#feed).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Check the bell

The **Notifications** bell in the Top Bar counts items that need you and opens the **Notifications** panel.
Its categories are **Needs you**, **All**, **Tasks**, **Documents**, **Rooms**, **Gates** and **Helpers**.

## Catch up with the Feed

The **Feed** tab shows cards by day: Tasks created, Rooms started, documents changed, and asks raised and
resolved.

- **Filter** by Team and Agent.
- **Expand** a card for a preview.
- **Stay current** with a live "↑ n new items" pill.

## See what Helpers did

A Helper's gate change appears as a Notification with its reason. If a Helper pauses at its cap or keeps
failing, Huddle reports it in the **Helpers** category.

## Good to know

- The Feed never shows Message text and never changes the work.
- Items are kept for 30 days.
- Setting `Team:Feed:Enabled` to false turns the Feed off.

## In more detail

### What rings the bell

- A Task assigned to you
- A gated Task waiting for your approval
- A Mention, a Question or a Form
- A Room that reached its Budget
- A Request only you can confirm
- A Task move that a gate stopped

An item you caused yourself never rings the bell.

### How Notifications behave

| You do this | What happens |
| --- | --- |
| Open the bell | What it shows is marked seen and the number goes away. The items stay |
| Press ✕ on a row | It's hidden from the bell and stays in the Feed |
| Select **See all activity** | The Feed tab opens on the same category |

A Notification fades and leaves **Needs you** once its reason is gone, such as when you post in the Room, accept the
Task, or the Task is reassigned.

### Reading a Feed card

A card shows who did what, a chip for the Task, Room or document it's about, the Team, and for a Notification a pill
such as "Waiting for you". A thin bar at its left edge marks items you haven't seen, and clears next time you open the
tab.

Press the chevron or the card's empty space to expand it. You get a preview, the other items about the same thing
under "In the Feed", and **Open Task**, **Open Room** or **Open document**. **Review in Needs you** appears for a gated
Task that's waiting. These buttons only open things, so deciding and replying stay in **Needs you** and the Room.

### Filtering

- **Categories:** **All**, **Needs you**, **Tasks**, **Documents**, **Rooms**, **Gates** and **Helpers**, each with a count.
- **Team:** the **All teams** select narrows the list to one Team.
- **Agent:** click an Agent's avatar to see only its items. The tab remembers this in its address.

### Helper items

| Event | What you see |
| --- | --- |
| **Needs Human** turns the gate on | A Notification with its reason, until you turn the gate off or decide the Task, it waits in **Needs you**, or it closes |
| A Helper pauses at its cap, or fails three times in a row | **Huddle** says so under **Helpers**, linking to its card on **Settings > Helpers** |
| **AGENTS.md Changes** skips a small edit | A line saying it didn't tell Agents, with the reason |
| **Team Memory** doesn't file a proposal | The reason, under **Helpers** |

A Helper's wake-up Message is written by **Huddle**, so it doesn't answer a Mention of you or clear a Room from **Needs
you**.

### Keeping and turning off

Items are kept for 30 days, and a dismissed Notification for 30 days after you dismissed it. With the Feed off, the
bell and the **Feed** icon disappear.
