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

# ---- Verified downloads (tasks 30): file_sha256, parse_sums, expected_hash, get_verified_asset ----

HELLO_SHA=5891b5b522d5df086d0ff0b110fbd9d21bb4fc7163af34d08286a2e846f6be03
A_SHA=$(printf '%064d' 0 | tr 0 a)
B_SHA=$(printf '%064d' 0 | tr 0 b)
ZERO_SHA=$(printf '%064d' 0)
TAMPERED_SHA=92e78d0b032962f47792a9fa95fd981ef63e1e3ef074d536d6304c75eddbe29f

# Fresh scratch folder for one download test. The cache path has a space in it on purpose (the
# real one lives under "Application Support"). Resets the seam logs and the fake's behaviour.
dl_setup() {
    DL=$(mktemp -d "$TEST_HOME/dl.XXXXXX")
    CACHE="$DL/Cache Dir/sub"
    HG_LOG="$DL/http.log"
    SLEEP_LOG="$DL/sleep.log"
    HG_FAIL_FIRST=0
    HG_BODY=good
    : >"$HG_LOG"
    : >"$SLEEP_LOG"
}

# Fake http_get: records "URL|OUTFILE" per call; the first HG_FAIL_FIRST calls leave junk in the
# output file and fail; later calls write "hello\n" (or "tampered\n" when HG_BODY=tampered).
# Counters live in files because get_verified_asset is usually called inside $( ).
http_get() {
    printf '%s|%s\n' "$1" "$2" >>"$HG_LOG"
    hg_n=$(wc -l <"$HG_LOG" | tr -d ' ')
    if [ "$hg_n" -le "$HG_FAIL_FIRST" ]; then
        printf 'junk' >"$2"
        return 22
    fi
    if [ "$HG_BODY" = tampered ]; then
        printf 'tampered\n' >"$2"
    else
        printf 'hello\n' >"$2"
    fi
}

# Fake sleep seam: records the requested seconds instead of waiting.
sleep_seconds() {
    printf '%s\n' "$1" >>"$SLEEP_LOG"
}

dl_calls() {
    wc -l <"$HG_LOG" | tr -d ' '
}

test_file_sha256_known_vector() {
    dl_setup
    printf 'hello\n' >"$DL/f x"
    t_assert_eq "$HELLO_SHA" "$(file_sha256 "$DL/f x")" "sha256 of a known file (path has a space)"
    rc=0
    out=$(file_sha256 "$DL/missing" 2>&1) || rc=$?
    t_assert_eq "1" "$rc" "missing file returns 1"
    t_assert_contains "$out" "$DL/missing" "missing file message names the path"
}

test_file_sha256_falls_back_to_shasum() {
    dl_setup
    printf 'hello\n' >"$DL/f"
    stubs=$(make_stub_dir)
    printf '#!/bin/sh\nprintf "%%s\\n" "$*" >"%s/shasum.args"\nprintf "%s  -\\n"\n' "$DL" "$HELLO_SHA" >"$stubs/shasum"
    chmod +x "$stubs/shasum"
    out=$( (PATH="$stubs"; file_sha256 "$DL/f") 2>&1 )
    t_assert_eq "$HELLO_SHA" "$out" "hash comes from shasum when sha256sum is absent"
    t_assert_eq "-a 256" "$(cat "$DL/shasum.args")" "shasum is asked for SHA-256"
}

test_file_sha256_without_tool_fails_with_message() {
    dl_setup
    printf 'hello\n' >"$DL/f"
    stubs=$(make_stub_dir)
    rc=0
    out=$( (PATH="$stubs"; file_sha256 "$DL/f") 2>&1 ) || rc=$?
    t_assert_eq "1" "$rc" "no hash tool returns 1"
    t_assert_contains "$out" "sha256sum or shasum" "no hash tool message"
}

# Writes a SHA256SUMS fixture. $1 is the line ending ("\n" or "\r\n").
write_sums() {
    printf '%s  huddle-osx-arm64.tar.gz%b' "$A_SHA" "$2" >"$1"
    printf '%s  huddle-osx-x64.tar.gz%b' "$(printf '%064d' 0 | tr 0 B)" "$2" >>"$1"
    printf '%s *install.sh%b' "$B_SHA" "$2" >>"$1"
    printf '%b' "$2" >>"$1"
    printf 'this is not a hash line%b' "$2" >>"$1"
    printf 'abc123  short.txt%b' "$2" >>"$1"
    printf '%s  my file.txt%b' "$A_SHA" "$2" >>"$1"
}

test_parse_sums_and_expected_hash() {
    dl_setup
    write_sums "$DL/SHA256SUMS" '\n'
    parse_sums "$DL/SHA256SUMS"
    t_assert_eq "$A_SHA" "$(expected_hash huddle-osx-arm64.tar.gz)" "plain line"
    t_assert_eq "$B_SHA" "$(expected_hash huddle-osx-x64.tar.gz)" "upper-case hash is lower-cased"
    t_assert_eq "$B_SHA" "$(expected_hash install.sh)" "binary-mode star prefix is dropped"
    t_assert_eq "$A_SHA" "$(expected_hash "my file.txt")" "a name with a space"
    rc=0
    expected_hash short.txt >/dev/null 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "a short hash is not a sums line"
}

test_parse_sums_tolerates_crlf() {
    dl_setup
    write_sums "$DL/SHA256SUMS" '\r\n'
    parse_sums "$DL/SHA256SUMS"
    t_assert_eq "$A_SHA" "$(expected_hash huddle-osx-arm64.tar.gz)" "CRLF file: first line"
    t_assert_eq "$A_SHA" "$(expected_hash "my file.txt")" "CRLF file: last line, no stray CR in the name"
    t_assert_eq "$B_SHA" "$(expected_hash install.sh)" "CRLF file: star line"
}

test_parse_sums_handles_missing_final_newline() {
    dl_setup
    printf '%s  a.zip\n%s  b.zip' "$A_SHA" "$B_SHA" >"$DL/SHA256SUMS"
    parse_sums "$DL/SHA256SUMS"
    t_assert_eq "$B_SHA" "$(expected_hash b.zip)" "last line without a newline is read"
}

test_expected_hash_unlisted_fails_with_message() {
    dl_setup
    write_sums "$DL/SHA256SUMS" '\n'
    parse_sums "$DL/SHA256SUMS"
    rc=0
    out=$(expected_hash huddle-win-x64.zip 2>"$DL/err") || rc=$?
    t_assert_eq "1" "$rc" "unlisted name returns 1"
    t_assert_eq "" "$out" "nothing on stdout"
    t_assert_contains "$(cat "$DL/err")" "'huddle-win-x64.zip' is not listed in SHA256SUMS" "message"
    SUMS=""
    rc=0
    expected_hash huddle-osx-arm64.tar.gz >/dev/null 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "before parse_sums nothing is listed"
}

test_parse_sums_missing_file_fails_and_clears() {
    dl_setup
    write_sums "$DL/SHA256SUMS" '\n'
    parse_sums "$DL/SHA256SUMS"
    rc=0
    parse_sums "$DL/nope" 2>"$DL/err" || rc=$?
    t_assert_eq "1" "$rc" "missing sums file returns 1"
    t_assert_contains "$(cat "$DL/err")" "SHA256SUMS" "message names SHA256SUMS"
    rc=0
    expected_hash huddle-osx-arm64.tar.gz >/dev/null 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "the previous table is cleared"
}

test_get_verified_asset_downloads_and_verifies() {
    dl_setup
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "url:https://example.test/asset.tar.gz" "$CACHE" 2>"$DL/err") || rc=$?
    t_assert_eq "0" "$rc" "exit code"
    t_assert_eq "$CACHE/asset.tar.gz" "$out" "prints the cache path on stdout"
    t_assert_file "$CACHE/asset.tar.gz" "file in the cache"
    t_assert_eq "hello" "$(cat "$CACHE/asset.tar.gz")" "content"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "no partial left"
    t_assert_eq "1" "$(dl_calls)" "one download"
    t_assert_eq "https://example.test/asset.tar.gz|$CACHE/asset.tar.gz.partial" "$(cat "$HG_LOG")" "downloaded to NAME.partial"
    t_assert_eq "" "$(cat "$SLEEP_LOG")" "no backoff on success"
}

test_get_verified_asset_accepts_upper_case_expected_hash() {
    dl_setup
    upper=$(printf '%s' "$HELLO_SHA" | tr 'a-f' 'A-F')
    rc=0
    out=$(get_verified_asset asset.bin "$upper" "url:https://example.test/asset.bin" "$CACHE" 2>/dev/null) || rc=$?
    t_assert_eq "0" "$rc" "upper-case expected hash"
    t_assert_eq "$CACHE/asset.bin" "$out" "path"
}

test_get_verified_asset_mismatch_deletes_and_reports_both_hashes() {
    dl_setup
    HG_BODY=tampered
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "url:https://example.test/asset.tar.gz" "$CACHE" 2>"$DL/err") || rc=$?
    t_assert_eq "1" "$rc" "mismatch returns 1"
    t_assert_eq "" "$out" "nothing on stdout"
    t_assert_no_file "$CACHE/asset.tar.gz" "bad file is not kept"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "bad partial is deleted"
    err=$(cat "$DL/err")
    t_assert_contains "$err" "$HELLO_SHA" "message has the expected hash"
    t_assert_contains "$err" "$TAMPERED_SHA" "message has the actual hash"
    t_assert_contains "$err" "asset.tar.gz" "message names the asset"
}

test_get_verified_asset_cached_with_right_hash_skips_download() {
    dl_setup
    mkdir -p "$CACHE"
    printf 'hello\n' >"$CACHE/asset.tar.gz"
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "url:https://example.test/asset.tar.gz" "$CACHE" 2>/dev/null) || rc=$?
    t_assert_eq "0" "$rc" "exit code"
    t_assert_eq "$CACHE/asset.tar.gz" "$out" "path"
    t_assert_eq "0" "$(dl_calls)" "http_get was not called"
}

test_get_verified_asset_cached_with_wrong_hash_is_redownloaded() {
    dl_setup
    mkdir -p "$CACHE"
    printf 'stale corrupt bytes\n' >"$CACHE/asset.tar.gz"
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "url:https://example.test/asset.tar.gz" "$CACHE" 2>/dev/null) || rc=$?
    t_assert_eq "0" "$rc" "exit code"
    t_assert_eq "1" "$(dl_calls)" "one download"
    t_assert_eq "hello" "$(cat "$CACHE/asset.tar.gz")" "stale file replaced"
}

test_get_verified_asset_retries_with_backoff() {
    dl_setup
    HG_FAIL_FIRST=2
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "url:https://example.test/asset.tar.gz" "$CACHE" 2>/dev/null) || rc=$?
    t_assert_eq "0" "$rc" "third attempt succeeds"
    t_assert_eq "3" "$(dl_calls)" "three http_get calls"
    t_assert_eq "1${NL}3" "$(cat "$SLEEP_LOG")" "waits 1 s then 3 s"
    t_assert_eq "hello" "$(cat "$CACHE/asset.tar.gz")" "content from the good attempt"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "no partial left"
}

test_get_verified_asset_three_failures_fail() {
    dl_setup
    HG_FAIL_FIRST=3
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "url:https://example.test/asset.tar.gz" "$CACHE" 2>"$DL/err") || rc=$?
    t_assert_eq "1" "$rc" "returns 1"
    t_assert_eq "" "$out" "nothing on stdout"
    t_assert_eq "3" "$(dl_calls)" "exactly three attempts"
    t_assert_eq "1${NL}3" "$(cat "$SLEEP_LOG")" "no wait after the last attempt"
    t_assert_no_file "$CACHE/asset.tar.gz" "no final file"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "junk partial is deleted"
    t_assert_contains "$(cat "$DL/err")" "asset.tar.gz" "message names the asset"
    t_assert_contains "$(cat "$DL/err")" "3 attempt" "message says how many attempts"
}

test_get_verified_asset_file_source_is_copied() {
    dl_setup
    mkdir -p "$DL/release dir"
    printf 'hello\n' >"$DL/release dir/asset.tar.gz"
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "file:$DL/release dir/asset.tar.gz" "$CACHE" 2>/dev/null) || rc=$?
    t_assert_eq "0" "$rc" "exit code"
    t_assert_eq "$CACHE/asset.tar.gz" "$out" "path"
    t_assert_eq "hello" "$(cat "$CACHE/asset.tar.gz")" "copied"
    t_assert_file "$DL/release dir/asset.tar.gz" "the source stays in place"
    t_assert_eq "0" "$(dl_calls)" "no download for a file source"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "no partial left"
}

test_get_verified_asset_file_source_wrong_hash_fails_once() {
    dl_setup
    printf 'tampered\n' >"$DL/asset.tar.gz"
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "file:$DL/asset.tar.gz" "$CACHE" 2>"$DL/err") || rc=$?
    t_assert_eq "1" "$rc" "returns 1"
    t_assert_no_file "$CACHE/asset.tar.gz" "bad copy deleted"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "bad partial deleted"
    t_assert_file "$DL/asset.tar.gz" "the source file is never deleted"
    t_assert_eq "" "$(cat "$SLEEP_LOG")" "a file source is tried once, no backoff"
    t_assert_contains "$(cat "$DL/err")" "$HELLO_SHA" "expected hash in message"
    t_assert_contains "$(cat "$DL/err")" "$TAMPERED_SHA" "actual hash in message"
    t_assert_contains "$(cat "$DL/err")" "1 attempt" "one attempt"
}

test_get_verified_asset_missing_file_source_fails() {
    dl_setup
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "file:$DL/nope" "$CACHE" 2>"$DL/err") || rc=$?
    t_assert_eq "1" "$rc" "returns 1"
    t_assert_contains "$(cat "$DL/err")" "asset.tar.gz" "message names the asset"
    t_assert_no_file "$CACHE/asset.tar.gz.partial" "no partial left"
}

test_get_verified_asset_unknown_source_kind_fails() {
    dl_setup
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "ftp:example" "$CACHE" 2>"$DL/err") || rc=$?
    t_assert_eq "1" "$rc" "returns 1"
    t_assert_contains "$(cat "$DL/err")" "ftp:example" "message names the source"
    t_assert_eq "0" "$(dl_calls)" "nothing downloaded"
}

test_get_verified_asset_takes_resolve_asset_output() {
    dl_setup
    mkdir -p "$DL/rel"
    printf 'hello\n' >"$DL/rel/asset.tar.gz"
    parse_args --base-url "$DL/rel"
    resolve_asset asset.tar.gz
    rc=0
    out=$(get_verified_asset asset.tar.gz "$HELLO_SHA" "$ASSET_KIND:$ASSET_SRC" "$CACHE" 2>/dev/null) || rc=$?
    t_assert_eq "0" "$rc" "ASSET_KIND:ASSET_SRC is a valid SOURCE"
    t_assert_eq "$CACHE/asset.tar.gz" "$out" "path"
}

# The real seams run in a child shell that re-sources install.sh, so the fakes above do not apply.
test_http_get_runs_curl_with_fixed_flags() {
    dl_setup
    shell_path=$(command -v "$T_SHELL")
    stubs=$(make_stub_dir)
    printf '#!/bin/sh\nprintf "%%s\\n" "$@" >"%s/curl.args"\n' "$DL" >"$stubs/curl"
    chmod +x "$stubs/curl"
    rc=0
    PATH="$stubs" HUDDLE_INSTALL_NO_MAIN=1 "$shell_path" -c '. "$1"; http_get https://example.test/a "$2"' sh "$HERE/../install.sh" "$DL/out" </dev/null >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "http_get succeeds when curl does"
    t_assert_eq "-fsSL${NL}--retry${NL}0${NL}-o${NL}$DL/out${NL}https://example.test/a" "$(cat "$DL/curl.args")" "curl arguments"
}

test_sleep_seconds_runs_sleep() {
    dl_setup
    shell_path=$(command -v "$T_SHELL")
    stubs=$(make_stub_dir)
    printf '#!/bin/sh\nprintf "%%s\\n" "$1" >"%s/sleep.arg"\n' "$DL" >"$stubs/sleep"
    chmod +x "$stubs/sleep"
    PATH="$stubs" HUDDLE_INSTALL_NO_MAIN=1 "$shell_path" -c '. "$1"; sleep_seconds 3' sh "$HERE/../install.sh" </dev/null >/dev/null 2>&1 || true
    t_assert_eq "3" "$(cat "$DL/sleep.arg")" "sleep_seconds passes the seconds to sleep"
}

test_download_functions_leave_the_script_with_one_placeholder() {
    count=$(grep -o '__HUDDLE_REPO__' "$HERE/../install.sh" | wc -l | tr -d ' ')
    t_assert_eq "1" "$count" "placeholder still appears exactly once"
}

test_read_state_key_missing_file() {
    out=""
    rc=0
    out=$(read_state_key "/nonexistent/file" "app_sha256") || rc=$?
    t_assert_eq "1" "$rc" "returns 1 for missing file"
    t_assert_eq "" "$out" "prints nothing for missing file"
}

test_read_state_key_missing_key() {
    state_file=$(mktemp)
    printf "schema=1\nversion=1.0.0\n" >"$state_file"
    out=""
    rc=0
    out=$(read_state_key "$state_file" "app_sha256") || rc=$?
    t_assert_eq "1" "$rc" "returns 1 for missing key"
    t_assert_eq "" "$out" "prints nothing for missing key"
    rm -f "$state_file"
}

test_read_state_key_existing_key() {
    state_file=$(mktemp)
    printf "schema=1\napp_sha256=abc123def456\nversion=1.0.0\n" >"$state_file"
    out=$(read_state_key "$state_file" "app_sha256")
    t_assert_eq "abc123def456" "$out" "reads existing key"
    rm -f "$state_file"
}

test_write_state_key_roundtrip() {
    state_file=$(mktemp)
    write_state_key "$state_file" "schema" "1"
    write_state_key "$state_file" "version" "1.0.0"
    write_state_key "$state_file" "app_sha256" "abc123"
    v1=$(read_state_key "$state_file" "schema")
    v2=$(read_state_key "$state_file" "version")
    v3=$(read_state_key "$state_file" "app_sha256")
    t_assert_eq "1" "$v1" "schema roundtrip"
    t_assert_eq "1.0.0" "$v2" "version roundtrip"
    t_assert_eq "abc123" "$v3" "app_sha256 roundtrip"
    rm -f "$state_file"
}

test_write_state_key_keeps_others() {
    state_file=$(mktemp)
    write_state_key "$state_file" "schema" "1"
    write_state_key "$state_file" "version" "1.0.0"
    write_state_key "$state_file" "app_sha256" "old_hash"
    write_state_key "$state_file" "app_sha256" "new_hash"
    count=$(grep -c "^app_sha256=" "$state_file")
    t_assert_eq "1" "$count" "key appears exactly once"
    v1=$(read_state_key "$state_file" "schema")
    v2=$(read_state_key "$state_file" "version")
    v3=$(read_state_key "$state_file" "app_sha256")
    t_assert_eq "1" "$v1" "schema preserved"
    t_assert_eq "1.0.0" "$v2" "version preserved"
    t_assert_eq "new_hash" "$v3" "app_sha256 replaced"
    rm -f "$state_file"
}

test_write_state_key_values_with_equals() {
    state_file=$(mktemp)
    write_state_key "$state_file" "schema" "1"
    write_state_key "$state_file" "data" "key=value=extra"
    value=$(read_state_key "$state_file" "data")
    t_assert_eq "key=value=extra" "$value" "values with = are preserved"
    rm -f "$state_file"
}

test_write_state_key_no_temp_left() {
    state_file=$(mktemp)
    rm -f "$state_file"
    write_state_key "$state_file" "schema" "1"
    tmp_files=$(find "$(dirname "$state_file")" -maxdepth 1 -name "$(basename "$state_file").tmp*" 2>/dev/null | wc -l)
    t_assert_eq "0" "$tmp_files" "no .tmp files left"
    rm -f "$state_file"
}

test_step_satisfied_exact_match() {
    state_file=$(mktemp)
    write_state_key "$state_file" "app_sha256" "abc123"
    rc=0
    step_satisfied "$state_file" "app_sha256" "abc123" || rc=$?
    t_assert_eq "0" "$rc" "returns 0 on exact match"
    rm -f "$state_file"
}

test_step_satisfied_mismatch() {
    state_file=$(mktemp)
    write_state_key "$state_file" "app_sha256" "abc123"
    rc=0
    step_satisfied "$state_file" "app_sha256" "xyz789" || rc=$?
    t_assert_eq "1" "$rc" "returns 1 on mismatch"
    rm -f "$state_file"
}

test_step_satisfied_missing_key() {
    state_file=$(mktemp)
    write_state_key "$state_file" "schema" "1"
    rc=0
    step_satisfied "$state_file" "app_sha256" "abc123" || rc=$?
    t_assert_eq "1" "$rc" "returns 1 when key is missing"
    rm -f "$state_file"
}

test_state_functions_use_local_variables() {
    file="KEEP"
    key="KEEP"
    value="KEEP"
    expected="KEEP"
    tmpfile="KEEP"
    state_file=$(mktemp)
    write_state_key "$state_file" "schema" "1"
    read_state_key "$state_file" "schema" >/dev/null
    step_satisfied "$state_file" "schema" "1" >/dev/null
    t_assert_eq "KEEP" "$file" "file variable protected"
    t_assert_eq "KEEP" "$key" "key variable protected"
    t_assert_eq "KEEP" "$value" "value variable protected"
    t_assert_eq "KEEP" "$expected" "expected variable protected"
    t_assert_eq "KEEP" "$tmpfile" "tmpfile variable protected"
    rm -f "$state_file"
}

t_run test_read_state_key_missing_file
t_run test_read_state_key_missing_key
t_run test_read_state_key_existing_key
t_run test_write_state_key_roundtrip
t_run test_write_state_key_keeps_others
t_run test_write_state_key_values_with_equals
t_run test_write_state_key_no_temp_left
t_run test_step_satisfied_exact_match
t_run test_step_satisfied_mismatch
t_run test_step_satisfied_missing_key
t_run test_state_functions_use_local_variables

t_run test_file_sha256_known_vector
t_run test_file_sha256_falls_back_to_shasum
t_run test_file_sha256_without_tool_fails_with_message
t_run test_parse_sums_and_expected_hash
t_run test_parse_sums_tolerates_crlf
t_run test_parse_sums_handles_missing_final_newline
t_run test_expected_hash_unlisted_fails_with_message
t_run test_parse_sums_missing_file_fails_and_clears
t_run test_get_verified_asset_downloads_and_verifies
t_run test_get_verified_asset_accepts_upper_case_expected_hash
t_run test_get_verified_asset_mismatch_deletes_and_reports_both_hashes
t_run test_get_verified_asset_cached_with_right_hash_skips_download
t_run test_get_verified_asset_cached_with_wrong_hash_is_redownloaded
t_run test_get_verified_asset_retries_with_backoff
t_run test_get_verified_asset_three_failures_fail
t_run test_get_verified_asset_file_source_is_copied
t_run test_get_verified_asset_file_source_wrong_hash_fails_once
t_run test_get_verified_asset_missing_file_source_fails
t_run test_get_verified_asset_unknown_source_kind_fails
t_run test_get_verified_asset_takes_resolve_asset_output
t_run test_http_get_runs_curl_with_fixed_flags
t_run test_sleep_seconds_runs_sleep
t_run test_download_functions_leave_the_script_with_one_placeholder

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
