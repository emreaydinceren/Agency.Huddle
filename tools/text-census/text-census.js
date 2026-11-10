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

  // ---- Lines report settings -------------------------------------------------------------
  // Ink ladder: a line sits on rung n when colour = Ink n% over Ground (sRGB mix).
  const INK_VAR = '--mud-palette-text-primary';
  const GROUND_VAR = '--mud-palette-surface';
  // Largest distance (0-255 sRGB units) from the Ink-Ground line that still counts as "on the ladder".
  const LADDER_TOLERANCE = 5;
  // Medians closer than this many percentage points are treated as equal when checking order.
  const ORDER_EPSILON = 0.5;

  // ---- Classifier table: edit here to add a class name -----------------------------------
  // Each row says which audit tier a border belongs to. The first row whose `test` matches the
  // element wins, so list the more specific rows first. `test` receives the element and returns a
  // boolean. Only border sides are classified; fills are reported by rung but not ordered.
  //   Line  = dividers, card and tile borders         (ladder: 18%)
  //   Field = input outlines                          (ladder: 30%)
  //   Chrome = toolbars, tab strip, hairlines         (ladder: 30%)
  const TIER_RANK = { Line: 0, Field: 1, Chrome: 1 };
  // The required order. '<' means the first tier's median rung must be lower; '<=' allows equal.
  const ORDER_RULES = [
    ['Line', 'Field', '<'],
    ['Field', 'Chrome', '<='],
    ['Line', 'Chrome', '<']
  ];
  function hasClass(el, pattern) {
    const cls = typeof el.className === 'string' ? el.className : '';
    return pattern.test(cls);
  }
  const CLASSIFIER = [
    { tier: 'Field', label: '.mud-input-outlined-border', test: function (el) { return hasClass(el, /(^|\s)mud-input-outlined-border(\s|$)/); } },
    { tier: 'Field', label: '.filter-chip (FND)', test: function (el) { return hasClass(el, /(^|\s)filter-chip(\s|$)/); } },
    { tier: 'Chrome', label: '.toolbar-hairline', test: function (el) { return hasClass(el, /(^|\s)toolbar-hairline(\s|$)/); } },
    { tier: 'Chrome', label: '.mud-toolbar', test: function (el) { return hasClass(el, /(^|\s)mud-toolbar(\s|$)/); } },
    { tier: 'Chrome', label: 'tab strip (.mud-tabs-toolbar, .mud-tabs-tabbar, .tab-rail)', test: function (el) { return hasClass(el, /(^|\s)(mud-tabs-toolbar|mud-tabs-tabbar|tab-rail)(\s|$)/); } },
    { tier: 'Line', label: 'dividers (.mud-divider, hr)', test: function (el) { return el.tagName === 'HR' || hasClass(el, /(^|\s)mud-divider(\s|$)/); } },
    { tier: 'Line', label: '.tag-chip, .floating-panel (FND)', test: function (el) { return hasClass(el, /(^|\s)(tag-chip|floating-panel)(\s|$)/); } },
    { tier: 'Line', label: 'cards and tiles (.mud-card, .mud-paper-outlined, *-card, *-tile)', test: function (el) { return hasClass(el, /(^|\s)(mud-card|mud-paper-outlined|[\w-]+-card|[\w-]+-tile)(\s|$)/); } }
  ];

  /**
   * Parses "rgb(...)" or "rgba(...)" (comma or space syntax) into {r,g,b,a}, or null. Also reads
   * "color(srgb r g b / a)", which is how a browser reports a resolved color-mix().
   */
  function parseColor(text) {
    const srgb = /^color\(srgb\s+([\d.e-]+)\s+([\d.e-]+)\s+([\d.e-]+)(?:\s*\/\s*([\d.]+)(%?))?\s*\)$/.exec((text || '').trim());
    if (srgb) {
      let alpha = srgb[4] === undefined ? 1 : parseFloat(srgb[4]);
      if (srgb[5] === '%') {
        alpha /= 100;
      }
      return { r: parseFloat(srgb[1]) * 255, g: parseFloat(srgb[2]) * 255, b: parseFloat(srgb[3]) * 255, a: alpha };
    }
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

  /** Linear scale (x axis) of one computed `transform` matrix string, or 1. Rotation does not change it. */
  function matrixScale(transform) {
    const m = /^matrix(3d)?\(([^)]*)\)$/.exec((transform || '').trim());
    if (!m) {
      return 1;
    }
    const v = m[2].split(',').map(parseFloat);
    const a = v[0];
    const b = v[1];
    const s = Math.sqrt(a * a + b * b);
    return s > 0 && isFinite(s) ? s : 1;
  }

  /**
   * Product of the transform scale of the element and all its ancestors: the computed `transform`
   * matrix and the individual `scale` property. A floated MudBlazor label is scaled by 0.857.
   */
  function effectiveScale(el) {
    let k = 1;
    for (let e = el; e && e.nodeType === 1; e = e.parentElement) {
      const cs = getComputedStyle(e);
      k *= matrixScale(cs.transform);
      const own = parseFloat(cs.scale);
      if (own > 0 && isFinite(own)) {
        k *= own;
      }
    }
    return k;
  }

  /** Parses #rgb, #rrggbb, #rrggbbaa as well as the forms parseColor reads. */
  function parseAnyColor(text) {
    const t = (text || '').trim();
    const h = /^#([0-9a-f]{3,8})$/i.exec(t);
    if (h) {
      let x = h[1];
      if (x.length === 3 || x.length === 4) {
        x = x.split('').map(function (c) { return c + c; }).join('');
      }
      if (x.length === 6 || x.length === 8) {
        return {
          r: parseInt(x.slice(0, 2), 16),
          g: parseInt(x.slice(2, 4), 16),
          b: parseInt(x.slice(4, 6), 16),
          a: x.length === 8 ? parseInt(x.slice(6, 8), 16) / 255 : 1
        };
      }
      return null;
    }
    return parseColor(t);
  }

  /** Composites a possibly translucent colour over an opaque ground. */
  function over(c, ground) {
    return {
      r: c.r * c.a + ground.r * (1 - c.a),
      g: c.g * c.a + ground.g * (1 - c.a),
      b: c.b * c.a + ground.b * (1 - c.a),
      a: 1
    };
  }

  /**
   * Solves colour = Ink n over Ground (per channel, least squares) for opaque colours.
   * Returns { n, off } where n is the fraction of Ink (1 = 100%) and off is the distance, in 0-255
   * sRGB units, of the colour from the Ink-Ground line; or null if Ink and Ground are identical.
   */
  function solveRung(colour, ink, ground) {
    const dr = ink.r - ground.r;
    const dg = ink.g - ground.g;
    const db = ink.b - ground.b;
    const denom = dr * dr + dg * dg + db * db;
    if (denom === 0) {
      return null;
    }
    const n = ((colour.r - ground.r) * dr + (colour.g - ground.g) * dg + (colour.b - ground.b) * db) / denom;
    const er = colour.r - (ground.r + n * dr);
    const eg = colour.g - (ground.g + n * dg);
    const eb = colour.b - (ground.b + n * db);
    return { n: n, off: Math.sqrt(er * er + eg * eg + eb * eb) };
  }

  /** The tier row matching an element, or null. */
  function classify(el) {
    for (let i = 0; i < CLASSIFIER.length; i++) {
      if (CLASSIFIER[i].test(el)) {
        return CLASSIFIER[i];
      }
    }
    return null;
  }

  function median(values) {
    const s = values.slice().sort(function (a, b) { return a - b; });
    const mid = s.length >> 1;
    return s.length % 2 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
  }

  /** Checks the tier medians against ORDER_RULES and returns the per-tier summary and the violations. */
  function orderCheck(byTier) {
    const tiers = Object.keys(TIER_RANK).map(function (t) {
      const v = byTier[t] || [];
      return {
        tier: t,
        count: v.length,
        min: v.length ? round(Math.min.apply(null, v), 1) : null,
        median: v.length ? round(median(v), 1) : null,
        max: v.length ? round(Math.max.apply(null, v), 1) : null
      };
    });
    const med = {};
    tiers.forEach(function (t) { med[t.tier] = t.median; });
    const violations = [];
    ORDER_RULES.forEach(function (r) {
      const lo = med[r[0]];
      const hi = med[r[1]];
      if (lo === null || hi === null) {
        return;
      }
      const bad = r[2] === '<' ? lo > hi - ORDER_EPSILON : lo > hi + ORDER_EPSILON;
      if (bad) {
        violations.push(r[0] + ' ' + r[2] + ' ' + r[1] + ' required, but ' + r[0] + ' is Ink ' + lo + '% and ' + r[1] + ' is Ink ' + hi + '%');
      }
    });
    return { tiers: tiers, violations: violations };
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

  const SIDES = ['top', 'right', 'bottom', 'left'];

  /** Border radius as one string: a single value when all four corners agree. */
  function radiusOf(cs) {
    const r = [cs.borderTopLeftRadius, cs.borderTopRightRadius, cs.borderBottomRightRadius, cs.borderBottomLeftRadius];
    return r.every(function (v) { return v === r[0]; }) ? r[0] : r.join(' ');
  }

  /**
   * Lines report: every visible element's non-transparent border sides and background fill, placed
   * on the ink ladder and grouped by rung, plus the Rule < Edge < Field <= Chrome order check.
   */
  function linesReport(root) {
    const rootStyle = getComputedStyle(document.documentElement);
    const inkRaw = parseAnyColor(rootStyle.getPropertyValue(INK_VAR));
    const groundRaw = parseAnyColor(rootStyle.getPropertyValue(GROUND_VAR));
    if (!inkRaw || !groundRaw) {
      return { error: 'Could not read ' + INK_VAR + ' and ' + GROUND_VAR + ' from :root.' };
    }
    const ground = over(groundRaw, { r: 255, g: 255, b: 255, a: 1 });
    const ink = over(inkRaw, ground);
    const rungs = new Map();
    const offLadder = { count: 0, examples: [] };
    const byTier = {};
    const unclassified = { count: 0 };
    let elements = 0;
    let lines = 0;

    function place(el, cs, prop, colour, groundHere, tierRow, isBorder, rect) {
      const solid = over(colour, groundHere);
      const ratio = round(contrast(solid, groundHere), 2);
      const fit = solveRung(solid, ink, ground);
      const percent = fit ? round(fit.n * 100, 1) : null;
      const off = fit ? round(fit.off, 1) : null;
      const onLadder = fit !== null && fit.off <= LADDER_TOLERANCE && fit.n >= -0.01 && fit.n <= 1.01;
      const text = prop + ' ' + 'rgb(' + Math.round(solid.r) + ', ' + Math.round(solid.g) + ', ' + Math.round(solid.b) + ')' +
        '  ratio ' + ratio + ':1  Ink ' + percent + '% (off line ' + off + ')' +
        '  ' + Math.round(rect.width) + 'x' + Math.round(rect.height) + '  radius ' + radiusOf(cs) +
        (tierRow ? '  tier ' + tierRow.tier : '') + '  [' + selectorOf(el) + ']';
      lines++;
      if (onLadder) {
        const key = Math.round(fit.n * 100);
        const g = rungs.get(key) || { rung: key, count: 0, examples: [] };
        g.count++;
        if (g.examples.length < EXAMPLES) {
          g.examples.push(text);
        }
        rungs.set(key, g);
      } else {
        offLadder.count++;
        if (offLadder.examples.length < EXAMPLES) {
          offLadder.examples.push(text);
        }
      }
      if (isBorder) {
        if (tierRow && onLadder) {
          (byTier[tierRow.tier] = byTier[tierRow.tier] || []).push(fit.n * 100);
        } else if (!tierRow) {
          unclassified.count++;
        }
      }
    }

    const all = root.querySelectorAll('*');
    for (let i = 0; i < all.length; i++) {
      const el = all[i];
      if (/^(SCRIPT|STYLE|NOSCRIPT|TEMPLATE|HEAD|META|LINK)$/.test(el.tagName)) {
        continue;
      }
      const rect = el.getBoundingClientRect();
      if (!isVisible(el, rect)) {
        continue;
      }
      const cs = getComputedStyle(el);
      elements++;

      // Border sides: sides with the same colour merge into one entry (all four = "border-color").
      const visibleSides = new Map();
      SIDES.forEach(function (side) {
        const width = parseFloat(cs['border' + side[0].toUpperCase() + side.slice(1) + 'Width']);
        const style = cs['border' + side[0].toUpperCase() + side.slice(1) + 'Style'];
        const raw = cs['border' + side[0].toUpperCase() + side.slice(1) + 'Color'];
        const c = parseColor(raw);
        if (width > 0 && style !== 'none' && style !== 'hidden' && c && c.a > 0) {
          const list = visibleSides.get(raw) || { color: c, sides: [] };
          list.sides.push(side);
          visibleSides.set(raw, list);
        }
      });
      visibleSides.forEach(function (entry) {
        const prop = entry.sides.length === 4 ? 'border-color' : entry.sides.map(function (s) { return 'border-' + s + '-color'; }).join('+');
        place(el, cs, prop, entry.color, opaqueBackground(el).color, classify(el), true, rect);
      });

      // Background fill, composited over the nearest opaque ground above the element.
      const fill = parseColor(cs.backgroundColor);
      if (fill && fill.a > 0) {
        const above = el.parentElement ? opaqueBackground(el.parentElement).color : { r: 255, g: 255, b: 255, a: 1 };
        place(el, cs, 'background-color', fill, above, null, false, rect);
      }
    }

    const order = orderCheck(byTier);
    order.unclassifiedBorders = unclassified.count;
    return {
      ink: 'rgb(' + Math.round(ink.r) + ', ' + Math.round(ink.g) + ', ' + Math.round(ink.b) + ')',
      ground: 'rgb(' + Math.round(ground.r) + ', ' + Math.round(ground.g) + ', ' + Math.round(ground.b) + ')',
      ladderTolerance: LADDER_TOLERANCE,
      elements: elements,
      lines: lines,
      rungs: Array.from(rungs.values()).sort(function (a, b) { return a.rung - b.rung; }),
      offLadder: offLadder,
      order: order
    };
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
    let transformScaled = 0;

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
      const scale = effectiveScale(el);
      if (Math.abs(scale - 1) > 0.001) {
        transformScaled++;
      }
      const px = round(parseFloat(cs.fontSize) * zoom * scale, 2);
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
      transformScaled: transformScaled,
      lines: linesReport(root),
      sizes: Array.from(sizes.values()).sort(function (a, b) { return a.px - b.px; }),
      failures: failures,
      belowMinimum: belowMinimum,
      weights: weights
    };
  }

  const api = { parseColor: parseColor, luminance: luminance, contrast: contrast, run: run, parseAnyColor: parseAnyColor, solveRung: solveRung, matrixScale: matrixScale, orderCheck: orderCheck, over: over };
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = api;
    return api;
  }
  return run();
})();
