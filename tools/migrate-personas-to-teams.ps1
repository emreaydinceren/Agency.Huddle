<#
.SYNOPSIS
    One-off migration: reshapes the repo owner's pre-Teams Persona files into the frontmatter
    shape the Teams feature's PersonaIndex requires (Name, Title, Alias, Teams).

.DESCRIPTION
    Every Persona file under the source folder predates the Teams feature and would be REJECTED
    outright by today's index: PersonaFrontmatter.TryReadIdentity requires Title and Alias fields
    that none of these files have, and their existing 'name:' value is really a job title
    ("Chief of Staff", "Agency Code") rather than a Persona's actual Name.

    This script does the mechanical 90% of the fix, one file at a time:
      - The existing 'name:' value is kept AND copied into a new 'title:' field, so both read the
        same job-title text to start with.
      - 'mention:' is dropped. Nothing reads it any more (list_agents and /invite both resolve
        against Name/Alias now), and it was only ever "name, with an @ in front".
      - A new 'alias:' field is inserted, holding a SUGGESTED short handle (lower-cased initials
        for a multi-word title, e.g. "Chief of Staff" -> "cos"; the first three letters for a
        one-word title, e.g. "Finances" -> "fin"), de-collided against every Name and every other
        Alias produced in the same run.
      - A new, empty 'teams: []' field is appended, ready for a human to fill in.
      - Every other frontmatter field, and the entire body, is carried over BYTE-FOR-BYTE: this
        script edits only the three lines it is documented to touch (rename 'name:' into a pair of
        'name:'/'title:' lines, drop 'mention:', append 'teams: []') and never reflows, re-quotes,
        or reorders anything else. These files carry carefully-worded prose that is not this
        script's to rewrite.

    What this script CANNOT do, on purpose:
      - It cannot invent the 12 humans-would-choose Names ("Jarvis" for "Chief of Staff" is a
        branding decision, not a mechanical one) - 'name:' is deliberately left holding the OLD
        value, identical to the new 'title:', as a visible placeholder for a human to replace.
      - It cannot decide Team membership - 'teams: []' is a deliberately empty flow list. Teams is
        an OPTIONAL identity field (PersonaIdentity.Teams), so a file left with 'teams: []' loads
        into the app fine; a file whose Name, Title or Alias is missing or invalid does NOT.
      - It cannot guarantee the suggested Alias is the one a human would actually want - only that
        it is unique and passes the same validation (NameRules.IsValidAgentName) the app itself
        enforces, so the migrated file loads without a human having to fix the Alias first.

    Never modifies anything under the source folder - every file is read there and the reshaped
    copy is written under the destination folder only. Safe to point at a read-only or backed-up
    copy of 'personas/' for a dry run.

.PARAMETER Source
    Folder to read the original Persona '*.md' files from. Defaults to 'personas' next to this
    script's repo root - the repo owner's private, git-ignored library.

.PARAMETER Destination
    Folder to write the reshaped files to. Defaults to 'App_Data/Teams' next to this script's
    repo root - {Team:DataDir}/{Team:Acp:TeamsDir} in the app's own configuration, which is also
    git-ignored and may not exist yet (it is created if missing).

.PARAMETER Force
    Required to proceed when Destination already contains one or more '*.md' files. Without it,
    the script refuses to run rather than risk silently overwriting a human's in-progress edits
    (a real Name choice, a Teams assignment) with a fresh, un-reviewed migration pass.

.EXAMPLE
    pwsh tools/migrate-personas-to-teams.ps1
    Migrates personas/*.md into App_Data/Teams/, refusing if the latter already holds *.md files.

.EXAMPLE
    pwsh tools/migrate-personas-to-teams.ps1 -Source 'C:\temp\personas-copy' -Destination 'C:\temp\teams-out' -Force
    A dry run against a throwaway copy, safe to inspect and re-run.
#>
[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..' 'personas'),
    [string]$Destination = (Join-Path $PSScriptRoot '..' 'App_Data' 'Teams'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Mirrors Agency.Huddle.Contracts.NameRules.IsValidAgentName: opens on a letter or digit, then any
# run of letter/digit/underscore/hyphen with at most one interior space, <=64 chars total. Kept in
# lock-step with that regex by hand (there is no shared source between a .ps1 tool and the C#
# library it isn't allowed to reference) so a suggested Alias can never be one the app would
# itself go on to reject.
$AgentNamePattern = '^[A-Za-z0-9](?:[ ]?[A-Za-z0-9_-])*$'

function Test-ValidAgentName
{
    param([Parameter(Mandatory)][string]$Value)

    return $Value.Length -ge 1 -and $Value.Length -le 64 -and $Value -match $script:AgentNamePattern
}

<#
.SYNOPSIS
    Strips one layer of matching '...' or "..." YAML quoting from a scalar value, unescaping the
    YAML '' escape for an embedded apostrophe. Mirrors PersonaFrontmatter.StripYamlQuotes closely
    enough for the plain scalars these 12 files actually use (no folded/literal block scalars
    appear in a 'name:' value).
#>
function Get-UnquotedScalar
{
    param([Parameter(Mandatory)][string]$Raw)

    $trimmed = $Raw.Trim()
    if ($trimmed.Length -ge 2 -and $trimmed[0] -eq "'" -and $trimmed[-1] -eq "'")
    {
        return $trimmed.Substring(1, $trimmed.Length - 2).Replace("''", "'")
    }

    if ($trimmed.Length -ge 2 -and $trimmed[0] -eq '"' -and $trimmed[-1] -eq '"')
    {
        return $trimmed.Substring(1, $trimmed.Length - 2)
    }

    return $trimmed
}

<#
.SYNOPSIS
    Computes the base (pre-de-collision) suggested Alias for a Persona's old 'name:' value - really
    its job title. Multi-word titles ("Chief of Staff") become the lower-cased initials of every
    word ("cos"); a single-word title ("Finances") becomes its first three letters ("fin"), or the
    whole word if it is shorter than three letters. Non-letter/digit characters (punctuation) never
    become part of a word, so "Agency Code" splits into exactly ["Agency"; "Code"].
#>
function Get-SuggestedAliasBase
{
    param([Parameter(Mandatory)][string]$Title)

    # @(...) forces an array even when the regex has zero or exactly one match: PowerShell
    # unwraps a single pipeline result to a bare scalar, and indexing a bare string with [0]
    # silently returns its first CHARACTER rather than the whole word.
    $words = @([regex]::Matches($Title, '[A-Za-z0-9]+') | ForEach-Object { $_.Value })

    if ($words.Count -ge 2)
    {
        $initials = $words | ForEach-Object { $_.Substring(0, 1) }
        return ($initials -join '').ToLowerInvariant()
    }

    if ($words.Count -eq 1)
    {
        $word = $words[0]
        $length = [Math]::Min(3, $word.Length)
        return $word.Substring(0, $length).ToLowerInvariant()
    }

    # No letters or digits at all in the title (never true of the real 12 files) - fall back to a
    # placeholder base so de-collision below still has something to append digits to.
    return 'persona'
}

<#
.SYNOPSIS
    De-collides a base Alias against every Name and Alias already reserved in this run (compared
    case-insensitively, matching PersonaIndex's own collision rules), by appending an incrementing
    digit suffix until the candidate is free. Reserves the winning candidate before returning it,
    so the very next call sees it too.
#>
function Get-DeCollidedAlias
{
    param(
        [Parameter(Mandatory)][string]$Base,
        [Parameter(Mandatory)][System.Collections.Generic.HashSet[string]]$Reserved
    )

    $candidate = $Base
    $suffix = 2
    while ($Reserved.Contains($candidate))
    {
        $candidate = "$Base$suffix"
        $suffix++
    }

    [void]$Reserved.Add($candidate)
    return $candidate
}

<#
.SYNOPSIS
    Reshapes one Persona file's raw text: renames its 'name:' line into a 'name:'/'title:' pair
    holding the same (old) value, drops 'mention:', inserts the given Alias right after, and
    appends 'teams: []' as the frontmatter's last line. Every other line - every other field, the
    closing '---', and the whole body - passes through unchanged, in its original order, with its
    original line-ending style preserved.
#>
function Convert-PersonaFrontmatter
{
    param(
        [Parameter(Mandatory)][string]$RawText,
        [Parameter(Mandatory)][string]$SuggestedAlias
    )

    $usesCrLf = $RawText.Contains("`r`n")
    $normalized = $RawText.Replace("`r`n", "`n")
    $lines = $normalized -split "`n"

    if ($lines.Count -lt 2 -or $lines[0].Trim() -ne '---')
    {
        throw 'File does not open with a "---" frontmatter delimiter on its first line.'
    }

    $closeIndex = -1
    for ($i = 1; $i -lt $lines.Count; $i++)
    {
        if ($lines[$i].Trim() -eq '---')
        {
            $closeIndex = $i
            break
        }
    }

    if ($closeIndex -lt 0)
    {
        throw 'Frontmatter opening "---" is never closed by a second "---" line.'
    }

    $frontmatterLines = @()
    if ($closeIndex -gt 1)
    {
        $frontmatterLines = $lines[1..($closeIndex - 1)]
    }

    $bodyLines = @()
    if ($closeIndex -lt $lines.Count - 1)
    {
        $bodyLines = $lines[($closeIndex + 1)..($lines.Count - 1)]
    }

    $nameLineIndices = @()
    for ($i = 0; $i -lt $frontmatterLines.Count; $i++)
    {
        if ($frontmatterLines[$i] -match '^(?i)name\s*:')
        {
            $nameLineIndices += $i
        }
    }

    if ($nameLineIndices.Count -eq 0)
    {
        throw 'No top-level "name:" field found in frontmatter.'
    }

    if ($nameLineIndices.Count -gt 1)
    {
        throw 'More than one top-level "name:" field found in frontmatter - refusing to guess which is the real one.'
    }

    $nameLineIndex = $nameLineIndices[0]
    $nameLine = $frontmatterLines[$nameLineIndex]
    $nameValueRaw = $nameLine.Substring($nameLine.IndexOf(':') + 1).Trim()
    $plainName = Get-UnquotedScalar $nameValueRaw

    $newFrontmatter = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $frontmatterLines.Count; $i++)
    {
        $line = $frontmatterLines[$i]

        if ($i -eq $nameLineIndex)
        {
            $newFrontmatter.Add($line)                              # 'name:' - left at its old value; only a human renames it
            $newFrontmatter.Add("title: $nameValueRaw")              # 'title:' - the old value, moved here verbatim
            $newFrontmatter.Add("alias: '$SuggestedAlias'")          # 'alias:' - newly suggested, still to be reviewed
            continue
        }

        if ($line -match '^(?i)mention\s*:')
        {
            continue                                                 # dropped: dead weight, nothing reads it any more
        }

        $newFrontmatter.Add($line)
    }

    $newFrontmatter.Add('teams: []')                                  # for a human to fill in; optional, so this alone still loads

    $newLines = @('---') + $newFrontmatter + @('---') + $bodyLines
    $newText = [string]::Join("`n", $newLines)
    if ($usesCrLf)
    {
        $newText = $newText.Replace("`n", "`r`n")
    }

    return [PSCustomObject]@{
        Text = $newText
        Name = $plainName
    }
}

$sourceItem = Get-Item -LiteralPath $Source -ErrorAction SilentlyContinue
if (-not $sourceItem -or -not $sourceItem.PSIsContainer)
{
    throw "Source folder '$Source' does not exist."
}

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceItem.FullName -Filter '*.md' -File)
if ($sourceFiles.Count -eq 0)
{
    throw "No '*.md' files found under source folder '$($sourceItem.FullName)'."
}

if (Test-Path -LiteralPath $Destination)
{
    $existingMarkdown = Get-ChildItem -LiteralPath $Destination -Filter '*.md' -File -Recurse -ErrorAction SilentlyContinue
    if ($existingMarkdown -and -not $Force)
    {
        throw "Destination '$Destination' already contains $($existingMarkdown.Count) '.md' file(s). " +
            'Pass -Force to proceed anyway (this will overwrite any file whose name collides), or point ' +
            '-Destination at an empty folder.'
    }
}
else
{
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
}

$destinationFull = (Get-Item -LiteralPath $Destination).FullName

# Pass 1: read every file's raw text and its OLD 'name:' value up front, so every Name in the set
# is known (and reserved) before any Alias is suggested - an Alias must not collide with ANY Name
# in the set, not just the one it is being suggested for.
$parsedFiles = @(foreach ($file in $sourceFiles)
{
    $rawText = Get-Content -LiteralPath $file.FullName -Raw
    $probe = Convert-PersonaFrontmatter -RawText $rawText -SuggestedAlias 'placeholder'
    [PSCustomObject]@{
        File    = $file
        RawText = $rawText
        Name    = $probe.Name
    }
})

$reserved = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($parsed in $parsedFiles)
{
    if (-not (Test-ValidAgentName $parsed.Name))
    {
        throw "'$($parsed.Name)' (from $($parsed.File.Name)) is not a valid Persona Name - migration cannot proceed for it."
    }

    if (-not $reserved.Add($parsed.Name))
    {
        throw "Two source files share the Name '$($parsed.Name)' - resolve that collision by hand before migrating."
    }
}

# Pass 2: suggest and de-collide each real Alias (now that every Name is reserved), reshape, and
# write. Ordered by filename so a re-run with the same source folder produces the same Aliases.
$review = @(foreach ($parsed in ($parsedFiles | Sort-Object { $_.File.Name }))
{
    $aliasBase = Get-SuggestedAliasBase $parsed.Name
    $alias = Get-DeCollidedAlias -Base $aliasBase -Reserved $reserved

    if (-not (Test-ValidAgentName $alias))
    {
        throw "Suggested alias '$alias' for '$($parsed.Name)' is not a valid Persona alias - this indicates a bug in Get-SuggestedAliasBase."
    }

    $converted = Convert-PersonaFrontmatter -RawText $parsed.RawText -SuggestedAlias $alias
    $destinationPath = Join-Path $destinationFull $parsed.File.Name

    # -NoNewline: Convert-PersonaFrontmatter's $newText already carries whatever trailing newline
    # (or lack of one) the source file had: Set-Content would otherwise add a second one.
    Set-Content -LiteralPath $destinationPath -Value $converted.Text -NoNewline -Encoding utf8

    [PSCustomObject]@{
        File  = $parsed.File.Name
        Name  = $converted.Name
        Title = $converted.Name
        Alias = $alias
        Teams = 'TODO'
    }
})

Write-Host ''
Write-Host "Migrated $($review.Count) file(s) from '$($sourceItem.FullName)' to '$destinationFull'."
Write-Host 'Review before this Team goes live - see this script''s header comment for what still needs a human:'
Write-Host ''
$review | Format-Table -Property File, Name, Title, Alias, Teams -AutoSize | Out-String | Write-Host
