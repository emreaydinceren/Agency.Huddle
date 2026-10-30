# Front matter properties

**Status:** designed 2026-10-29, not built · **Extends:** [Library](Huddle.Library-Specifications.md) §6.17 (tags) and the
`L3e` row of [wireframes.md](engineering/wireframes.md) · **Vocabulary:** [language.md](engineering/language.md)

This spec makes the Library **read and show a note's front matter properties exactly as the reference notes app does**:
the same types, the same type for the same key across a root, the same links, the same reserved keys and the same
handling of what it does not support. It is for whoever builds or reviews the change to `LibraryProperties`, its reader
and the link index.

**The goal is full read compatibility.** A folder of notes written by the reference app, pinned as a Library Root, must
show every property the way that app shows it, and every link a property makes must resolve and count the way that
app's do. **Editing is out of scope:** Huddle never writes front matter (ADR-0028), so where the reference app offers a
control, Huddle draws the value it would hold.

**What is documented and what is not.** The reference app's help documents the types, the YAML syntax, quoted links,
the reserved keys and the deprecated ones. Every rule marked **(V)** below is not on its help pages and comes from
observed behaviour. Each must be checked against a reference vault before it is built (FP-M1), and a disagreement changes
the rule and its test. The same practice governs tags (§6.17, LB-M2).

## Contents

- [The example](#the-example)
- [Scope](#scope)
- [Vocabulary](#vocabulary)
- [What happens today](#what-happens-today)
- [Decisions](#decisions)
- [Figure: the Properties block](#figure-the-properties-block)
- [Behaviour](#behaviour)
- [Configuration](#configuration)
- [Seeded demo](#seeded-demo)
- [What changes in code](#what-changes-in-code)
- [Tests](#tests)
- [Open points](#open-points)

## The example

Every rule below is checked against this note. It is the one end-to-end case, and the test fixture of the same name
(`AllTypes.md`) is this text. The root's types file assigns `pages` the Number type and `due` the Date type; every other
key has no assigned type.

```yaml
---
title: Project Plan
url: https://example.com/plan
pages: 320
reviewed: true
created: 2024-01-15
logged: 2024-01-15T09:00:00
due: next week
owner: "[[Architecture]]"
authors:
  - Ada
  - "[[Architecture]]"
tags:
  - recipe
  - cooking
aliases: [Plan, Q1 plan]
cssclasses:
  - wide
budget:
  amount: 1200
  currency: EUR
notes: |
  First line.
  Second line.
empty:
---
```

What the Properties block shows for it:

| Key | Type | Shown as |
| --- | --- | --- |
| `title` | Text | `Project Plan` |
| `url` | Text | A link to `https://example.com/plan` |
| `pages` | Number (assigned) | `320` |
| `reviewed` | Checkbox | A ticked, read-only checkbox |
| `created` | Date | `15 January 2024` |
| `logged` | Date and time | `15 January 2024 09:00` |
| `due` | Date (assigned) | **Type mismatch:** `next week` with a warning mark (D-3) |
| `owner` | Text | A Library link to the note `Architecture` |
| `authors` | List | Chips: `Ada`, and a link chip to `Architecture` |
| `tags` | Tags | Chips `recipe`, `cooking`, read by `NoteTagReader` (§6.17, unchanged) |
| `aliases` | Aliases | Chips `Plan`, `Q1 plan`; the note is now also found as `Plan` (D-7) |
| `cssclasses` | List | Chips `wide`; no style is applied |
| `budget` | Unsupported | Its YAML source, in a monospace block |
| `notes` | Text | Two lines, as written |
| `empty` | Text, empty | An empty value |

## Scope

| In scope | Out of scope |
| --- | --- |
| A Library-only reader that turns a front matter block into typed properties | Editing a property, or any write to a note or to the types file (ADR-0028) |
| The root's types file: one type per key across the root, read and watched | Creating or changing the types file |
| Type inference for a key the types file does not name | Querying or filtering notes by property value |
| How `LibraryProperties` draws each type, including a type mismatch | `cssclasses` changing how the note is styled (D-8) |
| `[[links]]` and URLs in values, resolved like links in the body | The reference app's publish-only keys doing anything (`publish`, `permalink`, `description`, `image`, `cover`): they are ordinary rows |
| Property links counted in backlinks | The Agent-file (Persona) front matter and `PersonaFrontmatter`: unchanged |
| `aliases`: in Global Search and in the editor's `[[` completion | |
| Front matter written as JSON | |
| A nested value shown as source instead of being flattened | |

## Vocabulary

| Term | Meaning |
| --- | --- |
| **Reference app** | The Markdown notes app whose files Huddle reads. The spec never needs its name |
| **Front matter** | The block at the top of a note, as `NoteFrontmatter.Split` decides (§6.17), extended to JSON by D-9 |
| **Property** | One top-level entry in the front matter |
| **Property type** | Text, List, Number, Checkbox, Date, Date and time, Aliases or Tags: the reference app's eight. Plus two of Huddle's own for drawing: Unsupported (D-6) and Mismatch (D-3) |
| **Types file** | The JSON file in a root's settings folder where the reference app stores each key's assigned type. Its path inside a root is the setting `Team:Library:PropertyTypesFile` (see [Configuration](#configuration)) |
| **Properties block** | `LibraryProperties` (`.library-properties`), the card above a rendered note's body (`L3e`) |

## What happens today

`LibraryDocument` cuts the block out with `NoteFrontmatter.Split` and parses it with **`PersonaFrontmatter.Parse`**, the
parser for Agent definition files. `LibraryProperties` then draws one row per field. A list becomes chips; anything else
is plain text. The `tags` row alone is special (§6.17).

Against the example, that gives:

| Key | Today | Problem |
| --- | --- | --- |
| `reviewed` | `true` | Not a checkbox |
| `created`, `logged` | `2024-01-15`, `2024-01-15T09:00:00` | Raw strings |
| `pages`, `due` | Plain text | The types file is not read; no mismatch is shown |
| `url` | Plain text | Not a link |
| `owner`, `authors` | `[[Architecture]]` as text or as a chip | Not a link |
| `budget` | **Two extra rows, `amount` and `currency`** | `PersonaFrontmatter` trims each line, so an indented `amount: 1200` becomes a top-level field. This is wrong data, not just plain drawing |
| `notes` | One line | Line breaks collapsed to spaces |
| `aliases` | Chips | The note is not found by its aliases |

`PersonaFrontmatter` is shaped for its about 60 Agent-file callers: its `Value` joins list items with `"; "`, and its
rules (block scalars, `_packs`, `consult_when`) are the Agent file's. Changing it for notes would put Agent parsing at
risk, which is why D-1 gives notes their own reader.

## Decisions

### D-1. Notes get their own reader, `NoteProperties`

A new `Library/NoteProperties.cs` reads the block that `NoteFrontmatter.Split` returns into an ordered list of
`NoteProperty` values. `LibraryDocument` calls it instead of `PersonaFrontmatter.Parse`. `PersonaFrontmatter` is not
changed, and Agent files keep their parser.

`NoteTagReader` stays the one reader of `tags` (§6.17): the `tags` row keeps drawing what it returns, so the Properties
block and the tag index cannot disagree.

The reader parses YAML as YAML 1.2's core schema does, which is what the reference app's parser applies. Where the two
could differ (a duplicate key, a tab, an anchor), the reference app wins, and the rule is marked **(V)**.

### D-2. A key's type: the types file first, then fixed defaults, then the value

A property's type is decided in this order. The first step that gives one wins.

1. **The types file.** If it assigns the key a type, that type. Keys are matched case-insensitively
   (`StringComparer.OrdinalIgnoreCase`) **(V)**.
2. **The fixed defaults.** `tags` is Tags, `aliases` is Aliases, `cssclasses` is List. The types file cannot change these
   **(V)**.
3. **Inference from the value**, by the first matching row:

| Written value | Inferred type | Notes |
| --- | --- | --- |
| Nothing, `~`, `null` | Text, empty | `key:` with no value and no list items below |
| `true` or `false`, any YAML 1.2 casing | Checkbox | `yes`, `no`, `on`, `off` are Text |
| An integer or decimal: `320`, `-4`, `1.5`, `1e3` | Number | Kept as written, never reformatted, so `1.50` stays `1.50` |
| `YYYY-MM-DD` that is a real date | Date | `2026-02-30` is Text |
| `YYYY-MM-DDTHH:MM`, optional `:SS` and fraction, optional `Z` or `±HH:MM`; a space may replace the `T` **(V)** | Date and time | An impossible time is Text |
| A block list or a flow list of scalars | List | Each item a string, quotes removed |
| A map, or a list holding a map or a list | Unsupported | D-6 |
| A block scalar, `\|` or `>` | Text | `\|` keeps its line breaks, `>` folds them, as YAML does |
| Anything else, quoted or not | Text | |

A quoted value is a string, so `"true"`, `"320"` and `"2024-01-15"` infer as Text.

The types file's type names map to Huddle's as follows. An unknown name in the file is ignored for that key, which then
falls through to step 2.

| Type name in the file | Property type |
| --- | --- |
| `text` | Text |
| `multitext` | List |
| `number` | Number |
| `checkbox` | Checkbox |
| `date` | Date |
| `datetime` | Date and time |
| `aliases` | Aliases |
| `tags` | Tags |

### D-3. A value that does not fit its assigned type is a Mismatch

When step 1 or 2 of D-2 gives a type and the value cannot be read as that type, the row is a **Mismatch**: the raw value
as text, with a warning icon (`Icons.Material.Outlined.WarningAmber`) whose tooltip says *Type mismatch: expected
Number*. Nothing is coerced. What fits each type:

| Assigned type | Fits **(V)** |
| --- | --- |
| Text | Any scalar. A list does not fit |
| List, Aliases | A list of scalars, or a single scalar (read as a one-item list) |
| Number | A number, quoted or not |
| Checkbox | `true` or `false`, quoted or not; an empty value reads as unticked |
| Date | `YYYY-MM-DD`, quoted or not |
| Date and time | The Date and time form of D-2, or a bare date (read as midnight) |
| Tags | Whatever `NoteTagReader` reads (§6.17); never a Mismatch |

An empty value fits every type and draws as that type's empty form.

### D-4. How each type is drawn

| Type | Drawn as |
| --- | --- |
| Text | The text, with line breaks kept (`white-space: pre-line`). `[[links]]` in it are links (D-5); a whole-value URL is a link |
| List, Aliases | One chip per item. An item that is a `[[link]]` or a URL is a link chip |
| Number | The text as written |
| Checkbox | A read-only checkbox icon, ticked for true (`MudIcon` with `Icons.Material.Outlined.CheckBox` / `CheckBoxOutlineBlank`), with `aria-label` "Yes" or "No". Not a control, because Huddle does not edit |
| Date | `d MMMM yyyy`, invariant culture, the format `DocumentByline` uses. Its `title` is the raw value |
| Date and time | `d MMMM yyyy HH:mm`. Without an offset it is the wall time as written, never converted. With `Z` or an offset it is converted to the local time zone through the injected `TimeProvider`. Its `title` is the raw value |
| Tags | Chips, as §6.17 says today |
| Unsupported | The entry's YAML source (its lines, indentation kept) in a small monospace block (D-6) |
| Mismatch | D-3 |
| Any empty value | An empty row: the key, and nothing beside it **(V)** |

The reference app draws dates in the operating system's locale. Huddle uses its own date format everywhere and keeps it
here: the date is the same date, only its spelling differs. Keys are shown exactly as written, in file order. Text is
never Markdown: `**bold**` in a value shows its asterisks.

### D-5. Links in values are links everywhere a body link is

A value or list item that is exactly `[[Target]]`, `[[Target|Label]]` or `[[Target#Heading]]`, in quotes, is a link. Text
that holds quoted wikilinks among other words has each one drawn as a link and the rest as text.

- **Drawing.** The link resolves through the same `ILibraryNoteResolver.ResolveWikiLink` the body uses, from the note's
  own path, so only inside the note's own Library Root: a note under `Teams/` cannot link a note under `Agents/`, which
  is a separate root. A missing or ambiguous target gets the body's style and title. The label is the alias when there is one,
  otherwise the target.
- **Backlinks.** A property link counts as a link from its note: the target's backlinks list it, with the property's line
  as context. `WikiLinkIndex` parses each note's whole text today, front matter included, so this may already hold; the
  test pins it either way.
- **Unquoted `[[Target]]`** is, in YAML, a list holding a one-item list. It is read and drawn as YAML says: Unsupported,
  as its source **(V)**. Whether it still counts as a backlink follows what the reference app does **(V)**.
- **URLs.** A value that is exactly an `http:`, `https:` or `mailto:` URL is a link that opens outside the app, as a body
  link does. Any other scheme is plain text (D-10).

### D-6. A nested value is Unsupported and shown as source

A map, or a list that holds a map or a list, is not one of the reference app's types. Its row shows the entry's own lines
as YAML source, so nothing is hidden and nothing is invented. Its indented lines never become rows of their own; that is
the bug in [What happens today](#what-happens-today). How the reference app itself shows such a value is **(V)**; if it
draws something richer, this row follows it.

### D-7. `aliases` are other names for the note

The Aliases type is a list of other names for the note. As in the reference app, an alias is a way to **find** a note,
not a way to **link** to it:

- **Global Search** matches a note by any of its aliases, as it matches the file name. The result shows the file name,
  with the matched alias beside it.
- **The editor's `[[` completion** (`WikiLinkCompletionsAsync`) offers each alias as its own entry. Choosing it inserts
  `[[Note|Alias]]`, so the link resolves by file name and reads as the alias **(V)**.
- **A bare `[[Alias]]` does not resolve to the note.** With no file of that name it is a missing link, as in the body
  today **(V)**.

`WikiLinkIndex` already reads every note in a root; it collects aliases in the same pass, and drops them on the same
`Invalidated` event.

### D-8. The other reserved and deprecated keys

| Key | Read as | Drawn as |
| --- | --- | --- |
| `cssclasses` | A List | Chips. **No class from a file is ever applied to the page**: the value is text from a file an Agent may have written. The reference app would restyle the note; Huddle shows the names only, and this is the one place display differs on purpose |
| `tag`, `alias`, `cssclass` | Ordinary properties, inferred like any other key | Ordinary rows. The reference app no longer gives them a meaning, and `tag` is still not read as tags (§6.17, D-31) |
| `publish`, `permalink`, `description`, `image`, `cover` | Ordinary properties | Ordinary rows. They act only in the reference app's publishing service |

### D-9. Front matter written as JSON

A note whose first line starts with `{` and whose leading text parses as one JSON object, closed before the body, has
that object as its front matter **(V)**. Its top-level members are properties, read by the same types and rules as YAML
(a JSON array is a list, a JSON object is Unsupported, `true`, `false` and numbers infer as their types).

A JSON string is always quoted, so D-2's rule for quoted values applies: `"2024-01-15"` infers as **Text**, not Date.
A key the types file assigns Date or Date and time still reads a JSON string of that shape as its type, as D-3 allows
for a quoted YAML value. So without a types file entry, a JSON note's dates draw as text where its YAML twin draws them
as dates.

`NoteFrontmatter.Split` is the one place that learns this second boundary, so the tag pass, `NoteTagReader` and the
Properties block stay in step, as §6.17 requires. `NoteTagReader` reads a JSON `tags` member as it reads a flow list.

### D-10. A malformed entry spoils only itself

The reader never fails the note. An entry it cannot read (an unclosed quote or `[`, a tab in its indentation) is drawn
as Unsupported: its source, as D-6. The entries around it are read normally. A repeated key keeps the **last** entry, at
the last one's place, as the tag reader does **(V)**. Nothing is reported or rewritten.

A types file that is missing, unreadable, larger than 1 MB or not valid JSON gives no assigned types: every key falls to
D-2 steps 2 and 3. It is never an error the Human sees.

### D-11. Property text is untrusted

Values come from files an Agent may have written, so the rules for tag text (Library spec, security rule 6) apply to every
property and to the types file:

- Keys and values are rendered as Razor text, never as a `MarkupString`.
- A link's `href` is built only by the resolver (D-5) or from an allowed URL scheme; nothing else is placed in an
  attribute, and no `class` comes from a file (D-8).
- Bidi control characters (U+200E, U+200F, U+202A–202E, U+2066–2069) are removed before drawing.
- A Text value, and an Unsupported entry's source block, longer than 2,000 characters is cut there with `…`; the file
  is unchanged. The cut never splits a surrogate pair.

### D-12. The settings folder stays hidden, but its types file is read and watched

The root's settings folder is already in `LibraryHiddenFolders.AlwaysHidden`, so the Library tree, the files endpoint and
`WikiLinkIndex` skip it. That stays: the Human never sees the folder, and none of its files are indexed as notes.

The types file is the one exception, and only for reading:

- `PropertyTypes` reads it through `LibraryPathResolver`, which does not apply the hidden-folder rule, so the read works
  without changing that rule.
- `PropertyTypes` checks the file's last-write time and length each time a note in that root is drawn, and rereads it
  when either changed. There is no watcher for the file and the folder is not unhidden, so a change reaches an open note
  on its **next draw**, not at once (B6).
- A read that fails (the file locked by the other app mid-write, for example) is not cached: the next draw tries again.
- Nothing else in the settings folder is read.

## Figure: the Properties block

The example's block, as `L3e` draws it above the body. `▣` is a ticked checkbox, `⚠` the mismatch mark and `⟨…⟩` a chip.

```text
┌ PROPERTIES ────────────────────────────────────────────────┐
│ title       Project Plan                                    │
│ url         https://example.com/plan ↗                      │
│ pages       320                                             │
│ reviewed    ▣                                               │
│ created     15 January 2024                                 │
│ logged      15 January 2024 09:00                           │
│ due         next week  ⚠              (tooltip: expected    │
│                                         Date)               │
│ owner       Architecture              (Library link)        │
│ authors     ⟨Ada⟩ ⟨Architecture⟩      (second chip a link)  │
│ tags        ⟨recipe⟩ ⟨cooking⟩                              │
│ aliases     ⟨Plan⟩ ⟨Q1 plan⟩                                │
│ cssclasses  ⟨wide⟩                                          │
│ budget      ┌──────────────────┐                            │
│             │ amount: 1200     │  (monospace source)        │
│             │ currency: EUR    │                            │
│             └──────────────────┘                            │
│ notes       First line.                                     │
│             Second line.                                    │
│ empty                                                       │
└─────────────────────────────────────────────────────────────┘
```

## Behaviour

| # | Situation | Result |
| --- | --- | --- |
| B1 | A note with no front matter | No Properties block, as today |
| B2 | A block whose every entry is empty | The block is drawn, one empty row per key |
| B3 | The Human clicks a property link to a note | The note opens as a body `[[link]]` would |
| B4 | A property link to a missing note | Drawn in the missing style with its title; clicking it does what a missing body link does |
| B5 | The note is changed on disk while open | The block redraws with the document, as today |
| B6 | The types file is changed on disk | The next draw of a note in that root uses the new types, although the file sits in a hidden folder (D-12). An open note does not redraw by itself; the root's alias and link index is unaffected |
| B7 | Split view | The preview side draws the block; the raw side shows the front matter as typed, as today |
| B8 | An Agent file (`Agents/<Name>/<Name>.md`) opened in the Library | Uses `NoteProperties` for display like any note. `PersonaFrontmatter` still reads it for everything else |
| B9 | A root with no settings folder | No assigned types; inference only (D-2) |

## Configuration

One new key, documented with the others in [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) when built:

| Key | Default | Meaning |
| --- | --- | --- |
| `Team:Library:PropertyTypesFile` | `.obsidian/types.json`, the reference app's path, set in code (`LibraryOptions.DefaultPropertyTypesFile`); like the other `Team:Library` keys it is absent from `appsettings.json` | The types file's path inside every Library Root, `/`-separated and relative to the root. Empty turns the types file off: inference only |

The file is read per root through the same `LibraryPathResolver` the Library uses, so it can never be read from outside
a root. Its parsed types are cached per root and dropped when the file changes (D-12).

## Seeded demo

The `software-co` scenario (`SoftwareCoLibrary`) gets notes that show every case, so the seeded app is the demo, the
place for the FP-M1 hand check, and a fixture the seeder tests pin. The existing tag notes stay as they are: they already
cover one tag form each (a flow list in `Architecture.md`, a comma string in `Meeting-Notes-2026-10-14.md`, inline tags
only in `Engineering-Handbook.md`).

| Seeded file | Holds | Shows |
| --- | --- | --- |
| `Teams/Platform/AllTypes.md` | [The example](#the-example), **character for character** | Every type, a Mismatch (`due`), Unsupported (`budget`), property links, aliases, a multi-line Text |
| `Teams/Platform/AllTypes-Json.md` | The same properties as JSON front matter (D-9), without `budget` and `notes` | The JSON form draws the same rows as the YAML one, except `created` and `logged`: JSON strings are quoted, so they draw as Text (D-9) |
| `Teams/Platform/Properties-Edge-Cases.md` | `reviewer: "[[Nobody]]"`, `related: [[Architecture]]` (unquoted), `flag: "true"`, `tag: legacy`, `cssclass: wide`, `publish: true`, `broken: "unclosed`, `status: draft` twice with different values | A missing link, an unquoted link as Unsupported, a quoted boolean as Text, deprecated and publish-only keys as ordinary rows, a malformed entry spoiling only itself, the last duplicate winning |
| `Teams/Platform/Plan-Review.md` | A body that links `[[AllTypes\|Plan]]`, and the text `[[Plan]]` | A link written through an alias, and a bare alias staying missing (D-7) |
| The types file, at `Team:Library:PropertyTypesFile` inside the seeded root | `pages` assigned Number and `due` assigned Date, in the file's format **(V)** | D-2 step 1 and D-3 |

Rules for the seed:

- The property links target `[[Architecture]]`, an existing note in the same `Teams` root. A link to an Agent file such
  as `[[Grace]]` would be missing, because `Agents` is a separate root (D-5).
- The seed's only deliberately missing links are the ones named in this section (`[[Nobody]]`, the bare `[[Plan]]`) and
  any older ones the seeder test already lists by name. The test lists them exactly, so a new missing link fails it.
- The seeder writes the types file at the path the option's **default** names, taken from the same constant
  `LibraryOptions` uses, never a second copy of the path.
- The settings folder is hidden in the Library tree (D-12), so the seeded types file is not visible there; the Mismatch
  row in `AllTypes.md` is how the demo shows it is read.
- `AllTypes.md` adds its two aliases to the seeded root, so Global Search finds it as `Plan` and as `Q1 plan`.

## What changes in code

| File | Change |
| --- | --- |
| `Library/NoteProperties.cs` (new) | D-1, D-2 step 3, D-6, D-10: `Read(NoteFrontmatterBlock block)` returns `IReadOnlyList<NoteProperty>`. Pure, no I/O |
| `Library/NoteProperty.cs`, `Library/NotePropertyKind.cs` (new) | The record (`Key`, `Kind`, the typed value, `Items` for a List, `Source` for Unsupported, `Expected` for a Mismatch) and the enum of the types |
| `Library/PropertyTypes.cs` (new) | Reads one root's types file (D-2 step 1, D-10), maps its names, caches per root, drops on change |
| `Library/PropertyTyping.cs` (new) | D-2's order and D-3's fit rules: takes the read properties and the root's types, returns the final kinds. Pure |
| `Library/NoteFrontmatter.cs` | D-9: `Split` also recognises a JSON object and reports which form it found |
| `Library/NoteTagReader.cs` | D-9: reads a JSON `tags` member. Its YAML behaviour is unchanged |
| `Library/WikiLinkIndex.cs` | D-7: collects each note's aliases in its existing pass. D-5: confirm, and if needed make, quoted property links count as backlinks |
| `Library/LibraryOptions.cs` | `PropertyTypesFile` |
| `Components/Library/LibraryProperties.razor` | Takes `IReadOnlyList<NoteProperty>` and the note's `LibraryPath` instead of `PersonaFrontmatterField`. Draws D-4 and D-3; resolves links through an injected `ILibraryNoteResolver`; formats dates through `TimeProvider`. `ChipText` goes |
| `Components/Library/LibraryDocument.razor` | `RenderMarkdown` calls `NoteProperties` and `PropertyTyping` instead of `PersonaFrontmatter.Parse`, and passes `Path` |
| The Library search provider and `WikiLinkCompletionsAsync` | D-7: aliases |
| `wwwroot/app.css` | `.library-properties-source` (monospace, the code-block tint), `.library-properties-mismatch`, and `pre-line` on Text values |
| `src/Huddle.Seeder/Scenarios/SoftwareCoLibrary.cs` | The four notes and the types file of [Seeded demo](#seeded-demo). `AllTypes.md`'s text is shared with the FP-T1 fixture, not copied |
| `Library/LibraryOptions.cs` | The types-file default as a `public const`, so the seeder reads the same path |
| `PropertyTypes` | D-12: a last-write-time and length check on each draw makes a change to the types file reach the next draw; the settings folder stays hidden |
| `docs/Huddle.Library-Specifications.md` | §6.17: "the other Properties rows are still parsed by `PersonaFrontmatter`" becomes a pointer to this spec; the frontmatter boundary paragraph gains D-9 |
| `docs/engineering/wireframes.md`, `docs/Huddle.EngineeringGuide.md` | At ship: the `L3e` row describes the typed rows; the new key |

## Tests

| ID | Test | Pins |
| --- | --- | --- |
| FP-T1 | `NotePropertiesTests`: the example (`AllTypes.md`) with its types file reads to the table in [The example](#the-example), key by key | Everything, end to end |
| FP-T2 | One theory per row of D-2's inference table, including the boundaries: `"true"` is Text, `yes` is Text, `2026-02-30` is Text, `1.50` keeps its text, `2024-01-15 09:00` is Date and time | D-2 step 3 |
| FP-T3 | `PropertyTypingTests`: the types file wins over inference; `TAGS` in the file cannot change `tags`; an unknown type name falls through; a key matches case-insensitively | D-2 steps 1 and 2 |
| FP-T4 | One theory per row of D-3: a fitting value takes the type, a non-fitting one is a Mismatch with its expected type, an empty value fits | D-3 |
| FP-T5 | A missing, oversized, invalid or unreadable types file gives inference only, with no exception | D-10 |
| FP-T6 | A map, a `{…}` value, a list of maps and an unquoted `[[A]]` each read as Unsupported with their source lines, and add no other properties | D-5, D-6, the flattening bug |
| FP-T7 | An unclosed quote in one entry makes that entry Unsupported and leaves its neighbours typed; a repeated key keeps the last | D-10 |
| FP-T8 | JSON front matter reads to the same properties as its YAML twin; its `tags` reach `NoteTagReader`; a `{` line that is not a closed object is body | D-9 |
| FP-T9 | `LibraryPropertiesTests` (bUnit): each type renders as D-4, the checkbox is not an `input`, a Mismatch shows its value and the warning with its tooltip | D-3, D-4 |
| FP-T10 | A link property renders an `a.library-ref` with the resolver's `href`; a missing target gets `library-ref-missing`; `[[A\|B]]` shows `B` | D-5 |
| FP-T11 | `WikiLinkIndex` on a temp root: a quoted property link appears in the target's backlinks | D-5 |
| FP-T12 | `WikiLinkIndex`, search and completion on a temp root: a note is found by its alias; completion offers the alias and inserts `[[Note\|Alias]]`; a bare `[[Alias]]` stays missing | D-7 |
| FP-T13 | A `javascript:` URL renders as text with no `href`; a `cssclasses` value appears in no `class` attribute; a bidi control is gone from the output | D-11 |
| FP-T14 | A `Z` date and time renders in a fake `TimeProvider`'s local zone; one without an offset renders as written | D-4 |
| FP-T15 | The existing `LibraryPropertiesTagsTests`, `NoteTagReaderTests` and `PersonaFrontmatter` tests pass unchanged | D-1, Scope |
| FP-T16 | `SoftwareCoScenarioTests`: the plan holds the four seeded notes and the types file at the option's default path; `AllTypes.md` equals the FP-T1 fixture; reading each seeded note through `NoteProperties` and `PropertyTyping` gives the rows [Seeded demo](#seeded-demo) lists; every property link in the seeded notes resolves in its own root, and the unresolved links are exactly the deliberate ones | The seed |
| FP-T17 | The Library tree listing of the seeded root does not show the settings folder, and `WikiLinkIndex` indexes nothing inside it, while `PropertyTypes` still reads the types file | D-12 |
| FP-T18 | Changing the types file on a temp root makes the next draw of a note in that root use the new types; a failed read is retried on the next draw | D-12, B6 |
| FP-M1 | By hand: open a reference vault as a pinned root, side by side with the reference app, and check every **(V)** rule and every row of the example. The seeded `software-co` app's four notes ([Seeded demo](#seeded-demo)) are the checklist on the Huddle side | The **(V)** rules |

## Open points

| # | Question | Recommendation |
| --- | --- | --- |
| OP-1 | The reference app can show properties visibly, hidden, or as raw source, as a per-vault setting. Should Huddle read that setting from the root's settings folder? | No for now. The Library's Split view already shows the source. Revisit if a pinned root relies on hidden properties |
| OP-2 | Dates are spelled in Huddle's format, not the operating system's locale (D-4). Is that a compatibility gap? | No: the value is the same date. Huddle uses one date format everywhere |
| OP-3 | Should a URL inside longer Text be a link, not just a whole-value URL? | Follow FP-M1: if the reference app links it, link it, under D-11's scheme rule |
| OP-4 | This spec makes Huddle depend on a third-party app's settings file format. Does it need an ADR? | Yes: a short ADR beside ADR-0067, recording that Library compatibility extends from tags to properties and to the types file, and that the file is read and never written |
