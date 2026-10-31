// The Library editor's JS half (Spec §6.7). Vendored CodeMirror 6 is imported here by RELATIVE
// URL (corrections-B6 D11 item 13), never by bare specifier, so no bundler or import map is
// needed at runtime. The exported `create` returns a handle object (Blazor turns it into the
// caller's IJSObjectReference) rather than mutating any module-level state, so several
// editors can exist on one page at once.
import {
  EditorState,
  EditorSelection,
  autocompletion,
  startCompletion,
  syntaxTree,
  ensureSyntaxTree,
  EditorView,
  keymap,
  lineNumbers,
  defaultKeymap,
  history,
  historyKeymap,
  StreamLanguage,
  markdown,
  json,
  javascript,
  css,
  xml,
  csharp,
  powerShell,
  yaml,
} from "./lib/codemirror/codemirror.bundle.js";
import { planCommand, wikiLinkBeforeCaret } from "./markdown-commands.js";
import { wireToolbar } from "./markdown-toolbar.js";

function languageExtension(languageId) {
  switch (languageId) {
    case "markdown":
      return markdown();
    case "json":
      return json();
    case "javascript":
      return javascript();
    case "css":
      return css();
    case "xml":
      return xml();
    case "yaml":
      return StreamLanguage.define(yaml);
    case "csharp":
      return StreamLanguage.define(csharp);
    case "powershell":
      return StreamLanguage.define(powerShell);
    default:
      return [];
  }
}

// Wait this long after the last keystroke before asking .NET for names, so a fast typist sends one
// circuit round trip rather than one per key. CodeMirror aborts the superseded request meanwhile.
const wikiLinkDelayMs = 120;

// Markdown syntax-tree nodes inside which `[[` is code, not a link (Spec §6.5 skips them too).
const codeNodeNames = new Set(["InlineCode", "FencedCode", "CodeBlock", "CodeText"]);

/** Whether the syntax node at `pos` sits inside a code span, a fenced block or an indented code block. */
function insideCode(state, pos) {
  const tree = ensureSyntaxTree(state, pos, 50) ?? syntaxTree(state);
  for (let node = tree.resolveInner(pos, -1); node; node = node.parent) {
    if (codeNodeNames.has(node.name)) {
      return true;
    }
  }

  return false;
}

/**
 * Applies a toolbar or keyboard command as ONE transaction (one undo step). The text transform is the
 * pure `planCommand`: it returns only marker insertions and deletions at the selection or the touched
 * lines, so ADR-0028's "never rewrite what you did not edit" holds.
 */
function applyCommand(view, name) {
  const { state } = view;
  const plan = planCommand(
    name,
    state.doc.toString(),
    state.selection.ranges.map((r) => ({ anchor: r.anchor, head: r.head })),
  );
  if (plan === null) {
    return;
  }

  const spec = { changes: plan.changes, userEvent: "input.format", scrollIntoView: true };
  if (plan.selection !== null) {
    spec.selection = EditorSelection.create(
      plan.selection.map((r) => EditorSelection.range(r.anchor, r.head)),
      state.selection.mainIndex,
    );
  }

  view.dispatch(spec);
  if (plan.openCompletion) {
    // Nothing typed yet: pop the list straight away for the (empty) query inside the brackets.
    startCompletion(view);
  }

  view.focus();
}

/** A CodeMirror key binding that runs `name` through `applyCommand`. */
function commandKey(key, name) {
  return {
    key,
    run: (view) => {
      applyCommand(view, name);
      return true;
    },
  };
}

/**
 * Wires the Markdown toolbar element to `view` through the helper the Task Description's textarea shares
 * (markdown-toolbar.js), so a click edits the document without a trip through the Blazor circuit and the editor
 * keeps its focus and selection. Returns the function that detaches it all.
 */
function attachToolbar(view, toolbar) {
  return wireToolbar(
    toolbar,
    (name) => applyCommand(view, name),
    () => view.focus(),
  );
}

/**
 * A CodeMirror completion source for `[[`. The names come from .NET (`WikiLinkCompletionsAsync`) because
 * the per-root link index lives there; the source only decides WHEN to ask and how to apply a pick. It
 * stays silent inside code (Spec §6.5) and, through the `wikiLinkBeforeCaret` pattern, after `[[x|` and
 * `[[x#`. `filter: false` because .NET already filtered and ranked, and no `validFor`, so every keystroke
 * asks again rather than reusing a stale list.
 */
function wikiLinkSource(dotNetRef) {
  return async (context) => {
    const match = context.matchBefore(wikiLinkBeforeCaret);
    if (!match || insideCode(context.state, match.from)) {
      return null;
    }

    await new Promise((resolve) => setTimeout(resolve, wikiLinkDelayMs));
    if (context.aborted) {
      return null;
    }

    let names;
    try {
      names = await dotNetRef.invokeMethodAsync("WikiLinkCompletionsAsync", match.text.slice(2));
    } catch {
      // The circuit is gone or the component disposed mid-request: offering nothing is correct.
      return null;
    }

    if (context.aborted || !names || names.length === 0) {
      return null;
    }

    return {
      from: match.from + 2,
      filter: false,
      options: names.map((name) => ({
        label: name,
        apply: (view, _completion, from, to) => {
          // Close the link with `]]` unless the brackets are already there (the toolbar's `[[]]` button).
          const hasClose = view.state.doc.sliceString(to, to + 2) === "]]";
          view.dispatch({
            changes: { from, to, insert: hasClose ? name : name + "]]" },
            selection: { anchor: from + name.length + 2 },
            userEvent: "input.complete",
          });
        },
      })),
    };
  };
}

/**
 * Builds a CodeMirror 6 `EditorView` inside `element` and returns a handle object exposing
 * `getText`, `setText` and `dispose`. Text crosses the .NET/JS boundary only on save and on
 * mode switch, never per keystroke: `OnDirtyChanged` fires only when the dirty state flips, and
 * `OnSaveRequested` fires only on the `Mod-s` keymap. When `previewMode` is set (Split mode,
 * corrections-B6 item 15), `OnPreviewText` fires 300 ms after the last keystroke - the debounce
 * timer lives here, never as a .NET `Timer`, and is cleared by `dispose`. `showLineNumbers` adds
 * CodeMirror's line-number gutter (Split mode's raw side). An editable Markdown editor also binds
 * Mod-b / Mod-i / Mod-e and finds the Markdown toolbar of its document pane (the `[data-md-toolbar]`
 * element inside the nearest `.library-document`, rendered by `MarkdownToolbar` before this editor), whose
 * buttons then edit client-side; `dispose` detaches it.
 */
export function create(element, text, readOnly, languageId, dotNetRef, previewMode, showLineNumbers) {
  let savedText = text;
  let dirty = false;
  let previewTimer = null;
  let detachToolbar = null;
  const editableMarkdown = languageId === "markdown" && !readOnly;

  const updateListener = EditorView.updateListener.of((update) => {
    if (!update.docChanged) {
      return;
    }

    // Length first: a keystroke almost always changes it, so the O(n) toString (documents run to
    // MaxEditableBytes) only happens when an edit leaves the length unchanged.
    const doc = update.state.doc;
    const nowDirty = doc.length !== savedText.length || doc.toString() !== savedText;
    if (nowDirty !== dirty) {
      dirty = nowDirty;
      dotNetRef.invokeMethodAsync("OnDirtyChanged", dirty);
    }

    if (previewMode) {
      if (previewTimer !== null) {
        clearTimeout(previewTimer);
      }

      previewTimer = setTimeout(() => {
        previewTimer = null;
        dotNetRef.invokeMethodAsync("OnPreviewText", doc.toString());
      }, 300);
    }
  });

  const saveKeymap = keymap.of([
    {
      key: "Mod-s",
      run: () => {
        dotNetRef.invokeMethodAsync("OnSaveRequested");
        return true;
      },
    },
    ...(editableMarkdown
      ? [commandKey("Mod-b", "bold"), commandKey("Mod-i", "italic"), commandKey("Mod-e", "code")]
      : []),
    ...defaultKeymap,
    ...historyKeymap,
  ]);

  const view = new EditorView({
    parent: element,
    state: EditorState.create({
      doc: text,
      extensions: [
        EditorState.lineSeparator.of("\n"),
        EditorView.editable.of(!readOnly),
        EditorState.readOnly.of(readOnly),
        EditorView.lineWrapping,
        showLineNumbers ? lineNumbers() : [],
        history(),
        saveKeymap,
        languageExtension(languageId),
        // `[[` suggestions are a Markdown-editing aid only: a read-only or non-Markdown editor has none.
        editableMarkdown ? autocompletion({ override: [wikiLinkSource(dotNetRef)], icons: false }) : [],
        updateListener,
      ],
    }),
  });

  if (editableMarkdown) {
    const toolbar = element.closest(".library-document")?.querySelector("[data-md-toolbar]") ?? null;
    if (toolbar !== null) {
      detachToolbar = attachToolbar(view, toolbar);
    }
  }

  const handle = {
    getText: () => view.state.doc.toString(),
    setText: (newText) => {
      // savedText moves BEFORE the dispatch: the update listener compares against it, so setting
      // it afterwards would flash dirty=true (and a stray OnDirtyChanged pair) on every reload.
      savedText = newText;
      view.dispatch({
        changes: { from: 0, to: view.state.doc.length, insert: newText },
      });
      if (dirty) {
        dirty = false;
        dotNetRef.invokeMethodAsync("OnDirtyChanged", false);
      }
    },
    revealLine: (line) => {
      // Clamped into 1..doc.lines: an outline can outlive an edit that shortened the document.
      const n = Math.min(Math.max(1, line), view.state.doc.lines);
      const pos = view.state.doc.line(n).from;
      view.dispatch({
        selection: { anchor: pos },
        effects: EditorView.scrollIntoView(pos, { y: "start" }),
      });
      view.focus();
    },
    dispose: () => {
      if (previewTimer !== null) {
        clearTimeout(previewTimer);
        previewTimer = null;
      }
      if (detachToolbar !== null) {
        detachToolbar();
        detachToolbar = null;
      }
      view.destroy();
    },
  };

  // Returned as is: the .NET caller asks for an IJSObjectReference, so Blazor wraps this return
  // value itself. Wrapping it here too (DotNet.createJSObjectReference) made the .NET reference
  // point at the wrapper `{ __jsObjectId }`, which has no getText/setText/dispose.
  return handle;
}
