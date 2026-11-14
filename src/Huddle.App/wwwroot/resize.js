// ES module, formerly window.huddleResize in app.js. C# calls it through JsModule.Resize; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

import * as storage from "./storage.js";

const resize = {
  // Drag-to-resize for the left drawer's Sidebar (the Activity bar is fixed). MudBlazor's layout reads the drawer's
  // width from "--mud-drawer-width-left" on .mud-layout, so a drag sets that variable live; the settled width is then
  // reported to .NET, whose render writes the same value (and so replaces this inline one). barPx is the part of the
  // drawer that is not Sidebar. Anchor.Start puts the drawer on the left, so dragging right (or ArrowRight) grows it.
  // side is "left" (the default) or "right": the Right sidebar's handle sits on its left edge, so dragging left grows it.
  attachSidebar(handle, dotnet, storageKey, minPx, maxPx, defaultPx, barPx, side = "left") {
    const layout = handle.closest(".mud-layout");
    if (!layout) {
      return;
    }

    const dir = side === "right" ? -1 : 1;
    const clamp = (px) => Math.min(maxPx, Math.max(minPx, Math.round(px)));
    const apply = (px) => {
      const width = clamp(px);
      layout.style.setProperty("--mud-drawer-width-" + side, (width + barPx) + "px");
      handle.setAttribute("aria-valuenow", String(width));
      return width;
    };
    const commit = (px) => {
      const width = apply(px);
      storage.set(storageKey, String(width));
      return dotnet.invokeMethodAsync("OnWidthCommittedAsync", width);
    };
    const currentWidth = () => {
      const drawer = handle.closest(".mud-drawer");
      return (drawer ? drawer.offsetWidth : defaultPx + barPx) - barPx;
    };

    const stored = parseInt(storage.get(storageKey) ?? "", 10);
    if (Number.isFinite(stored) && clamp(stored) !== defaultPx) {
      commit(stored);
    }

    handle.addEventListener("keydown", (e) => {
      const step = e.shiftKey ? 64 : 16;
      let next;
      switch (e.key) {
        case "ArrowRight": next = currentWidth() + (dir * step); break;
        case "ArrowLeft": next = currentWidth() - (dir * step); break;
        case "Home": next = minPx; break;
        case "End": next = maxPx; break;
        default: return;
      }
      e.preventDefault();
      commit(next);
    });

    let dragging = false;
    let startX = 0;
    let startWidth = 0;
    let lastWidth = 0;

    handle.addEventListener("pointerdown", (e) => {
      dragging = true;
      startX = e.clientX;
      startWidth = currentWidth();
      lastWidth = startWidth;
      handle.setPointerCapture(e.pointerId);
      document.body.classList.add("sidebar-resizing");
      e.preventDefault();
    });

    handle.addEventListener("pointermove", (e) => {
      if (!dragging) {
        return;
      }
      const zoom = parseFloat(getComputedStyle(document.documentElement).zoom) || 1;
      lastWidth = apply(startWidth + (dir * (e.clientX - startX) / zoom));
    });

    const endDrag = (e) => {
      if (!dragging) {
        return;
      }
      dragging = false;
      handle.releasePointerCapture(e.pointerId);
      document.body.classList.remove("sidebar-resizing");
      commit(lastWidth);
    };

    handle.addEventListener("pointerup", endDrag);
    handle.addEventListener("pointercancel", endDrag);
  },
};

export const { attachSidebar } = resize;
