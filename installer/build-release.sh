#!/bin/sh
# Builds one release archive (plan Spec 2): huddle-<rid>.zip for win-x64 and win-arm64,
# huddle-<rid>.tar.gz for osx-arm64 and osx-x64.
#
#   build-release.sh --version 1.2.3 --rid win-x64|win-arm64|osx-arm64|osx-x64 --out <dir> [--publish-dir <dir>]
#
# --publish-dir skips 'dotnet publish' and packages that directory as app/ (the test hook).
# Layout of the archive root: app/, tools/acp/package.json, tools/acp/package-lock.json, release-info.json.
set -eu

ROOT=$(cd "$(dirname "$0")/.." && pwd)
WORK=""

usage() {
    echo "usage: build-release.sh --version <version> --rid win-x64|win-arm64|osx-arm64|osx-x64 --out <dir> [--publish-dir <dir>]" >&2
}

die() {
    echo "error: $*" >&2
    exit 1
}

cleanup() {
    if [ -n "$WORK" ] && [ -d "$WORK" ]; then
        rm -rf "$WORK"
    fi
}

# Prints the path of a tar that is bsdtar (it can write zip), or nothing.
find_bsdtar() {
    for candidate in bsdtar tar /c/Windows/System32/tar.exe; do
        if command -v "$candidate" > /dev/null 2>&1 && "$candidate" --version 2> /dev/null | grep -qi bsdtar; then
            command -v "$candidate"
            return 0
        fi
    done
    return 0
}

# make_zip <stage-dir> <zip-path>: archives app, tools and release-info.json at the zip root.
make_zip() {
    if command -v zip > /dev/null 2>&1; then
        (cd "$1" && zip -qr "$2" app tools release-info.json)
        return 0
    fi
    bsd=$(find_bsdtar)
    if [ -n "$bsd" ]; then
        "$bsd" -a -cf "$2" -C "$1" app tools release-info.json
        return 0
    fi
    die "neither 'zip' nor bsdtar is installed; fix: install zip (apt-get install zip) or use a bsdtar"
}

main() {
    version=""
    rid=""
    out=""
    publish_dir=""

    while [ $# -gt 0 ]; do
        case "$1" in
            --version | --rid | --out | --publish-dir)
                [ $# -ge 2 ] || { usage; die "$1 needs a value"; }
                case "$1" in
                    --version) version=$2 ;;
                    --rid) rid=$2 ;;
                    --out) out=$2 ;;
                    --publish-dir) publish_dir=$2 ;;
                esac
                shift 2
                ;;
            *)
                usage
                die "unknown argument: $1"
                ;;
        esac
    done

    [ -n "$version" ] || { usage; die "--version is required"; }
    [ -n "$rid" ] || { usage; die "--rid is required"; }
    [ -n "$out" ] || { usage; die "--out is required"; }

    case "$version" in
        *[!A-Za-z0-9._+-]*) die "--version may only contain letters, digits and . _ + -: $version" ;;
    esac
    case "$rid" in
        win-x64 | win-arm64 | osx-arm64 | osx-x64) ;;
        *) die "unsupported --rid '$rid' (use win-x64, win-arm64, osx-arm64 or osx-x64)" ;;
    esac
    if [ -n "$publish_dir" ] && [ ! -d "$publish_dir" ]; then
        die "--publish-dir is not a directory: $publish_dir"
    fi

    node_file="$ROOT/installer/node-version.txt"
    [ -f "$node_file" ] || die "missing $node_file"
    node_version=$(tr -d '\r\n ' < "$node_file")
    [ -n "$node_version" ] || die "$node_file is empty"

    for f in package.json package-lock.json; do
        [ -f "$ROOT/tools/acp/$f" ] || die "missing $ROOT/tools/acp/$f"
    done

    mkdir -p "$out"
    out=$(cd "$out" && pwd)

    WORK=$(mktemp -d)
    trap cleanup EXIT INT TERM
    stage="$WORK/stage"
    mkdir -p "$stage/tools/acp"

    if [ -n "$publish_dir" ]; then
        cp -R "$publish_dir" "$stage/app"
    else
        # Self-contained and ReadyToRun; deliberately not trimmed or AOT (decision D2).
        # -p:Version stamps the release version into the binaries (file and product version), which
        # otherwise all say 1.0.0.
        dotnet publish "$ROOT/src/Huddle.App/Huddle.App.csproj" -c Release -r "$rid" \
            --self-contained true -p:PublishReadyToRun=true -p:Version="$version" \
            -p:DebugType=none -p:DebugSymbols=false -o "$stage/app"
    fi

    cp "$ROOT/tools/acp/package.json" "$stage/tools/acp/package.json"
    cp "$ROOT/tools/acp/package-lock.json" "$stage/tools/acp/package-lock.json"
    printf '{"schema":"1","version":"%s","rid":"%s","nodeVersion":"%s"}\n' \
        "$version" "$rid" "$node_version" > "$stage/release-info.json"

    case "$rid" in
        win-x64 | win-arm64)
            archive="$out/huddle-$rid.zip"
            rm -f "$archive"
            make_zip "$stage" "$archive"
            ;;
        *)
            # macOS kills any executable whose name ends in .app (seen on macOS 27: even a copy of
            # /bin/echo named Echo.App dies with SIGKILL), and the apphost is named after the
            # assembly, Huddle.App. It finds Huddle.App.dll by its own embedded name, so renaming
            # the host is safe. install.sh and tests/smoke.sh expect app/HuddleApp.
            if [ -f "$stage/app/Huddle.App" ]; then
                mv "$stage/app/Huddle.App" "$stage/app/HuddleApp"
            fi
            # macOS needs the apphost executable; a publish from Windows does not set the mode bit.
            if [ -f "$stage/app/HuddleApp" ]; then
                chmod +x "$stage/app/HuddleApp"
            fi
            archive="$out/huddle-$rid.tar.gz"
            rm -f "$archive"
            # COPYFILE_DISABLE keeps macOS tar from adding AppleDouble ._ entries.
            COPYFILE_DISABLE=1 tar -czf "$archive" -C "$stage" app tools release-info.json
            ;;
    esac

    echo "built $archive"
}

[ -n "${HUDDLE_INSTALL_NO_MAIN:-}" ] || main "$@"
