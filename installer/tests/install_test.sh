#!/bin/sh
# Tests for installer/install.sh. Run with sh or dash; macOS commands are only ever reached
# through the seam functions, which tests override after sourcing.
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
# shellcheck source=lib.sh
. "$HERE/lib.sh"

HUDDLE_INSTALL_NO_MAIN=1
export HUDDLE_INSTALL_NO_MAIN
TEST_HOME=$(mktemp -d)
HOME="$TEST_HOME"
export HOME
trap 'rm -rf "$TEST_HOME"' EXIT
# A developer's own HUDDLE_* variables must not leak into the assertions.
unset HUDDLE_VERSION HUDDLE_BASE_URL HUDDLE_INSTALL_ROOT HUDDLE_DATA_ROOT HUDDLE_CHECK_ONLY \
    HUDDLE_UNINSTALL HUDDLE_PURGE_DATA HUDDLE_FORCE HUDDLE_NO_LAUNCH

# shellcheck source=../install.sh
. "$HERE/../install.sh"

NL='
'
DATA_DEFAULT="$HOME/Library/Application Support/Agency.Huddle"

test_resolve_paths_defaults() {
    parse_args
    resolve_paths
    t_assert_eq "$DATA_DEFAULT/install" "$INSTALL_ROOT" "INSTALL_ROOT"
    t_assert_eq "$DATA_DEFAULT" "$DATA_ROOT" "DATA_ROOT"
    t_assert_eq "$DATA_DEFAULT/install/app/Huddle.App" "$APP_EXE" "APP_EXE"
    t_assert_eq "$DATA_DEFAULT/install/node/bin/node" "$NODE_BIN" "NODE_BIN"
    t_assert_eq "$DATA_DEFAULT/install/tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js" "$ADAPTER_ENTRY" "ADAPTER_ENTRY"
    t_assert_eq "$DATA_DEFAULT/install/install-state" "$STATE_FILE" "STATE_FILE"
    t_assert_eq "$DATA_DEFAULT/cache" "$CACHE_DIR" "CACHE_DIR"
}

test_resolve_paths_honours_overrides() {
    parse_args --install-root /opt/hi --data-root /opt/hd
    resolve_paths
    t_assert_eq "/opt/hi" "$INSTALL_ROOT" "INSTALL_ROOT override"
    t_assert_eq "/opt/hd" "$DATA_ROOT" "DATA_ROOT override"
    t_assert_eq "/opt/hi/app/Huddle.App" "$APP_EXE" "APP_EXE follows INSTALL_ROOT"
    t_assert_eq "/opt/hi/install-state" "$STATE_FILE" "STATE_FILE follows INSTALL_ROOT"
    t_assert_eq "/opt/hd/cache" "$CACHE_DIR" "CACHE_DIR follows DATA_ROOT"
}

test_parse_args_defaults() {
    parse_args
    t_assert_eq "latest" "$OPT_VERSION" "default version"
    t_assert_eq "" "$OPT_BASE_URL" "default base url"
    t_assert_eq "0" "$OPT_CHECK_ONLY" "default check-only"
    t_assert_eq "0" "$OPT_UNINSTALL" "default uninstall"
    t_assert_eq "0" "$OPT_PURGE_DATA" "default purge-data"
    t_assert_eq "0" "$OPT_FORCE" "default force"
    t_assert_eq "0" "$OPT_NO_LAUNCH" "default no-launch"
}

test_parse_args_accepts_every_flag() {
    parse_args --version v1.2.3 --base-url "$TEST_HOME" --install-root /a --data-root /b \
        --check-only --uninstall --purge-data --force --no-launch
    t_assert_eq "v1.2.3" "$OPT_VERSION" "--version"
    t_assert_eq "$TEST_HOME" "$OPT_BASE_URL" "--base-url"
    t_assert_eq "/a" "$OPT_INSTALL_ROOT" "--install-root"
    t_assert_eq "/b" "$OPT_DATA_ROOT" "--data-root"
    t_assert_eq "1" "$OPT_CHECK_ONLY" "--check-only"
    t_assert_eq "1" "$OPT_UNINSTALL" "--uninstall"
    t_assert_eq "1" "$OPT_PURGE_DATA" "--purge-data"
    t_assert_eq "1" "$OPT_FORCE" "--force"
    t_assert_eq "1" "$OPT_NO_LAUNCH" "--no-launch"
}

test_parse_args_resets_between_calls() {
    parse_args --force --version v9
    parse_args
    t_assert_eq "0" "$OPT_FORCE" "force reset"
    t_assert_eq "latest" "$OPT_VERSION" "version reset"
}

test_parse_args_unknown_flag_exits_3() {
    rc=0
    err=$( (parse_args --bogus) 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "unknown flag exit code"
    t_assert_contains "$err" "--bogus" "unknown flag is named"
}

test_parse_args_missing_value_exits_3() {
    rc=0
    (parse_args --version) >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "--version without a value"
    rc=0
    (parse_args --base-url) >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "--base-url without a value"
}

test_resolve_asset_folder_is_file() {
    mkdir -p "$TEST_HOME/assets"
    parse_args --base-url "$TEST_HOME/assets"
    resolve_asset "huddle-osx-arm64.tar.gz"
    t_assert_eq "file" "$ASSET_KIND" "folder kind"
    t_assert_eq "$TEST_HOME/assets/huddle-osx-arm64.tar.gz" "$ASSET_SRC" "folder source"
    parse_args --base-url "$TEST_HOME/assets/"
    resolve_asset "a.tgz"
    t_assert_eq "$TEST_HOME/assets/a.tgz" "$ASSET_SRC" "trailing slash is not doubled"
}

test_resolve_asset_https_base_is_url() {
    parse_args --base-url "https://x"
    resolve_asset "a.tgz"
    t_assert_eq "url" "$ASSET_KIND" "https kind"
    t_assert_eq "https://x/a.tgz" "$ASSET_SRC" "https source"
    parse_args --base-url "https://x/dl/"
    resolve_asset "a.tgz"
    t_assert_eq "https://x/dl/a.tgz" "$ASSET_SRC" "https trailing slash"
}

test_resolve_asset_bad_base_exits_3() {
    rc=0
    (parse_args --base-url "$TEST_HOME/does-not-exist"; resolve_asset "a.tgz") >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "missing folder"
    rc=0
    (parse_args --base-url "ftp://x"; resolve_asset "a.tgz") >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "non-https url"
}

test_resolve_asset_release_urls() {
    HUDDLE_REPO="owner/repo"
    parse_args
    resolve_asset "a.tgz"
    t_assert_eq "url" "$ASSET_KIND" "latest kind"
    t_assert_eq "https://github.com/owner/repo/releases/latest/download/a.tgz" "$ASSET_SRC" "latest url"
    parse_args --version v1.2.3
    resolve_asset "a.tgz"
    t_assert_eq "https://github.com/owner/repo/releases/download/v1.2.3/a.tgz" "$ASSET_SRC" "tagged url"
}

test_log_step_formats() {
    t_assert_eq "[ok] node - v24.21.0" "$(log_step ok node v24.21.0)" "ok line"
    t_assert_eq "[skip] node - already satisfied" "$(log_step skip node "already satisfied")" "skip line"
    t_assert_eq "[..] node - working" "$(log_step work node working)" "working line"
    t_assert_eq "[!!] disk - 500 MB free${NL}     fix: free 2048 MB" "$(log_step fail disk "500 MB free" "free 2048 MB")" "fail line with fix"
    t_assert_eq "[!!] disk - 500 MB free" "$(log_step fail disk "500 MB free")" "fail line without fix"
}

test_sourcing_with_guard_runs_nothing() {
    out=$(HUDDLE_INSTALL_NO_MAIN=1 "$T_SHELL" -c '. "$1"; printf "[%s]" "${INSTALL_ROOT:-}"' sh "$HERE/../install.sh" 2>&1)
    t_assert_eq "[]" "$out" "no paths resolved, nothing printed"
}

test_main_returns_zero_for_folder_base() {
    mkdir -p "$TEST_HOME/assets"
    rc=0
    (HUDDLE_REPO="owner/repo"; main --base-url "$TEST_HOME/assets") >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "main exit code"
}

test_main_unreplaced_placeholder_exits_3() {
    rc=0
    err=$( (HUDDLE_REPO="__HUDDLE_REPO__"; main) 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "placeholder exit code"
    t_assert_contains "$err" "release" "run-from-a-release message"
    rc=0
    (HUDDLE_REPO="__HUDDLE_REPO__"; main --base-url "$TEST_HOME") >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "base-url makes the repo irrelevant"
}

test_release_replacement_keeps_comparison_intact() {
    placeholder_count=$(grep -o '__HUDDLE_REPO__' "$HERE/../install.sh" | wc -l | tr -d ' ')
    t_assert_eq "1" "$placeholder_count" "the placeholder literal appears exactly once"
    replaced="$TEST_HOME/install-replaced.sh"
    sed "s|__HUDDLE_REPO__|owner/repo|g" "$HERE/../install.sh" >"$replaced"
    err=$(env -u HUDDLE_REPO HUDDLE_INSTALL_NO_MAIN= "$T_SHELL" "$replaced" --check-only 2>&1 >/dev/null) || true
    t_assert_not_contains "$err" "does not know which repository" "a released copy knows its repository"
    rm -f "$replaced"
}

test_script_run_directly() {
    rc=0
    HUDDLE_INSTALL_NO_MAIN= "$T_SHELL" "$HERE/../install.sh" --bogus >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "direct run, unknown flag"
    rc=0
    HUDDLE_INSTALL_NO_MAIN= HUDDLE_REPO="owner/repo" "$T_SHELL" -s -- --bogus <"$HERE/../install.sh" >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "piped run (curl | sh), unknown flag"
    rc=0
    HUDDLE_INSTALL_NO_MAIN= HUDDLE_REPO="owner/repo" "$T_SHELL" "$HERE/../install.sh" --base-url "$TEST_HOME" >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "direct run, folder base url"
}

# ---- Task 29: prerequisite checks (Spec section 5) ----------------------------------------

PROBE_LOG="$TEST_HOME/probe.log"

# Prints the path of a fresh directory holding no-op executables with the given names.
make_stub_dir() {
    stub_dir=$(mktemp -d "$TEST_HOME/stubs.XXXXXX")
    for stub_name in "$@"; do
        printf '#!/bin/sh\nexit 0\n' >"$stub_dir/$stub_name"
        chmod +x "$stub_dir/$stub_name"
    done
    printf '%s' "$stub_dir"
}

# Adds a sysctl stub to a stub dir; it prints $2 only for the exact call `sysctl -n hw.optional.arm64`.
add_sysctl_stub() {
    printf '#!/bin/sh\nif [ "$1 $2" = "-n hw.optional.arm64" ]; then echo %s; else echo 99; fi\n' "$2" >"$1/sysctl"
    chmod +x "$1/sysctl"
}

# Every seam answers "healthy Apple Silicon Mac"; tests override the one they care about.
good_seams() {
    macos_major() { printf '15\n'; }
    host_arch() { printf 'arm64\n'; }
    free_disk_mb() { printf '10000\n'; }
    http_probe() { printf '%s\n' "$1" >>"$PROBE_LOG"; return 0; }
    rm -f "$PROBE_LOG"
}

# run_check <PATH to use> [install.sh args]: parses, resolves paths, runs the checks in this shell
# with PATH narrowed to the stub dir, so only `command -v` stubs and shell builtins are reachable.
# Anything else the product calls shows up in $TEST_HOME/err as "not found".
run_check() {
    saved_path=$PATH
    PATH=$1
    shift
    parse_args "$@"
    resolve_paths
    check_prerequisites 2>"$TEST_HOME/err"
    PATH=$saved_path
}

test_prereq_all_good() {
    good_seams
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    run_check "$stubs"
    out=$(print_prerequisites)
    for item in "macOS version" "CPU" "curl" "tar" "hash tool" "codesign" "xattr" "disk space" "write access" \
        "network github.com" "network nodejs.org" "network registry.npmjs.org"; do
        t_assert_contains "$out" "[ok] $item" "$item is ok"
    done
    t_assert_not_contains "$out" "[!!]" "no failures"
    t_assert_eq "0" "$PREREQ_FAILED" "failure count"
    t_assert_eq "osx-arm64" "$HOST_RID" "runtime id"
    t_assert_eq "" "$(cat "$TEST_HOME/err")" "only builtins and stubs were reached"
    t_assert_contains "$(cat "$PROBE_LOG")" "https://registry.npmjs.org" "probed npm registry"
}

test_prereq_old_macos_fails_with_fix() {
    good_seams
    macos_major() { printf '11\n'; }
    stubs=$(make_stub_dir curl tar shasum codesign xattr)
    run_check "$stubs"
    out=$(print_prerequisites)
    t_assert_contains "$out" "[!!] macOS version" "macOS 11 fails"
    t_assert_contains "$out" "     fix: " "failure carries a fix"
    t_assert_contains "$out" "[ok] CPU" "later checks still run"
    t_assert_eq "1" "$PREREQ_FAILED" "failure count"
}

test_prereq_unreadable_macos_fails() {
    good_seams
    macos_major() { printf '\n'; }
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    run_check "$stubs"
    t_assert_contains "$(print_prerequisites)" "[!!] macOS version" "empty version fails"
}

test_prereq_arch_selects_runtime_id() {
    good_seams
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    host_arch() { printf 'arm64\n'; }
    run_check "$stubs"
    t_assert_eq "osx-arm64" "$HOST_RID" "arm64"
    host_arch() { printf 'x86_64\n'; }
    run_check "$stubs"
    t_assert_eq "osx-x64" "$HOST_RID" "x86_64 without sysctl"
    t_assert_contains "$(print_prerequisites)" "[ok] CPU" "x86_64 is supported"
}

test_prereq_rosetta_shell_selects_arm64() {
    good_seams
    host_arch() { printf 'x86_64\n'; }
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    add_sysctl_stub "$stubs" 1
    run_check "$stubs"
    t_assert_eq "osx-arm64" "$HOST_RID" "Rosetta shell on Apple Silicon"
    rm -f "$stubs/sysctl"
    add_sysctl_stub "$stubs" 0
    run_check "$stubs"
    t_assert_eq "osx-x64" "$HOST_RID" "sysctl says Intel"
}

test_prereq_unsupported_cpu_fails() {
    good_seams
    host_arch() { printf 'i386\n'; }
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    run_check "$stubs"
    t_assert_contains "$(print_prerequisites)" "[!!] CPU" "i386 fails"
    t_assert_eq "1" "$PREREQ_FAILED" "failure count"
}

test_prereq_missing_tool_fails() {
    good_seams
    all="curl tar sha256sum codesign xattr"
    for missing in curl tar codesign xattr; do
        keep=""
        for tool in $all; do
            [ "$tool" = "$missing" ] || keep="$keep $tool"
        done
        # shellcheck disable=SC2086 # word splitting of the tool list is intended
        stubs=$(make_stub_dir $keep)
        run_check "$stubs"
        out=$(print_prerequisites)
        t_assert_contains "$out" "[!!] $missing" "$missing missing"
        t_assert_eq "1" "$PREREQ_FAILED" "only $missing failed"
    done
}

test_prereq_hash_tool_either_is_enough() {
    good_seams
    stubs=$(make_stub_dir curl tar shasum codesign xattr)
    run_check "$stubs"
    t_assert_contains "$(print_prerequisites)" "[ok] hash tool" "shasum alone"
    stubs=$(make_stub_dir curl tar codesign xattr)
    run_check "$stubs"
    t_assert_contains "$(print_prerequisites)" "[!!] hash tool" "neither sha256sum nor shasum"
}

test_prereq_disk_threshold() {
    good_seams
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    free_disk_mb() { printf '500\n'; }
    run_check "$stubs"
    out=$(print_prerequisites)
    t_assert_contains "$out" "[!!] disk space" "500 MB fails"
    t_assert_contains "$out" "     fix: " "disk failure carries a fix"
    free_disk_mb() { printf '2047\n'; }
    run_check "$stubs"
    t_assert_contains "$(print_prerequisites)" "[!!] disk space" "2047 MB fails"
    free_disk_mb() { printf '2048\n'; }
    run_check "$stubs"
    t_assert_contains "$(print_prerequisites)" "[ok] disk space" "2048 MB is enough"
}

test_prereq_probe_failure_is_isolated() {
    good_seams
    http_probe() {
        printf '%s\n' "$1" >>"$PROBE_LOG"
        case $1 in
            *nodejs.org*) return 1 ;;
            *) return 0 ;;
        esac
    }
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    run_check "$stubs"
    out=$(print_prerequisites)
    t_assert_contains "$out" "[!!] network nodejs.org" "nodejs.org fails"
    t_assert_contains "$out" "[ok] network github.com" "github.com still checked"
    t_assert_contains "$out" "[ok] network registry.npmjs.org" "registry still checked"
    t_assert_eq "1" "$PREREQ_FAILED" "failure count"
}

test_prereq_reports_every_failure() {
    good_seams
    macos_major() { printf '11\n'; }
    free_disk_mb() { printf '500\n'; }
    http_probe() { return 1; }
    stubs=$(make_stub_dir curl tar sha256sum xattr)
    run_check "$stubs"
    out=$(print_prerequisites)
    t_assert_eq "6" "$PREREQ_FAILED" "macOS, codesign, disk and three hosts"
    t_assert_contains "$out" "[!!] codesign" "codesign listed"
    t_assert_contains "$out" "[!!] network github.com" "github listed"
}

test_prereq_folder_base_url_skips_network() {
    good_seams
    mkdir -p "$TEST_HOME/assets"
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    run_check "$stubs" --base-url "$TEST_HOME/assets"
    out=$(print_prerequisites)
    t_assert_contains "$out" "[skip] network" "network skipped"
    t_assert_not_contains "$out" "[!!]" "no failures"
    t_assert_no_file "$PROBE_LOG" "http_probe was never called"
}

test_prereq_write_access_blocked_by_a_file() {
    good_seams
    : >"$TEST_HOME/afile"
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    run_check "$stubs" --install-root "$TEST_HOME/afile/sub"
    t_assert_contains "$(print_prerequisites)" "[!!] write access" "install root under a file"
    t_assert_no_file "$TEST_HOME/afile/sub" "nothing was created"
}

test_check_only_exit_codes_and_no_side_effects() {
    good_seams
    fresh=$(mktemp -d "$TEST_HOME/fresh.XXXXXX")
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    rc=0
    out=$( (HOME="$fresh"; PATH="$stubs"; HUDDLE_REPO="owner/repo"; main --check-only) 2>&1 ) || rc=$?
    t_assert_eq "0" "$rc" "all good exits 0"
    t_assert_contains "$out" "[ok] macOS version" "table is printed"
    t_assert_eq "" "$(ls -A "$fresh")" "no directory was created"
    macos_major() { printf '11\n'; }
    rc=0
    out=$( (HOME="$fresh"; PATH="$stubs"; HUDDLE_REPO="owner/repo"; main --check-only) 2>&1 ) || rc=$?
    t_assert_eq "2" "$rc" "unmet prerequisites exit 2"
    t_assert_contains "$out" "[!!] macOS version" "failure is printed"
    t_assert_eq "" "$(ls -A "$fresh")" "still no directory created"
}

test_check_only_folder_base_never_probes() {
    good_seams
    mkdir -p "$TEST_HOME/assets"
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    rc=0
    (PATH="$stubs"; HUDDLE_REPO="owner/repo"; main --check-only --base-url "$TEST_HOME/assets") >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "exit code"
    t_assert_no_file "$PROBE_LOG" "http_probe was never called"
}

# ---- Environment variables (Spec section 7) -----------------------------------------------

clear_env() {
    unset HUDDLE_VERSION HUDDLE_BASE_URL HUDDLE_INSTALL_ROOT HUDDLE_DATA_ROOT
    unset HUDDLE_CHECK_ONLY HUDDLE_UNINSTALL HUDDLE_PURGE_DATA HUDDLE_FORCE HUDDLE_NO_LAUNCH
}

test_env_used_when_flag_absent() {
    clear_env
    HUDDLE_VERSION=v2.0.0
    HUDDLE_BASE_URL="$TEST_HOME"
    HUDDLE_INSTALL_ROOT=/env/i
    HUDDLE_DATA_ROOT=/env/d
    HUDDLE_CHECK_ONLY=1
    HUDDLE_UNINSTALL=1
    HUDDLE_PURGE_DATA=yes
    HUDDLE_FORCE=true
    HUDDLE_NO_LAUNCH=1
    parse_args
    resolve_paths
    clear_env
    t_assert_eq "v2.0.0" "$OPT_VERSION" "HUDDLE_VERSION"
    t_assert_eq "$TEST_HOME" "$OPT_BASE_URL" "HUDDLE_BASE_URL"
    t_assert_eq "/env/i" "$INSTALL_ROOT" "HUDDLE_INSTALL_ROOT"
    t_assert_eq "/env/d" "$DATA_ROOT" "HUDDLE_DATA_ROOT"
    t_assert_eq "1" "$OPT_CHECK_ONLY" "HUDDLE_CHECK_ONLY"
    t_assert_eq "1" "$OPT_UNINSTALL" "HUDDLE_UNINSTALL"
    t_assert_eq "1" "$OPT_PURGE_DATA" "HUDDLE_PURGE_DATA any non-empty value"
    t_assert_eq "1" "$OPT_FORCE" "HUDDLE_FORCE"
    t_assert_eq "1" "$OPT_NO_LAUNCH" "HUDDLE_NO_LAUNCH"
}

test_env_flag_beats_env() {
    clear_env
    HUDDLE_VERSION=v2.0.0
    HUDDLE_BASE_URL=https://env.example
    HUDDLE_INSTALL_ROOT=/env/i
    HUDDLE_DATA_ROOT=/env/d
    parse_args --version v3.0.0 --base-url https://flag.example --install-root /flag/i --data-root /flag/d
    resolve_paths
    clear_env
    t_assert_eq "v3.0.0" "$OPT_VERSION" "--version wins"
    t_assert_eq "https://flag.example" "$OPT_BASE_URL" "--base-url wins"
    t_assert_eq "/flag/i" "$INSTALL_ROOT" "--install-root wins"
    t_assert_eq "/flag/d" "$DATA_ROOT" "--data-root wins"
}

test_env_zero_and_empty_are_off() {
    clear_env
    HUDDLE_CHECK_ONLY=0
    HUDDLE_UNINSTALL=
    HUDDLE_PURGE_DATA=0
    HUDDLE_FORCE=
    HUDDLE_NO_LAUNCH=0
    HUDDLE_VERSION=
    HUDDLE_BASE_URL=
    parse_args
    clear_env
    t_assert_eq "0" "$OPT_CHECK_ONLY" "0 is off"
    t_assert_eq "0" "$OPT_UNINSTALL" "empty is off"
    t_assert_eq "0" "$OPT_PURGE_DATA" "0 is off (purge)"
    t_assert_eq "0" "$OPT_FORCE" "empty is off (force)"
    t_assert_eq "0" "$OPT_NO_LAUNCH" "0 is off (no-launch)"
    t_assert_eq "latest" "$OPT_VERSION" "empty version means latest"
    t_assert_eq "" "$OPT_BASE_URL" "empty base url means none"
}

test_env_check_only_reaches_main() {
    good_seams
    stubs=$(make_stub_dir curl tar sha256sum codesign xattr)
    fresh=$(mktemp -d "$TEST_HOME/fresh.XXXXXX")
    rc=0
    out=$( (HOME="$fresh"; PATH="$stubs"; HUDDLE_REPO="owner/repo"; HUDDLE_CHECK_ONLY=1; main) 2>&1 ) || rc=$?
    t_assert_eq "0" "$rc" "env check-only exits 0"
    t_assert_contains "$out" "[ok] macOS version" "env check-only prints the table"
}

t_run test_env_used_when_flag_absent
t_run test_env_flag_beats_env
t_run test_env_zero_and_empty_are_off
t_run test_env_check_only_reaches_main
t_run test_resolve_paths_defaults
t_run test_resolve_paths_honours_overrides
t_run test_parse_args_defaults
t_run test_parse_args_accepts_every_flag
t_run test_parse_args_resets_between_calls
t_run test_parse_args_unknown_flag_exits_3
t_run test_parse_args_missing_value_exits_3
t_run test_resolve_asset_folder_is_file
t_run test_resolve_asset_https_base_is_url
t_run test_resolve_asset_bad_base_exits_3
t_run test_resolve_asset_release_urls
t_run test_log_step_formats
t_run test_sourcing_with_guard_runs_nothing
t_run test_main_returns_zero_for_folder_base
t_run test_main_unreplaced_placeholder_exits_3
t_run test_release_replacement_keeps_comparison_intact
t_run test_script_run_directly
t_run test_prereq_all_good
t_run test_prereq_old_macos_fails_with_fix
t_run test_prereq_unreadable_macos_fails
t_run test_prereq_arch_selects_runtime_id
t_run test_prereq_rosetta_shell_selects_arm64
t_run test_prereq_unsupported_cpu_fails
t_run test_prereq_missing_tool_fails
t_run test_prereq_hash_tool_either_is_enough
t_run test_prereq_disk_threshold
t_run test_prereq_probe_failure_is_isolated
t_run test_prereq_reports_every_failure
t_run test_prereq_folder_base_url_skips_network
t_run test_prereq_write_access_blocked_by_a_file
t_run test_check_only_exit_codes_and_no_side_effects
t_run test_check_only_folder_base_never_probes

t_summary
