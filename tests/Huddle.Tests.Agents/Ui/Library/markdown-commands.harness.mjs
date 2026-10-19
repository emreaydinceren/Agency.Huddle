// Node harness for MarkdownCommandsScriptTests: runs the PURE Markdown toolbar transforms from
// wwwroot/markdown-commands.js over small documents and prints every outcome as one JSON object.
// Usage: node markdown-commands.harness.mjs <path to markdown-commands.js>
import { readFileSync } from "node:fs";

// Imported as a data: URL rather than by path: a `.js` file with `export` is CommonJS to Node 18
// (CI's version) without a "type": "module" package.json, and only newer Node detects ES syntax by
// itself. A text/javascript data: URL is always an ES module. markdown-commands.js imports nothing,
// so it has no relative specifiers for the data: URL to break.
const source = readFileSync(process.argv[2], "utf8");
const { planCommand, wikiLinkQuery } = await import(
  `data:text/javascript;base64,${Buffer.from(source, "utf8").toString("base64")}`
);

/** Applies the plan the way the editor does: every change against the ORIGINAL text, in one go. */
function run(command, doc, ranges) {
  const plan = planCommand(command, doc, ranges);
  if (plan === null) {
    return null;
  }

  // Array.prototype.sort is stable, so two inserts at one position keep their order, as CodeMirror keeps it.
  let text = "";
  let cursor = 0;
  for (const change of [...plan.changes].sort((a, b) => a.from - b.from)) {
    text += doc.slice(cursor, change.from) + change.insert;
    cursor = change.to;
  }

  text += doc.slice(cursor);

  return {
    text,
    selection: plan.selection === null ? null : plan.selection.map((r) => [r.anchor, r.head]),
    openCompletion: plan.openCompletion,
  };
}

const at = (anchor, head = anchor) => [{ anchor, head }];

const results = {
  headingPlain: run("heading", "Platform", at(0)),
  headingH1: run("heading", "# Platform architecture", at(5)),
  headingH2Off: run("heading", "## Platform", at(4)),
  headingH3: run("heading", "### Platform", at(0)),
  headingH6: run("heading", "###### Platform", at(0)),
  headingMulti: run("heading", "# a\n## b\nc", at(0, 10)),
  headingHashtagIsNotAHeading: run("heading", "#hashtag", at(0)),
  headingLeadingBlankLine: run("heading", "\na", at(0)),
  boldWrap: run("bold", "hello world", at(0, 5)),
  boldOffOutside: run("bold", "**hello** world", at(2, 7)),
  boldOffInside: run("bold", "**hello** world", at(0, 9)),
  boldCaretPair: run("bold", "****", at(2)),
  boldEmptyCaret: run("bold", "ab", at(1)),
  italicOnBoldOutside: run("italic", "**x**", at(2, 3)),
  italicOnBoldInside: run("italic", "**x**", at(0, 5)),
  boldOnItalic: run("bold", "*x*", at(1, 2)),
  italicOffOnItalic: run("italic", "*x*", at(1, 2)),
  italicOffOnBoldItalic: run("italic", "***x***", at(3, 4)),
  boldOffOnBoldItalic: run("bold", "***x***", at(3, 4)),
  codeWrap: run("code", "run it", at(0, 3)),
  codeOff: run("code", "`run` it", at(1, 4)),
  boldBetweenTwoBoldSpans: run("bold", "**a** b **c**", at(6, 7)),
  multiRangeBold: run("bold", "ab cd", [{ anchor: 0, head: 2 }, { anchor: 3, head: 5 }]),
  bulletAdd: run("bulletList", "a\nb", at(0, 3)),
  bulletOff: run("bulletList", "- a\n- b", at(0, 7)),
  bulletOnlyTouchedLines: run("bulletList", "a\nb\nc", at(2)),
  bulletSelectionEndsAtLineStart: run("bulletList", "a\nb", at(0, 2)),
  bulletLeavesCarriageReturns: run("bulletList", "a\r\nb", at(0, 4)),
  taskMixed: run("taskList", "- [ ] a\nb", at(0, 9)),
  taskOff: run("taskList", "- [ ] a\n- [ ] b", at(0, 15)),
  wikilinkEmpty: run("wikilink", "x", at(1)),
  wikilinkSelection: run("wikilink", "see", at(0, 3)),
  unknown: run("nope", "x", at(0)),
  queryOpen: wikiLinkQuery("see [[pla"),
  queryEmpty: wikiLinkQuery("[["),
  queryAfterAlias: wikiLinkQuery("[[plan|al"),
  queryAfterHeading: wikiLinkQuery("[[plan#sec"),
  queryAfterClosed: wikiLinkQuery("[[plan]]"),
  queryAfterClosedThenOpen: wikiLinkQuery("[[a]] and [[b"),
  queryPathPrefix: wikiLinkQuery("[[Platform/arc"),
  queryNotBrackets: wikiLinkQuery("a [ [b"),
};

process.stdout.write(JSON.stringify(results));
