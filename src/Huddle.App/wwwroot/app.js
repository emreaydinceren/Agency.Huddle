window.teamComposer = {
    // The `#` picker (Spec §13.13.4) lives in this same keydown handler rather than a second one,
    // with its open/query state kept on the element itself (el._picker) so several Composer
    // instances on the page never share state.
    //
    // A draft is the unsent text, kept per key (the Composer passes its Room) in sessionStorage, so
    // switching Tabs or Rooms and coming back finds what was typed. It is cleared on send.
    // sessionStorage and not localStorage: a half-written message should survive a reload, not an
    // app restart.
    draftGet(key) {
        try { return window.sessionStorage.getItem(key); } catch { return null; }
    },

    draftSet(key, value) {
        try {
            if (value) {
                window.sessionStorage.setItem(key, value);
            } else {
                window.sessionStorage.removeItem(key);
            }
        } catch { }
    },

    // Points the textarea at another draft: what is typed stays under the old key (it was saved on
    // input) and the new key's text, if any, replaces it.
    setDraftKey(el, key) {
        el._draftKey = key;
        el.value = (key && window.teamComposer.draftGet(key)) || "";
        el._picker.open = false;
        el._picker.suppressFrom = null;
        el._typedNotified = false;
        window.teamComposer.fit(el);
    },

    // Grows the box to fit its text. The CSS min-height (3 lines) and max-height (18 lines) bound it, so past
    // 18 lines the height stops and the box scrolls. Height goes back to auto first so the box can shrink
    // when text is deleted or the draft is cleared; the border is added because scrollHeight leaves it out.
    fit(el) {
        if (!el || !el.style) {
            return;
        }

        el.style.height = "auto";
        const border = el.offsetHeight - el.clientHeight;
        el.style.height = (el.scrollHeight + border) + "px";
    },

    // Refits when the box's width changes (the window or a pane was resized): text that wrapped to many lines
    // when narrow no longer does, and without this an empty-looking Composer stayed as tall as it had grown.
    // Only a width change refits - fit() itself changes the box's height, which must not feed the observer.
    // detach() disconnects it when the Composer is disposed.
    observeResize(el) {
        if (typeof ResizeObserver !== "function") {
            return;
        }

        const box = el.closest(".composer-box") || el;
        let lastWidth = null;
        el._resizeObserver = new ResizeObserver((entries) => {
            const width = entries[0] ? entries[0].contentRect.width : null;
            if (width === lastWidth) {
                return;
            }

            const first = lastWidth === null;
            lastWidth = width;
            if (!first) {
                window.teamComposer.fit(el);
            }
        });
        el._resizeObserver.observe(box);
    },

    detach(el) {
        if (el && el._resizeObserver) {
            el._resizeObserver.disconnect();
            el._resizeObserver = null;
        }
    },

    // Puts dropped or pasted files into the hidden file input and fires `change`, so Blazor's
    // InputFile handles them exactly as a pick from the paperclip.
    handOver(input, files) {
        const transfer = new DataTransfer();
        for (const file of files) {
            transfer.items.add(file);
        }
        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    },

    // Paste and drop of files (docs/Huddle.ChatUploads-Specifications.md 6.5). `fileInputHost` is the
    // div wrapping the paperclip's input; null (Library off) means files are never taken, but a file
    // dragged or dropped over the box is still cancelled so the browser does not open it (spec U20).
    // The input is looked up on each paste or drop: the picker may replace it after a pick.
    attachFiles(el, fileInputHost) {
        const findInput = () => (fileInputHost ? fileInputHost.querySelector("input[type=file]") : null);

        const box = el.closest(".composer-box") || el;
        const hasFiles = (transfer) => !!transfer && Array.from(transfer.types || []).includes("Files");

        el.addEventListener("paste", (e) => {
            const input = findInput();
            const files = e.clipboardData ? e.clipboardData.files : null;
            if (!input || !files || files.length === 0) {
                return;
            }
            e.preventDefault();
            window.teamComposer.handOver(input, files);
        });

        box.addEventListener("dragover", (e) => {
            if (!hasFiles(e.dataTransfer)) {
                return;
            }
            e.preventDefault();
            if (findInput()) {
                box.classList.add("composer-dropping");
            }
        });

        box.addEventListener("dragleave", (e) => {
            if (e.relatedTarget && box.contains(e.relatedTarget)) {
                return;
            }
            box.classList.remove("composer-dropping");
        });

        box.addEventListener("drop", (e) => {
            if (!hasFiles(e.dataTransfer)) {
                return;
            }
            e.preventDefault();
            box.classList.remove("composer-dropping");
            const input = findInput();
            if (input && e.dataTransfer.files.length > 0) {
                window.teamComposer.handOver(input, e.dataTransfer.files);
            }
        });
    },

    attach(el, dotnetRef, draftKey, fileInputHost) {
        window.teamComposer.attachFiles(el, fileInputHost);
        el._dotnetRef = dotnetRef;
        el._picker = { open: false, suppressFrom: null, suppressPrefix: "" };
        el._draftKey = draftKey || null;
        el._typedNotified = false;
        if (el._draftKey) {
            const saved = window.teamComposer.draftGet(el._draftKey);
            if (saved) {
                el.value = saved;
            }
        }
        window.teamComposer.fit(el);
        window.teamComposer.observeResize(el);

        el.addEventListener("keydown", async (e) => {
            const picker = el._picker;

            // While an IME composes (isComposing, or the legacy keyCode 229) every key belongs to the IME:
            // arrows choose its candidates, Esc cancels the composition, Enter commits it.
            if (window.teamComposer.isIme(e)) {
                return;
            }

            if (picker.open) {
                // Enter picks and never sends, even with no rows (spec 5.4, OP-7); Tab picks (OP-10).
                if ((e.key === "Enter" && !e.shiftKey) || (e.key === "Tab" && !e.shiftKey)) {
                    e.preventDefault();
                    const text = await dotnetRef.invokeMethodAsync("PickAsync");
                    picker.open = false;
                    if (text) {
                        window.teamComposer.insertReference(el, text);
                    }
                    return;
                }
                if (e.key === "Enter") {
                    // Shift+Enter is a new line and ends the token: the browser default stays, the picker closes.
                    picker.open = false;
                    await dotnetRef.invokeMethodAsync("ReferenceQueryAsync", null, "@", false);
                    return;
                }
                if (e.key === "ArrowUp") {
                    e.preventDefault();
                    await dotnetRef.invokeMethodAsync("MoveAsync", -1);
                    return;
                }
                if (e.key === "ArrowDown") {
                    e.preventDefault();
                    await dotnetRef.invokeMethodAsync("MoveAsync", 1);
                    return;
                }
                if (e.key === "Escape") {
                    picker.open = false;
                    window.teamComposer.suppress(el, null);
                    await dotnetRef.invokeMethodAsync("ReferenceQueryAsync", null, "@", false);
                    return;
                }
                return;
            }

            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                await window.teamComposer.send(el);
            }
        });

        // The picker does not query while a composition is active; the committed text is queried once it ends.
        el.addEventListener("compositionstart", () => {
            el._composing = true;
        });

        el.addEventListener("compositionend", async () => {
            el._composing = false;
            await window.teamComposer.refresh(el);
        });

        el.addEventListener("input", async (e) => {
            window.teamComposer.fit(el);
            if (el._draftKey) {
                window.teamComposer.draftSet(el._draftKey, el.value);
            }

            // Typing keeps the Tab (docs/Huddle.Tabs-Specifications.md): told once per Composer, not per keystroke.
            if (!el._typedNotified && el.value) {
                el._typedNotified = true;
                try {
                    await dotnetRef.invokeMethodAsync("TypedAsync");
                } catch { }
            }

            if (el._composing || (e && e.isComposing)) {
                return;
            }

            await window.teamComposer.refresh(el);
        });

        el.addEventListener("blur", () => {
            setTimeout(async () => {
                try {
                    if (el._picker.open) {
                        el._picker.open = false;
                        await dotnetRef.invokeMethodAsync("ReferenceQueryAsync", null, "@", false);
                    }
                } catch {
                    // The Composer, or its DotNetObjectReference, may already be disposed by the
                    // time this fires - Dispose can run before a queued 150 ms timer.
                }
            }, 150);
        });
    },

    // Sends what is typed and clears the box and its saved draft: the one path both Enter and the
    // Send button take, so the two can never drift apart.
    async send(el) {
        const handled = await el._dotnetRef.invokeMethodAsync("SendAsync", el.value);
        if (handled === false) {
            // The Composer held the message back (an upload is still running): keep the text.
            return;
        }
        el.value = "";
        window.teamComposer.fit(el);
        el._picker.open = false;
        el._picker.suppressFrom = null;
        if (el._draftKey) {
            window.teamComposer.draftSet(el._draftKey, "");
        }
    },

    // True while an IME composition is active: the event says so, or it is the legacy keyCode 229.
    isIme(e) {
        return !!e && (e.isComposing === true || e.keyCode === 229);
    },

    // Opens, updates or closes the picker for the token at the caret, honouring the suppression.
    async refresh(el) {
        const hit = window.teamComposer.findToken(el);
        const picker = el._picker;

        // After a pick (or Esc, or a token with no hits) the picker stays shut for that SAME token only:
        // it still starts where it did and still begins with the text that was closed. Any other token (the
        // box was emptied or replaced, or the caret moved elsewhere) ends the suppression, so a new `@`
        // always reopens. An insert fires `input` itself, so the record was made before it.
        if (picker.suppressFrom !== null) {
            const sameToken = !!hit && hit.start === picker.suppressFrom && hit.token.startsWith(picker.suppressPrefix);
            if (sameToken) {
                return;
            }
            picker.suppressFrom = null;
        }

        if (hit) {
            picker.open = true;
            const inviteCommand = /^\/invite\s/.test(el.value);
            await el._dotnetRef.invokeMethodAsync("ReferenceQueryAsync", hit.token, hit.trigger, inviteCommand);
        } else if (picker.open) {
            picker.open = false;
            await el._dotnetRef.invokeMethodAsync("ReferenceQueryAsync", null, "@", false);
        }
    },

    // The picker token ending at the caret, or null. `@` is MentionParser's rule (at the start or after a
    // character that is not a letter, digit, `_` or `@`, then up to 64 characters with no newline and no
    // `@`; spaces allowed because a Name may contain them). `#` is the old rule, unchanged. `#` is tried
    // first so "@Nova see #12" still opens the Task picker. start is the index of the trigger character.
    findToken(el) {
        return window.teamComposer.tokenAt(el.value, el.selectionStart);
    },

    // findToken over plain text: the token in `value` that ends at `caret`.
    tokenAt(value, caret) {
        const beforeCaret = value.substring(0, caret);
        const hash = beforeCaret.match(/(?:^|\s)#([A-Za-z0-9-]{0,40})$/);
        if (hash) {
            return { trigger: "#", token: hash[1], start: caret - hash[1].length - 1 };
        }
        const at = beforeCaret.match(/(?:^|[^A-Za-z0-9_@])@([^\n@]{0,64})$/);
        if (at) {
            return { trigger: "@", token: at[1], start: caret - at[1].length - 1 };
        }
        return null;
    },

    // Keeps the picker shut for the token at the caret for as long as the SAME token continues: it still starts
    // at suppressFrom and begins with suppressPrefix (see refresh). Not "until the next space", because a token
    // may contain spaces. Any other token ends the suppression.
    suppress(el, prefix) {
        const hit = window.teamComposer.findToken(el);
        if (!hit) {
            return;
        }
        el._picker.suppressFrom = hit.start;
        el._picker.suppressPrefix = prefix === null ? hit.token : prefix;
    },

    // Called by the Composer when a token with a space has no hits and it closed the picker: the script's
    // state follows, and the next keystroke of the same token does not reopen it.
    pickerClosed(el) {
        el._picker.open = false;
        window.teamComposer.suppress(el, null);
    },

    // Replaces the token ending at the caret with the pick's text plus a space and puts the caret after it
    // (spec 6). One undoable edit (D-24, OP-8): execCommand("insertText") is recorded by the browser as
    // such, so Ctrl+Z brings the token back; setRangeText is the fallback where it is refused. The
    // suppression is recorded first because the insert raises `input` itself.
    insertReference(el, text) {
        el.focus();
        const hit = window.teamComposer.findToken(el);
        const caret = el.selectionStart;
        const tokenStart = hit ? hit.start : caret;
        el._picker.open = false;
        el._picker.suppressFrom = tokenStart;
        el._picker.suppressPrefix = text.startsWith("@") ? text.substring(1) : "";
        if (!text.startsWith("@")) {
            // A Name may contain spaces, so an EARLIER `@` ("@Nova check #gro") still reaches the caret once the
            // pick is in. That token is the one kept shut: nothing is active after a pick until the Human types.
            const earlier = window.teamComposer.tokenAt(el.value, tokenStart);
            if (earlier && earlier.trigger === "@") {
                el._picker.suppressFrom = earlier.start;
                el._picker.suppressPrefix = earlier.token + text + " ";
            }
        }
        el.setSelectionRange(tokenStart, caret);
        if (!document.execCommand("insertText", false, text + " ")) {
            el.setRangeText(text + " ", tokenStart, caret, "end");
            el.dispatchEvent(new Event("input", { bubbles: true }));
        }
    },
};

window.teamScroll = {
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

// The Feed list's scroll awareness (Feed spec 6.12, D-22): the list is not the scroller itself, so both calls act on the
// nearest scrollable ancestor (the panel body or the page). Unavailable scrolling reads as "at the top".
window.huddleFeed = {
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
        const scroller = window.huddleFeed.scroller(el);
        return !!scroller && scroller.scrollTop > 4;
    },
    scrollToTop(el) {
        const scroller = window.huddleFeed.scroller(el);
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
    // Scrolls a sideways scroller just far enough to show its selected segment. Only the scroller moves, never the page, so a
    // Feed Tab opened on Gates in a narrow column shows Gates rather than All. Blazor calls this right after a render, before the
    // browser has laid the segments out, so it measures two frames later; measured at once it fell short by a segment.
    revealSelected(scroller) {
        requestAnimationFrame(() => requestAnimationFrame(() => {
            const item = scroller && scroller.isConnected && scroller.querySelector(".huddle-segmented-active-item");
            if (!item) {
                return;
            }

            const box = scroller.getBoundingClientRect();
            const itemBox = item.getBoundingClientRect();
            if (itemBox.left < box.left) {
                scroller.scrollLeft -= box.left - itemBox.left;
            } else if (itemBox.right > box.right) {
                scroller.scrollLeft += itemBox.right - box.right;
            }
        }));
    },
};

// Remembers which Right sidebar panels a viewer collapsed (Right sidebar spec D-8). A per-viewer
// convenience: storage can be missing or throw, so every access is guarded and a failure reads as
// "expanded".
window.huddleSidebar = {
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
        window.huddleSidebar.unwatchHeadings();
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
        window.huddleSidebar._headingWatch = () => {
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
        const stop = window.huddleSidebar._headingWatch;
        if (stop) {
            stop();
            window.huddleSidebar._headingWatch = null;
        }
    },
};

// Tab shortcuts (docs/Huddle.Tabs-Specifications.md). The browser keeps Ctrl+W, Ctrl+Tab and
// Ctrl+PageUp/PageDown for its own tabs and a page cannot take them, so these use Alt:
// Alt+W closes the active Tab, Alt+PageDown / Alt+PageUp move to the next / previous one.
window.huddleTabs = {
  _handler: null,
  attach(dotnetRef) {
    window.huddleTabs.detach();
    window.huddleTabs._handler = (e) => {
      if (!e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) {
        return;
      }

      let action = null;
      if (e.key === "w" || e.key === "W") {
        action = "close";
      } else if (e.key === "PageDown") {
        action = "next";
      } else if (e.key === "PageUp") {
        action = "previous";
      }

      if (action) {
        e.preventDefault();
        dotnetRef.invokeMethodAsync("ShortcutAsync", action).catch(() => { });
      }
    };
    document.addEventListener("keydown", window.huddleTabs._handler);
  },
  detach() {
    if (window.huddleTabs._handler) {
      document.removeEventListener("keydown", window.huddleTabs._handler);
      window.huddleTabs._handler = null;
    }
  }
};

// Global search keys (docs/Huddle.GlobalSearch-Specifications.md §5.2, §6.5). One keydown listener on window in
// the capture phase, so the composer and CodeMirror cannot swallow a key first. Ctrl+K and "/" open the box;
// Alt+1..6 and Ctrl+Left/Right move between the chips, and only while the dropdown is open (setOpen), so they
// never collide with the Tab shortcuts above. Edge may keep Ctrl+K for its address bar: if so, set
// shortcutNeedsShift to true and the shortcut becomes Ctrl+Shift+K (D-4).
window.huddleSearch = {
  _handler: null,
  _open: false,
  _previous: null,
  shortcutNeedsShift: false,
  attach(dotnetRef, host) {
    window.huddleSearch.detach();
    window.huddleSearch._handler = (e) => {
      if (e.ctrlKey && !e.altKey && !e.metaKey && e.shiftKey === window.huddleSearch.shortcutNeedsShift
        && e.key.toLowerCase() === "k") {
        e.preventDefault();
        openBox();
        return;
      }

      if (e.key === "/" && !e.ctrlKey && !e.altKey && !e.metaKey) {
        const target = e.target;
        const tag = target && target.tagName;
        if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT"
          || (target && target.closest && (target.closest("[contenteditable]") || target.closest(".cm-editor")))) {
          return;
        }

        e.preventDefault();
        openBox();
        return;
      }

      if (!window.huddleSearch._open) {
        return;
      }

      if (e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey) {
        const digit = e.code && e.code.startsWith("Digit") ? Number(e.code.substring(5)) : Number(e.key);
        if (digit >= 1 && digit <= 6) {
          e.preventDefault();
          dotnetRef.invokeMethodAsync("SelectChipAsync", digit).catch(() => { });
        }
      } else if (e.ctrlKey && !e.altKey && !e.metaKey && !e.shiftKey && (e.key === "ArrowLeft" || e.key === "ArrowRight")) {
        e.preventDefault();
        dotnetRef.invokeMethodAsync("MoveChipAsync", e.key === "ArrowLeft" ? -1 : 1).catch(() => { });
      }
    };
    const openBox = () => {
      const input = host && host.querySelector("input");
      if (!input) {
        return;
      }

      if (document.activeElement !== input) {
        window.huddleSearch._previous = document.activeElement;
      }

      input.focus();
      dotnetRef.invokeMethodAsync("OpenAsync").catch(() => { });
    };
    window.addEventListener("keydown", window.huddleSearch._handler, true);
  },
  detach() {
    if (window.huddleSearch._handler) {
      window.removeEventListener("keydown", window.huddleSearch._handler, true);
      window.huddleSearch._handler = null;
    }

    window.huddleSearch._open = false;
    window.huddleSearch._previous = null;
  },
  setOpen(open) {
    window.huddleSearch._open = open;
  },
  restoreFocus() {
    const previous = window.huddleSearch._previous;
    window.huddleSearch._previous = null;
    if (previous && previous.isConnected && typeof previous.focus === "function") {
      previous.focus();
    }
  }
};

window.huddleStorage = {
  get: function (key) { try { return window.localStorage.getItem(key); } catch { return null; } },
  set: function (key, value) { try { window.localStorage.setItem(key, value); } catch { } }
};

window.huddleResize = {
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
      window.huddleStorage.set(storageKey, String(width));
      return dotnet.invokeMethodAsync("OnWidthCommittedAsync", width);
    };
    const currentWidth = () => {
      const drawer = handle.closest(".mud-drawer");
      return (drawer ? drawer.offsetWidth : defaultPx + barPx) - barPx;
    };

    const stored = parseInt(window.huddleStorage.get(storageKey) ?? "", 10);
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

window.huddleClipboard = {
  // Returns true only when the text really reached the clipboard.
  copy: async function (text) {
    if (window.isSecureContext && navigator.clipboard) {
      try { await navigator.clipboard.writeText(text); return true; } catch { /* fall through */ }
    }
    const area = document.createElement("textarea");
    area.value = text; area.setAttribute("readonly", ""); area.style.position = "fixed"; area.style.opacity = "0";
    document.body.appendChild(area); area.select();
    let ok = false;
    try { ok = document.execCommand("copy"); } catch { ok = false; }
    area.remove();
    return ok;
  }
};

/* Library */
window.huddleLibrary = {
  // Registers a visibilitychange listener that calls the given .NET reference's OnVisible method
  // whenever the page becomes visible again, so an open document can refresh itself.
  onVisible: function (dotNetRef) {
    const handler = () => {
      if (document.visibilityState === "visible") {
        dotNetRef.invokeMethodAsync("OnVisible");
      }
    };
    dotNetRef._huddleLibraryVisibilityHandler = handler;
    document.addEventListener("visibilitychange", handler);
  },
  offVisible: function (dotNetRef) {
    const handler = dotNetRef._huddleLibraryVisibilityHandler;
    if (handler) {
      document.removeEventListener("visibilitychange", handler);
      delete dotNetRef._huddleLibraryVisibilityHandler;
    }
  },
  // Scrolls the heading with the given id inside root to the top of its scroll container. The id is
  // escaped so a heading id with selector characters cannot break the query. Returns whether it was found.
  scrollToHeading(root, id) {
    const el = root && root.querySelector("#" + CSS.escape(id));
    if (!el) {
      return false;
    }
    el.scrollIntoView({ block: "start" });
    return true;
  },
};

/* Avatar Library picker */
window.huddleAvatarLibrary = {
  // MudBlazor closes a dialog on Escape through a DOM key interceptor on the dialog element, which a Blazor
  // @onkeydown:stopPropagation cannot stop. This listener sits on the search box wrapper, below the dialog:
  // a first Escape with text in the box clears the box (an input event, so the bound value follows) and stops
  // the event before it reaches the dialog; with the box empty it is left alone and closes the dialog.
  guardSearchEscape: function (wrap) {
    wrap.addEventListener("keydown", (e) => {
      const input = e.target;
      if (e.key !== "Escape" || !(input instanceof HTMLInputElement) || input.value === "") {
        return;
      }
      e.stopPropagation();
      input.value = "";
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
  },
};
