#!/usr/bin/env bash
# PreToolUse hook: when Claude writes or edits a C# file in this repo, inject a
# one-line reminder that agents/CSharpPrinciples.md is binding.
#
# CLAUDE.md already @-imports agents/CSharpPrinciples.md, so the reminder stays
# short rather than restating the rules. Uses bash builtins only ($(</dev/stdin),
# [[ =~ ]], printf): every external process costs seconds under Git Bash on Windows,
# and this runs before every Write and Edit. $(</dev/stdin) reads the payload in
# blocks without a fork; `read -d ''` would read a large Write payload one byte at a time.
set -uo pipefail

payload=$(</dev/stdin)

# The first "file_path" in the payload is the tool's target; a later one could sit
# inside old_string/new_string.
[[ $payload =~ \"file_path\"[[:space:]]*:[[:space:]]*\"([^\"]*)\" ]] || exit 0
shopt -s nocasematch
[[ ${BASH_REMATCH[1]} =~ \.(cs|csx|razor|cshtml)$ ]] || exit 0

printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":"C# edit: agents/CSharpPrinciples.md (already loaded via CLAUDE.md) is binding - run dotnet build Huddle.slnx before declaring success."}}'
