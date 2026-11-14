// ES module, formerly window.huddleFeed in app.js. C# calls it through JsModule.Feed; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

// The Feed list's scroll awareness (Feed spec 6.12, D-22): the list is not the scroller itself, so both calls act on the
// nearest scrollable ancestor (the panel body or the page). Unavailable scrolling reads as "at the top".
const feed = {
    scroller(el) {
        for (let node = el; node; node = node.parentElement) {
            const overflow = getComputedStyle(node).overflowY;
            if ((overflow === "auto" || overflow === "scroll") && node.scrollHeight > node.clientHeight) {
                return node;
            }
        }

        return document.scrollingElement;
    },
    // True when the list is scrolled down by more than 4 px.
    isScrolled(el) {
        const scroller = feed.scroller(el);
        return !!scroller && scroller.scrollTop > 4;
    },
    scrollToTop(el) {
        const scroller = feed.scroller(el);
        if (scroller) {
            scroller.scrollTop = 0;
        }
    },
    // Moves focus onto the checked radio of a group, after the arrow keys changed the choice.
    focusChecked(group) {
        const checkedChip = group && group.querySelector("[role='radio'][aria-checked='true']");
        if (checkedChip) {
            checkedChip.focus();
        }
    },
};

export const { isScrolled, scrollToTop, focusChecked } = feed;
