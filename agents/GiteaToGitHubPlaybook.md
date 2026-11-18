# Gitea → GitHub: the public mirror

## Contents

- The model
- The three workflows
- What the scrub removes
- One-time setup
- Dry-running a rule change locally
- Taking a GitHub pull request
- Not part of this mirror

How Agency.Huddle reaches `github.com/emreaydinceren/Agency.Huddle`, and how a GitHub pull
request gets back in. Ported from the Agency.NET playbook, with everything NuGet-shaped
removed: Huddle publishes no packages, so there is no release workflow, no environment
approval and no trusted-publishing policy.

Read this before touching `.gitea/workflows/sync-github.yaml`, `.github/workflows/ci.yaml`,
or `.gitleaks.toml`.

## The model

- **Gitea `main` is the source of truth.** GitHub `main` is a scrubbed, write-only mirror.
- **Nothing is ever merged on GitHub.** Never click Merge there. A GitHub PR is fetched,
  validated on Gitea and merged into Gitea `main`; the next sync carries it across.
- **Gitea's history is never rewritten.** Only a disposable clone inside the workflow is.

```text
Gitea main --(manual workflow_dispatch: sync-github.yaml)--> disposable clone
    clone -> filter-repo scrub -> verify (fails closed) -> gitleaks (history + tree)
        -> force-push -> GitHub main --> GitHub CI (ci.yaml)
```

Gitea's built-in push mirror is not used: it is all-refs with no pre-push guard and no
filtering, so an internal hostname in an old commit message would ship the moment it
slipped through review.

## The three workflows

| File | Runs | Job |
| --- | --- | --- |
| `.gitea/workflows/ci-pr-local.yaml`, `ci-main-local.yaml` (both call `ci-shared.yaml`) | PR into `main`, push to `main` | The strict private gate. Unchanged by the mirror. |
| `.gitea/workflows/sync-github.yaml` | Manual only | Scrub, verify, scan, force-push. Any failing step sends nothing. |
| `.github/workflows/ci.yaml` | PR and push on GitHub | Restore, vulnerable-package check, build, test, health smoke. Proves a clean clone builds from public inputs. |

`ci.yaml` mirrors the Gitea `validate` job step for step. Keep them in lockstep: the SDK image
tag lives in **three** places (`ci-shared.yaml`, which holds it twice, `ci.yaml`, plus `sync-github.yaml`,
which only borrows the tag to reuse the cached image), and the two `--filter-not-method`
quarantines must match what `docs/engineering/known-limits.md` records.

## What the scrub removes

The rules are **repo secrets, never tracked files**, because they contain the real values:

| Secret | Holds |
| --- | --- |
| `SYNC_REPLACEMENTS` | `git-filter-repo --replace-text` lines (`needle==>placeholder`): internal Gitea and model-host names, the maintainer's local Windows user path, the maintainer's personal email |
| `SYNC_MAILMAP` | One mailmap line folding the personal author email into the GitHub noreply identity |
| `SYNC_GITHUB_TOKEN` | Fine-grained PAT, this one repo only, `Contents` and `Workflows`: Read and write. GitHub rejects a push that changes `.github/workflows/` without `Workflows`. Fine-grained tokens expire (at most a year); the secret's description in Gitea records the date |

Placeholders use reserved `.example` hosts, which `.gitleaks.toml` already allowlists. The
working tree is written to be redaction-safe on its own (docs say `gitea-host.example`), so
most of what the scrub fixes lives in **history**: `Reviewed-on:` trailers in merge-commit
messages, and old revisions of docs.

### Traps

- **Needles must be specific.** The GitHub username `emreaydinceren` contains the substring of
  the local Windows username. A bare short needle would mangle the profile links in the docs
  and the noreply identity. Use full path forms (`Users\<name>`), never the bare name.
- **A tree scan is not enough.** The internal-hostname findings were in old revisions that no
  longer exist in the tree. The sync runs `gitleaks git` over the rewritten history as well as
  `gitleaks dir` over the tree.
- **CI scans only what a PR changes; the sync scans all of history.** A fake value a PR later
  replaced (`a@b.com`, `probe@example.test`, a schema id on an invented mDNS host) still fails the
  next sync. Run 1649, the first after a six-week gap, stopped on ten of them. When a finding is
  a fake, allowlist that exact value in `.gitleaks.toml`, or the whole line with
  `regexTarget = "line"`. Never a file or a whole domain. Reproduce first with
  `gitleaks git . --config .gitleaks.toml --log-opts="--all -- <files>"`.
- **An expired token fails only at the very end.** Clone, scrub, verify and both scans all pass,
  then the push says `Invalid username or token. Password authentication is not supported`. That
  is `SYNC_GITHUB_TOKEN` expired or revoked, not a scrub problem. Run 1653 failed this way on
  2026-11-16: the token from 2026-10-03 had lapsed, and its replacement expires on 2027-11-15.
  Create a new one as in the table above and update the secret on the repo page.
- **The verify step reads its needles from the runtime copy of the secret**, so this repo
  never contains them. Add a new needle to the secret and the verify step checks it
  automatically.
- **The `personal-email` rule in `.gitleaks.toml` embeds the address as an escaped regex.**
  The replacements file therefore needs the escaped form as a separate line, or the public
  copy of the rule would name the very address the scrub is hiding.
- **`private/` never ships.** The maintainer's history lives there: the PR logs and their
  classification (`private/feature-logs/`), and plans that carry run costs or
  owner gates. The sync strips the folder from every revision, along with the paths those files
  had before they moved (listed in `sync-github.yaml`), and the verify step fails if any of them
  survives. A public page may name a `private/` path in backticks, but must never link to it:
  the link would be broken on GitHub. To keep a new file off the mirror, put it under
  `private/`. If it ever lived somewhere else, add that old path to both lists in
  `sync-github.yaml`. One page stays under `docs/` and off the mirror: the update guide,
  `docs/FeaturesSummary/UPDATING.md`. The sync strips it by path, and `Check-DocIndex.ps1` exempts it, so
  `Index.md` names it in backticks and never links it.
- **Stripping a path does not scrub text.** A line that leaked into a kept page stays in that
  page's old revisions. Fix the page, and if the old revisions matter, add a needle to
  `SYNC_REPLACEMENTS`.
- **`.claude/settings.local.json` is stripped defensively.** It has never been committed, but
  it is the file that would hold machine-specific permissions.
- **JSON-escaped Windows paths need their own rule.** A path inside a JSON string appears with
  doubled backslashes (`Users\\<name>`), which a single-backslash needle does not match. Typing
  a doubled backslash into a shell or an editing tool can silently collapse it to one, leaving
  a duplicate line and an unscrubbed form. Cover it with a `regex:` line that needs no
  backslash, such as `regex:Users[^A-Za-z0-9]{1,2}<name>==>Users/user`, and dry-run it.
- **Add the secrets on the repo page, not under user settings.** A secret saved at user level
  still reaches this workflow, but it reaches every repo that user owns. The repo page lists
  `SYNC_*` when they are in the right place.
- **The first sync into an existing GitHub repo may start no CI.** A repo created with a
  licence file is not empty, and the first force-push replaces its commit. GitHub can show
  "no workflow runs" afterwards. Push one new commit to Gitea `main` and sync again.

## One-time setup

| Where | What |
| --- | --- |
| GitHub | An empty `Agency.Huddle` repo and the fine-grained PAT above |
| Gitea repo secrets | `SYNC_GITHUB_TOKEN`, `SYNC_MAILMAP`, `SYNC_REPLACEMENTS` |
| Gitea runner | The `dotnet-10-local` label (the Windows desktop runner), the same as CI. Until 2026-11-16 it named the Mac mini's retired `dotnet-10` runner, and run 1642 sat queued with no runner to take it |
| Local clone | `git remote add github https://github.com/emreaydinceren/Agency.Huddle.git` |

## Dry-running a rule change locally

Do this on a throwaway clone **before** editing the secrets. It needs `git-filter-repo` and
`gitleaks` on PATH, takes about two seconds, and touches no remote.

```bash
git clone --no-local <path-to-this-repo> scrub-clone
cd scrub-clone
git filter-repo --force --path .claude/settings.local.json --path private/ \
  --path docs/FeaturesSummary/PRs.md --path docs/FeaturesSummary/Prs-Feature.md \
  --path docs/FeaturesSummary/UPDATING.md --path docs/FeaturesSummary/pr-classification.csv \
  --path docs/Huddle.SecondBrainZeroFixes-ProjectPlan.md --invert-paths \
  --replace-text ../replacements.txt --replace-message ../replacements.txt \
  --mailmap ../mailmap.txt
gitleaks git . --config .gitleaks.toml --redact --no-banner --log-opts="--all"
gitleaks dir . --config .gitleaks.toml --redact --no-banner
```

Then confirm each needle returns nothing from `git log --all -S "<needle>"` and
`git log --all --fixed-strings --grep="<needle>"`, and that
`git log --all --format='%an <%ae>' | sort -u` lists exactly one identity.

## Taking a GitHub pull request

1. **Fetch it at its read-only ref** and check it out on a throwaway branch:

   ```bash
   git fetch github refs/pull/<n>/head
   git switch -c pr-<n> FETCH_HEAD
   ```

2. **Validate on Gitea**: `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --`, plus the
   `Check-*` scripts for anything under `docs/`.
3. **Needs changes?** Comment on the GitHub PR. It stays open; when the contributor pushes,
   `refs/pull/<n>/head` updates, so re-run step 1.
4. **Merge with `--no-ff`** into Gitea `main`, push, then run `sync-github` from the Gitea
   Actions tab.
5. **Close the GitHub PR by hand** with a link to the merge. The sync rewrites every
   descendant SHA, so GitHub cannot see the contributor's commits arrive and will not mark
   the PR merged on its own. (Unverified against a real external PR; if it does auto-close,
   delete this step.)

## Not part of this mirror

No NuGet publish, SBOM, Codecov, DocFX or GitHub Pages. If Huddle ever ships a package, port
`release.yaml` from the Agency.NET playbook and add the trusted-publishing policy then.
