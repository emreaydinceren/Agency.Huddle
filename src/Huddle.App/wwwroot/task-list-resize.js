// Column resizing for the Tasks List (".task-list-grid"). It runs entirely in the browser: MudDataGrid's own resizing
// makes a server round trip per pointer move, which lags and loses a quick drag, so the grid's own is switched off.
//
// Every header cell but the last gets a drag handle on its right border. Dragging it trades width between the column
// before the border and the column after it, so the border follows the pointer and the table keeps its width. At the
// start of a drag every column is frozen at its current width, so only those two columns change. Neither can go
// narrower than MIN_WIDTH or than its content can fit. A header cell the grid re-renders gets its handle back.
(function () {
  "use strict";

  const GRID = ".task-list-grid";
  const HANDLE = "task-col-resizer";
  const MIN_WIDTH = 56;

  // The Blazor TaskListView registers itself here so a finished drag can be saved to the View.
  let listener = null;
  window.taskListResize = {
    register: (reference) => { listener = reference; },
    unregister: () => { listener = null; },
  };

  /** Adds a handle to every header cell of every List grid that does not have one yet, except the last cell. */
  function ensureHandles() {
    for (const row of document.querySelectorAll(GRID + " thead tr")) {
      const cells = Array.from(row.children);
      cells.forEach((cell, index) => {
        if (index === cells.length - 1 || cell.querySelector(":scope > ." + HANDLE)) {
          return;
        }

        const handle = document.createElement("div");
        handle.className = HANDLE;
        handle.setAttribute("role", "separator");
        handle.setAttribute("aria-orientation", "vertical");
        cell.appendChild(handle);
      });
    }
  }

  /** The width style value of a cell: its computed width, which follows the cell's own box-sizing. */
  function styleWidth(cell) {
    return parseFloat(getComputedStyle(cell).width);
  }

  /**
   * How many CSS pixels one pixel of pointer movement is: the page runs at a UI zoom (html { zoom }), so pointer
   * coordinates and bounding boxes are in zoomed pixels while widths set in a style are not.
   */
  function pointerScale(cell) {
    return cell.offsetWidth > 0 ? cell.getBoundingClientRect().width / cell.offsetWidth : 1;
  }

  /**
   * The narrowest a column may be: the floor, or the width the browser itself gives it when asked for 1px, which is
   * its widest content - the header and every cell below it - that cannot wrap. The cell's own width is restored.
   */
  function minWidthOf(cell) {
    const previous = cell.style.width;
    cell.style.width = "1px";
    const narrowest = cell.offsetWidth;
    cell.style.width = previous;
    return Math.max(MIN_WIDTH, narrowest);
  }

  document.addEventListener("pointerdown", (event) => {
    if (event.button !== 0 || !(event.target instanceof Element)) {
      return;
    }

    const handle = event.target.closest("." + HANDLE);
    if (!handle) {
      return;
    }

    const cell = handle.parentElement;
    const next = cell.nextElementSibling;
    if (!next) {
      return;
    }

    const cells = Array.from(cell.parentElement.children);
    const widths = cells.map(styleWidth);
    cells.forEach((c, i) => {
      c.style.width = widths[i] + "px";
    });

    const startX = event.clientX;
    const scale = pointerScale(cell);
    const startCell = styleWidth(cell);
    const startNext = styleWidth(next);
    const lowest = minWidthOf(cell) - startCell;
    const highest = startNext - minWidthOf(next);

    handle.setPointerCapture(event.pointerId);
    handle.classList.add("active");
    event.preventDefault();

    const move = (e) => {
      const delta = Math.max(lowest, Math.min(highest, (e.clientX - startX) / scale));
      cell.style.width = startCell + delta + "px";
      next.style.width = startNext - delta + "px";
    };

    const stop = () => {
      handle.classList.remove("active");
      handle.removeEventListener("pointermove", move);
      handle.removeEventListener("pointerup", stop);
      handle.removeEventListener("pointercancel", stop);
      if (listener) {
        listener.invokeMethodAsync("OnColumnsResized", cells.map(styleWidth));
      }
    };

    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", stop);
    handle.addEventListener("pointercancel", stop);
  });

  new MutationObserver(ensureHandles).observe(document.body, { childList: true, subtree: true });
  ensureHandles();
})();
