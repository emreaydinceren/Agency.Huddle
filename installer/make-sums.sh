#!/bin/sh

# Generate SHA256SUMS file for release artifacts
# Usage: make-sums.sh <dir> <file>...

set -e

if [ $# -lt 2 ]; then
    echo "Usage: make-sums.sh <dir> <file>..." >&2
    exit 1
fi

dir="$1"
shift

# Check all files exist before processing
for file in "$@"; do
    if [ ! -f "$dir/$file" ]; then
        echo "Error: $dir/$file does not exist" >&2
        exit 1
    fi
done

# Determine which hash tool to use
if command -v sha256sum >/dev/null 2>&1; then
    hash_cmd="sha256sum"
else
    hash_cmd="shasum -a 256"
fi

# Create temp files inside the target directory for atomic rename
temp_sums=$(mktemp "$dir/.SHA256SUMS.XXXXXX")
temp_sorted=$(mktemp "$dir/.SHA256SUMS.XXXXXX")
# shellcheck disable=SC2064 # Expanding now is intended: the temp file names are fixed here and the trap must remove exactly these two.
trap "rm -f '$temp_sums' '$temp_sorted'" EXIT

# Generate hashes, sorted by filename
for file in "$@"; do
    # Change to directory to compute hash with just filename
    hash_output=$(cd "$dir" && $hash_cmd "$file")
    # Extract hash (first field, 64 hex chars)
    hash=$(echo "$hash_output" | cut -d' ' -f1)
    # Write in the format: hash  filename (two spaces)
    echo "$hash  $file" >> "$temp_sums"
done

# Sort by filename (second column) in C locale for consistent results
LC_ALL=C sort -k2 < "$temp_sums" > "$temp_sorted"

# Write atomically with mv (same filesystem, atomic rename)
mv "$temp_sorted" "$dir/SHA256SUMS"
