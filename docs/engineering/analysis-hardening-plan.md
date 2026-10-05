# Hardening automated analysis: plan

Most code in this repo is written by agents, so the build and CI are the quality gate.
This page ranks the ways to make that gate harder to get past, and records which were
rejected and why. Written 2026-10-01.

The threat model is not a careless human. It is an agent that is told to make the build
pass and takes the cheapest route: a `#pragma warning disable`, a `NoWarn`, a loosened
`.editorconfig` severity, or a test that asserts nothing. Every item below either
stops that route, closes a rule that today lives only in prose, or catches what the
current analyzers cannot see.

What runs today: Roslyn compiler, `IDE` style rules (`EnforceCodeStyleInBuild`), `CA`
rules at `latest-recommended`, a curated SonarAnalyzer subset, and xUnit analyzers, all
as errors (`Directory.Build.props`); in CI, gitleaks and a vulnerable-package check;
locally, the `agents/scripts/Check-*.ps1` scripts.

> [!NOTE]
> `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` and `.gitea/**`
> are shared root files. Announce a change to any of them before making it
> ([CLAUDE.md](../../CLAUDE.md)). The two CI `validate` jobs stay byte-identical.

## Scales

- **Impact (1-5):** how much the item lowers the chance that bad agent code reaches
  `main` unnoticed. 5 closes a way around the whole gate; 1 is marginal.
- **Effort (1-5):** 1 is under an hour, 2 a few hours, 3 about a day including triage of
  what the new rule finds in existing code, 4 several days, 5 a week or more.
- **Score:** Impact divided by Effort. Rank is by score, ties broken by impact.
- **State:** `planned`, `in-progress` or `rejected`. Change it in the table and in the
  item's heading in the same edit.

## Ranking

| Rank | Item | Impact | Effort | Score | State |
| --- | --- | --- | --- | --- | --- |
| 1 | [Protect the guardrail files in Gitea](#1-protect-the-guardrail-files-in-gitea) | 5 | 1 | 5.00 | planned |
| 2 | [CI check for added suppressions and weakened config](#2-ci-check-for-added-suppressions-and-weakened-config) | 5 | 2 | 2.50 | planned |
| 3 | [Audit `.editorconfig`: fix defects, promote house rules](#3-audit-editorconfig-fix-defects-promote-house-rules) | 4 | 2 | 2.00 | planned |
| 4 | [Banned-API analyzer](#4-banned-api-analyzer) | 4 | 2 | 2.00 | planned |
| 5 | [Format check in CI](#5-format-check-in-ci) | 3 | 2 | 1.50 | planned |
| 6 | [Agent hooks: fast feedback and a Stop gate](#6-agent-hooks-fast-feedback-and-a-stop-gate) | 3 | 2 | 1.50 | planned |
| 7 | [Mutation testing with Stryker.NET](#7-mutation-testing-with-strykernet) | 4 | 4 | 1.00 | planned |
| 8 | [Semgrep rules for the prose house rules](#8-semgrep-rules-for-the-prose-house-rules) | 3 | 3 | 1.00 | planned |
| 9 | [Meziantou.Analyzer](#9-meziantouanalyzer) | 3 | 3 | 1.00 | planned |
| 10 | [Architecture tests](#10-architecture-tests) | 3 | 3 | 1.00 | planned |
| 11 | [Diff-coverage gate](#11-diff-coverage-gate) | 2 | 2 | 1.00 | planned |
| 12 | [Roslynator analyzers](#12-roslynator-analyzers) | 2 | 3 | 0.67 | planned |
| 13 | [Renovate for dependency updates](#13-renovate-for-dependency-updates) | 2 | 3 | 0.67 | planned |
| n/a | [CodeQL](#r1-codeql) | 5 | 3 | n/a | rejected |
| n/a | [StyleCop.Analyzers](#r2-stylecopanalyzers) | 2 | 4 | n/a | rejected |
| n/a | [SonarQube server](#r3-sonarqube-server) | 2 | 4 | n/a | rejected |
| n/a | [Trivy or OSV-Scanner](#r4-trivy-or-osv-scanner) | 2 | 2 | n/a | rejected |

**Suggested first slice:** items 1 to 5. Items 2 to 5 fit one PR; item 1 is a Gitea
setting and needs no code. Do 3 before 5, or the format check will report the same
drift twice.

---

## 1. Protect the guardrail files in Gitea

**State:** planned. **Impact 5, Effort 1.**

**What it is.** Branch protection on `main` that treats the files defining the gate as
protected, so a change to them cannot merge without a human decision.

**How it helps.** Everything else on this page is a rule file or a script an agent can
edit. This is the one control that sits outside the repo's own contents. Without it,
items 2 to 13 can be switched off in the same PR that breaks them.

**How it works.** Gitea branch protection has a *protected file patterns* field. A
change to a matching path is refused on direct push and needs the reviewers you
allow-list when it arrives by PR. Pair it with required status checks, so the
`validate` job must be green before merge.

**Implementation pointers.**

- Settings, Branches, rule for `main`: require the `validate` check, require approval.
- Patterns (semicolon-separated):
  `.editorconfig;Directory.Build.props;Directory.Packages.props;.gitleaks.toml;.gitea/**;BannedSymbols.txt;.claude/settings.json;agents/scripts/Check-*.ps1`
- Verify on the Gitea version in use how a PR touching a protected file is gated; do
  not assume the wording of the docs. Operational detail is in
  [GiteaOperations.md](../../agents/GiteaOperations.md).
- Needs the repository owner; an agent should not change its own branch protection.

## 2. CI check for added suppressions and weakened config

**State:** planned. **Impact 5, Effort 2.**

**What it is.** A script that reads the PR diff and fails when it adds a way around the
analyzers: `#pragma warning disable`, `[SuppressMessage]`, a `NoWarn`, a
`dotnet_diagnostic.*.severity` set to `none`, `silent` or `suggestion`, or a lowered
severity in `.editorconfig`.

**How it helps.** The analyzers decide what is wrong; this decides whether the agent
may overrule them. A suppression is sometimes right
([CSharpPrinciples.md](../../agents/CSharpPrinciples.md) allows one with a reason), so
the check does not forbid it. It makes it visible and forces a recorded reason, which
turns an invisible shortcut into a review item.

**How it works.** Diff `origin/main...HEAD` with no context lines, keep added lines, and
match them against a pattern list. A hit fails unless the line (or the one above it)
carries a justification, or the exact hit is listed in an allow-list file reviewed like
code.

**Implementation pointers.**

- New `agents/scripts/Check-Suppressions.ps1`, modelled on `Check-Eol.ps1` (strict mode,
  `$ErrorActionPreference = 'Stop'`).
- Allow-list pattern: copy the shape of `accepted-untested.example.txt`.
- Call it from `Check-All.ps1` and from both `.gitea/workflows/` `validate` jobs
  (change both or neither).
- Prove it can fail: commit a deliberate `#pragma warning disable` on a scratch branch
  and watch the check go red.

## 3. Audit `.editorconfig`: fix defects, promote house rules

**State:** planned. **Impact 4, Effort 2.**

**What it is.** A one-time audit that checks every house-style rule in
[CSharpPrinciples.md](../../agents/CSharpPrinciples.md) actually fails the build, then
fixes the ones that do not.

**How it helps.** A rule that does not fail the build is a suggestion to an agent, and
agents act on what the build tells them. Known defects in the current file:

- `csharp_style_unused_value_expression_statement_preference` ends in `silentrning`,
  which is not a valid severity, so that rule is probably ignored.
- Many rules carry no `:severity`, so they run at the rule's default, which for most
  style rules is hidden and does not fail the build. That includes the `this.`
  qualification rules.
- The `var` rules are `suggestion` and `IDE0008` is `none`, so "explicit type on the
  left" is not enforced.
- `csharp_style_var_when_type_is_apparent = true` may contradict the written rule
  (`StringBuilder x = new();`). Settle which is intended before raising severity.

**How it works.** For each rule, write the smallest violating snippet and build. If the
build stays green, the rule is not enforced: give it an explicit `:warning`. Then
decide, rule by rule, whether it should stay prose.

**Implementation pointers.**

- Fix the `silentrning` typo first; it is a one-word change.
- Raise severities in one commit, then count new violations with
  `dotnet build Huddle.slnx -p:TreatWarningsAsErrors=false` before deciding what to
  fix versus relax. Expect some churn in older files; `CLAUDE.md` says not to churn
  files only to move `using` placement.
- Keep the violating snippets as a checklist in the PR description, not as committed
  code.

## 4. Banned-API analyzer

**State:** planned. **Impact 4, Effort 2.**

**What it is.** `Microsoft.CodeAnalysis.BannedApiAnalyzers` reads a text file of
forbidden APIs and reports each use as a build diagnostic (`RS0030`).

**How it helps.** Several principles are "never call X": `DateTime.Now`,
`new Random()`, `.Result`, `.Wait()`, `Thread.Sleep`. Today they are advice. This makes
them a compile error with a message that tells the agent what to use instead.

**How it works.** `BannedSymbols.txt` lists documentation-ID entries, one per line,
with an optional message after a semicolon. The analyzer is added once and applies to
every project.

**Implementation pointers.**

- Add `<GlobalPackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" ... />`
  to `Directory.Packages.props`, next to the Sonar entry.
- Add `<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />` to
  `Directory.Build.props`.
- Starter entries: `P:System.DateTime.Now;Use TimeProvider`,
  `P:System.DateTime.UtcNow;Use TimeProvider`, `M:System.Random.#ctor;Inject a random source`,
  `M:System.Threading.Thread.Sleep(System.Int32);Await Task.Delay with the token`.
- Some uses are legitimate (the injected `TimeProvider` adapter, a test helper). Allow
  those with a single scoped `#pragma` and a reason, per the suppression rules; item 2
  then surfaces each one.

## 5. Format check in CI

**State:** planned. **Impact 3, Effort 2.**

**What it is.** `dotnet format --verify-no-changes` as a CI step.

**How it helps.** `Check-Eol.ps1` covers line endings only. This covers the rest of the
formatting rules (indentation, spacing, braces, `using` order) so formatting drift
cannot accumulate across many agent PRs.

**How it works.** The command applies `.editorconfig` in memory and exits non-zero if
any file would change.

**Implementation pointers.**

- Check that the pinned SDK (`global.json`) accepts `Huddle.slnx`; if not, loop over the
  projects.
- Land item 3 first, then do one formatting-only commit so the check starts green.
- Add it to `Check-All.ps1` and both `validate` jobs. Use `--no-restore` after the
  restore step.

## 6. Agent hooks: fast feedback and a Stop gate

**State:** planned. **Impact 3, Effort 2.**

**What it is.** Two Claude Code hooks in `.claude/settings.json`: a `PostToolUse` hook
that builds the project an edited `.cs` file belongs to, and a `Stop` hook that runs the
fast subset of `Check-All.ps1` before the agent may finish.

**How it helps.** It does not raise the bar; it makes the agent meet it sooner and
stops an agent declaring success with red checks. That saves review rounds and tokens.
It is ranked below the CI items because a hook is advisory: CI is the authority.

**How it works.** The existing `PreToolUse` hook re-states the style rules before an
edit. A `PostToolUse` hook returns the build's errors to the agent right after it. A
`Stop` hook exits non-zero when a check fails, which keeps the agent working.

**Implementation pointers.**

- Keep the `PostToolUse` build to one project with `--no-restore`; a whole-solution
  build per edit is too slow.
- The `Stop` hook should run only the cheap checks (`Check-Eol`, `Check-Suppressions`,
  `Check-DocIndex`, format verify). Do not run the Linux Docker repro
  ([CLAUDE.md](../../CLAUDE.md)).
- Put `.claude/settings.json` in the item 1 protected patterns.

## 7. Mutation testing with Stryker.NET

**State:** planned. **Impact 4, Effort 4.**

**What it is.** A tool that makes small deliberate breaks in the code and checks the
tests fail. A break the tests do not notice is a test that proves nothing.

**How it helps.** It is the only item here that judges test quality. Agents write tests
that pass and assert little; line coverage rewards that, mutation score does not. The
repo already does this by hand (`Prove-Mutation.ps1`); this automates it.

**How it works.** `dotnet stryker` mutates the source, runs the tests per mutant and
reports a score. With `--since:origin/main` it mutates only changed code, which keeps a
run to minutes. `--threshold-break` fails the run below a score.

**Implementation pointers.**

- Spike first: confirm Stryker.NET supports the xunit.v3 runner in use
  (Microsoft Testing Platform). If it does not, this item stops at the spike.
- Install as a local tool in `.config/dotnet-tools.json`.
- Run as a separate, optional-to-start CI job, not in `validate`, until the threshold
  is tuned. Start with a low break threshold and raise it.
- One `stryker-config.json` per test project (`tests/Huddle.Tests`,
  `tests/Huddle.Acp.Tests`).

## 8. Semgrep rules for the prose house rules

**State:** planned. **Impact 3, Effort 3.**

**What it is.** Semgrep (open source) with a small repo-owned rule set for conventions
that no Roslyn analyzer here checks.

**How it helps.** It closes the rules enforced only by a hook or review today. The
clearest case is "use `///` XML doc comments, never plain `//`, on types and methods",
which the compiler cannot check because `CS1591` is suppressed. It also adds a
security pass that works on Gitea, where CodeQL does not.

**How it works.** Rules are YAML patterns matched against syntax. `semgrep scan
--error` returns non-zero on a finding.

**Implementation pointers.**

- Rules in `.semgrep/` (protected in item 1). Start with two or three: a plain `//`
  comment directly above a member declaration, `catch (Exception)` with no log or
  comment, and the registry `p/csharp` pack for the security pass.
- Pin the Semgrep version in the workflow, as gitleaks is pinned.
- Expect false positives; make each rule's message say what to write instead.

## 9. Meziantou.Analyzer

**State:** planned. **Impact 3, Effort 3.**

**What it is.** A widely used third-party Roslyn analyzer package focused on API
misuse: culture, async, regex, exceptions, collections.

**How it helps.** It overlaps the `CA` and Sonar rules already on, but it catches
additional cases and gives each rule a precise message an agent can act on.

**How it works.** Same mechanism as Sonar here: a global package reference, rules
configured by ID in `.editorconfig`.

**Implementation pointers.**

- Add as a `GlobalPackageReference` in `Directory.Packages.props`.
- Triage before enforcing: build with `-p:TreatWarningsAsErrors=false`, group the output
  by rule ID, promote the useful rules to `warning`, set the rest to `none` in
  `.editorconfig` by ID. The Sonar setup in `Directory.Build.props` is the model, but
  prefer `.editorconfig` severities over a longer `NoWarn`.
- Budget the triage; it is most of the effort.

## 10. Architecture tests

**State:** planned. **Impact 3, Effort 3.**

**What it is.** Unit tests that assert the dependency rules between projects and
namespaces, using `NetArchTest.Rules` or `ArchUnitNET`.

**How it helps.** Agents add a reference wherever it compiles. These tests pin the
direction the design intends, so a convenient wrong dependency fails a test.

**How it works.** Tests load the assemblies and assert, for example, that one namespace
has no dependency on another.

**Implementation pointers.**

- Take the allowed directions from the docs, not from the code: the subtree split in
  [CLAUDE.md](../../CLAUDE.md) and [architecture.md](architecture.md). Do not encode
  today's accidental dependencies as rules.
- Put them in `tests/Huddle.Tests`; add the package to `Directory.Packages.props`.
- `agents/scripts/Check-Visibility.ps1` already guards the public type surface; do not
  duplicate it.

## 11. Diff-coverage gate

**State:** planned. **Impact 2, Effort 2.**

**What it is.** A CI gate on coverage of the *lines a PR changes*, not the global
percentage.

**How it helps.** It catches a new method added with no test at all. It is ranked low
because coverage is easy to satisfy with asserts that prove nothing; item 7 is the
real defence and this is the cheap complement.

**How it works.** Collect coverage during the test run, intersect with the PR diff,
fail below a threshold.

**Implementation pointers.**

- Pick the collector that matches the runner (xunit.v3 on Microsoft Testing Platform);
  check which is supported before choosing.
- Global coverage thresholds invite padding; gate on the diff only.

## 12. Roslynator analyzers

**State:** planned. **Impact 2, Effort 3.**

**What it is.** A large set of third-party analyzers (`RCS####`), mostly simplification
and style.

**How it helps.** It mostly overlaps `IDE` rules already on, so the gain is modest. A
handful of its correctness rules may be worth keeping.

**How it works.** Global package reference, with rules selected by ID.

**Implementation pointers.**

- Same triage as item 9, and expect most rules to be set to `none`.
- Do after item 9; if item 9 leaves no appetite, close this as rejected.

## 13. Renovate for dependency updates

**State:** planned. **Impact 2, Effort 3.**

**What it is.** A bot that opens PRs for newer package versions. Dependabot is
GitHub-only; Renovate supports Gitea.

**How it helps.** The CI vulnerable-package check goes red when an advisory lands, and
the fix is a version bump in `Directory.Packages.props`. Renovate makes that bump
arrive as a PR before the build goes red.

**How it works.** It runs on a schedule, reads `Directory.Packages.props`, and opens
one PR per update.

**Implementation pointers.**

- Needs a Gitea token for the bot and a place to run it (a scheduled workflow or the
  hosted app). Confirm with the repository owner before creating a token.
- Group minor and patch updates, and keep the SDK and MudBlazor on manual review.

---

## Rejected

### R1. CodeQL

**State:** rejected. **Impact 5, Effort 3.**

The strongest security scanner for C#, but it is built around GitHub Actions and code
scanning. `origin` is a self-hosted Gitea, so there is no supported way to run it in
CI. Semgrep (item 8) is the substitute. Revisit if the repository moves to GitHub.

### R2. StyleCop.Analyzers

**State:** rejected. **Impact 2, Effort 4.**

Its defaults conflict with this repo's style (for example `this.` and `using`
placement), so adopting it means turning off most of it and keeping a long override
list. The layout rules it would add are already covered by the `IDE` rules and the
format check (item 5).

### R3. SonarQube server

**State:** rejected. **Impact 2, Effort 4.**

The Sonar analyzers already run in every build. The server adds duplication tracking,
a dashboard and quality gates, at the cost of hosting and maintaining it. The gates
overlap what the build and items 2, 7 and 11 do directly. Revisit if a dashboard over
time becomes a requirement.

### R4. Trivy or OSV-Scanner

**State:** rejected for now. **Impact 2, Effort 2.**

`dotnet list package --vulnerable --include-transitive` already runs in CI and covers
the NuGet advisories. These tools add licence, container and infrastructure scanning
that this repo does not use. Revisit if the app is packaged as a container image.
