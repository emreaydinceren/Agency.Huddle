<#
.SYNOPSIS
    Regenerates src/Huddle.App/prompts.default.json from PromptCatalog.All, then runs the
    PromptDefaultsFileTests to confirm the result matches what the app itself computes.

.DESCRIPTION
    Runs the throwaway helper project at agents/scripts/RegeneratePromptDefaults (not part of
    Huddle.slnx - see that folder's .csproj for why it is excluded) via `dotnet run`, which serialises
    PromptCatalog.All (key -> Default) using the same indented JsonSerializerOptions
    PromptStore.IndentedJsonOptions uses, normalises line endings, and overwrites prompts.default.json in
    place. It then calls Run-Tests.ps1 (resolved next to this script via $PSScriptRoot) filtered to
    *PromptDefaultsFileTests to prove the file still round-trips against the catalog.

.EXAMPLE
    pwsh agents/scripts/Regenerate-PromptDefaults.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$HelperProject = Join-Path $ScriptDir 'RegeneratePromptDefaults\RegeneratePromptDefaults.csproj'

$WorktreeRoot = (git rev-parse --show-toplevel).Trim()
Write-Host "Building and running $HelperProject against $WorktreeRoot ..."
# The helper reference and output follow the caller's worktree, not this script's location.
& dotnet run --project $HelperProject "-p:HuddleRoot=$WorktreeRoot" -- $WorktreeRoot
if ($LASTEXITCODE -ne 0) {
    throw "RegeneratePromptDefaults helper exited with code $LASTEXITCODE."
}

$RunTests = Join-Path $ScriptDir 'Run-Tests.ps1'
& $RunTests -FilterClass '*PromptDefaultsFileTests' -Label 'prompt-defaults'
exit $LASTEXITCODE
