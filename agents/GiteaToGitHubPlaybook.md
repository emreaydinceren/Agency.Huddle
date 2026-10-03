# Gitea → GitHub: the public mirror

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
| `.gitea/workflows/ci-pr.yaml`, `ci-main.yaml` | PR into `main`, push to `main` | The strict private gate. Unchanged by the mirror. |
| `.gitea/workflows/sync-github.yaml` | Manual only | Scrub, verify, scan, force-push. Any failing step sends nothing. |
| `.github/workflows/ci.yaml` | PR and push on GitHub | Restore, vulnerable-package check, build, test, health smoke. Proves a clean clone builds from public inputs. |

`ci.yaml` mirrors the Gitea `validate` job step for step. Keep them in lockstep: the SDK image
tag lives in **three** places (`ci-pr.yaml`, `ci-main.yaml`, `ci.yaml`, plus `sync-github.yaml`,
which only borrows the tag to reuse the cached image), and the two `--filter-not-method`
quarantines must match what `docs/engineering/known-limits.md` records.

## What the scrub removes

The rules are **repo secrets, never tracked files**, because they contain the real values:

| Secret | Holds |
| --- | --- |
| `SYNC_REPLACEMENTS` | `git-filter-repo --replace-text` lines (`needle==>placeholder`): internal Gitea and model-host names, the maintainer's local Windows user path, the maintainer's personal email |
| `SYNC_MAILMAP` | One mailmap line folding the personal author email into the GitHub noreply identity |
| `SYNC_GITHUB_TOKEN` | Fine-grained PAT, this one repo only, `Contents: Read and write` |

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
- **The verify step reads its needles from the runtime copy of the secret**, so this repo
  never contains them. Add a new needle to the secret and the verify step checks it
  automatically.
- **The `personal-email` rule in `.gitleaks.toml` embeds the address as an escaped regex.**
  The replacements file therefore needs the escaped form as a separate line, or the public
  copy of the rule would name the very address the scrub is hiding.
- **`.claude/settings.local.json` is stripped defensively.** It has never been committed, but
  it is the file that would hold machine-specific permissions.

## One-time setup

| Where | What |
| --- | --- |
| GitHub | An empty `Agency.Huddle` repo and the fine-grained PAT above |
| Gitea repo secrets | `SYNC_GITHUB_TOKEN`, `SYNC_MAILMAP`, `SYNC_REPLACEMENTS` |
| Gitea runner | The `dotnet-10` label, already used by `ci-pr.yaml` |
| Local clone | `git remote add github https://github.com/emreaydinceren/Agency.Huddle.git` |

## Dry-running a rule change locally

Do this on a throwaway clone **before** editing the secrets. It needs `git-filter-repo` and
`gitleaks` on PATH, takes about two seconds, and touches no remote.

```bash
git clone --no-local <path-to-this-repo> scrub-clone
cd scrub-clone
git filter-repo --force --path .claude/settings.local.json --invert-paths \
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
