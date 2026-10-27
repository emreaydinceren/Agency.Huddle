#!/usr/bin/env bash
# PreToolUse hook (Write, Edit): refuses to edit a test file that agents/scripts/Freeze-Tests.ps1
# froze after the delivery manager approved its red. A failing test is the spec: an implementer
# who cannot get one green must stop and report, not weaken it.
#
# The freeze lives in the worktree's own git directory (<git-dir>/frozen-tests/manifest.txt),
# one key per line: the absolute path, lower case, forward slashes, "e:/..." drive form. A
# worktree with no manifest, or a path not in it, passes untouched. It cannot see a shell
# redirect or a script that rewrites a file; Freeze-Tests.ps1 -Check is the backstop for those.
#
# Bash builtins only: this runs before every Write and Edit, and under Git Bash each external
# process (git, grep, sed, cygpath, ...) costs seconds under load - enough to hit the 10s hook
# timeout, which lets the edit through unchecked. $(</dev/stdin) reads the payload in blocks
# without a fork; `read -d ''` would read a large Write payload one byte at a time.
set -uo pipefail

payload=$(</dev/stdin)

# The first "file_path" is the tool's target; a later one could sit inside the edited text.
[[ $payload =~ \"file_path\"[[:space:]]*:[[:space:]]*\"([^\"]*)\" ]] || exit 0
path=${BASH_REMATCH[1]}
path=${path//\\\\/\\}    # JSON-escaped backslashes
path=${path//\\//}       # backslashes to slashes
if [[ $path =~ ^/([A-Za-z])/(.*)$ ]]; then
    path="${BASH_REMATCH[1]}:/${BASH_REMATCH[2]}"   # /e/... to e:/...
fi
[[ -n $path ]] || exit 0

# Same key Freeze-Tests.ps1 writes: forward slashes, drive form, lower case.
key=${path,,}

# Walk up to a directory that exists: a file being created has no folder yet, and is never frozen.
dir=${path%/*}
while [[ ! -d $dir ]]; do
    parent=${dir%/*}
    [[ $parent != "$dir" && -n $parent ]] || exit 0
    dir=$parent
done

# Find the git directory as `git rev-parse --absolute-git-dir` would: a .git folder, or a
# worktree's .git file holding "gitdir: <path>".
gitdir=''
while :; do
    if [[ -d $dir/.git ]]; then
        gitdir=$dir/.git
        break
    fi
    if [[ -f $dir/.git ]]; then
        IFS= read -r line < "$dir/.git" || [[ -n $line ]] || exit 0
        gitdir=${line#gitdir: }
        gitdir=${gitdir%$'\r'}
        [[ $gitdir == /* || $gitdir =~ ^[A-Za-z]: ]] || gitdir=$dir/$gitdir
        break
    fi
    parent=${dir%/*}
    [[ $parent != "$dir" && -n $parent ]] || exit 0
    dir=$parent
done

manifest=$gitdir/frozen-tests/manifest.txt
[[ -f $manifest ]] || exit 0

while IFS= read -r entry || [[ -n $entry ]]; do
    entry=${entry%$'\r'}
    entry=${entry#$'\xef\xbb\xbf'}
    if [[ $entry == "$key" ]]; then
        printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' \
            "Blocked: this test file was frozen when its red was approved. A failing test is the spec - fix the code under test, not the test. If you believe the test itself is wrong, stop and report it as a test change request with the failing line and your evidence; the delivery manager decides and runs Freeze-Tests.ps1 -Release."
        exit 0
    fi
done < "$manifest"

exit 0
