// Re-exports the exact CodeMirror 6 surface `library-editor.js` (11.2) needs, plus the
// language modes the Library editor selects by file kind. This is the single source module
// bundled by esbuild into wwwroot/lib/codemirror/codemirror.bundle.js; nothing else in
// tools/codemirror is shipped.

export { EditorState, Compartment } from "@codemirror/state";
export { EditorView, keymap, lineNumbers } from "@codemirror/view";
export { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
export { LanguageSupport, StreamLanguage } from "@codemirror/language";

export { markdown } from "@codemirror/lang-markdown";
export { json } from "@codemirror/lang-json";
export { javascript } from "@codemirror/lang-javascript";
export { css } from "@codemirror/lang-css";
export { xml } from "@codemirror/lang-xml";
export { csharp } from "@codemirror/legacy-modes/mode/clike";
export { powerShell } from "@codemirror/legacy-modes/mode/powershell";
// @codemirror/lang-yaml (6.1.3) was measured against the legacy YAML mode below and found far
// larger (a lezer-based parser vs. a small StreamParser): B6 item 12 says prefer lang-yaml only
// if it is smaller, so YAML uses the legacy mode instead and lang-yaml is not a dependency here.
export { yaml } from "@codemirror/legacy-modes/mode/yaml";
