# Settings and appearance

Make Huddle look the way you like, see what it costs, and change the text it sends to models. For
step-by-step help, see the [User Guide](../../Huddle.UserGuide.md#settings).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Pick a Theme

**Settings > Appearance** offers four Theme sets, each in light and dark: **Lumen** (the default),
**Fluent**, **Nordic** and **Cobalt Soft**. Choose one light and one dark Theme plus an accent colour. The
Top Bar button switches between them.

## See what you've spent

**Settings > Spending** shows what the Adapters reported: a line per Team, the Chief of Staff, each Agent
in no Team and the Helpers, then a **Total**. Currencies are never added together.

This page reports only. It sets no budgets or limits.

## Change what Huddle says to models

Every piece of text sent to a model can be changed in **Settings > Prompts**, and changes apply without a
restart. Each change is one file in the **Prompts** root, and deleting the file restores the default.

## Find the rest

| Section | What it holds |
| --- | --- |
| **Profile** | Your name, alias, title, description and avatar |
| **Prompts** | The text sent to models |
| **Helpers** | Each Helper's on/off, Adapter, Model, Effort and daily cap |
| **Spending** | Reported cost |
| **Appearance** | Themes and accent colour |
| **Personas** | Agent definitions |
| **Skills** | Skills, Packs and Sources |
| **Team types** | Built-in and custom types, and any **Problems** |
| **Library** | Pinned folders |
| **Sessions** | How much an Agent rereads when it catches up |
| **Remote access** | Devices and connection settings |

## In more detail

### Your profile

A Name or Alias an Agent already uses is refused. Renaming yourself moves your avatar and Tasks with you, and every
Message you ever sent shows the new Name without rewriting the transcripts. A blank system opens on this page until you
save a profile.

### Themes

A Theme is a JSON skin of palette, tokens and structural traits, so adding one means editing data. The picker lists
Light Themes and Dark Themes separately. Older saved Theme choices map to Lumen.

### Spending in depth

- A line can appear when an Agent's spend counts in more than one Team, because **Total** counts every Agent once.
- The panel covers only what the Adapters report.
- Removing an Agent removes its spend from the panel.
- For one Agent, see **Spent since start** and **Total spent** on its card. For a Team, see **Spent** on its page.

### Prompts in depth

**Reset all to defaults**, beside **Save**, asks first and restores every prompt. A changed prompt is a file such as
`Prompts/systemPrompt.chatRules.md`. Helper prompts are listed under **Helpers**, and an edit applies from the Helper's
next call.

To see which prompts reach an Agent, use **What this Agent reads** on
[its card](agents.md#look-after-a-running-agent).

### Sessions

This section sets how much a fresh session rereads of a Room: a limit in Messages and an estimated limit in tokens. See
[Rooms and chat](rooms-and-chat.md#sessions-and-catch-up).

### Where each setting is saved

| Section | Saved to |
| --- | --- |
| **Profile** | `profile.json`, with the avatar in `avatars.json` |
| **Prompts** | One file per changed prompt in `Prompts/` |
| **Helpers** | `team.db` |
| **Appearance** | `appearance.json` |
| **Personas** | `Agents/…` |
| **Skills** | `Skills/…` and `SkillSources/` |
| **Team types** | `TeamTypes/…`. This section is a read-only list with **Reload** |
| **Library** | `library-roots.json` |
| **Sessions** | `session-settings.json` |

### Other switches

Some features are turned on or off in configuration under the `Team:` root, such as `Team:Feed:Enabled`,
`Team:Jobs:Enabled`, `Team:Library:Enabled` and `Team:Tasks:Enabled`. A feature that's off also disappears from the
Activity bar or its page. For every key, see the [Engineering Guide](../../Huddle.EngineeringGuide.md).
