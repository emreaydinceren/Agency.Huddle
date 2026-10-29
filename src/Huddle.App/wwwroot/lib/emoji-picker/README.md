# Vendored emoji picker

`emoji-picker.bundle.js` is a single, pre-built, minified ES module bundle of the `<emoji-picker>` custom
element, produced from `tools/emoji-picker/` and committed here so the app ships no `node_modules`, needs no
Node.js at runtime and never reaches a CDN. `data.json` is the picker's emoji data, served locally. Both are
used by the avatar editor's label emoji button through `wwwroot/emoji-picker.js`, which imports the bundle
only when the picker is first opened.

## Packages (exact versions, from `tools/emoji-picker/package-lock.json`)

| Package | Version | Licence | Ships |
| --- | --- | --- | --- |
| `emoji-picker-element` | 1.29.1 | Apache-2.0 | `emoji-picker.bundle.js` |
| `emoji-picker-element-data` | 1.8.0 | Apache-2.0 (data from Emojibase, MIT) | `data.json` (`en/emojibase/data.json`) |

`esbuild` 0.28.2 is a build-time-only `devDependency`. `emoji-picker-element` has no runtime dependencies.
Both packages were published more than two weeks before they were pinned. `LICENSE` holds the notice and the
Apache-2.0 text.

## Sizes (2026-10-29 build)

| File | Raw | Gzip (level 9) | Brotli (quality 11) |
| --- | --- | --- | --- |
| `emoji-picker.bundle.js` | 38,202 bytes (37.3 KB) | 13,715 bytes | 12,343 bytes |
| `data.json` | 439,662 bytes (429.4 KB) | 72,049 bytes | 54,614 bytes |

SHA-256 of the files as committed (CRLF line endings, see below): bundle
`ba991a77b0fd629d767e22162b7b7c883ecb4b8a10d5e1647f4d6794167dfb65`, data
`e5408f6c06fda053316e98ee811aae2774876809d409bb5f2d9186e60b48e0c3`.

## No CDN

`emoji-picker-element` falls back to `https://cdn.jsdelivr.net/npm/emoji-picker-element-data@^1/...` when it is
given no data source, and starts fetching as soon as the element exists. `tools/emoji-picker/build.mjs`
therefore replaces that default with `/lib/emoji-picker/data.json` wherever it appears in the package, and
throws if the package stops holding the string, so a new version cannot slip a CDN back in unnoticed. The
shipped bundle contains no `jsdelivr` text (`grep -c jsdelivr emoji-picker.bundle.js` prints `0`). The element
in `AvatarEditor.razor` also sets `data-source` to the same local URL. The app is served from the site root, as
`/agent-avatars` is, so the absolute path is right.

## Rebuilding

```powershell
pwsh -NoProfile -File tools/emoji-picker/build.ps1
```

This runs `npm ci` against the committed `package-lock.json` (exact, locked versions; never `npm install`) and
then `node build.mjs`, which bundles `entry.js` with esbuild, applies the substitution above and copies
`data.json` and the licence here. The output is deterministic. The build writes CRLF line endings so the files
pass `agents/scripts/Check-Eol.ps1`; a CRLF inside a JavaScript template literal is read back as LF and inside
JSON it is whitespace, so behaviour is unchanged. This folder is allowlisted in `.gitleaks.toml`.
