// ES module, formerly window.huddleSidebar in app.js. C# calls it through JsModule.Sidebar; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

// Remembers which Right sidebar panels a viewer collapsed (Right sidebar spec D-8). A per-viewer
// convenience: storage can be missing or throw, so every access is guarded and a failure reads as
// "expanded".
const sidebar = {
    isCollapsed(id) {
        try {
            return window.localStorage.getItem("huddle.sidebar.collapsed." + id) === "1";
        } catch {
            return false;
        }
    },
    setCollapsed(id, collapsed) {
        try {
            if (collapsed) {
                window.localStorage.setItem("huddle.sidebar.collapsed." + id, "1");
            } else {
                window.localStorage.removeItem("huddle.sidebar.collapsed." + id);
            }
        } catch {
            // Storage is unavailable (private window, blocked site data): the choice is not kept.
        }
    },
    // The Outline's scroll watch: tells the panel (ref.OnHeadingInView) which heading is in view, i.e. the last one
    // whose top has passed the line just under the Tab strip, or the first one when none has. Only a rendered
    // (Read mode) document has heading elements; with none found nothing is reported and a clicked heading stays marked.
    _headingWatch: null,
    watchHeadings(ids, ref) {
        sidebar.unwatchHeadings();
        let last;
        let frame = 0;
        let timer = 0;
        let stopped = false;
        const report = () => {
            frame = 0;
            if (stopped) {
                return;
            }
            let current = null;
            for (const id of ids) {
                const element = document.getElementById(id);
                if (!element) {
                    continue;
                }
                if (current === null || element.getBoundingClientRect().top <= 140) {
                    current = id;
                } else {
                    break;
                }
            }
            if (current !== null && current !== last) {
                last = current;
                // The panel may be gone by the time this lands (its reference is disposed with it): nothing is left to tell.
                ref.invokeMethodAsync("OnHeadingInView", current).catch(() => { });
            }
        };
        const onScroll = () => {
            if (!frame) {
                frame = window.requestAnimationFrame(report);
            }
        };
        document.addEventListener("scroll", onScroll, { capture: true, passive: true });
        sidebar._headingWatch = () => {
            // Stop the pending frame and the first-report timer too, or one can still call into a disposed panel.
            stopped = true;
            window.clearTimeout(timer);
            if (frame) {
                window.cancelAnimationFrame(frame);
            }
            document.removeEventListener("scroll", onScroll, { capture: true });
        };
        timer = window.setTimeout(report, 100);
    },
    unwatchHeadings() {
        const stop = sidebar._headingWatch;
        if (stop) {
            stop();
            sidebar._headingWatch = null;
        }
    },
};

export const { isCollapsed, setCollapsed, watchHeadings, unwatchHeadings } = sidebar;
