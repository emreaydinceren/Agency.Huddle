#!/usr/bin/env bash
# PostToolUse hook: the Write tool creates files with LF endings, but this repo's
# working copy is CRLF (.editorconfig). Rewrites the file just written to CRLF for
# text types. Edit preserves a file's existing endings, so only Write is hooked.
#
# Git already normalises on commit (core.autocrlf=true in Git for Windows' system
# config, and every index blob is LF), so this is working-copy hygiene: no mixed
# endings, no "LF will be replaced by CRLF" warnings. Uses grep and perl rather
# than jq or python (jq is not installed; python3 rewrites line endings itself).
set -uo pipefail

payload=$(cat)

# The payload is JSON: Windows paths arrive with escaped backslashes (E:\\Repos\\...).
path=$(printf '%s' "$payload" \
    | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' \
    | head -n 1 \
    | sed -E 's/^"file_path"[[:space:]]*:[[:space:]]*"//; s/"$//; s/\\\\/\\/g')

[ -n "$path" ] && [ -f "$path" ] || exit 0

case "${path,,}" in
    *.sh) exit 0 ;;  # bash rejects CRLF scripts; they stay LF
    *.cs|*.csx|*.razor|*.cshtml|*.csproj|*.props|*.targets|*.slnx|*.md|*.json|*.css|*.js|*.ps1|*.yaml|*.yml|*.txt|*.toml) ;;
    *) exit 0 ;;
esac

# Nothing to do when there is no bare LF (already CRLF, or no newlines at all).
perl -ne 'exit 1 if /(?<!\r)\n/' "$path" && exit 0

# Idempotent; keeps a BOM; never adds a final newline the file did not have.
perl -pi -e 's/\r?\n/\r\n/' "$path" || exit 0

printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"The CRLF hook converted %s to CRLF on disk after this Write. Re-Read it before your next Edit of it."}}\n' \
    "$(basename "$path")"
