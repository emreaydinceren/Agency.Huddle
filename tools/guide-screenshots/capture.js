// Captures the annotated screenshots in docs/images/user-guide/ from a running seeded app.
// Usage: node capture.js [baseUrl] [outDir]. See README.md in this folder.
'use strict';

const path = require('path');
const fs = require('fs');
const os = require('os');
const { chromium } = require('playwright-core');

const BASE = process.argv[2] || 'http://localhost:1111';
const OUT = process.argv[3] || path.resolve(__dirname, '../../docs/images/user-guide');
const VIEWPORT = { width: 1440, height: 900 };
const SCALE = 2;
const CALLOUT = '#d6336c';
const RADIUS = 11;

/** Finds the newest Chromium that Playwright has downloaded, unless CHROMIUM is set. */
function chromiumPath() {
  if (process.env.CHROMIUM) {
    return process.env.CHROMIUM;
  }
  const root = path.join(os.homedir(), 'AppData', 'Local', 'ms-playwright');
  const dirs = fs.readdirSync(root).filter((d) => /^chromium-\d+$/.test(d)).sort().reverse();
  for (const d of dirs) {
    for (const sub of ['chrome-win64', 'chrome-win']) {
      const exe = path.join(root, d, sub, 'chrome.exe');
      if (fs.existsSync(exe)) {
        return exe;
      }
    }
  }
  throw new Error('No Chromium found; set CHROMIUM to a chrome.exe.');
}

/** The point on a box that a callout's leader line ends at. */
function anchorPoint(box, at) {
  switch (at) {
    case 'left': return { x: box.x, y: box.y + (box.height / 2) };
    case 'right': return { x: box.x + box.width, y: box.y + (box.height / 2) };
    case 'top': return { x: box.x + (box.width / 2), y: box.y };
    case 'bottom': return { x: box.x + (box.width / 2), y: box.y + box.height };
    default: return { x: box.x + (box.width / 2), y: box.y + (box.height / 2) };
  }
}

/**
 * Resolves each callout to a badge and a leader-line end in canvas coordinates (the clip plus its
 * padding). A badge sits at the anchor moved by dx/dy, or in the middle of a padding band when
 * bx is 'left'/'right' or by is 'top'/'bottom'.
 */
async function resolveCallouts(callouts, clip, pad) {
  const width = pad.left + clip.width + pad.right;
  const height = pad.top + clip.height + pad.bottom;
  const marks = [];
  for (const c of callouts) {
    const box = await c.target.boundingBox();
    if (!box) {
      throw new Error(`Callout ${c.n} has no box.`);
    }
    const a = anchorPoint(box, c.at || 'left');
    const end = { x: a.x - clip.x + pad.left, y: a.y - clip.y + pad.top };
    let x = end.x + (c.dx || 0);
    let y = end.y + (c.dy || 0);
    if (c.bx === 'left') {
      x = pad.left / 2;
    }
    if (c.bx === 'right') {
      x = width - (pad.right / 2);
    }
    if (c.by === 'top') {
      y = pad.top / 2;
    }
    if (c.by === 'bottom') {
      y = height - (pad.bottom / 2);
    }
    marks.push({ n: c.n, end, badge: { x, y } });
  }
  return marks;
}

/** Draws the raw PNG on a padded white canvas with the numbered badges and writes the annotated PNG. */
async function annotate(page, raw, clip, pad, marks, file) {
  const width = pad.left + clip.width + pad.right;
  const height = pad.top + clip.height + pad.bottom;
  const svg = marks.map((m) => {
    const line = (m.badge.x !== m.end.x || m.badge.y !== m.end.y)
      ? `<line x1="${m.badge.x}" y1="${m.badge.y}" x2="${m.end.x}" y2="${m.end.y}" stroke="${CALLOUT}" stroke-width="2"/>`
        + `<circle cx="${m.end.x}" cy="${m.end.y}" r="3" fill="${CALLOUT}"/>`
      : '';
    return `${line}<circle cx="${m.badge.x}" cy="${m.badge.y}" r="${RADIUS}" fill="${CALLOUT}" stroke="#fff" stroke-width="2"/>`
      + `<text x="${m.badge.x}" y="${m.badge.y}" dy="0.35em" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="700" fill="#fff">${m.n}</text>`;
  }).join('');
  const html = `<!doctype html><html><body style="margin:0;background:#fff">
<div id="shot" style="position:relative;width:${width}px;height:${height}px;background:#fff">
<img src="data:image/png;base64,${raw.toString('base64')}" style="position:absolute;left:${pad.left}px;top:${pad.top}px;width:${clip.width}px;height:${clip.height}px;outline:1px solid #c9c9c9">
<svg style="position:absolute;inset:0" width="${width}" height="${height}">${svg}</svg>
</div></body></html>`;
  await page.setViewportSize({ width: Math.ceil(width), height: Math.ceil(height) });
  await page.setContent(html);
  await page.locator('#shot').screenshot({ path: path.join(OUT, file), scale: 'device' });
  console.log(`wrote ${file} (${marks.length} callouts)`);
}

/** Opens a fresh page at the given path with the tab state cleared. */
async function open(context, url) {
  const page = await context.newPage();
  await page.goto(BASE + url);
  await page.evaluate(() => localStorage.clear());
  await page.goto(BASE + url);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(800);
  return page;
}

/** Captures one shot: the clip of the app page, a padding band for badges, and the callouts. */
async function shot(app, annotator, file, clip, pad, callouts) {
  const padding = { top: 0, right: 0, bottom: 0, left: 0, ...pad };
  const marks = await resolveCallouts(callouts, clip, padding);
  const raw = await app.screenshot({ clip });
  await annotate(annotator, raw, clip, padding, marks, file);
}

/** Captures the six User Guide screenshots. */
async function main() {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch({ executablePath: chromiumPath() });
  const context = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: SCALE, colorScheme: 'light' });
  const annotator = await context.newPage();
  const nav = (page, name) => page.locator('.mud-navmenu').getByText(name, { exact: true }).first();
  const aria = (page, label) => page.locator(`[aria-label="${label}"]`).first();
  const tab = (page, name) => page.locator('.tab-item').filter({ hasText: name }).first();

  // 1. The Activity bar and the sidebar, on its Chats view. The bar's callouts sit in the left band, the
  // sidebar's in the right one.
  let page = await open(context, '/');
  await nav(page, 'Grace').click();
  await page.waitForTimeout(800);
  const drawer = await page.locator('.mud-drawer').first().boundingBox();
  const view = (name) => page.locator(`button[data-view="${name}"]`);
  // The Needs you card is drawn only while a Room waits, so it has no callout: the guide explains it in prose.
  await shot(page, annotator, 'sidebar.png', { x: 0, y: drawer.y, width: drawer.width, height: VIEWPORT.height - drawer.y }, { left: 44, right: 44 }, [
    { n: 1, target: view('Chats'), at: 'left', bx: 'left' },
    { n: 2, target: view('Teams'), at: 'left', bx: 'left' },
    { n: 3, target: view('Agents'), at: 'left', bx: 'left' },
    { n: 4, target: view('Tasks'), at: 'left', bx: 'left' },
    { n: 5, target: view('Library'), at: 'left', bx: 'left' },
    { n: 6, target: aria(page, 'Archived chats'), at: 'left', bx: 'left' },
    { n: 7, target: aria(page, 'Settings'), at: 'left', bx: 'left' },
    { n: 8, target: aria(page, 'Toggle Rooms'), at: 'right', bx: 'right' },
    { n: 9, target: aria(page, 'Room actions'), at: 'right', bx: 'right' },
    { n: 10, target: nav(page, 'Show all (13)'), at: 'right', bx: 'right' },
    { n: 11, target: nav(page, 'New chat'), at: 'right', bx: 'right' },
  ]);

  // 2. The tab strip: two kept tabs and a preview tab, with the middle one hovered.
  await nav(page, 'Grace').dblclick();
  await nav(page, 'Platform standup').dblclick();
  await nav(page, 'Owen').click();
  await page.waitForTimeout(800);
  const kept = tab(page, 'Platform standup');
  await kept.hover();
  await page.waitForTimeout(300);
  const strip = await page.locator('.tab-strip').first().boundingBox();
  const owen = await tab(page, 'Owen').boundingBox();
  const clipTabs = { x: strip.x, y: strip.y, width: (owen.x + owen.width + 12) - strip.x, height: strip.height };
  await shot(page, annotator, 'tab-strip.png', clipTabs, { top: 8, bottom: 44 }, [
    { n: 1, target: tab(page, 'Grace'), at: 'bottom', by: 'bottom' },
    { n: 2, target: kept.locator('svg').first(), at: 'bottom', by: 'bottom' },
    { n: 3, target: aria(page, 'More actions for Platform standup'), at: 'bottom', by: 'bottom' },
    { n: 4, target: kept.locator('[aria-label*="Move" i]').last(), at: 'bottom', by: 'bottom' },
    { n: 5, target: aria(page, 'Close Platform standup'), at: 'bottom', by: 'bottom' },
    { n: 6, target: tab(page, 'Owen'), at: 'bottom', by: 'bottom' },
  ]);

  // 3. A tab's More (⋮) menu.
  await aria(page, 'More actions for Platform standup').click();
  await page.waitForTimeout(500);
  const menu = page.locator('.mud-popover-open').last();
  const menuBox = await menu.boundingBox();
  const clipMenu = { x: strip.x, y: strip.y, width: (menuBox.x + menuBox.width + 24) - strip.x, height: (menuBox.y + menuBox.height + 16) - strip.y };
  await shot(page, annotator, 'tab-menu.png', clipMenu, { top: 40, right: 44 }, [
    { n: 1, target: aria(page, 'More actions for Platform standup'), at: 'top', by: 'top' },
    { n: 2, target: menu.getByText('Pin tab'), at: 'right', bx: 'right' },
    { n: 3, target: menu.getByText('Move left'), at: 'right', bx: 'right' },
    { n: 4, target: menu.getByText('Move right'), at: 'right', bx: 'right' },
    { n: 5, target: menu.getByText('Close', { exact: true }), at: 'right', bx: 'right' },
  ]);
  await page.keyboard.press('Escape');
  await page.close();

  // 4. A Room.
  page = await open(context, '/');
  await nav(page, 'Grace').click();
  await page.waitForTimeout(1000);
  const main = await page.locator('.mud-main-content').first().boundingBox();
  await shot(page, annotator, 'room.png', { x: main.x, y: 0, width: VIEWPORT.width - main.x, height: VIEWPORT.height }, {}, [
    { n: 1, target: page.locator('h1,h2,h3,h4,h5,h6').filter({ hasText: /^Grace$/ }).first(), at: 'bottom', dy: 30 },
    { n: 2, target: page.locator('[aria-label*="ename"]').first(), at: 'bottom', dy: 30 },
    { n: 3, target: page.locator('[aria-label*="link" i]').first(), at: 'bottom', dy: 30 },
    { n: 4, target: page.getByText(/member/).first(), at: 'bottom', dy: 30 },
    { n: 5, target: page.locator('[aria-label*="nvite" i]').first(), at: 'bottom', dx: -10, dy: 30 },
    { n: 6, target: aria(page, 'Show right sidebar'), at: 'left', dx: -40 },
    { n: 7, target: page.getByText('Today', { exact: true }).last(), at: 'right', dx: 60 },
    { n: 8, target: page.locator('.mud-avatar').filter({ hasText: 'GR' }).last(), at: 'left', dx: -40 },
    { n: 9, target: page.locator('a[href^="/tasks/item/"]').last(), at: 'bottom', dy: 30 },
    { n: 10, target: page.locator('textarea').first(), at: 'left', dx: -40 },
    { n: 11, target: page.locator('[aria-label*="ttach" i]').first(), at: 'top', dy: -30 },
    { n: 12, target: page.locator('[aria-label*="end" i]').last(), at: 'top', dx: 20, dy: -40 },
  ]);
  await page.close();

  // 5. A Board card.
  page = await open(context, '/tasks/platform-board');
  const card = page.locator('.task-card').filter({ hasText: 'PLAT-0002' }).first();
  const cardBox = await card.boundingBox();
  const lane = page.getByText('In Progress', { exact: true }).first();
  const laneBox = await lane.boundingBox();
  const clipCard = { x: cardBox.x - 10, y: laneBox.y - 10, width: cardBox.width + 20, height: (cardBox.y + cardBox.height + 10) - (laneBox.y - 10) };
  await shot(page, annotator, 'board-card.png', clipCard, { left: 44, right: 44 }, [
    { n: 1, target: lane.locator('xpath=..'), at: 'left', bx: 'left' },
    { n: 2, target: card.getByText('PLAT-0002'), at: 'left', bx: 'left' },
    { n: 3, target: card.getByText('High'), at: 'right', bx: 'right' },
    { n: 4, target: aria(page, 'Actions for PLAT-0002'), at: 'right', bx: 'right', dy: -28 },
    { n: 5, target: card.getByText('Implement pagination for list endpoints'), at: 'left', bx: 'left' },
    { n: 6, target: card.getByText('api', { exact: true }), at: 'left', bx: 'left' },
    { n: 7, target: card.locator('.mud-avatar').first(), at: 'left', bx: 'left' },
    { n: 8, target: card.getByText('Oct 13'), at: 'right', bx: 'right' },
  ]);
  await page.close();

  // 6. Build a team.
  page = await open(context, '/teams/build');
  await page.evaluate(() => document.activeElement?.blur());
  const mb = await page.locator('.mud-main-content').first().boundingBox();
  await shot(page, annotator, 'build-team.png', { x: mb.x, y: 0, width: VIEWPORT.width - mb.x, height: 465 }, {}, [
    { n: 1, target: page.getByText('Choose one', { exact: true }), at: 'bottom', dy: 22 },
    { n: 2, target: page.getByText(/^name and what it.s for$/), at: 'bottom', dy: 22 },
    { n: 3, target: page.getByText('add the roles'), at: 'bottom', dy: 22 },
    { n: 4, target: page.getByText('only you'), at: 'bottom', dy: 22 },
    { n: 5, target: page.getByText('Blank Team', { exact: true }), at: 'right', dx: -16 },
    { n: 6, target: page.getByText('Development Team', { exact: true }), at: 'right', dx: -16 },
    { n: 7, target: page.getByText('Manager + 7 roles'), at: 'right', dx: 36 },
  ]);
  await page.close();

  await browser.close();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
