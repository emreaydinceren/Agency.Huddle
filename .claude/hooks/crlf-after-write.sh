#!/usr/bin/env bash
# PostToolUse hook: the Write tool creates files with LF endings, but this repo's
# working copy is CRLF (.editorconfig). Rewrites the file just written to CRLF for
# text types. Edit preserves a file's existing endings, so only Write is hooked.
#
# Git already normalises on commit (core.autocrlf=true in Git for Windows' system
# config, and every index blob is LF), so this is working-copy hygiene: no mixed
# endings, no "LF will be replaced by CRLF" warnings.
#
# One process at most: under Git Bash each external process costs seconds under load,
# so the payload is parsed with bash builtins and a single perl both checks and
# rewrites. $(cat) reads the payload in blocks ($(</dev/stdin) fails in the harness's
# Git Bash); `read -d ''` would read a large Write payload one byte at a time.
set -uo pipefail

payload=$(cat)

# The payload is JSON: Windows paths arrive with escaped backslashes (E:\\Repos\\...).
# The first "file_path" is the tool's target; a later one could sit inside the content.
[[ $payload =~ \"file_path\"[[:space:]]*:[[:space:]]*\"([^\"]*)\" ]] || exit 0
path=${BASH_REMATCH[1]}
path=${path//\\\\/\\}

[[ -n $path ]] || exit 0

# The Write tool has been seen to report success and leave no file behind; a later script
# then overwrote the missing spec with an empty one. Say so while the agent can still act.
if [[ ! -f $path ]]; then
    printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"Write reported success but %s does not exist on disk. Write it again (or use PowerShell Set-Content) and confirm with ls before relying on it."}}\n' \
        "${path##*[\\/]}"
    exit 0
fi

case "${path,,}" in
    *.sh) exit 0 ;;  # bash rejects CRLF scripts; they stay LF
    *.cs|*.csx|*.razor|*.cshtml|*.csproj|*.props|*.targets|*.slnx|*.md|*.json|*.css|*.js|*.ps1|*.yaml|*.yml|*.txt|*.toml) ;;
    *) exit 0 ;;
esac

# Exits 1 when there is no bare LF (already CRLF, or no newlines at all) and nothing changed.
# Idempotent; keeps a BOM; never adds a final newline the file did not have.
perl -e '
    my $p = shift;
    open(my $in, "<:raw", $p) or exit 1;
    local $/; my $s = <$in>; close $in;
    exit 1 unless $s =~ /(?<!\r)\n/;
    $s =~ s/\r?\n/\r\n/g;
    open(my $out, ">:raw", $p) or exit 1;
    print $out $s; close $out or exit 1;
' "$path" || exit 0

name=${path##*[\\/]}
printf '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"The CRLF hook converted %s to CRLF on disk after this Write. Re-Read it before your next Edit of it."}}\n' \
    "$name"
