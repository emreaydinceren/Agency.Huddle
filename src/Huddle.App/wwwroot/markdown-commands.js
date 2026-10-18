// The Library editor's Markdown toolbar commands as PURE text transforms (Spec §6.7, ADR-0028).
// No CodeMirror and no DOM in here: each command takes the document text and the selection ranges and
// returns the minimal changes (marker characters only, in ORIGINAL document coordinates) plus where the
// selection lands afterwards. `library-editor.js` applies the plan as ONE transaction, so a command is one
// undo step, and nothing outside the selection or the touched lines is ever read back or rewritten. Being
// pure, the logic runs under Node in the test suite (tests/Huddle.Tests/Ui/Library).

/** The commands `planCommand` understands, in toolbar order. */
export const commandNames = ["bold", "italic", "heading", "code", "bulletList", "taskList", "wikilink"];

const headingPrefix = /^#{1,6} /;

/**
 * Text before the caret that means "the user is typing a wikilink target": `[[` followed by anything that
 * cannot end or split a target (`]`, `[`, a line break, the `|` alias bar, the `#` heading mark). Used by
 * the editor's completion source via `matchBefore`, which anchors it at the caret.
 */
export const wikiLinkBeforeCaret = /\[\[[^\[\]\r\n|#]*/;

/** The query typed after `[[` at the end of `textBeforeCaret`, or `null` when the caret is not in a target. */
export function wikiLinkQuery(textBeforeCaret) {
  const match = new RegExp(wikiLinkBeforeCaret.source + "$").exec(textBeforeCaret);
  return match === null ? null : match[0].slice(2);
}

/** The wrap marker for each wrapping command. */
const wrapMarkers = { bold: "**", italic: "*", code: "`" };

/** The line prefix for each list command. */
const listPrefixes = { bulletList: "- ", taskList: "- [ ] " };

/**
 * Plans `name` over `doc` for `ranges` (each `{ anchor, head }`).
 * Returns `{ changes, selection, openCompletion }`, or `null` for an unknown command. `changes` are
 * `{ from, to, insert }` against the original text; `selection` is `{ anchor, head }[]` against the text
 * AFTER the changes, or `null` to let the editor map the selection through the changes itself.
 */
export function planCommand(name, doc, ranges) {
  const normalised = ranges
    .map((r) => ({ from: Math.min(r.anchor, r.head), to: Math.max(r.anchor, r.head), forward: r.head >= r.anchor }))
    .sort((a, b) => a.from - b.from);

  if (name in wrapMarkers) {
    return planWrap(doc, normalised, wrapMarkers[name]);
  }

  if (name === "wikilink") {
    const plan = planInsertWrap(normalised, "[[", "]]");
    plan.openCompletion = normalised.length === 1 && normalised[0].from === normalised[0].to;
    return plan;
  }

  if (name === "heading") {
    return planHeading(doc, normalised);
  }

  if (name in listPrefixes) {
    return planListPrefix(doc, normalised, listPrefixes[name]);
  }

  return null;
}

/** The start offsets of the lines (split on `\n` only, as the editor's line separator) the ranges touch. */
function touchedLineStarts(doc, ranges) {
  const starts = new Set();
  for (const range of ranges) {
    let start = range.from === 0 ? 0 : doc.lastIndexOf("\n", range.from - 1) + 1;
    // A selection that ends at the very start of a line has not selected that line.
    const end = range.to > range.from && doc[range.to - 1] === "\n" ? range.to - 1 : range.to;
    for (;;) {
      starts.add(start);
      const next = doc.indexOf("\n", start);
      if (next === -1 || next + 1 > end) {
        break;
      }
      start = next + 1;
    }
  }

  return [...starts].sort((a, b) => a - b);
}

/** The text of the line that begins at `start` (up to, not including, the `\n`). */
function lineAt(doc, start) {
  const end = doc.indexOf("\n", start);
  return doc.slice(start, end === -1 ? doc.length : end);
}

/**
 * Heading: per line, any `#{1,6} ` prefix is a heading. A `## ` line loses its prefix; a heading of any
 * other level is set to `## `; a plain line gets `## `. Only the prefix characters change.
 */
function planHeading(doc, ranges) {
  const changes = [];
  for (const start of touchedLineStarts(doc, ranges)) {
    const match = headingPrefix.exec(lineAt(doc, start));
    if (match === null) {
      changes.push({ from: start, to: start, insert: "## " });
    } else if (match[0] === "## ") {
      changes.push({ from: start, to: start + 3, insert: "" });
    } else {
      changes.push({ from: start, to: start + match[0].length, insert: "## " });
    }
  }

  return { changes, selection: null, openCompletion: false };
}

/**
 * Bullet and task lists: when every touched line already starts with `prefix` it is removed from all of
 * them; otherwise it is added to those that lack it.
 */
function planListPrefix(doc, ranges, prefix) {
  const starts = touchedLineStarts(doc, ranges);
  const allHave = starts.every((start) => lineAt(doc, start).startsWith(prefix));
  const changes = [];
  for (const start of starts) {
    if (allHave) {
      changes.push({ from: start, to: start + prefix.length, insert: "" });
    } else if (!lineAt(doc, start).startsWith(prefix)) {
      changes.push({ from: start, to: start, insert: prefix });
    }
  }

  return { changes, selection: null, openCompletion: false };
}

/** Plain insert of `open`/`close` around every range (no toggle): the `[[ ]]` button. */
function planInsertWrap(ranges, open, close) {
  const changes = [];
  const selection = [];
  let shift = 0;
  for (const range of ranges) {
    changes.push({ from: range.from, to: range.from, insert: open });
    changes.push({ from: range.to, to: range.to, insert: close });
    selection.push({ anchor: range.from + shift + open.length, head: range.to + shift + open.length });
    shift += open.length + close.length;
  }

  return { changes, selection, openCompletion: false };
}

/** How many `ch` characters run unbroken, ending just before `index`. */
function runBefore(doc, index, ch) {
  let n = 0;
  while (index - n - 1 >= 0 && doc[index - n - 1] === ch) {
    n++;
  }

  return n;
}

/** How many `ch` characters run unbroken from `index` on. */
function runAfter(doc, index, ch) {
  let n = 0;
  while (index + n < doc.length && doc[index + n] === ch) {
    n++;
  }

  return n;
}

/**
 * Whether a run of `run` marker characters on both sides holds `marker` itself. `*` is bold when the run
 * is two or more and italic when it is odd (`***x***` is both); the doubled `**` is present from two on;
 * a backtick is present from one. So italic on `**x**` adds (never strips half the bold), and bold on
 * `*x*` adds.
 */
function holdsMarker(marker, run) {
  if (marker === "*") {
    return run % 2 === 1;
  }

  return run >= marker.length;
}

/**
 * Wraps a range in `marker`, or removes it when the range is already wrapped: markers just OUTSIDE the
 * selection (`**|x|**`) or just INSIDE it (`|**x**|`). An empty range becomes the pair with the caret
 * between; a caret already between a pair removes it.
 */
function planWrap(doc, ranges, marker) {
  const ch = marker[0];
  const len = marker.length;
  const changes = [];
  const selection = [];
  let shift = 0;

  for (const range of ranges) {
    const before = runBefore(doc, range.from, ch);
    const after = runAfter(doc, range.to, ch);
    const text = doc.slice(range.from, range.to);
    const leading = runAfter(text, 0, ch);
    const trailing = runBefore(text, text.length, ch);
    const innerFits = text.length >= 2 * len && leading + trailing <= text.length;

    if (holdsMarker(marker, Math.min(before, after)) && before >= len && after >= len) {
      // Markers outside: delete one marker either side; the selection keeps its text.
      changes.push({ from: range.from - len, to: range.from, insert: "" });
      changes.push({ from: range.to, to: range.to + len, insert: "" });
      selection.push({ anchor: range.from + shift - len, head: range.to + shift - len });
      shift -= 2 * len;
    } else if (innerFits && holdsMarker(marker, Math.min(leading, trailing))) {
      // Markers inside: delete the first and last marker; the selection shrinks to the text between.
      changes.push({ from: range.from, to: range.from + len, insert: "" });
      changes.push({ from: range.to - len, to: range.to, insert: "" });
      selection.push({ anchor: range.from + shift, head: range.to + shift - 2 * len });
      shift -= 2 * len;
    } else {
      changes.push({ from: range.from, to: range.from, insert: marker });
      changes.push({ from: range.to, to: range.to, insert: marker });
      selection.push({ anchor: range.from + shift + len, head: range.to + shift + len });
      shift += 2 * len;
    }
  }

  return { changes, selection, openCompletion: false };
}
