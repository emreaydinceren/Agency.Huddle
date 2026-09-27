window.teamComposer = {
    // The `#` picker (Spec §13.13.4) lives in this same keydown handler rather than a second one,
    // with its open/query state kept on the element itself (el._picker) so several Composer
    // instances on the page never share state.
    attach(el, dotnetRef) {
        el._picker = { open: false };

        el.addEventListener("keydown", async (e) => {
            const picker = el._picker;

            if (picker.open) {
                if (e.key === "Enter" && !e.shiftKey) {
                    if (e.isComposing) {
                        return;
                    }
                    e.preventDefault();
                    const id = await dotnetRef.invokeMethodAsync("PickAsync");
                    if (id) {
                        window.teamComposer.insertTask(el, id);
                    }
                    return;
                }
                if (e.key === "Tab") {
                    e.preventDefault();
                    const id = await dotnetRef.invokeMethodAsync("PickAsync");
                    if (id) {
                        window.teamComposer.insertTask(el, id);
                    }
                    return;
                }
                if (e.key === "ArrowUp") {
                    e.preventDefault();
                    await dotnetRef.invokeMethodAsync("MoveAsync", -1);
                    return;
                }
                if (e.key === "ArrowDown") {
                    e.preventDefault();
                    await dotnetRef.invokeMethodAsync("MoveAsync", 1);
                    return;
                }
                if (e.key === "Escape") {
                    picker.open = false;
                    await dotnetRef.invokeMethodAsync("TaskQueryAsync", null);
                    return;
                }
                return;
            }

            if (e.key === "Enter" && !e.shiftKey) {
                if (e.isComposing) {
                    return;
                }
                e.preventDefault();
                await dotnetRef.invokeMethodAsync("SendAsync", el.value);
                el.value = "";
                el._picker.open = false;
            }
        });

        el.addEventListener("input", async () => {
            const beforeCaret = el.value.substring(0, el.selectionStart);
            const match = beforeCaret.match(/(?:^|\s)#([A-Za-z0-9-]{0,40})$/);
            if (match) {
                el._picker.open = true;
                await dotnetRef.invokeMethodAsync("TaskQueryAsync", match[1]);
            } else if (el._picker.open) {
                el._picker.open = false;
                await dotnetRef.invokeMethodAsync("TaskQueryAsync", null);
            }
        });

        el.addEventListener("blur", () => {
            setTimeout(async () => {
                try {
                    if (el._picker.open) {
                        el._picker.open = false;
                        await dotnetRef.invokeMethodAsync("TaskQueryAsync", null);
                    }
                } catch {
                    // The Composer, or its DotNetObjectReference, may already be disposed by the
                    // time this fires - Dispose can run before a queued 150 ms timer.
                }
            }, 150);
        });
    },

    // Replaces the `#query` token ending at the caret with the plain id plus a space (D-33: the
    // `#` is not kept), puts the caret after it, refocuses the textarea and closes the picker.
    insertTask(el, id) {
        const beforeCaret = el.value.substring(0, el.selectionStart);
        const afterCaret = el.value.substring(el.selectionStart);
        const replaced = beforeCaret.replace(/(?:^|\s)#([A-Za-z0-9-]{0,40})$/, (whole) => {
            const lead = whole.startsWith("#") ? "" : whole[0];
            return lead + id + " ";
        });
        el.value = replaced + afterCaret;
        const caret = replaced.length;
        el.selectionStart = el.selectionEnd = caret;
        el._picker.open = false;
        el.focus();
    },
};

window.teamScroll = {
    toBottom(el) {
        el.scrollTop = el.scrollHeight;
    },
};

window.huddleStorage = {
  get: function (key) { try { return window.localStorage.getItem(key); } catch { return null; } },
  set: function (key, value) { try { window.localStorage.setItem(key, value); } catch { } }
};

window.huddleResize = {
  // Drag-to-resize for the Task detail panel's End-anchored MudDrawer. Writes straight to the
  // drawer's own "--mud-drawer-width" instance variable on every pointermove - the same one
  // MudBlazor's compiled CSS already falls back to var(--mud-drawer-width-right) for - so dragging
  // never round-trips to the server; only the width huddleStorage remembers does, on release.
  attachDrawer(handle, storageKey, minPx, maxPx, defaultPx) {
    const clamp = (px) => Math.min(maxPx, Math.max(minPx, px));
    const drawer = handle.closest(".mud-drawer");
    if (!drawer) {
      return;
    }

    const stored = window.huddleStorage.get(storageKey);
    const restored = stored ? parseInt(stored, 10) : NaN;
    drawer.style.setProperty("--mud-drawer-width", clamp(Number.isFinite(restored) ? restored : defaultPx) + "px");

    let dragging = false;
    let startX = 0;
    let startWidth = 0;

    handle.addEventListener("pointerdown", (e) => {
      dragging = true;
      startX = e.clientX;
      startWidth = drawer.getBoundingClientRect().width;
      handle.setPointerCapture(e.pointerId);
      e.preventDefault();
    });

    handle.addEventListener("pointermove", (e) => {
      if (!dragging) {
        return;
      }
      // Anchor.End puts the drawer on the right, so dragging the handle left (clientX decreasing,
      // startX - e.clientX growing) is what should grow it.
      drawer.style.setProperty("--mud-drawer-width", clamp(startWidth + (startX - e.clientX)) + "px");
    });

    const endDrag = (e) => {
      if (!dragging) {
        return;
      }
      dragging = false;
      handle.releasePointerCapture(e.pointerId);
      window.huddleStorage.set(storageKey, String(Math.round(drawer.getBoundingClientRect().width)));
    };

    handle.addEventListener("pointerup", endDrag);
    handle.addEventListener("pointercancel", endDrag);
  },
};

window.huddleClipboard = {
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
