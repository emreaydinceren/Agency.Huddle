// Text census: rendered size, weight, colour and WCAG contrast of every visible text node.
// Paste into the DevTools console, or pass to a browser tool's evaluate. The last expression is the report.
// See README.md in this folder for what it measures and its limits.
(function () {
  'use strict';

  const MIN_PX = 11;
  const NORMAL_RATIO = 4.5;
  const LARGE_RATIO = 3;
  const LARGE_PX = 24;
  const LARGE_BOLD_PX = 18.66;
  const EXAMPLES = 3;

  /** Parses "rgb(...)" or "rgba(...)" (comma or space syntax) into {r,g,b,a}, or null. */
  function parseColor(text) {
    const m = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)(?:\s*[,/]\s*([\d.]+)(%?))?\s*\)$/.exec((text || '').trim());
    if (!m) {
      return null;
    }
    let a = m[4] === undefined ? 1 : parseFloat(m[4]);
    if (m[5] === '%') {
      a /= 100;
    }
    return { r: parseFloat(m[1]), g: parseFloat(m[2]), b: parseFloat(m[3]), a: a };
  }

  /** WCAG 2.x relative luminance. */
  function luminance(c) {
    const f = function (v) {
      const s = v / 255;
      return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
  }

  /** WCAG 2.x contrast ratio between two opaque colours. */
  function contrast(a, b) {
    const la = luminance(a);
    const lb = luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
  }

  /** Product of the computed CSS zoom of the element and all its ancestors. */
  function effectiveZoom(el) {
    let z = 1;
    for (let e = el; e && e.nodeType === 1; e = e.parentElement) {
      const v = parseFloat(getComputedStyle(e).zoom);
      if (v > 0 && isFinite(v)) {
        z *= v;
      }
    }
    return z;
  }

  /** Nearest ancestor background that is fully opaque; translucent fills are skipped. Falls back to white. */
  function opaqueBackground(el) {
    for (let e = el; e && e.nodeType === 1; e = e.parentElement) {
      const c = parseColor(getComputedStyle(e).backgroundColor);
      if (c && c.a >= 1) {
        return { color: c, found: true };
      }
    }
    return { color: { r: 255, g: 255, b: 255, a: 1 }, found: false };
  }

  /** Short selector: tag, id or first two classes, with up to two parents. */
  function selectorOf(el) {
    const parts = [];
    for (let e = el, i = 0; e && e.nodeType === 1 && i < 3; e = e.parentElement, i++) {
      const p = e.tagName.toLowerCase();
      if (e.id) {
        parts.unshift(p + '#' + e.id);
        break;
      }
      const cls = (typeof e.className === 'string' ? e.className : '').trim().split(/\s+/).filter(Boolean).slice(0, 2);
      parts.unshift(p + (cls.length ? '.' + cls.join('.') : ''));
    }
    return parts.join(' > ');
  }

  function isVisible(el, rect) {
    if (rect.width === 0 || rect.height === 0) {
      return false;
    }
    for (let e = el; e && e.nodeType === 1; e = e.parentElement) {
      const s = getComputedStyle(e);
      if (s.display === 'none' || s.visibility === 'hidden' || s.visibility === 'collapse' || parseFloat(s.opacity) === 0) {
        return false;
      }
    }
    return true;
  }

  function round(n, d) {
    const k = Math.pow(10, d);
    return Math.round(n * k) / k;
  }

  /** Walks every visible text node under root and builds the report. */
  function run(root) {
    root = root || document.body;
    const sizes = new Map();
    const weights = {};
    const failures = [];
    const belowMinimum = [];
    let nodes = 0;
    let noOpaqueBackground = 0;

    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const text = n.nodeValue.replace(/\s+/g, ' ').trim();
      const el = n.parentElement;
      if (!text || !el || /^(SCRIPT|STYLE|NOSCRIPT|TEMPLATE)$/.test(el.tagName)) {
        continue;
      }
      const range = document.createRange();
      range.selectNodeContents(n);
      if (!isVisible(el, range.getBoundingClientRect())) {
        continue;
      }
      nodes++;

      const cs = getComputedStyle(el);
      const zoom = effectiveZoom(el);
      const px = round(parseFloat(cs.fontSize) * zoom, 2);
      const weight = String(cs.fontWeight);
      const bold = parseInt(weight, 10) >= 700;
      const sample = text.length > 40 ? text.slice(0, 40) + '...' : text;
      const where = selectorOf(el);

      const entry = sizes.get(px) || { px: px, count: 0, examples: [] };
      entry.count++;
      if (entry.examples.length < EXAMPLES) {
        entry.examples.push(sample + '  [' + where + ']');
      }
      sizes.set(px, entry);
      weights[weight] = (weights[weight] || 0) + 1;

      if (px < MIN_PX) {
        belowMinimum.push({ px: px, text: sample, selector: where });
      }

      let fg = parseColor(cs.color);
      const bg = opaqueBackground(el);
      if (!bg.found) {
        noOpaqueBackground++;
      }
      if (fg) {
        if (fg.a < 1) {
          // Translucent text colour: blend over the background it sits on.
          fg = {
            r: fg.r * fg.a + bg.color.r * (1 - fg.a),
            g: fg.g * fg.a + bg.color.g * (1 - fg.a),
            b: fg.b * fg.a + bg.color.b * (1 - fg.a),
            a: 1
          };
        }
        const ratio = contrast(fg, bg.color);
        const large = px >= LARGE_PX || (bold && px >= LARGE_BOLD_PX);
        const required = large ? LARGE_RATIO : NORMAL_RATIO;
        if (ratio < required) {
          failures.push({
            ratio: round(ratio, 2),
            required: required,
            large: large,
            px: px,
            weight: weight,
            fontStyle: cs.fontStyle,
            color: cs.color,
            background: 'rgb(' + bg.color.r + ', ' + bg.color.g + ', ' + bg.color.b + ')' + (bg.found ? '' : ' (assumed)'),
            text: sample,
            selector: where
          });
        }
      }
    }

    failures.sort(function (a, b) { return a.ratio - b.ratio; });
    belowMinimum.sort(function (a, b) { return a.px - b.px; });
    return {
      textNodes: nodes,
      noOpaqueBackground: noOpaqueBackground,
      zoom: effectiveZoom(document.documentElement),
      sizes: Array.from(sizes.values()).sort(function (a, b) { return a.px - b.px; }),
      failures: failures,
      belowMinimum: belowMinimum,
      weights: weights
    };
  }

  const api = { parseColor: parseColor, luminance: luminance, contrast: contrast, run: run };
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = api;
    return api;
  }
  return run();
})();
