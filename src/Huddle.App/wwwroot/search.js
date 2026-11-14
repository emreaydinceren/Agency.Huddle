// ES module, formerly window.huddleSearch in app.js. C# calls it through JsModule.Search; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

// Global search keys (docs/Huddle.GlobalSearch-Specifications.md §5.2, §6.5). One keydown listener on window in
// the capture phase, so the composer and CodeMirror cannot swallow a key first. Ctrl+K and "/" open the box;
// Alt+1..6 and Ctrl+Left/Right move between the chips, and only while the dropdown is open (setOpen), so they
// never collide with the Tab shortcuts in tabs.js. Edge may keep Ctrl+K for its address bar: if so, set
// shortcutNeedsShift below to true and the shortcut becomes Ctrl+Shift+K (D-4). It is module state now, not a window
// property, so it is changed here in the source, not from the browser console.
const search = {
  _handler: null,
  _open: false,
  _previous: null,
  shortcutNeedsShift: false,
  attach(dotnetRef, host) {
    search.detach();
    search._handler = (e) => {
      if (e.ctrlKey && !e.altKey && !e.metaKey && e.shiftKey === search.shortcutNeedsShift
        && e.key.toLowerCase() === "k") {
        e.preventDefault();
        openBox();
        return;
      }

      if (e.key === "/" && !e.ctrlKey && !e.altKey && !e.metaKey) {
        const target = e.target;
        const tag = target && target.tagName;
        if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT"
          || (target && target.closest && (target.closest("[contenteditable]") || target.closest(".cm-editor")))) {
          return;
        }

        e.preventDefault();
        openBox();
        return;
      }

      if (!search._open) {
        return;
      }

      if (e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey) {
        const digit = e.code && e.code.startsWith("Digit") ? Number(e.code.substring(5)) : Number(e.key);
        if (digit >= 1 && digit <= 6) {
          e.preventDefault();
          dotnetRef.invokeMethodAsync("SelectChipAsync", digit).catch(() => { });
        }
      } else if (e.ctrlKey && !e.altKey && !e.metaKey && !e.shiftKey && (e.key === "ArrowLeft" || e.key === "ArrowRight")) {
        e.preventDefault();
        dotnetRef.invokeMethodAsync("MoveChipAsync", e.key === "ArrowLeft" ? -1 : 1).catch(() => { });
      }
    };
    const openBox = () => {
      const input = host && host.querySelector("input");
      if (!input) {
        return;
      }

      if (document.activeElement !== input) {
        search._previous = document.activeElement;
      }

      input.focus();
      dotnetRef.invokeMethodAsync("OpenAsync").catch(() => { });
    };
    window.addEventListener("keydown", search._handler, true);
  },
  detach() {
    if (search._handler) {
      window.removeEventListener("keydown", search._handler, true);
      search._handler = null;
    }

    search._open = false;
    search._previous = null;
  },
  setOpen(open) {
    search._open = open;
  },
  restoreFocus() {
    const previous = search._previous;
    search._previous = null;
    if (previous && previous.isConnected && typeof previous.focus === "function") {
      previous.focus();
    }
  }
};

export const { attach, detach, setOpen, restoreFocus } = search;
