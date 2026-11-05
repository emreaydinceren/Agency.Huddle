<#
.SYNOPSIS
    Prints the SHA-256 of every shipped version of src/Huddle.App/Builtin/chief-of-staff.md.

.DESCRIPTION
    One line per distinct version, oldest first: "<hash>  # <commit or label>". The output is the
    content of src/Huddle.App/Builtin/chief-of-staff.hashes.txt (the file BuiltinAgentVersions reads).
    The hash is taken the way BuiltinAgentVersions.Normalise does it: line endings to LF, the
    name: and alias: lines of the front matter removed, trailing white space trimmed.
    The last line is the working-tree file, i.e. the version this build ships.

    When the default text changes, run this with -Write and commit the result. Earlier lines never
    change, so the previous version stays in the list; the guard test fails until you do.

.PARAMETER Write
    Append the current version's hash to the hashes file when it is not listed yet; existing lines
    are never rewritten. Without -Write the full list is printed.
#>
[CmdletBinding()]
param([switch]$Write)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$relative = 'src/Huddle.App/Builtin/chief-of-staff.md'
$listPath = Join-Path $root 'src/Huddle.App/Builtin/chief-of-staff.hashes.txt'

function Get-NormalisedHash([string]$text) {
    $lines = ($text -replace "`r`n", "`n" -replace "`r", "`n") -split "`n"
    $delimiters = 0
    $kept = foreach ($line in $lines) {
        if ($line.TrimEnd() -eq '---' -and $delimiters -lt 2) { $delimiters++ }
        elseif ($delimiters -eq 1 -and ($line -match '^(?i)(name|alias):')) { continue }
        $line
    }
    $normalised = (($kept -join "`n") + "`n").TrimEnd()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalised)
    -join ([System.Security.Cryptography.SHA256]::HashData($bytes) | ForEach-Object { $_.ToString('x2') })
}

$entries = [System.Collections.Generic.List[object]]::new()
$commits = @(git -C $root log --format=%h --follow -- $relative)
[array]::Reverse($commits)
foreach ($commit in $commits) {
    # Raw bytes via cmd's redirect, so the console's code page cannot change a hash. git show has no
    # --output for a blob: it prints to stdout and leaves the file empty.
    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        cmd /c "git -C `"$root`" show `"${commit}:$relative`" > `"$tempFile`""
        if ($LASTEXITCODE -ne 0) { throw "git show failed for $commit" }
        $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($tempFile))
    } finally {
        Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
    }
    $entries.Add([pscustomobject]@{ Hash = Get-NormalisedHash $text; Label = $commit })
}
$current = [System.IO.File]::ReadAllText((Join-Path $root $relative))
$entries.Add([pscustomobject]@{ Hash = Get-NormalisedHash $current; Label = 'working tree (current)' })

# Keep the first appearance of each hash; a later commit that returned to an old text adds nothing.
$seen = [System.Collections.Generic.HashSet[string]]::new()
$out = foreach ($e in $entries) {
    if ($seen.Add($e.Hash)) { "$($e.Hash)  # $($e.Label)" }
}

if ($Write) {
    # Existing lines are never rewritten: only a hash not yet listed is appended.
    $existing = [System.IO.File]::ReadAllText($listPath)
    $currentHash = $entries[$entries.Count - 1].Hash
    $listed = $existing -split "`r?`n" | ForEach-Object { ($_ -split '\s+')[0] }
    if ($listed -contains $currentHash) {
        Write-Host "The current version is already listed; $listPath left as it is."
    } else {
        if (-not $existing.EndsWith("`n")) { $existing += "`r`n" }
        [System.IO.File]::WriteAllText($listPath, $existing + "$currentHash  # $(git -C $root rev-parse --short HEAD) + working tree`r`n")
        Write-Host "Appended $currentHash to $listPath"
    }
} else {
    $out
}
