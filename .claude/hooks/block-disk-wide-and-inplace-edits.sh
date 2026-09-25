#!/usr/bin/env bash
# PreToolUse hook (Bash, PowerShell): refuses two commands that have no legitimate use in this
# repo and have caused real damage:
#   - `find /` (or `find /c`, `/e` ...): on Windows, Git Bash's `/` mounts every drive, so it
#     crawls the whole disk and outlives the tool call as an orphaned find.exe.
#   - `sed -i` / `--in-place`: rewrites the file with LF endings in this CRLF working copy.
# Uses perl rather than jq (jq is not installed). Everything else passes through untouched.
# Regression cases: block-disk-wide-and-inplace-edits.cases.txt beside this file, one
# "BLOCK|allow|name|payload" per line; pipe each payload in and compare with the first field.
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

exit 0
