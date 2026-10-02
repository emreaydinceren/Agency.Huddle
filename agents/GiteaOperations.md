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
  branches, pull requests, issues, and the couple of Actions calls that touch a run rather than
  debug one.

## Resolved 2026-09-15: `origin` was moved off the stale mDNS host

Until 2026-09-15 this repo's `origin` pointed at a `*.local` mDNS name that no longer resolved
on this network, while the sibling Agency repo had already been moved to the `.home` static DNS
entry that replaced it (see `E:\Repos\Agency\Agents\GiteaOperations.md` for that history). It
was moved at the repo owner's explicit request, keeping the same scheme, port and path and
changing only the host, and verified with `git ls-remote --heads origin` before anything was
pushed. Both repos now agree.

Two things about that change are worth keeping:

- **It is shared repo config.** Ask before running `git remote set-url origin ...`; it is a
  one-line fix, but it is not something to silently rewrite. That bar was met here.
- **The real host stayed out of every tracked file**, this page included — which is why the
  paragraph above says `.home` and not the name itself. Read the real one from
  `git remote get-url origin`, the way the snippets below do.

Linked worktrees under `.claude/worktrees/` share the main repo's `.git`, so they inherited the
new URL with no separate change. If `git push`/`git fetch` ever hangs or fails DNS resolution
again, the symptom and the fix are in [Connectivity / auth troubleshooting](#connectivity--auth-troubleshooting)
directly below — and note that git credentials are cached **per hostname**, so the first push
after a host change may need the `http.extraHeader` bypass even though the old name worked fine.

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

A successful delete returns **204** with an empty body, so check the status code rather than the
output — `curl -s -o /dev/null -w "%{http_code}"` is enough.

**Deleting a branch is recoverable, but only if you wrote the sha down first.** The commits
survive until git garbage-collects them, and nothing in the Gitea UI will tell you what the tip
was afterwards. Record it before deleting, and recreate with an ordinary push:

```bash
git rev-parse "origin/<branch-name>"                    # BEFORE deleting
git push origin <sha>:refs/heads/<branch-name>          # to put it back
```

**Confirm a branch is really merged before deleting it, and do not trust a three-dot diff for
it.** `git diff main...branch` shows what the *branch* changed since the merge base, so it still
prints a full diff for a branch whose work has already landed — including one whose commit was
cherry-picked rather than merged, which is a different sha and therefore not an ancestor. The
reliable checks are `git merge-base --is-ancestor origin/<branch> origin/main`, or, for a
cherry-pick, confirming the actual content is present on `main`.

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

## Issues

Issues are how a failed manual test is recorded — [the manual test
tracker](../docs/engineering/manual-tests/tracker.md#failures-are-tracked-as-gitea-issues) owns
the policy (when a Fail earns an issue, the `TESTID-NN: what broke` title format, what the body
must quote). This section owns the mechanics: the endpoints, and the four ways they surprise you.

The `$API` below is the one derived in [Branches](#branches); the browsable list is at
`$base/issues`, with `$base` from the same snippet.

### Read

```bash
# Open issues only. type=issues is NOT optional - see the traps below.
curl "$API/issues?state=open&type=issues" -H "Authorization: token ${GITEA_ACCESS_TOKEN}"

# One issue, by the number shown in the UI and in the tracker's Issue column.
curl "$API/issues/12" -H "Authorization: token ${GITEA_ACCESS_TOKEN}"

# Already reported? q= matches title AND body, so a test id also finds the issues it blocks.
curl "$API/issues?state=all&type=issues&q=TEAMMATECARD-02" -H "Authorization: token ${GITEA_ACCESS_TOKEN}"

curl "$API/issues/12/comments" -H "Authorization: token ${GITEA_ACCESS_TOKEN}"
```

Useful query parameters: `state` (`open` | `closed` | `all`, default `open`), `type`
(`issues` | `pulls`), `q`, `labels` (comma-separated names), `milestones`, `page` and `limit`
(default 10, hence `limit=50` when you want the lot).

### Write

```bash
curl -X POST "$API/issues" \
  -H "Authorization: token ${GITEA_ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"title":"TEAMMATECARD-02: New teammate opens no card", "body":"..."}'

# Comment on one.
curl -X POST "$API/issues/12/comments" \
  -H "Authorization: token ${GITEA_ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"body":"Still reproduces on ..."}'

# Close one. PATCH edits any field; state is just another field.
curl -X PATCH "$API/issues/12" \
  -H "Authorization: token ${GITEA_ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"state":"closed"}'
```

PowerShell follows the [Pull Requests](#pull-requests) shape exactly — same `$api`, same
`Authorization: token ...` header, same `ConvertTo-Json` body:

```powershell
$body = @{ title = "TESTID-NN: ..."; body = "..." } | ConvertTo-Json

Invoke-RestMethod `
    -Uri "$api/issues" `
    -Method Post `
    -Headers @{ Authorization = "token $env:GITEA_ACCESS_TOKEN" } `
    -ContentType "application/json" `
    -Body $body
```

Opening an issue is a visible action on a shared tracker. Draft the title and body, show them to
the user, and post once they say go — the same bar this doc applies to opening a PR.

### Four traps

1. **`/issues` returns pull requests too.** Gitea models a PR as an issue, so a bare
   `GET $API/issues` mixes both, numbered in one sequence — on this repo today #1-#9 are PRs and
   #10 upward are issues. Pass `type=issues`, or filter on the `pull_request` field being `null`.
   Skip it and your "open issues" answer is wrong in a way that looks plausible.
2. **A missing or bad token gives `404`, not `401`.** The repo is private, so Gitea hides its
   existence rather than admitting an auth failure. A 404 from `/issues` means *check that
   `GITEA_ACCESS_TOKEN` is set in this shell* far more often than it means the path is wrong.
   Confirm with `curl -s -o /dev/null -w "%{http_code}" "$API"` before rewriting the URL.
3. **`labels` and `milestones` on create take IDs, not names** — the read-side `labels=` query
   parameter takes names, so the two sides are not symmetric. This repo defines **no labels and
   no milestones** as of 2026-09-14: `GET $API/labels` returns `[]`, and any label passed by name
   is rejected. Create the label first, or leave the field out.
4. **`limit` defaults to 10.** Concluding "there are only 10 issues" from an unpaged call is the
   same class of quiet, believable error as trap 1.

## Actions (workflow runs)

For re-running a job or checking a run's status without the web UI, use the endpoints already
documented in [CIPipeline.md § Reading CI results from a
session](CIPipeline.md#reading-ci-results-from-a-session) — that page owns the full endpoint
table and the three API traps already hit (`/actions/tasks` returning an empty list,
`/rerun` on a run that isn't done, and a run reporting `in_progress` while the failure is
already in its log). Come here only for branches, PRs and issues; go there for anything
Actions-shaped.

## Validation status

The environment facts above (`tea` absence, the per-hostname credential caching behavior, the
gitleaks `internal-mdns-host` rule) are confirmed as of 2026-09-13. Everything below was
exercised against this instance on 2026-09-14, running Gitea **1.26.4**.

**Observed, not inferred.** The whole **read** side of [Issues](#issues) — the listing,
`type=issues`, `q=`, the comments endpoint, the empty label set and the 404-not-401 behaviour.
Three **write** shapes, each returning `201`: commenting on an issue
(`POST /issues/{n}/comments`), closing one (`PATCH /issues/{n}` with `{"state":"closed"}`), and
[creating a pull request](#pull-requests) (`POST /pulls` with `title`/`head`/`base`/`body`).
Listing Actions runs (`GET /actions/runs`) also works as described, and the `.git` suffix strip
plus `cut -d/ -f1-3` in the [Branches](#branches) snippet derives the right base URL unchanged.

**Branch delete moved from documented to observed on 2026-09-15.** It was exercised thirteen
times in one session, clearing every merged branch off the remote, and returned **204** with an
empty body each time — including for branch names containing a `/`, which need no escaping. The
endpoint and the shape above are exactly as written.

**Still documented from Gitea's published REST API conventions rather than observed:** issue
**create** and Actions **rerun**. Both are visible and non-trivial to undo, so neither was run
just to validate this page. Treat those two request bodies as a strong starting point, not a
guarantee; if one 4xxs, check the response body for the actual Gitea version's field names before
assuming the whole approach is wrong.

## Related

- [CIPipeline.md](CIPipeline.md) — CI/Actions internals: workflow topology, failure modes, and
  the full Actions API reference this doc points to above
- [Manual test tracker](../docs/engineering/manual-tests/tracker.md) — when an issue gets opened
  and how it is titled; this doc covers how to open it
- [C# Principles](CSharpPrinciples.md) — the house style the build enforces
