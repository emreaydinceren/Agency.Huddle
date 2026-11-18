// ES module, formerly window.huddleResize in app.js. C# calls it through JsModule.Resize; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

import * as storage from "./storage.js";
import { arrowStep, startDrag } from "./pointer-drag.js";

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

    // Arrow keys step by 16px (Shift: 64px) through the shared arrowStep; Home and End jump to the bounds.
    handle.addEventListener("keydown", (e) => {
      let next;
      switch (e.key) {
        case "Home": next = minPx; break;
        case "End": next = maxPx; break;
        default: {
          const delta = arrowStep(e, 16, 64);
          if (delta === 0) {
            return;
          }
          next = currentWidth() + (dir * delta);
        }
      }
      e.preventDefault();
      commit(next);
    });

    // The drag itself is the shared startDrag; the page's CSS zoom, read on every move, turns pointer pixels into
    // CSS pixels.
    const zoom = () => parseFloat(getComputedStyle(document.documentElement).zoom) || 1;
    handle.addEventListener("pointerdown", (e) => {
      const startWidth = currentWidth();
      let lastWidth = startWidth;
      document.body.classList.add("sidebar-resizing");
      startDrag(handle, e, {
        scale: zoom,
        onMove: (delta) => {
          lastWidth = apply(startWidth + (dir * delta));
        },
        onEnd: () => {
          document.body.classList.remove("sidebar-resizing");
          commit(lastWidth);
        },
      });
    });
  },
};

export const { attachSidebar } = resize;
