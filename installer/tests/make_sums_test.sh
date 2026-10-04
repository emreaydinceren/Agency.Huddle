#!/bin/sh

# Test: make-sums.sh

set -e

# Test 1: Basic functionality with two files
test_dir=$(mktemp -d)
trap "rm -rf '$test_dir'" EXIT

# Create test files
printf 'abc' > "$test_dir/a.txt"
touch "$test_dir/b.txt"

# Run make-sums.sh
sh installer/make-sums.sh "$test_dir" a.txt b.txt

# Verify SHA256SUMS exists
if [ ! -f "$test_dir/SHA256SUMS" ]; then
    echo "FAIL: SHA256SUMS not created"
    exit 1
fi

# Expected hashes (computed with printf 'abc' | sha256sum and echo -n '' | sha256sum)
expected_a="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
expected_b="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"

# Verify content line by line
line_a=$(grep 'a.txt$' "$test_dir/SHA256SUMS" || echo "")
line_b=$(grep 'b.txt$' "$test_dir/SHA256SUMS" || echo "")

if [ -z "$line_a" ]; then
    echo "FAIL: a.txt line not found in SHA256SUMS"
    exit 1
fi

if [ -z "$line_b" ]; then
    echo "FAIL: b.txt line not found in SHA256SUMS"
    exit 1
fi

# Check exact format (hash, two spaces, filename)
expected_line_a="$expected_a  a.txt"
expected_line_b="$expected_b  b.txt"

if [ "$line_a" != "$expected_line_a" ]; then
    echo "FAIL: a.txt line mismatch"
    echo "Expected: $expected_line_a"
    echo "Got:      $line_a"
    exit 1
fi

if [ "$line_b" != "$expected_line_b" ]; then
    echo "FAIL: b.txt line mismatch"
    echo "Expected: $expected_line_b"
    echo "Got:      $line_b"
    exit 1
fi

# Verify lines are sorted by name (a.txt before b.txt)
if ! head -1 "$test_dir/SHA256SUMS" | grep -q 'a.txt$'; then
    echo "FAIL: Files not sorted (a.txt should come first)"
    exit 1
fi

# Test 2: Missing file argument should exit non-zero and not create SHA256SUMS
test_dir2=$(mktemp -d)
trap "rm -rf '$test_dir' '$test_dir2'" EXIT

if sh installer/make-sums.sh "$test_dir2" 2>/dev/null; then
    echo "FAIL: make-sums.sh should exit non-zero with missing file argument"
    exit 1
fi

if [ -f "$test_dir2/SHA256SUMS" ]; then
    echo "FAIL: SHA256SUMS should not be created when missing file"
    exit 1
fi

# Test 3: C-locale sorting (uppercase before lowercase)
test_dir3=$(mktemp -d)
trap "rm -rf '$test_dir' '$test_dir2' '$test_dir3'" EXIT

# Create files with mixed case names
touch "$test_dir3/c.txt"
touch "$test_dir3/a.txt"
touch "$test_dir3/B.txt"

# Run make-sums.sh
sh installer/make-sums.sh "$test_dir3" c.txt a.txt B.txt

# In C locale, uppercase comes before lowercase, so order should be: B.txt, a.txt, c.txt
if ! head -1 "$test_dir3/SHA256SUMS" | grep -q 'B.txt$'; then
    echo "FAIL: C-locale sort failed (B.txt should come first)"
    exit 1
fi

if ! tail -1 "$test_dir3/SHA256SUMS" | grep -q 'c.txt$'; then
    echo "FAIL: C-locale sort failed (c.txt should come last)"
    exit 1
fi

echo "PASS: All tests passed"
