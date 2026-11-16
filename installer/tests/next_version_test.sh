#!/bin/sh
# Tests for installer/next-version.sh: release versions only go up.
set -u

HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../.." && pwd)
SCRIPT="$ROOT/installer/next-version.sh"
. "$HERE/lib.sh"

# next <tags, one per line> [args...]: prints stdout, then "rc=<code>" on its own line.
next() {
    tags=$1
    shift
    rc=0
    out=$(printf '%s' "$tags" | sh "$SCRIPT" "$@" 2>&1) || rc=$?
    printf '%s\nrc=%s\n' "$out" "$rc"
}

test_no_tags_starts_at_the_first_version() {
    t_assert_eq "0.1.0
rc=0" "$(next '')" "first release"
}

test_patch_is_the_default_bump_of_the_highest_tag() {
    t_assert_eq "1.2.4
rc=0" "$(next 'v1.0.0
v1.2.3
v1.1.9
')" "patch bump"
}

test_minor_and_major_bumps_reset_the_lower_parts() {
    t_assert_eq "1.3.0
rc=0" "$(next 'v1.2.3' --bump minor)" "minor bump"
    t_assert_eq "2.0.0
rc=0" "$(next 'v1.2.3' --bump major)" "major bump"
}

test_numbers_compare_as_numbers_not_text() {
    t_assert_eq "1.10.1
rc=0" "$(next 'v1.9.0
v1.10.0
')" "1.10 is above 1.9"
}

test_tags_that_are_not_versions_and_crlf_are_ignored() {
    t_assert_eq "0.2.1
rc=0" "$(next 'nightly
v0.2.0
release-candidate
')" "other tags"
    t_assert_eq "0.2.1
rc=0" "$(printf 'v0.2.0\r\n' | sh "$SCRIPT"; printf 'rc=%s\n' $?)" "a CRLF line"
}

test_only_a_prerelease_of_the_highest_means_its_final_release_is_next() {
    t_assert_eq "1.3.0
rc=0" "$(next 'v1.2.0
v1.3.0-rc.1
')" "promote the rc"
    t_assert_eq "1.3.1
rc=0" "$(next 'v1.3.0-rc.1
v1.3.0
')" "a final release beats its rc"
}

test_an_explicit_newer_version_is_accepted_without_the_v() {
    t_assert_eq "2.0.0
rc=0" "$(next 'v1.4.2' --version v2.0.0)" "explicit major"
    t_assert_eq "1.5.0-rc.1
rc=0" "$(next 'v1.4.2' --version 1.5.0-rc.1)" "explicit pre-release"
    t_assert_eq "1.0.0
rc=0" "$(next '' --version 1.0.0)" "explicit first version"
}

test_an_explicit_version_that_does_not_go_up_is_refused() {
    out=$(next 'v1.4.2' --version 1.4.2)
    t_assert_contains "$out" "rc=1" "same version"
    t_assert_contains "$out" "not newer than the latest existing version 1.4.2" "message"
    t_assert_contains "$(next 'v1.4.2' --version 1.4.1)" "rc=1" "older version"
    t_assert_contains "$(next 'v1.4.2' --version 1.4.2-rc.1)" "rc=1" "an rc of a released version"
}

test_prereleases_of_one_version_and_their_final_release_go_up() {
    t_assert_eq "1.5.0-rc.2
rc=0" "$(next 'v1.5.0-rc.1' --version 1.5.0-rc.2)" "rc.2 after rc.1"
    t_assert_eq "1.5.0
rc=0" "$(next 'v1.5.0-rc.2' --version 1.5.0)" "final after rc"
}

test_bad_arguments_are_refused() {
    t_assert_contains "$(next '' --version 1.0)" "rc=1" "not three numbers"
    t_assert_contains "$(next '' --version '1.0.0;rm')" "rc=1" "shell characters"
    t_assert_contains "$(next '' --bump huge)" "--bump must be patch, minor or major" "bad bump"
    t_assert_contains "$(next '' --what)" "unknown argument" "unknown argument"
}

t_run test_no_tags_starts_at_the_first_version
t_run test_patch_is_the_default_bump_of_the_highest_tag
t_run test_minor_and_major_bumps_reset_the_lower_parts
t_run test_numbers_compare_as_numbers_not_text
t_run test_tags_that_are_not_versions_and_crlf_are_ignored
t_run test_only_a_prerelease_of_the_highest_means_its_final_release_is_next
t_run test_an_explicit_newer_version_is_accepted_without_the_v
t_run test_an_explicit_version_that_does_not_go_up_is_refused
t_run test_prereleases_of_one_version_and_their_final_release_go_up
t_run test_bad_arguments_are_refused
t_summary
