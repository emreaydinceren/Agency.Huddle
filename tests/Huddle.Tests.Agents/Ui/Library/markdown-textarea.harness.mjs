// Node harness for MarkdownTextareaScriptTests: runs the PURE half of wwwroot/markdown-textarea.js (`applyPlan`) over
// small documents, with the real `planCommand` of markdown-commands.js producing the plans, and prints every outcome as
// one JSON object. The DOM half (`insertText`, the `input` and `change` events) is checked by hand in the app.
// Usage: node markdown-textarea.harness.mjs <markdown-textarea.js> <markdown-commands.js> <markdown-toolbar.js>
import { readFileSync } from "node:fs";

const [, , textareaPath, commandsPath, toolbarPath] = process.argv;

// Modules are imported as data: URLs (see markdown-commands.harness.mjs for why), so markdown-textarea.js's two relative
// imports are rewritten to the data: URLs of the files they name.
const dataUrl = (source) => `data:text/javascript;base64,${Buffer.from(source, "utf8").toString("base64")}`;
const commandsUrl = dataUrl(readFileSync(commandsPath, "utf8"));
const toolbarUrl = dataUrl(readFileSync(toolbarPath, "utf8"));
const textareaSource = readFileSync(textareaPath, "utf8")
  .replace('"./markdown-commands.js"', JSON.stringify(commandsUrl))
  .replace('"./markdown-toolbar.js"', JSON.stringify(toolbarUrl));

const { applyPlan } = await import(dataUrl(textareaSource));
const { planCommand } = await import(commandsUrl);

const at = (anchor, head = anchor) => [{ anchor, head }];

/** Plans `command` over `doc` and applies it the way the textarea does, reporting the text, selection and span. */
function run(command, doc, ranges) {
  const plan = planCommand(command, doc, ranges);
  const applied = applyPlan(doc, ranges, plan);
  return {
    text: applied.text,
    selection: [applied.selection.start, applied.selection.end],
    // Replacing the span in the original text must give the same text: that is the one `insertText` the DOM step makes.
    spanText: doc.slice(0, applied.span.from) + applied.span.insert + doc.slice(applied.span.to),
    span: [applied.span.from, applied.span.to, applied.span.insert],
  };
}

const results = {
  boldWrap: run("bold", "hello world", at(0, 5)),
  boldCaret: run("bold", "ab", at(1)),
  boldOff: run("bold", "**hello** world", at(2, 7)),
  bulletLines: run("bulletList", "a\nb\nc", at(0, 3)),
  bulletOff: run("bulletList", "- a\n- b", at(0, 7)),
  headingCaretAtLineStart: run("heading", "one\ntwo", at(4)),
  taskListMixed: run("taskList", "- [ ] a\nb", at(0, 9)),
  codeWrap: run("code", "run it", at(0, 3)),
};

process.stdout.write(JSON.stringify(results));
