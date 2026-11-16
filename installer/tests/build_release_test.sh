#!/bin/sh
# Pins the archive layout that the installers depend on (plan Spec 2 and 3).
# Plain checks only: the shared lib.sh does not exist yet (task 28.t).
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../.." && pwd)
SCRIPT="$ROOT/installer/build-release.sh"
NODE_VERSION=$(tr -d '\r\n ' < "$ROOT/installer/node-version.txt")

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT INT TERM

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

# Finds a tar that can read and write zip (bsdtar): prints its path, or nothing.
find_bsdtar() {
    for candidate in bsdtar tar /c/Windows/System32/tar.exe; do
        if command -v "$candidate" > /dev/null 2>&1 && "$candidate" --version 2> /dev/null | grep -qi bsdtar; then
            command -v "$candidate"
            return 0
        fi
    done
    return 0
}

# Prints the entry names of a zip, one per line, without a leading "./".
list_zip() {
    if command -v unzip > /dev/null 2>&1; then
        unzip -Z1 "$1" | sed 's|^\./||'
    else
        bsd=$(find_bsdtar)
        [ -n "$bsd" ] || fail "no unzip or bsdtar available to list $1"
        "$bsd" -tf "$1" | sed 's|^\./||'
    fi
}

# Prints the entry names of a tar.gz, one per line, without a leading "./".
list_targz() {
    tar -tzf "$1" | sed 's|^\./||'
}

has_entry() {
    # $1 = listing, $2 = exact entry name
    printf '%s\n' "$1" | grep -qx "$2"
}

# A fake publish directory standing in for 'dotnet publish' output.
mkdir -p "$TMP/fake/wwwroot"
echo host > "$TMP/fake/Huddle.App.exe"
echo dll > "$TMP/fake/Huddle.App.dll"
echo x > "$TMP/fake/wwwroot/x.txt"

# The macOS fake is a separate directory: on Windows (MSYS) 'Huddle.App' and 'Huddle.App.exe'
# resolve to the same file, so they cannot sit side by side.
mkdir -p "$TMP/fake-mac/wwwroot"
echo host > "$TMP/fake-mac/Huddle.App"
echo dll > "$TMP/fake-mac/Huddle.App.dll"
echo x > "$TMP/fake-mac/wwwroot/x.txt"

# --- win-x64 -> zip ---------------------------------------------------------
OUT="$TMP/out"
sh "$SCRIPT" --version 1.2.3 --rid win-x64 --publish-dir "$TMP/fake" --out "$OUT" > "$TMP/win.log" 2>&1 \
    || { cat "$TMP/win.log" >&2; fail "build-release.sh --rid win-x64 exited non-zero"; }

ZIP="$OUT/huddle-win-x64.zip"
[ -f "$ZIP" ] || fail "missing $ZIP"
LISTING=$(list_zip "$ZIP")
for entry in app/Huddle.App.exe app/Huddle.App.dll app/wwwroot/x.txt \
    tools/acp/package.json tools/acp/package-lock.json release-info.json; do
    has_entry "$LISTING" "$entry" || fail "zip does not list $entry"
done

# release-info.json is read from the zip by extracting it with bsdtar or unzip.
mkdir -p "$TMP/win-x"
if command -v unzip > /dev/null 2>&1; then
    unzip -q "$ZIP" -d "$TMP/win-x"
else
    bsd=$(find_bsdtar)
    "$bsd" -xf "$ZIP" -C "$TMP/win-x"
fi
INFO=$(cat "$TMP/win-x/release-info.json")
case "$INFO" in
    *'"version":"1.2.3"'*) ;;
    *) fail "release-info.json lacks version 1.2.3: $INFO" ;;
esac
case "$INFO" in
    *'"rid":"win-x64"'*) ;;
    *) fail "release-info.json lacks rid win-x64: $INFO" ;;
esac
case "$INFO" in
    *"\"nodeVersion\":\"$NODE_VERSION\""*) ;;
    *) fail "release-info.json lacks nodeVersion $NODE_VERSION: $INFO" ;;
esac
case "$INFO" in
    *'"schema":"1"'*) ;;
    *) fail "release-info.json lacks schema 1: $INFO" ;;
esac
cmp -s "$ROOT/tools/acp/package.json" "$TMP/win-x/tools/acp/package.json" \
    || fail "packaged package.json differs from tools/acp/package.json"
cmp -s "$ROOT/tools/acp/package-lock.json" "$TMP/win-x/tools/acp/package-lock.json" \
    || fail "packaged package-lock.json differs from tools/acp/package-lock.json"

# --- osx-arm64 -> tar.gz ----------------------------------------------------
sh "$SCRIPT" --version 1.2.3 --rid osx-arm64 --publish-dir "$TMP/fake-mac" --out "$OUT" > "$TMP/osx.log" 2>&1 \
    || { cat "$TMP/osx.log" >&2; fail "build-release.sh --rid osx-arm64 exited non-zero"; }

TGZ="$OUT/huddle-osx-arm64.tar.gz"
[ -f "$TGZ" ] || fail "missing $TGZ"
LISTING=$(list_targz "$TGZ")
for entry in app/HuddleApp app/Huddle.App.dll app/wwwroot/x.txt \
    tools/acp/package.json tools/acp/package-lock.json release-info.json; do
    has_entry "$LISTING" "$entry" || fail "tar.gz does not list $entry"
done
# macOS kills an executable whose name ends in .app, so the host must not ship as app/Huddle.App.
if has_entry "$LISTING" app/Huddle.App; then
    fail "tar.gz still ships the host as app/Huddle.App, which macOS kills on launch"
fi
mkdir -p "$TMP/osx-x"
tar -xzf "$TGZ" -C "$TMP/osx-x"
INFO=$(cat "$TMP/osx-x/release-info.json")
case "$INFO" in
    *'"rid":"osx-arm64"'*) ;;
    *) fail "osx release-info.json lacks rid osx-arm64: $INFO" ;;
esac
case "$INFO" in
    *"\"nodeVersion\":\"$NODE_VERSION\""*) ;;
    *) fail "osx release-info.json lacks nodeVersion $NODE_VERSION: $INFO" ;;
esac

# --- osx-x64 -> tar.gz ------------------------------------------------------
sh "$SCRIPT" --version 1.2.3 --rid osx-x64 --publish-dir "$TMP/fake-mac" --out "$OUT" > "$TMP/osx64.log" 2>&1 \
    || { cat "$TMP/osx64.log" >&2; fail "build-release.sh --rid osx-x64 exited non-zero"; }
[ -f "$OUT/huddle-osx-x64.tar.gz" ] || fail "missing $OUT/huddle-osx-x64.tar.gz"

# --- argument errors --------------------------------------------------------
if sh "$SCRIPT" --version 1.2.3 --rid linux-x64 --publish-dir "$TMP/fake" --out "$TMP/bad" > /dev/null 2>&1; then
    fail "--rid linux-x64 should exit non-zero"
fi
[ ! -e "$TMP/bad/huddle-linux-x64.tar.gz" ] || fail "--rid linux-x64 produced an archive"

if sh "$SCRIPT" --version 1.2.3 --rid win-x64 --publish-dir "$TMP/fake" > /dev/null 2>&1; then
    fail "a missing --out should exit non-zero"
fi

echo "PASS: build_release_test.sh"
