// ES module, formerly window.huddleClipboard in app.js. C# calls it through JsModule.Clipboard (through ClipboardCopier); only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

const clipboard = {
  // Returns true only when the text really reached the clipboard.
  copy: async function (text) {
    if (window.isSecureContext && navigator.clipboard) {
      try { await navigator.clipboard.writeText(text); return true; } catch { /* fall through */ }
    }
    const area = document.createElement("textarea");
    area.value = text; area.setAttribute("readonly", ""); area.style.position = "fixed"; area.style.opacity = "0";
    document.body.appendChild(area); area.select();
    let ok = false;
    try { ok = document.execCommand("copy"); } catch { ok = false; }
    area.remove();
    return ok;
  }
};

export const { copy } = clipboard;
