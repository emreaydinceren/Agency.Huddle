// The one pointer drag and the one arrow-key step behind the app's resize handles: resize.js (the left and right
// sidebars) and task-list-resize.js (the Tasks List's columns) both import it. Each caller keeps what differs: what
// a move changes, how it turns pointer pixels into CSS pixels, its step sizes, Home and End, and what it reports to
// .NET when a resize is done. Nothing here reaches .NET, so it is imported by those two modules, not by C#.

/**
 * How far a key press moves a horizontal handle: -step for ArrowLeft and +step for ArrowRight (largeStep with
 * Shift), or 0 for any other key, which the caller then leaves alone.
 */
export function arrowStep(event, step, largeStep) {
  const direction = event.key === "ArrowLeft" ? -1 : event.key === "ArrowRight" ? 1 : 0;
  return direction * (event.shiftKey ? largeStep : step);
}

/**
 * Starts a drag from a pointerdown on handle. It captures the pointer, so the drag keeps going outside the handle,
 * and stops the browser's own text selection. Each pointermove then calls onMove with how far the pointer is from
 * where it went down, in CSS pixels: the pointer distance divided by scale(), which is read on every move. The first
 * pointerup or pointercancel ends the drag: the listeners go, the capture is released and onEnd is called once.
 */
export function startDrag(handle, event, { scale, onMove, onEnd }) {
  const startX = event.clientX;
  handle.setPointerCapture(event.pointerId);
  event.preventDefault();

  const move = (e) => onMove((e.clientX - startX) / scale());

  const stop = (e) => {
    handle.removeEventListener("pointermove", move);
    handle.removeEventListener("pointerup", stop);
    handle.removeEventListener("pointercancel", stop);
    if (handle.hasPointerCapture(e.pointerId)) {
      handle.releasePointerCapture(e.pointerId);
    }
    onEnd();
  };

  handle.addEventListener("pointermove", move);
  handle.addEventListener("pointerup", stop);
  handle.addEventListener("pointercancel", stop);
}
