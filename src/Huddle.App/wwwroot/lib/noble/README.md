# Vendored noble crypto

The `/auth` connect page (`wwwroot/auth.js`) signs the device handshake with Ed25519 in plain JavaScript, so it works
on a plain-HTTP LAN page where `crypto.subtle` does not exist. These files are the unmodified ES-module builds of two
MIT-licensed packages by Paul Miller, committed here so the app ships no `node_modules` and never reaches a CDN.

| Package | Version | Source | Files kept |
| --- | --- | --- | --- |
| `@noble/ed25519` | 2.3.0 | `https://registry.npmjs.org/@noble/ed25519/-/ed25519-2.3.0.tgz` | `ed25519/index.js`, `ed25519/LICENSE` |
| `@noble/hashes` | 1.8.0 | `https://registry.npmjs.org/@noble/hashes/-/hashes-1.8.0.tgz` | `hashes/{sha2,_md,_u64,utils,crypto}.js` (from `esm/`), `hashes/LICENSE` |

`hashes/utils.js` imports the bare specifier `@noble/hashes/crypto`; the connect page maps it to `hashes/crypto.js` with an
import map, so no vendored file is edited. `@noble/ed25519` needs a SHA-512 supplied by the caller (`etc.sha512Sync` and
`etc.sha512Async`); `auth.js` supplies it from `hashes/sha2.js`. To upgrade, replace the files from a newer tarball of
the same major version and run the manual connect tests.
