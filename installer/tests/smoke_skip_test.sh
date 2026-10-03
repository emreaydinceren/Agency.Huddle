#!/bin/sh
# Pins the guard of installer/tests/smoke.sh (plan task 40): on anything but macOS the smoke test
# prints "SKIP: macOS only" and exits 0 without building, installing or touching the machine.
# The real macOS run is a manual or runner step; on macOS this file does nothing (it must not start
# a full install from the unit test run).
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
. "$HERE/lib.sh"

SMOKE="$HERE/smoke.sh"

test_smoke_skips_when_not_macos() {
    if [ "$(uname -s)" = Darwin ]; then
        t_pass
        return 0
    fi
    smoke_rc=0
    smoke_out=$(sh "$SMOKE" "$HERE/no-such-release-dir" 2>&1 </dev/null) || smoke_rc=$?
    t_assert_eq 0 "$smoke_rc" "smoke.sh exits 0 off macOS"
    t_assert_eq "SKIP: macOS only" "$smoke_out" "smoke.sh prints exactly the SKIP line"
    t_assert_no_file "$HERE/no-such-release-dir" "smoke.sh creates nothing off macOS"
}

test_smoke_has_a_valid_posix_syntax() {
    t_assert_file "$SMOKE" "smoke.sh exists"
    syntax_rc=0
    sh -n "$SMOKE" >/dev/null 2>&1 || syntax_rc=$?
    t_assert_eq 0 "$syntax_rc" "sh -n smoke.sh"
}

t_run test_smoke_skips_when_not_macos
t_run test_smoke_has_a_valid_posix_syntax
t_summary
