#!/usr/bin/env bash
# Runs the regression cases beside each PreToolUse hook: .claude/hooks/<hook>.cases.txt holds one
# "BLOCK|allow|name|payload" per line (blank lines and lines starting with # are skipped). Each
# payload is piped into <hook>.sh and the hook's answer is compared with the first field: BLOCK
# means the output holds a "deny" decision, allow means it does not.
#
# {TMP} in a payload is replaced by a scratch directory that holds a fake worktree: a .git folder
# whose frozen-tests/manifest.txt freezes {TMP}/tests/Frozen.cs, so a frozen-path case can run.
#
# Usage: bash agents/scripts/Test-HookCases.sh [path/to/hook.cases.txt ...]
# With no argument every .claude/hooks/*.cases.txt runs. Exit 0 when every case matches, 1 otherwise.
set -uo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
if [[ $# -gt 0 ]]; then
    files=("$@")
else
    files=("$root"/.claude/hooks/*.cases.txt)
fi

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
tmpm=$(cygpath -m "$tmp" 2>/dev/null || printf '%s' "$tmp")
mkdir -p "$tmp/.git/frozen-tests" "$tmp/tests"
printf '%s\n' "${tmpm,,}/tests/frozen.cs" > "$tmp/.git/frozen-tests/manifest.txt"

pass=0
fail=0
for cases in "${files[@]}"; do
    hook=${cases%.cases.txt}.sh
    while IFS= read -r line || [[ -n $line ]]; do
        line=${line%$'\r'}
        [[ -z $line || $line == \#* ]] && continue
        expect=${line%%|*}
        rest=${line#*|}
        name=${rest%%|*}
        payload=${rest#*|}
        payload=${payload//'{TMP}'/$tmpm}
        out=$(printf '%s' "$payload" | bash "$hook")
        if [[ $out == *'"permissionDecision":"deny"'* ]]; then got=BLOCK; else got=allow; fi
        if [[ $got == "$expect" ]]; then
            pass=$((pass + 1))
        else
            fail=$((fail + 1))
            printf 'FAIL %s: %s (expected %s, got %s)\n' "$(basename "$hook")" "$name" "$expect" "$got"
        fi
    done < "$cases"
done

printf '%d passed, %d failed\n' "$pass" "$fail"
[[ $fail -eq 0 ]]
