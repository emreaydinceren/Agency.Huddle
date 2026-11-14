// ES module, formerly window.teamComposer in app.js. C# calls it through JsModule.Composer; only the exports below are
// reachable from outside, and module state (handlers, flags) stays private to it.

const composer = {
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
        el.value = (key && composer.draftGet(key)) || "";
        el._picker.open = false;
        el._picker.suppressFrom = null;
        el._typedNotified = false;
        composer.fit(el);
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
                composer.fit(el);
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
            composer.handOver(input, files);
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
                composer.handOver(input, e.dataTransfer.files);
            }
        });
    },

    attach(el, dotnetRef, draftKey, fileInputHost) {
        composer.attachFiles(el, fileInputHost);
        el._dotnetRef = dotnetRef;
        el._picker = { open: false, suppressFrom: null, suppressPrefix: "" };
        el._draftKey = draftKey || null;
        el._typedNotified = false;
        if (el._draftKey) {
            const saved = composer.draftGet(el._draftKey);
            if (saved) {
                el.value = saved;
            }
        }
        composer.fit(el);
        composer.observeResize(el);

        el.addEventListener("keydown", async (e) => {
            const picker = el._picker;

            // While an IME composes (isComposing, or the legacy keyCode 229) every key belongs to the IME:
            // arrows choose its candidates, Esc cancels the composition, Enter commits it.
            if (composer.isIme(e)) {
                return;
            }

            if (picker.open) {
                // Enter picks and never sends, even with no rows (spec 5.4, OP-7); Tab picks (OP-10).
                if ((e.key === "Enter" && !e.shiftKey) || (e.key === "Tab" && !e.shiftKey)) {
                    e.preventDefault();
                    const text = await dotnetRef.invokeMethodAsync("PickAsync");
                    picker.open = false;
                    if (text) {
                        composer.insertReference(el, text);
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
                    composer.suppress(el, null);
                    await dotnetRef.invokeMethodAsync("ReferenceQueryAsync", null, "@", false);
                    return;
                }
                return;
            }

            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                await composer.send(el);
            }
        });

        // The picker does not query while a composition is active; the committed text is queried once it ends.
        el.addEventListener("compositionstart", () => {
            el._composing = true;
        });

        el.addEventListener("compositionend", async () => {
            el._composing = false;
            await composer.refresh(el);
        });

        el.addEventListener("input", async (e) => {
            composer.fit(el);
            if (el._draftKey) {
                composer.draftSet(el._draftKey, el.value);
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

            await composer.refresh(el);
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
        composer.fit(el);
        el._picker.open = false;
        el._picker.suppressFrom = null;
        if (el._draftKey) {
            composer.draftSet(el._draftKey, "");
        }
    },

    // True while an IME composition is active: the event says so, or it is the legacy keyCode 229.
    isIme(e) {
        return !!e && (e.isComposing === true || e.keyCode === 229);
    },

    // Opens, updates or closes the picker for the token at the caret, honouring the suppression.
    async refresh(el) {
        const hit = composer.findToken(el);
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
        return composer.tokenAt(el.value, el.selectionStart);
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
        const hit = composer.findToken(el);
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
        composer.suppress(el, null);
    },

    // Replaces the token ending at the caret with the pick's text plus a space and puts the caret after it
    // (spec 6). One undoable edit (D-24, OP-8): execCommand("insertText") is recorded by the browser as
    // such, so Ctrl+Z brings the token back; setRangeText is the fallback where it is refused. The
    // suppression is recorded first because the insert raises `input` itself.
    insertReference(el, text) {
        el.focus();
        const hit = composer.findToken(el);
        const caret = el.selectionStart;
        const tokenStart = hit ? hit.start : caret;
        el._picker.open = false;
        el._picker.suppressFrom = tokenStart;
        el._picker.suppressPrefix = text.startsWith("@") ? text.substring(1) : "";
        if (!text.startsWith("@")) {
            // A Name may contain spaces, so an EARLIER `@` ("@Nova check #gro") still reaches the caret once the
            // pick is in. That token is the one kept shut: nothing is active after a pick until the Human types.
            const earlier = composer.tokenAt(el.value, tokenStart);
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

export const { attach, detach, insertReference, pickerClosed, send, setDraftKey } = composer;
