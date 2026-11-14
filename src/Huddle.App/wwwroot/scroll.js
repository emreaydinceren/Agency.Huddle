// ES module, formerly window.teamScroll in app.js. C# calls it through JsModule.Scroll; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

const scroll = {
    toBottom(el) {
        el.scrollTop = el.scrollHeight;
    },
    // Scrolls the Message row with this id to the middle of the list, moves focus to it without
    // scrolling again, and flashes it (Global Search spec 9.1). Returns false when no row has the id.
    toMessage(el, id) {
        const row = el.querySelector('[data-message-id="' + CSS.escape(id) + '"]');
        if (!row) {
            return false;
        }

        row.scrollIntoView({ block: "center" });
        row.focus({ preventScroll: true });
        row.classList.remove("search-hit-flash");
        void row.offsetWidth;
        row.classList.add("search-hit-flash");
        const clear = () => row.classList.remove("search-hit-flash");
        row.addEventListener("animationend", clear, { once: true });
        setTimeout(clear, 2000);
        return true;
    },
    // Scrolls the element with this id into the middle of the view (the Pack card a "ready" snackbar's Show points at,
    // Skill Packs spec 6.9.9). Returns false when no element has the id.
    intoView(id) {
        const el = document.getElementById(id);
        if (!el) {
            return false;
        }

        el.scrollIntoView({ block: "center" });
        return true;
    },
    // True when the list is scrolled to within 4 px of its bottom, in its own coordinates.
    isAtBottom(el) {
        return el.scrollHeight - el.scrollTop - el.clientHeight <= 4;
    },
};

export const { toBottom, toMessage, intoView, isAtBottom } = scroll;
