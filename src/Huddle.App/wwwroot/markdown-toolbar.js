// The Markdown toolbar's browser behaviour that does not depend on what it edits (Spec §6.7; Task read view spec D-7).
// The Library's CodeMirror editor and a Task's Description textarea both wire the same `MarkdownToolbar` row with
// it, so the two read the same: a click runs the button's command without taking focus from the field, the buttons
// follow the WAI-ARIA toolbar pattern (a roving tabindex: one tab stop, Left/Right/Home/End move, Escape returns to
// the field). What a command DOES to the text is the caller's: it is handed `runCommand(name)` and `returnFocus()`.

/**
 * Wires `toolbar` with ONE delegated listener set. `runCommand(name)` runs the clicked button's `data-md-command`;
 * `returnFocus()` is called on Escape. `mousedown` is prevented on a button so the field keeps its focus and
 * selection. Returns the function that detaches every listener.
 */
export function wireToolbar(toolbar, runCommand, returnFocus) {
  const buttons = () => [...toolbar.querySelectorAll("[data-md-command]")];
  const commandButton = (target) => (target instanceof Element ? target.closest("[data-md-command]") : null);

  const setActive = (active) => {
    for (const button of buttons()) {
      button.tabIndex = button === active ? 0 : -1;
    }
  };

  const onMouseDown = (event) => {
    if (commandButton(event.target) !== null) {
      event.preventDefault();
    }
  };

  const onClick = (event) => {
    const button = commandButton(event.target);
    if (button !== null) {
      runCommand(button.dataset.mdCommand);
    }
  };

  const onFocusIn = (event) => {
    const button = commandButton(event.target);
    if (button !== null) {
      setActive(button);
    }
  };

  const onKeyDown = (event) => {
    if (event.key === "Escape") {
      returnFocus();
      return;
    }

    const all = buttons();
    const current = all.indexOf(commandButton(event.target));
    if (current === -1) {
      return;
    }

    let next = -1;
    switch (event.key) {
      case "ArrowRight":
        next = (current + 1) % all.length;
        break;
      case "ArrowLeft":
        next = (current - 1 + all.length) % all.length;
        break;
      case "Home":
        next = 0;
        break;
      case "End":
        next = all.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    setActive(all[next]);
    all[next].focus();
  };

  toolbar.addEventListener("mousedown", onMouseDown);
  toolbar.addEventListener("click", onClick);
  toolbar.addEventListener("focusin", onFocusIn);
  toolbar.addEventListener("keydown", onKeyDown);

  return () => {
    toolbar.removeEventListener("mousedown", onMouseDown);
    toolbar.removeEventListener("click", onClick);
    toolbar.removeEventListener("focusin", onFocusIn);
    toolbar.removeEventListener("keydown", onKeyDown);
  };
}
