<#
.SYNOPSIS
    Prints one Gitea issue and its comments as plain text.
.DESCRIPTION
    Reads GITEA_ACCESS_TOKEN from the process, then the User scope. Derives the API base
    from `git remote get-url origin`, so no host is written here. Prints the title, state,
    body and every comment. The token and the host are never printed.
.PARAMETER Number
    The issue number.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$Number
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$token = $env:GITEA_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    $token = [Environment]::GetEnvironmentVariable('GITEA_ACCESS_TOKEN', 'User')
}
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'GITEA_ACCESS_TOKEN is not set in the process or the User environment.'
}

$remote = (git remote get-url origin).Trim()
$base = $remote -replace '\.git$', ''
$origin = ($base -split '/')[0..2] -join '/'
$api = "$origin/api/v1/repos/emre/Huddle"
$headers = @{ Authorization = "token $token" }

function Get-Json([string]$Path) {
    try {
        return Invoke-RestMethod -Uri "$api$Path" -Headers $headers -Method Get
    }
    catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        throw "Request for issue #$Number failed (HTTP status $status)."
    }
}

function Hide-Token([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    return $Text.Replace($token, '[token]')
}

$issue = Get-Json "/issues/$Number"
Write-Output (Hide-Token "# #$Number $($issue.title) [$($issue.state)]")
Write-Output ''
Write-Output (Hide-Token $issue.body)

foreach ($comment in @(Get-Json "/issues/$Number/comments")) {
    Write-Output ''
    Write-Output '---'
    Write-Output (Hide-Token "## Comment $($comment.id) ($($comment.created_at))")
    Write-Output ''
    Write-Output (Hide-Token $comment.body)
}
