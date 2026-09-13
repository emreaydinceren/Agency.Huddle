# Working With the Remote (Gitea Home Lab)

`origin` for this repo is a self-hosted Gitea instance, **not GitHub**. GitHub, if this repo
is ever mirrored there, would be a one-way push target only — never open PRs or trigger
Actions there. Everything below talks to the Gitea instance directly via its REST API
(`/api/v1/...`), since `gh` (GitHub CLI) does not work against Gitea.

> [!WARNING]
> Do not write the real internal hostname into a tracked file. `.gitleaks.toml`'s
> `internal-mdns-host` rule fails `secret-scan` on any `*.local` name it finds, and this
> repo's own [CIPipeline.md](CIPipeline.md#topology) already hit that. `gitea-host.example`
> below is a placeholder — read the real one from `git remote get-url origin` each time,
> the same way the snippets do.

- **Owner/repo:** `emre/Huddle`
- **Auth:** `GITEA_ACCESS_TOKEN`, a Windows **User**-scope environment variable. Send it as
  `Authorization: token <TOKEN>` (not `Bearer`). Full detail — why it's an env var and not
  `git config gitea.token`, and the caution against ever writing the value to a file, commit,
  or log line — is in [CIPipeline.md § Reading CI results from a
  session](CIPipeline.md#reading-ci-results-from-a-session); this doc doesn't repeat it.
- **CLI alternative:** Gitea's official `tea` CLI is the closest analog to `gh`, but is **not
  installed** on this machine as of 2026-09-13 (`tea` not on PATH). Check with `tea --version`
  before assuming otherwise — if present, prefer it over hand-rolled API calls for anything it
  supports (`tea pr create`, `tea pr list`, `tea login`, etc.).
- **CI/Actions internals** (workflow topology, failure modes, backing services, reading a run's
  log) live in [CIPipeline.md](CIPipeline.md) — this doc only covers *talking to the remote*:
  branches, pull requests, and the couple of Actions calls that touch a run rather than debug
  one.

## Known issue: this repo's `origin` points at a stale host

As of 2026-09-13, `git remote -v` in this repo resolves to a `*.local` mDNS name that no
longer resolves on this network — the sibling Agency repo's remote was already moved to the
`.home` static DNS entry that replaced it (see `E:\Repos\Agency\Agents\GiteaOperations.md` for
that history). Symptom here would match the one below: `git push`/`git fetch` hangs or fails
DNS resolution outright. If you hit that, confirm with `git remote get-url origin` and ask
before running `git remote set-url origin ...` — it's a one-line fix, but it's shared repo
config, not something to silently rewrite.

## Connectivity / auth troubleshooting

Git credentials are a Gitea PAT cached **per-hostname** in Windows Credential Manager — a
cached cred for one host name doesn't cover another spelling of the same machine (e.g. an old
mDNS name vs. its DNS replacement) and vice versa. Symptom: `git push` hangs for 2+ minutes
(waiting on a credential prompt that never surfaces) or fails fast with
`remote: Failed to authenticate user`.

**Fix — bypass the credential manager by injecting the token as a header:**

```bash
git -c http.extraHeader="Authorization: token ${GITEA_ACCESS_TOKEN}" push -u origin <branch>
```

If DNS itself is down:

1. Resolve manually: `Resolve-DnsName <host> -Type A` (PowerShell), where `<host>` is whatever
   `git remote get-url origin` reports.
2. Push with an explicit IP: `git push http://emre:<token>@<ip>:3000/emre/Huddle.git <branch>:<branch>`
3. API calls against the IP need an explicit `Host` header: `Invoke-RestMethod http://<ip>:3000/api/v1/...`
   with `Authorization: token <token>` and `Host: <host>:3000`.

Retry once on a transient auth/DNS failure before reporting it as broken — this has been flaky
rather than truly down often enough to be worth one retry.

## Branches

Standard `git push`/`git fetch` against `origin` once auth is working (see above). To list or
delete a remote branch via the API instead of git, derive the host the same way
[CIPipeline.md](CIPipeline.md#reading-ci-results-from-a-session) does rather than hardcoding it:

```bash
REMOTE=$(git remote get-url origin)          # http://<host>/emre/Huddle.git
BASE=${REMOTE%.git}
API="$(echo "$BASE" | cut -d/ -f1-3)/api/v1/repos/emre/Huddle"

curl "$API/branches" -H "Authorization: token ${GITEA_ACCESS_TOKEN}"

curl -X DELETE "$API/branches/<branch-name>" -H "Authorization: token ${GITEA_ACCESS_TOKEN}"
```

## Pull Requests

```powershell
$remote = git remote get-url origin                 # http://<host>/emre/Huddle.git
$base   = $remote -replace '\.git$', ''
$api    = ($base -split '/')[0..2] -join '/'         # http://<host>
$api    = "$api/api/v1/repos/emre/Huddle"

$body = @{
    title = "feat(huddle): ..."
    head  = "feat/some-branch"     # source branch
    base  = "main"                 # target branch
    body  = "PR description..."
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "$api/pulls" `
    -Method Post `
    -Headers @{ Authorization = "token $env:GITEA_ACCESS_TOKEN" } `
    -ContentType "application/json" `
    -Body $body
```

Bash/curl equivalent, reusing the `$API` derived in [Branches](#branches) above:

```bash
curl -X POST "$API/pulls" \
  -H "Authorization: token ${GITEA_ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"title":"...", "head":"feat/some-branch", "base":"main", "body":"..."}'
```

The branch must already be pushed to `origin` before opening the PR. If neither the API nor
`tea` works, push the branch and hand the user the compare URL to open manually — build it from
the same `$base` above: `$base/compare/main...feat/some-branch`.

## Actions (workflow runs)

For re-running a job or checking a run's status without the web UI, use the endpoints already
documented in [CIPipeline.md § Reading CI results from a
session](CIPipeline.md#reading-ci-results-from-a-session) — that page owns the full endpoint
table and the three API traps already hit (`/actions/tasks` returning an empty list,
`/rerun` on a run that isn't done, and a run reporting `in_progress` while the failure is
already in its log). Come here only for branches and PRs; go there for anything Actions-shaped.

## Validation status

The environment facts above (`tea` absence, the per-hostname credential caching behavior, the
gitleaks `internal-mdns-host` rule) are confirmed as of 2026-09-13. **The API request shapes
(PR creation, branch delete, Actions rerun) are documented from Gitea's published REST API
conventions but have not been exercised end-to-end against this instance from within this
repo** — creating a real PR/deleting a branch/re-running a job are visible, non-trivial-to-undo
actions, so they weren't tested just to validate this doc. Treat the request bodies as a strong
starting point, not a guarantee; if one 4xxs, check the response body for the actual Gitea
version's field names before assuming the whole approach is wrong.

## Related

- [CIPipeline.md](CIPipeline.md) — CI/Actions internals: workflow topology, failure modes, and
  the full Actions API reference this doc points to above
- [C# Principles](CSharpPrinciples.md) — the house style the build enforces
