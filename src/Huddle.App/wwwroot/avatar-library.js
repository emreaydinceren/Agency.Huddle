// ES module, formerly window.huddleAvatarLibrary in app.js. C# calls it through JsModule.AvatarLibrary; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

const avatarLibrary = {
  // MudBlazor closes a dialog on Escape through a DOM key interceptor on the dialog element, which a Blazor
  // @onkeydown:stopPropagation cannot stop. This listener sits on the search box wrapper, below the dialog:
  // a first Escape with text in the box clears the box (an input event, so the bound value follows) and stops
  // the event before it reaches the dialog; with the box empty it is left alone and closes the dialog.
  guardSearchEscape: function (wrap) {
    wrap.addEventListener("keydown", (e) => {
      const input = e.target;
      if (e.key !== "Escape" || !(input instanceof HTMLInputElement) || input.value === "") {
        return;
      }
      e.stopPropagation();
      input.value = "";
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
  },
};

export const { guardSearchEscape } = avatarLibrary;
