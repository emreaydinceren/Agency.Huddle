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

# Prints a "[!!] usage" line with its fix on stderr and returns 3. Callers pass the 3 up to main,
# which returns it; only the entry guard at the end of the file exits.
usage_error() {
    log_step fail usage "$1" "usage: install.sh [--version TAG] [--base-url URL|DIR] [--install-root DIR] [--data-root DIR] [--check-only] [--uninstall [--purge-data]] [--force] [--no-launch]" >&2
    return 3
}

# Returns 3 (after the usage message) unless the flag in $1 has a value in $2.
need_value() {
    [ "$#" -ge 2 ] || usage_error "$1 needs a value" || return $?
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
            --version) need_value "$@" || return $?; OPT_VERSION=$2; shift 2 ;;
            --base-url) need_value "$@" || return $?; OPT_BASE_URL=$2; shift 2 ;;
            --install-root) need_value "$@" || return $?; OPT_INSTALL_ROOT=$2; shift 2 ;;
            --data-root) need_value "$@" || return $?; OPT_DATA_ROOT=$2; shift 2 ;;
            --check-only) OPT_CHECK_ONLY=1; shift ;;
            --uninstall) OPT_UNINSTALL=1; shift ;;
            --purge-data) OPT_PURGE_DATA=1; shift ;;
            --force) OPT_FORCE=1; shift ;;
            --no-launch) OPT_NO_LAUNCH=1; shift ;;
            *) usage_error "unknown option: $1" || return $? ;;
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
    APP_BUNDLE="$HOME/Applications/Agency Huddle.app"
    LINK_PATH="$HOME/.local/bin/huddle"
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
                *) usage_error "--base-url must be an https URL or an existing folder: $OPT_BASE_URL" || return $? ;;
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
    if [ "${OPT_FORCE:-0}" != 1 ] && [ -f "$APP_EXE" ] && step_satisfied "$STATE_FILE" app_sha256 "$sha"; then
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

# Set by install_node: 1 when the node folder was replaced, 0 when the step was skipped or failed.
NODE_CHANGED=0

# Seam: prints the version the node binary in $1 reports (`node --version`, e.g. v24.21.0).
node_version_of() {
    "$1" --version </dev/null
}

# Installs the Node release named by nodeVersion in <InstallRoot>/release-info.json (Spec section 6,
# Findings F2) so that node/bin/node and node/lib/node_modules/npm/bin/npm-cli.js exist. $1 is an
# optional base for SHASUMS256.txt and the tarball: an existing folder or an https URL, default
# https://nodejs.org/dist/<version>. The tarball name comes from HOST_RID (osx-arm64 -> darwin-arm64,
# osx-x64 -> darwin-x64) and is verified against its SHASUMS256.txt line. Sets NODE_CHANGED to 1 when
# the node folder was replaced and 0 when the step was skipped (the state holds this version and the
# installed binary prints it) or failed. Returns 1 on a failure, after logging it; the previous node
# is left in place. A running instance is stopped only once the new node is verified and ready to be
# swapped in. Does not write the install state (the caller does, last).
install_node() {
    local base version bare rid arch name wrapper root staging old sums_file saved_sums expected
    local archive kind inner installed old_moved
    base=${1:-}
    NODE_CHANGED=0
    root=${INSTALL_ROOT%/}
    if [ ! -f "$root/release-info.json" ]; then
        log_step fail node "$root/release-info.json does not exist" "Run the installer again so the app is installed first."
        return 1
    fi
    version=$(sed -n 's/.*"nodeVersion" *: *"\([^"]*\)".*/\1/p' "$root/release-info.json" | head -1) || version=""
    if [ -z "$version" ]; then
        log_step fail node "release-info.json has no nodeVersion" "Download the release again."
        return 1
    fi
    bare=${version#v}
    if ! printf '%s\n' "$bare" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
        log_step fail node "the Node version '$version' is not of the form v24.21.0" "Download the release again."
        return 1
    fi
    version="v$bare"
    if [ -x "$NODE_BIN" ] && { step_satisfied "$STATE_FILE" node_version "$version" || step_satisfied "$STATE_FILE" node_version "$bare"; }; then
        installed=$(node_version_of "$NODE_BIN" 2>/dev/null) || installed=""
        installed=$(printf '%s' "$installed" | tr -d ' \r\n')
        if [ "${installed#v}" = "$bare" ]; then
            log_step skip node "already installed ($version)"
            return 0
        fi
    fi
    rid=${HOST_RID:-}
    case $rid in
        osx-arm64) arch=arm64 ;;
        osx-x64) arch=x64 ;;
        *)
            log_step fail node "unsupported CPU '$rid'" "Agency Huddle needs an Apple Silicon or Intel Mac."
            return 1
            ;;
    esac
    wrapper="node-$version-darwin-$arch"
    name="$wrapper.tar.gz"
    if [ -z "$base" ]; then
        base="https://nodejs.org/dist/$version"
    fi
    base=${base%/}
    if [ -d "$base" ]; then
        kind=file
    else
        case $base in
            https://*) kind=url ;;
            *)
                log_step fail node "the Node source must be an https URL or an existing folder: $base"
                return 1
                ;;
        esac
    fi
    log_step work node "downloading $name"
    sums_file="$CACHE_DIR/node-$version-SHASUMS256.txt"
    if ! mkdir -p "$CACHE_DIR" || ! rm -f "$sums_file"; then
        log_step fail node "cannot use the download cache $CACHE_DIR" "Check the permissions on $CACHE_DIR."
        return 1
    fi
    # SHASUMS256.txt has no hash of its own; it comes from the same TLS-protected origin as the
    # tarball and decides the tarball's expected hash.
    if [ "$kind" = file ]; then
        cp "$base/SHASUMS256.txt" "$sums_file" || true
    else
        http_get "$base/SHASUMS256.txt" "$sums_file" || true
    fi
    # parse_sums replaces the release's SUMS, so the caller's SUMS is put back straight away.
    saved_sums=${SUMS:-}
    expected=""
    if [ -f "$sums_file" ] && parse_sums "$sums_file"; then
        expected=$(expected_hash "$name") || expected=""
    fi
    SUMS=$saved_sums
    if [ -z "$expected" ]; then
        log_step fail node "cannot read the checksum of $name from $base/SHASUMS256.txt" "Check your network or proxy settings (HTTPS_PROXY) and run the installer again."
        return 1
    fi
    if ! archive=$(get_verified_asset "$name" "$expected" "$kind:$base/$name" "$CACHE_DIR"); then
        log_step fail node "cannot get a verified $name" "Check your network or proxy settings (HTTPS_PROXY) and run the installer again."
        return 1
    fi
    staging="$root/node.staging"
    old="$root/node.old"
    # Leftovers of an interrupted run: a swap that died between its two renames, then stale folders.
    if [ ! -e "$root/node" ] && [ -e "$old" ]; then
        if ! mv "$old" "$root/node"; then
            log_step fail node "cannot restore $old" "Rename $old to $root/node by hand and run the installer again."
            return 1
        fi
    fi
    rm -rf "$staging" "$old"
    if ! mkdir -p "$staging" || ! run_tar "$archive" "$staging"; then
        rm -rf "$staging"
        log_step fail node "cannot extract $archive" "Delete the file and run the installer again."
        return 1
    fi
    inner="$staging/$wrapper"
    if [ ! -f "$inner/bin/node" ] || [ ! -f "$inner/lib/node_modules/npm/bin/npm-cli.js" ]; then
        rm -rf "$staging"
        log_step fail node "the Node archive does not contain $wrapper/bin/node and lib/node_modules/npm/bin/npm-cli.js; the previous node was left untouched" "Delete $archive and run the installer again."
        return 1
    fi
    if ! chmod +x "$inner/bin/node"; then
        rm -rf "$staging"
        log_step fail node "cannot make node runnable; the previous node was left untouched" "Run the installer again."
        return 1
    fi
    old_moved=0
    if [ -e "$root/node" ]; then
        # A running adapter holds the node folder open, so the app stops before the swap.
        if ! stop_huddle_if_running; then
            rm -rf "$staging"
            return 1
        fi
        if ! mv "$root/node" "$old"; then
            rm -rf "$staging"
            log_step fail node "cannot rename $root/node; the previous node was left untouched" "Close anything using it and run the installer again."
            return 1
        fi
        old_moved=1
    fi
    if ! mv "$inner" "$root/node"; then
        if [ "$old_moved" = 1 ] && [ ! -e "$root/node" ]; then
            mv "$old" "$root/node" || true
        fi
        rm -rf "$staging"
        log_step fail node "cannot move the new node into place; the previous node was restored" "Run the installer again."
        return 1
    fi
    rm -rf "$staging" "$old"
    NODE_CHANGED=1
    log_step ok node "installed ($version)"
    return 0
}

# Set by install_adapter: 1 when npm ci ran and installed the adapter, 0 when the step was skipped or failed.
ADAPTER_CHANGED=0

# Seam: runs the bundled node on the bundled npm CLI with the given arguments (never a global npm).
# The caller sets the working directory and PATH; stdin is the caller's business.
run_npm_ci() {
    "$NODE_BIN" "$INSTALL_ROOT/node/lib/node_modules/npm/bin/npm-cli.js" "$@"
}

# Installs the adapter's production dependencies into <InstallRoot>/tools/acp with the bundled node
# (Spec section 6). Sets ADAPTER_CHANGED to 1 when `npm ci` ran and 0 when the step was skipped (the
# state holds the SHA-256 of package-lock.json and the adapter entry exists) or failed. Returns 1 on a
# failure, after logging it and the last 15 lines of $CACHE_DIR/npm-ci.log. A running instance is
# stopped only once `npm ci` is certain to run. Does not write the install state (the caller does,
# last). Needs install_node to have run: it uses the node and npm under <InstallRoot>/node.
install_adapter() {
    local root acp_dir lock sha npm_cli log rc
    ADAPTER_CHANGED=0
    root=${INSTALL_ROOT%/}
    acp_dir="$root/tools/acp"
    lock="$acp_dir/package-lock.json"
    npm_cli="$root/node/lib/node_modules/npm/bin/npm-cli.js"
    log="$CACHE_DIR/npm-ci.log"
    if [ ! -f "$lock" ]; then
        log_step fail adapter "$lock does not exist" "Download the release again."
        return 1
    fi
    sha=$(file_sha256 "$lock") || return 1
    if [ -f "$ADAPTER_ENTRY" ] && step_satisfied "$STATE_FILE" lock_sha256 "$sha"; then
        log_step skip adapter "already installed"
        return 0
    fi
    if [ ! -x "$NODE_BIN" ] || [ ! -f "$npm_cli" ]; then
        log_step fail adapter "the bundled node and npm are missing from $root/node" "Run the installer again so the node step runs first."
        return 1
    fi
    if ! mkdir -p "$CACHE_DIR"; then
        log_step fail adapter "cannot create $CACHE_DIR" "Check the permissions on $CACHE_DIR."
        return 1
    fi
    # node_modules is replaced, and a running adapter holds files in it open.
    if ! stop_huddle_if_running; then
        return 1
    fi
    log_step work adapter "installing the adapter with npm ci"
    rc=0
    (
        cd "$acp_dir" && PATH="$root/node/bin:$PATH" && export PATH && run_npm_ci ci --omit=dev --no-audit --no-fund
    ) >"$log" 2>&1 </dev/null || rc=$?
    if [ "$rc" -ne 0 ]; then
        tail -n 15 "$log" >&2
        log_step fail adapter "npm ci failed (exit code $rc); the full log is $log" "Check your network, and if you are behind a proxy set HTTPS_PROXY (and HTTP_PROXY), then run the installer again."
        return 1
    fi
    if [ ! -f "$ADAPTER_ENTRY" ]; then
        log_step fail adapter "npm ci finished but claude-agent-acp is missing (expected $ADAPTER_ENTRY); the full log is $log" "Run the installer again, or check $log."
        return 1
    fi
    ADAPTER_CHANGED=1
    log_step ok adapter "installed"
    return 0
}

# Quotes a string for use in shell: wraps it in single quotes, escaping ' as '"'"'
shell_quote() {
    local str="$1"
    # Replace each single quote with '"'"' (close quote, double-quoted quote, open quote)
    # This is equivalent to '\'' but using double quotes
    printf '%s\n' "$str" | sed "s/'/'\"'\"'/g" | sed -e 's/^/'"'"'/' -e "s/$/'/"
}

# Generates the launcher shell script. Uses APP_EXE, NODE_BIN, DATA_ROOT, INSTALL_ROOT.
launcher_content() {
    local node_dir
    node_dir=$(dirname "$NODE_BIN")

    printf '%s\n' "#!/bin/sh"
    printf '%s\n' "set -eu"
    printf '%s\n' "export Team__Desktop__Enabled=true"
    printf '%s\n' "export Team__Acp__Enabled=true"
    printf "export Team__Acp__Command=%s\n" "$(shell_quote "$NODE_BIN")"
    printf "export Team__DataDir=%s\n" "$(shell_quote "$DATA_ROOT/data")"
    printf '%s\n' "export Team__PipeName=\"huddle-\$(id -un)\""
    printf '%s\n' "export ASPNETCORE_URLS='http://127.0.0.1:0'"
    printf '%s\n' "export ASPNETCORE_ENVIRONMENT=Production"
    printf '%s\n' "export AllowedHosts='localhost;127.0.0.1'"
    printf "export PATH=%s:\"\$PATH\"\n" "$(shell_quote "$node_dir")"
    printf "exec %s \"\$@\"\n" "$(shell_quote "$APP_EXE")"
}

# Generates the app stub script for macOS .app bundle. Uses INSTALL_ROOT, DATA_ROOT.
app_stub_content() {
    printf '%s\n' "#!/bin/sh"
    printf "exec %s \"\$@\" >>%s 2>&1\n" "$(shell_quote "$INSTALL_ROOT/bin/huddle")" "$(shell_quote "$DATA_ROOT/launcher.log")"
}

# Generates the Info.plist XML for macOS .app bundle. Takes VERSION as argument.
plist_content() {
    local version="$1"
    local version_escaped
    version_escaped=$(printf '%s' "$version" | sed 's/&/\&amp;/g; s/</\&lt;/g; s/>/\&gt;/g')

    printf '%s\n' "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
    printf '%s\n' "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">"
    printf '%s\n' "<plist version=\"1.0\">"
    printf '%s\n' "<dict>"
    printf '%s\n' "	<key>CFBundleExecutable</key>"
    printf '%s\n' "	<string>AgencyHuddle</string>"
    printf '%s\n' "	<key>CFBundleIdentifier</key>"
    printf '%s\n' "	<string>com.agency.huddle</string>"
    printf '%s\n' "	<key>CFBundleName</key>"
    printf '%s\n' "	<string>Agency Huddle</string>"
    printf '%s\n' "	<key>CFBundlePackageType</key>"
    printf '%s\n' "	<string>APPL</string>"
    printf '%s\n' "	<key>CFBundleVersion</key>"
    printf "%s\n" "	<string>$version_escaped</string>"
    printf '%s\n' "	<key>LSUIElement</key>"
    printf '%s\n' "	<true/>"
    printf '%s\n' "</dict>"
    printf '%s' "</plist>"
}

# ---- Launcher, .app bundle and symlink (Spec sections 3 and 6) ------------------------------

LAUNCHER_CHANGED=0
FILE_WRITTEN=0

# Seam: a scratch file outside the install folders; prints its path.
make_temp_file() {
    mktemp "${TMPDIR:-/tmp}/huddle-install.XXXXXX"
}

# Seam: sets the execute bit on the file $1.
make_executable() {
    chmod 755 "$1"
}

# Seam: makes the symbolic link $2 pointing at $1.
make_symlink() {
    ln -s "$1" "$2"
}

# Seam: prints the target of the symbolic link $1 and succeeds; fails for anything that is not a link.
read_symlink() {
    [ -L "$1" ] || return 1
    readlink "$1"
}

# Seam: removes the symbolic link $1 (the caller has already checked that it is a link).
remove_symlink() {
    rm -f "$1"
}

# Writes the output of the command "$3..." to the file $1, but only when it differs from what is
# there (cmp -s), so an unchanged file keeps its mtime. $2 is 1 to make the file executable. Sets
# FILE_WRITTEN and LAUNCHER_CHANGED to 1 when the file changed. Returns 1 when something could not be written.
write_if_changed() {
    local dest exec_flag scratch staged
    dest=$1
    exec_flag=$2
    shift 2
    FILE_WRITTEN=0
    scratch=$(make_temp_file) || return 1
    if ! "$@" >"$scratch"; then
        rm -f "$scratch"
        return 1
    fi
    if [ -f "$dest" ] && cmp -s "$scratch" "$dest"; then
        rm -f "$scratch"
        if [ "$exec_flag" = 1 ] && [ ! -x "$dest" ]; then
            make_executable "$dest" || return 1
            FILE_WRITTEN=1
            LAUNCHER_CHANGED=1
        fi
        return 0
    fi
    staged="$dest.new.$$"
    if ! cp "$scratch" "$staged"; then
        rm -f "$scratch" "$staged"
        return 1
    fi
    rm -f "$scratch"
    if [ "$exec_flag" = 1 ] && ! make_executable "$staged"; then
        rm -f "$staged"
        return 1
    fi
    if ! mv -f "$staged" "$dest"; then
        rm -f "$staged"
        return 1
    fi
    FILE_WRITTEN=1
    LAUNCHER_CHANGED=1
    return 0
}

# Re-asserts the entry points on every run and rewrites only what differs: <InstallRoot>/bin/huddle,
# the .app bundle (Info.plist, MacOS/AgencyHuddle) and the symlink LINK_PATH. Reads the bundle
# version from <InstallRoot>/release-info.json. Sets LAUNCHER_CHANGED (1 when anything changed).
# A regular file at LINK_PATH is never overwritten (warns). Returns 1 when a file cannot be written.
install_launcher() {
    local info version link_dir link_target current link_ok
    LAUNCHER_CHANGED=0
    info="$INSTALL_ROOT/release-info.json"
    version=""
    if [ -f "$info" ]; then
        version=$(sed -n 's/.*"version" *: *"\([^"]*\)".*/\1/p' "$info" | head -1) || version=""
    fi
    if [ -z "$version" ]; then
        log_step fail launcher "$info has no version" "Run the installer again so the app is installed first."
        return 1
    fi
    link_dir=$(dirname "$LINK_PATH")
    link_target="$INSTALL_ROOT/bin/huddle"
    if ! mkdir -p "$INSTALL_ROOT/bin" "$APP_BUNDLE/Contents/MacOS" "$link_dir"; then
        log_step fail launcher "cannot create the launcher folders" "Check that you may write to $INSTALL_ROOT, $APP_BUNDLE and $link_dir."
        return 1
    fi
    if ! write_if_changed "$link_target" 1 launcher_content ||
        ! write_if_changed "$APP_BUNDLE/Contents/Info.plist" 0 plist_content "$version" ||
        ! write_if_changed "$APP_BUNDLE/Contents/MacOS/AgencyHuddle" 1 app_stub_content; then
        log_step fail launcher "cannot write the launcher files" "Check that you may write to $INSTALL_ROOT and $APP_BUNDLE."
        return 1
    fi
    link_ok=1
    if current=$(read_symlink "$LINK_PATH"); then
        if [ "$current" != "$link_target" ]; then
            if ! remove_symlink "$LINK_PATH" || ! make_symlink "$link_target" "$LINK_PATH"; then
                log_step fail launcher "cannot replace the link $LINK_PATH" "Remove it by hand and run the installer again."
                return 1
            fi
            LAUNCHER_CHANGED=1
        fi
    elif [ -e "$LINK_PATH" ] || [ -L "$LINK_PATH" ]; then
        link_ok=0
        log_step warn link "$LINK_PATH already exists and is not a link; left alone. Run $link_target instead."
    else
        if ! make_symlink "$link_target" "$LINK_PATH"; then
            log_step fail launcher "cannot create the link $LINK_PATH" "Check that you may write to $link_dir."
            return 1
        fi
        LAUNCHER_CHANGED=1
    fi
    if [ "$LAUNCHER_CHANGED" -eq 1 ]; then
        log_step ok launcher "bin/huddle, Agency Huddle.app and the huddle link are in place"
    else
        log_step skip launcher "up to date"
    fi
    if [ "$link_ok" -eq 1 ]; then
        case ":$PATH:" in
            *":$link_dir:"*) ;;
            *) printf 'hint: %s is not on your PATH, so "huddle" will not run from a terminal; add it to your shell profile.\n' "$link_dir" ;;
        esac
    fi
    return 0
}

# ---- Uninstall (Spec section 6, rules 6 and 7) ----------------------------------------------

SAFE_CANON=""
SAFE_REASON=""

# Seam: deletes the folder $1 recursively. Only ever called with a path safe_to_delete accepted;
# it still refuses an empty path and the filesystem root.
remove_tree() {
    case ${1:-} in
        '' | /) return 1 ;;
    esac
    rm -rf -- "$1"
}

# Decides whether the folder $1 may be deleted. $2 names a marker (relative to $1) that must exist
# inside it, proving the folder is ours. Succeeds only for an absolute path to a real folder (not a
# symbolic link) that is not the filesystem root, not the home folder or a folder holding it, has at
# least 3 path segments once canonicalised (cd ... && pwd -P) and holds the marker. On success sets
# SAFE_CANON to the canonical path; on failure sets SAFE_REASON.
safe_to_delete() {
    local dir marker canon home_dir home_canon slashes
    dir=${1:-}
    marker=${2:-}
    SAFE_CANON=""
    SAFE_REASON=""
    if [ -z "$dir" ]; then
        SAFE_REASON="the path is empty"
        return 1
    fi
    if [ -z "$marker" ]; then
        SAFE_REASON="no marker file was named for it"
        return 1
    fi
    case $dir in
        /*) ;;
        *)
            SAFE_REASON="the path is not absolute"
            return 1
            ;;
    esac
    if [ -L "$dir" ]; then
        SAFE_REASON="it is a symbolic link"
        return 1
    fi
    if [ ! -d "$dir" ]; then
        SAFE_REASON="it is not a folder"
        return 1
    fi
    canon=$(cd "$dir" 2>/dev/null && pwd -P) || canon=""
    if [ -z "$canon" ]; then
        SAFE_REASON="the folder cannot be resolved"
        return 1
    fi
    if [ "$canon" = "/" ]; then
        SAFE_REASON="it is the filesystem root"
        return 1
    fi
    home_dir=${HOME:-}
    if [ -n "$home_dir" ] && home_canon=$(cd "$home_dir" 2>/dev/null && pwd -P); then
        case "$home_canon/" in
            "$canon/"*)
                SAFE_REASON="it is your home folder or a folder that contains your home folder"
                return 1
                ;;
        esac
    fi
    slashes=$(printf '%s' "$canon" | tr -cd '/' | wc -c)
    if [ "$((slashes))" -lt 3 ]; then
        SAFE_REASON="it has fewer than 3 path segments"
        return 1
    fi
    if [ ! -e "$canon/$marker" ]; then
        SAFE_REASON="it does not contain $marker, so it does not look like an Agency Huddle folder"
        return 1
    fi
    SAFE_CANON=$canon
    return 0
}

# Removes the install folder, the .app bundle and the symlink; the data folder only with
# --purge-data. Everything that is missing prints [skip]; every folder is checked with
# safe_to_delete before anything is deleted, so a refusal deletes nothing. Stops a running app
# first. Returns 0 (including "nothing installed"), 1 on a refusal or failure.
do_uninstall() {
    local purge have_install have_bundle have_link have_data install_canon bundle_canon data_canon data_any current removed
    purge=${OPT_PURGE_DATA:-0}
    if [ -z "$INSTALL_ROOT" ] || [ -z "$DATA_ROOT" ]; then
        log_step fail uninstall "an install or data folder path is empty" "Pass --install-root and --data-root, or leave both out."
        return 1
    fi
    have_install=0
    have_bundle=0
    have_link=0
    have_data=0
    install_canon=""
    bundle_canon=""
    data_canon=""
    if [ -e "$INSTALL_ROOT" ] || [ -L "$INSTALL_ROOT" ]; then
        have_install=1
    else
        log_step skip install "not installed ($INSTALL_ROOT)"
    fi
    if [ -e "$APP_BUNDLE" ] || [ -L "$APP_BUNDLE" ]; then
        have_bundle=1
    else
        log_step skip app "no app bundle ($APP_BUNDLE)"
    fi
    if [ -e "$LINK_PATH" ] || [ -L "$LINK_PATH" ]; then
        have_link=1
    else
        log_step skip link "no link ($LINK_PATH)"
    fi
    if [ "$purge" -eq 1 ]; then
        if [ -e "$DATA_ROOT" ] || [ -L "$DATA_ROOT" ]; then
            have_data=1
        else
            log_step skip data "no data folder ($DATA_ROOT)"
        fi
    fi

    if [ "$have_install" -eq 1 ]; then
        if ! safe_to_delete "$INSTALL_ROOT" install-state; then
            log_step fail uninstall "refusing to remove $INSTALL_ROOT: $SAFE_REASON" "Pass --install-root with an Agency Huddle install folder, or delete the folder yourself."
            return 1
        fi
        install_canon=$SAFE_CANON
        data_any=$(cd "$DATA_ROOT" 2>/dev/null && pwd -P) || data_any=""
        case "$data_any/" in
            "$install_canon/"*)
                if [ "$purge" -ne 1 ]; then
                    log_step fail uninstall "the data folder $DATA_ROOT is inside the install folder $INSTALL_ROOT" "Pass --purge-data to remove both, or move the data folder out first."
                    return 1
                fi
                ;;
        esac
    fi
    if [ "$have_data" -eq 1 ]; then
        if ! safe_to_delete "$DATA_ROOT" data && ! safe_to_delete "$DATA_ROOT" cache; then
            log_step fail uninstall "refusing to remove $DATA_ROOT: $SAFE_REASON" "Pass --data-root with the Agency Huddle data folder, or delete the folder yourself."
            return 1
        fi
        data_canon=$SAFE_CANON
    fi
    if [ "$have_bundle" -eq 1 ]; then
        if safe_to_delete "$APP_BUNDLE" Contents/Info.plist; then
            bundle_canon=$SAFE_CANON
        else
            have_bundle=0
            log_step warn app "$APP_BUNDLE was left alone: $SAFE_REASON"
        fi
    fi
    if [ "$have_link" -eq 1 ]; then
        current=""
        current=$(read_symlink "$LINK_PATH") || current=""
        if [ "$current" != "$INSTALL_ROOT/bin/huddle" ]; then
            have_link=0
            log_step skip link "$LINK_PATH is not a link to this install; left alone"
        fi
    fi

    if [ "$have_install" -eq 0 ] && [ "$have_bundle" -eq 0 ] && [ "$have_link" -eq 0 ] && [ "$have_data" -eq 0 ]; then
        return 0
    fi
    stop_huddle_if_running || return 1

    removed=0
    if [ "$have_link" -eq 1 ]; then
        if ! remove_symlink "$LINK_PATH"; then
            log_step fail link "cannot remove $LINK_PATH" "Remove it by hand and run the uninstall again."
            return 1
        fi
        log_step ok link "removed $LINK_PATH"
        removed=1
    fi
    if [ "$have_bundle" -eq 1 ]; then
        if ! remove_tree "$bundle_canon"; then
            log_step fail app "cannot remove $APP_BUNDLE" "Remove it by hand and run the uninstall again."
            return 1
        fi
        log_step ok app "removed $APP_BUNDLE"
        removed=1
    fi
    if [ "$have_install" -eq 1 ]; then
        if ! remove_tree "$install_canon"; then
            log_step fail install "cannot remove $INSTALL_ROOT" "Close anything using it, remove it by hand and run the uninstall again."
            return 1
        fi
        log_step ok install "removed $INSTALL_ROOT"
        removed=1
    fi
    if [ "$have_data" -eq 1 ]; then
        if [ -d "$data_canon" ] && ! remove_tree "$data_canon"; then
            log_step fail data "cannot remove $DATA_ROOT" "Remove it by hand."
            return 1
        fi
        log_step ok data "removed $DATA_ROOT"
        removed=1
    elif [ "$removed" -eq 1 ] && [ -d "$DATA_ROOT" ]; then
        log_step skip data "kept $DATA_ROOT (--purge-data removes it)"
    fi
    return 0
}

# ---- Orchestration (Spec sections 6 and 7) -----------------------------------------------------

LOCK_HELD=0
LOCK_DIR=""
# Seconds the Claude sign-in check may take before it counts as "not signed in".
AUTH_STATUS_SECONDS=10

# Takes the single-installer lock: mkdir is atomic, so the second installer fails at once. Sets
# LOCK_HELD only when this run created the folder. Returns 1 when the lock is taken by someone else
# or the cache folder cannot be created.
acquire_lock() {
    LOCK_DIR="$CACHE_DIR/install.lock"
    mkdir -p "$CACHE_DIR" 2>/dev/null || return 1
    mkdir "$LOCK_DIR" 2>/dev/null || return 1
    LOCK_HELD=1
}

# Removes the lock, but only one this run took. Also the EXIT trap of the entry path.
release_lock() {
    [ "$LOCK_HELD" = 1 ] || return 0
    LOCK_HELD=0
    # --purge-data removes the data root, and the lock with it: then there is nothing to release.
    rmdir "$LOCK_DIR" 2>/dev/null || true
}

# acquire_lock plus the [!!] message and fix. Returns 1 on failure.
take_lock() {
    if acquire_lock; then
        return 0
    fi
    if [ -d "$CACHE_DIR/install.lock" ]; then
        log_step fail installer "another Agency Huddle installer is already running." "Wait for it to finish, then run this command again. If none is running, delete the folder $CACHE_DIR/install.lock and run it again."
    else
        log_step fail installer "cannot create $CACHE_DIR" "Check that you may write to $DATA_ROOT."
    fi
    return 1
}

# Seam: starts the app bundle in $1 the way a double-click does.
run_open() {
    open "$1" </dev/null
}

# Seam: runs "<claude> auth status" and returns its exit code (0 = signed in). Output is discarded
# (it names the account) and stdin is closed. A binary that is missing, not executable or still
# running after AUTH_STATUS_SECONDS counts as not signed in.
claude_auth_status() {
    local claude pid watchdog rc
    claude=$1
    [ -x "$claude" ] || return 1
    "$claude" auth status >/dev/null 2>&1 </dev/null &
    pid=$!
    ( sleep "$AUTH_STATUS_SECONDS"; kill "$pid" ) >/dev/null 2>&1 </dev/null &
    watchdog=$!
    rc=0
    wait "$pid" || rc=$?
    kill "$watchdog" >/dev/null 2>&1 || true
    return "$rc"
}

# 0 unless the environment says nobody can complete a browser sign-in (Findings F3).
interactive_environment() {
    [ -z "${NO_BROWSER:-}${SSH_CONNECTION:-}${SSH_CLIENT:-}${SSH_TTY:-}${CLAUDE_CODE_REMOTE:-}" ]
}

# Seam: 0 when a person is looking at this run. stdin is the script itself under `curl | sh`, so the
# terminal is judged by stdout.
is_interactive() {
    interactive_environment && [ -t 1 ]
}

# Prints the bundled claude binary for this Mac.
claude_path() {
    local arch
    case ${HOST_RID:-} in
        osx-x64) arch=x64 ;;
        *) arch=arm64 ;;
    esac
    printf '%s/tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-darwin-%s/claude' "${INSTALL_ROOT%/}" "$arch"
}

# The closing Claude sign-in note (Findings F3). Advice only: it never fails the install.
show_claude_signin() {
    local claude
    claude=$(claude_path)
    if is_interactive; then
        if claude_auth_status "$claude"; then
            printf 'Claude: signed in.\n'
            return 0
        fi
        printf 'Claude is not signed in yet. Sign in once, then start Agency Huddle:\n'
    else
        printf 'To use Claude, sign in once before you start Agency Huddle:\n'
    fi
    printf '  "%s" auth login\n' "$claude"
    printf '(This opens your browser. Add --console to use API billing instead of a Claude subscription.)\n'
    return 0
}

# Copies or downloads SHA256SUMS into the cache and parses it into SUMS (3 attempts for a URL).
fetch_sums() {
    local dest attempt
    dest="$CACHE_DIR/SHA256SUMS"
    resolve_asset SHA256SUMS || return $?
    if ! mkdir -p "$CACHE_DIR"; then
        log_step fail release "cannot create $CACHE_DIR" "Check that you may write to $DATA_ROOT."
        return 1
    fi
    rm -f "$dest"
    if [ "$ASSET_KIND" = file ]; then
        if ! cp "$ASSET_SRC" "$dest" 2>/dev/null; then
            log_step fail release "cannot read $ASSET_SRC" "Check --base-url: the folder must hold SHA256SUMS and the release archives."
            return 1
        fi
    else
        attempt=1
        while ! http_get "$ASSET_SRC" "$dest"; do
            rm -f "$dest"
            if [ "$attempt" -ge 3 ]; then
                log_step fail release "cannot download $ASSET_SRC" "Check your network connection and --version, then run the installer again."
                return 1
            fi
            if [ "$attempt" -eq 1 ]; then
                sleep_seconds 1
            else
                sleep_seconds 3
            fi
            attempt=$((attempt + 1))
        done
    fi
    if ! parse_sums "$dest"; then
        log_step fail release "cannot read $dest" "Run the installer again."
        return 1
    fi
}

# Writes a state key unless it already holds the value, so a run with nothing new touches no file.
state_set() {
    if step_satisfied "$STATE_FILE" "$1" "$2"; then
        return 0
    fi
    write_state_key "$STATE_FILE" "$1" "$2"
}

# Prints a flat string value of the installed release-info.json.
release_info_value() {
    sed -n "s/.*\"$1\" *: *\"\([^\"]*\)\".*/\1/p" "$INSTALL_ROOT/release-info.json" 2>/dev/null | head -1
}

# Logs a failed state write and returns 1.
state_failed() {
    log_step fail state "cannot write $STATE_FILE" "Check that you may write to $INSTALL_ROOT, then run the installer again."
    return 1
}

# Steps 1 to 5 of Spec section 6 plus summary, sign-in note and launch. Each step writes its state key
# after itself, so an interrupted run resumes at the first unsatisfied step. Returns 0, or 1 after
# logging the failing step.
install_steps() {
    local name expected archive version node_version lock_sha
    fetch_sums || return 1
    name="huddle-$HOST_RID.tar.gz"
    if ! expected=$(expected_hash "$name" 2>/dev/null); then
        log_step fail release "SHA256SUMS has no line for $name" "Use a release that contains $name for this Mac (--version), or run the installer attached to it."
        return 1
    fi
    # A satisfied app step is decided from the SHA256SUMS line alone: the archive is not downloaded.
    if [ "${OPT_FORCE:-0}" != 1 ] && [ -f "$APP_EXE" ] && step_satisfied "$STATE_FILE" app_sha256 "$expected"; then
        log_step skip app "already installed"
        APP_CHANGED=0
    else
        resolve_asset "$name" || return $?
        if ! archive=$(get_verified_asset "$name" "$expected" "$ASSET_KIND:$ASSET_SRC" "$CACHE_DIR"); then
            log_step fail app "cannot get a verified $name" "Check your network connection (the reason is above), then run the installer again."
            return 1
        fi
        install_app "$archive" || return 1
    fi
    version=$(release_info_value version)
    if [ -z "$version" ]; then
        log_step fail app "$INSTALL_ROOT/release-info.json has no version" "Run the installer again with --force."
        return 1
    fi
    state_set schema 1 || state_failed || return 1
    state_set version "$version" || state_failed || return 1
    state_set app_sha256 "$expected" || state_failed || return 1

    install_node || return 1
    node_version=$(release_info_value nodeVersion)
    state_set node_version "$node_version" || state_failed || return 1

    install_adapter || return 1
    lock_sha=$(file_sha256 "$INSTALL_ROOT/tools/acp/package-lock.json") || return 1
    state_set lock_sha256 "$lock_sha" || state_failed || return 1

    install_launcher || return 1

    if [ "$APP_CHANGED" = 1 ] || [ "$NODE_CHANGED" = 1 ] || [ "$ADAPTER_CHANGED" = 1 ] || [ "$LAUNCHER_CHANGED" = 1 ]; then
        printf 'Agency Huddle %s installed.\n' "$version"
    else
        printf 'Already up to date (%s).\n' "$version"
    fi
    printf 'Start it from Launchpad or Spotlight (Agency Huddle), or open a new terminal and run: huddle\n'
    show_claude_signin
    if [ "${OPT_NO_LAUNCH:-0}" != 1 ]; then
        if run_open "$APP_BUNDLE"; then
            log_step ok launch "started Agency Huddle"
        else
            log_step fail launch "could not start Agency Huddle" "Start it from Launchpad or Spotlight (Agency Huddle), or open a new terminal and run: huddle"
        fi
    fi
    return 0
}

# Runs the whole installer and returns its exit code (0 success or up to date, 1 a step failed, 2
# prerequisites unmet, 3 bad usage). Never exits: only the entry guard below does.
# Order: arguments, prerequisites (an uninstall skips them: it must work offline and on a Mac that no
# longer qualifies), lock, uninstall or install steps.
main() {
    local rc
    parse_args "$@" || return $?
    if [ "$OPT_PURGE_DATA" = 1 ] && [ "$OPT_UNINSTALL" != 1 ]; then
        usage_error "--purge-data only works together with --uninstall. Run again with --uninstall --purge-data to remove the program and your data, or drop --purge-data." || return $?
    fi
    if [ "$OPT_UNINSTALL" != 1 ] && [ -z "$OPT_BASE_URL" ] && [ "$HUDDLE_REPO" = "$HUDDLE_REPO_PLACEHOLDER" ]; then
        log_step fail usage "this copy does not know which repository to download from." "Run the install.sh attached to a release, or pass --base-url <folder|https url>." >&2
        return 3
    fi
    if [ -z "${HOME:-}" ]; then
        log_step fail usage "HOME is not set, so the install folders cannot be worked out." "Run the installer from a normal login shell, or pass --install-root and --data-root and set HOME." >&2
        return 3
    fi
    resolve_asset SHA256SUMS || return $?
    resolve_paths
    if [ "$OPT_UNINSTALL" != 1 ] || [ "$OPT_CHECK_ONLY" = 1 ]; then
        check_prerequisites
        print_prerequisites
        if [ "$PREREQ_FAILED" -ne 0 ]; then
            return 2
        fi
        if [ "$OPT_CHECK_ONLY" = 1 ]; then
            printf 'All prerequisites are met.\n'
            return 0
        fi
    fi
    rc=0
    if [ "$OPT_UNINSTALL" = 1 ]; then
        # With no data folder there is nothing installed to race with, and nothing is created.
        if [ -d "$DATA_ROOT" ]; then
            take_lock || return 1
        fi
        do_uninstall || rc=$?
    else
        take_lock || return 1
        install_steps || rc=$?
    fi
    release_lock
    return "$rc"
}

# Entry guard: the only place that exits, and the only place that installs traps (a test that sources
# this file with HUDDLE_INSTALL_NO_MAIN set keeps its own traps).
if [ -z "${HUDDLE_INSTALL_NO_MAIN:-}" ]; then
    trap release_lock EXIT
    trap 'exit 129' HUP
    trap 'exit 130' INT
    trap 'exit 143' TERM
    main_status=0
    main "$@" || main_status=$?
    exit "$main_status"
fi
