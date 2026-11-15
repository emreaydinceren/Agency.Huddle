---
status: accepted
date: 2026-11-14
---

# A built-in diagram is inlined by id and never served

> Amends [ADR-0019](0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md) ("Never SVG") and
> [ADR-0053](0053-an-upload-is-saved-to-the-library-and-sent-as-its-path.md) (SVG uploads are refused)
> for diagrams that ship inside Huddle, and only for them.

The Chief of Staff explains how to set up and use Huddle in the conversation itself, and a few concept
diagrams make that explanation land: the layers of the Second Brain, how Agents work together through
Tasks, the Human gate. Those diagrams have to read well under every Theme, light and dark, and follow
the Theme toggle in the top bar.

Chat renders an image only from a Library file through `/library-files/`, and that route serves only
bytes it has confirmed are PNG, JPEG, GIF or WebP, never an SVG. A raster diagram cannot follow the
Theme. An SVG served as a file and shown with `<img>` cannot either: it cannot read the page's CSS
variables, and its `prefers-color-scheme` follows the operating system, not Huddle's toggle. Serving
SVG as a document also reopens what ADR-0019 closed: an SVG can carry a `<script>`, and Huddle serves
files same-origin with no authentication.

## The decision

**A built-in diagram is an SVG embedded in the `Huddle.App` assembly, named by a catalog id, and written
inline into the page by the Markdown renderer. No SVG is ever served as a file.**

- **D-1. Named, not supplied.** Markdown names a diagram as an image whose address is `guide:<id>`, as in
  `![The layers](guide:layers)`. The renderer looks the id up in `GuideDiagrams`, the catalog built from
  `src/Huddle.App/Guide/guide.json` and the `.svg` files beside it, and writes that diagram's markup. The
  text of a Message or a note never reaches the page as markup: Markdig's `DisableHtml()` stays on, and an
  Agent can choose only an id.
- **D-2. An unknown id is text.** An id the catalog does not hold renders as the image's alt text, or the
  id when there is none. Nothing broken is drawn.
- **D-3. The Theme draws it.** A shipped SVG carries no colour, font or `style` of its own. Its elements
  carry `g-*` classes, and `wwwroot/css/guide.css` maps each class to a MudBlazor palette or typography
  variable, so every Theme and the accent colour reach the diagram with no change to it.
- **D-4. A test holds every shipped SVG to the rule.** `GuideDiagramSafetyTests` refuses a script, a
  `foreignObject`, an `image`, an `iframe`, a `style` element or attribute, an `on*` attribute, any `href`
  but a local `#` fragment, a colour attribute other than `none` or `currentColor`, and an `id` that does
  not start with the diagram's own id (two diagrams in one Message must not share an id).
- **D-5. Everything else keeps "Never SVG".** An upload, an avatar and a Library file are refused as
  before, and the ADR-0019 test that plants an `.svg` and expects a 404 is untouched, because no route is
  added.

## Consequences

- A diagram changes only with a release of Huddle, through code review, like any other embedded text.
- The same renderer draws Library notes, so a note can show a diagram too.
- The Chief of Staff learns which diagrams exist from an index in its Skill, kept in step with the catalog
  by a test. That index, and the diagrams beyond the first, are separate work.
- Screenshots of the UI are not diagrams. If any are kept, they are ordinary PNG files.
