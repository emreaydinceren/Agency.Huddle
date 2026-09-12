#!/usr/bin/env bash
# PreToolUse hook: when Claude writes or edits a C# file in this repo, inject a
# reminder to follow agents/CSharpPrinciples.md.
#
# Reads the tool-call payload on stdin and emits PreToolUse additionalContext
# only for C# source files. Uses grep rather than jq (jq is not installed here).
set -uo pipefail

payload=$(cat)

# Capture the match first rather than piping straight into `grep -q`: under
# `pipefail`, the early exit of `grep -q` can SIGPIPE the upstream grep and
# report failure for a payload that actually matched.
target=$(printf '%s' "$payload" | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -n 1)

printf '%s' "$target" | grep -qiE '\.(cs|csx|razor|cshtml)"$' || exit 0

cat <<'JSON'
{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":"C# file change in this repo: agents/CSharpPrinciples.md is mandatory. Read agents/CSharpPrinciples.md now if you have not already this session, and check this edit against it - XML doc comments (///) on every class and method including tests; record for data, sealed class for behaviour; immutable by default (init-only, readonly, IReadOnlyList<T> on public APIs); constructor injection only, inject TimeProvider and other ambient dependencies; make illegal states unrepresentable; Result<T> for expected failure, exceptions only for the exceptional; no async void, no .Result/.Wait(), CancellationToken on anything that waits; never catch Exception; materialise LINQ at boundaries; no yield return inside try-catch; package versions live only in Directory.Build.props. Warnings are errors and nullable is enabled, so run dotnet build before declaring success."}}
JSON
