// Drives src/Huddle.App/wwwroot/pointer-drag.js against a hand-made handle, one scenario per argv[2], and prints a
// JSON object of what happened. PointerDragJsTests runs this with `node` and asserts on the output.
import { readFile } from "node:fs/promises";
import path from "node:path";

// Imported as text through a data: URL for the reason theme-lab-harness.mjs gives: Node 18 (the CI image) loads a
// .js file with no package.json beside it as CommonJS and fails on `export`.
const root = process.argv[3];
const source = await readFile(path.join(root, "src", "Huddle.App", "wwwroot", "pointer-drag.js"), "utf8");
const drag = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

/** A handle that records pointer capture and keeps its listeners, so a test can dispatch events to it. */
function fakeHandle() {
  const listeners = new Map();
  const captured = new Set();
  const log = [];
  return {
    log,
    setPointerCapture: (id) => { captured.add(id); log.push("capture " + id); },
    hasPointerCapture: (id) => captured.has(id),
    releasePointerCapture: (id) => { captured.delete(id); log.push("release " + id); },
    addEventListener: (type, fn) => { listeners.set(type, [...(listeners.get(type) ?? []), fn]); },
    removeEventListener: (type, fn) => { listeners.set(type, (listeners.get(type) ?? []).filter((f) => f !== fn)); },
    dispatch: (type, event) => { for (const fn of listeners.get(type) ?? []) { fn(event); } },
    listenerCount: () => [...listeners.values()].reduce((sum, list) => sum + list.length, 0),
  };
}

/** A pointer event at clientX for pointer 7 that records whether its default was prevented. */
function pointer(clientX) {
  const event = { clientX, pointerId: 7, prevented: false };
  event.preventDefault = () => { event.prevented = true; };
  return event;
}

/** Runs one drag that moves to 140 and 80 from 100 and ends with endType, then moves and ends once more. */
function runDrag(endType, scales) {
  const handle = fakeHandle();
  const moves = [];
  let ends = 0;
  let reads = 0;
  const down = pointer(100);
  drag.startDrag(handle, down, {
    scale: () => scales[Math.min(reads++, scales.length - 1)],
    onMove: (delta) => moves.push(delta),
    onEnd: () => { ends++; },
  });
  handle.dispatch("pointermove", pointer(140));
  handle.dispatch("pointermove", pointer(80));
  handle.dispatch(endType, pointer(80));
  handle.dispatch("pointermove", pointer(200));
  handle.dispatch(endType, pointer(200));
  return { moves, ends, prevented: down.prevented, log: handle.log, listenersLeft: handle.listenerCount() };
}

const out = {};
const scenario = process.argv[2];

if (scenario === "arrow-step") {
  const key = (name, shiftKey = false) => drag.arrowStep({ key: name, shiftKey }, 10, 40);
  out.steps = [key("ArrowLeft"), key("ArrowRight"), key("ArrowLeft", true), key("ArrowRight", true), key("Home"), key("a")];
} else if (scenario === "drag-up") {
  out.result = runDrag("pointerup", [2]);
} else if (scenario === "drag-cancel") {
  out.result = runDrag("pointercancel", [1, 4]);
}

process.stdout.write(JSON.stringify(out));
