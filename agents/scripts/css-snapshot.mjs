#!/usr/bin/env node
// Computed-style snapshot and diff for the Huddle chat surface. Driven by Css-Snapshot.ps1; run it directly
// with `node agents/scripts/css-snapshot.mjs capture|diff|routes ...` only when you already have an instance.
//
//   capture : drives headless Chrome (DevTools protocol over Node's built-in WebSocket and fetch, no npm
//             packages) over every route in every Theme at every width and writes one gzip JSON per page.
//   diff    : compares two snapshot folders and reports every difference; exit 0 only when there are none.
//   routes  : prints the route list that capture would walk (needs --manifest).
//
// It reads the rendered page only (computed styles), so it does not depend on stylesheet file names.
// The exclusions are listed in `Css-Snapshot.ps1 -?` (the help there is the authoritative copy).

import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync, statSync } from 'node:fs';
import { gunzipSync, gzipSync } from 'node:zlib';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseArgs } from 'node:util';

const FORMAT_VERSION = 1;

/** The eight Theme and mode combinations: four skins, each with a light and a dark Theme. */
const ALL_THEMES = ['lumen-light', 'lumen-dark', 'fluent-light', 'fluent-dark', 'nordic-light', 'nordic-dark', 'cobalt-soft-light', 'cobalt-soft-dark'];
const ALL_WIDTHS = [1280, 768];
const VIEWPORT_HEIGHT = 900;

/** Animation and transition declarations are read before the snapshot browser switches motion off. */
const MOTION_PROPS = [
  'transition-property', 'transition-duration', 'transition-timing-function', 'transition-delay',
  'animation-name', 'animation-duration', 'animation-timing-function', 'animation-iteration-count', 'animation-fill-mode', 'will-change',
];

/** The fixed property list recorded for every element, read with the snapshot browser's motion switched off. */
const STYLE_PROPS = [
  // box
  'display', 'position', 'inset', 'width', 'height', 'min-width', 'min-height', 'max-width', 'max-height', 'margin', 'padding', 'box-sizing',
  'overflow', 'overflow-x', 'overflow-y', 'float', 'clear', 'aspect-ratio', 'object-fit', 'z-index', 'order', 'vertical-align',
  // flex and grid
  'flex-direction', 'flex-wrap', 'flex-grow', 'flex-shrink', 'flex-basis', 'gap', 'align-items', 'align-content', 'align-self',
  'justify-content', 'justify-items', 'justify-self', 'grid-template-columns', 'grid-template-rows', 'grid-column', 'grid-row', 'grid-auto-flow',
  // type
  'font-family', 'font-size', 'font-weight', 'font-style', 'font-variant-numeric', 'line-height', 'letter-spacing', 'text-transform',
  'white-space', 'text-overflow', 'text-align', 'text-decoration-line', 'text-decoration-color', 'text-indent', 'word-break', 'overflow-wrap',
  'line-clamp', '-webkit-line-clamp', 'list-style-type',
  // colour
  'color', 'background-color', 'background-image', 'background-size', 'background-position', 'background-repeat', 'opacity', 'fill', 'stroke', 'stroke-width',
  // border, outline, shadow
  'border-width', 'border-style', 'border-color', 'border-radius', 'outline-width', 'outline-style', 'outline-color', 'outline-offset',
  'box-shadow', 'text-shadow',
  // effects and interaction
  'visibility', 'transform', 'translate', 'scale', 'rotate', 'filter', 'backdrop-filter', 'mix-blend-mode', 'cursor', 'pointer-events', 'user-select',
  'resize', 'scrollbar-width', 'scrollbar-gutter', 'accent-color', 'color-scheme', 'appearance',
];

const PSEUDO_PROPS = [
  'content', 'color', 'background-color', 'background-image', 'display', 'position', 'width', 'height', 'margin', 'padding',
  'font-size', 'font-weight', 'opacity', 'border-width', 'border-style', 'border-color', 'border-radius', 'box-shadow', 'transform', 'inset',
];
const PLACEHOLDER_PROPS = ['color', 'opacity', 'font-style'];

/** What the snapshot browser injects (never the repo): motion off. */
const NEUTRALISER_CSS = '*,*::before,*::after{transition:none!important;animation:none!important;caret-color:transparent!important}';

// ---------------------------------------------------------------------------------------------------------------
// Page-side library. Evaluated once per page after it has loaded; everything inside is plain browser JavaScript.

const PAGE_LIB = String.raw`
(() => {
  if (window.__snap) { return true; }
  const SKIP = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'LINK', 'META']);
  const GENERATED = /[0-9a-f]{8}-[0-9a-f]{4}-|[0-9a-f]{12,}/i;
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const frame = () => new Promise(r => { requestAnimationFrame(() => r()); setTimeout(r, 400); });

  // Relative times ("5 minutes ago") change with the real clock whatever the seed's pinned date is, and they
  // change the width of the element that holds them, so the snapshot browser rewrites them to fixed text.
  const RELATIVE = [
    [/\b(?:just now|a moment ago)\b/gi, 'just now'],
    [/\b(?:about |over |almost |less than )?(?:\d+|an?|one)\s*(?:seconds?|secs?|minutes?|mins?|hours?|hrs?|days?|weeks?|wks?|months?|mos?|years?|yrs?|[smhdwy])\s+ago\b/gi, '5 min ago'],
    [/\bin\s+(?:\d+|an?)\s*(?:seconds?|secs?|minutes?|mins?|hours?|hrs?|days?|weeks?|months?|years?)\b/gi, 'in 5 min'],
    // The Feed's compact card age ("12 min", "217 h", "8 d") stands alone in its own text node.
    [/^\s*\d+\s*(?:min|mins|h|hr|hrs|d|w|wk|mo|y|yr|s|m)\s*$/i, '5 min'],
  ];

  function normaliseText() {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let changed = 0;
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const parent = n.parentElement;
      if (!parent || SKIP.has(parent.tagName)) { continue; }
      let text = n.nodeValue;
      for (const [pattern, replacement] of RELATIVE) { text = text.replace(pattern, replacement); }
      if (text !== n.nodeValue) { n.nodeValue = text; changed++; }
    }
    return changed;
  }

  async function settle(minMs, quietMs, maxMs) {
    const t0 = performance.now();
    let lastMutation = t0;
    const observer = new MutationObserver(() => { lastMutation = performance.now(); });
    observer.observe(document.documentElement, { subtree: true, childList: true, attributes: true, characterData: true });
    try { await document.fonts.ready; } catch (e) { /* no fonts API: nothing to wait for */ }
    let timedOut = false;
    for (;;) {
      await sleep(50);
      const now = performance.now();
      if (now - t0 >= minMs && now - lastMutation >= quietMs) { break; }
      if (now - t0 >= maxMs) { timedOut = true; break; }
    }
    observer.disconnect();
    const pending = [...document.images].filter(i => !i.complete);
    if (pending.length) { await Promise.race([Promise.all(pending.map(i => new Promise(r => { i.addEventListener('load', r); i.addEventListener('error', r); }))), sleep(3000)]); }
    if (document.activeElement && document.activeElement !== document.body) { document.activeElement.blur(); }
    try { await document.fonts.ready; } catch (e) { /* as above */ }
    await frame();
    await frame();
    return timedOut;
  }

  function segment(el) {
    const sig = signature(el);
    let n = 1;
    for (let s = el.previousElementSibling; s; s = s.previousElementSibling) { if (signature(s) === sig) { n++; } }
    return sig + ':' + n;
  }
  // Tag plus the sorted classes without generated ones. Counting siblings of the same signature, not just the same tag,
  // keeps the path stable when MudBlazor appends popovers of other kinds in a different order from one load to the next.
  function signature(el) {
    const classes = [...el.classList].filter(c => !GENERATED.test(c)).sort().join('.');
    return el.localName + (classes ? '.' + classes : '');
  }

  function collect() {
    const rows = [];
    const visit = (el, parent) => {
      const index = rows.length;
      rows.push({ el, parent, seg: segment(el) });
      for (let c = el.firstElementChild; c; c = c.nextElementSibling) { if (!SKIP.has(c.tagName)) { visit(c, index); } }
    };
    visit(document.body, -1);
    return rows;
  }

  function snapshot(cfg) {
    const normalised = normaliseText();
    const rows = collect();
    const strs = [];
    const lookup = new Map();
    const sv = s => { let i = lookup.get(s); if (i === undefined) { i = strs.length; strs.push(s); lookup.set(s, i); } return i; };

    // Pass 1: the motion declarations, before anything switches motion off.
    const motion = rows.map(r => { const cs = getComputedStyle(r.el); return cfg.motion.map(p => sv(cs.getPropertyValue(p))); });

    // Pass 2: motion off, then everything else.
    const style = document.createElement('style');
    style.setAttribute('data-css-snapshot', '');
    style.textContent = cfg.neutraliser;
    document.documentElement.appendChild(style);
    void document.body.offsetHeight;

    const pseudo = (el, which, props) => {
      const cs = getComputedStyle(el, which);
      const content = cs.getPropertyValue('content');
      if (which !== '::placeholder' && (content === 'none' || content === 'normal' || content === '')) { return 0; }
      return props.map(p => sv(cs.getPropertyValue(p)));
    };

    const out = rows.map((r, i) => {
      const el = r.el;
      const cs = getComputedStyle(el);
      const values = motion[i].concat(cfg.props.map(p => sv(cs.getPropertyValue(p))));
      const tag = el.localName;
      const placeholder = (tag === 'input' || tag === 'textarea') ? pseudo(el, '::placeholder', cfg.placeholder) : 0;
      return [r.parent, sv(r.seg), values, pseudo(el, '::before', cfg.pseudo), pseudo(el, '::after', cfg.pseudo), placeholder];
    });

    const root = document.documentElement;
    const probe = getComputedStyle(root);
    const meta = {
      finalPath: (location.pathname + location.search).replace(/[0-9a-f]{32}/g, '<id>'),
      themeMarker: (document.querySelector('[data-huddle-theme]') || {}).getAttribute ? document.querySelector('[data-huddle-theme]').getAttribute('data-huddle-theme') : null,
      palettePrimary: probe.getPropertyValue('--mud-palette-primary').trim(),
      paletteBackground: probe.getPropertyValue('--mud-palette-background').trim(),
      documentWidth: root.scrollWidth,
      documentHeight: root.scrollHeight,
      elements: rows.length,
      normalisedTextNodes: normalised,
    };
    return JSON.stringify({ meta, strs, rows: out });
  }

  const find = spec => {
    const nodes = [...document.querySelectorAll(spec.sel)];
    return nodes.find(n => !spec.text || (n.textContent || '').replace(/\s+/g, ' ').trim().includes(spec.text)) || null;
  };
  async function waitFor(spec, timeoutMs) {
    const t0 = performance.now();
    for (;;) {
      const el = find(spec);
      if (el) { return true; }
      if (performance.now() - t0 > timeoutMs) { return false; }
      await sleep(100);
    }
  }
  function click(spec) { const el = find(spec); if (!el) { return false; } el.click(); return true; }
  function focus(spec) { const el = find(spec); if (!el) { return false; } el.focus(); return document.activeElement === el; }

  window.__snap = { settle, snapshot, waitFor, click, focus };
  return true;
})()
`;

// ---------------------------------------------------------------------------------------------------------------
// DevTools protocol client

class Cdp {
  static async connect(url) {
    const ws = new WebSocket(url);
    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = () => reject(new Error('cannot connect to ' + url)); });
    return new Cdp(ws);
  }

  constructor(ws) {
    this.ws = ws;
    this.nextId = 0;
    this.pending = new Map();
    ws.onmessage = event => {
      const message = JSON.parse(event.data);
      if (message.id !== undefined && this.pending.has(message.id)) {
        const { resolve, reject } = this.pending.get(message.id);
        this.pending.delete(message.id);
        if (message.error) { reject(new Error(message.error.message)); } else { resolve(message.result); }
      }
    };
    ws.onclose = () => { for (const { reject } of this.pending.values()) { reject(new Error('DevTools connection closed')); } this.pending.clear(); };
  }

  send(method, params = {}, sessionId = undefined) {
    return new Promise((resolve, reject) => {
      const id = ++this.nextId;
      this.pending.set(id, { resolve, reject });
      this.ws.send(JSON.stringify({ id, method, params, sessionId }));
    });
  }

  close() { try { this.ws.close(); } catch (e) { /* already closed */ } }
}

async function launchChrome(chromePath) {
  const userDir = mkdtempSync(join(tmpdir(), 'css-snap-chrome-'));
  const args = [
    '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${userDir}`, '--no-first-run', '--no-default-browser-check',
    '--disable-gpu', '--force-device-scale-factor=1', '--font-render-hinting=none', '--disable-background-timer-throttling',
    '--disable-renderer-backgrounding', '--disable-backgrounding-occluded-windows', '--disable-features=Translate,MediaRouter', '--mute-audio', 'about:blank',
  ];
  const proc = spawn(chromePath, args, { stdio: 'ignore' });
  const portFile = join(userDir, 'DevToolsActivePort');
  for (let i = 0; i < 100 && !existsSync(portFile); i++) { await sleep(100); }
  if (!existsSync(portFile)) { proc.kill(); throw new Error('Chrome did not start (no DevToolsActivePort in ' + userDir + ').'); }
  const [port, path] = readFileSync(portFile, 'utf8').split(/\r?\n/);
  const cdp = await Cdp.connect(`ws://127.0.0.1:${port}${path}`);
  const version = await cdp.send('Browser.getVersion');
  return {
    cdp, version: version.product,
    async close() { cdp.close(); proc.kill(); await sleep(300); try { rmSync(userDir, { recursive: true, force: true }); } catch (e) { /* Chrome still holds a file: the temp folder is cleaned by the OS */ } },
  };
}

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

// ---------------------------------------------------------------------------------------------------------------
// Pages

/**
 * Opens a page in its own browser context (so local storage, tab restore and the sidebar state start empty),
 * runs `body(page)`, and always disposes the context.
 */
async function withPage(chrome, width, body) {
  const { cdp } = chrome;
  const { browserContextId } = await cdp.send('Target.createBrowserContext', { disposeOnDetach: false });
  try {
    const { targetId } = await cdp.send('Target.createTarget', { url: 'about:blank', browserContextId });
    const { sessionId } = await cdp.send('Target.attachToTarget', { targetId, flatten: true });
    await cdp.send('Page.enable', {}, sessionId);
    await cdp.send('Page.bringToFront', {}, sessionId);
    await cdp.send('Emulation.setDeviceMetricsOverride', { width, height: VIEWPORT_HEIGHT, deviceScaleFactor: 1, mobile: false }, sessionId);
    const page = {
      async eval(expression) {
        const result = await cdp.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true }, sessionId);
        if (result.exceptionDetails) { throw new Error('page script failed: ' + (result.exceptionDetails.exception?.description ?? result.exceptionDetails.text)); }
        return result.result.value;
      },
      async goto(url) {
        await cdp.send('Page.navigate', { url }, sessionId);
        for (let i = 0; i < 300; i++) {
          await sleep(100);
          try { if ((await page.eval('document.readyState')) === 'complete' && (await page.eval('location.href')) !== 'about:blank') { return; } } catch (e) { /* navigating */ }
        }
        throw new Error('timed out loading ' + url);
      },
      async typeText(text) { await cdp.send('Input.insertText', { text }, sessionId); },
    };
    await body(page);
  } finally {
    try { await cdp.send('Target.disposeBrowserContext', { browserContextId }); } catch (e) { /* the browser is shutting down */ }
  }
}

async function readThemeMarker(chrome, baseUrl) {
  let marker = null;
  await withPage(chrome, 1280, async page => {
    await page.goto(baseUrl + '/feed');
    for (let i = 0; i < 40 && !marker; i++) {
      marker = await page.eval(`(document.querySelector('[data-huddle-theme]') || {getAttribute: () => null}).getAttribute('data-huddle-theme')`);
      if (!marker) { await sleep(150); }
    }
  });
  return marker;
}

/** Writes the Theme into appearance.json and waits until a fresh page really renders it. */
async function applyTheme(chrome, opts, theme) {
  writeFileSync(opts.appearancePath, JSON.stringify({ theme }) + '\n');
  let last = null;
  for (let attempt = 0; attempt < 40; attempt++) {
    await sleep(attempt === 0 ? 1200 : 700);
    last = await readThemeMarker(chrome, opts.baseUrl);
    if (last === theme) { return; }
  }
  throw new Error(`The Theme did not switch to ${theme} (the page still reports ${last}). Is ${opts.appearancePath} the running instance's appearance.json?`);
}

// ---------------------------------------------------------------------------------------------------------------
// Routes

function parseRooms(manifestText) {
  const rooms = new Map();
  let inRooms = false;
  for (const line of manifestText.split(/\r?\n/)) {
    if (line.startsWith('## ')) { inRooms = line.trim() === '## Rooms'; continue; }
    if (!inRooms || !line.startsWith('|')) { continue; }
    const cells = line.split('|').map(c => c.trim());
    const id = /`([0-9a-f]+)`/.exec(cells[3] ?? '');
    if (id) { rooms.set(`${cells[1]}|${cells[2]}`, id[1]); }
  }
  return rooms;
}

function buildRoutes(manifestText, settingsPaths) {
  const rooms = parseRooms(manifestText);
  const room = (name, scope) => {
    const id = rooms.get(`${name}|${scope}`);
    if (!id) { throw new Error(`The manifest has no Room '${name}' with scope '${scope}'.`); }
    return `/rooms/${id}`;
  };
  const routes = [
    { name: 'feed', path: '/feed' },
    { name: 'needs-you', path: '/needs-you' },
    { name: 'agents', path: '/agents' },
    { name: 'teams-build', path: '/teams/build' },
    { name: 'scheduled', path: '/scheduled' },
    { name: 'library', path: '/library' },
    { name: 'library-note', path: '/library?root=teams&path=Platform%2FEngineering-Handbook.md' },
    { name: 'library-note-pagination', path: '/library?root=teams&path=Platform%2FAPI%20v2%2FPagination.md' },
    { name: 'room-group', path: room('Platform standup', 'root') },
    { name: 'room-direct', path: room('Owen', 'root') },
    { name: 'room-needs-you', path: room('Launch crew', 'root') },
    { name: 'room-incident', path: room('Incident 0412 war room', 'root') },
    { name: 'room-team', path: room('Platform', 'Platform Team Room') },
    { name: 'room-team-chat', path: room('Pagination design', 'Platform chat') },
  ];
  for (const tab of ['members', 'files', 'tasks', 'process', 'details']) { routes.push({ name: `team-platform-${tab}`, path: `/teams/Platform/${tab}` }); }
  routes.push({ name: 'team-sandbox', path: '/teams/Sandbox' });
  routes.push({ name: 'team-home', path: '/teams/Home%20and%20Family' });
  routes.push({ name: 'project-api-v2-files', path: '/teams/Platform/projects/API%20v2/files' });
  routes.push({ name: 'project-api-v2-tasks', path: '/teams/Platform/projects/API%20v2/tasks' });
  routes.push({ name: 'tasks-all', path: '/tasks/all-tasks' });
  routes.push({ name: 'tasks-kanban', path: '/tasks/platform-board' });
  routes.push({ name: 'tasks-list-view', path: '/tasks/growth-by-project' });
  routes.push({ name: 'task-read', path: '/tasks/item/PLAT-0002' });
  routes.push({ name: 'task-edit', path: '/tasks/item/PLAT-0002/edit' });
  routes.push({ name: 'task-request', path: '/tasks/item/PLAT-0016' });
  for (const path of settingsPaths) { routes.push({ name: 'settings-' + path.split('/').pop(), path }); }
  routes.push({ name: 'settings-theme-lab', path: '/settings/theme-lab' });

  // Opened states: each opens something deterministically with a click or a typed character, then waits for it.
  routes.push({
    name: 'open-notifications', path: '/feed',
    steps: [{ click: { sel: 'button[aria-label^="Notifications"]' } }, { waitFor: { sel: '.notifications-panel' } }],
  });
  routes.push({
    name: 'open-new-chat', path: '/feed',
    steps: [{ click: { sel: '.mud-nav-link-text', text: 'New chat' } }, { waitFor: { sel: '.new-chat-panel' } }],
  });
  routes.push({
    name: 'open-archived-chats', path: '/feed',
    steps: [{ click: { sel: '[aria-label="Archived chats"]' } }, { waitFor: { sel: '.mud-dialog' } }],
  });
  routes.push({
    name: 'open-reference-picker', path: room('Platform standup', 'root'),
    steps: [{ focus: { sel: 'textarea.composer-input' } }, { type: '@' }, { waitFor: { sel: '.composer-picker-item' } }],
  });
  return routes;
}

// ---------------------------------------------------------------------------------------------------------------
// Capture

const slug = text => text.replace(/[^A-Za-z0-9._-]+/g, '-');
const fileName = (route, theme, width) => `${slug(route)}__${theme}__${width}.json.gz`;

async function runSteps(page, steps) {
  for (const step of steps) {
    if (step.click) {
      if (!(await page.eval(`window.__snap.click(${JSON.stringify(step.click)})`))) { throw new Error('step click found nothing: ' + JSON.stringify(step.click)); }
    } else if (step.focus) {
      if (!(await page.eval(`window.__snap.focus(${JSON.stringify(step.focus)})`))) { throw new Error('step focus found nothing: ' + JSON.stringify(step.focus)); }
    } else if (step.type !== undefined) {
      await page.typeText(step.type);
    } else if (step.waitFor) {
      if (!(await page.eval(`window.__snap.waitFor(${JSON.stringify(step.waitFor)}, 8000)`))) { throw new Error('step waitFor timed out: ' + JSON.stringify(step.waitFor)); }
    }
  }
}

async function captureOne(chrome, opts, route, theme, width) {
  let result = null;
  await withPage(chrome, width, async page => {
    await page.goto(opts.baseUrl + route.path);
    await page.eval(PAGE_LIB);
    let timedOut = await page.eval(`window.__snap.settle(${opts.settleMinMs}, ${opts.settleQuietMs}, ${opts.settleMaxMs})`);
    if (route.steps) {
      await runSteps(page, route.steps);
      timedOut = (await page.eval(`window.__snap.settle(300, ${opts.settleQuietMs}, ${opts.settleMaxMs})`)) || timedOut;
    }
    const cfg = { motion: MOTION_PROPS, props: STYLE_PROPS, pseudo: PSEUDO_PROPS, placeholder: PLACEHOLDER_PROPS, neutraliser: NEUTRALISER_CSS };
    const json = await page.eval(`window.__snap.snapshot(${JSON.stringify(cfg)})`);
    const data = JSON.parse(json);
    if (data.meta.themeMarker !== theme) { throw new Error(`rendered Theme '${data.meta.themeMarker}', expected '${theme}'`); }
    data.meta.settleTimedOut = timedOut;
    data.v = FORMAT_VERSION;
    data.route = route.name;
    data.theme = theme;
    data.width = width;
    data.props = MOTION_PROPS.concat(STYLE_PROPS);
    data.pseudoProps = PSEUDO_PROPS;
    data.placeholderProps = PLACEHOLDER_PROPS;
    result = data;
  });
  return result;
}

async function pool(items, concurrency, worker) {
  let next = 0;
  const run = async () => { while (next < items.length) { const item = items[next++]; await worker(item); } };
  await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, run));
}

async function capture(opts) {
  const started = Date.now();
  if (existsSync(opts.out)) {
    const entries = readdirSync(opts.out);
    if (entries.length && !entries.includes('snapshot-meta.json')) { throw new Error(`${opts.out} is not empty and is not a snapshot folder; refusing to overwrite it.`); }
    for (const entry of entries) { if (entry.endsWith('.json.gz') || entry === 'snapshot-meta.json') { rmSync(join(opts.out, entry)); } }
  } else {
    mkdirSync(opts.out, { recursive: true });
  }
  const manifestText = readFileSync(opts.manifest, 'utf8');
  const originalAppearance = existsSync(opts.appearancePath) ? readFileSync(opts.appearancePath) : null;
  const chrome = await launchChrome(opts.chrome);
  const failures = [];
  const written = [];
  let routes = [];
  const signatures = new Map();
  try {
    // The Settings menu is read from the running app so a new tab is picked up without editing this script.
    await applyTheme(chrome, opts, opts.themes[0]);
    let settingsPaths = [];
    await withPage(chrome, 1280, async page => {
      await page.goto(opts.baseUrl + '/settings');
      await page.eval(PAGE_LIB);
      await page.eval('window.__snap.settle(1500, 500, 15000)');
      settingsPaths = await page.eval(`[...new Set([...document.querySelectorAll('nav.settings-nav a[href]')].map(a => a.getAttribute('href')))]`);
    });
    settingsPaths = settingsPaths.filter(p => p !== '/settings/theme-lab');
    if (settingsPaths.length === 0) { throw new Error('No Settings tabs were found in nav.settings-nav.'); }
    routes = buildRoutes(manifestText, settingsPaths);
    if (opts.only) { routes = routes.filter(r => opts.only.test(r.name)); }
    if (routes.length === 0) { throw new Error('--only matched no route.'); }
    console.log(`${routes.length} routes x ${opts.themes.length} Themes x ${opts.widths.length} widths = ${routes.length * opts.themes.length * opts.widths.length} pages; concurrency ${opts.concurrency}; Chrome ${chrome.version}.`);

    for (const theme of opts.themes) {
      await applyTheme(chrome, opts, theme);
      const jobs = routes.flatMap(route => opts.widths.map(width => ({ route, width })));
      let done = 0;
      await pool(jobs, opts.concurrency, async job => {
        let lastError = null;
        for (let attempt = 1; attempt <= 3; attempt++) {
          try {
            const data = await captureOne(chrome, opts, job.route, theme, job.width);
            const file = fileName(job.route.name, theme, job.width);
            writeFileSync(join(opts.out, file), gzipSync(Buffer.from(JSON.stringify(data)), { level: 9 }));
            written.push({ file, elements: data.meta.elements, finalPath: data.meta.finalPath, settleTimedOut: data.meta.settleTimedOut });
            const sig = `${data.meta.palettePrimary}|${data.meta.paletteBackground}`;
            if (!signatures.has(theme)) { signatures.set(theme, sig); }
            lastError = null;
            break;
          } catch (error) {
            lastError = error;
            await sleep(500);
          }
        }
        if (lastError) { failures.push(`${job.route.name} | ${theme} | ${job.width}: ${lastError.message}`); }
        done++;
        if (done % 20 === 0 || done === jobs.length) { console.log(`  ${theme}: ${done}/${jobs.length} pages (${Math.round((Date.now() - started) / 1000)}s)`); }
      });
    }
  } finally {
    if (originalAppearance) { writeFileSync(opts.appearancePath, originalAppearance); } else { rmSync(opts.appearancePath, { force: true }); }
    await chrome.close();
  }

  const distinct = new Set(signatures.values());
  const warnings = [];
  if (opts.themes.length > 1 && distinct.size < opts.themes.length) { warnings.push(`only ${distinct.size} distinct palette signatures for ${opts.themes.length} Themes; a Theme switch may not have taken`); }
  for (const w of written.filter(w => w.settleTimedOut)) { warnings.push(`${w.file}: never went quiet before the settle limit (a live region keeps changing)`); }
  const meta = {
    v: FORMAT_VERSION, tool: 'css-snapshot', capturedAtUtc: new Date().toISOString(), capturedOnUtcDate: new Date().toISOString().slice(0, 10),
    baseUrl: opts.baseUrl, chrome: chrome.version, node: process.version, themes: opts.themes, widths: opts.widths,
    routes: routes.map(r => ({ name: r.name, path: r.path, steps: r.steps ?? null })), propertyCount: MOTION_PROPS.length + STYLE_PROPS.length,
    pages: written.sort((a, b) => a.file.localeCompare(b.file)), failures, warnings,
  };
  writeFileSync(join(opts.out, 'snapshot-meta.json'), JSON.stringify(meta, null, 1));

  const bytes = written.reduce((sum, w) => sum + statSync(join(opts.out, w.file)).size, 0);
  console.log(`Captured ${written.length} pages, ${(bytes / 1048576).toFixed(1)} MB gzip, ${Math.round((Date.now() - started) / 1000)}s.`);
  for (const w of warnings) { console.log('WARNING: ' + w); }
  if (failures.length) { console.log(`${failures.length} page(s) failed:`); for (const f of failures) { console.log('  ' + f); } return 1; }
  return 0;
}

// ---------------------------------------------------------------------------------------------------------------
// Diff

function loadSnapshot(folder, file) {
  const data = JSON.parse(gunzipSync(readFileSync(join(folder, file))).toString('utf8'));
  const paths = new Array(data.rows.length);
  const byPath = new Map();
  data.rows.forEach((row, i) => {
    const seg = data.strs[row[1]];
    paths[i] = row[0] < 0 ? seg : paths[row[0]] + ' > ' + seg;
    byPath.set(paths[i], i);
  });
  return { data, paths, byPath };
}

/**
 * Elements whose presence follows server state rather than CSS. Pages are captured six at a time against one instance,
 * and opening some pages marks Feed items seen, so the notification count's badge wrapper is there or not depending on
 * which page loaded first. It is absolutely positioned, so it moves nothing else.
 */
const VOLATILE_PATH = /notifications-badge:\d+ > span\.mud-badge-/;

const PX = /(-?\d*\.?\d+(?:e[+-]?\d+)?)px/g;

const MATRIX = /matrix\(([^,]+),([^,]+),([^,]+),([^,]+),\s*([^,]+),\s*([^)]+)\)/g;

/** A computed `matrix(a, b, c, d, e, f)` with its translation (e, f) written as px, so only those two are loosened. */
function translationAsPx(value) {
  return value.replace(MATRIX, (_, a, b, c, d, e, f) => `matrix(${a},${b},${c},${d}, ${e.trim()}px, ${f.trim()}px)`);
}

/**
 * True when two computed values differ only in px lengths (and a matrix's translation), each by at most `tolerance`
 * px. Text that Chrome lays out with a web font (Nordic) measures up to about 0.15 px differently from one capture of
 * the same build to the next; a CSS change moves whole pixels or changes a token. Only px numbers are compared loosely:
 * opacity, colour channels, weights, scale factors and every keyword still have to match exactly.
 */
function samePx(rawA, rawB, tolerance) {
  if (tolerance <= 0) { return false; }
  const va = translationAsPx(rawA);
  const vb = translationAsPx(rawB);
  const restA = va.replace(PX, 'px');
  const restB = vb.replace(PX, 'px');
  if (restA !== restB) { return false; }
  const numsA = [...va.matchAll(PX)].map(m => Number(m[1]));
  const numsB = [...vb.matchAll(PX)].map(m => Number(m[1]));
  if (numsA.length === 0 || numsA.length !== numsB.length) { return false; }
  return numsA.every((n, i) => Math.abs(n - numsB[i]) <= tolerance);
}

/** The differences between two snapshots of one page, as { removed, added, changes: [{ path, prop, from, to }] }. */
function comparePage(a, b, tolerance) {
  const changes = [];
  const removed = [];
  const added = [];
  const value = (snap, row, i) => snap.data.strs[row[2][i]];
  const names = a.data.props;
  for (const [path, ia] of a.byPath) {
    if (VOLATILE_PATH.test(path)) { continue; }
    const ib = b.byPath.get(path);
    if (ib === undefined) { removed.push(path); continue; }
    const ra = a.data.rows[ia];
    const rb = b.data.rows[ib];
    for (let p = 0; p < names.length; p++) {
      const va = value(a, ra, p);
      const vb = value(b, rb, p);
      if (va !== vb && !samePx(va, vb, tolerance)) { changes.push({ path, prop: names[p], from: va, to: vb }); }
    }
    const groups = [[3, '::before', a.data.pseudoProps], [4, '::after', a.data.pseudoProps], [5, '::placeholder', a.data.placeholderProps]];
    for (const [slot, label, props] of groups) {
      const pa = ra[slot];
      const pb = rb[slot];
      if (pa === 0 && pb === 0) { continue; }
      props.forEach((prop, k) => {
        const va = pa === 0 ? '(none)' : a.data.strs[pa[k]];
        const vb = pb === 0 ? '(none)' : b.data.strs[pb[k]];
        if (va !== vb && !samePx(va, vb, tolerance)) { changes.push({ path, prop: `${label} ${prop}`, from: va, to: vb }); }
      });
    }
  }
  for (const path of b.byPath.keys()) { if (!a.byPath.has(path) && !VOLATILE_PATH.test(path)) { added.push(path); } }
  return { removed, added, changes };
}

function topLevel(list) {
  const set = new Set(list);
  return list.filter(path => { const cut = path.lastIndexOf(' > '); return cut < 0 || !set.has(path.slice(0, cut)); });
}

function readMeta(folder) {
  const file = join(folder, 'snapshot-meta.json');
  if (!existsSync(file)) { throw new Error(`${folder} has no snapshot-meta.json; it is not a snapshot folder.`); }
  return JSON.parse(readFileSync(file, 'utf8'));
}

function diff(opts) {
  const metaA = readMeta(opts.baseline);
  const metaB = readMeta(opts.candidate);
  const filesA = new Set(readdirSync(opts.baseline).filter(f => f.endsWith('.json.gz')));
  const filesB = new Set(readdirSync(opts.candidate).filter(f => f.endsWith('.json.gz')));
  const all = [...new Set([...filesA, ...filesB])].sort();
  const lines = [];
  const rollup = new Map();
  let pagesWithDiff = 0;
  let totalChanges = 0;
  const notes = [];

  if (metaA.capturedOnUtcDate !== metaB.capturedOnUtcDate) {
    notes.push(`NOTE: the snapshots were taken on different days (${metaA.capturedOnUtcDate} and ${metaB.capturedOnUtcDate}); anything the app derives from today's date (overdue, relative days) can differ without a CSS change.`);
  }
  if (metaA.propertyCount !== metaB.propertyCount) { notes.push(`NOTE: the property lists differ (${metaA.propertyCount} and ${metaB.propertyCount}); take both snapshots with the same version of the tool.`); }

  for (const file of all) {
    const label = file.replace(/\.json\.gz$/, '').split('__').join(' | ');
    if (!filesA.has(file)) { lines.push(`== ${label}: only in the candidate`); pagesWithDiff++; totalChanges++; continue; }
    if (!filesB.has(file)) { lines.push(`== ${label}: only in the baseline`); pagesWithDiff++; totalChanges++; continue; }
    const a = loadSnapshot(opts.baseline, file);
    const b = loadSnapshot(opts.candidate, file);
    const result = comparePage(a, b, opts.tolerance);
    const pageNotes = [];
    for (const key of ['finalPath', 'themeMarker', 'documentWidth', 'documentHeight']) {
      if (a.data.meta[key] !== b.data.meta[key] && !samePx(`${a.data.meta[key]}px`, `${b.data.meta[key]}px`, opts.tolerance)) { pageNotes.push(`${key}: ${a.data.meta[key]} -> ${b.data.meta[key]}`); }
    }
    const removed = topLevel(result.removed);
    const added = topLevel(result.added);
    const count = result.changes.length + removed.length + added.length + pageNotes.length;
    if (count === 0) { continue; }
    pagesWithDiff++;
    totalChanges += count;
    const byPath = new Map();
    for (const change of result.changes) {
      if (!byPath.has(change.path)) { byPath.set(change.path, []); }
      byPath.get(change.path).push(change);
      const key = `${change.prop}: ${change.from} -> ${change.to}`;
      const entry = rollup.get(key) ?? { pages: new Set(), elements: 0, example: change.path };
      entry.pages.add(file);
      entry.elements++;
      rollup.set(key, entry);
    }
    lines.push(`== ${label}: ${byPath.size} element(s) changed, ${result.changes.length} property change(s), ${removed.length} removed, ${added.length} added`);
    for (const note of pageNotes) { lines.push(`   page ${note}`); }
    const limit = opts.full ? Infinity : opts.maxElements;
    let shown = 0;
    for (const [path, list] of byPath) {
      if (shown++ >= limit) { lines.push(`   ... ${byPath.size - limit} more element(s) (use -Full for all)`); break; }
      lines.push(`   ${path}`);
      for (const c of list) { lines.push(`       ${c.prop}: ${c.from} -> ${c.to}`); }
    }
    for (const path of removed.slice(0, limit)) { lines.push(`   - removed element ${path}`); }
    for (const path of added.slice(0, limit)) { lines.push(`   + added element ${path}`); }
  }

  const rollupLines = [...rollup.entries()].sort((x, y) => y[1].pages.size - x[1].pages.size).slice(0, opts.full ? Infinity : 40)
    .map(([key, e]) => `  ${key}  [${e.pages.size} page(s), ${e.elements} element(s); e.g. ${e.example}]`);

  console.log(`Baseline  ${opts.baseline}  (${metaA.pages.length} pages, ${metaA.capturedAtUtc})`);
  console.log(`Candidate ${opts.candidate}  (${metaB.pages.length} pages, ${metaB.capturedAtUtc})`);
  for (const n of notes) { console.log(n); }
  if (totalChanges === 0) { console.log(`No differences across ${all.length} pages.`); return 0; }
  console.log(`${totalChanges} difference(s) on ${pagesWithDiff} of ${all.length} pages.`);
  if (rollupLines.length) { console.log('\nDistinct property changes, most widespread first:'); for (const l of rollupLines) { console.log(l); } }
  console.log('\nBy route | Theme | width:');
  for (const l of lines) { console.log(l); }
  return 1;
}

// ---------------------------------------------------------------------------------------------------------------

async function main() {
  const [command, ...rest] = process.argv.slice(2);
  const { values } = parseArgs({
    args: rest,
    options: {
      'base-url': { type: 'string' }, out: { type: 'string' }, 'data-dir': { type: 'string' }, manifest: { type: 'string' },
      chrome: { type: 'string', default: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe' },
      concurrency: { type: 'string', default: '6' }, themes: { type: 'string' }, widths: { type: 'string' }, only: { type: 'string' },
      'settle-min-ms': { type: 'string', default: '1500' }, 'settle-quiet-ms': { type: 'string', default: '700' }, 'settle-max-ms': { type: 'string', default: '20000' },
      baseline: { type: 'string' }, candidate: { type: 'string' }, full: { type: 'boolean', default: false }, 'max-elements': { type: 'string', default: '25' },
      tolerance: { type: 'string', default: '0.25' },
    },
  });
  const need = (...names) => { for (const n of names) { if (!values[n]) { throw new Error(`--${n} is required.`); } } };

  if (command === 'capture') {
    need('base-url', 'out', 'data-dir', 'manifest');
    const themes = values.themes ? values.themes.split(',') : ALL_THEMES;
    for (const t of themes) { if (!ALL_THEMES.includes(t)) { throw new Error(`Unknown Theme '${t}'. Known: ${ALL_THEMES.join(', ')}.`); } }
    const opts = {
      baseUrl: values['base-url'].replace(/\/$/, ''), out: values.out, appearancePath: join(values['data-dir'], 'appearance.json'), manifest: values.manifest,
      chrome: values.chrome, concurrency: Math.max(1, parseInt(values.concurrency, 10)), themes,
      widths: values.widths ? values.widths.split(',').map(Number) : ALL_WIDTHS, only: values.only ? new RegExp(values.only) : null,
      settleMinMs: parseInt(values['settle-min-ms'], 10), settleQuietMs: parseInt(values['settle-quiet-ms'], 10), settleMaxMs: parseInt(values['settle-max-ms'], 10),
    };
    return capture(opts);
  }
  if (command === 'diff') {
    need('baseline', 'candidate');
    return diff({ baseline: values.baseline, candidate: values.candidate, full: values.full, maxElements: parseInt(values['max-elements'], 10), tolerance: Number(values.tolerance) });
  }
  if (command === 'routes') {
    need('manifest');
    for (const r of buildRoutes(readFileSync(values.manifest, 'utf8'), ['/settings/profile', '/settings/<each tab from the Settings menu>'])) { console.log(`${r.name}\t${r.path}${r.steps ? '\t(opened state)' : ''}`); }
    return 0;
  }
  throw new Error('usage: css-snapshot.mjs capture|diff|routes ...');
}

main().then(code => process.exit(code), error => { console.error('css-snapshot: ' + error.message); process.exit(2); });
