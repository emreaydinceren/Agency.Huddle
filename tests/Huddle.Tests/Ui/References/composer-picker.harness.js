// Behaviour harness for window.teamComposer's Reference picker (app.js). Run by ComposerScriptTests with
// `node composer-picker.harness.js <path to app.js>`; prints one JSON object of scenario results.
// app.js is loaded into a vm context with just enough of a DOM: a textarea stand-in, a recording
// DotNetObjectReference, and a document.execCommand that either succeeds or is refused.
"use strict";
const fs = require("fs");
const vm = require("vm");

const source = fs.readFileSync(process.argv[2], "utf8");

function load(execCommandWorks) {
    const context = { setTimeout: () => 0, console };
    context.window = context;
    context.Event = class Event {
        constructor(type) { this.type = type; this.defaultPrevented = false; }
        preventDefault() { this.defaultPrevented = true; }
    };
    context.document = {
        execCommand: (command, _ui, text) => {
            if (!execCommandWorks || command !== "insertText") {
                return false;
            }
            const el = context.currentEl;
            el.setRangeText(text, el.selectionStart, el.selectionEnd, "end");
            el.dispatchEvent(new context.Event("input"));
            return true;
        },
    };
    vm.createContext(context);
    vm.runInContext(source, context);
    return context;
}

function makeEl(context, pickText) {
    const handlers = {};
    const pending = [];
    const calls = [];
    const dotnet = {
        invokeMethodAsync: async (name, ...args) => {
            calls.push({ name, args });
            return name === "PickAsync" ? pickText.value : undefined;
        },
    };
    const el = {
        value: "",
        selectionStart: 0,
        selectionEnd: 0,
        closest: () => null,
        focus() { },
        addEventListener(type, handler) { (handlers[type] = handlers[type] || []).push(handler); },
        setSelectionRange(start, end) { this.selectionStart = start; this.selectionEnd = end; },
        setRangeText(text, start, end, mode) {
            this.value = this.value.substring(0, start) + text + this.value.substring(end);
            if (mode === "end") {
                this.selectionStart = this.selectionEnd = start + text.length;
            }
        },
        dispatchEvent(event) {
            for (const handler of handlers[event.type] || []) {
                pending.push(handler(event));
            }
        },
        async fire(type, props) {
            const event = Object.assign(new context.Event(type), props || {});
            for (const handler of handlers[type] || []) {
                await handler(event);
            }
            await Promise.all(pending.splice(0));
            return event;
        },
        async type(text) {
            this.value = this.value.substring(0, this.selectionStart) + text + this.value.substring(this.selectionEnd);
            this.selectionStart = this.selectionEnd = this.selectionStart + text.length;
            await this.fire("input", { isComposing: false });
        },
        setBox(value) {
            this.value = value;
            this.selectionStart = this.selectionEnd = value.length;
        },
        calls,
        names() { return calls.map((c) => c.name); },
        queries() { return calls.filter((c) => c.name === "ReferenceQueryAsync" && c.args[0] !== null).map((c) => c.args[0]); },
    };
    context.currentEl = el;
    context.teamComposer.attach(el, dotnet, null, null);
    return el;
}

function token(context, value, caret) {
    return context.teamComposer.findToken({ value, selectionStart: caret === undefined ? value.length : caret });
}

async function run() {
    const out = {};

    // Token detection.
    {
        const c = load(false);
        out.tokenLibSpace = token(c, "@lib: ");
        out.tokenLibPhrase = token(c, "@lib: my plan");
        out.tokenNameWithSpace = token(c, "ask @Nova Pri");
        out.tokenHash = token(c, "@Nova see #12");
        out.tokenMidWord = token(c, "mail@host");
        out.tokenAtSpanBreak = token(c, "@a @b");
    }

    // IME: picker keys and queries are ignored while composing.
    {
        const c = load(false);
        const el = makeEl(c, { value: null });
        await el.type("@no");
        const before = el.calls.length;
        const down = await el.fire("keydown", { key: "ArrowDown", isComposing: true });
        const up = await el.fire("keydown", { key: "ArrowUp", keyCode: 229 });
        const esc = await el.fire("keydown", { key: "Escape", isComposing: true });
        out.imeCallsDuringComposingKeys = el.calls.length - before;
        out.imeDefaultPrevented = down.defaultPrevented || up.defaultPrevented || esc.defaultPrevented;
        out.imePickerStillOpen = el._picker.open;
        const plain = await el.fire("keydown", { key: "ArrowDown" });
        out.plainArrowMoves = el.names().includes("MoveAsync") && plain.defaultPrevented;
    }
    {
        const c = load(false);
        const el = makeEl(c, { value: null });
        await el.fire("compositionstart");
        el.setBox("@たな");
        await el.fire("input", { isComposing: true });
        out.imeQueriesWhileComposing = el.queries().length;
        await el.fire("compositionend");
        out.imeQueriesAfterEnd = el.queries();
    }

    // After a pick no earlier token becomes active, in both insert paths.
    for (const works of [false, true]) {
        const c = load(works);
        const pick = { value: "GROW-0001" };
        const el = makeEl(c, pick);
        await el.type("@Nova check #gro");
        const openBefore = el._picker.open;
        await el.fire("keydown", { key: "Tab" });
        const queriesAfterPick = el.queries().length;
        out["postPick" + works] = {
            openBefore,
            value: el.value,
            pickerOpenAfterPick: el._picker.open,
            queries: el.queries(),
            queriesAfterPick,
        };
        const enter = await el.fire("keydown", { key: "Enter" });
        out["postPickEnterSends" + works] = el.names().includes("SendAsync") && enter.defaultPrevented;
        el.calls.length = 0;
        await el.type("x");
        out["postPickTypingStaysShut" + works] = el.queries().length === 0 && !el._picker.open;
    }

    // Suppression ends when a different token starts.
    {
        const c = load(false);
        const pick = { value: "@Nova" };
        const el = makeEl(c, pick);
        await el.type("@No");
        await el.fire("keydown", { key: "Tab" });
        el.calls.length = 0;
        await el.type("hi @ta");
        out.newTokenAfterPickQueries = el.queries();
    }
    {
        const c = load(false);
        const el = makeEl(c, { value: null });
        await el.type("@No");
        await el.fire("keydown", { key: "Escape" });
        el.calls.length = 0;
        await el.type("v");
        const sameTokenQueries = el.queries().length;
        await el.type(" and @ta");
        out.escape = { sameTokenQueries, newTokenQueries: el.queries() };
    }

    process.stdout.write(JSON.stringify(out));
}

run().catch((error) => {
    process.stderr.write(String(error && error.stack ? error.stack : error));
    process.exit(1);
});
