<#
.SYNOPSIS
    Rebuilds the vendored CodeMirror 6 bundle used by the Library editor.

.DESCRIPTION
    Installs the exact, locked dependency versions with `npm ci`, then bundles entry.js with
    esbuild into a single minified ES module at
    src/Huddle.App/wwwroot/lib/codemirror/codemirror.bundle.js. Run from any working directory;
    paths are resolved relative to this script.

.EXAMPLE
    pwsh -NoProfile -File tools/codemirror/build.ps1
#>
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    npm ci
    npx esbuild entry.js --bundle --format=esm --minify --outfile=../../src/Huddle.App/wwwroot/lib/codemirror/codemirror.bundle.js
}
finally {
    Pop-Location
}
