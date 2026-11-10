// The Theme lab's page script (development only; see ThemeLab.razor). It previews edits on the live page for the
// current browser tab only, by replacing the text of the layout's <style id="hu-skin"> element and setting MudBlazor
// palette custom properties inline on <html>. Nothing is saved: reset() puts both back.

const styleId = "hu-skin";
const highlightId = "theme-lab-highlight";

let original = null;
let setNames = new Set();
let hoverHandler = null;

/** Replaces the skin style block's CSS and sets the MudBlazor palette variables (name to colour text). */
export function apply(css, paletteVariables) {
    const style = document.getElementById(styleId);
    if (style) {
        if (original === null) {
            original = style.textContent;
        }
        style.textContent = css;
    }

    const root = document.documentElement.style;
    for (const name of Object.keys(paletteVariables ?? {})) {
        root.setProperty(name, paletteVariables[name]);
        setNames.add(name);
    }
}

/** Puts the skin style block and the palette variables back as the page rendered them, and stops inspecting. */
export function reset() {
    inspect(false, null);
    const style = document.getElementById(styleId);
    if (style && original !== null) {
        style.textContent = original;
    }

    original = null;
    const root = document.documentElement.style;
    for (const name of setNames) {
        root.removeProperty(name);
    }

    setNames = new Set();
}

/** Copies text to the clipboard. */
export async function copy(text) {
    await navigator.clipboard.writeText(text);
}

/**
 * Which --hu-* and --ink-* tokens an element's computed style reads: every stylesheet rule whose selector matches the element and
 * whose declarations use var(--hu-*) or var(--ink-*). Returns [{ property, tokens, selector }].
 */
export function tokensReadBy(element, sheets) {
    const found = [];
    const visit = (rules) => {
        for (const rule of rules) {
            if (!rule.selectorText) {
                if (rule.cssRules) {
                    visit(rule.cssRules);
                }

                continue;
            }

            let matches = false;
            try {
                matches = element.matches(rule.selectorText);
            } catch {
                matches = false;
            }

            if (!matches || !rule.style) {
                continue;
            }

            for (const declaration of rule.style.cssText.matchAll(/([a-z-]+)\s*:\s*([^;]*var\(\s*--(?:hu|ink)-[^;]*)/g)) {
                if (declaration[1].startsWith("--")) {
                    continue;
                }

                const tokens = [...new Set([...declaration[2].matchAll(/--(?:hu|ink)-[a-z0-9-]+/g)].map((token) => token[0]))];
                found.push({ property: declaration[1], tokens, selector: rule.selectorText });
            }
        }
    };

    for (const sheet of sheets) {
        let rules = null;
        try {
            rules = sheet.cssRules;
        } catch {
            rules = null;
        }

        if (rules) {
            visit(rules);
        }
    }

    return found;
}

/** The text the inspector shows for an element: its name, then one line per property that reads a token. */
export function describe(element, sheets) {
    const name = element.tagName.toLowerCase() + (element.id ? "#" + element.id : "");
    const lines = new Set();
    for (const entry of tokensReadBy(element, sheets)) {
        lines.add(entry.property + " <- " + entry.tokens.join(", "));
    }

    return lines.size === 0 ? name + ": reads no skin token" : name + "\n" + [...lines].join("\n");
}

/** Turns hover inspection on or off. While on, hovering an element outlines it and writes describe() into #outputId. */
export function inspect(enabled, outputId) {
    if (hoverHandler) {
        document.removeEventListener("mouseover", hoverHandler, true);
        hoverHandler = null;
        document.getElementById(highlightId)?.remove();
    }

    if (!enabled) {
        return;
    }

    const highlight = document.createElement("div");
    highlight.id = highlightId;
    highlight.style.cssText = "position:fixed;pointer-events:none;z-index:99999;outline:2px solid #e11d48;outline-offset:-2px;";
    document.body.appendChild(highlight);

    hoverHandler = (event) => {
        const target = event.target;
        if (!(target instanceof Element) || target === highlight) {
            return;
        }

        const box = target.getBoundingClientRect();
        highlight.style.left = box.left + "px";
        highlight.style.top = box.top + "px";
        highlight.style.width = box.width + "px";
        highlight.style.height = box.height + "px";
        const output = outputId ? document.getElementById(outputId) : null;
        if (output) {
            output.textContent = describe(target, document.styleSheets);
        }
    };

    document.addEventListener("mouseover", hoverHandler, true);
}
