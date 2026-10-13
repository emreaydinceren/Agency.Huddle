// Drives window.teamComposer.attach / send from src/Huddle.App/wwwroot/app.js against a hand-made
// DOM, one scenario per argv[2], and prints a JSON object of what happened. ComposerAttachJsTests
// runs this with `node` and asserts on the output.
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";

const root = process.argv[3];
const source = fs.readFileSync(path.join(root, "src", "Huddle.App", "wwwroot", "app.js"), "utf8");

class FakeNode {
    constructor() {
        this.handlers = {};
        this.classes = new Set();
        this.classList = {
            add: (c) => this.classes.add(c),
            remove: (c) => this.classes.delete(c),
            contains: (c) => this.classes.has(c),
        };
    }
    addEventListener(type, fn) { (this.handlers[type] ??= []).push(fn); }
    async fire(type, event) {
        for (const fn of this.handlers[type] ?? []) { await fn(event); }
        return event;
    }
    contains(other) { return other === this || (this.children ?? []).includes(other); }
}

function ev(extra = {}) {
    const e = { defaultPrevented: false, preventDefault() { e.defaultPrevented = true; }, ...extra };
    return e;
}

const box = new FakeNode();
const textarea = new FakeNode();
textarea.value = "hello";
textarea.closest = (selector) => (selector === ".composer-box" ? box : null);
textarea.focus = () => { };
box.children = [textarea];

const input = new FakeNode();
input.files = null;
input.changes = 0;
input.dispatchEvent = (e) => { if (e.type === "change") { input.changes++; } return true; };
const host = { querySelector: (s) => (s === "input[type=file]" ? input : null) };
const emptyHost = { querySelector: () => null };

class FakeDataTransfer {
    constructor() {
        this.files = [];
        this.items = { add: (f) => this.files.push(f) };
    }
}

const context = {
    DataTransfer: FakeDataTransfer,
    Event: class { constructor(type) { this.type = type; } },
    document: {},
};
context.window = context;
vm.createContext(context);
vm.runInContext(source, context);

const file = { name: "image.png" };
const calls = [];
const ref = { invokeMethodAsync: async (name, ...args) => { calls.push(name); return name === "SendAsync" ? ref.sendResult : undefined; } };
const out = {};
const scenario = process.argv[2];

function attach(h) { context.window.teamComposer.attach(textarea, ref, null, h); }

if (scenario === "paste-files") {
    attach(host);
    const e = await textarea.fire("paste", ev({ clipboardData: { files: [file] } }));
    out.prevented = e.defaultPrevented;
    out.assigned = input.files?.length;
    out.first = input.files?.[0]?.name;
    out.changes = input.changes;
} else if (scenario === "paste-text") {
    attach(host);
    const e = await textarea.fire("paste", ev({ clipboardData: { files: [] } }));
    out.prevented = e.defaultPrevented;
    out.assigned = input.files === null ? null : input.files.length;
    out.changes = input.changes;
} else if (scenario === "drag-drop") {
    attach(host);
    const over = await box.fire("dragover", ev({ dataTransfer: { types: ["Files"], files: [] } }));
    out.overPrevented = over.defaultPrevented;
    out.droppingDuring = box.classes.has("composer-dropping");
    await box.fire("dragleave", ev({ relatedTarget: null }));
    out.droppingAfterLeave = box.classes.has("composer-dropping");
    await box.fire("dragover", ev({ dataTransfer: { types: ["Files"], files: [] } }));
    const drop = await box.fire("drop", ev({ dataTransfer: { types: ["Files"], files: [file] } }));
    out.dropPrevented = drop.defaultPrevented;
    out.droppingAfterDrop = box.classes.has("composer-dropping");
    out.assigned = input.files?.length;
    out.changes = input.changes;
} else if (scenario === "drag-text") {
    attach(host);
    const over = await box.fire("dragover", ev({ dataTransfer: { types: ["text/plain"], files: [] } }));
    out.overPrevented = over.defaultPrevented;
    out.dropping = box.classes.has("composer-dropping");
    const drop = await box.fire("drop", ev({ dataTransfer: { types: ["text/plain"], files: [] } }));
    out.dropPrevented = drop.defaultPrevented;
    out.changes = input.changes;
} else if (scenario === "drag-inner-leave") {
    attach(host);
    await box.fire("dragover", ev({ dataTransfer: { types: ["Files"], files: [] } }));
    await box.fire("dragleave", ev({ relatedTarget: textarea }));
    out.dropping = box.classes.has("composer-dropping");
} else if (scenario === "no-host" || scenario === "no-input") {
    attach(scenario === "no-host" ? null : emptyHost);
    const files = { dataTransfer: { types: ["Files"], files: [file] } };
    const text = { dataTransfer: { types: ["text/plain"], files: [] } };
    out.overPrevented = (await box.fire("dragover", ev(files))).defaultPrevented;
    out.dropPrevented = (await box.fire("drop", ev(files))).defaultPrevented;
    out.dropping = box.classes.has("composer-dropping");
    out.textOverPrevented = (await box.fire("dragover", ev(text))).defaultPrevented;
    out.textDropPrevented = (await box.fire("drop", ev(text))).defaultPrevented;
    out.pastePrevented = (await textarea.fire("paste", ev({ clipboardData: { files: [file] } }))).defaultPrevented;
    out.keydown = (textarea.handlers.keydown ?? []).length;
} else if (scenario === "paste-input-replaced" || scenario === "drop-input-replaced") {
    let current = input;
    attach({ querySelector: (s) => (s === "input[type=file]" ? current : null) });
    const replacement = new FakeNode();
    replacement.files = null;
    replacement.changes = 0;
    replacement.dispatchEvent = (e) => { if (e.type === "change") { replacement.changes++; } return true; };
    current = replacement;
    if (scenario === "paste-input-replaced") {
        await textarea.fire("paste", ev({ clipboardData: { files: [file] } }));
    } else {
        await box.fire("drop", ev({ dataTransfer: { types: ["Files"], files: [file] } }));
    }
    out.oldChanges = input.changes;
    out.newChanges = replacement.changes;
    out.newAssigned = replacement.files?.length;
} else if (scenario === "send-held" || scenario === "send-posted") {
    attach(null);
    ref.sendResult = scenario === "send-posted";
    await context.window.teamComposer.send(textarea);
    out.value = textarea.value;
} else if (scenario === "send-legacy") {
    attach(null);
    ref.sendResult = undefined;
    await context.window.teamComposer.send(textarea);
    out.value = textarea.value;
}

console.log(JSON.stringify(out));
