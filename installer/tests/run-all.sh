#!/bin/sh
# Runs every installer/tests/*_test.sh with sh (or $T_SHELL) and fails if any file fails.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
shell="${T_SHELL:-sh}"
failed=0
for test_file in "$here"/*_test.sh; do
    [ -f "$test_file" ] || continue
    printf '== %s (%s)\n' "$test_file" "$shell"
    "$shell" "$test_file" || failed=$((failed + 1))
done

if [ "$failed" -ne 0 ]; then
    printf '%s test file(s) failed\n' "$failed"
    exit 1
fi
printf 'all test files passed\n'
