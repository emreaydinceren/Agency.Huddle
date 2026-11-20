// Captures the product tour's screenshots in tour/screens/ from a running seeded app.
// Usage: node capture.js [baseUrl] [outDir]. See README.md in this folder.
'use strict';

const path = require('path');
const fs = require('fs');
const os = require('os');
const { chromium } = require('playwright-core');

const BASE = process.argv[2] || 'http://localhost:5311';
const OUT = process.argv[3] || path.resolve(__dirname, '../../tour/screens');
const SEED = process.argv[4] || 'C:\\seeds\\tour';
const VIEWPORT = { width: 1440, height: 900 };

/** A Team Room's id, read from the seed's manifest: ids are minted on each seed run. */
function teamRoomId(team) {
  const manifest = fs.readFileSync(path.join(SEED, 'manifest.md'), 'utf8');
  const match = manifest.match(new RegExp(`\\| ${team} Team Room \\| \`([0-9a-f]+)\``));
  if (!match) {
    throw new Error(`No Team Room for ${team} in ${SEED}\\manifest.md.`);
  }
  return match[1];
}

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

/** Clicks a sidebar or page item by its exact text, if it is there. */
async function clickText(page, text) {
  await page.getByText(text, { exact: true }).first().click();
  await page.waitForTimeout(1200);
}

/**
 * The tour's pictures, in TOUR.md's order. Each opens a URL with the saved tabs cleared, runs an
 * optional step on the page, and saves the whole 1440 x 900 window.
 */
const SHOTS = [
  { file: 'team-room.png', url: () => `/rooms/${teamRoomId('Home and Family')}` },
  { file: 'board.png', url: '/tasks/home-board' },
  { file: 'task.png', url: '/tasks/item/HOME-0007' },
  { file: 'needs-you.png', url: '/needs-you' },
  {
    // The note open, and its Team's folder unfolded in the tree so raw, memory and outputs show.
    file: 'library.png',
    url: '/library?root=teams&path=Home%20and%20Family%2FFamily-Calendar.md',
    step: async (page) => {
      const tree = page.locator('.library-tree-node-name');
      if (!(await tree.getByText('raw', { exact: true }).first().isVisible())) {
        await tree.getByText('Home and Family', { exact: true }).first().click();
        await page.waitForTimeout(1200);
      }
    },
  },
  { file: 'build-team.png', url: '/teams/build' },
  { file: 'dev-team.png', url: '/teams/Storefront' },
  { file: 'dev-room.png', url: () => `/rooms/${teamRoomId('Storefront')}` },
  {
    // The Task panel scrolled down to its Timeline's Evidence lines.
    file: 'dev-task.png',
    url: '/tasks/item/STOR-0003',
    step: async (page) => {
      await page.getByText('browser-check /checkout').first().scrollIntoViewIfNeeded();
      await page.waitForTimeout(600);
    },
  },
  {
    // One Team only: the seed's Feed also holds a failing-Helper item, which reads as broken in a tour.
    file: 'feed.png',
    url: '/feed',
    step: async (page) => {
      await page.getByText('All teams', { exact: true }).first().click();
      await page.waitForTimeout(500);
      await page.locator('.mud-popover-open').getByText('Home and Family', { exact: true }).first().click();
      await page.waitForTimeout(1200);
    },
  },
  { file: 'needs-you-level.png', url: '/settings/needsyou' },
];

/** The tour's two Themes, one light and one dark; each picture is taken in both, as <shot>--<theme>.png. */
const THEMES = [
  { id: 'nordic-light', scheme: 'light' },
  { id: 'cobalt-soft-dark', scheme: 'dark' },
];

/** Writes the Theme into the lane's appearance.json and waits until a fresh page renders it. */
async function applyTheme(browser, theme) {
  fs.writeFileSync(path.join(SEED, 'data', 'appearance.json'), JSON.stringify({ theme: theme.id }) + '\n');
  const context = await browser.newContext({ viewport: VIEWPORT });
  const page = await context.newPage();
  let marker = null;
  for (let i = 0; i < 20 && marker !== theme.id; i++) {
    await page.goto(BASE + '/feed');
    marker = await page.evaluate(() => document.querySelector('[data-huddle-theme]')?.getAttribute('data-huddle-theme') ?? null);
    if (marker !== theme.id) {
      await page.waitForTimeout(500);
    }
  }
  await context.close();
  if (marker !== theme.id) {
    throw new Error(`The Theme did not switch to ${theme.id} (the page reports ${marker}).`);
  }
}

/** Captures every picture in SHOTS in every Theme, then puts the lane's appearance.json back. */
async function main() {
  fs.mkdirSync(OUT, { recursive: true });
  const appearance = path.join(SEED, 'data', 'appearance.json');
  const original = fs.existsSync(appearance) ? fs.readFileSync(appearance) : null;
  const browser = await chromium.launch({ executablePath: chromiumPath() });
  try {
    for (const theme of THEMES) {
      await applyTheme(browser, theme);
      await captureAll(browser, theme);
    }
  } finally {
    if (original) {
      fs.writeFileSync(appearance, original);
    } else {
      fs.rmSync(appearance, { force: true });
    }
    await browser.close();
  }
}

/** Captures every picture in SHOTS in one Theme. */
async function captureAll(browser, theme) {
  const context = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 1, colorScheme: theme.scheme });
  for (const s of SHOTS) {
    const file = s.file.replace(/\.png$/, `--${theme.id}.png`);
    const page = await context.newPage();
    const url = BASE + (typeof s.url === 'function' ? s.url() : s.url);
    await page.goto(url);
    await page.evaluate(() => localStorage.clear());
    await page.goto(url);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1200);
    if (s.step) {
      await s.step(page);
    }
    await page.evaluate(() => document.activeElement?.blur());
    await page.screenshot({ path: path.join(OUT, file) });
    console.log(`wrote ${file}`);
    await page.close();
  }
  await context.close();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
