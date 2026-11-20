#!/bin/sh
# Decides the version of the next release from the tags that already exist, so versions only go up.
#
#   gh api repos/OWNER/REPO/tags --paginate --jq '.[].name' | next-version.sh [--bump patch|minor|major] [--version X.Y.Z]
#
# Reads the existing tag names on stdin (v1.2.3 or 1.2.3, one per line; other tags are ignored) and
# prints one version without the v prefix.
# - With no --version, it bumps the highest existing version: patch (the default), minor or major.
#   When that highest version exists only as a pre-release (1.3.0-rc.1, no 1.3.0), the next version
#   is its final release, 1.3.0. With no tags at all the first version is 0.1.0.
# - With --version, it checks that version and prints it. It must be a release version (1.0.0, or
#   1.0.0-rc.1 for a pre-release) and newer than every existing tag. A pre-release sorts before its
#   final release; two pre-releases of the same version are both allowed, and the release workflow
#   refuses a tag that already exists.
set -eu

FIRST_VERSION=0.1.0

die() {
    echo "error: $*" >&2
    exit 1
}

# The pattern install.ps1, install.sh and the release workflow use for a version.
is_release_version() {
    printf '%s\n' "$1" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+([-+.][0-9A-Za-z.]+)*$'
}

# Prints "major minor patch final" for a version, where final is 1 for a release and 0 for a
# pre-release (a suffix starting with -). Build metadata (+...) does not change the order.
version_key() {
    printf '%s\n' "$1" | awk '{
        core = $0
        sub(/[-+].*$/, "", core)
        n = split(core, part, ".")
        rest = substr($0, length(core) + 1)
        final = (substr(rest, 1, 1) == "-") ? 0 : 1
        printf "%d %d %d %d\n", part[1], part[2], part[3], final
    }'
}

main() {
    bump='patch'
    version=""
    while [ $# -gt 0 ]; do
        case "$1" in
            --bump | --version)
                [ $# -ge 2 ] || die "$1 needs a value"
                case "$1" in
                    --bump) bump=$2 ;;
                    --version) version=$2 ;;
                esac
                shift 2
                ;;
            *) die "unknown argument: $1 (use --bump patch|minor|major or --version X.Y.Z)" ;;
        esac
    done

    case "$bump" in
        patch | minor | major) ;;
        *) die "--bump must be patch, minor or major, not '$bump'" ;;
    esac

    # The existing versions, highest first, as "major minor patch final".
    # "|| [ -n ... ]" keeps a last line that has no newline after it.
    existing=$(tr -d '\r' | sed 's/^v//' | while IFS= read -r tag || [ -n "$tag" ]; do
        if [ -n "$tag" ] && is_release_version "$tag"; then
            version_key "$tag"
        fi
    done | sort -k1,1nr -k2,2nr -k3,3nr -k4,4nr)
    highest=$(printf '%s\n' "$existing" | sed -n 1p)

    if [ -n "$version" ]; then
        version=${version#v}
        is_release_version "$version" || die "'$version' is not a release version such as 1.0.0 or 1.0.0-rc.1"
        if [ -n "$highest" ]; then
            # shellcheck disable=SC2046,SC2086 # word splitting into four numbers is the point
            set -- $(version_key "$version") $highest
            # Compare major, minor, patch, then final over pre-release. Two pre-releases of one
            # version (rc.1 then rc.2) count as newer.
            newer=$(awk -v a="$1 $2 $3 $4" -v b="$5 $6 $7 $8" 'BEGIN {
                split(a, x, " "); split(b, y, " ")
                for (i = 1; i <= 4; i++) {
                    if (x[i] + 0 > y[i] + 0) { print 1; exit }
                    if (x[i] + 0 < y[i] + 0) { print 0; exit }
                }
                print (x[4] == 0 && y[4] == 0) ? 1 : 0
            }')
            if [ "$newer" != 1 ]; then
                die "version $version is not newer than the latest existing version $5.$6.$7; versions only go up"
            fi
        fi
        printf '%s\n' "$version"
        return 0
    fi

    if [ -z "$highest" ]; then
        printf '%s\n' "$FIRST_VERSION"
        return 0
    fi

    # shellcheck disable=SC2086 # word splitting into four numbers is the point
    set -- $highest
    if [ "$4" -eq 0 ]; then
        # Only a pre-release of the highest version exists: its final release comes next.
        printf '%s.%s.%s\n' "$1" "$2" "$3"
        return 0
    fi

    case "$bump" in
        patch) printf '%s.%s.%s\n' "$1" "$2" $(($3 + 1)) ;;
        minor) printf '%s.%s.0\n' "$1" $(($2 + 1)) ;;
        major) printf '%s.0.0\n' $(($1 + 1)) ;;
    esac
}

[ -n "${HUDDLE_INSTALL_NO_MAIN:-}" ] || main "$@"
