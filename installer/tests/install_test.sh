#!/bin/sh
# Tests for installer/install.sh. Run with sh or dash; macOS commands are only ever reached
# through the seam functions, which tests override after sourcing.
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
# shellcheck source=SCRIPTDIR/lib.sh
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

# shellcheck source=SCRIPTDIR/../install.sh
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
    parse_args --force --version v9.0.0
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

test_parse_args_rejects_a_version_that_is_not_latest_or_a_release_tag() {
    for bad in '../x' 'v1.2.3?x=1' 'v1.2.3#a' 'v1' 'v1.2' '' ' ' 'v1.2.3/../../x' 'v1.2.3 ' "v1.2.3${NL}x" 'latest2' 'v1.2.3-'; do
        rc=0
        err=$( (parse_args --version "$bad") 2>&1 ) || rc=$?
        t_assert_eq "3" "$rc" "--version '$bad' exits 3"
        t_assert_contains "$err" "[!!]" "--version '$bad' prints a failure line"
        t_assert_contains "$err" "fix: use latest or a version such as v1.2.3" "--version '$bad' prints the fix"
    done
    rc=0
    err=$( (HUDDLE_VERSION='v1.2.3?x=1'; export HUDDLE_VERSION; parse_args) 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "HUDDLE_VERSION with a query exits 3"
    t_assert_contains "$err" "fix: use latest or a version such as v1.2.3" "HUDDLE_VERSION prints the fix"
    rc=0
    err=$( (main --version '../x') 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "main returns 3 for a bad version"
}

test_parse_args_accepts_latest_and_release_tags() {
    for good in latest v1.2.3 1.2.3 v10.20.30 v1.2.3-rc.1 v1.2.3+build.5 v1.2.3.4; do
        rc=0
        (parse_args --version "$good") >/dev/null 2>&1 || rc=$?
        t_assert_eq "0" "$rc" "--version $good is accepted"
    done
    parse_args --version v1.2.3-rc.1
    t_assert_eq "v1.2.3-rc.1" "$OPT_VERSION" "the accepted version is kept as given"
}

test_parse_args_makes_the_roots_absolute_without_requiring_them_to_exist() {
    pa_dir=$(mktemp -d "$TEST_HOME/pa.XXXXXX")
    got=$(cd "$pa_dir" && parse_args --install-root rel/inst --data-root ./a/../b/./c/ && printf '%s|%s|%s' "$OPT_INSTALL_ROOT" "$OPT_DATA_ROOT" "$PWD")
    pa_pwd=${got##*|}
    pa_rest=${got%|*}
    t_assert_eq "$pa_pwd/rel/inst|$pa_pwd/b/c" "$pa_rest" "relative roots become absolute and . .. are collapsed"
    t_assert_no_file "$pa_pwd/rel" "nothing is created"
    parse_args --install-root /opt/hi/ --data-root /opt//hd/./x/..
    t_assert_eq "/opt/hi" "$OPT_INSTALL_ROOT" "a trailing slash is dropped"
    t_assert_eq "/opt/hd" "$OPT_DATA_ROOT" "slashes and dots are collapsed in an absolute root"
    got=$(cd "$pa_dir" && HUDDLE_INSTALL_ROOT=envrel HUDDLE_DATA_ROOT=../envdata && export HUDDLE_INSTALL_ROOT HUDDLE_DATA_ROOT && parse_args && printf '%s|%s|%s' "$OPT_INSTALL_ROOT" "$OPT_DATA_ROOT" "$PWD")
    pa_pwd=${got##*|}
    pa_rest=${got%|*}
    t_assert_eq "$pa_pwd/envrel|$(dirname "$pa_pwd")/envdata" "$pa_rest" "HUDDLE_*_ROOT values are made absolute too"
}

test_parse_args_refuses_an_empty_or_root_folder() {
    for flag in --install-root --data-root; do
        for bad in '' / // /. /..; do
            rc=0
            err=$( (parse_args "$flag" "$bad") 2>&1 ) || rc=$?
            t_assert_eq "3" "$rc" "$flag '$bad' exits 3"
            t_assert_contains "$err" "[!!]" "$flag '$bad' prints a failure line"
            t_assert_contains "$err" "$flag" "$flag '$bad' names the option"
        done
    done
    rc=0
    (HUDDLE_INSTALL_ROOT=/; export HUDDLE_INSTALL_ROOT; parse_args) >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "HUDDLE_INSTALL_ROOT=/ exits 3"
    rc=0
    (HUDDLE_DATA_ROOT=/; export HUDDLE_DATA_ROOT; parse_args) >/dev/null 2>&1 || rc=$?
    t_assert_eq "3" "$rc" "HUDDLE_DATA_ROOT=/ exits 3"
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
    (HUDDLE_REPO="owner/repo"; main --base-url "$TEST_HOME/assets" --uninstall) >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "main exit code (uninstall of nothing)"
}

test_main_unreplaced_placeholder_exits_3() {
    rc=0
    err=$( (HUDDLE_REPO="__HUDDLE_REPO__"; main) 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "placeholder exit code"
    t_assert_contains "$err" "release" "run-from-a-release message"
    rc=0
    (HUDDLE_REPO="__HUDDLE_REPO__"; main --base-url "$TEST_HOME" --uninstall) >/dev/null 2>&1 || rc=$?
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
    HUDDLE_INSTALL_NO_MAIN= HUDDLE_REPO="owner/repo" "$T_SHELL" "$HERE/../install.sh" --base-url "$TEST_HOME" --uninstall >/dev/null 2>&1 || rc=$?
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
    # Fake sleep seam: records the requested seconds instead of waiting. Defined here, not at the top
    # level, because the ap_* sandboxes install their own sleep_seconds fake.
    sleep_seconds() {
        printf '%s\n' "$1" >>"$SLEEP_LOG"
    }
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

# ---- Task 32: stop_huddle_if_running, ensure_signed and install_app -----------------------------

# Test doubles for the seams. Every call is appended to $AP_CALLS (a file, because some seams run in
# a pipeline subshell). ap_setup installs them again, so a test that needs a real seam calls
# ap_real_seams afterwards, which sources install.sh a second time.
ap_stubs() {
    run_codesign() {
        printf 'codesign %s\n' "$*" >>"$AP_CALLS"
        case $* in
            "--verify "*x.dylib) return 1 ;;
            "--force "*) return "${AP_SIGN_RC:-0}" ;;
        esac
        return 0
    }
    run_xattr() {
        printf 'xattr %s\n' "$*" >>"$AP_CALLS"
    }
    is_macho() {
        case $1 in
            *.dylib | */Huddle.App) return 0 ;;
        esac
        return 1
    }
    run_huddle_stop() {
        if [ -e "$INSTALL_ROOT.old" ]; then
            printf 'stop old_present=1\n' >>"$AP_CALLS"
        else
            printf 'stop old_present=0\n' >>"$AP_CALLS"
        fi
        return "${AP_STOP_RC:-0}"
    }
    process_running() {
        [ "${AP_ALIVE:-0}" -eq 1 ]
    }
    run_kill_process() {
        printf 'kill %s\n' "$1" >>"$AP_CALLS"
    }
    app_running() {
        process_running "$1"
    }
    process_command_of() {
        printf '%s\n' "${AP_COMM-${APP_EXE##*/}}"
    }
    sleep_seconds() {
        printf 'sleep %s\n' "$1" >>"$AP_CALLS"
    }
}

# ap_child <script line>...: runs the lines in a child shell that sourced the real install.sh and
# resolved the sandbox paths, so a test can reach a real seam without losing the fakes of this
# file. Prints the child's stdout and stderr.
ap_child() {
    {
        printf 'HUDDLE_INSTALL_NO_MAIN=1\n. "%s/../install.sh"\n' "$HERE"
        printf 'parse_args --install-root "%s" --data-root "%s"\nresolve_paths\n' "$INSTALL_ROOT" "$DATA_ROOT"
        printf '%s\n' "$@"
    } >"$AP/child.sh"
    ap_shell=$(command -v "$T_SHELL")
    "$ap_shell" "$AP/child.sh" </dev/null 2>&1 || true
}

# A fresh sandbox with paths that contain spaces: $AP, the call log and the resolved paths.
ap_setup() {
    AP=$(mktemp -d "$TEST_HOME/ap.XXXXXX")
    AP_CALLS="$AP/calls"
    : >"$AP_CALLS"
    AP_STOP_RC=0
    AP_ALIVE=0
    AP_SIGN_RC=0
    unset AP_COMM
    APP_CHANGED=unset
    ap_stubs
    parse_args --install-root "$AP/root dir/Agency Huddle" --data-root "$AP/data dir"
    resolve_paths
}

# ap_build_archive <tag> [nohost]: builds $AP_ARCHIVE the way the release workflow lays it out.
# Files are created without the execute bit, so the install must set it.
ap_build_archive() {
    ap_src="$AP/src"
    rm -rf "$ap_src"
    mkdir -p "$ap_src/app" "$ap_src/tools/acp"
    if [ "${2:-}" != nohost ]; then
        # The shebang makes `[ -x ]` meaningful on Windows (Git Bash infers it from the shebang).
        printf '#!/bin/sh\n# host %s\n' "$1" >"$ap_src/app/Huddle.App"
    fi
    printf 'lib %s\n' "$1" >"$ap_src/app/libx.dylib"
    printf '{}\n' >"$ap_src/tools/acp/package.json"
    printf '{}\n' >"$ap_src/tools/acp/package-lock.json"
    printf '{"version": "%s", "nodeVersion": "v24.21.0"}\n' "$1" >"$ap_src/release-info.json"
    AP_ARCHIVE="$AP/huddle-osx-arm64.tar.gz"
    rm -f "$AP_ARCHIVE"
    tar -czf "$AP_ARCHIVE" -C "$ap_src" .
}

# Installs version <tag> and then drops the markers a later upgrade has to keep.
ap_install_with_markers() {
    ap_build_archive "$1"
    install_app "$AP_ARCHIVE" >"$AP/first.out" 2>&1 || true
    mkdir -p "$INSTALL_ROOT/node/bin" "$INSTALL_ROOT/tools/acp/node_modules" "$INSTALL_ROOT/bin"
    printf 'node-marker\n' >"$INSTALL_ROOT/node/bin/node"
    printf 'module-marker\n' >"$INSTALL_ROOT/tools/acp/node_modules/marker"
    printf 'launcher-marker\n' >"$INSTALL_ROOT/bin/huddle"
    printf 'schema=1\nversion=%s\n' "$1" >"$STATE_FILE"
    : >"$AP_CALLS"
}

ap_instance_json() {
    mkdir -p "$DATA_ROOT/data"
    printf '{"url":"http://127.0.0.1:5000","stopToken":"tok","pid":%s}\n' "$1" >"$DATA_ROOT/data/instance.json"
}

ap_count_calls() {
    ap_n=$(grep -c "$1" "$AP_CALLS" || true)
    printf '%s' "$ap_n"
}

test_t_run_honours_t_only_and_prints_run_lines_only_for_failures() {
    ap_dir=$(mktemp -d "$TEST_HOME/tonly.XXXXXX")
    {
        printf '. "%s/lib.sh"\n' "$HERE"
        printf 'test_alpha() { t_assert_eq 1 1 alpha-check; }\n'
        printf 'test_beta() { t_assert_eq 1 2 beta-check; }\n'
        printf 't_run test_alpha\nt_run test_beta\nt_summary\n'
    } >"$ap_dir/child.sh"
    shell_path=$(command -v "$T_SHELL")
    only_alpha=$(T_ONLY=alpha "$shell_path" "$ap_dir/child.sh" </dev/null 2>&1 || true)
    only_beta=$(T_ONLY=beta "$shell_path" "$ap_dir/child.sh" </dev/null 2>&1 || true)
    everything=$(T_ONLY= "$shell_path" "$ap_dir/child.sh" </dev/null 2>&1 || true)
    t_assert_eq "1 passed, 0 failed" "$only_alpha" "T_ONLY=alpha runs one passing test and prints no RUN line"
    t_assert_contains "$only_beta" "RUN  test_beta" "a failing test prints its RUN line"
    t_assert_contains "$only_beta" "FAIL [test_beta]" "a failing test prints its FAIL line"
    t_assert_not_contains "$only_beta" "test_alpha" "T_ONLY=beta skips alpha"
    t_assert_contains "$everything" "1 passed, 1 failed" "an empty T_ONLY runs everything"
    t_assert_not_contains "$everything" "RUN  test_alpha" "a passing test prints no RUN line"
}

test_install_app_fresh_install() {
    ap_setup
    ap_build_archive v1
    chmod() {
        printf 'chmod %s\n' "$*" >>"$AP_CALLS"
        command chmod "$@"
    }
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    unset -f chmod
    t_assert_eq "0" "$rc" "install_app succeeds"
    t_assert_eq "1" "$APP_CHANGED" "APP_CHANGED is 1 after an install"
    if [ -x "$APP_EXE" ]; then t_pass; else t_fail "app/Huddle.App must be executable"; fi
    t_assert_eq "chmod +x $INSTALL_ROOT.staging/app/Huddle.App" "$(grep '^chmod' "$AP_CALLS")" "the host is made executable (Windows cannot show the bit)"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the host comes from the archive"
    t_assert_file "$INSTALL_ROOT/app/libx.dylib" "the dylib is installed"
    t_assert_file "$INSTALL_ROOT/tools/acp/package.json" "package.json is installed"
    t_assert_file "$INSTALL_ROOT/tools/acp/package-lock.json" "package-lock.json is installed"
    t_assert_file "$INSTALL_ROOT/release-info.json" "release-info.json is installed"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old folder is left"
    t_assert_no_file "$STATE_FILE" "install_app does not write the state"
    t_assert_contains "$(cat "$AP/out")" "[ok] app" "the step is reported"
}

test_install_app_clears_quarantine_on_the_install_root() {
    ap_setup
    ap_build_archive v1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    t_assert_eq "xattr -dr com.apple.quarantine $INSTALL_ROOT" "$(grep '^xattr' "$AP_CALLS")" "xattr arguments"
    t_assert_eq "1" "$(ap_count_calls '^xattr')" "xattr runs once"
}

test_install_app_signs_only_macho_files_that_fail_verification() {
    ap_setup
    ap_build_archive v1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    expected_verify="codesign --verify $INSTALL_ROOT.staging/app/Huddle.App${NL}codesign --verify $INSTALL_ROOT.staging/app/libx.dylib"
    t_assert_eq "$expected_verify" "$(grep -- '--verify' "$AP_CALLS" | sort)" "only the Mach-O files are verified"
    t_assert_eq "codesign --force --sign - $INSTALL_ROOT.staging/app/libx.dylib" "$(grep -- '--force' "$AP_CALLS")" "only the file that failed verification is signed"
}

test_ensure_signed_handles_spaces_and_reports_a_signing_failure() {
    ap_setup
    mkdir -p "$AP/with space/inner dir"
    printf 'x\n' >"$AP/with space/inner dir/my x.dylib"
    printf 'x\n' >"$AP/with space/inner dir/notes.txt"
    rc=0
    ensure_signed "$AP/with space" || rc=$?
    t_assert_eq "0" "$rc" "ensure_signed succeeds"
    t_assert_eq "codesign --force --sign - $AP/with space/inner dir/my x.dylib" "$(grep -- '--force' "$AP_CALLS")" "the path with spaces is passed whole"
    t_assert_eq "0" "$(ap_count_calls 'notes.txt')" "a non Mach-O file is left alone"
    AP_SIGN_RC=1
    rc=0
    ensure_signed "$AP/with space" 2>"$AP/err" || rc=$?
    t_assert_eq "1" "$rc" "a signing failure is returned"
    t_assert_contains "$(cat "$AP/err")" "could not sign" "and explained"
}

test_install_app_second_run_with_matching_state_does_nothing() {
    ap_setup
    ap_build_archive v1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    write_state_key "$STATE_FILE" app_sha256 "$(file_sha256 "$AP_ARCHIVE")"
    ap_instance_json 4242
    : >"$AP_CALLS"
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out2" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "the skip succeeds"
    t_assert_eq "0" "$APP_CHANGED" "APP_CHANGED is 0 when skipped"
    t_assert_contains "$(cat "$AP/out2")" "[skip] app" "the skip is reported"
    t_assert_eq "" "$(cat "$AP_CALLS")" "no stop, no signing, no xattr"
}

test_install_app_reinstalls_when_the_host_is_missing_or_the_hash_differs() {
    ap_setup
    ap_build_archive v1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    write_state_key "$STATE_FILE" app_sha256 "$(file_sha256 "$AP_ARCHIVE")"
    rm -f "$APP_EXE"
    install_app "$AP_ARCHIVE" >"$AP/out2" 2>&1 || true
    t_assert_eq "1" "$APP_CHANGED" "a missing host is reinstalled although the state matches"
    t_assert_file "$APP_EXE" "and the host is back"
    write_state_key "$STATE_FILE" app_sha256 "0000"
    install_app "$AP_ARCHIVE" >"$AP/out3" 2>&1 || true
    t_assert_eq "1" "$APP_CHANGED" "a different recorded hash is reinstalled"
}

test_install_app_upgrade_keeps_node_modules_state_and_launcher() {
    ap_setup
    ap_install_with_markers v1
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "the upgrade succeeds"
    t_assert_eq "1" "$APP_CHANGED" "APP_CHANGED is 1"
    t_assert_eq "# host v2" "$(tail -1 "$APP_EXE")" "the host is the new one"
    t_assert_eq "node-marker" "$(cat "$INSTALL_ROOT/node/bin/node")" "node/ is preserved"
    t_assert_eq "module-marker" "$(cat "$INSTALL_ROOT/tools/acp/node_modules/marker")" "node_modules is preserved"
    t_assert_eq "launcher-marker" "$(cat "$INSTALL_ROOT/bin/huddle")" "bin/ is preserved"
    t_assert_eq "version=v1" "$(grep '^version=' "$STATE_FILE")" "install-state is carried over"
    t_assert_file "$INSTALL_ROOT/tools/acp/package-lock.json" "the new lock file is there"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old folder is left"
}

test_install_app_archive_without_host_fails_and_changes_nothing() {
    ap_setup
    ap_install_with_markers v1
    ap_build_archive v2 nohost
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "install_app fails"
    t_assert_eq "0" "$APP_CHANGED" "APP_CHANGED stays 0"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the old host is untouched"
    t_assert_eq "node-marker" "$(cat "$INSTALL_ROOT/node/bin/node")" "node/ is untouched"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old folder is left"
    t_assert_contains "$(cat "$AP/out")" "app/Huddle.App" "the missing file is named"
    t_assert_contains "$(cat "$AP/out")" "fix:" "with a fix line"
    t_assert_eq "0" "$(ap_count_calls '^stop')" "the app was not stopped for a bad archive"
}

test_install_app_corrupt_archive_fails_and_changes_nothing() {
    ap_setup
    ap_install_with_markers v1
    printf 'not a tarball\n' >"$AP/bad.tar.gz"
    rc=0
    install_app "$AP/bad.tar.gz" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "install_app fails"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the old host is untouched"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
}

test_install_app_failed_swap_rolls_back() {
    ap_setup
    ap_install_with_markers v1
    ap_build_archive v2
    mv() {
        if [ "$1" = "$INSTALL_ROOT.staging" ] && [ "$2" = "$INSTALL_ROOT" ]; then
            return 1
        fi
        command mv "$@"
    }
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    unset -f mv
    t_assert_eq "1" "$rc" "install_app fails"
    t_assert_eq "0" "$APP_CHANGED" "APP_CHANGED stays 0"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the previous host is back"
    t_assert_eq "node-marker" "$(cat "$INSTALL_ROOT/node/bin/node")" "node/ is back"
    t_assert_eq "module-marker" "$(cat "$INSTALL_ROOT/tools/acp/node_modules/marker")" "node_modules is back"
    t_assert_eq "launcher-marker" "$(cat "$INSTALL_ROOT/bin/huddle")" "bin/ is back"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old folder is left"
    t_assert_contains "$(cat "$AP/out")" "[!!] app" "the failure is reported"
}

test_install_app_recovers_a_swap_that_died_between_its_renames() {
    ap_setup
    ap_install_with_markers v1
    mv "$INSTALL_ROOT" "$INSTALL_ROOT.old"
    mkdir -p "$INSTALL_ROOT.staging"
    printf 'junk\n' >"$INSTALL_ROOT.staging/junk"
    printf '{}\n' >"$INSTALL_ROOT.staging/release-info.json"
    mkdir -p "$INSTALL_ROOT.staging/app"
    printf 'host\n' >"$INSTALL_ROOT.staging/app/Huddle.App"
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "install_app succeeds"
    t_assert_eq "# host v2" "$(tail -1 "$APP_EXE")" "the new host is installed"
    t_assert_eq "node-marker" "$(cat "$INSTALL_ROOT/node/bin/node")" "node/ came back from .old"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old folder is left"
}

test_install_app_stops_a_running_instance_before_the_swap() {
    ap_setup
    ap_install_with_markers v1
    ap_instance_json 4242
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "install_app succeeds"
    t_assert_eq "1" "$(ap_count_calls '^stop old_present=0$')" "stop ran once, before the old folder was renamed"
    t_assert_eq "xattr" "$(tail -1 "$AP_CALLS" | cut -d' ' -f1)" "quarantine is cleared last"
}

test_install_app_fresh_install_never_stops() {
    ap_setup
    ap_instance_json 4242
    ap_build_archive v1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    t_assert_eq "0" "$(ap_count_calls '^stop')" "nothing to stop when there is no install yet"
    t_assert_eq "1" "$APP_CHANGED" "and the install happened"
}

test_install_app_survivor_aborts_the_upgrade_untouched() {
    ap_setup
    ap_install_with_markers v1
    ap_instance_json 4242
    AP_ALIVE=1
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "install_app fails"
    t_assert_eq "0" "$APP_CHANGED" "APP_CHANGED stays 0"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the old host is untouched"
    t_assert_eq "node-marker" "$(cat "$INSTALL_ROOT/node/bin/node")" "node/ is untouched"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is left"
    t_assert_contains "$(cat "$AP/out")" "--force" "the fix names --force"
}

# ap_assert_refused <label>: the last install_app was refused by the root guard with the fix text.
ap_assert_refused() {
    t_assert_eq "1" "$rc" "$1: install_app fails"
    t_assert_eq "0" "$APP_CHANGED" "$1: APP_CHANGED stays 0"
    t_assert_contains "$(cat "$AP/out")" "[!!] app" "$1: a failure line"
    t_assert_contains "$(cat "$AP/out")" "fix: choose an empty folder or remove it" "$1: the fix"
    t_assert_eq "0" "$(ap_count_calls '^stop')" "$1: the app was not stopped"
}

test_install_app_refuses_an_install_root_that_is_not_a_huddle_install() {
    ap_setup
    mkdir -p "$INSTALL_ROOT/sub"
    printf 'precious\n' >"$INSTALL_ROOT/precious.txt"
    printf 'deep\n' >"$INSTALL_ROOT/sub/deep.txt"
    ap_build_archive v1
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    ap_assert_refused "foreign folder"
    t_assert_eq "precious" "$(cat "$INSTALL_ROOT/precious.txt")" "the foreign file is untouched"
    t_assert_eq "deep" "$(cat "$INSTALL_ROOT/sub/deep.txt")" "the foreign subfolder is untouched"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old folder is created"
    t_assert_no_file "$INSTALL_ROOT.staging" "no staging folder is created"
    t_assert_no_file "$INSTALL_ROOT/app" "nothing is installed into it"
}

test_install_app_refuses_an_install_root_that_is_a_file() {
    ap_setup
    mkdir -p "$(dirname "$INSTALL_ROOT")"
    printf 'a file\n' >"$INSTALL_ROOT"
    ap_build_archive v1
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    ap_assert_refused "file as root"
    t_assert_eq "a file" "$(cat "$INSTALL_ROOT")" "the file is untouched"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old"
}

test_install_app_accepts_an_empty_install_root() {
    ap_setup
    mkdir -p "$INSTALL_ROOT"
    ap_build_archive v1
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "an empty folder is accepted: $(cat "$AP/out")"
    t_assert_eq "1" "$APP_CHANGED" "APP_CHANGED"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the host is installed"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old left"
}

test_install_app_upgrades_a_folder_with_either_marker() {
    ap_setup
    ap_install_with_markers v1
    rm -f "$STATE_FILE"
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "release-info.json alone is a marker: $(cat "$AP/out")"
    t_assert_eq "# host v2" "$(tail -1 "$APP_EXE")" "upgraded"
    rm -f "$INSTALL_ROOT/release-info.json"
    printf 'schema=1\n' >"$STATE_FILE"
    ap_build_archive v3
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "install-state alone is a marker: $(cat "$AP/out")"
    t_assert_eq "# host v3" "$(tail -1 "$APP_EXE")" "upgraded again"
    t_assert_eq "module-marker" "$(cat "$INSTALL_ROOT/tools/acp/node_modules/marker")" "node_modules carried over"
}

test_install_app_does_not_delete_an_unmarked_sibling_old_or_staging() {
    ap_setup
    ap_install_with_markers v1
    mkdir -p "$INSTALL_ROOT.old"
    printf 'keep\n' >"$INSTALL_ROOT.old/keep.txt"
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    ap_assert_refused "unmarked .old"
    t_assert_eq "keep" "$(cat "$INSTALL_ROOT.old/keep.txt")" "the unmarked .old is not deleted"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the install is untouched"
    rm -rf "$INSTALL_ROOT.old"
    mkdir -p "$INSTALL_ROOT.staging"
    printf 'keep\n' >"$INSTALL_ROOT.staging/keep.txt"
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    ap_assert_refused "unmarked .staging"
    t_assert_eq "keep" "$(cat "$INSTALL_ROOT.staging/keep.txt")" "the unmarked .staging is not deleted"
    t_assert_eq "# host v1" "$(tail -1 "$APP_EXE")" "the install is still untouched"
}

test_install_app_does_not_restore_an_unmarked_old_into_a_missing_root() {
    ap_setup
    mkdir -p "$INSTALL_ROOT.old"
    printf 'keep\n' >"$INSTALL_ROOT.old/keep.txt"
    ap_build_archive v1
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    ap_assert_refused "unmarked .old, no root"
    t_assert_eq "keep" "$(cat "$INSTALL_ROOT.old/keep.txt")" "the folder is left alone"
    t_assert_no_file "$INSTALL_ROOT" "and not renamed into place"
}

test_install_app_deletes_a_marked_leftover_old_and_staging() {
    ap_setup
    ap_install_with_markers v1
    mkdir -p "$INSTALL_ROOT.old/app" "$INSTALL_ROOT.staging"
    printf '{}\n' >"$INSTALL_ROOT.old/release-info.json"
    printf 'host\n' >"$INSTALL_ROOT.old/app/Huddle.App"
    printf 'schema=1\n' >"$INSTALL_ROOT.staging/install-state"
    ap_build_archive v2
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "marked leftovers are cleaned: $(cat "$AP/out")"
    t_assert_no_file "$INSTALL_ROOT.old" "no .old left"
    t_assert_no_file "$INSTALL_ROOT.staging" "no .staging left"
    t_assert_eq "# host v2" "$(tail -1 "$APP_EXE")" "upgraded"
}

test_install_app_refuses_the_home_folder_even_with_a_marker() {
    ap_setup
    printf 'schema=1\n' >"$HOME/install-state"
    INSTALL_ROOT=$HOME
    ap_build_archive v1
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    rm -f "$HOME/install-state"
    ap_assert_refused "HOME as root"
    t_assert_no_file "$HOME.old" "HOME is not moved aside"
    t_assert_no_file "$HOME.staging" "no staging beside HOME"
}

test_install_root_check_applies_the_safe_path_rules() {
    ap_setup
    rc=0
    install_root_check /opt/huddle >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "a missing two-segment root such as /opt/huddle is accepted"
    for bad in "" / /huddle relative/dir "$HOME" "$(dirname "$HOME")"; do
        rc=0
        install_root_check "$bad" >/dev/null 2>&1 || rc=$?
        t_assert_eq "1" "$rc" "'$bad' is refused"
        t_assert_contains "${SAFE_REASON:-}" " " "'$bad' sets a reason"
    done
    rc=0
    install_root_check "$HOME/nested/deep" >/dev/null 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "a folder below HOME is accepted"
}

test_install_app_with_force_reinstalls_although_the_state_matches() {
    ap_setup
    ap_build_archive v1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    write_state_key "$STATE_FILE" app_sha256 "$(file_sha256 "$AP_ARCHIVE")"
    OPT_FORCE=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    t_assert_eq "0" "$APP_CHANGED" "without --force the matching state is a skip"
    OPT_FORCE=1
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    OPT_FORCE=0
    t_assert_eq "1" "$APP_CHANGED" "--force reinstalls"
    t_assert_contains "$(cat "$AP/out")" "[ok] app" "and says so"
}

test_stop_waits_for_the_process_to_exit_after_the_stop_request() {
    ap_setup
    ap_instance_json 4242
    AP_POLLS=0
    app_running() {
        AP_POLLS=$((AP_POLLS + 1))
        [ "$AP_POLLS" -le 3 ]
    }
    OPT_FORCE=0
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "a process that exits during the wait is not an error: $(cat "$AP/out")"
    t_assert_eq "3" "$(ap_count_calls '^sleep 1$')" "one 1 s sleep per poll that still saw the process"
    t_assert_eq "0" "$(ap_count_calls '^kill')" "nothing is killed"
    t_assert_contains "$(cat "$AP/out")" "[ok] stop" "the stop is reported"
}

test_stop_gives_up_after_ten_seconds_and_names_the_force_switches() {
    ap_setup
    ap_instance_json 4242
    AP_ALIVE=1
    OPT_FORCE=0
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "a survivor fails"
    t_assert_eq "10" "$(ap_count_calls '^sleep 1$')" "the wait is ten 1 s polls"
    t_assert_contains "$(cat "$AP/out")" "--force" "the fix names --force"
    t_assert_contains "$(cat "$AP/out")" "HUDDLE_FORCE=1" "and HUDDLE_FORCE=1"
    t_assert_eq "0" "$(ap_count_calls '^kill')" "nothing is killed"
}

test_stop_without_a_survivor_does_not_sleep() {
    ap_setup
    ap_instance_json 4242
    OPT_FORCE=0
    stop_huddle_if_running >"$AP/out" 2>&1 || true
    t_assert_eq "0" "$(ap_count_calls '^sleep')" "a process that is already gone costs no wait"
}

test_app_running_seam_sees_live_and_dead_processes() {
    ap_setup
    out=$(ap_child 'app_running $$ && echo live' 'app_running 2147483646 || echo dead')
    t_assert_eq "live${NL}dead" "$out" "app_running reports whether the pid exists"
}

test_stop_without_an_instance_record_never_calls_stop() {
    ap_setup
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "returns 0"
    t_assert_eq "" "$(cat "$AP_CALLS")" "run_huddle_stop was not called"
    t_assert_eq "" "$(cat "$AP/out")" "and nothing was printed"
}

test_stop_with_an_instance_record_calls_stop() {
    ap_setup
    ap_instance_json 4242
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "returns 0 once the process is gone"
    t_assert_eq "stop old_present=0" "$(cat "$AP_CALLS")" "run_huddle_stop was called once"
    t_assert_contains "$(cat "$AP/out")" "[ok] stop" "the stop is reported"
}

test_stop_survivor_fails_without_force() {
    ap_setup
    ap_instance_json 4242
    AP_ALIVE=1
    OPT_FORCE=0
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "returns 1"
    t_assert_contains "$(cat "$AP/out")" "pid 4242" "the pid is named"
    t_assert_contains "$(cat "$AP/out")" "--force" "the fix names --force"
    t_assert_eq "0" "$(ap_count_calls '^kill')" "nothing is killed"
}

test_stop_survivor_is_ended_with_force() {
    ap_setup
    ap_instance_json 4242
    AP_ALIVE=1
    OPT_FORCE=1
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "returns 0"
    t_assert_eq "kill 4242" "$(grep '^kill' "$AP_CALLS")" "the survivor is killed"
    t_assert_no_file "$DATA_ROOT/data/instance.json" "the stale record is removed"
}

test_stop_failed_request_without_a_pid_decides_by_exit_code() {
    ap_setup
    mkdir -p "$DATA_ROOT/data"
    printf 'garbage\n' >"$DATA_ROOT/data/instance.json"
    AP_STOP_RC=1
    OPT_FORCE=0
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "1" "$rc" "a failed stop request without a pid fails"
    OPT_FORCE=1
    rc=0
    stop_huddle_if_running >"$AP/out2" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "--force continues"
    AP_STOP_RC=0
    OPT_FORCE=0
    rc=0
    stop_huddle_if_running >"$AP/out3" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "a clean stop request without a pid is fine"
}

test_stop_never_waits_for_or_kills_an_unrelated_process_behind_a_stale_pid() {
    ap_setup
    ap_instance_json 4242
    AP_ALIVE=1
    AP_COMM=zsh
    OPT_FORCE=1
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    OPT_FORCE=0
    t_assert_eq "0" "$rc" "a stale record is not an error: $(cat "$AP/out")"
    t_assert_eq "0" "$(ap_count_calls '^sleep')" "the unrelated process is not waited for"
    t_assert_eq "0" "$(ap_count_calls '^kill')" "the unrelated process is not killed, even with --force"
    t_assert_no_file "$DATA_ROOT/data/instance.json" "the stale record is removed"
    t_assert_contains "$(cat "$AP/out")" "process 4242 is not Agency Huddle" "and the output says why"
    ap_instance_json 4242
    AP_COMM=""
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "a pid that names no process is stale too"
    t_assert_eq "0" "$(ap_count_calls '^sleep')" "and costs no wait"
}

test_stop_still_waits_for_and_kills_the_real_app_pid() {
    ap_setup
    ap_instance_json 4242
    AP_ALIVE=1
    AP_COMM=Huddle.App
    OPT_FORCE=1
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    OPT_FORCE=0
    t_assert_eq "0" "$rc" "the survivor is ended: $(cat "$AP/out")"
    t_assert_eq "10" "$(ap_count_calls '^sleep 1$')" "the app pid is waited for"
    t_assert_eq "kill 4242" "$(grep '^kill' "$AP_CALLS")" "and killed with --force"
}

test_stop_skips_the_pid_wait_when_the_stop_verb_succeeded_and_removed_the_record() {
    ap_setup
    ap_instance_json 4242
    AP_ALIVE=1
    run_huddle_stop() {
        printf 'stop\n' >>"$AP_CALLS"
        rm -f "$DATA_ROOT/data/instance.json"
        return 0
    }
    OPT_FORCE=0
    rc=0
    stop_huddle_if_running >"$AP/out" 2>&1 || rc=$?
    t_assert_eq "0" "$rc" "a stop that cleaned up after itself succeeds: $(cat "$AP/out")"
    t_assert_eq "0" "$(ap_count_calls '^sleep')" "no wait on the pid"
    t_assert_eq "0" "$(ap_count_calls '^kill')" "no kill"
}

test_process_command_of_prints_the_executable_name_from_ps() {
    ap_setup
    mkdir -p "$AP/bin"
    printf '#!/bin/sh\nif [ "$1 $2 $3" = "-p 77 -o" ] && [ "$4" = "comm=" ]; then\n    printf "  /Applications/My Apps/Huddle.App  \\n"\n    exit 0\nfi\nexit 1\n' >"$AP/bin/ps"
    chmod +x "$AP/bin/ps"
    out=$(ap_child "PATH=\"$AP/bin:\$PATH\"" 'printf "[%s]\n" "$(process_command_of 77)"' 'printf "[%s]\n" "$(process_command_of 99)"')
    t_assert_eq "[Huddle.App]${NL}[]" "$out" "the base name of the command, and nothing for a missing process"
}

test_resolve_asset_adds_the_v_of_the_release_tag_to_a_bare_version() {
    HUDDLE_REPO="owner/repo"
    parse_args --version 1.0.0
    resolve_asset "a.tgz"
    t_assert_eq "https://github.com/owner/repo/releases/download/v1.0.0/a.tgz" "$ASSET_SRC" "bare version"
    parse_args --version v1.0.0
    resolve_asset "a.tgz"
    t_assert_eq "https://github.com/owner/repo/releases/download/v1.0.0/a.tgz" "$ASSET_SRC" "tagged version unchanged"
    parse_args --version 1.2.3-rc.1
    resolve_asset "a.tgz"
    t_assert_eq "https://github.com/owner/repo/releases/download/v1.2.3-rc.1/a.tgz" "$ASSET_SRC" "bare prerelease"
    parse_args --version latest
    resolve_asset "a.tgz"
    t_assert_eq "https://github.com/owner/repo/releases/latest/download/a.tgz" "$ASSET_SRC" "latest unchanged"
}

test_folder_is_replaceable_needs_the_host_next_to_release_info() {
    ap_setup
    ap_dir="$AP/folder"
    mkdir -p "$ap_dir"
    rc=0
    folder_is_replaceable "$ap_dir" || rc=$?
    t_assert_eq "0" "$rc" "an empty folder is replaceable"
    printf '{}\n' >"$ap_dir/release-info.json"
    rc=0
    folder_is_replaceable "$ap_dir" || rc=$?
    t_assert_eq "1" "$rc" "a folder with only release-info.json is not ours"
    mkdir -p "$ap_dir/app"
    printf 'host\n' >"$ap_dir/app/Huddle.App"
    rc=0
    folder_is_replaceable "$ap_dir" || rc=$?
    t_assert_eq "0" "$rc" "release-info.json with app/Huddle.App is replaceable"
    rm -rf "$ap_dir"
    mkdir -p "$ap_dir"
    printf 'schema=1\n' >"$ap_dir/install-state"
    rc=0
    folder_is_replaceable "$ap_dir" || rc=$?
    t_assert_eq "0" "$rc" "install-state alone is replaceable"
}

test_install_app_refuses_a_folder_with_only_a_release_info_file() {
    ap_setup
    mkdir -p "$INSTALL_ROOT"
    printf '{}\n' >"$INSTALL_ROOT/release-info.json"
    ap_build_archive v1
    rc=0
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || rc=$?
    ap_assert_refused "lone release-info.json"
    t_assert_file "$INSTALL_ROOT/release-info.json" "the folder is untouched"
    t_assert_no_file "$INSTALL_ROOT.old" "nothing was renamed to .old"
}

test_run_huddle_stop_prefers_the_launcher() {
    ap_setup
    mkdir -p "$INSTALL_ROOT/bin" "$INSTALL_ROOT/app"
    printf '#!/bin/sh\nprintf "launcher %%s\\n" "$*" >"%s/who"\nexit 7\n' "$AP" >"$INSTALL_ROOT/bin/huddle"
    printf '#!/bin/sh\nprintf "host %%s\\n" "$*" >"%s/who"\n' "$AP" >"$APP_EXE"
    chmod +x "$INSTALL_ROOT/bin/huddle" "$APP_EXE"
    out=$(ap_child 'rc=0' 'run_huddle_stop || rc=$?' 'echo "rc=$rc"')
    t_assert_eq "rc=7" "$out" "the launcher's exit code is returned"
    t_assert_eq "launcher stop" "$(cat "$AP/who")" "the launcher gets the stop verb"
}

test_run_huddle_stop_falls_back_to_the_host_with_the_data_dir() {
    ap_setup
    mkdir -p "$INSTALL_ROOT/app"
    printf '#!/bin/sh\nprintf "%%s|%%s\\n" "$1" "$Team__DataDir" >"%s/who"\n' "$AP" >"$APP_EXE"
    chmod +x "$APP_EXE"
    out=$(ap_child 'rc=0' 'run_huddle_stop || rc=$?' 'echo "rc=$rc"')
    t_assert_eq "rc=0" "$out" "the host's exit code is returned"
    t_assert_eq "stop|$DATA_ROOT/data" "$(cat "$AP/who")" "stop verb and Team__DataDir"
}

test_is_macho_reads_file_output() {
    ap_setup
    out=$(ap_child \
        "file() { printf 'Mach-O 64-bit dynamically linked shared library arm64\\n'; }" \
        'if is_macho x; then echo yes; else echo no; fi' \
        "file() { printf 'ASCII text\\n'; }" \
        'if is_macho x; then echo yes; else echo no; fi')
    t_assert_eq "yes${NL}no" "$out" "Mach-O output is recognised and other output is not"
}

test_run_kill_process_kills_and_waits_a_bounded_time() {
    ap_setup
    out=$(ap_child \
        "kill() { printf 'kill %s\\n' \"\$*\"; }" \
        "sleep_seconds() { printf 'sleep %s\\n' \"\$1\"; }" \
        'process_running() { return 1; }' \
        'if run_kill_process 4242; then echo gone; else echo stays; fi' \
        'process_running() { return 0; }' \
        'if run_kill_process 4242; then echo gone; else echo stays; fi')
    expected="kill -KILL 4242${NL}gone${NL}kill -KILL 4242"
    for ap_i in 1 2 3 4 5 6 7 8 9 10; do
        expected="$expected${NL}sleep 1"
    done
    expected="$expected${NL}stays"
    t_assert_eq "$expected" "$out" "SIGKILL once per call, ten one-second waits, then gives up"
}

test_process_running_sees_live_and_dead_processes() {
    ap_setup
    out=$(ap_child \
        'if process_running $$; then echo live; else echo dead; fi' \
        "sh -c 'exit 0' &" \
        'child=$!' \
        'wait "$child"' \
        'if process_running "$child"; then echo live; else echo dead; fi')
    t_assert_eq "live${NL}dead" "$out" "this shell is running, a finished child is not"
}

test_install_app_uses_local_variables() {
    ap_setup
    ap_install_with_markers v1
    ap_instance_json 4242
    ap_build_archive v2
    archive=KEEP sha=KEEP root=KEEP staging=KEEP old=KEEP parent=KEEP item=KEEP carried=KEEP old_moved=KEEP
    record=KEEP pid=KEEP rc=KEEP dir=KEEP file_path=KEEP waited=KEEP
    install_app "$AP_ARCHIVE" >"$AP/out" 2>&1 || true
    for kept in "$archive" "$sha" "$root" "$staging" "$old" "$parent" "$item" "$carried" "$old_moved" "$record" "$pid" "$rc" "$dir" "$file_path" "$waited"; do
        t_assert_eq "KEEP" "$kept" "a caller variable survived install_app"
    done
}

test_install_app_leaves_the_script_with_one_placeholder() {
    count=$(grep -o '__HUDDLE_REPO__' "$HERE/../install.sh" | wc -l | tr -d ' ')
    t_assert_eq "1" "$count" "placeholder still appears exactly once"
}

# ---- Task 33: install_node -------------------------------------------------------------------------

# nd_setup: an ap_setup sandbox whose release-info.json asks for Node v24.21.0 on Apple Silicon.
# node_version_of is faked: the fake node is a script whose second line is "# fakenode <version>".
nd_setup() {
    ap_setup
    HOST_RID=osx-arm64
    NODE_CHANGED=unset
    mkdir -p "$INSTALL_ROOT"
    printf '{"version": "1.0.0", "nodeVersion": "v24.21.0"}\n' >"$INSTALL_ROOT/release-info.json"
    node_version_of() {
        printf 'node_version_of %s\n' "$1" >>"$AP_CALLS"
        if [ -f "$1" ]; then
            sed -n 's/^# fakenode //p' "$1"
        fi
    }
}

# nd_build_node <version> <arm64|x64> [tamper|nonpm]: puts node-<version>-darwin-<arch>.tar.gz and a
# SHASUMS256.txt into $AP/nodebase, laid out like nodejs.org's archive (one wrapper folder).
# Files are written without the execute bit, so the install must set it on bin/node.
nd_build_node() {
    nd_name="node-$1-darwin-$2"
    nd_src="$AP/nodesrc"
    rm -rf "$nd_src"
    mkdir -p "$nd_src/$nd_name/bin" "$nd_src/$nd_name/lib/node_modules/npm/bin" "$AP/nodebase"
    printf '#!/bin/sh\n# fakenode %s\n' "$1" >"$nd_src/$nd_name/bin/node"
    printf '// npm for %s\n' "$1" >"$nd_src/$nd_name/lib/node_modules/npm/bin/npm-cli.js"
    printf 'license\n' >"$nd_src/$nd_name/LICENSE"
    if [ "${3:-}" = nonpm ]; then
        rm -f "$nd_src/$nd_name/lib/node_modules/npm/bin/npm-cli.js"
    fi
    tar -czf "$AP/nodebase/$nd_name.tar.gz" -C "$nd_src" "$nd_name"
    nd_hash=$(file_sha256 "$AP/nodebase/$nd_name.tar.gz")
    if [ "${3:-}" = tamper ]; then
        nd_hash=1111111111111111111111111111111111111111111111111111111111111111
    fi
    nd_zero=0000000000000000000000000000000000000000000000000000000000000000
    {
        printf '%s  node-%s-win-x64.zip\n' "$nd_zero" "$1"
        printf '%s  node-%s-darwin-arm64.tar.gz\n' "$nd_zero" "$1"
        printf '%s  node-%s-darwin-x64.tar.gz\n' "$nd_zero" "$1"
    } >"$AP/nodebase/SHASUMS256.txt"
    # The real line wins over the decoy of the same name (the last duplicate is used).
    printf '%s  %s.tar.gz\n' "$nd_hash" "$nd_name" >>"$AP/nodebase/SHASUMS256.txt"
}

# nd_run [base]: runs install_node in this shell; NDRC is its status, NDOUT its output.
nd_run() {
    NDRC=0
    install_node "${1-$AP/nodebase}" >"$AP/out" 2>&1 || NDRC=$?
    NDOUT=$(cat "$AP/out")
}

test_install_node_fresh_install() {
    nd_setup
    nd_build_node v24.21.0 arm64
    nd_run
    t_assert_eq "0" "$NDRC" "install_node succeeded: $NDOUT"
    t_assert_eq "1" "$NODE_CHANGED" "NODE_CHANGED"
    t_assert_eq "yes" "$([ -x "$NODE_BIN" ] && echo yes || echo no)" "NODE_BIN is executable"
    t_assert_file "$INSTALL_ROOT/node/lib/node_modules/npm/bin/npm-cli.js" "npm-cli.js under node/"
    t_assert_no_file "$INSTALL_ROOT/node/node-v24.21.0-darwin-arm64" "no wrapper folder inside node/"
    t_assert_no_file "$INSTALL_ROOT/node.staging" "staging folder removed"
    t_assert_no_file "$INSTALL_ROOT/node.old" "old folder removed"
    t_assert_no_file "$STATE_FILE" "install_node never writes state"
    t_assert_contains "$NDOUT" "[ok] node" "success line"
    t_assert_eq "v24.21.0" "$(node_version_of "$NODE_BIN")" "the installed node is the pinned one"
}

test_install_node_is_skipped_when_state_and_binary_agree() {
    nd_setup
    nd_build_node v24.21.0 arm64
    nd_run
    write_state_key "$STATE_FILE" node_version v24.21.0
    printf 'keep\n' >"$INSTALL_ROOT/node/marker"
    : >"$AP_CALLS"
    NODE_CHANGED=unset
    nd_run
    t_assert_eq "0" "$NDRC" "second run succeeded"
    t_assert_eq "0" "$NODE_CHANGED" "NODE_CHANGED after a skip"
    t_assert_contains "$NDOUT" "[skip] node" "skip line"
    t_assert_file "$INSTALL_ROOT/node/marker" "the node folder was not touched"
    t_assert_eq "1" "$(ap_count_calls '^node_version_of')" "the installed binary was asked once"
    t_assert_eq "0" "$(ap_count_calls '^stop')" "a skipped step does not stop the app"
}

test_install_node_is_not_skipped_when_the_binary_is_missing() {
    nd_setup
    nd_build_node v24.21.0 arm64
    write_state_key "$STATE_FILE" node_version v24.21.0
    nd_run
    t_assert_eq "0" "$NDRC" "install_node succeeded: $NDOUT"
    t_assert_eq "1" "$NODE_CHANGED" "NODE_CHANGED when state matches but node is missing"
    t_assert_file "$NODE_BIN" "node installed"
}

test_install_node_is_not_skipped_when_the_binary_reports_another_version() {
    nd_setup
    nd_build_node v24.21.0 arm64
    mkdir -p "$INSTALL_ROOT/node/bin"
    printf '#!/bin/sh\n# fakenode v22.0.0\n' >"$NODE_BIN"
    chmod +x "$NODE_BIN"
    write_state_key "$STATE_FILE" node_version v24.21.0
    nd_run
    t_assert_eq "1" "$NODE_CHANGED" "a binary that prints another version is replaced"
    t_assert_eq "v24.21.0" "$(node_version_of "$NODE_BIN")" "the pinned version is installed"
}

test_install_node_replaces_the_node_on_a_version_change() {
    nd_setup
    nd_build_node v24.21.0 arm64
    nd_run
    write_state_key "$STATE_FILE" node_version v24.21.0
    printf 'old\n' >"$INSTALL_ROOT/node/old-marker"
    ap_instance_json 4242
    printf '{"version": "1.0.1", "nodeVersion": "v24.22.0"}\n' >"$INSTALL_ROOT/release-info.json"
    nd_build_node v24.22.0 arm64
    : >"$AP_CALLS"
    nd_run
    t_assert_eq "0" "$NDRC" "upgrade succeeded: $NDOUT"
    t_assert_eq "1" "$NODE_CHANGED" "NODE_CHANGED"
    t_assert_no_file "$INSTALL_ROOT/node/old-marker" "the old node folder is gone"
    t_assert_eq "v24.22.0" "$(node_version_of "$NODE_BIN")" "the new version is installed"
    t_assert_eq "1" "$(ap_count_calls '^stop old_present=0')" "the running app was stopped once, before the swap"
    t_assert_no_file "$INSTALL_ROOT/node.old" "old folder removed"
    t_assert_no_file "$INSTALL_ROOT/node.staging" "staging folder removed"
}

test_install_node_fresh_install_never_stops_the_app() {
    nd_setup
    nd_build_node v24.21.0 arm64
    ap_instance_json 4242
    nd_run
    t_assert_eq "1" "$NODE_CHANGED" "installed"
    t_assert_eq "0" "$(ap_count_calls '^stop')" "no node folder exists yet, so nothing can hold it open"
}

test_install_node_survivor_aborts_the_upgrade_untouched() {
    nd_setup
    nd_build_node v24.21.0 arm64
    nd_run
    write_state_key "$STATE_FILE" node_version v24.21.0
    printf 'old\n' >"$INSTALL_ROOT/node/old-marker"
    ap_instance_json 4242
    AP_ALIVE=1
    printf '{"version": "1.0.1", "nodeVersion": "v24.22.0"}\n' >"$INSTALL_ROOT/release-info.json"
    nd_build_node v24.22.0 arm64
    nd_run
    AP_ALIVE=0
    t_assert_eq "1" "$NDRC" "a surviving app fails the step"
    t_assert_eq "0" "$NODE_CHANGED" "NODE_CHANGED"
    t_assert_file "$INSTALL_ROOT/node/old-marker" "the previous node is untouched"
    t_assert_no_file "$INSTALL_ROOT/node.staging" "staging folder removed"
}

test_install_node_rejects_a_tampered_tarball() {
    nd_setup
    nd_build_node v24.21.0 arm64
    nd_run
    write_state_key "$STATE_FILE" node_version v24.21.0
    printf 'old\n' >"$INSTALL_ROOT/node/old-marker"
    printf '{"version": "1.0.1", "nodeVersion": "v24.22.0"}\n' >"$INSTALL_ROOT/release-info.json"
    nd_build_node v24.22.0 arm64 tamper
    nd_run
    t_assert_eq "1" "$NDRC" "a hash mismatch fails the step"
    t_assert_eq "0" "$NODE_CHANGED" "NODE_CHANGED"
    t_assert_contains "$NDOUT" "hash mismatch" "the reason is shown"
    t_assert_contains "$NDOUT" "[!!] node" "failure line"
    t_assert_file "$INSTALL_ROOT/node/old-marker" "the previous node is untouched"
    t_assert_eq "v24.21.0" "$(node_version_of "$NODE_BIN")" "the previous node still runs"
}

test_install_node_rejects_an_archive_without_npm() {
    nd_setup
    nd_build_node v24.21.0 arm64 nonpm
    nd_run
    t_assert_eq "1" "$NDRC" "an archive without npm-cli.js fails"
    t_assert_eq "0" "$NODE_CHANGED" "NODE_CHANGED"
    t_assert_no_file "$INSTALL_ROOT/node" "nothing was installed"
    t_assert_no_file "$INSTALL_ROOT/node.staging" "staging folder removed"
}

test_install_node_arch_x64_fetches_the_darwin_x64_name() {
    nd_setup
    HOST_RID=osx-x64
    nd_build_node v24.21.0 x64
    rm -f "$AP/nodebase/node-v24.21.0-darwin-arm64.tar.gz"
    nd_run
    t_assert_eq "0" "$NDRC" "x64 install succeeded: $NDOUT"
    t_assert_file "$AP/data dir/cache/node-v24.21.0-darwin-x64.tar.gz" "the x64 tarball was cached"
    t_assert_file "$INSTALL_ROOT/node/lib/node_modules/npm/bin/npm-cli.js" "npm-cli.js under node/"
}

test_install_node_unknown_arch_fails() {
    nd_setup
    nd_build_node v24.21.0 arm64
    HOST_RID=""
    nd_run
    t_assert_eq "1" "$NDRC" "no HOST_RID fails"
    t_assert_eq "0" "$NODE_CHANGED" "NODE_CHANGED"
    t_assert_no_file "$INSTALL_ROOT/node" "nothing was installed"
}

test_install_node_needs_a_node_version_in_release_info() {
    nd_setup
    nd_build_node v24.21.0 arm64
    printf '{"version": "1.0.0"}\n' >"$INSTALL_ROOT/release-info.json"
    nd_run
    t_assert_eq "1" "$NDRC" "no nodeVersion fails"
    t_assert_contains "$NDOUT" "nodeVersion" "the message names the key"
    printf '{"nodeVersion": "latest"}\n' >"$INSTALL_ROOT/release-info.json"
    nd_run
    t_assert_eq "1" "$NDRC" "a version that is not vX.Y.Z fails"
    rm -f "$INSTALL_ROOT/release-info.json"
    nd_run
    t_assert_eq "1" "$NDRC" "no release-info.json fails"
}

test_install_node_keeps_the_release_sums() {
    nd_setup
    nd_build_node v24.21.0 arm64
    SUMS="release-sums-marker${NL}"
    nd_run
    t_assert_eq "0" "$NDRC" "install_node succeeded: $NDOUT"
    t_assert_eq "release-sums-marker${NL}" "$SUMS" "the release SUMS survive the Node SHASUMS"
    SUMS=""
}

test_install_node_defaults_to_nodejs_org() {
    nd_setup
    nd_build_node v24.21.0 arm64
    out=$(ap_child \
        "HOST_RID=osx-arm64" \
        "http_get() { printf 'get %s\n' \"\$1\" >&2; cp \"$AP/nodebase/\${1##*/}\" \"\$2\"; }" \
        "node_version_of() { sed -n 's/^# fakenode //p' \"\$1\"; }" \
        "install_node; echo \"rc=\$? changed=\$NODE_CHANGED\"")
    t_assert_contains "$out" "get https://nodejs.org/dist/v24.21.0/SHASUMS256.txt" "SHASUMS comes from nodejs.org"
    t_assert_contains "$out" "get https://nodejs.org/dist/v24.21.0/node-v24.21.0-darwin-arm64.tar.gz" "the tarball comes from nodejs.org"
    t_assert_contains "$out" "rc=0 changed=1" "installed through the default source"
}

test_install_node_uses_local_variables() {
    nd_setup
    nd_build_node v24.21.0 arm64
    base=KEEP version=KEEP bare=KEEP rid=KEEP arch=KEEP name=KEEP wrapper=KEEP staging=KEEP old=KEEP
    sums_file=KEEP expected=KEEP archive=KEEP source=KEEP kind=KEEP inner=KEEP installed=KEEP root=KEEP
    saved_sums=KEEP file_path=KEEP rc=KEEP old_moved=KEEP
    install_node "$AP/nodebase" >"$AP/out" 2>&1 || true
    for kept in "$old_moved" "$base" "$version" "$bare" "$rid" "$arch" "$name" "$wrapper" "$staging" "$old" "$sums_file" "$expected" "$archive" "$source" "$kind" "$inner" "$installed" "$root" "$saved_sums" "$file_path" "$rc"; do
        t_assert_eq "KEEP" "$kept" "a caller variable survived install_node"
    done
}

# ---- Task 34: install_adapter ----------------------------------------------------------------------

# ad_setup: an ap_setup sandbox with a bundled (fake) node and npm CLI, and tools/acp holding a lock
# file. run_npm_ci is faked: it logs where and how it ran, then creates the adapter entry unless
# AD_NPM_RC is non-zero (it then prints 20 numbered lines and returns that status).
ad_setup() {
    ap_setup
    ADAPTER_CHANGED=unset
    AD_NPM_RC=0
    AD_NPM_ENTRY=1
    mkdir -p "$INSTALL_ROOT/node/bin" "$INSTALL_ROOT/node/lib/node_modules/npm/bin" "$INSTALL_ROOT/tools/acp"
    printf '#!/bin/sh\n' >"$NODE_BIN"
    chmod +x "$NODE_BIN"
    printf '// npm\n' >"$INSTALL_ROOT/node/lib/node_modules/npm/bin/npm-cli.js"
    printf '{"lockfileVersion": 3}\n' >"$INSTALL_ROOT/tools/acp/package-lock.json"
    printf '{"name": "acp"}\n' >"$INSTALL_ROOT/tools/acp/package.json"
    run_npm_ci() {
        printf 'npm_ci pwd=%s path1=%s args=%s\n' "$(pwd)" "${PATH%%:*}" "$*" >>"$AP_CALLS"
        printf 'files=%s\n' "$(ls | tr '\n' ' ')" >>"$AP_CALLS"
        if [ "$AD_NPM_RC" -ne 0 ]; then
            ad_n=1
            while [ "$ad_n" -le 20 ]; do
                printf 'npm line %s\n' "$ad_n"
                ad_n=$((ad_n + 1))
            done
            return "$AD_NPM_RC"
        fi
        if [ "$AD_NPM_ENTRY" -eq 1 ]; then
            # npm ci installs into ./node_modules of its working directory (the staged copy).
            ad_rel=${ADAPTER_ENTRY#"$INSTALL_ROOT/tools/acp/"}
            mkdir -p "$(dirname "./$ad_rel")"
            printf '// adapter\n' >"./$ad_rel"
        fi
        return 0
    }
}

# ad_run: runs install_adapter in this shell; ADRC is its status, ADOUT its output.
ad_run() {
    ADRC=0
    install_adapter >"$AP/out" 2>&1 || ADRC=$?
    ADOUT=$(cat "$AP/out")
}

test_install_adapter_runs_npm_ci_when_nothing_is_installed() {
    ad_setup
    ad_run
    ad_dir=$(cd "$INSTALL_ROOT/tools" && pwd)/acp.staging
    t_assert_eq "0" "$ADRC" "install_adapter succeeded: $ADOUT"
    t_assert_eq "1" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_file "$ADAPTER_ENTRY" "the adapter entry exists"
    t_assert_eq "npm_ci pwd=$ad_dir path1=$INSTALL_ROOT/node/bin args=ci --omit=dev --no-audit --no-fund" "$(grep '^npm_ci' "$AP_CALLS")" "cwd, PATH and arguments of the npm ci call"
    t_assert_eq "1" "$(ap_count_calls '^npm_ci')" "npm ci ran once"
    t_assert_no_file "$STATE_FILE" "install_adapter never writes state"
    t_assert_contains "$ADOUT" "[ok] adapter" "success line"
    t_assert_file "$CACHE_DIR/npm-ci.log" "the log file exists"
}

test_install_adapter_is_skipped_when_the_lock_hash_matches_and_the_entry_exists() {
    ad_setup
    ad_run
    write_state_key "$STATE_FILE" lock_sha256 "$(file_sha256 "$INSTALL_ROOT/tools/acp/package-lock.json")"
    : >"$AP_CALLS"
    ad_run
    t_assert_eq "0" "$ADRC" "second run succeeded"
    t_assert_eq "0" "$ADAPTER_CHANGED" "ADAPTER_CHANGED after a skip"
    t_assert_contains "$ADOUT" "[skip] adapter" "skip line"
    t_assert_eq "0" "$(ap_count_calls '^npm_ci')" "npm ci did not run"
    t_assert_eq "0" "$(ap_count_calls '^stop')" "a skipped step does not stop the app"
}

test_install_adapter_runs_when_the_entry_is_missing() {
    ad_setup
    write_state_key "$STATE_FILE" lock_sha256 "$(file_sha256 "$INSTALL_ROOT/tools/acp/package-lock.json")"
    ad_run
    t_assert_eq "0" "$ADRC" "install_adapter succeeded: $ADOUT"
    t_assert_eq "1" "$ADAPTER_CHANGED" "ADAPTER_CHANGED when the hash matches but the entry is missing"
    t_assert_eq "1" "$(ap_count_calls '^npm_ci')" "npm ci ran"
}

test_install_adapter_runs_when_the_lock_hash_differs() {
    ad_setup
    ad_run
    write_state_key "$STATE_FILE" lock_sha256 "$(file_sha256 "$INSTALL_ROOT/tools/acp/package-lock.json")"
    printf '{"lockfileVersion": 3, "changed": true}\n' >"$INSTALL_ROOT/tools/acp/package-lock.json"
    : >"$AP_CALLS"
    ad_run
    t_assert_eq "1" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_eq "1" "$(ap_count_calls '^npm_ci')" "npm ci ran again"
}

test_install_adapter_stops_a_running_app_only_when_it_will_change() {
    ad_setup
    ap_instance_json 4242
    ad_run
    t_assert_eq "1" "$(ap_count_calls '^stop')" "the running app was stopped once"
    write_state_key "$STATE_FILE" lock_sha256 "$(file_sha256 "$INSTALL_ROOT/tools/acp/package-lock.json")"
    : >"$AP_CALLS"
    ad_run
    t_assert_eq "0" "$(ap_count_calls '^stop')" "a skip leaves the app alone"
}

test_install_adapter_survivor_aborts_before_npm_ci() {
    ad_setup
    ap_instance_json 4242
    AP_ALIVE=1
    ad_run
    AP_ALIVE=0
    t_assert_eq "1" "$ADRC" "a surviving app fails the step"
    t_assert_eq "0" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_eq "0" "$(ap_count_calls '^npm_ci')" "npm ci did not run"
}

test_install_adapter_failure_shows_the_log_tail_and_a_proxy_fix() {
    ad_setup
    AD_NPM_RC=7
    ad_run
    t_assert_eq "1" "$ADRC" "a failing npm ci fails the step"
    t_assert_eq "0" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_contains "$ADOUT" "[!!] adapter" "failure line"
    t_assert_contains "$ADOUT" "exit code 7" "the exit code is shown"
    t_assert_contains "$ADOUT" "npm line 20" "the last log line is shown"
    t_assert_contains "$ADOUT" "npm line 6" "the 15th line from the end is shown"
    t_assert_not_contains "$ADOUT" "npm line 5${NL}" "older lines are not shown"
    t_assert_contains "$ADOUT" "fix:" "a fix line"
    t_assert_contains "$ADOUT" "HTTPS_PROXY" "the fix names the proxy variable"
    t_assert_contains "$(cat "$CACHE_DIR/npm-ci.log")" "npm line 1" "the log keeps the whole output"
    t_assert_no_file "$ADAPTER_ENTRY" "no adapter entry after a failure"
}

test_install_adapter_verifies_the_entry_afterwards() {
    ad_setup
    AD_NPM_ENTRY=0
    ad_run
    t_assert_eq "1" "$ADRC" "an npm ci that leaves no entry fails the step"
    t_assert_eq "0" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_contains "$ADOUT" "[!!] adapter" "failure line"
    t_assert_contains "$ADOUT" "claude-agent-acp" "the message names the adapter"
}

test_install_adapter_needs_the_bundled_node_and_a_lock_file() {
    ad_setup
    rm -f "$NODE_BIN"
    ad_run
    t_assert_eq "1" "$ADRC" "no bundled node fails"
    t_assert_contains "$ADOUT" "fix:" "a fix line"
    t_assert_eq "0" "$(ap_count_calls '^npm_ci')" "npm ci did not run"
    ad_setup
    rm -f "$INSTALL_ROOT/tools/acp/package-lock.json"
    ad_run
    t_assert_eq "1" "$ADRC" "no package-lock.json fails"
    t_assert_eq "0" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
}

test_run_npm_ci_runs_the_bundled_node_on_the_bundled_npm() {
    ad_setup
    printf '#!/bin/sh\nprintf "node %%s\\n" "$*"\ncat >/dev/null\n' >"$NODE_BIN"
    chmod +x "$NODE_BIN"
    out=$(ap_child "run_npm_ci ci --omit=dev --no-audit --no-fund")
    t_assert_eq "node $INSTALL_ROOT/node/lib/node_modules/npm/bin/npm-cli.js ci --omit=dev --no-audit --no-fund" "$out" "node runs npm-cli.js with the arguments, no global npm"
}

test_install_adapter_runs_npm_ci_in_a_staged_copy_and_swaps_node_modules_in() {
    ad_setup
    mkdir -p "$INSTALL_ROOT/tools/acp/node_modules"
    printf 'old\n' >"$INSTALL_ROOT/tools/acp/node_modules/marker"
    ad_run
    t_assert_eq "0" "$ADRC" "install_adapter succeeded: $ADOUT"
    t_assert_eq "1" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_contains "$(grep '^files=' "$AP_CALLS")" "package.json" "the staged copy holds package.json"
    t_assert_contains "$(grep '^files=' "$AP_CALLS")" "package-lock.json" "and package-lock.json"
    t_assert_not_contains "$(grep '^files=' "$AP_CALLS")" "node_modules" "but not the live node_modules"
    t_assert_no_file "$INSTALL_ROOT/tools/acp/node_modules/marker" "the previous node_modules was replaced"
    t_assert_file "$ADAPTER_ENTRY" "the staged tree is now the live node_modules"
    t_assert_no_file "$INSTALL_ROOT/tools/acp.staging" "no staging folder is left"
    t_assert_no_file "$INSTALL_ROOT/tools/acp/node_modules.old" "no node_modules.old is left"
}

test_install_adapter_failure_leaves_the_previous_node_modules_untouched() {
    ad_setup
    mkdir -p "$INSTALL_ROOT/tools/acp/node_modules"
    printf 'old\n' >"$INSTALL_ROOT/tools/acp/node_modules/marker"
    AD_NPM_RC=7
    ad_run
    t_assert_eq "1" "$ADRC" "a failing npm ci fails the step"
    t_assert_eq "old" "$(cat "$INSTALL_ROOT/tools/acp/node_modules/marker")" "the previous node_modules is intact"
    t_assert_no_file "$INSTALL_ROOT/tools/acp.staging" "the staging folder is deleted"
    t_assert_no_file "$INSTALL_ROOT/tools/acp/node_modules.old" "no node_modules.old"
    t_assert_contains "$ADOUT" "HTTPS_PROXY" "the proxy fix is still shown"
}

test_install_adapter_missing_entry_in_the_staged_tree_leaves_the_previous_node_modules() {
    ad_setup
    mkdir -p "$INSTALL_ROOT/tools/acp/node_modules"
    printf 'old\n' >"$INSTALL_ROOT/tools/acp/node_modules/marker"
    AD_NPM_ENTRY=0
    ad_run
    t_assert_eq "1" "$ADRC" "an npm ci that leaves no entry fails the step"
    t_assert_eq "old" "$(cat "$INSTALL_ROOT/tools/acp/node_modules/marker")" "the previous node_modules is intact"
    t_assert_no_file "$INSTALL_ROOT/tools/acp.staging" "the staging folder is deleted"
    t_assert_contains "$ADOUT" "claude-agent-acp" "the message names the adapter"
}

test_install_adapter_rolls_back_when_the_swap_fails() {
    ad_setup
    mkdir -p "$INSTALL_ROOT/tools/acp/node_modules"
    printf 'old\n' >"$INSTALL_ROOT/tools/acp/node_modules/marker"
    mv() {
        case $1 in
            *acp.staging/node_modules) return 1 ;;
        esac
        command mv "$@"
    }
    ad_run
    unset -f mv
    t_assert_eq "1" "$ADRC" "a failed swap fails the step"
    t_assert_eq "0" "$ADAPTER_CHANGED" "ADAPTER_CHANGED"
    t_assert_eq "old" "$(cat "$INSTALL_ROOT/tools/acp/node_modules/marker")" "the previous node_modules is back"
    t_assert_no_file "$INSTALL_ROOT/tools/acp.staging" "the staging folder is deleted"
    t_assert_no_file "$INSTALL_ROOT/tools/acp/node_modules.old" "no node_modules.old"
    t_assert_contains "$ADOUT" "[!!] adapter" "the failure is reported"
}

test_install_adapter_needs_a_package_json() {
    ad_setup
    rm -f "$INSTALL_ROOT/tools/acp/package.json"
    ad_run
    t_assert_eq "1" "$ADRC" "no package.json fails"
    t_assert_eq "0" "$(ap_count_calls '^npm_ci')" "npm ci did not run"
    t_assert_no_file "$INSTALL_ROOT/tools/acp.staging" "no staging folder is left"
}

test_install_adapter_replaces_a_leftover_staging_folder() {
    ad_setup
    mkdir -p "$INSTALL_ROOT/tools/acp.staging/node_modules"
    printf 'stale\n' >"$INSTALL_ROOT/tools/acp.staging/node_modules/stale"
    ad_run
    t_assert_eq "0" "$ADRC" "install_adapter succeeded: $ADOUT"
    t_assert_no_file "$INSTALL_ROOT/tools/acp/node_modules/stale" "stale staged files do not reach the live tree"
}

test_install_adapter_uses_local_variables() {
    ad_setup
    acp_dir=KEEP lock=KEEP sha=KEEP npm_cli=KEEP log=KEEP rc=KEEP root=KEEP staging=KEEP aside=KEEP entry_rel=KEEP
    install_adapter >"$AP/out" 2>&1 || true
    for kept in "$acp_dir" "$lock" "$sha" "$npm_cli" "$log" "$rc" "$root" "$staging" "$aside" "$entry_rel"; do
        t_assert_eq "KEEP" "$kept" "a caller variable survived install_adapter"
    done
}

test_shell_quote_empty_string() {
    result=$(shell_quote "")
    t_assert_eq "''" "$result" "empty string"
}

test_shell_quote_no_special_chars() {
    result=$(shell_quote "hello")
    t_assert_eq "'hello'" "$result" "no special chars"
}

test_shell_quote_single_quote() {
    result=$(shell_quote "it's")
    t_assert_eq "'it'\"'\"'s'" "$result" "single quote escaped"
}

test_shell_quote_multiple_quotes() {
    result=$(shell_quote "it's a'test")
    expected="'it'\"'\"'s a'\"'\"'test'"
    t_assert_eq "$expected" "$result" "multiple quotes"
}

test_shell_quote_space_and_quote() {
    result=$(shell_quote "/path with space/and'quote")
    expected="'/path with space/and'\"'\"'quote'"
    t_assert_eq "$expected" "$result" "space and quote"
}

test_launcher_content_basic() {
    parse_args
    resolve_paths
    output=$(launcher_content)
    t_assert_contains "$output" "#!/bin/sh" "shebang"
    t_assert_contains "$output" "set -eu" "set -eu"
    t_assert_contains "$output" "export Team__Desktop__Enabled=true" "Desktop enabled"
    t_assert_contains "$output" "export Team__Acp__Enabled=true" "ACP enabled"
    t_assert_contains "$output" "export Team__PipeName=\"huddle-\$(id -un)\"" "PipeName with literal $(id -un)"
    t_assert_not_contains "$output" "$(printf '\r')" "no CR characters"
}

test_launcher_content_with_space_in_paths() {
    parse_args --install-root "$TEST_HOME/my app" --data-root "$TEST_HOME/data root"
    resolve_paths
    output=$(launcher_content)
    expected_data_dir="'$TEST_HOME/data root/data'"
    expected_app_exe="'$TEST_HOME/my app/app/Huddle.App'"
    t_assert_contains "$output" "export Team__DataDir=$expected_data_dir" "DataDir quoted with space"
    t_assert_contains "$output" "exec $expected_app_exe \"\$@\"" "APP_EXE quoted with space"
}

test_launcher_content_with_quote_in_path() {
    parse_args --install-root "$TEST_HOME/app'test" --data-root "$TEST_HOME/data'root"
    resolve_paths
    output=$(launcher_content)
    t_assert_not_contains "$output" "$(printf '\r')" "no CR in quoted paths"
}

test_app_stub_content_basic() {
    parse_args
    resolve_paths
    output=$(app_stub_content)
    first_line=$(printf '%s\n' "$output" | head -1)
    t_assert_eq "#!/bin/sh" "$first_line" "shebang"
    t_assert_contains "$output" "exec '$INSTALL_ROOT/bin/huddle'" "huddle launcher path"
    t_assert_contains "$output" ">>" "append to log"
    t_assert_contains "$output" "$DATA_ROOT/launcher.log" "log path"
    t_assert_not_contains "$output" "$(printf '\r')" "no CR characters"
}

test_app_stub_content_with_space_and_quote() {
    parse_args --install-root "$TEST_HOME/app'dir with space" --data-root "$TEST_HOME/data'dir"
    resolve_paths
    output=$(app_stub_content)
    t_assert_not_contains "$output" "$(printf '\r')" "no CR in paths with space and quote"
}

test_plist_content_basic() {
    version="1.2.3"
    output=$(plist_content "$version")
    t_assert_contains "$output" "<?xml" "XML header"
    t_assert_contains "$output" "<plist version=\"1.0\">" "plist element"
    t_assert_contains "$output" "<dict>" "dict element"
    t_assert_contains "$output" "<key>CFBundleExecutable</key>" "CFBundleExecutable key"
    t_assert_contains "$output" "<string>AgencyHuddle</string>" "CFBundleExecutable value"
    t_assert_contains "$output" "<key>CFBundleIdentifier</key>" "CFBundleIdentifier key"
    t_assert_contains "$output" "<string>com.agency.huddle</string>" "CFBundleIdentifier value"
    t_assert_contains "$output" "<key>CFBundleName</key>" "CFBundleName key"
    t_assert_contains "$output" "<string>Agency Huddle</string>" "CFBundleName value"
    t_assert_contains "$output" "<key>CFBundlePackageType</key>" "CFBundlePackageType key"
    t_assert_contains "$output" "<string>APPL</string>" "CFBundlePackageType value"
    t_assert_contains "$output" "<key>CFBundleVersion</key>" "CFBundleVersion key"
    t_assert_contains "$output" "<string>1.2.3</string>" "CFBundleVersion value"
    t_assert_contains "$output" "<key>LSUIElement</key>" "LSUIElement key"
    t_assert_contains "$output" "<true/>" "LSUIElement value"
}

test_plist_content_escapes_xml_special_chars() {
    version="1.2.3-beta&special<chars>here"
    output=$(plist_content "$version")
    t_assert_contains "$output" "1.2.3-beta&amp;special&lt;chars&gt;here" "XML escape"
    t_assert_not_contains "$output" "<1.2.3" "< is escaped"
    t_assert_not_contains "$output" ">here" "> is escaped"
}

test_launcher_content_lines_exact_order() {
    parse_args
    resolve_paths
    output=$(launcher_content)
    lines=$(printf '%s' "$output" | tr '\n' '|')
    t_assert_contains "$lines" "#!/bin/sh|set -eu|export Team__Desktop__Enabled=true|export Team__Acp__Enabled=true" "first four lines in order"
    t_assert_not_contains "$output" "$(printf '\r')" "no carriage returns"
}

# ---- Task 38: main (Spec sections 6 and 7) -------------------------------------------------
# A FAKE release (tar.gz + SHA256SUMS in a folder, a fake node tarball served by a fake http_get) and
# a private HOME: nothing here downloads, runs real npm or open, or touches the real home.

MN_ZERO=0000000000000000000000000000000000000000000000000000000000000000

# mn_build_release <tag>: builds $MN_BASE/huddle-osx-arm64.tar.gz and a SHA256SUMS with decoy lines.
mn_build_release() {
    ap_build_archive "$1"
    mkdir -p "$MN_BASE"
    cp "$AP_ARCHIVE" "$MN_BASE/huddle-osx-arm64.tar.gz"
    mn_hash=$(file_sha256 "$MN_BASE/huddle-osx-arm64.tar.gz")
    {
        printf '%s  huddle-osx-x64.tar.gz\n' "$MN_ZERO"
        printf '%s  huddle-win-x64.zip\n' "$MN_ZERO"
        printf '%s  huddle-osx-arm64.tar.gz\n' "$mn_hash"
    } >"$MN_BASE/SHA256SUMS"
}

# A sandbox with spaces in every path. MN_DATA is deliberately NOT created: "nothing created" is a test.
mn_setup() {
    ap_setup
    MN_HOME="$AP/home dir"
    MN_BASE="$AP/release dir"
    MN_DATA="$AP/data dir"
    MN_ROOT="$MN_DATA/install"
    MN_NPM_FAIL="$AP/npm-fail-once"
    MN_MACOS=15
    MN_AUTH_RC=0
    MN_INTERACTIVE=1
    mkdir -p "$MN_HOME"
    nd_build_node v24.21.0 arm64
    mn_build_release 1.2.3
    rm -f "$AP/http.log"
}

# The seams main reaches, faked. Called inside the subshell of mn_run.
mn_fakes() {
    ap_stubs
    la_stubs
    good_seams
    macos_major() { printf '%s\n' "$MN_MACOS"; }
    codesign() { :; }
    xattr() { :; }
    sleep_seconds() { :; }
    http_get() {
        printf '%s\n' "$1" >>"$AP/http.log"
        [ -f "$AP/nodebase/${1##*/}" ] || return 22
        cp "$AP/nodebase/${1##*/}" "$2"
    }
    node_version_of() {
        if [ -f "$1" ]; then
            sed -n 's/^# fakenode //p' "$1"
        fi
    }
    run_npm_ci() {
        printf 'npm_ci\n' >>"$AP_CALLS"
        if [ -f "$MN_NPM_FAIL" ]; then
            rm -f "$MN_NPM_FAIL"
            printf 'npm exploded\n'
            return 1
        fi
        # npm ci installs into ./node_modules of its working directory (the staged copy).
        mn_rel=${ADAPTER_ENTRY#"$INSTALL_ROOT/tools/acp/"}
        mkdir -p "$(dirname "./$mn_rel")"
        printf '// adapter\n' >"./$mn_rel"
    }
    run_open() {
        printf 'run_open %s\n' "$1" >>"$AP_CALLS"
        return "${MN_OPEN_RC:-0}"
    }
    claude_auth_status() {
        printf 'auth_status %s\n' "$1" >>"$AP_CALLS"
        return "$MN_AUTH_RC"
    }
    is_interactive() {
        [ "$MN_INTERACTIVE" -eq 1 ]
    }
}

# mn_run [main args]: runs main the way the entry guard does, in a subshell, against the fake release
# and the sandbox roots. MN_RC is its status, MN_OUT its stdout and stderr.
mn_run() {
    MN_RC=0
    (
        HOME="$MN_HOME"
        export HOME
        HUDDLE_REPO="owner/repo"
        mn_fakes
        mn_status=0
        main --base-url "$MN_BASE" --install-root "$MN_ROOT" --data-root "$MN_DATA" "$@" || mn_status=$?
        exit "$mn_status"
    ) >"$AP/mn.out" 2>&1 || MN_RC=$?
    MN_OUT=$(cat "$AP/mn.out")
}

mn_state() {
    read_state_key "$MN_ROOT/install-state" "$1" || true
}

# Path, size and modification time of every file main may touch, sorted.
mn_snapshot() {
    find "$MN_ROOT" "$MN_HOME" -type f -exec ls -l {} + | sort
}

# mn_entry direct|piped args...: runs the real script as a child process the two ways users do.
mn_entry() {
    mn_mode=$1
    shift
    mn_shell=$(command -v "$T_SHELL")
    mn_script="$HERE/../install.sh"
    if [ "$mn_mode" = piped ]; then
        env HOME="$MN_HOME" HUDDLE_INSTALL_NO_MAIN= HUDDLE_REPO=owner/repo "$mn_shell" -s -- "$@" <"$mn_script" 2>&1 || printf 'rc=%s\n' "$?"
    else
        env HOME="$MN_HOME" HUDDLE_INSTALL_NO_MAIN= HUDDLE_REPO=owner/repo "$mn_shell" "$mn_script" "$@" </dev/null 2>&1 || printf 'rc=%s\n' "$?"
    fi
}

test_main_refuses_a_foreign_install_root_and_a_bad_version() {
    mn_setup
    mkdir -p "$MN_ROOT"
    printf 'precious\n' >"$MN_ROOT/precious.txt"
    mn_run
    t_assert_eq "1" "$MN_RC" "a foreign install root exits 1: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!] app" "the failure line"
    t_assert_contains "$MN_OUT" "fix: choose an empty folder or remove it" "the fix"
    t_assert_eq "precious" "$(cat "$MN_ROOT/precious.txt")" "the foreign file is untouched"
    t_assert_no_file "$MN_ROOT.old" "no .old"
    t_assert_no_file "$MN_ROOT.staging" "no .staging"
    mn_run --version v1
    t_assert_eq "3" "$MN_RC" "a bad version exits 3: $MN_OUT"
    t_assert_contains "$MN_OUT" "fix: use latest or a version such as v1.2.3" "with its fix"
    mn_run --install-root /
    t_assert_eq "3" "$MN_RC" "a root install folder exits 3: $MN_OUT"
}

test_main_refuses_nested_data_and_install_folders() {
    mn_setup
    mn_run --data-root "$MN_ROOT/data"
    t_assert_eq "3" "$MN_RC" "a data folder inside the install folder exits 3: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!] usage - the data folder $MN_ROOT/data is inside the install folder $MN_ROOT" "the failure line"
    t_assert_contains "$MN_OUT" "fix: " "with a fix"
    t_assert_no_file "$MN_ROOT" "nothing was installed"
    mn_run --install-root "$MN_DATA"
    t_assert_eq "3" "$MN_RC" "the same folder for both exits 3: $MN_OUT"
    mn_run --install-root "$MN_DATA/other"
    t_assert_eq "3" "$MN_RC" "an install folder inside the data folder exits 3: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!] usage - the install folder $MN_DATA/other is inside the data folder $MN_DATA" "the failure line"
    t_assert_contains "$MN_OUT" "fix: " "with a fix"
    t_assert_no_file "$MN_DATA/other" "nothing was installed"
    mn_run --install-root "$MN_DATA/install/deeper"
    t_assert_eq "3" "$MN_RC" "only <data root>/install itself is allowed: $MN_OUT"
    mn_run
    t_assert_eq "0" "$MN_RC" "the documented default layout (<data root>/install) installs: $MN_OUT"
}

test_main_first_run_installs_everything() {
    mn_setup
    mn_run
    t_assert_eq "0" "$MN_RC" "exit code: $MN_OUT"
    t_assert_contains "$MN_OUT" "Agency Huddle 1.2.3 installed." "summary line"
    t_assert_contains "$MN_OUT" "[ok] app" "app step"
    t_assert_contains "$MN_OUT" "[ok] node" "node step"
    t_assert_contains "$MN_OUT" "[ok] adapter" "adapter step"
    t_assert_contains "$MN_OUT" "[ok] launcher" "launcher step"
    t_assert_file "$MN_ROOT/app/Huddle.App" "app host"
    t_assert_file "$MN_ROOT/node/bin/node" "node"
    t_assert_file "$MN_ROOT/tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js" "adapter entry"
    t_assert_file "$MN_ROOT/bin/huddle" "launcher"
    t_assert_file "$MN_HOME/Applications/Agency Huddle.app/Contents/Info.plist" "bundle"
    t_assert_eq "1.2.3" "$(mn_state version)" "state version"
    t_assert_eq "$(file_sha256 "$MN_BASE/huddle-osx-arm64.tar.gz")" "$(mn_state app_sha256)" "state app_sha256"
    t_assert_eq "v24.21.0" "$(mn_state node_version)" "state node_version"
    t_assert_eq "$(file_sha256 "$MN_ROOT/tools/acp/package-lock.json")" "$(mn_state lock_sha256)" "state lock_sha256"
    t_assert_eq "1" "$(ap_count_calls '^npm_ci')" "npm ci ran once"
    t_assert_eq "1" "$(ap_count_calls '^run_open ')" "open ran once"
    t_assert_eq "run_open $MN_HOME/Applications/Agency Huddle.app" "$(grep '^run_open ' "$AP_CALLS")" "open starts the .app"
    t_assert_no_file "$MN_DATA/cache/install.lock" "the lock is released"
}

test_main_second_run_skips_everything_and_changes_no_file() {
    mn_setup
    mn_run
    t_assert_eq "0" "$MN_RC" "first run: $MN_OUT"
    find "$MN_ROOT" "$MN_HOME" -type f -exec touch -t 200101010000 {} +
    rm -rf "$MN_DATA/cache"
    rm -f "$AP/http.log"
    : >"$AP_CALLS"
    before=$(mn_snapshot)
    mn_run
    after=$(mn_snapshot)
    t_assert_eq "0" "$MN_RC" "second run: $MN_OUT"
    t_assert_contains "$MN_OUT" "Already up to date (1.2.3)." "up-to-date line"
    t_assert_not_contains "$MN_OUT" "installed." "no installed line"
    t_assert_contains "$MN_OUT" "[skip] app" "app skipped"
    t_assert_contains "$MN_OUT" "[skip] node" "node skipped"
    t_assert_contains "$MN_OUT" "[skip] adapter" "adapter skipped"
    t_assert_contains "$MN_OUT" "[skip] launcher" "launcher skipped"
    t_assert_not_contains "$MN_OUT" "[..]" "no step did any work"
    t_assert_eq "$before" "$after" "path, size and mtime snapshot"
    t_assert_no_file "$AP/http.log" "http_get was never called (empty cache dir)"
    t_assert_eq "0" "$(ap_count_calls '^npm_ci')" "no npm ci"
}

test_main_changed_archive_runs_only_the_app_step() {
    mn_setup
    mn_run
    printf 'mine\n' >"$MN_DATA/marker.txt"
    mn_build_release 1.3.0
    : >"$AP_CALLS"
    mn_run
    t_assert_eq "0" "$MN_RC" "upgrade: $MN_OUT"
    t_assert_contains "$MN_OUT" "Agency Huddle 1.3.0 installed." "new version"
    t_assert_contains "$MN_OUT" "[ok] app" "app replaced"
    t_assert_contains "$MN_OUT" "[skip] node" "node kept"
    t_assert_contains "$MN_OUT" "[skip] adapter" "adapter kept"
    t_assert_eq "mine" "$(cat "$MN_DATA/marker.txt")" "the data root is untouched"
    t_assert_eq "1.3.0" "$(mn_state version)" "state version"
    t_assert_eq "$(file_sha256 "$MN_BASE/huddle-osx-arm64.tar.gz")" "$(mn_state app_sha256)" "state app_sha256"
    t_assert_eq "0" "$(ap_count_calls '^npm_ci')" "no npm ci on an app-only change"
}

test_main_force_reinstalls_the_app() {
    mn_setup
    mn_run
    : >"$AP_CALLS"
    mn_run --force
    t_assert_eq "0" "$MN_RC" "forced run: $MN_OUT"
    t_assert_contains "$MN_OUT" "[ok] app" "app reinstalled although nothing changed"
}

test_main_interrupted_run_resumes_at_the_adapter() {
    mn_setup
    : >"$MN_NPM_FAIL"
    mn_run
    t_assert_eq "1" "$MN_RC" "exit code of the failed run: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!] adapter" "the failing step is named"
    t_assert_contains "$MN_OUT" "fix:" "with a fix line"
    t_assert_not_contains "$MN_OUT" "installed." "no success line"
    t_assert_eq "v24.21.0" "$(mn_state node_version)" "node_version was written"
    t_assert_eq "" "$(mn_state lock_sha256)" "lock_sha256 was not written"
    t_assert_eq "0" "$(ap_count_calls '^run_open ')" "no launch after a failure"
    t_assert_no_file "$MN_DATA/cache/install.lock" "the lock is released after a failure"
    mn_run
    t_assert_eq "0" "$MN_RC" "resumed run: $MN_OUT"
    t_assert_contains "$MN_OUT" "[skip] app" "app already done"
    t_assert_contains "$MN_OUT" "[skip] node" "node already done"
    t_assert_contains "$MN_OUT" "[ok] adapter" "adapter resumed"
    t_assert_eq "2" "$(ap_count_calls '^npm_ci')" "npm ci ran again exactly once"
    t_assert_eq "$(file_sha256 "$MN_ROOT/tools/acp/package-lock.json")" "$(mn_state lock_sha256)" "lock_sha256 written last"
}

test_main_concurrent_run_exits_1_and_keeps_the_other_lock() {
    mn_setup
    mkdir -p "$MN_DATA/cache/install.lock"
    mn_run
    t_assert_eq "1" "$MN_RC" "exit code: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!] installer" "named step"
    t_assert_contains "$MN_OUT" "another Agency Huddle installer is already running" "message"
    t_assert_contains "$MN_OUT" "fix:" "fix line"
    t_assert_no_file "$MN_ROOT" "nothing installed"
    if [ -d "$MN_DATA/cache/install.lock" ]; then
        t_pass
    else
        t_fail "the other installer's lock must stay"
    fi
}

test_main_unmet_prerequisite_exits_2_and_creates_nothing() {
    mn_setup
    MN_MACOS=14
    mn_run
    t_assert_eq "2" "$MN_RC" "exit code: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!]" "failed prerequisite shown"
    t_assert_no_file "$MN_DATA" "no data root"
    t_assert_no_file "$MN_ROOT" "no install root"
    t_assert_no_file "$MN_HOME/Applications" "no bundle folder"
    t_assert_no_file "$AP/http.log" "no download"
}

test_main_check_only_exits_0_or_2_and_creates_nothing() {
    mn_setup
    mn_run --check-only
    t_assert_eq "0" "$MN_RC" "met: $MN_OUT"
    t_assert_contains "$MN_OUT" "All prerequisites are met." "message"
    MN_MACOS=14
    mn_run --check-only
    t_assert_eq "2" "$MN_RC" "unmet: $MN_OUT"
    t_assert_no_file "$MN_DATA" "no data root"
    t_assert_no_file "$MN_ROOT" "no install root"
    t_assert_eq "0" "$(ap_count_calls '^run_open ')" "no launch"
}

test_main_purge_data_without_uninstall_exits_3() {
    mn_setup
    mn_run --purge-data
    t_assert_eq "3" "$MN_RC" "exit code: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!]" "failure marker"
    t_assert_contains "$MN_OUT" "fix:" "fix line"
    t_assert_contains "$MN_OUT" "--uninstall" "names the missing flag"
    t_assert_no_file "$MN_DATA" "nothing created"
}

test_main_bad_arguments_print_a_fix_and_return_3() {
    mn_setup
    mn_run --bogus
    t_assert_eq "3" "$MN_RC" "unknown flag: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!]" "failure marker"
    t_assert_contains "$MN_OUT" "fix:" "fix line"
    t_assert_contains "$MN_OUT" "--bogus" "the flag is named"
    t_assert_not_contains "$MN_OUT" "line " "no shell trace"
    mn_run --version
    t_assert_eq "3" "$MN_RC" "missing value: $MN_OUT"
    rc=0
    out=$( (HOME="$MN_HOME"; HUDDLE_REPO="owner/repo"; main --base-url "$AP/nope" --install-root "$MN_ROOT") 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "bad base url: $out"
    t_assert_contains "$out" "fix:" "bad base url has a fix"
    rc=0
    out=$( (HOME=""; HUDDLE_REPO="owner/repo"; main --base-url "$MN_BASE") 2>&1 ) || rc=$?
    t_assert_eq "3" "$rc" "empty HOME: $out"
    t_assert_contains "$out" "HOME" "names HOME"
}

test_main_sign_in_note_when_interactive_and_signed_in() {
    mn_setup
    mn_run
    t_assert_contains "$MN_OUT" "Claude: signed in." "signed-in note"
    t_assert_not_contains "$MN_OUT" "auth login" "no instruction when signed in"
    t_assert_eq "1" "$(ap_count_calls '^auth_status ')" "auth status asked once"
    t_assert_eq "auth_status $MN_ROOT/tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-darwin-arm64/claude" "$(grep '^auth_status ' "$AP_CALLS")" "the bundled claude binary"
}

test_main_sign_in_note_when_interactive_and_not_signed_in() {
    mn_setup
    MN_AUTH_RC=1
    mn_run
    t_assert_eq "0" "$MN_RC" "a missing sign-in never fails the install: $MN_OUT"
    t_assert_contains "$MN_OUT" "Claude is not signed in yet. Sign in once, then start Agency Huddle:" "heading"
    t_assert_contains "$MN_OUT" "  \"$MN_ROOT/tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-darwin-arm64/claude\" auth login" "exact command line"
    t_assert_contains "$MN_OUT" "(This opens your browser. Add --console to use API billing instead of a Claude subscription.)" "browser hint"
    t_assert_not_contains "$MN_OUT" "Claude: signed in." "no signed-in line"
}

test_main_sign_in_note_when_not_interactive_only_prints_the_instruction() {
    mn_setup
    MN_INTERACTIVE=0
    MN_AUTH_RC=1
    mn_run
    t_assert_eq "0" "$MN_RC" "exit code: $MN_OUT"
    t_assert_contains "$MN_OUT" "To use Claude, sign in once before you start Agency Huddle:" "heading"
    t_assert_contains "$MN_OUT" "claude-agent-sdk-darwin-arm64/claude\" auth login" "command line"
    t_assert_eq "0" "$(ap_count_calls '^auth_status ')" "auth status is not run without a terminal"
}

test_main_no_launch_omits_open() {
    mn_setup
    mn_run --no-launch
    t_assert_eq "0" "$MN_RC" "exit code: $MN_OUT"
    t_assert_eq "0" "$(ap_count_calls '^run_open ')" "open was not called"
    t_assert_not_contains "$MN_OUT" "[ok] launch -" "no launch line"
    mn_setup
    HUDDLE_NO_LAUNCH=1
    export HUDDLE_NO_LAUNCH
    mn_run
    unset HUDDLE_NO_LAUNCH
    t_assert_eq "0" "$(ap_count_calls '^run_open ')" "HUDDLE_NO_LAUNCH works too"
}

test_main_launch_failure_is_reported_but_does_not_fail_the_install() {
    mn_setup
    mn_run
    t_assert_contains "$MN_OUT" "[ok] launch -" "launch line"
    mn_setup
    MN_OPEN_RC=1
    mn_run
    MN_OPEN_RC=0
    t_assert_eq "0" "$MN_RC" "exit code: $MN_OUT"
    t_assert_contains "$MN_OUT" "[!!] launch -" "launch failure shown"
    t_assert_contains "$MN_OUT" "fix:" "with a fix"
    t_assert_contains "$MN_OUT" "Agency Huddle 1.2.3 installed." "install summary still printed"
}

test_main_uninstall_removes_the_install_and_keeps_data() {
    mn_setup
    mn_run
    printf 'mine\n' >"$MN_DATA/marker.txt"
    mn_run --uninstall
    t_assert_eq "0" "$MN_RC" "exit code: $MN_OUT"
    t_assert_no_file "$MN_ROOT" "install root removed"
    t_assert_no_file "$MN_HOME/Applications/Agency Huddle.app" "bundle removed"
    t_assert_eq "mine" "$(cat "$MN_DATA/marker.txt")" "data kept"
    t_assert_no_file "$MN_DATA/cache/install.lock" "lock released"
    t_assert_not_contains "$MN_OUT" "Agency Huddle 1.2.3" "no install summary on uninstall"
}

test_main_uninstall_purge_removes_the_data_too() {
    mn_setup
    mn_run
    mn_run --uninstall --purge-data
    t_assert_eq "0" "$MN_RC" "exit code: $MN_OUT"
    t_assert_no_file "$MN_ROOT" "install root removed"
    t_assert_no_file "$MN_DATA" "data root removed"
}

test_main_uninstall_works_when_prerequisites_are_unmet() {
    mn_setup
    MN_MACOS=14
    mn_run --uninstall
    t_assert_eq "0" "$MN_RC" "uninstall does not need the install prerequisites: $MN_OUT"
    t_assert_contains "$MN_OUT" "[skip] install" "nothing installed"
    t_assert_no_file "$MN_DATA" "nothing created"
}

test_main_entry_paths_direct_and_piped() {
    mn_setup
    for mn_mode in direct piped; do
        out=$(mn_entry "$mn_mode" --purge-data)
        t_assert_contains "$out" "rc=3" "$mn_mode: --purge-data alone exits 3"
        t_assert_contains "$out" "fix:" "$mn_mode: with a fix line"
        out=$(mn_entry "$mn_mode" --bogus)
        t_assert_contains "$out" "rc=3" "$mn_mode: unknown flag exits 3"
        out=$(mn_entry "$mn_mode" --uninstall --install-root "$MN_ROOT" --data-root "$MN_DATA")
        t_assert_contains "$out" "[skip] install" "$mn_mode: uninstall of nothing"
        t_assert_not_contains "$out" "rc=" "$mn_mode: and exits 0"
        t_assert_no_file "$MN_DATA" "$mn_mode: nothing created"
    done
}

test_main_entry_path_installs_the_trap_and_sourcing_does_not() {
    mn_setup
    out=$(HUDDLE_INSTALL_NO_MAIN=1 HOME="$MN_HOME" "$(command -v "$T_SHELL")" -c '. "$1"; main --purge-data >/dev/null 2>&1 || true; trap' sh "$HERE/../install.sh" 2>&1 </dev/null)
    t_assert_eq "" "$out" "sourcing and calling main sets no trap"
    mkdir -p "$MN_DATA"
    out=$(mn_entry direct --uninstall --install-root "$MN_ROOT" --data-root "$MN_DATA")
    t_assert_no_file "$MN_DATA/cache/install.lock" "the entry path leaves no lock behind"
}

test_release_lock_only_removes_a_lock_this_run_took() {
    mn_setup
    mkdir -p "$MN_DATA/cache/install.lock"
    parse_args --install-root "$MN_ROOT" --data-root "$MN_DATA"
    resolve_paths
    LOCK_HELD=0
    release_lock
    if [ -d "$MN_DATA/cache/install.lock" ]; then
        t_pass
    else
        t_fail "a lock that is not ours was removed"
    fi
    rmdir "$MN_DATA/cache/install.lock"
    acquire_lock
    t_assert_eq "1" "$LOCK_HELD" "LOCK_HELD after acquire"
    if acquire_lock 2>/dev/null; then
        t_fail "a second acquire must fail"
    else
        t_pass
    fi
    release_lock
    t_assert_no_file "$MN_DATA/cache/install.lock" "released"
    t_assert_eq "0" "$LOCK_HELD" "LOCK_HELD after release"
}

test_claude_path_follows_the_host_arch() {
    mn_setup
    HOST_RID=osx-arm64
    t_assert_eq "$INSTALL_ROOT/tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-darwin-arm64/claude" "$(claude_path)" "arm64"
    HOST_RID=osx-x64
    t_assert_eq "$INSTALL_ROOT/tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-darwin-x64/claude" "$(claude_path)" "x64"
}

test_claude_auth_status_seam_returns_the_exit_code_and_discards_output() {
    mn_setup
    mkdir -p "$AP/bin"
    printf '#!/bin/sh\ncat >"%s/stdin.seen"\necho person@example.com\nexit 0\n' "$AP" >"$AP/bin/ok"
    printf '#!/bin/sh\necho person@example.com\nexit 1\n' >"$AP/bin/no"
    printf '#!/bin/sh\nexec sleep 30\n' >"$AP/bin/hang"
    chmod +x "$AP/bin/ok" "$AP/bin/no" "$AP/bin/hang"
    out=$(ap_child "rc=0; claude_auth_status '$AP/bin/ok' || rc=\$?; echo \"ok rc=\$rc\"" \
        "rc=0; claude_auth_status '$AP/bin/no' || rc=\$?; echo \"no rc=\$rc\"" \
        "rc=0; claude_auth_status '$AP/missing' || rc=\$?; echo \"missing rc=\$rc\"" \
        "AUTH_STATUS_SECONDS=1; rc=0; claude_auth_status '$AP/bin/hang' || rc=\$?; echo \"hang rc=\$rc\"")
    t_assert_contains "$out" "ok rc=0" "signed in"
    t_assert_contains "$out" "no rc=1" "not signed in"
    t_assert_not_contains "$out" "missing rc=0" "a missing binary is not signed in"
    t_assert_contains "$out" "missing rc=" "missing binary answered"
    t_assert_not_contains "$out" "hang rc=0" "a hung status call is not signed in"
    t_assert_contains "$out" "hang rc=" "a hung status call returned"
    t_assert_not_contains "$out" "person@example.com" "the account is never printed"
    if [ -s "$AP/stdin.seen" ]; then
        t_fail "the status call must read /dev/null"
    else
        t_pass
    fi
}

test_run_open_and_is_interactive_seams() {
    mn_setup
    mkdir -p "$AP/bin"
    printf '#!/bin/sh\nprintf "%%s|" "$@" >"%s/open.args"\ncat >"%s/open.stdin"\n' "$AP" "$AP" >"$AP/bin/open"
    chmod +x "$AP/bin/open"
    out=$(ap_child "PATH='$AP/bin':\$PATH" "run_open '/some dir/Agency Huddle.app'; echo \"rc=\$?\"")
    t_assert_contains "$out" "rc=0" "run_open succeeded"
    t_assert_eq "/some dir/Agency Huddle.app|" "$(cat "$AP/open.args")" "open receives the bundle path as one argument"
    t_assert_eq "" "$(cat "$AP/open.stdin")" "open reads /dev/null"
    out=$(ap_child "unset NO_BROWSER SSH_CONNECTION SSH_CLIENT SSH_TTY CLAUDE_CODE_REMOTE" \
        "if interactive_environment; then echo yes; else echo no; fi" \
        "NO_BROWSER=1; if interactive_environment; then echo yes; else echo no; fi" \
        "NO_BROWSER=; SSH_CONNECTION=x; if interactive_environment; then echo yes; else echo no; fi" \
        "SSH_CONNECTION=; SSH_TTY=x; if interactive_environment; then echo yes; else echo no; fi" \
        "SSH_TTY=; CLAUDE_CODE_REMOTE=1; if interactive_environment; then echo yes; else echo no; fi" \
        "CLAUDE_CODE_REMOTE=; if is_interactive </dev/null; then echo yes; else echo no; fi")
    t_assert_eq "yes${NL}no${NL}no${NL}no${NL}no${NL}no" "$out" "NO_BROWSER, SSH_* and CLAUDE_CODE_REMOTE mean not interactive; no terminal on stdout means no"
}

t_run test_shell_quote_empty_string
t_run test_shell_quote_no_special_chars
t_run test_shell_quote_single_quote
t_run test_shell_quote_multiple_quotes
t_run test_shell_quote_space_and_quote
t_run test_launcher_content_basic
t_run test_launcher_content_with_space_in_paths
t_run test_launcher_content_with_quote_in_path
t_run test_app_stub_content_basic
t_run test_app_stub_content_with_space_and_quote
t_run test_plist_content_basic
t_run test_plist_content_escapes_xml_special_chars
t_run test_launcher_content_lines_exact_order

t_run test_t_run_honours_t_only_and_prints_run_lines_only_for_failures
t_run test_install_adapter_runs_npm_ci_when_nothing_is_installed
t_run test_install_adapter_is_skipped_when_the_lock_hash_matches_and_the_entry_exists
t_run test_install_adapter_runs_when_the_entry_is_missing
t_run test_install_adapter_runs_when_the_lock_hash_differs
t_run test_install_adapter_stops_a_running_app_only_when_it_will_change
t_run test_install_adapter_survivor_aborts_before_npm_ci
t_run test_install_adapter_failure_shows_the_log_tail_and_a_proxy_fix
t_run test_install_adapter_verifies_the_entry_afterwards
t_run test_install_adapter_needs_the_bundled_node_and_a_lock_file
t_run test_run_npm_ci_runs_the_bundled_node_on_the_bundled_npm
t_run test_install_adapter_runs_npm_ci_in_a_staged_copy_and_swaps_node_modules_in
t_run test_install_adapter_failure_leaves_the_previous_node_modules_untouched
t_run test_install_adapter_missing_entry_in_the_staged_tree_leaves_the_previous_node_modules
t_run test_install_adapter_rolls_back_when_the_swap_fails
t_run test_install_adapter_needs_a_package_json
t_run test_install_adapter_replaces_a_leftover_staging_folder
t_run test_install_adapter_uses_local_variables
t_run test_install_node_fresh_install
t_run test_install_node_is_skipped_when_state_and_binary_agree
t_run test_install_node_is_not_skipped_when_the_binary_is_missing
t_run test_install_node_is_not_skipped_when_the_binary_reports_another_version
t_run test_install_node_replaces_the_node_on_a_version_change
t_run test_install_node_fresh_install_never_stops_the_app
t_run test_install_node_survivor_aborts_the_upgrade_untouched
t_run test_install_node_rejects_a_tampered_tarball
t_run test_install_node_rejects_an_archive_without_npm
t_run test_install_node_arch_x64_fetches_the_darwin_x64_name
t_run test_install_node_unknown_arch_fails
t_run test_install_node_needs_a_node_version_in_release_info
t_run test_install_node_keeps_the_release_sums
t_run test_install_node_defaults_to_nodejs_org
t_run test_install_node_uses_local_variables
t_run test_install_app_fresh_install
t_run test_install_app_clears_quarantine_on_the_install_root
t_run test_install_app_signs_only_macho_files_that_fail_verification
t_run test_ensure_signed_handles_spaces_and_reports_a_signing_failure
t_run test_install_app_second_run_with_matching_state_does_nothing
t_run test_install_app_reinstalls_when_the_host_is_missing_or_the_hash_differs
t_run test_install_app_upgrade_keeps_node_modules_state_and_launcher
t_run test_install_app_archive_without_host_fails_and_changes_nothing
t_run test_install_app_corrupt_archive_fails_and_changes_nothing
t_run test_install_app_failed_swap_rolls_back
t_run test_install_app_recovers_a_swap_that_died_between_its_renames
t_run test_install_app_stops_a_running_instance_before_the_swap
t_run test_install_app_fresh_install_never_stops
t_run test_install_app_survivor_aborts_the_upgrade_untouched
t_run test_install_app_refuses_an_install_root_that_is_not_a_huddle_install
t_run test_install_app_refuses_an_install_root_that_is_a_file
t_run test_install_app_accepts_an_empty_install_root
t_run test_install_app_upgrades_a_folder_with_either_marker
t_run test_install_app_does_not_delete_an_unmarked_sibling_old_or_staging
t_run test_install_app_does_not_restore_an_unmarked_old_into_a_missing_root
t_run test_install_app_deletes_a_marked_leftover_old_and_staging
t_run test_install_app_refuses_the_home_folder_even_with_a_marker
t_run test_install_root_check_applies_the_safe_path_rules
t_run test_install_app_with_force_reinstalls_although_the_state_matches
t_run test_stop_waits_for_the_process_to_exit_after_the_stop_request
t_run test_stop_gives_up_after_ten_seconds_and_names_the_force_switches
t_run test_stop_without_a_survivor_does_not_sleep
t_run test_app_running_seam_sees_live_and_dead_processes
t_run test_stop_without_an_instance_record_never_calls_stop
t_run test_stop_with_an_instance_record_calls_stop
t_run test_stop_survivor_fails_without_force
t_run test_stop_survivor_is_ended_with_force
t_run test_stop_failed_request_without_a_pid_decides_by_exit_code
t_run test_stop_never_waits_for_or_kills_an_unrelated_process_behind_a_stale_pid
t_run test_stop_still_waits_for_and_kills_the_real_app_pid
t_run test_stop_skips_the_pid_wait_when_the_stop_verb_succeeded_and_removed_the_record
t_run test_process_command_of_prints_the_executable_name_from_ps
t_run test_resolve_asset_adds_the_v_of_the_release_tag_to_a_bare_version
t_run test_folder_is_replaceable_needs_the_host_next_to_release_info
t_run test_install_app_refuses_a_folder_with_only_a_release_info_file
t_run test_run_huddle_stop_prefers_the_launcher
t_run test_run_huddle_stop_falls_back_to_the_host_with_the_data_dir
t_run test_is_macho_reads_file_output
t_run test_run_kill_process_kills_and_waits_a_bounded_time
t_run test_process_running_sees_live_and_dead_processes
t_run test_install_app_uses_local_variables
t_run test_install_app_leaves_the_script_with_one_placeholder

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
t_run test_parse_args_rejects_a_version_that_is_not_latest_or_a_release_tag
t_run test_parse_args_accepts_latest_and_release_tags
t_run test_parse_args_makes_the_roots_absolute_without_requiring_them_to_exist
t_run test_parse_args_refuses_an_empty_or_root_folder
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

# ---- Task 36: install_launcher -------------------------------------------------------------------

# Test doubles for the launcher seams. A "symlink" is a regular file whose first line is
# SYMLINK:<target>: `ln -s` in Git Bash may copy instead of linking, so the tests assert on the
# seam calls (kept in $AP_CALLS) and on this stand-in. They stay installed after la_setup.
la_stubs() {
    make_symlink() {
        printf 'make_symlink %s %s\n' "$1" "$2" >>"$AP_CALLS"
        printf 'SYMLINK:%s\n' "$1" >"$2"
    }
    read_symlink() {
        local first
        [ -f "$1" ] || return 1
        first=$(head -1 "$1")
        case $first in
            SYMLINK:*)
                printf '%s' "${first#SYMLINK:}"
                return 0
                ;;
        esac
        return 1
    }
    remove_symlink() {
        printf 'remove_symlink %s\n' "$1" >>"$AP_CALLS"
        rm -f "$1"
    }
    make_executable() {
        printf 'make_executable %s\n' "$1" >>"$AP_CALLS"
        chmod 755 "$1"
    }
}

# A sandbox in the default layout (InstallRoot inside DataRoot, spaces in every path) with a
# release-info.json, an empty bundle path and an empty link path.
la_setup() {
    ap_setup
    la_stubs
    DATA_ROOT="$AP/data dir"
    parse_args --install-root "$DATA_ROOT/install" --data-root "$DATA_ROOT"
    resolve_paths
    LA_HOME="$AP/home dir"
    APP_BUNDLE="$LA_HOME/Applications/Agency Huddle.app"
    LINK_PATH="$LA_HOME/.local/bin/huddle"
    mkdir -p "$INSTALL_ROOT"
    printf '{"version": "1.2.3", "nodeVersion": "v24.21.0"}\n' >"$INSTALL_ROOT/release-info.json"
    LAUNCHER_CHANGED=unset
}

# la_run: runs install_launcher in this shell; sets LA_RC and LA_OUT (stdout and stderr).
la_run() {
    LA_RC=0
    install_launcher >"$AP/out" 2>&1 || LA_RC=$?
    LA_OUT=$(cat "$AP/out")
}

# la_snapshot: a sorted listing of everything the launcher step may touch.
la_snapshot() {
    find "$INSTALL_ROOT" "$APP_BUNDLE" "$LA_HOME/.local" 2>/dev/null | sort
}

test_resolve_paths_app_bundle_and_link_path() {
    parse_args
    resolve_paths
    t_assert_eq "$HOME/Applications/Agency Huddle.app" "$APP_BUNDLE" "APP_BUNDLE"
    t_assert_eq "$HOME/.local/bin/huddle" "$LINK_PATH" "LINK_PATH"
}

test_install_launcher_first_run_creates_every_entry_point() {
    la_setup
    la_run
    t_assert_eq "0" "$LA_RC" "exit code"
    t_assert_eq "1" "$LAUNCHER_CHANGED" "LAUNCHER_CHANGED"
    t_assert_file "$INSTALL_ROOT/bin/huddle" "bin/huddle"
    t_assert_eq "$(launcher_content)" "$(cat "$INSTALL_ROOT/bin/huddle")" "launcher text"
    if [ -x "$INSTALL_ROOT/bin/huddle" ]; then t_pass; else t_fail "bin/huddle is not executable"; fi
    t_assert_file "$APP_BUNDLE/Contents/Info.plist" "Info.plist"
    t_assert_eq "$(plist_content 1.2.3)" "$(cat "$APP_BUNDLE/Contents/Info.plist")" "plist text carries the release version"
    t_assert_file "$APP_BUNDLE/Contents/MacOS/AgencyHuddle" "app stub"
    t_assert_eq "$(app_stub_content)" "$(cat "$APP_BUNDLE/Contents/MacOS/AgencyHuddle")" "stub text"
    if [ -x "$APP_BUNDLE/Contents/MacOS/AgencyHuddle" ]; then t_pass; else t_fail "app stub is not executable"; fi
    calls=$(cat "$AP_CALLS")
    t_assert_contains "$calls" "make_executable " "chmod went through the seam"
    t_assert_contains "$calls" "make_symlink $INSTALL_ROOT/bin/huddle $LINK_PATH" "symlink through the seam, to bin/huddle"
    t_assert_eq "$INSTALL_ROOT/bin/huddle" "$(read_symlink "$LINK_PATH")" "link target"
    t_assert_contains "$LA_OUT" "[ok] launcher" "step line"
}

test_install_launcher_second_run_writes_nothing() {
    la_setup
    la_run
    find "$INSTALL_ROOT" "$APP_BUNDLE" "$LA_HOME/.local" -type f -exec touch -t 200001010000 {} +
    touch -t 200101010000 "$AP/ref"
    before=$(la_snapshot)
    : >"$AP_CALLS"
    la_run
    t_assert_eq "0" "$LA_RC" "exit code"
    t_assert_eq "0" "$LAUNCHER_CHANGED" "LAUNCHER_CHANGED"
    t_assert_eq "" "$(find "$INSTALL_ROOT" "$APP_BUNDLE" "$LA_HOME/.local" -type f -newer "$AP/ref")" "no file has a new mtime"
    t_assert_eq "$before" "$(la_snapshot)" "no file or folder was added or removed"
    t_assert_eq "" "$(cat "$AP_CALLS")" "no seam was called (no chmod, no ln, no rm)"
    t_assert_contains "$LA_OUT" "[skip] launcher" "skip line"
    t_assert_not_contains "$LA_OUT" "[ok]" "nothing reported as changed"
}

test_install_launcher_rewrites_changed_content() {
    la_setup
    la_run
    printf 'old\n' >"$INSTALL_ROOT/bin/huddle"
    printf 'old\n' >"$APP_BUNDLE/Contents/Info.plist"
    printf 'old\n' >"$APP_BUNDLE/Contents/MacOS/AgencyHuddle"
    chmod 644 "$APP_BUNDLE/Contents/MacOS/AgencyHuddle"
    la_run
    t_assert_eq "0" "$LA_RC" "exit code"
    t_assert_eq "1" "$LAUNCHER_CHANGED" "LAUNCHER_CHANGED"
    t_assert_eq "$(launcher_content)" "$(cat "$INSTALL_ROOT/bin/huddle")" "launcher restored"
    t_assert_eq "$(plist_content 1.2.3)" "$(cat "$APP_BUNDLE/Contents/Info.plist")" "plist restored"
    t_assert_eq "$(app_stub_content)" "$(cat "$APP_BUNDLE/Contents/MacOS/AgencyHuddle")" "stub restored"
    if [ -x "$APP_BUNDLE/Contents/MacOS/AgencyHuddle" ]; then t_pass; else t_fail "app stub is not executable again"; fi
    t_assert_not_contains "$(cat "$AP_CALLS")" "remove_symlink" "the correct link is left alone"
}

test_install_launcher_rewrites_when_the_version_changes() {
    la_setup
    la_run
    printf '{"version": "2.0.0", "nodeVersion": "v24.21.0"}\n' >"$INSTALL_ROOT/release-info.json"
    la_run
    t_assert_eq "1" "$LAUNCHER_CHANGED" "LAUNCHER_CHANGED"
    t_assert_eq "$(plist_content 2.0.0)" "$(cat "$APP_BUNDLE/Contents/Info.plist")" "plist carries the new version"
}

test_install_launcher_replaces_a_symlink_that_points_elsewhere() {
    la_setup
    mkdir -p "$LA_HOME/.local/bin"
    printf 'SYMLINK:/somewhere/else/huddle\n' >"$LINK_PATH"
    la_run
    t_assert_eq "0" "$LA_RC" "exit code"
    t_assert_eq "$INSTALL_ROOT/bin/huddle" "$(read_symlink "$LINK_PATH")" "link now points at bin/huddle"
    calls=$(cat "$AP_CALLS")
    t_assert_contains "$calls" "remove_symlink $LINK_PATH" "old link removed through the seam"
    t_assert_contains "$calls" "make_symlink $INSTALL_ROOT/bin/huddle $LINK_PATH" "new link made through the seam"
    t_assert_eq "1" "$LAUNCHER_CHANGED" "LAUNCHER_CHANGED"
}

test_install_launcher_never_overwrites_a_regular_file_at_the_link_path() {
    la_setup
    mkdir -p "$LA_HOME/.local/bin"
    printf 'my own script\n' >"$LINK_PATH"
    la_run
    t_assert_eq "0" "$LA_RC" "the run continues"
    t_assert_eq "my own script" "$(cat "$LINK_PATH")" "file untouched"
    t_assert_not_contains "$(cat "$AP_CALLS")" "make_symlink" "no link made"
    t_assert_not_contains "$(cat "$AP_CALLS")" "remove_symlink" "nothing removed"
    t_assert_contains "$LA_OUT" "[warn] link" "warning line"
    t_assert_contains "$LA_OUT" "$LINK_PATH" "warning names the path"
    t_assert_file "$INSTALL_ROOT/bin/huddle" "the other entry points are still written"
    t_assert_file "$APP_BUNDLE/Contents/Info.plist" "the bundle is still written"
}

test_install_launcher_hints_when_the_link_folder_is_not_on_path() {
    la_setup
    saved_path=$PATH
    la_run
    hints=$(printf '%s\n' "$LA_OUT" | grep -c 'hint:' || true)
    t_assert_eq "1" "$hints" "exactly one hint line"
    t_assert_contains "$LA_OUT" "hint: " "hint text"
    t_assert_contains "$LA_OUT" "$LA_HOME/.local/bin" "hint names the folder"
    for rc_file in .zshrc .zprofile .bashrc .bash_profile .profile; do
        t_assert_no_file "$HOME/$rc_file" "no shell startup file written ($rc_file)"
    done
    PATH="$LA_HOME/.local/bin:$saved_path"
    la_run
    PATH=$saved_path
    t_assert_not_contains "$LA_OUT" "hint:" "no hint when the folder is on PATH"
}

test_install_launcher_fails_without_release_info() {
    la_setup
    rm -f "$INSTALL_ROOT/release-info.json"
    la_run
    t_assert_eq "1" "$LA_RC" "exit code"
    t_assert_contains "$LA_OUT" "[!!] launcher" "failure line"
    t_assert_contains "$LA_OUT" "fix:" "fix line"
    t_assert_eq "0" "$LAUNCHER_CHANGED" "LAUNCHER_CHANGED"
    t_assert_no_file "$APP_BUNDLE/Contents/Info.plist" "no plist without a version"
}

test_install_launcher_protects_caller_variables() {
    la_setup
    dest="KEEP"
    mode="KEEP"
    scratch="KEEP"
    version="KEEP"
    link_dir="KEEP"
    target="KEEP"
    changed="KEEP"
    la_run
    t_assert_eq "KEEP" "$dest" "dest"
    t_assert_eq "KEEP" "$mode" "mode"
    t_assert_eq "KEEP" "$scratch" "scratch"
    t_assert_eq "KEEP" "$version" "version"
    t_assert_eq "KEEP" "$link_dir" "link_dir"
    t_assert_eq "KEEP" "$target" "target"
    t_assert_eq "KEEP" "$changed" "changed"
}

test_launcher_real_seams() {
    la_setup
    printf '#!/bin/sh\n# plain\n' >"$AP/plain"
    out=$(ap_child "chmod 644 '$AP/plain'" "make_executable '$AP/plain'" "[ -x '$AP/plain' ] && echo exec=yes || echo exec=no" "read_symlink '$AP/plain' && echo link=yes || echo link=no")
    t_assert_contains "$out" "exec=yes" "make_executable sets the execute bit"
    t_assert_contains "$out" "link=no" "a regular file is not a symlink"
}

t_run test_resolve_paths_app_bundle_and_link_path
t_run test_install_launcher_first_run_creates_every_entry_point
t_run test_install_launcher_second_run_writes_nothing
t_run test_install_launcher_rewrites_changed_content
t_run test_install_launcher_rewrites_when_the_version_changes
t_run test_install_launcher_replaces_a_symlink_that_points_elsewhere
t_run test_install_launcher_never_overwrites_a_regular_file_at_the_link_path
t_run test_install_launcher_hints_when_the_link_folder_is_not_on_path
t_run test_install_launcher_fails_without_release_info
t_run test_install_launcher_protects_caller_variables
t_run test_launcher_real_seams

# ---- Task 37: safe_to_delete and do_uninstall ------------------------------------------------------

# un_setup: a complete install in the default layout (DataRoot/install inside DataRoot) built by the
# launcher step, plus the state marker, an app host, user data and a cache. Uses the launcher
# doubles, so the "symlink" is a stand-in file.
un_setup() {
    la_setup
    la_run
    printf 'schema=1\nversion=1.2.3\n' >"$INSTALL_ROOT/install-state"
    mkdir -p "$INSTALL_ROOT/app" "$DATA_ROOT/data" "$DATA_ROOT/cache"
    printf '#!/bin/sh\n' >"$INSTALL_ROOT/app/Huddle.App"
    printf 'my rooms\n' >"$DATA_ROOT/data/rooms.json"
    printf 'download\n' >"$DATA_ROOT/cache/asset.tar.gz"
    : >"$AP_CALLS"
}

# un_run: runs do_uninstall in this shell; sets UN_RC and UN_OUT (stdout and stderr).
un_run() {
    UN_RC=0
    do_uninstall >"$AP/out" 2>&1 || UN_RC=$?
    UN_OUT=$(cat "$AP/out")
}

# un_refusal <install root> [home]: runs do_uninstall in a child shell where every deleting seam only
# prints "REMOVE <path>", so a refusal is proven without risking the real folder. Prints the output
# and an "rc=<n>" line.
un_refusal() {
    ap_child "remove_tree() { printf 'REMOVE %s\\n' \"\$1\"; }" \
        "remove_symlink() { printf 'REMOVE %s\\n' \"\$1\"; }" \
        "HOME='${2:-$HOME}'" \
        "INSTALL_ROOT='$1'" \
        "DATA_ROOT='$AP/data dir'" \
        "APP_BUNDLE='$AP/absent.app'" \
        "LINK_PATH='$AP/absent-link'" \
        "rc=0" \
        "do_uninstall || rc=\$?" \
        "echo rc=\$rc"
}

test_safe_to_delete_accepts_a_folder_with_the_marker() {
    un_setup
    rc=0
    safe_to_delete "$INSTALL_ROOT" install-state || rc=$?
    t_assert_eq "0" "$rc" "install folder with install-state"
    t_assert_contains "$SAFE_CANON" "data dir/install" "canonical path is returned"
}

test_safe_to_delete_rejects_without_the_marker() {
    un_setup
    rm -f "$INSTALL_ROOT/install-state"
    rc=0
    safe_to_delete "$INSTALL_ROOT" install-state || rc=$?
    t_assert_eq "1" "$rc" "no marker"
    t_assert_contains "$SAFE_REASON" "install-state" "reason names the marker"
}

test_safe_to_delete_rejects_empty_missing_relative_and_unmarked_calls() {
    un_setup
    rc=0
    safe_to_delete "" install-state || rc=$?
    t_assert_eq "1" "$rc" "empty path"
    rc=0
    safe_to_delete "$AP/does not exist" install-state || rc=$?
    t_assert_eq "1" "$rc" "missing path"
    rc=0
    safe_to_delete "relative/dir" install-state || rc=$?
    t_assert_eq "1" "$rc" "relative path"
    rc=0
    safe_to_delete "$INSTALL_ROOT" "" || rc=$?
    t_assert_eq "1" "$rc" "a call without a marker is refused"
}

test_do_uninstall_removes_install_bundle_and_link_and_keeps_the_data() {
    un_setup
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_no_file "$INSTALL_ROOT" "InstallRoot removed"
    t_assert_no_file "$APP_BUNDLE" "app bundle removed"
    t_assert_no_file "$LINK_PATH" "link removed"
    t_assert_contains "$(cat "$AP_CALLS")" "remove_symlink $LINK_PATH" "link removed through the seam"
    t_assert_file "$DATA_ROOT/data/rooms.json" "user data kept"
    t_assert_file "$DATA_ROOT/cache/asset.tar.gz" "cache kept"
    t_assert_contains "$UN_OUT" "kept" "tells the user the data was kept"
    t_assert_not_contains "$UN_OUT" "[!!]" "no failure line"
}

test_do_uninstall_purge_data_also_removes_the_data_root() {
    un_setup
    parse_args --install-root "$INSTALL_ROOT" --data-root "$DATA_ROOT" --purge-data
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_no_file "$INSTALL_ROOT" "InstallRoot removed"
    t_assert_no_file "$DATA_ROOT" "DataRoot removed"
    t_assert_no_file "$LINK_PATH" "link removed"
}

test_do_uninstall_purge_after_a_plain_uninstall_removes_the_kept_data() {
    un_setup
    un_run
    t_assert_file "$DATA_ROOT/data/rooms.json" "kept by the first run"
    parse_args --install-root "$INSTALL_ROOT" --data-root "$DATA_ROOT" --purge-data
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_no_file "$DATA_ROOT" "DataRoot removed by the second run"
    t_assert_contains "$UN_OUT" "[skip] install" "the install was already gone"
}

test_do_uninstall_second_run_skips_and_succeeds() {
    un_setup
    un_run
    : >"$AP_CALLS"
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_contains "$UN_OUT" "[skip] install" "install skipped"
    t_assert_contains "$UN_OUT" "[skip] app" "bundle skipped"
    t_assert_contains "$UN_OUT" "[skip] link" "link skipped"
    t_assert_not_contains "$UN_OUT" "[ok]" "nothing reported as removed"
    t_assert_not_contains "$UN_OUT" "[!!]" "no failure"
    t_assert_eq "" "$(cat "$AP_CALLS")" "nothing was touched"
    t_assert_file "$DATA_ROOT/data/rooms.json" "user data still kept"
}

test_do_uninstall_with_nothing_anywhere_prints_skip_before_canonicalising() {
    la_setup
    parse_args --install-root "$AP/never/was/here/install" --data-root "$AP/never/was/here" --purge-data
    resolve_paths
    APP_BUNDLE="$AP/never/was/here/Agency Huddle.app"
    LINK_PATH="$AP/never/was/here/huddle"
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_contains "$UN_OUT" "[skip] install" "install skipped"
    t_assert_contains "$UN_OUT" "[skip] data" "data skipped"
    t_assert_not_contains "$UN_OUT" "[!!]" "no failure"
    t_assert_no_file "$AP/never" "nothing was created"
}

test_do_uninstall_refuses_a_folder_without_the_marker() {
    un_setup
    rm -f "$INSTALL_ROOT/install-state"
    un_run
    t_assert_eq "1" "$UN_RC" "exit code"
    t_assert_contains "$UN_OUT" "[!!]" "failure line"
    t_assert_contains "$UN_OUT" "fix:" "fix line"
    t_assert_file "$INSTALL_ROOT/app/Huddle.App" "install untouched"
    t_assert_file "$APP_BUNDLE/Contents/Info.plist" "bundle untouched"
    t_assert_file "$LINK_PATH" "link untouched"
    t_assert_file "$DATA_ROOT/data/rooms.json" "data untouched"
    t_assert_not_contains "$(cat "$AP_CALLS")" "remove_symlink" "no removal call"
}

test_do_uninstall_refuses_dangerous_roots_and_deletes_nothing() {
    un_setup
    mkdir -p "$AP/hh" "$AP/up/hh"
    printf 'schema=1\n' >"$AP/hh/install-state"
    printf 'schema=1\n' >"$AP/up/install-state"
    out=$(un_refusal "/")
    t_assert_contains "$out" "rc=1" "root: exit code"
    t_assert_contains "$out" "filesystem root" "root: reason"
    t_assert_not_contains "$out" "REMOVE" "root: nothing removed"
    out=$(un_refusal "$AP/hh" "$AP/hh")
    t_assert_contains "$out" "rc=1" "HOME: exit code"
    t_assert_contains "$out" "home folder" "HOME: reason"
    t_assert_not_contains "$out" "REMOVE" "HOME: nothing removed"
    out=$(un_refusal "$AP/up" "$AP/up/hh")
    t_assert_contains "$out" "rc=1" "parent of HOME: exit code"
    t_assert_contains "$out" "home folder" "parent of HOME: reason"
    t_assert_not_contains "$out" "REMOVE" "parent of HOME: nothing removed"
    out=$(un_refusal "/usr")
    t_assert_contains "$out" "rc=1" "one segment: exit code"
    t_assert_contains "$out" "segments" "one segment: reason"
    t_assert_not_contains "$out" "REMOVE" "one segment: nothing removed"
    out=$(un_refusal "/usr/bin")
    t_assert_contains "$out" "rc=1" "two segments: exit code"
    t_assert_contains "$out" "segments" "two segments: reason"
    t_assert_not_contains "$out" "REMOVE" "two segments: nothing removed"
    out=$(un_refusal "")
    t_assert_contains "$out" "rc=1" "empty: exit code"
    t_assert_contains "$out" "empty" "empty: reason"
    t_assert_not_contains "$out" "REMOVE" "empty: nothing removed"
    t_assert_file "$AP/hh/install-state" "marker folder untouched"
}

test_do_uninstall_refuses_when_the_data_root_is_inside_the_install_root() {
    un_setup
    parse_args --install-root "$AP/shared" --data-root "$AP/shared/data dir"
    resolve_paths
    APP_BUNDLE="$AP/absent.app"
    LINK_PATH="$AP/absent-link"
    mkdir -p "$AP/shared/data dir/data"
    printf 'schema=1\n' >"$AP/shared/install-state"
    printf 'keep me\n' >"$AP/shared/data dir/data/rooms.json"
    un_run
    t_assert_eq "1" "$UN_RC" "exit code"
    t_assert_contains "$UN_OUT" "--purge-data" "tells the user how to remove both"
    t_assert_file "$AP/shared/data dir/data/rooms.json" "data survived"
    t_assert_file "$AP/shared/install-state" "install untouched"
}

test_do_uninstall_purge_refuses_a_data_root_that_does_not_look_like_ours() {
    un_setup
    mkdir -p "$AP/other stuff/docs"
    printf 'precious\n' >"$AP/other stuff/docs/a.txt"
    parse_args --install-root "$INSTALL_ROOT" --data-root "$AP/other stuff" --purge-data
    DATA_ROOT="$AP/other stuff"
    un_run
    t_assert_eq "1" "$UN_RC" "exit code"
    t_assert_contains "$UN_OUT" "[!!]" "failure line"
    t_assert_file "$AP/other stuff/docs/a.txt" "their files survived"
    t_assert_file "$INSTALL_ROOT/install-state" "validation happens before any deletion"
}

test_do_uninstall_leaves_a_link_that_points_elsewhere() {
    un_setup
    printf 'SYMLINK:/somewhere/else/huddle\n' >"$LINK_PATH"
    : >"$AP_CALLS"
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_no_file "$INSTALL_ROOT" "install removed"
    t_assert_file "$LINK_PATH" "foreign link left alone"
    t_assert_contains "$UN_OUT" "[skip] link" "link skipped"
    t_assert_not_contains "$(cat "$AP_CALLS")" "remove_symlink" "link not removed"
}

test_do_uninstall_leaves_a_regular_file_at_the_link_path() {
    un_setup
    printf 'my own script\n' >"$LINK_PATH"
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_eq "my own script" "$(cat "$LINK_PATH")" "file untouched"
    t_assert_no_file "$INSTALL_ROOT" "install removed"
}

test_do_uninstall_leaves_a_bundle_folder_that_is_not_ours() {
    un_setup
    rm -f "$APP_BUNDLE/Contents/Info.plist"
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_file "$APP_BUNDLE/Contents/MacOS/AgencyHuddle" "folder left alone"
    t_assert_contains "$UN_OUT" "[warn] app" "warning line"
    t_assert_no_file "$INSTALL_ROOT" "install removed"
}

test_do_uninstall_stops_a_running_instance_before_deleting() {
    un_setup
    ap_instance_json 4242
    run_huddle_stop() {
        if [ -f "$INSTALL_ROOT/install-state" ] && [ -x "$INSTALL_ROOT/bin/huddle" ]; then
            printf 'stop install_present=1\n' >>"$AP_CALLS"
        else
            printf 'stop install_present=0\n' >>"$AP_CALLS"
        fi
        return 0
    }
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_contains "$(cat "$AP_CALLS")" "stop install_present=1" "the stop request ran while the launcher still existed"
    t_assert_no_file "$INSTALL_ROOT" "install removed afterwards"
}

test_do_uninstall_does_not_delete_when_the_app_will_not_stop() {
    un_setup
    ap_instance_json 4242
    AP_STOP_RC=1
    AP_ALIVE=1
    un_run
    t_assert_eq "1" "$UN_RC" "exit code"
    t_assert_contains "$UN_OUT" "[!!] stop" "stop failure shown"
    t_assert_file "$INSTALL_ROOT/install-state" "install untouched"
    t_assert_file "$APP_BUNDLE/Contents/Info.plist" "bundle untouched"
    t_assert_file "$LINK_PATH" "link untouched"
}

test_do_uninstall_does_not_stop_anything_when_nothing_is_installed() {
    la_setup
    parse_args --install-root "$AP/never/install" --data-root "$AP/never"
    resolve_paths
    APP_BUNDLE="$AP/never/a.app"
    LINK_PATH="$AP/never/huddle"
    ap_instance_json 4242
    un_run
    t_assert_eq "0" "$UN_RC" "exit code"
    t_assert_eq "" "$(cat "$AP_CALLS")" "no stop request"
}

test_do_uninstall_protects_caller_variables() {
    un_setup
    have_install="KEEP"
    current="KEEP"
    purge="KEEP"
    canon="KEEP"
    dir="KEEP"
    marker="KEEP"
    home_canon="KEEP"
    un_run
    safe_to_delete "$AP" install-state || true
    t_assert_eq "KEEP" "$have_install" "have_install"
    t_assert_eq "KEEP" "$current" "current"
    t_assert_eq "KEEP" "$purge" "purge"
    t_assert_eq "KEEP" "$canon" "canon"
    t_assert_eq "KEEP" "$dir" "dir"
    t_assert_eq "KEEP" "$marker" "marker"
    t_assert_eq "KEEP" "$home_canon" "home_canon"
}

test_remove_tree_refuses_empty_and_root() {
    rc=0
    remove_tree "" || rc=$?
    t_assert_eq "1" "$rc" "empty"
    rc=0
    remove_tree "/" || rc=$?
    t_assert_eq "1" "$rc" "root"
}

t_run test_safe_to_delete_accepts_a_folder_with_the_marker
t_run test_safe_to_delete_rejects_without_the_marker
t_run test_safe_to_delete_rejects_empty_missing_relative_and_unmarked_calls
t_run test_do_uninstall_removes_install_bundle_and_link_and_keeps_the_data
t_run test_do_uninstall_purge_data_also_removes_the_data_root
t_run test_do_uninstall_purge_after_a_plain_uninstall_removes_the_kept_data
t_run test_do_uninstall_second_run_skips_and_succeeds
t_run test_do_uninstall_with_nothing_anywhere_prints_skip_before_canonicalising
t_run test_do_uninstall_refuses_a_folder_without_the_marker
t_run test_do_uninstall_refuses_dangerous_roots_and_deletes_nothing
t_run test_do_uninstall_refuses_when_the_data_root_is_inside_the_install_root
t_run test_do_uninstall_purge_refuses_a_data_root_that_does_not_look_like_ours
t_run test_do_uninstall_leaves_a_link_that_points_elsewhere
t_run test_do_uninstall_leaves_a_regular_file_at_the_link_path
t_run test_do_uninstall_leaves_a_bundle_folder_that_is_not_ours
t_run test_do_uninstall_stops_a_running_instance_before_deleting
t_run test_do_uninstall_does_not_delete_when_the_app_will_not_stop
t_run test_do_uninstall_does_not_stop_anything_when_nothing_is_installed
t_run test_do_uninstall_protects_caller_variables
t_run test_remove_tree_refuses_empty_and_root
t_run test_main_refuses_a_foreign_install_root_and_a_bad_version
t_run test_main_refuses_nested_data_and_install_folders
t_run test_main_first_run_installs_everything
t_run test_main_second_run_skips_everything_and_changes_no_file
t_run test_main_changed_archive_runs_only_the_app_step
t_run test_main_force_reinstalls_the_app
t_run test_main_interrupted_run_resumes_at_the_adapter
t_run test_main_concurrent_run_exits_1_and_keeps_the_other_lock
t_run test_main_unmet_prerequisite_exits_2_and_creates_nothing
t_run test_main_check_only_exits_0_or_2_and_creates_nothing
t_run test_main_purge_data_without_uninstall_exits_3
t_run test_main_bad_arguments_print_a_fix_and_return_3
t_run test_main_sign_in_note_when_interactive_and_signed_in
t_run test_main_sign_in_note_when_interactive_and_not_signed_in
t_run test_main_sign_in_note_when_not_interactive_only_prints_the_instruction
t_run test_main_no_launch_omits_open
t_run test_main_launch_failure_is_reported_but_does_not_fail_the_install
t_run test_main_uninstall_removes_the_install_and_keeps_data
t_run test_main_uninstall_purge_removes_the_data_too
t_run test_main_uninstall_works_when_prerequisites_are_unmet
t_run test_main_entry_paths_direct_and_piped
t_run test_main_entry_path_installs_the_trap_and_sourcing_does_not
t_run test_release_lock_only_removes_a_lock_this_run_took
t_run test_claude_path_follows_the_host_arch
t_run test_claude_auth_status_seam_returns_the_exit_code_and_discards_output
t_run test_run_open_and_is_interactive_seams

t_summary
