// Column resizing for the Tasks List (".task-list-grid"). It runs entirely in the browser: MudDataGrid's own resizing
// makes a server round trip per pointer move, which lags and loses a quick drag, so the grid's own is switched off.
//
// Every header cell but the last gets a drag handle on its right border. Dragging it trades width between the column
// before the border and the column after it, so the border follows the pointer and the table keeps its width. At the
// start of a drag every column is frozen at its current width, so only those two columns change. Neither can go
// narrower than MIN_WIDTH or than its content can fit. A header cell the grid re-renders gets its handle back.
//
// An ES module: importing it (JsModule.TaskListResize, which every circuit imports when it opens) starts the handles
// and the listeners below; TaskListView then registers itself through the exported register.

const GRID = ".task-list-grid";
const HANDLE = "task-col-resizer";
const MIN_WIDTH = 56;
const KEY_STEP = 16;
const KEY_STEP_LARGE = 64;

// The Blazor TaskListView registers itself here so a finished drag can be saved to the View.
let listener = null;
/** Registers the Blazor list that a finished drag reports its widths to. */
export function register(reference) {
  listener = reference;
}

/** Forgets the registered list. */
export function unregister() {
  listener = null;
}

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
      handle.setAttribute("tabindex", "0");
      handle.setAttribute("aria-label", "Resize " + (cell.textContent || "").trim() + " column");
      cell.appendChild(handle);
    });
  }
}

/** The width style value of a cell: its computed width, which follows the cell's own box-sizing. */
function styleWidth(cell) {
  return parseFloat(getComputedStyle(cell).width);
}

/**
 * How many CSS pixels one pixel of pointer movement is: pointer coordinates and bounding boxes are in visual
 * pixels while widths set in a style are not; the two agree unless the page is zoomed (ADR-0056 removed the zoom).
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

/**
 * Freezes every column of the handle's row at its current width and returns the drag state for the border: the
 * two columns either side of it, the pointer scale and how far the border may move either way. Null for the last
 * column, which has no neighbour to trade with.
 */
function beginResize(handle) {
  const cell = handle.parentElement;
  const next = cell.nextElementSibling;
  if (!next) {
    return null;
  }

  const cells = Array.from(cell.parentElement.children);
  const widths = cells.map(styleWidth);
  cells.forEach((c, i) => {
    c.style.width = widths[i] + "px";
  });

  const startCell = styleWidth(cell);
  const startNext = styleWidth(next);
  return {
    cell,
    next,
    cells,
    startCell,
    startNext,
    scale: pointerScale(cell),
    lowest: minWidthOf(cell) - startCell,
    highest: startNext - minWidthOf(next),
  };
}

/** Moves the border by delta CSS pixels from where the drag began, clamped to what both columns allow. */
function applyDelta(state, delta) {
  const clamped = Math.max(state.lowest, Math.min(state.highest, delta));
  state.cell.style.width = state.startCell + clamped + "px";
  state.next.style.width = state.startNext - clamped + "px";
}

/** Tells the Blazor list the final widths of every column, so they are saved to the View. */
function commit(state) {
  if (listener) {
    listener.invokeMethodAsync("OnColumnsResized", state.cells.map(styleWidth));
  }
}

document.addEventListener("pointerdown", (event) => {
  if (event.button !== 0 || !(event.target instanceof Element)) {
    return;
  }

  const handle = event.target.closest("." + HANDLE);
  if (!handle) {
    return;
  }

  const state = beginResize(handle);
  if (!state) {
    return;
  }

  const startX = event.clientX;
  handle.setPointerCapture(event.pointerId);
  handle.classList.add("active");
  event.preventDefault();

  const move = (e) => applyDelta(state, (e.clientX - startX) / state.scale);

  const stop = () => {
    handle.classList.remove("active");
    handle.removeEventListener("pointermove", move);
    handle.removeEventListener("pointerup", stop);
    handle.removeEventListener("pointercancel", stop);
    commit(state);
  };

  handle.addEventListener("pointermove", move);
  handle.addEventListener("pointerup", stop);
  handle.addEventListener("pointercancel", stop);
});

// Keyboard: Left and Right move the focused border by KEY_STEP pixels (Shift: KEY_STEP_LARGE), like the Task detail's
// own resize handle. Each press is a complete resize, so it is saved as it is made.
document.addEventListener("keydown", (event) => {
  if (!(event.target instanceof Element) || !event.target.classList.contains(HANDLE)) {
    return;
  }

  const direction = event.key === "ArrowLeft" ? -1 : event.key === "ArrowRight" ? 1 : 0;
  if (direction === 0) {
    return;
  }

  event.preventDefault();
  const state = beginResize(event.target);
  if (!state) {
    return;
  }

  applyDelta(state, direction * (event.shiftKey ? KEY_STEP_LARGE : KEY_STEP));
  commit(state);
});

new MutationObserver(ensureHandles).observe(document.body, { childList: true, subtree: true });
ensureHandles();
