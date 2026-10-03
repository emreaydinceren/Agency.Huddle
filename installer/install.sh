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
