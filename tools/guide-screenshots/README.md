# Guide screenshots

`capture.js` takes the annotated screenshots in `docs/images/user-guide/` from a running,
seeded app, so they can be retaken when the screens change rather than redrawn by hand.

Each image is a crop of a 1440 x 900 window in Huddle Light, taken at twice the pixel
density, with numbered callouts burned in. The callouts are placed from the live page's
element boxes, so they follow a layout change. The numbers match the numbered legend under
each image in `docs/Huddle.UserGuide.md`; if you add, remove or reorder a callout here,
change that legend in the same commit.

| Image | Shows |
| --- | --- |
| `sidebar.png` | The Activity bar and the sidebar's Chats view, with Grace selected |
| `tab-strip.png` | Two kept tabs (one hovered) and a preview tab |
| `tab-menu.png` | A tab's More (⋮) menu, open |
| `room.png` | The Room with Grace |
| `board-card.png` | PLAT-0002 on the Platform board |
| `build-team.png` | Build a team, step 1 |

## How to run

1. Start the seeded app: `pwsh -NoProfile -File run.ps1 -Seed -Port 1111` (the `huddle-seed`
   entry in `.claude/launch.json`). The app must be in Huddle Light.
2. Install `playwright-core` anywhere outside the repo, for example
   `npm i playwright-core` in a scratch folder, and point `NODE_PATH` at its `node_modules`.
3. Run `node tools/guide-screenshots/capture.js [baseUrl] [outDir]`. The defaults are
   `http://localhost:1111` and `docs/images/user-guide/`.

The script uses the newest Chromium under `%LOCALAPPDATA%\ms-playwright`
(`npx playwright install chromium` puts one there), or the `chrome.exe` named by the
`CHROMIUM` environment variable. It clears the browser's saved tabs before each shot, so
it does not depend on what you had open.
