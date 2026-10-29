---
status: accepted
date: 2026-10-10
---

# An Agent's avatar is edited in its own dialog, opened from the avatar

> **Builds on [ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) and
> [ADR-0044](0044-an-avatar-library-is-a-read-only-source-and-a-pick-copies.md).** Nothing they
> decided changes. An Avatar is still three optional fields held apart from the Persona, a pick
> still goes through the upload path, and nothing is written to disk before there is a Name to file
> it under.

## The defect

The Agent card carried the whole avatar editor inline: a radio group, a label box, an upload
button, the library button, a colour picker and its reset. It was a panel of its own, shown in Create
and Edit and hidden in View, so an Agent in View had an avatar the Human could not change without
first pressing **Edit**. The card was also long enough that the avatar controls sat below the model, effort and work
mode selects, far from the picture they change.

## The decision

**The avatar on the card is a button. Pressing it, in View, Edit and Create alike, opens
`AvatarEditorDialog`, which holds the whole editor. The card has no inline avatar controls.**

The dialog works on a copy and returns an `AvatarEditorResult` only when the Human commits. It never
writes to disk. What a result means is the card's decision, and it depends on the mode:

| Mode | Last button | What the card does with the result |
| --- | --- | --- |
| Create, Edit | **Apply** | Holds it. The card's own avatar shows it at once, but nothing is written until the card's **Save** or **Add agent**. The card's **Cancel** throws it away. |
| View | **Save** | Writes it at once. View has no Save of its own, so holding the result would lose it. |

An image that was chosen but not written has no URL a browser could fetch, so the card and the dialog
show it as a `data:` URL built from its own bytes (`AgentAvatar.PendingImage`). The bytes travel
only under the **An image** choice, so uploading a picture and then choosing a label files no orphan
file.

**The Background controls are hidden under An image.** An image covers the background, so a colour has
nothing to do there. Only the controls go: a stored colour is left exactly as it was and is there
again on a return to initials or a label.

### Decisions

| # | Decision | The alternative it beat |
| --- | --- | --- |
| D-1 | **Apply buffers in Create and Edit; Save writes at once in View.** | Writing at once everywhere. Create has no Name to file an image under until it is saved, and an Edit whose Cancel keeps an avatar change is a surprise. |
| D-2 | **The dialog never writes; the card does.** | The dialog writing through `AvatarStore`. It would need the Name, the mode and the persisted-name rule that `AgentProfileCard.SaveAsync` already owns, and a second place to get a rename wrong. |
| D-3 | **A pending image is previewed as a `data:` URL.** | A text line only, as before. With the controls behind a dialog, an Apply that changes nothing visible on the card reads as broken. The size is bounded by the 512 000-byte upload cap. |
| D-4 | **Hide the colour controls under An image; keep the stored colour.** | Clearing the colour when an image is chosen. That throws away a choice the Human may want back on switching to initials. |
| D-5 | **No backdrop click on the editor.** | The default. One click outside would discard a half-made choice with no way back. Escape, **Cancel** and the close button still leave. |
| D-6 | **`Settings → Appearance` keeps its own inline editor.** | Opening this dialog from there. The Human's avatar is written at once on that page by design (L11), which is the one thing the dialog's two modes do not offer. |
| D-7 | **The Background is a palette of swatches, not a colour field.** The swatches are `AvatarPalette.Colours`, a fixed set built from MudBlazor's tint/shade, analogous and gradient generators. | A hex text field and a colour-picker popup, which let a Human type a colour that clashes with the Theme. A fixed palette keeps every avatar colour a deliberate one. A colour already in `avatars.json` that is not in the palette keeps showing, and highlights no swatch. |
| D-8 | **A card that is creating draws a random colour from the palette when it opens.** It is held like any applied avatar and written with the Agent on **Add agent**; a cancelled card writes nothing. | Drawing it at save, which would show a different colour from the one the Human saw. Drawing it for every Agent that has none, which would recolour avatars that were deliberately left on the Theme's colour. |
| D-9 | **The card's editor has no "Use the theme colour" action, so a colour can be changed but not cleared.** | Keeping the action. Every Agent created from the card now starts with a colour (D-8), so "no colour" stops being the normal state, and a button that returns to it offers a way back to the one look the Human can no longer start from. Clearing a colour is still possible by editing `avatars.json`, and the Appearance page keeps its own action for the Human's own avatar. |

## Consequences

- The card shrinks by its largest panel, and the avatar is the one way in, so its pencil and focus
  ring are the only cue and carry the whole discovery. They are pinned by a stylesheet rule and a
  manual test.
- Three dialogs can now be open at once (card, **Avatar** window, library picker). Escape closes the
  top one only; [AVATARLIB-23](../engineering/manual-tests/avatar-library.md) walks the stack.
- `AgentAvatar` gains `PendingImage` and `PendingImageExtension`, and `app.css` scales an image
  under `.mud-avatar` to its circle. MudBlazor sizes only its own `MudImage`, so a bare `<img>` of
  256 px sat at full size inside a 56 px avatar and showed a corner of the face. That was true of
  stored images too.
- The two editors, this dialog and `Appearance.razor`, still duplicate the upload validation. Folding
  them together is possible once the Appearance page can take the dialog's deferred-or-immediate
  switch; it is not done here.
