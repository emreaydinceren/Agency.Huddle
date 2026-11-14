// ES module, formerly window.huddleTabs in app.js. C# calls it through JsModule.Tabs; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

// Tab shortcuts (docs/Huddle.Tabs-Specifications.md). The browser keeps Ctrl+W, Ctrl+Tab and
// Ctrl+PageUp/PageDown for its own tabs and a page cannot take them, so these use Alt:
// Alt+W closes the active Tab, Alt+PageDown / Alt+PageUp move to the next / previous one.
const tabs = {
  _handler: null,
  attach(dotnetRef) {
    tabs.detach();
    tabs._handler = (e) => {
      if (!e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) {
        return;
      }

      let action = null;
      if (e.key === "w" || e.key === "W") {
        action = "close";
      } else if (e.key === "PageDown") {
        action = "next";
      } else if (e.key === "PageUp") {
        action = "previous";
      }

      if (action) {
        e.preventDefault();
        dotnetRef.invokeMethodAsync("ShortcutAsync", action).catch(() => { });
      }
    };
    document.addEventListener("keydown", tabs._handler);
  },
  detach() {
    if (tabs._handler) {
      document.removeEventListener("keydown", tabs._handler);
      tabs._handler = null;
    }
  }
};

export const { attach, detach } = tabs;
