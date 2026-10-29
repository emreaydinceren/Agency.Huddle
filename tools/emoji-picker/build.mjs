// Bundles entry.js into one minified ES module and copies the offline emoji data beside it.
//
// emoji-picker-element falls back to a jsDelivr URL when no data source is given, and builds its database (and so fetches)
// as soon as the element exists. A local-first app must never reach a CDN, so this build replaces that default with the local
// data file's URL wherever it appears. After the build the bundle holds no CDN address at all.
import { build } from "esbuild";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const outDir = resolve(here, "../../src/Huddle.App/wwwroot/lib/emoji-picker");
const cdnDefault = "https://cdn.jsdelivr.net/npm/emoji-picker-element-data@^1/en/emojibase/data.json";
const localData = "/lib/emoji-picker/data.json";

const useLocalData = {
  name: "use-local-emoji-data",
  setup(b) {
    b.onLoad({ filter: /emoji-picker-element.(database|picker)\.js$/ }, (args) => {
      const text = readFileSync(args.path, "utf8");
      if (!text.includes(cdnDefault)) {
        throw new Error(`${args.path} no longer holds the default data URL; review the substitution.`);
      }
      return { contents: text.replaceAll(cdnDefault, localData), loader: "js" };
    });
  },
};

// The repo's text files are CRLF (Check-Eol). A CRLF in a JS template literal is read back as LF (ECMAScript normalises it),
// and in JSON it is whitespace, so converting the outputs changes no behaviour.
const crlf = (text) => text.replace(/\r?\n/g, "\r\n");
const writeCrlf = (path, text) => writeFileSync(path, crlf(text));

mkdirSync(outDir, { recursive: true });
await build({
  entryPoints: [resolve(here, "entry.js")],
  bundle: true,
  format: "esm",
  minify: true,
  legalComments: "none",
  plugins: [useLocalData],
  write: false,
  outfile: resolve(outDir, "emoji-picker.bundle.js"),
}).then((result) => {
  for (const file of result.outputFiles) {
    writeCrlf(file.path, file.text);
  }
});
writeCrlf(resolve(outDir, "data.json"), readFileSync(resolve(here, "node_modules/emoji-picker-element-data/en/emojibase/data.json"), "utf8"));

// Both packages ship the same Apache-2.0 text; one LICENSE names them and holds it once.
const licenseOf = (name) => readFileSync(resolve(here, "node_modules", name, "LICENSE"), "utf8").replace(/\r\n/g, "\n");
const licenseText = licenseOf("emoji-picker-element");
if (licenseText !== licenseOf("emoji-picker-element-data")) {
  throw new Error("The two packages' licenses differ; list both in LICENSE.");
}
const pkg = (name) => JSON.parse(readFileSync(resolve(here, "node_modules", name, "package.json"), "utf8")).version;
const header = `Third-party notices for the vendored emoji picker (emoji-picker.bundle.js, data.json)
=============================================================================

emoji-picker-element ${pkg("emoji-picker-element")} and emoji-picker-element-data ${pkg("emoji-picker-element-data")}, both by Nolan Lawson,
both Apache-2.0. tools/emoji-picker/package.json and package-lock.json pin the exact versions. The
emoji names, keywords and groups in data.json come from Emojibase (MIT) through emoji-picker-element-data.
The Apache-2.0 text, as the packages ship it:

`;
writeCrlf(resolve(outDir, "LICENSE"), header + licenseText);
