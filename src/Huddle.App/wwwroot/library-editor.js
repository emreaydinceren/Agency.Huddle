// The Library editor's JS half (Spec §6.7). Vendored CodeMirror 6 is imported here by RELATIVE
// URL (corrections-B6 D11 item 13), never by bare specifier, so no bundler or import map is
// needed at runtime. The exported `create` returns a handle object (via
// DotNet.createJSObjectReference) rather than mutating any module-level state, so several
// editors can exist on one page at once.
import {
  EditorState,
  EditorView,
  keymap,
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

/**
 * Builds a CodeMirror 6 `EditorView` inside `element` and returns a handle object exposing
 * `getText`, `setText` and `dispose`. Text crosses the .NET/JS boundary only on save and on
 * mode switch, never per keystroke: `OnDirtyChanged` fires only when the dirty state flips, and
 * `OnSaveRequested` fires only on the `Mod-s` keymap. When `previewMode` is set (Split mode,
 * corrections-B6 item 15), `OnPreviewText` fires 300 ms after the last keystroke - the debounce
 * timer lives here, never as a .NET `Timer`, and is cleared by `dispose`.
 */
export function create(element, text, readOnly, languageId, dotNetRef, previewMode) {
  let savedText = text;
  let dirty = false;
  let previewTimer = null;

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
        history(),
        saveKeymap,
        languageExtension(languageId),
        updateListener,
      ],
    }),
  });

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
    dispose: () => {
      if (previewTimer !== null) {
        clearTimeout(previewTimer);
        previewTimer = null;
      }
      view.destroy();
    },
  };

  return DotNet.createJSObjectReference(handle);
}
