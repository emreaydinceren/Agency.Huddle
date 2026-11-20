# Tour screenshots

`capture.js` takes the product tour's pictures in `tour/screens/` from a running seeded app, so
they can be retaken when the screens change. Each picture is the whole 1440 x 900 window, taken
twice: in Nordic Light and in Cobalt Soft Dark, saved as `<shot>--nordic-light.png` and
`<shot>--cobalt-soft-dark.png`. `TOUR.md` names each shot without the Theme suffix.

The pictures are published on GitHub, so take them only from a seed root whose path holds no
user name (the default, `C:\seeds\tour`), and look at every one before committing it.

## How to run

1. Build a seed into its own root:
   `dotnet run --project src/Huddle.Seeder -- --root C:\seeds\tour`.
2. Start the app on it:
   `pwsh -NoProfile -File run.ps1 -Seed -SeedRoot C:\seeds\tour -NoAcp -NoBrowser -Port 5311`.
3. Install `playwright-core` anywhere outside the repo, for example `npm i playwright-core` in a
   scratch folder, and point `NODE_PATH` at its `node_modules`.
4. Run `node tools/tour-screenshots/capture.js [baseUrl] [outDir] [seedRoot]`. The defaults are
   `http://localhost:5311`, `tour/screens/` and `C:\seeds\tour`.

The script switches the Theme by writing the seed's `data/appearance.json`, waits until the page
renders it, and puts the file back when it is done. It reads each Team Room's id from the seed's
`manifest.md`, because ids are minted on every seed run. It finds Chromium the same way as
`tools/guide-screenshots/capture.js`.
