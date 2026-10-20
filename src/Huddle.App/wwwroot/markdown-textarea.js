// Attaches the Markdown toolbar (`MarkdownToolbar`, L3g) to a plain `<textarea>`: the Task Description's field
// (Task read view spec D-7). The commands themselves are the Library's PURE `planCommand` (markdown-commands.js);
// this file only applies a plan to a textarea. `applyPlan` is pure, so it runs under Node in the test suite; the DOM
// half (`insertText`, the `input` and `change` events) is checked by hand in the running app.
import { planCommand } from "./markdown-commands.js";
import { wireToolbar } from "./markdown-toolbar.js";

/**
 * Applies `plan` (from `planCommand`) to `text`, whose selection is `selection` (`{ anchor, head }[]`).
 * Returns `{ text, selection, span }`: the new text, the new selection as `{ start, end }` (the plan's first range,
 * else the old first range moved through the changes, a position at an insertion staying before it), and `span`,
 * the one stretch `{ from, to, insert }` of the ORIGINAL text that differs. The span lets the DOM step replace it
 * with a single `insertText`, so the browser records ONE undo step however many lines a list command touches.
 */
export function applyPlan(text, selection, plan) {
  const changes = [...plan.changes].sort((a, b) => a.from - b.from);
  let result = "";
  let cursor = 0;
  for (const change of changes) {
    result += text.slice(cursor, change.from) + change.insert;
    cursor = change.to;
  }

  result += text.slice(cursor);

  const from = changes.length === 0 ? 0 : changes[0].from;
  const to = changes.length === 0 ? 0 : changes.reduce((end, change) => Math.max(end, change.to), 0);
  const tail = text.length - to;
  const span = { from, to, insert: result.slice(from, result.length - tail) };

  const mapped = (position) => {
    let shift = 0;
    for (const change of changes) {
      if (change.to < position || (change.to === position && change.from < position)) {
        shift += change.insert.length - (change.to - change.from);
      }
    }

    return position + shift;
  };

  let range;
  if (plan.selection !== null && plan.selection.length > 0) {
    range = { start: plan.selection[0].anchor, end: plan.selection[0].head };
  } else {
    const first = selection[0] ?? { anchor: 0, head: 0 };
    range = { start: mapped(first.anchor), end: mapped(first.head) };
  }

  if (range.start > range.end) {
    range = { start: range.end, end: range.start };
  }

  return { text: result, selection: range, span };
}

/** Replaces `[from, to)` of the textarea with `insert` as one undoable edit, falling back to a plain assignment. */
function replaceSpan(textarea, span) {
  textarea.setSelectionRange(span.from, span.to);
  const inserted = span.insert.length > 0 ? document.execCommand("insertText", false, span.insert) : document.execCommand("delete");
  if (!inserted) {
    textarea.setRangeText(span.insert, span.from, span.to, "end");
  }
}

/** Runs command `name` on `textarea` and tells Blazor's binding, which listens for `change`, that the value moved. */
function runCommand(textarea, name) {
  const ranges = [{ anchor: textarea.selectionStart, head: textarea.selectionEnd }];
  const plan = planCommand(name, textarea.value, ranges);
  if (plan === null) {
    return;
  }

  const applied = applyPlan(textarea.value, ranges, plan);
  if (applied.span.insert.length > 0 || applied.span.to > applied.span.from) {
    replaceSpan(textarea, applied.span);
  }

  textarea.setSelectionRange(applied.selection.start, applied.selection.end);
  // `insertText` raises `input`; Blazor's binding for a non-Immediate field reads `change`, which a blur would raise
  // later, so raise it now: the Task counts as edited (Save turns on) while the Human is still typing.
  textarea.dispatchEvent(new Event("input", { bubbles: true }));
  textarea.dispatchEvent(new Event("change", { bubbles: true }));
  textarea.focus();
}

/**
 * Attaches `toolbar` (the element carrying `data-md-toolbar`) to `textarea`, and binds Ctrl/Cmd+B, I and E on the
 * textarea. Returns the function that detaches both. Attaching again to the same textarea replaces the earlier wiring.
 */
export function attach(textarea, toolbar) {
  const shortcuts = { b: "bold", i: "italic", e: "code" };
  const onKeyDown = (event) => {
    const name = shortcuts[event.key.toLowerCase()];
    if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && name !== undefined) {
      event.preventDefault();
      runCommand(textarea, name);
    }
  };

  // Ctrl+Z and Ctrl+Y move the text without a blur, and Blazor reads `change`, so an undone toolbar edit would leave the Task
  // holding the formatted text while the field shows the old one. Raise `change` for them as the commands do.
  const onInput = (event) => {
    if (event.inputType === "historyUndo" || event.inputType === "historyRedo") {
      textarea.dispatchEvent(new Event("change", { bubbles: true }));
    }
  };

  textarea.addEventListener("keydown", onKeyDown);
  textarea.addEventListener("input", onInput);
  const detachToolbar = wireToolbar(toolbar, (name) => runCommand(textarea, name), () => textarea.focus());

  return {
    dispose: () => {
      textarea.removeEventListener("keydown", onKeyDown);
      textarea.removeEventListener("input", onInput);
      detachToolbar();
    },
  };
}

/**
 * Finds the textarea and the `data-md-toolbar` row inside `container` (the Description's editor wrapper) and attaches
 * them. Returns the handle to dispose, or `null` when either is missing, so a render that has not painted the field yet
 * attaches nothing rather than throwing. A missing `container` is the same case.
 */
export function attachIn(container) {
  // The element Blazor named can be gone by the time this runs (a render replaced it): attach nothing, and the caller tries again after its next render.
  const textarea = container?.querySelector("textarea") ?? null;
  const toolbar = container?.querySelector("[data-md-toolbar]") ?? null;
  return textarea === null || toolbar === null ? null : attach(textarea, toolbar);
}
