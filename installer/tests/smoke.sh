#!/bin/sh
# End-to-end smoke test of the macOS installer against a real release (plan task 40; the twin of
# smoke.ps1). Usage:
#
#   sh installer/tests/smoke.sh <release-dir> [KEEP_SANDBOX=1 in the environment keeps the sandbox]
#
# <release-dir> holds huddle-osx-arm64.tar.gz / huddle-osx-x64.tar.gz and SHA256SUMS. It is built
# with installer/build-release.sh when the archive for this Mac is missing (dotnet publish, a few
# minutes). The release is installed into a temp sandbox: HOME points into it, so the real
# ~/Applications and ~/.local/bin are never touched; --install-root and --data-root are under it too.
# The script walks Spec sections 6 and 8: install, check, idempotent rerun, start, second start,
# rerun while running, upgrade over the running app, stop, uninstall twice. It prints "STEP n: ..."
# lines (same numbering as smoke.ps1), exits 1 naming the failing step, and always ends every
# process whose command line contains the sandbox path.
#
# On anything but macOS it prints "SKIP: macOS only" and exits 0.
#
# POSIX sh only (macOS /bin/sh is bash 3.2, Ubuntu's is dash): no timeout(1), no readlink -f, no
# sed -i, no date +%s%N; waiting is sleep 1 in a counted loop.
set -eu

if [ "$(uname -s)" != Darwin ]; then
    echo "SKIP: macOS only"
    exit 0
fi

if [ "$#" -lt 1 ] || [ -z "$1" ]; then
    echo "usage: sh installer/tests/smoke.sh <release-dir>" >&2
    exit 2
fi

unset CDPATH
# A HUDDLE_* variable of the caller must not steer the installer under test.
unset HUDDLE_VERSION HUDDLE_BASE_URL HUDDLE_INSTALL_ROOT HUDDLE_DATA_ROOT HUDDLE_CHECK_ONLY \
    HUDDLE_UNINSTALL HUDDLE_PURGE_DATA HUDDLE_FORCE HUDDLE_NO_LAUNCH HUDDLE_INSTALL_NO_MAIN

HERE=$(cd "$(dirname "$0")" && pwd -P)
REPO_ROOT=$(cd "$HERE/../.." && pwd -P)
INSTALL_SH="$REPO_ROOT/installer/install.sh"
BUILD_SH="$REPO_ROOT/installer/build-release.sh"
SUMS_SH="$REPO_ROOT/installer/make-sums.sh"

STEP=0
DIED=0
SANDBOX=""
LOG_DIR=""
LOG_N=0
INSTALL_ROOT=""
DATA_ROOT=""
LAUNCHER=""
INSTANCE_FILE=""
INSTALL_LOG=""
INSTALL_RC=0
RUN_RC=0
INST_URL=""
INST_PID=""
LAUNCH_PID=""
VERSION1="0.0.1-smoke"
VERSION2="0.0.2-smoke"
REAL_HOME=${HOME:-}
REAL_BUNDLE_BEFORE=0
REAL_LINK_BEFORE=0

# ---- small helpers --------------------------------------------------------------------------

info() {
    printf '    %s\n' "$*"
}

begin_step() {
    STEP=$1
    shift
    printf 'STEP %s: %s\n' "$STEP" "$*"
}

# Prints the last lines of the logs that explain a failure.
failure_context() {
    local file
    for file in "$LOG_DIR"/app-*.log "$DATA_ROOT/launcher.log"; do
        if [ -f "$file" ]; then
            printf '  last lines of %s:\n' "$file"
            tail -n 15 "$file" | sed 's/^/    /'
        fi
    done
    if [ -n "$INSTALL_LOG" ] && [ -f "$INSTALL_LOG" ]; then
        printf '  last lines of the last installer run:\n'
        tail -n 12 "$INSTALL_LOG" | sed 's/^/    /'
    fi
}

die() {
    DIED=1
    printf 'FAIL at STEP %s: %s\n' "$STEP" "$*"
    failure_context
    exit 1
}

# Prints the last $2 lines of file $1 on one line.
last_lines() {
    tail -n "$2" "$1" | tr '\n' '|'
}

# True when process $1 exists and is not a zombie.
pid_alive() {
    local state
    state=$(ps -o stat= -p "$1" 2>/dev/null || true)
    case $state in
        '' | Z*) return 1 ;;
        *) return 0 ;;
    esac
}

# Prints "<pid> <command line>" for every process whose command line contains the sandbox path
# (and, when $1 is given, that text too). The values travel through the environment of awk so the
# awk and ps processes themselves never match.
list_procs() {
    [ -n "$SANDBOX" ] || return 0
    ps -axww -o pid=,command= | SMOKE_NEEDLE="$SANDBOX/" SMOKE_FRAGMENT="${1:-}" SMOKE_SELF="$$" awk '
        BEGIN { needle = ENVIRON["SMOKE_NEEDLE"]; fragment = ENVIRON["SMOKE_FRAGMENT"]; self = ENVIRON["SMOKE_SELF"] }
        {
            if ($1 == self) { next }
            line = $0
            sub(/^ *[0-9]+ +/, "", line)
            if (index(line, needle) == 0) { next }
            if (fragment != "" && index(line, fragment) == 0) { next }
            print $1 " " line
        }'
}

count_procs() {
    list_procs "${1:-}" | wc -l | tr -d ' '
}

# Ends every process under the sandbox: TERM, two seconds, then KILL.
kill_sandbox_processes() {
    local pids pid
    pids=$(list_procs | awk '{ print $1 }')
    [ -n "$pids" ] || return 0
    for pid in $pids; do
        kill "$pid" 2>/dev/null || true
    done
    sleep 2
    pids=$(list_procs | awk '{ print $1 }')
    for pid in $pids; do
        kill -9 "$pid" 2>/dev/null || true
    done
    return 0
}

# run_timed SECONDS LOGFILE COMMAND [ARGS...]: runs the command with stdin /dev/null and stdout and
# stderr in LOGFILE; sets RUN_RC to its exit code, or 124 after SECONDS (then the sandbox processes
# are ended). No timeout(1) on macOS: the command runs in a background group that writes its exit
# code to a file, which this loop polls once a second.
run_timed() {
    local secs logfile rcfile waited runner
    secs=$1
    logfile=$2
    shift 2
    rcfile="$LOG_DIR/rc.$$"
    rm -f "$rcfile" "$rcfile.tmp"
    {
        child_rc=0
        "$@" >"$logfile" 2>&1 </dev/null || child_rc=$?
        printf '%s\n' "$child_rc" >"$rcfile.tmp"
        mv "$rcfile.tmp" "$rcfile"
    } &
    runner=$!
    waited=0
    while [ ! -f "$rcfile" ]; do
        if [ "$waited" -ge "$secs" ]; then
            RUN_RC=124
            kill_sandbox_processes
            kill "$runner" 2>/dev/null || true
            wait "$runner" 2>/dev/null || true
            return 0
        fi
        sleep 1
        waited=$((waited + 1))
    done
    wait "$runner" 2>/dev/null || true
    RUN_RC=$(cat "$rcfile")
    rm -f "$rcfile"
}

# Prints the "[status] name - detail" rows of an installer log, indented.
show_step_rows() {
    grep -E '^\[(ok|skip|\.\.|!!|warn)\]' "$1" | sed 's/^/    /' || true
}

# run_installer BASE EXTRA-ARGS...: runs install.sh against the folder BASE inside the sandbox.
# Sets INSTALL_LOG and INSTALL_RC and echoes the step rows.
run_installer() {
    local base
    base=$1
    shift
    LOG_N=$((LOG_N + 1))
    INSTALL_LOG="$LOG_DIR/install-$LOG_N.txt"
    run_timed 900 "$INSTALL_LOG" sh "$INSTALL_SH" --base-url "$base" \
        --install-root "$INSTALL_ROOT" --data-root "$DATA_ROOT" "$@"
    INSTALL_RC=$RUN_RC
    show_step_rows "$INSTALL_LOG"
}

# Dies unless every installer step of the log (app, node, adapter, launcher, link) is [skip], the
# four main steps were all reported, and the run ended "Already up to date".
assert_all_skipped() {
    local name bad
    for name in app node adapter launcher; do
        grep -Eq "^\[skip\] $name( |\$)" "$1" || die "expected [skip] $name, the installer did not print it"
    done
    bad=$(grep -E '^\[(ok|\.\.|!!|warn)\] (app|node|adapter|launcher|link|stop)( |$)' "$1" | head -n 1 || true)
    [ -z "$bad" ] || die "expected every step to be [skip], got: $bad"
    grep -q '^Already up to date (' "$1" || die 'the rerun did not end with "Already up to date (...)."'
}

# Writes path|size|mtime of every non-directory under $1 and path|dir for every folder (a folder's
# own mtime is ignored, as in smoke.ps1) to the file $2, sorted.
take_snapshot() {
    {
        find "$1" -type d -print | sed 's/$/|dir/'
        find "$1" ! -type d -exec stat -f '%N|%z|%m' {} +
    } | LC_ALL=C sort >"$2"
}

# Sets INST_URL and INST_PID from instance.json ("" when missing or incomplete).
read_instance() {
    INST_URL=""
    INST_PID=""
    if [ -f "$INSTANCE_FILE" ]; then
        INST_URL=$(sed -n 's/.*"url" *: *"\([^"]*\)".*/\1/p' "$INSTANCE_FILE" | head -n 1)
        INST_PID=$(sed -n 's/.*"pid" *: *\([0-9][0-9]*\).*/\1/p' "$INSTANCE_FILE" | head -n 1)
    fi
}

# Starts bin/huddle in the background (it execs the app, so the pid is the app's). Sets LAUNCH_PID.
start_app() {
    LOG_N=$((LOG_N + 1))
    "$LAUNCHER" >"$LOG_DIR/app-$LOG_N.log" 2>&1 </dev/null &
    LAUNCH_PID=$!
}

# wait_for_app LAUNCHER-PID TIMEOUT: waits for a live instance.json, then GET /health = 200.
wait_for_app() {
    local waited status attempt
    waited=0
    while :; do
        read_instance
        if [ -n "$INST_URL" ] && [ -n "$INST_PID" ] && pid_alive "$INST_PID"; then
            break
        fi
        pid_alive "$1" || die "the launcher exited before instance.json appeared"
        [ "$waited" -lt "$2" ] || die "instance.json did not appear within $2 s"
        sleep 1
        waited=$((waited + 1))
    done
    info "instance: $INST_URL pid $INST_PID"
    status=000
    attempt=0
    while [ "$attempt" -lt 10 ]; do
        status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "${INST_URL%/}/health" </dev/null || true)
        [ "$status" != 200 ] || break
        sleep 1
        attempt=$((attempt + 1))
    done
    [ "$status" = 200 ] || die "GET /health returned $status, expected 200"
    info "GET /health = 200"
}

# Waits up to $2 seconds for process $1 to be gone.
wait_pid_gone() {
    local waited
    waited=0
    while pid_alive "$1"; do
        [ "$waited" -lt "$2" ] || return 1
        sleep 1
        waited=$((waited + 1))
    done
    return 0
}

installed_version() {
    sed -n 's/^version=//p' "$INSTALL_ROOT/install-state" 2>/dev/null | head -n 1
}

# ---- cleanup --------------------------------------------------------------------------------

cleanup() {
    # $? must be read before anything else: `local` itself would reset it to 0.
    status=$?
    trap - EXIT
    set +e
    if [ "$status" -ne 0 ] && [ "$DIED" -eq 0 ]; then
        printf 'FAIL at STEP %s: the script stopped unexpectedly (status %s)\n' "$STEP" "$status"
        failure_context
    fi
    if [ -n "$SANDBOX" ]; then
        if [ -n "$(list_procs)" ]; then
            echo "cleanup: ending the process(es) under the sandbox"
            kill_sandbox_processes
        fi
        # The sandboxed HOME must have kept the real ~/Applications and ~/.local/bin out of this.
        if [ -n "$REAL_HOME" ]; then
            if [ "$REAL_BUNDLE_BEFORE" -eq 0 ] && [ -e "$REAL_HOME/Applications/Agency Huddle.app" ]; then
                status=1
                echo "FAIL: $REAL_HOME/Applications/Agency Huddle.app was created during the smoke test; deleting it"
                rm -rf "$REAL_HOME/Applications/Agency Huddle.app"
            fi
            if [ "$REAL_LINK_BEFORE" -eq 0 ] && { [ -e "$REAL_HOME/.local/bin/huddle" ] || [ -L "$REAL_HOME/.local/bin/huddle" ]; }; then
                status=1
                echo "FAIL: $REAL_HOME/.local/bin/huddle was created during the smoke test; deleting it"
                rm -f "$REAL_HOME/.local/bin/huddle"
            fi
        fi
        if [ -d "$SANDBOX" ]; then
            if [ "${KEEP_SANDBOX:-0}" = 1 ]; then
                echo "Sandbox kept: $SANDBOX"
            else
                case $SANDBOX in
                    */huddle-smoke.*) rm -rf "$SANDBOX" || echo "cleanup: could not delete $SANDBOX" ;;
                esac
            fi
        fi
    fi
    if [ "$status" -eq 0 ]; then
        echo "SMOKE PASSED"
    else
        echo "SMOKE FAILED"
    fi
    exit "$status"
}

trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

# ---- setup ----------------------------------------------------------------------------------

case $(uname -m) in
    arm64) RID=osx-arm64 ;;
    x86_64) RID=osx-x64 ;;
    *)
        echo "FAIL: unsupported CPU $(uname -m)"
        exit 1
        ;;
esac
ARCHIVE_NAME="huddle-$RID.tar.gz"

mkdir -p "$1"
RELEASE_DIR=$(cd "$1" && pwd -P)

if [ -e "$REAL_HOME/Applications/Agency Huddle.app" ]; then
    REAL_BUNDLE_BEFORE=1
fi
if [ -e "$REAL_HOME/.local/bin/huddle" ] || [ -L "$REAL_HOME/.local/bin/huddle" ]; then
    REAL_LINK_BEFORE=1
fi

SANDBOX=$(mktemp -d "${TMPDIR:-/tmp}/huddle-smoke.XXXXXX")
SANDBOX=$(cd "$SANDBOX" && pwd -P)
LOG_DIR="$SANDBOX/log"
mkdir -p "$LOG_DIR" "$SANDBOX/home"
HOME="$SANDBOX/home"
export HOME
# Like the real default layout (Library/Application Support/Agency.Huddle/install): a space in the path.
DATA_ROOT="$SANDBOX/Application Support/Agency.Huddle"
INSTALL_ROOT="$DATA_ROOT/install"
LAUNCHER="$INSTALL_ROOT/bin/huddle"
INSTANCE_FILE="$DATA_ROOT/data/instance.json"
APP_EXE="$INSTALL_ROOT/app/Huddle.App"
APP_BUNDLE="$HOME/Applications/Agency Huddle.app"
LINK_PATH="$HOME/.local/bin/huddle"
Team__Desktop__OpenBrowser=false
export Team__Desktop__OpenBrowser
echo "Sandbox: $SANDBOX"

others=$(ps -axww -o command= | grep -c '/app/Huddle\.App' || true)
if [ "$others" -gt 0 ]; then
    echo "Note: $others other Huddle.App process(es) are running outside the sandbox; they are left alone."
fi

# ---- STEP 0: the release folder -------------------------------------------------------------

begin_step 0 "release folder $RELEASE_DIR"
if [ ! -f "$RELEASE_DIR/$ARCHIVE_NAME" ]; then
    info "building it with installer/build-release.sh (dotnet publish; this takes a few minutes)"
    LOG_N=$((LOG_N + 1))
    run_timed 1500 "$LOG_DIR/build-$LOG_N.txt" sh "$BUILD_SH" --version "$VERSION1" --rid "$RID" --out "$RELEASE_DIR"
    [ "$RUN_RC" -eq 0 ] || die "build-release.sh failed with exit code $RUN_RC: $(last_lines "$LOG_DIR/build-$LOG_N.txt" 8)"
fi
if [ ! -f "$RELEASE_DIR/SHA256SUMS" ]; then
    LOG_N=$((LOG_N + 1))
    run_timed 120 "$LOG_DIR/sums-$LOG_N.txt" sh "$SUMS_SH" "$RELEASE_DIR" "$ARCHIVE_NAME"
    [ "$RUN_RC" -eq 0 ] || die "make-sums.sh failed with exit code $RUN_RC: $(last_lines "$LOG_DIR/sums-$LOG_N.txt" 5)"
fi
[ -f "$RELEASE_DIR/$ARCHIVE_NAME" ] || die "$ARCHIVE_NAME is not in $RELEASE_DIR"
info "release archive: $(du -k "$RELEASE_DIR/$ARCHIVE_NAME" | awk '{ print int($1 / 1024) }') MB"

# ---- STEP 1: install ------------------------------------------------------------------------

begin_step 1 "install.sh into the sandbox (downloads Node and runs npm ci)"
run_installer "$RELEASE_DIR" --no-launch
[ "$INSTALL_RC" -eq 0 ] || die "install.sh exited with $INSTALL_RC; last lines: $(last_lines "$INSTALL_LOG" 6)"
[ -x "$LAUNCHER" ] || die "bin/huddle was not written or is not executable"
[ -x "$APP_EXE" ] || die "app/Huddle.App is missing or not executable"

# Extra macOS checks: the code signatures, the bundle plist, the bundle stub and the symlink.
codesign --verify --deep --strict "$APP_EXE" </dev/null 2>"$LOG_DIR/codesign.txt" ||
    die "codesign --verify --deep --strict failed on the host: $(last_lines "$LOG_DIR/codesign.txt" 3)"
info "codesign: app/Huddle.App verifies"
find "$INSTALL_ROOT/app" -type f -name '*.dylib' >"$LOG_DIR/dylibs.txt"
dylib_count=0
while IFS= read -r dylib; do
    codesign --verify --deep --strict "$dylib" </dev/null 2>"$LOG_DIR/codesign.txt" ||
        die "codesign --verify --deep --strict failed on $dylib: $(last_lines "$LOG_DIR/codesign.txt" 3)"
    dylib_count=$((dylib_count + 1))
done <"$LOG_DIR/dylibs.txt"
info "codesign: $dylib_count dylib(s) verify"
[ -f "$APP_BUNDLE/Contents/Info.plist" ] || die "the .app bundle has no Contents/Info.plist"
plutil -lint "$APP_BUNDLE/Contents/Info.plist" </dev/null >"$LOG_DIR/plutil.txt" 2>&1 ||
    die "plutil -lint failed on the bundle Info.plist: $(last_lines "$LOG_DIR/plutil.txt" 3)"
[ -x "$APP_BUNDLE/Contents/MacOS/AgencyHuddle" ] || die "the .app executable Contents/MacOS/AgencyHuddle is missing or not executable"
[ -L "$LINK_PATH" ] || die "$LINK_PATH is not a symlink"
[ "$(readlink "$LINK_PATH")" = "$LAUNCHER" ] || die "$LINK_PATH points to $(readlink "$LINK_PATH"), expected $LAUNCHER"
info "bundle: plist is valid, executable is executable; symlink points at bin/huddle"

# ---- STEP 2: check only ---------------------------------------------------------------------

begin_step 2 "install.sh --check-only"
run_installer "$RELEASE_DIR" --check-only
[ "$INSTALL_RC" -eq 0 ] || die "--check-only exited with $INSTALL_RC"

# ---- STEP 3: idempotent rerun ---------------------------------------------------------------

begin_step 3 "rerun: every step is [skip] and the install root is byte-for-byte unchanged"
take_snapshot "$INSTALL_ROOT" "$LOG_DIR/snapshot-before.txt"
run_installer "$RELEASE_DIR" --no-launch
[ "$INSTALL_RC" -eq 0 ] || die "the rerun exited with $INSTALL_RC"
assert_all_skipped "$INSTALL_LOG"
take_snapshot "$INSTALL_ROOT" "$LOG_DIR/snapshot-after.txt"
if ! cmp -s "$LOG_DIR/snapshot-before.txt" "$LOG_DIR/snapshot-after.txt"; then
    die "the install root changed on a rerun: $(diff "$LOG_DIR/snapshot-before.txt" "$LOG_DIR/snapshot-after.txt" | head -n 8 | tr '\n' ';')"
fi
info "snapshot identical ($(wc -l <"$LOG_DIR/snapshot-after.txt" | tr -d ' ') entries)"

# ---- STEP 4: start --------------------------------------------------------------------------

begin_step 4 "start bin/huddle in the background; instance.json within 90 s; GET /health = 200"
start_app
launch1_pid=$LAUNCH_PID
wait_for_app "$launch1_pid" 90
instance1_pid=$INST_PID

# ---- STEP 5: second start hands off ---------------------------------------------------------

begin_step 5 "start bin/huddle a second time: exits 0 within 20 s, exactly one Huddle.App"
LOG_N=$((LOG_N + 1))
run_timed 20 "$LOG_DIR/second-launch-$LOG_N.txt" "$LAUNCHER"
[ "$RUN_RC" -ne 124 ] || die "the second launcher did not exit within 20 s"
[ "$RUN_RC" -eq 0 ] || die "the second launcher exited with $RUN_RC"
apps=$(count_procs "$APP_EXE")
[ "$apps" -eq 1 ] || die "expected exactly one Huddle.App process under the sandbox, found $apps"
app_line=$(list_procs "$APP_EXE" | head -n 1)
[ "${app_line%% *}" = "$instance1_pid" ] || die "the Huddle.App process is ${app_line%% *} but instance.json names $instance1_pid"

# ---- STEP 6: rerun while running ------------------------------------------------------------

begin_step 6 "rerun the installer while the app runs: all [skip], same PID alive"
run_installer "$RELEASE_DIR" --no-launch
[ "$INSTALL_RC" -eq 0 ] || die "the rerun exited with $INSTALL_RC"
assert_all_skipped "$INSTALL_LOG"
pid_alive "$instance1_pid" || die "the app (pid $instance1_pid) was stopped by a no-change rerun"
read_instance
[ "$INST_PID" = "$instance1_pid" ] || die "instance.json changed during a no-change rerun"

# ---- STEP 7: upgrade over the running app ---------------------------------------------------

begin_step 7 "install $VERSION2 over the running app: stop, replace, node and adapter [skip], data survives"
marker="$DATA_ROOT/smoke-marker.txt"
printf 'keep me' >"$marker"
mkdir -p "$SANDBOX/x"
tar -xzf "$RELEASE_DIR/$ARCHIVE_NAME" -C "$SANDBOX/x" </dev/null || die "could not extract $ARCHIVE_NAME to reuse its app/ as the second release"
[ -f "$SANDBOX/x/app/Huddle.App" ] || die "the first archive has no app/Huddle.App to reuse as the second release"
release2="$SANDBOX/rel2"
LOG_N=$((LOG_N + 1))
run_timed 900 "$LOG_DIR/build-$LOG_N.txt" sh "$BUILD_SH" --version "$VERSION2" --rid "$RID" --out "$release2" --publish-dir "$SANDBOX/x/app"
[ "$RUN_RC" -eq 0 ] || die "building the second release failed with exit code $RUN_RC: $(last_lines "$LOG_DIR/build-$LOG_N.txt" 8)"
LOG_N=$((LOG_N + 1))
run_timed 120 "$LOG_DIR/sums-$LOG_N.txt" sh "$SUMS_SH" "$release2" "$ARCHIVE_NAME"
[ "$RUN_RC" -eq 0 ] || die "make-sums.sh failed for the second release with exit code $RUN_RC"
run_installer "$release2" --no-launch
[ "$INSTALL_RC" -eq 0 ] || die "the upgrade exited with $INSTALL_RC; last lines: $(last_lines "$INSTALL_LOG" 6)"
grep -q '^\[ok\] app' "$INSTALL_LOG" || die "the upgrade did not report [ok] app"
grep -q '^\[skip\] node' "$INSTALL_LOG" || die "the upgrade did not report [skip] node"
grep -q '^\[skip\] adapter' "$INSTALL_LOG" || die "the upgrade did not report [skip] adapter"
if pid_alive "$instance1_pid"; then
    die "the old app (pid $instance1_pid) is still running after the upgrade"
fi
wait "$launch1_pid" 2>/dev/null || true
[ "$(count_procs "$APP_EXE")" -eq 0 ] || die "a Huddle.App process is still running after the upgrade"
[ "$(installed_version)" = "$VERSION2" ] || die "install-state says version \"$(installed_version)\", expected $VERSION2"
[ -f "$marker" ] && [ "$(cat "$marker")" = "keep me" ] || die "the marker file in the data root did not survive the upgrade"

# ---- STEP 8: start again, then stop ---------------------------------------------------------

begin_step 8 'start again; bin/huddle stop exits 0 and prints "Stopped."; no Huddle.App or node is left'
start_app
launch3_pid=$LAUNCH_PID
wait_for_app "$launch3_pid" 90
instance3_pid=$INST_PID
LOG_N=$((LOG_N + 1))
stop_log="$LOG_DIR/stop-$LOG_N.txt"
run_timed 120 "$stop_log" "$LAUNCHER" stop
sed 's/^/    stop> /' "$stop_log"
[ "$RUN_RC" -eq 0 ] || die "bin/huddle stop exited with $RUN_RC"
grep -qx 'Stopped\.' "$stop_log" || die 'bin/huddle stop did not print "Stopped."'
wait_pid_gone "$instance3_pid" 20 || die "Huddle.App (pid $instance3_pid) is still running after stop"
wait "$launch3_pid" 2>/dev/null || true
[ ! -e "$INSTANCE_FILE" ] || die "instance.json is still there after stop"
waited=0
while [ "$(count_procs)" -gt 0 ] && [ "$waited" -lt 20 ]; do
    sleep 1
    waited=$((waited + 1))
done
left=$(list_procs)
if [ -n "$left" ]; then
    nodes=$(list_procs "/node/bin/node")
    if [ -n "$nodes" ]; then
        die "KNOWN RISK A5 (adapter shutdown): node process(es) under the sandbox survive stop: $(printf '%s' "$nodes" | cut -c1-120 | tr '\n' ';')"
    fi
    die "process(es) under the sandbox survive stop: $(printf '%s' "$left" | cut -c1-120 | tr '\n' ';')"
fi

# ---- STEP 9: uninstall ----------------------------------------------------------------------

begin_step 9 "--uninstall removes the install root, the .app and the symlink, and keeps the data"
run_installer "$RELEASE_DIR" --uninstall
[ "$INSTALL_RC" -eq 0 ] || die "--uninstall exited with $INSTALL_RC"
[ ! -e "$INSTALL_ROOT" ] || die "the install root still exists after --uninstall"
[ ! -e "$APP_BUNDLE" ] || die "the .app bundle still exists after --uninstall"
if [ -e "$LINK_PATH" ] || [ -L "$LINK_PATH" ]; then
    die "the symlink $LINK_PATH still exists after --uninstall"
fi
[ -f "$marker" ] || die "the data root was not kept by --uninstall"

# ---- STEP 10: uninstall again ---------------------------------------------------------------

begin_step 10 "--uninstall again exits 0"
run_installer "$RELEASE_DIR" --uninstall
[ "$INSTALL_RC" -eq 0 ] || die "the second --uninstall exited with $INSTALL_RC"

exit 0
