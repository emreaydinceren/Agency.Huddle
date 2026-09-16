# Gitea batch C — two corrections

Drafted 2026-09-15 while the Gitea instance was unreachable. **Nothing posted yet.**

Both retract something I filed or claimed earlier today. Post them together.

---

## 1. Comment on #20, then CLOSE it

This issue was closed by batch A, I reopened it on a bad verification, and it should
be closed again. The comment explains why, so post the comment *before* closing.

**Retracting my previous comment. This issue's precondition no longer holds on this machine, so my re-verification proved nothing — closing again.**

I reopened this and said the defect still reproduced. That was wrong, and the mistake is worth recording because the oracle I trusted is the one this issue's own steps recommend.

## What I got wrong

I checked the precondition with `assoc .md`, which reports:

```
File association not found for extension .md
```

and concluded no application was registered. **`assoc` does not see the whole picture.** It reports the classic default ProgId only, and `HKCR\.md`'s default value is genuinely empty here. But the shell also resolves through `OpenWithProgids`, and that key is populated:

```
HKCU:\Software\Classes\.md\OpenWithProgids
    VSCode.md
    AppXc5eekcytc3qx4t9p10r6czzyc15gmhgf
```

So ShellExecute has handlers to choose from, and it uses one.

## What actually happens

Reproducing the exact call the app makes:

```
$psi = New-Object System.Diagnostics.ProcessStartInfo('...\App_Data\Teams\Nova.md')
$psi.UseShellExecute = $true
[System.Diagnostics.Process]::Start($psi)
```

returns **a real process, not null**:

```
Process.Start returned process: id=53168 name=claude
```

The `AppX…` ProgId resolves to a packaged app, which is what opened. So `OpenInEditor`'s null check is behaving correctly by not firing — a process genuinely was started. The file opens in a desktop application, outside the browser, which is why I saw nothing: I was watching the browser pane and the app log, and neither is where the result appears.

That is exactly the third row of the verification brief I was working from — *"A file actually opens → BLOCKED — this machine has gained a `.md` association and the precondition no longer holds"* — and I should have reached for it instead of reading the silence as the defect.

## Where that leaves the fix

Untested rather than disproven. The null-return branch added in PR #34 is unreachable on this machine, so nothing I did exercised it. I have no evidence against it, and the code reads correctly for the case it targets.

## For whoever re-tests this

`assoc .md` is not a sufficient precondition check — it returns "not found" on a machine that opens `.md` files perfectly well. Check `HKCU:\Software\Classes\.md\OpenWithProgids` and `HKCR\.md` together, or better, assert on the call itself: if `Process.Start` returns non-null, the precondition is not met, whatever `assoc` says.

Watch the desktop, not just the browser — a successful open is a native window the app cannot see or report.

---

## 2. Comment on #33, then CLOSE it

I filed #33 this afternoon. It is not a defect.

**Not a defect. This is the model and effort catalog probe, which is documented behaviour — closing.**

I filed this, and the attribution was wrong. The evidence is decisive and free to reproduce.

## The test

Every run in the original report had ACP **on**, so a Persona session existing at all made "a session was created" look like a Persona restart. Run the same thing with the cost guard off:

```
dotnet run --project src/Huddle.App -- --urls http://localhost:5100 --Team:Acp:Enabled=false
```

open a teammate card, click **Edit**, and read the log. With `Team:Acp:Enabled=false` **no Persona session can exist** — and the triple appears anyway. From one such run:

```
[agent stderr] [session/create] sessionId=23c669ab-… phase=register durationMs=1 totalMs=942
Dropping update for unknown session 23c669ab-…
The agent process disconnected.
[agent stderr] [session/create] sessionId=c507eb38-… phase=register durationMs=1 totalMs=894
The agent process disconnected.
```

while the same log contains:

```
Persona '…' lines      : 0
PersonaSupervisor lines: 0
```

Four separate free-lane runs produced identical counts: 2 × `phase=register`, 1 × `unknown session`, 2 × `disconnected`, 0 Persona starts.

## What is actually happening

Two documented probes, both in `rules.md`:

> **The model catalog is read by spawning an adapter, even when `Acp:Enabled` is false.** That flag exists because ACP *spends money*; the probe spends none, because it never starts a Turn.

> **The effort catalog is probed per model, so changing the Model in the card spawns an adapter.**

Each probe spawns an adapter, creates a real ACP session — hence `phase=register` — reads the catalog, and exits, which is the `disconnected` line. `DotAcpClientAdapter.OnDisconnected` clears `this.sinks`, so a session update still in flight when the process goes finds no sink and logs `Dropping update for unknown session`. That explains the third line and why it names the session that was *just* registered.

It also explains both things that made this look mysterious:

- **No adapter process is replaced.** The probe's process lives about a second, so by the time you list processes it is gone and the long-lived Persona adapters are untouched.
- **It follows activity, not time.** 75 seconds idle produced none, because the probe only runs when a New/Edit card opens or the Model changes — never on a plain page load, which `rules.md` also states and a test pins.

## What survives, and is worth keeping

One real observation, stripped of the wrong cause: **`phase=register` is not an oracle for "did this restart the teammate?"** It counts probe sessions too, so a save that restarts a Persona once can show two or three new sessions. Counting adapter *processes* is the reliable signal — a genuine Persona restart replaces the Persona's `claude-agent-acp` process, and a probe does not.

That cost real time across three manual tests before it was understood, so it is worth a line in `known-limits.md` next to the probe entry rather than an open issue. Closing.
