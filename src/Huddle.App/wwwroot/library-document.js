// ES module, formerly window.huddleLibrary in app.js. C# calls it through JsModule.LibraryDocument; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

const libraryDocument = {
  // Registers a visibilitychange listener that calls the given .NET reference's OnVisible method
  // whenever the page becomes visible again, so an open document can refresh itself.
  onVisible: function (dotNetRef) {
    const handler = () => {
      if (document.visibilityState === "visible") {
        dotNetRef.invokeMethodAsync("OnVisible");
      }
    };
    dotNetRef._huddleLibraryVisibilityHandler = handler;
    document.addEventListener("visibilitychange", handler);
  },
  offVisible: function (dotNetRef) {
    const handler = dotNetRef._huddleLibraryVisibilityHandler;
    if (handler) {
      document.removeEventListener("visibilitychange", handler);
      delete dotNetRef._huddleLibraryVisibilityHandler;
    }
  },
  // Scrolls the heading with the given id inside root to the top of its scroll container. The id is
  // escaped so a heading id with selector characters cannot break the query. Returns whether it was found.
  scrollToHeading(root, id) {
    const el = root && root.querySelector("#" + CSS.escape(id));
    if (!el) {
      return false;
    }
    el.scrollIntoView({ block: "start" });
    return true;
  },
};

export const { onVisible, offVisible, scrollToHeading } = libraryDocument;
