<#
.SYNOPSIS
    Rebuilds the vendored emoji-picker-element bundle and its offline emoji data for the avatar editor.

.DESCRIPTION
    Installs the exact, locked dependency versions with `npm ci`, then runs build.mjs, which bundles entry.js with
    esbuild into src/Huddle.App/wwwroot/lib/emoji-picker/emoji-picker.bundle.js, replaces the package's CDN default
    data URL with the local one, and copies data.json and the licenses beside the bundle. Run from any working
    directory; paths are resolved relative to this script.

.EXAMPLE
    pwsh -NoProfile -File tools/emoji-picker/build.ps1
#>
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    npm ci
    node build.mjs
}
finally {
    Pop-Location
}
