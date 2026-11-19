// Copies a Css-Snapshot folder with CSS classes renamed inside its element paths, so a Report across a class rename compares
// the renamed elements property by property instead of listing each one as removed and added (which hides every real change).
//
// Usage: node agents/scripts/css-snapshot-rename.mjs <from> <to> <old=new> [<old=new> ...]
//   <old=new>    rename class old to new; `old=` drops it; `old=a+b` replaces it by two classes.
// Each path segment is `tag.class.class:n` with the classes sorted, as css-snapshot.mjs writes it, so a renamed segment is
// sorted again. Run it on the baseline, then Report the rewritten copy against the candidate. ComponentRollout.md has the
// B1 example.
import { readdirSync, readFileSync, writeFileSync, mkdirSync, copyFileSync } from 'node:fs';
import { join } from 'node:path';
import { gunzipSync, gzipSync } from 'node:zlib';

const [from, to, ...pairs] = process.argv.slice(2);
if (!from || !to || pairs.length === 0) {
  console.error('usage: node css-snapshot-rename.mjs <from> <to> <old=new> [<old=new> ...]');
  process.exit(2);
}
const map = new Map(pairs.map(pair => {
  const [old, replacement = ''] = pair.split('=');
  return [old, replacement.split('+').filter(Boolean)];
}));
const segment = /^([a-z][a-z0-9-]*)((?:\.[^.:\s]+)*):(\d+)$/;
let renamed = 0;
const fix = value => {
  const match = typeof value === 'string' ? segment.exec(value) : null;
  if (!match || !match[2]) { return value; }
  const classes = match[2].slice(1).split('.');
  if (!classes.some(c => map.has(c))) { return value; }
  const result = [...new Set(classes.flatMap(c => map.get(c) ?? [c]))].sort();
  renamed++;
  return `${match[1]}${result.length > 0 ? '.' + result.join('.') : ''}:${match[3]}`;
};
const walk = value => Array.isArray(value)
  ? value.map(walk)
  : value && typeof value === 'object' ? Object.fromEntries(Object.entries(value).map(([k, v]) => [fix(k), walk(v)])) : fix(value);
mkdirSync(to, { recursive: true });
for (const name of readdirSync(from)) {
  if (name.endsWith('.json.gz')) {
    const data = JSON.parse(gunzipSync(readFileSync(join(from, name))).toString('utf8'));
    writeFileSync(join(to, name), gzipSync(JSON.stringify(walk(data))));
  } else {
    copyFileSync(join(from, name), join(to, name));
  }
}
console.log(`renamed ${renamed} path segments into ${to}`);
