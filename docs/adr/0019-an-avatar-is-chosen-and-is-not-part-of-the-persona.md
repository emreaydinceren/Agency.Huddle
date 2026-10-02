---
status: accepted
date: 2026-09-22
---

# An Avatar is chosen, and is not part of the Persona

> **Reverses one thing [Known limits](../engineering/known-limits.md) recorded as settled**: the
> transcript now shows an avatar beside every Message. Manual test ROOMMESSAGING-16 asserted the
> opposite — *"No avatar, circle or monogram appears anywhere in the transcript"* — and has been
> rewritten rather than deleted, so the reversal stays visible.

## The defect

A Teammate's avatar was the initials of its Name on the Theme's `Primary` colour, and nothing about
it was choosable. It rendered in exactly two places, both spelled
`<MudAvatar Color="Color.Primary">@TeammateCard.Monogram(name)</MudAvatar>`, both calling one
`internal static` helper that lived on a Razor component and was already being reached across from
`Teammates.razor` — the same "one place should own a domain-value-to-rendering mapping" smell
`StatusDot` was extracted to fix for `PersonaState`.

Two Teammates whose Names share a first and last initial were visually identical, and `app.css`
admitted the gap in the comment above `.teammate-avatar`: *"The monogram stands in for Slack's
avatar photo."*

## The decision

**An Avatar is three optional fields, and absence is the default.**

```csharp
public sealed record Avatar(string? Label, string? Image, string? Background);
```

Rendering picks `Image`, then `Label`, then a monogram derived from the Name, stopping at the first
one present. All three `null` is today's rendering exactly, so **"Initials" is not a stored kind —
it is the absence of an override**. That is the same shape `hooks.json` and `appearance.json`
already use: an absent file, or an absent field, means the default applies, not that a value called
"default" was written down.

`Background` sits outside that three-way choice and is orthogonal to it. A transparent PNG wants a
background, and initials want one most of all.

### Why not an enum plus a payload

`AvatarKind` + payload was the obvious alternative and is worse in three specific ways:

- It needs a discriminator that **can disagree with its payload** — `kind: image` with no file. The
  resolution rule that fixes that is precedence, written a second time.
- It makes "no customisation" a written value, so every Teammate acquires a row and the
  absent-is-normal tolerance is gone.
- A fourth kind later becomes a schema change rather than a fourth nullable field.

The objection to the shape chosen is that it can hold a `Label` *and* an `Image` at once. That is
not a defect here: precedence makes the pair well-defined, and it is what lets the editor switch
Label → Image → Label without destroying the text the Human typed thirty seconds ago.

## Why it is not in the Persona's frontmatter

Frontmatter was the obvious home — it is where `adapter:` lives, `PersonaFrontmatter.WriteScalarField`
already rewrites one key in place, and it would travel with the `.md` file. It is wrong for four
reasons, and the first alone decides it.

**1. It would restart the session and destroy the Agent's memory.**
`PersonaSupervisor.NeedsRestart` is `persona != started` — whole-record value equality — and
`Persona.Text` is the full raw file text, frontmatter included. So an `avatar:` key means **picking
a background colour stops a live ACP session and throws away everything that Agent remembers.**
[Rules](../engineering/rules.md) already settled this exact question for Hooks: *"Restarting would
make every edit land immediately and throw away that Agent's conversation memory every time someone
reworded a sentence."* That sentence is, word for word, the argument here.

**2. `JobDescriptionExcludedKeys` is an opt-out list.** Every top-level frontmatter key that is not
explicitly excluded becomes one title-cased line of the job description `mcp__team__list_agents`
sends to the model. A forgotten line would put `Avatar Color: #4a154b` into model-facing text, which
[ADR-0007](0007-model-facing-text-is-configuration.md) forbids — a permanent trap one edit away,
forever.

**3. [ADR-0013](0013-an-adapter-is-a-property-of-the-persona.md)'s own test excludes it.**
Frontmatter holds what changes *how a Teammate runs*. The Model and the Effort do, and even they
live in SQLite rather than in the file. An Avatar changes nothing about the Agent.

**4. It would put an image file name inside a system prompt.**

So the Avatar lives in `{DataDir}/avatars.json`, a sibling of `hooks.json` and `appearance.json`,
read through `AvatarStore` — `AppearanceStore`'s sibling, with the same volatile snapshot, single
write lock, debounced `FileSystemWatcher` and wholesale fallback on a malformed file.

The cost, accepted: **an Avatar does not travel with a copied `.md` file.** Neither does that
Persona's Model or its Effort, and nobody has asked for those. It is recorded in
[Known limits](../engineering/known-limits.md).

## Why the key is a Teammate's Name

Keying the store by Name rather than by Persona buys three things that would each have been separate
work:

- **The Human fits with no special case.** It has no Persona file, but it has a Name
  (`Team:HumanName`, default `You`).
- **The transcript needs no new plumbing.** `ChatMessage.SenderName` and `Draft.SenderName` *are*
  that key, so a message row needs no path back to a `PersonaEntry`.
- **A raw pipe client gets an avatar for free**, without ever being a Persona.

A rename therefore has to move the key. It hangs off `PersonaStore.PersonaRenamed` and **not** off
`PersonaStore.Update`, for two reasons: `Update` would give `PersonaStore` a reference to an
avatars-layer store, breaking the decoupling [Rules](../engineering/rules.md) praises; and `Update` is
only one of the two rename paths — a Human editing `name:` in an editor reaches the watcher and the
event, never `Update`.

**The move is the first statement in `OnPersonaRenamed`, above the "no Agent row" early return, and
that placement is the single subtlest line in the feature.** An Avatar exists whether or not an Agent
has ever connected. `Team:Acp:Enabled` is `false` by default, so in a stock installation *no*
Teammate has an Agent row — put the move below the guard and renaming a Teammate silently loses its
face, in the default configuration. A test pins it, and was confirmed to fail when the line is moved.

A removal takes the Avatar and its image file with it, for the reason
[Rules](../engineering/rules.md) gives for the Model and the Effort: leaving per-Persona state behind
would silently resurrect it on a later Persona that reused the Name. `PersonaStore` gained a
`PersonaRemoved` event for this, raised from **both** doors `PersonaRenamed` is raised from — the
in-app path and the watcher — because a `.md` deleted in an editor is noticed only by the second.

## Images

- **PNG, JPEG and WebP, identified by magic bytes**, never by the filename extension and never by
  `IBrowserFile.ContentType`; both are client-supplied and trivially forged.
- **Never SVG.** It is a document format that can carry a `<script>` element, and this application
  serves files same-origin with no authentication of any kind. The same class of reasoning
  [Rules](../engineering/rules.md) gives for never enabling `UseAdvancedExtensions()` in the markdown
  pipeline.
- **512 000 bytes**, which is Blazor's own default `maxAllowedSize` for `IBrowserFile.OpenReadStream`
  rather than an invented number. Enforced twice — on `file.Size` before opening, and on the stream
  itself, so a lying `Size` still throws.
- **No decoding, no dimension cap, no resizing.** Each needs an image decoder, which means a new
  package and a decompression-bomb surface. The browser scales the image down with CSS.

**The file name is an opaque generated id, never the Teammate's Name.** Three concrete defects avoid
themselves: a Name may contain interior spaces, so a Name-derived URL would need percent-encoding at
every call site; [Known limits](../engineering/known-limits.md) records that Windows reserved device
names (`CON`, `NUL`, `COM1`) pass `NameRules`, and `CON.png` is a file Windows will not create; and an
opaque id means a rename touches **no file at all** and gives cache-busting for free, because a
replaced image gets a new id and therefore a new URL.

### Why a file and an endpoint, not a data URI

A base64 `data:` URI needs no middleware, no provider, no cache story and no rename or removal
cascade, and was seriously considered. It loses on payload: 512 KB of image is about 683 KB of
base64, and Blazor Server sends that down the SignalR circuit. Ten Teammates on `/teammates` is
roughly 6.8 MB on a page that currently paints in one small diff, and nothing is ever cached by the
browser.

So images are served by **`UseStaticFiles` with a `PhysicalFileProvider`** at `/teammate-avatars`.
`MapStaticAssets` cannot serve them: it is manifest-driven and knows only build-time assets, so a
file written at run time is a 404 through it. Roadmap item 7 reached the same conclusion for imported
Themes and named this mechanism first.

The content-type provider enumerates **exactly three** types rather than filtering. That is not
belt-and-braces: `.svg` is a *known* type to the default provider, so `ServeUnknownFileTypes = false`
alone would happily serve one that reached the folder by any other route. A test plants an `.svg`
and asserts both the 404 and that the file genuinely exists, so the refusal cannot pass for the
wrong reason.

## Colour

A background is real per-Teammate data, so it cannot live in a stylesheet — [Rules](../engineering/rules.md)
confines colour literals to `MainLayout.razor.css` and `ThemeSourceTests` fails the build on any
other. It is therefore emitted as an inline `style` built from a **C# string**, and a new test,
`RazorComponents_NoInlineColourLiteralInStyleAttribute`, enumerates `Components/**/*.razor` and fails
on a colour literal in any `Style="…"` attribute, with **no exemption list**. It passed the day it
was written; its value is preventing the regression, and it was confirmed to fail against a planted
violation.

**The foreground is pure black or pure white, and that is proven sufficient rather than convenient.**
Contrast against black clears WCAG 1.4.3's 4.5:1 exactly when the background's relative luminance
`L >= 0.175`; against white, exactly when `L <= 0.18333…`. Those intervals **overlap**, so no sRGB
colour exists for which neither clears. `ContrastColourTests` mechanises precisely that claim by
sweeping the sRGB cube rather than spot-checking. Black wins an exact tie, matching the
`PrimaryContrastText = "#000000"` `HuddleTheme` already chose for this monogram.

`ContrastColour` **replaced** the private contrast helpers that had been living in
`ThemeCatalogTests`; a second copy under `src/` would have broken the one-pure-function-owns-it rule
that `StatusDot`, `ReplyGate` and `PersonaStatusResolver` all rest on. Every contrast floor in that
suite is unchanged.

## What was given up

- **An Avatar does not travel with a copied `.md`.** Neither do the Model and the Effort.
- **No resizing and no dimension cap**, so a 512 KB image stays 512 KB on disk.
- **An emoji outside the Basic Multilingual Plane is stored escaped.** `System.Text.Json` escapes
  surrogate pairs whatever `JavaScriptEncoder` it is given — verified at the byte level, not assumed
  — so `🦊` is written as `🦊`. It round-trips exactly and a Human may type the literal
  character in; only the file's readability suffers.
- **An orphaned entry is never pruned.** A Persona whose `.md` is deleted while the app is not
  running leaves its entry behind, because nothing observed the deletion. Tolerated deliberately,
  the same way `appearance.json` tolerates a `theme` id naming nothing.
- **`RoomList` has no avatar.** A Room is named after *all* its Agent Members, and `Room` carries no
  member list, so no single Teammate is recoverable from a sidebar row without new per-row queries. A
  wrong face on a Room is worse than none.

## Existing files

There is no migration and none is needed. `avatars.json` does not exist until the first avatar is
saved, and an absent file means every Teammate renders exactly as it did before this change — which
is also what makes the upgrade invisible to an installation that never opens the new controls.
