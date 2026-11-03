#!/usr/bin/env bash
# PreToolUse hook (Bash, PowerShell): refuses two commands that have no legitimate use in this
# repo and have caused real damage:
#   - `find /` (or `find /c`, `/e` ...): on Windows, Git Bash's `/` mounts every drive, so it
#     crawls the whole disk and outlives the tool call as an orphaned find.exe.
#   - `sed -i` / `--in-place`: rewrites the file with LF endings in this CRLF working copy.
# It also refuses shell writes that go around the Edit hooks when the target is under src/ or
# tests/: cat > / >>, File.WriteAll*/AppendAll*, Set-Content, Add-Content, Out-File, and a script
# file run from /tmp.
# Uses perl rather than jq (jq is not installed). Everything else passes through untouched.
# Regression cases: block-disk-wide-and-inplace-edits.cases.txt beside this file, one
# "BLOCK|allow|name|payload" per line; run them with agents/scripts/Test-HookCases.sh.
set -uo pipefail

payload=$(cat)

deny() {
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$1"
    exit 0
}

# `find` rooted at / or a bare drive mount such as /c or /e (a path like /e/Repos/Huddle/src is fine).
# Both rules match only in a command position (start of the command string, after ; & | ( or an
# escaped newline, or as the last segment of a path such as ...\find.exe), so prose that merely
# mentions the command - a commit message, an echo - passes.
# Disk-wide roots: /, a drive (/c), a drive's Users folder or one user's home (/c/Users/me), ~,
# $HOME, or C:\ (JSON-escaped as C:\\). Deeper paths such as ~/.nuget/packages/x or the repo pass:
# the root must be followed by whitespace, a quote (possibly JSON-escaped) or the end, so
# "E:\Repos\..." is not mistaken for the bare drive.
if printf '%s' "$payload" | perl -0777 -ne 'exit(m{(?:\x22command\x22\s*:\s*\x22|[;&|(]\s*|\\n\s*|[\\/])find(?:\.exe)?[\x22\x27\\]*\s+[\x22\x27\\]*(?:/(?:[a-zA-Z](?:/Users(?:/[^/\s\x22\x27\\]+)?)?)?|~|\$HOME|[A-Za-z]:(?:\\\\|/)?(?:Users(?:(?:\\\\|/)[^\\/\s\x22\x27]+)?)?)/?(?=[\s\x22\x27]|\\+[\x22\x27]|\\[nrt]|$)} ? 0 : 1)' ; then
    deny "Blocked: find / searches every drive and leaves an orphaned find.exe. Search the repo instead (src/, tests/, docs/) with the Grep or Glob tool; NuGet package docs are under %USERPROFILE%\\\\.nuget\\\\packages\\\\<id>\\\\<version>\\\\."
fi

# In-place sed.
if printf '%s' "$payload" | perl -0777 -ne 'exit(m{(?:\x22command\x22\s*:\s*\x22|[;&|(]\s*|\\n\s*)sed\b[^|;&\n]*?\s(?:-[a-zA-Z]*i\b|--in-place)} ? 0 : 1)' ; then
    deny "Blocked: sed -i rewrites the file with LF endings in this CRLF repo. Use the Edit tool (replace_all: true for a rename)."
fi

# File writes that go around the Edit/Write hooks (frozen tests, CRLF repair, C# principles).
# Conservative on purpose: a command is refused only when the target visibly sits under src/ or
# tests/ (a segment of that name, so scratchpad paths and mysrc/ pass), or when it runs a script
# file from /tmp, whose content this hook cannot see. The patterns are handed to perl through the
# environment so they can be composed here.
pos='(?:\x22command\x22\s*:\s*\x22|[;&|(]\s*|\\n\s*)'
posc='(?:\x22command\x22\s*:\s*\x22|[;&|({]\s*|\\n\s*|-Command\s+[\x22\x27\\]*)'
under='(?<=[/\\\x22\x27\s=(])(?:src|tests)(?:/|\\+)'
same='(?:(?!\\n)[^|;&])*?'

matches() {
    PAT="$1" perl -0777 -ne 'exit(/$ENV{PAT}/ ? 0 : 1)' <<<"$payload"
}

if matches "${pos}cat\\b${same}>>?\\s*[\\x22\\x27\\\\]*(?:[^\\s\\x22\\x27|;&<>]*[/\\\\])?(?:src|tests)(?:/|\\\\+)"; then
    deny "Blocked: cat > into src/ or tests/ skips the Edit hooks and writes LF endings. Use the Edit or Write tool."
fi
if matches "${pos}(?:printf|echo)\\b${same}(?<![0-9&])>>?\\s*[\\x22\\x27\\\\]*(?:[^\\s\\x22\\x27|;&<>]*[/\\\\])?(?:src|tests)(?:/|\\\\+)"; then
    deny "Blocked: printf/echo redirected into src/ or tests/ skips the Edit hooks. Use the Edit or Write tool."
fi
if matches "${pos}(?:[^|;&]*\\|\\s*)?tee\\b${same}\\s[\\x22\\x27\\\\]*(?:[^\\s\\x22\\x27|;&<>]*[/\\\\])?(?:src|tests)(?:/|\\\\+)"; then
    deny "Blocked: tee into src/ or tests/ skips the Edit hooks. Use the Edit or Write tool."
fi
if matches "(?i)File\\]::(?:WriteAll|AppendAll)\\w*\\(${same}${under}"; then
    deny "Blocked: File.WriteAll/AppendAll into src/ or tests/ skips the Edit hooks. Use the Edit or Write tool."
fi
if matches "(?i)${posc}(?:Set-Content|Add-Content|Out-File)\\b${same}${under}"; then
    deny "Blocked: Set-Content, Add-Content and Out-File into src/ or tests/ skip the Edit hooks. Use the Edit or Write tool."
fi
if matches "${pos}truncate\\b${same}${under}"; then
    deny "Blocked: truncate on a file under src/ or tests/ skips the Edit hooks. Use the Edit or Write tool; to undo stub residue run agents/scripts/Remove-Stubs.ps1."
fi
if matches "(?i)${pos}(?:(?:pwsh|powershell)(?:\\.exe)?\\b${same}\\s-File\\s+|(?:bash|sh|source|\\.)\\s+)[\\x22\\x27\\\\]*/tmp/"; then
    deny "Blocked: a script run from /tmp can rewrite src/ or tests/ around the Edit hooks. Use the Edit or Write tool, or keep the script in the repo agents/scripts folder."
fi

exit 0
