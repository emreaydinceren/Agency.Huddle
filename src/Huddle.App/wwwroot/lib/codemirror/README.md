# Vendored CodeMirror 6 bundle

`codemirror.bundle.js` is a single, pre-built, minified ES module bundle produced at build time
from `tools/codemirror/` and committed here so the app ships no `node_modules` and needs no
Node.js at runtime (Library task 11.1). It is consumed by `library-editor.js` (task 11.2) as a
relative-URL ES module import.

## Packages (exact versions, from `tools/codemirror/package-lock.json`)

Direct dependencies of `tools/codemirror/entry.js`:

| Package | Version |
| --- | --- |
| `@codemirror/state` | 6.7.6 |
| `@codemirror/view` | 6.43.13 |
| `@codemirror/commands` | 6.11.1 |
| `@codemirror/language` | 6.12.4 |
| `@codemirror/lang-markdown` | 6.5.2 |
| `@codemirror/lang-json` | 6.0.2 |
| `@codemirror/lang-javascript` | 6.2.5 |
| `@codemirror/lang-css` | 6.3.1 |
| `@codemirror/lang-xml` | 6.1.0 |
| `@codemirror/legacy-modes` | 6.5.4 |

`esbuild` 0.28.2 is a build-time-only `devDependency`; none of its code ships in the bundle.

`@codemirror/lang-yaml` (6.1.3) was evaluated per corrections-B6 item 12 and found far larger
than the legacy YAML mode (a lezer-based parser vs. a small `StreamParser`: ~277 KB raw / ~80 KB
brotli standalone, against ~1.5 KB raw / ~0.6 KB brotli for the legacy mode). It is **not** a
dependency; YAML uses `@codemirror/legacy-modes/mode/yaml` instead, alongside the C#
(`mode/clike`, exported as `csharp`) and PowerShell (`mode/powershell`) legacy modes.

Transitive packages that end up in the bundle (per esbuild's metafile), all MIT — see `LICENSE`:
`@codemirror/autocomplete` 6.20.3, `@codemirror/lang-html` 6.4.12, `@lezer/common` 1.5.3,
`@lezer/css` 1.3.8, `@lezer/highlight` 1.2.4, `@lezer/html` 1.3.13, `@lezer/javascript` 1.5.5,
`@lezer/json` 1.0.3, `@lezer/lr` 1.4.10, `@lezer/markdown` 1.7.2, `@lezer/xml` 1.0.6,
`@marijn/find-cluster-break` 1.0.4, `crelt` 1.0.7, `style-mod` 4.1.4, `w3c-keyname` 2.2.8.

## Bundle size (2026-09-27 build)

| Measure | Size |
| --- | --- |
| Raw (minified) | 561,847 bytes (548.7 KB) |
| Gzip (level 9) | 194,926 bytes (190.4 KB) |
| Brotli (quality 11) | 165,179 bytes (161.3 KB) |

The acceptance target (corrections-B6 item 12) is on the **compressed** size: ≤ 250 KB brotli.
165.3 KB brotli is within budget. The bundle was also confirmed byte-for-byte identical across
two consecutive `build.ps1` runs (SHA-256
`f9c0ff34178b8851f4a79782036299711c1e247138aae715d65407d50b7a3189`).

## Rebuilding

```powershell
pwsh -NoProfile -File tools/codemirror/build.ps1
```

This runs `npm ci` against the committed `tools/codemirror/package-lock.json` (exact, locked
versions — never `npm install`) and then `npx esbuild entry.js --bundle --format=esm --minify`,
writing the result to this folder as `codemirror.bundle.js`. The output is deterministic: two
consecutive runs produce byte-identical files. `codemirror.bundle.js`,
`tools/codemirror/package-lock.json` and this whole `wwwroot/lib/codemirror/` folder are excluded
from `Conversation/scripts/Check-Eol.ps1`'s CRLF check and are allowlisted in `.gitleaks.toml`, so
the bundle stays exactly what the build produced (LF, minified, no secret-scan false positives).

`entry.js` re-exports only what the Library editor (11.2) needs: `EditorState`, `Compartment`,
`EditorView`, `keymap`, `lineNumbers`, `defaultKeymap`, `history`, `historyKeymap`,
`LanguageSupport`, `StreamLanguage`, and the language modes `markdown`, `json`, `javascript`,
`css`, `xml`, `yaml` (legacy), `csharp` (legacy) and `powerShell` (legacy).
