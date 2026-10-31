// The avatar editor's emoji picker (Spec: team avatars, section 7). The <emoji-picker> custom element comes from the vendored
// bundle in lib/emoji-picker/, which is imported only when a picker is first opened, so a page that never opens one never
// downloads it. The Blazor AvatarEditor renders the element; this module waits for it, matches it to the current theme, and
// calls back into .NET with the emoji the Human chose. It never fetches from a CDN: the bundle's data source is the local
// lib/emoji-picker/data.json.

let bundle = null;

/** Imports the vendored bundle once; it registers the `emoji-picker` element as a side effect. */
function loadBundle() {
  bundle ??= import("./lib/emoji-picker/emoji-picker.bundle.js");
  return bundle;
}

/** Resolves with the element once Blazor has rendered it (a popover paints in its own render pass), or null after about a second. */
function whenPresent(id) {
  return new Promise((resolve) => {
    let frames = 0;
    const look = () => {
      const element = document.getElementById(id);
      if (element !== null) {
        resolve(element);
      } else if (++frames > 60) {
        resolve(null);
      } else {
        requestAnimationFrame(look);
      }
    };
    look();
  });
}

/** MainLayout renders a hidden `.theme-mode-dark` span while the Theme is a Dark one. */
function isDarkTheme() {
  return document.querySelector(".theme-mode-dark") !== null;
}

/**
 * Wires the `<emoji-picker>` with element id `id`. `emoji-click` calls `reference.OnEmojiPickedAsync(emoji)`, and Escape
 * calls `reference.OnEmojiClosedAsync()`. Returns a handle whose `dispose()` removes the listeners, or null when the
 * element never appeared.
 */
export async function attach(id, reference) {
  await loadBundle();
  const picker = await whenPresent(id);
  if (picker === null) {
    return null;
  }

  // The picker's own colours follow prefers-color-scheme unless it carries a `light` or `dark` class; app.css maps its
  // variables to the Theme, and this class keeps its form controls and scrollbars (color-scheme) in step with it.
  picker.classList.remove("light", "dark");
  picker.classList.add(isDarkTheme() ? "dark" : "light");

  const onEmoji = (event) => {
    const unicode = event.detail?.unicode;
    if (typeof unicode === "string" && unicode.length > 0) {
      reference.invokeMethodAsync("OnEmojiPickedAsync", unicode);
    }
  };
  const onKey = (event) => {
    if (event.key === "Escape") {
      event.stopPropagation();
      reference.invokeMethodAsync("OnEmojiClosedAsync");
    }
  };

  picker.addEventListener("emoji-click", onEmoji);
  picker.addEventListener("keydown", onKey);
  picker.shadowRoot?.querySelector("input.search")?.focus();

  return {
    dispose() {
      picker.removeEventListener("emoji-click", onEmoji);
      picker.removeEventListener("keydown", onKey);
    },
  };
}
