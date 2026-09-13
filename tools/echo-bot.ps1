<#
.SYNOPSIS
    A minimal pipe client for Team: connects to the named pipe, registers as an
    Agent, prints every envelope it receives, and echoes back any message that
    mentions it.

.DESCRIPTION
    See docs/running-team.md and docs/Team-Specifications.md (Appendix A) for
    the wire protocol this script speaks.

.PARAMETER Name
    The Agent name to register with (default: echo).

.PARAMETER Pipe
    The named pipe to connect to (default: team).

.EXAMPLE
    pwsh tools/echo-bot.ps1 -Name echo
    pwsh tools/echo-bot.ps1 -Name alpha -Pipe team
#>
[CmdletBinding()]
param(
    [string]$Name = 'echo',
    [string]$Pipe = 'team'
)

$ErrorActionPreference = 'Stop'

Write-Host "Connecting to pipe '\\.\pipe\$Pipe' as agent '$Name'..."

$client = [System.IO.Pipes.NamedPipeClientStream]::new(
    '.', $Pipe, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
$client.Connect(5000)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$reader = [System.IO.StreamReader]::new($client, $utf8NoBom)
$writer = [System.IO.StreamWriter]::new($client, $utf8NoBom)
$writer.AutoFlush = $true

function Send-Envelope {
    param([Parameter(Mandatory)]$Envelope)
    $json = $Envelope | ConvertTo-Json -Compress
    $writer.WriteLine($json)
}

Send-Envelope @{
    type        = 'hello'
    version     = 3
    name        = $Name
    description = 'PowerShell echo agent'
}

Write-Host "Sent hello. Listening for messages (Ctrl+C to exit)..."

try {
    while ($true) {
        $line = $reader.ReadLine()
        if ($null -eq $line) {
            Write-Host 'Server closed the connection.'
            break
        }

        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        Write-Host $line

        $envelope = $line | ConvertFrom-Json

        if ($envelope.type -eq 'messagePosted' -and $envelope.mentioned -eq $true) {
            # Strip '@' before quoting so the reply cannot reproduce mentions and re-trigger
            # another agent (Team-Specifications.md §6.8, "Loop safety").
            $quotedText = $envelope.message.text -replace '@', ''
            $replyText = "**{0}:** {1}" -f $Name, $quotedText
            $reply = @{
                type      = 'postMessage'
                version   = 3
                roomId    = $envelope.roomId
                messageId = [guid]::NewGuid().ToString('N')
                text      = $replyText
            }
            Send-Envelope $reply
        }
    }
}
finally {
    $reader.Dispose()
    $writer.Dispose()
    $client.Dispose()
}
