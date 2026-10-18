// Drives src/Huddle.App/wwwroot/theme-lab.js against a hand-made DOM, one scenario per argv[2], and prints a JSON
// object of what happened. ThemeLabJsTests runs this with `node` and asserts on the output.
import { readFile } from "node:fs/promises";
import path from "node:path";

// theme-lab.js is an ES module in a .js file with no package.json beside it. Node 22 detects that from the syntax,
// but Node 18 (the CI image) loads a .js file as CommonJS and fails on `export`, so import its text as a module.
const root = process.argv[3];
const source = await readFile(path.join(root, "src", "Huddle.App", "wwwroot", "theme-lab.js"), "utf8");
const lab = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

const out = {};
const scenario = process.argv[2];

if (scenario === "tokens-read-by") {
    const element = { matches: (selector) => selector === ".card" || selector === "button" };
    const sheets = [
        {
            cssRules: [
                { selectorText: ":root", style: { cssText: "--hu-ground-page: #fff;" } },
                { selectorText: ".card", style: { cssText: "background: var(--hu-ground-raised); border: 1px solid var(--hu-ink-edge); color: red;" } },
                { selectorText: ".other", style: { cssText: "background: var(--hu-ground-sunk);" } },
                { cssRules: [{ selectorText: "button", style: { cssText: "box-shadow: var(--hu-shadow-card), var(--hu-shadow-card);" } }] },
                { selectorText: "::before", style: { cssText: "color: var(--hu-focus);" } },
            ],
        },
        { get cssRules() { throw new Error("cross-origin"); } },
    ];
    out.found = lab.tokensReadBy(element, sheets);
    out.described = lab.describe({ tagName: "DIV", id: "x", matches: element.matches }, sheets);
    out.none = lab.describe({ tagName: "P", id: "", matches: () => false }, sheets);
} else if (scenario === "apply-reset") {
    const style = { textContent: "ORIGINAL" };
    const props = {};
    globalThis.document = {
        getElementById: (id) => (id === "hu-skin" ? style : null),
        documentElement: { style: { setProperty: (n, v) => { props[n] = v; }, removeProperty: (n) => { delete props[n]; } } },
        removeEventListener: () => { },
    };
    lab.apply(":root{--hu-a:1}", { "--mud-palette-primary": "#123456" });
    lab.apply(":root{--hu-a:2}", { "--mud-palette-primary": "#654321" });
    out.during = { text: style.textContent, props: { ...props } };
    lab.reset();
    out.after = { text: style.textContent, props: { ...props } };
}

process.stdout.write(JSON.stringify(out));
