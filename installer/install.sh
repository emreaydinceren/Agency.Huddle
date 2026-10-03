#!/bin/sh
# Agency Huddle installer for macOS. Runs as `curl -fsSL <url>/install.sh | sh`, so this is
# POSIX sh only (macOS /bin/sh is bash 3.2, Ubuntu's is dash): no [[ ]], arrays, here-strings,
# process substitution or `echo -e`.
#
# Exit codes: 0 success / up to date / check passed, 1 a step failed, 2 prerequisites unmet,
# 3 bad usage.
set -eu

HUDDLE_REPO="${HUDDLE_REPO:-__HUDDLE_REPO__}"
# The release step replaces the literal placeholder everywhere in this file, so the comparison
# value is built from two pieces and survives the replacement.
HUDDLE_REPO_PLACEHOLDER="__HUDDLE_""REPO__"

# Prints a usage error on stderr and exits 3.
usage_error() {
    printf 'install.sh: %s\n' "$1" >&2
    printf 'usage: install.sh [--version TAG] [--base-url URL|DIR] [--install-root DIR] [--data-root DIR]\n' >&2
    printf '                  [--check-only] [--uninstall [--purge-data]] [--force] [--no-launch]\n' >&2
    exit 3
}

# Exits 3 unless the flag in $1 has a value in $2.
need_value() {
    [ "$#" -ge 2 ] || usage_error "$1 needs a value"
}

# Prints 1 when $1 is a non-empty value other than 0 (how HUDDLE_* switches are read), else 0.
env_switch() {
    case ${1:-} in
        '' | 0) printf '0' ;;
        *) printf '1' ;;
    esac
}

# Parses the Spec section 7 flags into OPT_* globals. Each option starts from its HUDDLE_*
# environment variable (the one-liner cannot pass flags) and an explicit flag wins over it.
# Starts from defaults on every call.
parse_args() {
    OPT_VERSION=${HUDDLE_VERSION:-latest}
    OPT_BASE_URL=${HUDDLE_BASE_URL:-}
    OPT_INSTALL_ROOT=${HUDDLE_INSTALL_ROOT:-}
    OPT_DATA_ROOT=${HUDDLE_DATA_ROOT:-}
    OPT_CHECK_ONLY=$(env_switch "${HUDDLE_CHECK_ONLY:-}")
    OPT_UNINSTALL=$(env_switch "${HUDDLE_UNINSTALL:-}")
    OPT_PURGE_DATA=$(env_switch "${HUDDLE_PURGE_DATA:-}")
    OPT_FORCE=$(env_switch "${HUDDLE_FORCE:-}")
    OPT_NO_LAUNCH=$(env_switch "${HUDDLE_NO_LAUNCH:-}")
    while [ "$#" -gt 0 ]; do
        case $1 in
            --version) need_value "$@"; OPT_VERSION=$2; shift 2 ;;
            --base-url) need_value "$@"; OPT_BASE_URL=$2; shift 2 ;;
            --install-root) need_value "$@"; OPT_INSTALL_ROOT=$2; shift 2 ;;
            --data-root) need_value "$@"; OPT_DATA_ROOT=$2; shift 2 ;;
            --check-only) OPT_CHECK_ONLY=1; shift ;;
            --uninstall) OPT_UNINSTALL=1; shift ;;
            --purge-data) OPT_PURGE_DATA=1; shift ;;
            --force) OPT_FORCE=1; shift ;;
            --no-launch) OPT_NO_LAUNCH=1; shift ;;
            *) usage_error "unknown option: $1" ;;
        esac
    done
}

# Derives every install path from HOME and the OPT_* overrides (Spec section 3). Creates nothing.
resolve_paths() {
    DATA_ROOT="${OPT_DATA_ROOT:-$HOME/Library/Application Support/Agency.Huddle}"
    INSTALL_ROOT="${OPT_INSTALL_ROOT:-$HOME/Library/Application Support/Agency.Huddle/install}"
    APP_EXE="$INSTALL_ROOT/app/Huddle.App"
    NODE_BIN="$INSTALL_ROOT/node/bin/node"
    ADAPTER_ENTRY="$INSTALL_ROOT/tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js"
    STATE_FILE="$INSTALL_ROOT/install-state"
    CACHE_DIR="$DATA_ROOT/cache"
}

# Decides where the asset named $1 comes from. Sets ASSET_KIND (file or url) and ASSET_SRC.
resolve_asset() {
    if [ -n "$OPT_BASE_URL" ]; then
        base=${OPT_BASE_URL%/}
        if [ -d "$OPT_BASE_URL" ]; then
            ASSET_KIND=file
        else
            case $OPT_BASE_URL in
                https://*) ASSET_KIND=url ;;
                *) usage_error "--base-url must be an https URL or an existing folder: $OPT_BASE_URL" ;;
            esac
        fi
        ASSET_SRC="$base/$1"
        return 0
    fi
    ASSET_KIND=url
    if [ "$OPT_VERSION" = "latest" ]; then
        ASSET_SRC="https://github.com/$HUDDLE_REPO/releases/latest/download/$1"
    else
        ASSET_SRC="https://github.com/$HUDDLE_REPO/releases/download/$OPT_VERSION/$1"
    fi
}

# Prints one step line: log_step <ok|skip|work|fail> <name> [detail] [fix].
# A failing step with a fix adds the indented "fix:" line.
log_step() {
    case $1 in
        ok) tag='[ok]' ;;
        skip) tag='[skip]' ;;
        work) tag='[..]' ;;
        fail) tag='[!!]' ;;
        *) tag="[$1]" ;;
    esac
    if [ -n "${3:-}" ]; then
        printf '%s %s - %s\n' "$tag" "$2" "$3"
    else
        printf '%s %s\n' "$tag" "$2"
    fi
    if [ "$1" = "fail" ] && [ -n "${4:-}" ]; then
        printf '     fix: %s\n' "$4"
    fi
}

# ---- Prerequisite checks (Spec section 5) ---------------------------------------------------
# Read-only: nothing here creates a directory or file.

# Seam: major version of macOS.
macos_major() {
    sw_vers -productVersion | cut -d. -f1
}

# Seam: machine hardware name as `uname -m` reports it (arm64 or x86_64 on supported Macs).
host_arch() {
    uname -m
}

# Seam: free megabytes on the volume holding the directory in $1 (default HOME).
free_disk_mb() {
    df -Pk "${1:-$HOME}" | awk 'NR == 2 { print int($4 / 1024) }'
}

# Seam: succeeds when the URL in $1 answers over HTTPS within 10 seconds.
http_probe() {
    curl -fsSI --max-time 10 "$1" >/dev/null 2>&1
}

# Hosts the installer downloads from; probed unless --base-url is a folder.
PREREQ_HOSTS="github.com nodejs.org registry.npmjs.org"
MIN_MACOS_MAJOR=15
MIN_FREE_MB=2048
NL='
'

# Appends one result: prereq_add <ok|skip|fail> <name> <detail> [fix]. Counts failures.
prereq_add() {
    if [ -n "$PREREQ_RESULTS" ]; then
        PREREQ_RESULTS="$PREREQ_RESULTS$NL"
    fi
    PREREQ_RESULTS="$PREREQ_RESULTS$1|$2|$3|${4:-}"
    if [ "$1" = "fail" ]; then
        PREREQ_FAILED=$((PREREQ_FAILED + 1))
    fi
}

# Prints the closest ancestor of $1 (or $1 itself) that exists, without creating anything.
nearest_existing() {
    near=$1
    while [ ! -e "$near" ]; do
        case $near in
            */*) near=${near%/*} ;;
            *) near=. ;;
        esac
        [ -n "$near" ] || near=/
    done
    printf '%s' "$near"
}

prereq_check_macos() {
    major=$(macos_major 2>/dev/null || true)
    case $major in
        '' | *[!0-9]*)
            prereq_add fail "macOS version" "could not read the macOS version" \
                "Run this installer on macOS $MIN_MACOS_MAJOR or newer."
            ;;
        *)
            if [ "$major" -ge "$MIN_MACOS_MAJOR" ]; then
                prereq_add ok "macOS version" "macOS $major"
            else
                prereq_add fail "macOS version" "macOS $major found, $MIN_MACOS_MAJOR or newer is needed" \
                    "Update macOS in System Settings > General > Software Update."
            fi
            ;;
    esac
}

# Sets HOST_RID to osx-arm64 or osx-x64. A Rosetta shell on Apple Silicon reports x86_64, so
# sysctl decides.
prereq_check_cpu() {
    arch=$(host_arch 2>/dev/null || true)
    case $arch in
        arm64)
            HOST_RID=osx-arm64
            prereq_add ok "CPU" "arm64 (Apple Silicon)"
            ;;
        x86_64)
            translated=""
            if command -v sysctl >/dev/null 2>&1; then
                translated=$(sysctl -n hw.optional.arm64 2>/dev/null || true)
            fi
            if [ "$translated" = "1" ]; then
                HOST_RID=osx-arm64
                prereq_add ok "CPU" "x86_64 shell on Apple Silicon (Rosetta), using osx-arm64"
            else
                HOST_RID=osx-x64
                prereq_add ok "CPU" "x86_64 (Intel)"
            fi
            ;;
        *)
            prereq_add fail "CPU" "unsupported processor '$arch'" \
                "Agency Huddle supports Apple Silicon (arm64) and Intel (x86_64) Macs."
            ;;
    esac
}

prereq_check_tools() {
    for tool in curl tar; do
        if command -v "$tool" >/dev/null 2>&1; then
            prereq_add ok "$tool" "found"
        else
            prereq_add fail "$tool" "not found on PATH" "Install the Xcode command line tools: xcode-select --install"
        fi
    done
    if command -v sha256sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1; then
        prereq_add ok "hash tool" "sha256sum or shasum found"
    else
        prereq_add fail "hash tool" "neither sha256sum nor shasum found on PATH" \
            "Install the Xcode command line tools: xcode-select --install"
    fi
    for tool in codesign xattr; do
        if command -v "$tool" >/dev/null 2>&1; then
            prereq_add ok "$tool" "found"
        else
            prereq_add fail "$tool" "not found on PATH" "Install the Xcode command line tools: xcode-select --install"
        fi
    done
}

# Free space is measured on the volumes that will hold the install and the download cache.
prereq_check_disk() {
    free_install=$(free_disk_mb "$(nearest_existing "$INSTALL_ROOT")" 2>/dev/null || true)
    free_cache=$(free_disk_mb "$(nearest_existing "$CACHE_DIR")" 2>/dev/null || true)
    case "$free_install" in
        '' | *[!0-9]*) free_install="" ;;
    esac
    case "$free_cache" in
        '' | *[!0-9]*) free_cache="" ;;
    esac
    if [ -z "$free_install" ] || [ -z "$free_cache" ]; then
        prereq_add fail "disk space" "could not read the free disk space" \
            "Check that the volume holding $HOME is mounted and readable."
        return 0
    fi
    free_min=$free_install
    if [ "$free_cache" -lt "$free_min" ]; then
        free_min=$free_cache
    fi
    if [ "$free_min" -ge "$MIN_FREE_MB" ]; then
        prereq_add ok "disk space" "$free_min MB free"
    else
        prereq_add fail "disk space" "$free_min MB free, $MIN_FREE_MB MB needed" \
            "Free up disk space, or use --install-root and --data-root on a larger volume."
    fi
}

prereq_check_write_access() {
    blocked=""
    for target in "$INSTALL_ROOT" "$DATA_ROOT"; do
        near=$(nearest_existing "$target")
        if [ ! -d "$near" ] || [ ! -w "$near" ]; then
            blocked="$blocked $target"
        fi
    done
    if [ -z "$blocked" ]; then
        prereq_add ok "write access" "install and data folders can be created"
    else
        prereq_add fail "write access" "cannot create:$blocked" \
            "Choose folders you own with --install-root and --data-root."
    fi
}

prereq_check_network() {
    if [ -n "$OPT_BASE_URL" ] && [ -d "$OPT_BASE_URL" ]; then
        prereq_add skip "network" "using the local folder given by --base-url"
        return 0
    fi
    for host in $PREREQ_HOSTS; do
        if http_probe "https://$host"; then
            prereq_add ok "network $host" "reachable"
        else
            prereq_add fail "network $host" "cannot reach https://$host" \
                "Check your internet connection, proxy and firewall settings."
        fi
    done
}

# Runs every check, in Spec section 5 order, and collects all results (never stops at the first
# failure). Sets PREREQ_RESULTS (one "status|name|detail|fix" record per line), PREREQ_FAILED and
# HOST_RID. Needs parse_args and resolve_paths to have run.
check_prerequisites() {
    PREREQ_RESULTS=""
    PREREQ_FAILED=0
    HOST_RID=""
    prereq_check_macos
    prereq_check_cpu
    prereq_check_tools
    prereq_check_disk
    prereq_check_write_access
    prereq_check_network
}

# Prints the results collected by check_prerequisites, one log_step line each.
print_prerequisites() {
    [ -n "$PREREQ_RESULTS" ] || return 0
    while IFS='|' read -r status name detail fix; do
        log_step "$status" "$name" "$detail" "$fix"
    done <<EOF
$PREREQ_RESULTS
EOF
}

# ---- Verified downloads (Spec sections 2 and 6) ----------------------------------------------
# Library functions here never exit; they print a message on stderr and return 1.

# Seam: downloads the URL in $1 to the file in $2. No retry of its own (get_verified_asset owns
# the retry policy). stdin is closed so nothing can ever read the piped script.
http_get() {
    curl -fsSL --retry 0 -o "$2" "$1" </dev/null
}

# Seam: waits $1 seconds (a seam so tests do not wait).
sleep_seconds() {
    sleep "$1" </dev/null
}

# Prints the lower-case SHA-256 of the file in $1 (sha256sum, else shasum -a 256). Returns 1 with
# a message when the file cannot be read or no hash tool exists.
file_sha256() {
    local hash_out
    if [ ! -f "$1" ]; then
        printf 'install.sh: cannot read %s to hash it\n' "$1" >&2
        return 1
    fi
    if command -v sha256sum >/dev/null 2>&1; then
        hash_out=$(sha256sum <"$1") || hash_out=""
    elif command -v shasum >/dev/null 2>&1; then
        hash_out=$(shasum -a 256 <"$1") || hash_out=""
    else
        printf 'install.sh: no hash tool found (sha256sum or shasum is required)\n' >&2
        return 1
    fi
    hash_out=${hash_out%% *}
    if [ -z "$hash_out" ]; then
        printf 'install.sh: could not compute the SHA-256 of %s\n' "$1" >&2
        return 1
    fi
    printf '%s\n' "$hash_out"
}

# Loads the SHA256SUMS file in $1 into SUMS (lines of "<lower-case hash> <name>"). Accepts CRLF, a
# missing final newline and the "*name" binary marker; lines that are not "<64 hex> <name>" are
# ignored. Returns 1 with a message when the file does not exist. SUMS is cleared first.
parse_sums() {
    local line sum_hash sum_name cr
    SUMS=""
    if [ ! -f "$1" ]; then
        printf 'install.sh: SHA256SUMS not found at %s\n' "$1" >&2
        return 1
    fi
    cr=$(printf '\r')
    while IFS= read -r line || [ -n "$line" ]; do
        line=${line%"$cr"}
        while [ "${line# }" != "$line" ]; do
            line=${line# }
        done
        sum_hash=${line%% *}
        sum_name=${line#"$sum_hash"}
        while [ "${sum_name# }" != "$sum_name" ]; do
            sum_name=${sum_name# }
        done
        sum_name=${sum_name#\*}
        while [ "${sum_name% }" != "$sum_name" ]; do
            sum_name=${sum_name% }
        done
        [ "${#sum_hash}" -eq 64 ] || continue
        case $sum_hash in
            *[!0-9a-fA-F]*) continue ;;
        esac
        [ -n "$sum_name" ] || continue
        sum_hash=$(printf '%s' "$sum_hash" | tr 'A-F' 'a-f')
        SUMS="$SUMS$sum_hash $sum_name$NL"
    done <"$1"
}

# Prints the hash that SUMS (see parse_sums) lists for the file name in $1. Returns 1 with a
# message when the name is not listed. The last line wins if a name appears twice.
expected_hash() {
    local found line_hash line_name
    found=""
    while IFS=' ' read -r line_hash line_name; do
        [ -n "$line_hash" ] || continue
        if [ "$line_name" = "$1" ]; then
            found=$line_hash
        fi
    done <<EOF
${SUMS:-}
EOF
    if [ -z "$found" ]; then
        printf "install.sh: '%s' is not listed in SHA256SUMS\n" "$1" >&2
        return 1
    fi
    printf '%s\n' "$found"
}

# get_verified_asset NAME SHA SOURCE CACHE
# Puts a SHA-256-verified copy of NAME in the CACHE folder and prints its path. SOURCE is
# "file:<path>" (copied once) or "url:<https url>" (3 attempts, 1 s then 3 s apart); that is
# "$ASSET_KIND:$ASSET_SRC" from resolve_asset. A cached copy with the right hash is reused, one
# with the wrong hash is replaced. Downloads land in NAME.partial and are renamed only after the
# hash matches; a bad file is deleted. Returns 1 with a message (both hashes on a mismatch).
get_verified_asset() {
    local name expected source cache kind location final partial max attempt actual last backoff
    name=$1
    expected=$(printf '%s' "$2" | tr 'A-F' 'a-f')
    source=$3
    cache=$4
    case $source in
        file:*) kind=file; location=${source#file:} ;;
        url:*) kind=url; location=${source#url:} ;;
        *)
            printf "install.sh: unknown source '%s' for %s (expected file:<path> or url:<url>)\n" "$source" "$name" >&2
            return 1
            ;;
    esac
    if ! mkdir -p "$cache"; then
        printf 'install.sh: cannot create the download cache %s\n' "$cache" >&2
        return 1
    fi
    final="$cache/$name"
    partial="$final.partial"
    if [ -f "$final" ]; then
        actual=$(file_sha256 "$final" 2>/dev/null) || actual=""
        if [ "$actual" = "$expected" ]; then
            printf '%s\n' "$final"
            return 0
        fi
        rm -f "$final"
    fi
    max=3
    if [ "$kind" = file ]; then
        max=1
    fi
    attempt=1
    last=""
    while [ "$attempt" -le "$max" ]; do
        rm -f "$partial"
        last=""
        if [ "$kind" = file ]; then
            if ! cp "$location" "$partial" 2>/dev/null; then
                last="cannot copy $location"
            fi
        elif ! http_get "$location" "$partial"; then
            last="download from $location failed"
        fi
        if [ -z "$last" ]; then
            if actual=$(file_sha256 "$partial"); then
                if [ "$actual" = "$expected" ]; then
                    if mv -f "$partial" "$final"; then
                        printf '%s\n' "$final"
                        return 0
                    fi
                    last="cannot move $partial to $final"
                else
                    last="hash mismatch: expected $expected but got $actual"
                fi
            else
                last="cannot hash the downloaded file"
            fi
        fi
        rm -f "$partial"
        if [ "$attempt" -lt "$max" ]; then
            backoff=3
            if [ "$attempt" -eq 1 ]; then
                backoff=1
            fi
            sleep_seconds "$backoff"
        fi
        attempt=$((attempt + 1))
    done
    printf 'install.sh: could not get a verified %s after %s attempt(s): %s\n' "$name" "$max" "$last" >&2
    return 1
}

read_state_key() {
    local file key value
    file=$1
    key=$2
    if [ ! -f "$file" ]; then
        return 1
    fi
    value=$(grep "^${key}=" "$file" | head -1 | sed "s/^${key}=//" 2>/dev/null)
    if [ -z "$value" ]; then
        return 1
    fi
    printf '%s' "$value"
    return 0
}

write_state_key() {
    local file key value tmpfile
    file=$1
    key=$2
    value=$3
    tmpfile="${file}.tmp.$$"
    if [ -f "$file" ]; then
        grep -v "^${key}=" "$file" >"$tmpfile" 2>/dev/null || true
    fi
    printf '%s=%s\n' "$key" "$value" >>"$tmpfile"
    mv "$tmpfile" "$file"
}

step_satisfied() {
    local file key expected value
    file=$1
    key=$2
    expected=$3
    value=$(read_state_key "$file" "$key") || return 1
    if [ "$value" = "$expected" ]; then
        return 0
    fi
    return 1
}

# ---- App install (Spec section 6, rules 3 and 4) -------------------------------------------------

# Set by install_app: 1 when the app on disk was replaced, 0 when the step was skipped or failed.
APP_CHANGED=0

# Seam: extracts the .tar.gz in $1 into the existing folder in $2.
run_tar() {
    tar -xzf "$1" -C "$2" </dev/null
}

# Seam (macOS only): runs codesign with the given arguments.
run_codesign() {
    codesign "$@" </dev/null
}

# Seam (macOS only): runs xattr with the given arguments.
run_xattr() {
    xattr "$@" </dev/null
}

# Seam (macOS only): succeeds when the file in $1 is a Mach-O binary according to `file -b`.
is_macho() {
    case $(file -b "$1" 2>/dev/null) in
        *Mach-O*) return 0 ;;
    esac
    return 1
}

# Seam: succeeds while the process $1 exists.
process_running() {
    kill -0 "$1" 2>/dev/null
}

# Seam: kills the process $1 and waits up to 10 s for it to go. Returns 1 if it is still there.
run_kill_process() {
    local pid waited
    pid=$1
    waited=0
    kill -KILL "$pid" 2>/dev/null || true
    while process_running "$pid"; do
        if [ "$waited" -ge 10 ]; then
            return 1
        fi
        sleep_seconds 1
        waited=$((waited + 1))
    done
    return 0
}

# Seam: asks the installed app to stop (the app's own `stop` verb waits up to 45 s for the process
# to exit; macOS has no `timeout`, so there is no second cap here). Prefers the launcher, which
# sets the data folder itself; without one the host runs with Team__DataDir exported.
# Returns the exit code of the stop request.
run_huddle_stop() {
    if [ -x "$INSTALL_ROOT/bin/huddle" ]; then
        "$INSTALL_ROOT/bin/huddle" stop </dev/null
    else
        Team__DataDir="$DATA_ROOT/data" "$APP_EXE" stop </dev/null
    fi
}

# Stops a running instance (an instance.json record exists in <DataRoot>/data). Returns 0 when
# nothing was running or the instance stopped, 1 when it is still running (or the stop request
# failed) and --force was not given. With --force a surviving process is killed.
stop_huddle_if_running() {
    local record pid rc
    record="$DATA_ROOT/data/instance.json"
    if [ ! -f "$record" ]; then
        return 0
    fi
    pid=$(sed -n 's/.*"[Pp][Ii][Dd]" *: *\([0-9][0-9]*\).*/\1/p' "$record" 2>/dev/null | head -1) || pid=""
    pid=${pid:-0}
    log_step work stop "stopping the running Agency Huddle"
    rc=0
    run_huddle_stop || rc=$?
    if [ "$pid" -gt 0 ]; then
        if process_running "$pid"; then
            if [ "${OPT_FORCE:-0}" -ne 1 ]; then
                log_step fail stop "Agency Huddle (pid $pid) is still running after the stop request" "Close it and run the installer again, or pass --force to end the process."
                return 1
            fi
            log_step work stop "ending process $pid (--force)"
            if ! run_kill_process "$pid"; then
                log_step fail stop "process $pid did not end" "End it by hand and run the installer again."
                return 1
            fi
            rm -f "$record"
        fi
    elif [ "$rc" -ne 0 ]; then
        if [ "${OPT_FORCE:-0}" -ne 1 ]; then
            log_step fail stop "the stop request for Agency Huddle failed (exit code $rc)" "Close it and run the installer again, or pass --force."
            return 1
        fi
        log_step work stop "stop request exited with code $rc; continuing because of --force"
    fi
    log_step ok stop stopped
    return 0
}

# Gives every Mach-O file under the folder $1 a signature the kernel accepts: a file whose
# `codesign --verify` fails is signed ad hoc. Arm64 macOS refuses to load unsigned native code.
# Returns 1 when a file cannot be signed.
ensure_signed() {
    local dir file_path
    dir=$1
    find "$dir" -type f | while IFS= read -r file_path; do
        if is_macho "$file_path"; then
            if ! run_codesign --verify "$file_path" >/dev/null 2>&1; then
                if ! run_codesign --force --sign - "$file_path" >/dev/null; then
                    printf 'install.sh: could not sign %s\n' "$file_path" >&2
                    return 1
                fi
            fi
        fi
    done
}

# Undoes a failed swap: puts the old install back, moves the carried items home, removes the
# staging folder. install_app_restore <staging> <old> <carried items> <1 when the old folder was
# renamed>. Returns 1 (with a message) when the rollback itself failed.
install_app_restore() {
    local staging old carried old_moved item rc
    staging=$1
    old=$2
    carried=$3
    old_moved=$4
    rc=0
    if [ "$old_moved" = 1 ] && [ ! -e "$INSTALL_ROOT" ]; then
        mv "$old" "$INSTALL_ROOT" || rc=1
    fi
    for item in $carried; do
        mkdir -p "$(dirname "$INSTALL_ROOT/$item")" || rc=1
        mv "$staging/$item" "$INSTALL_ROOT/$item" || rc=1
    done
    if [ "$rc" -ne 0 ]; then
        log_step fail app "rollback failed" "Restore $INSTALL_ROOT from $old by hand."
        return 1
    fi
    rm -rf "$staging"
    return 0
}

# Installs the app from the verified archive in $1 (Spec section 6, rule 3). Sets APP_CHANGED to 1
# when the install was replaced and 0 when the step was skipped (the state holds this archive's
# hash and the host exists) or failed. Returns 1 on a failure, after logging it; the previous
# install is left runnable. Does not write the install state (the caller does, last).
#
# The archive is extracted next to InstallRoot (<InstallRoot>.staging), checked, made executable
# and signed there. Only then is a running instance stopped and the swap done: InstallRoot becomes
# <InstallRoot>.old, staging becomes InstallRoot, .old is deleted. node/ and tools/acp/node_modules
# move across, install-state and bin/ are copied across.
install_app() {
    local archive sha root staging old parent item carried old_moved
    archive=$1
    APP_CHANGED=0
    sha=$(file_sha256 "$archive") || return 1
    if [ -f "$APP_EXE" ] && step_satisfied "$STATE_FILE" app_sha256 "$sha"; then
        log_step skip app "already installed"
        return 0
    fi
    root=${INSTALL_ROOT%/}
    staging="$root.staging"
    old="$root.old"
    parent=$(dirname "$root")
    # Leftovers of an interrupted run: a swap that died between its two renames, then stale folders.
    if [ ! -e "$root" ] && [ -e "$old" ]; then
        if ! mv "$old" "$root"; then
            log_step fail app "cannot restore $old" "Rename $old to $root by hand and run the installer again."
            return 1
        fi
    fi
    rm -rf "$staging" "$old"
    log_step work app "extracting the release archive"
    if ! mkdir -p "$parent" "$staging"; then
        log_step fail app "cannot create $staging" "Check the permissions on $parent."
        return 1
    fi
    if ! run_tar "$archive" "$staging"; then
        rm -rf "$staging"
        log_step fail app "cannot extract $archive" "Delete the file and run the installer again."
        return 1
    fi
    if [ ! -f "$staging/app/Huddle.App" ] || [ ! -f "$staging/release-info.json" ]; then
        rm -rf "$staging"
        log_step fail app "the release archive does not contain app/Huddle.App and release-info.json; the previous install was left untouched" "Download the release again."
        return 1
    fi
    if ! chmod +x "$staging/app/Huddle.App" || ! ensure_signed "$staging/app"; then
        rm -rf "$staging"
        log_step fail app "cannot make the app runnable; the previous install was left untouched" "Run the installer again."
        return 1
    fi
    carried=""
    old_moved=0
    if [ -e "$root" ]; then
        # macOS cannot replace a folder whose files a running process holds open.
        if ! stop_huddle_if_running; then
            rm -rf "$staging"
            return 1
        fi
        for item in node tools/acp/node_modules; do
            if [ -e "$root/$item" ]; then
                if mkdir -p "$(dirname "$staging/$item")" && rm -rf "$staging/$item" && mv "$root/$item" "$staging/$item"; then
                    carried="$carried $item"
                else
                    install_app_restore "$staging" "$old" "$carried" 0 || true
                    log_step fail app "cannot carry $item over; the previous install was left untouched" "Run the installer again."
                    return 1
                fi
            fi
        done
        # Small items the other steps read next are copied, so the old tree stays whole for a rollback.
        for item in install-state bin; do
            if [ -e "$root/$item" ] && [ ! -e "$staging/$item" ]; then
                if ! cp -R -p "$root/$item" "$staging/$item"; then
                    install_app_restore "$staging" "$old" "$carried" 0 || true
                    log_step fail app "cannot copy $item over; the previous install was left untouched" "Run the installer again."
                    return 1
                fi
            fi
        done
        if ! mv "$root" "$old"; then
            install_app_restore "$staging" "$old" "$carried" 0 || true
            log_step fail app "cannot rename $root; the previous install was left untouched" "Close anything using it and run the installer again."
            return 1
        fi
        old_moved=1
    fi
    if ! mv "$staging" "$root"; then
        install_app_restore "$staging" "$old" "$carried" "$old_moved" || true
        log_step fail app "cannot move the new app into place; the previous install was restored" "Run the installer again."
        return 1
    fi
    rm -rf "$old"
    # xattr exits non-zero when there is no quarantine flag to clear; either way the app is installed.
    run_xattr -dr com.apple.quarantine "$root" || true
    APP_CHANGED=1
    log_step ok app "installed"
    return 0
}

main() {
    parse_args "$@"
    if [ -z "$OPT_BASE_URL" ] && [ "$HUDDLE_REPO" = "$HUDDLE_REPO_PLACEHOLDER" ]; then
        printf 'install.sh: this copy does not know which repository to download from.\n' >&2
        printf 'Run the install.sh attached to a release, or pass --base-url <folder|https url>.\n' >&2
        exit 3
    fi
    resolve_paths
    if [ "$OPT_CHECK_ONLY" = 1 ]; then
        check_prerequisites
        print_prerequisites
        if [ "$PREREQ_FAILED" -ne 0 ]; then
            return 2
        fi
        printf 'All prerequisites are met.\n'
        return 0
    fi
    return 0
}

[ -n "${HUDDLE_INSTALL_NO_MAIN:-}" ] || main "$@"
