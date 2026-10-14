#!/usr/bin/env bash
# PreToolUse hook (Write, Edit): refuses to edit a test file that agents/scripts/Freeze-Tests.ps1
# froze after the delivery manager approved its red. A failing test is the spec: an implementer
# who cannot get one green must stop and report, not weaken it.
#
# The freeze lives in the worktree's own git directory (<git-dir>/frozen-tests/manifest.txt),
# one key per line: the absolute path, lower case, forward slashes, "e:/..." drive form. A
# worktree with no manifest, or a path not in it, passes untouched, so this costs one git call
# on every Write/Edit and nothing else. It cannot see a shell redirect or a script that rewrites
# a file; Freeze-Tests.ps1 -Check is the backstop for those.
# Uses grep, sed and perl rather than jq (jq is not installed).
set -uo pipefail

payload=$(cat)

path=$(printf '%s' "$payload" \
    | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' \
    | head -n 1 \
    | sed -E 's/^"file_path"[[:space:]]*:[[:space:]]*"//; s/"$//; s/\\\\/\\/g')

[ -n "$path" ] || exit 0

# Same key Freeze-Tests.ps1 writes: backslashes to slashes, /e/... to e:/..., lower case.
if command -v cygpath >/dev/null 2>&1; then
    path=$(cygpath -m "$path" 2>/dev/null || printf '%s' "$path")
fi
key=$(printf '%s' "$path" | sed -E 's#\\#/#g; s#^/([A-Za-z])/#\1:/#' | tr '[:upper:]' '[:lower:]')

# Walk up to a directory that exists: a file being created has no folder yet, and is never frozen.
dir=$(dirname "$path")
while [ ! -d "$dir" ] && [ "$dir" != "/" ] && [ "$dir" != "." ]; do
    dir=$(dirname "$dir")
done
[ -d "$dir" ] || exit 0

gitdir=$(git -C "$dir" rev-parse --absolute-git-dir 2>/dev/null) || exit 0
manifest="$gitdir/frozen-tests/manifest.txt"
[ -f "$manifest" ] || exit 0

if tr -d '\r' < "$manifest" | grep -Fxq -- "$key"; then
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' \
        "Blocked: this test file was frozen when its red was approved. A failing test is the spec - fix the code under test, not the test. If you believe the test itself is wrong, stop and report it as a test change request with the failing line and your evidence; the delivery manager decides and runs Freeze-Tests.ps1 -Release."
    exit 0
fi

exit 0
