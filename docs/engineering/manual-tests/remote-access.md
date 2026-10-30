# Remote access

Prove, in a real browser and on real second devices, that the Gateway admits only connections that present the shared secret and a device key; that the desktop launcher opens signed in; that Tailscale Serve, the downgraded LAN bind and Funnel behave as the design says; that a phone is paired, limited, asked to become admin and revoked; that the rate limit and the Origin rules hold; and that the Open action disappears while remote access is configured.

**13 tests** · 13 free · about 2.6 hours. Written 2026-10-30; **not yet run by a person**. REMOTE-03, REMOTE-04, REMOTE-09, REMOTE-10, REMOTE-12 and REMOTE-13 need Tailscale; REMOTE-03, REMOTE-04, REMOTE-05, REMOTE-09 and REMOTE-10 also need a second device (a phone). None spends money.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

## Setup

Run [`P-BUILD`](common.md#p-build). This area adds:

1. `huddle` below means the app's exe (`Agency.Huddle.App.exe`). In a dev run, `dotnet run --project src\Huddle.App --` stands for it, and the `devices` verbs need `--url http://127.0.0.1:<port>`.
2. A dev run sets the data folder with `--Team:DataDir=C:\seeds\huddle\remote\data` and the port with `--urls http://localhost:5100`. Use a fresh folder per test that says so.
3. Tests marked **needs Tailscale** need the Tailscale client installed and signed in on this computer. Tests that also need a second device use a phone or another computer; run them last, together.
4. The first non-loopback bind makes Windows ask about the firewall. Allow **Private networks** only.
5. Restarting means `P-STOP`, then the same launch command, so the data stays.

## Tests

### REMOTE-01 — The installed app opens signed in, and a second launch opens another signed-in tab

**Free** · about 10 min

*Proves the launcher's bootstrap handoff: the browser opens already connected, with no token typed, and a second launch asks the running copy for a fresh link.*

**Before you start**

- The installed app (see [installer.md](../installer.md)) is installed and not running.
- `Team:Gateway` is at its defaults (Bind `loopback`, Tailscale `off`).

**Steps**

1. Start Huddle from the Start menu. Wait for the browser to open.
2. Look at the address bar, then at the page.
3. Open **Settings › Remote access**.
4. Start Huddle from the Start menu a second time.
5. Look at the second tab the launch opens.

**Pass if — all of these**

- At step 2 the page is Huddle, not the connect page, and the address bar shows `http://127.0.0.1:` and a port, with no `#bootstrapToken` left in it.
- At step 3 the Devices list holds one row, **This device**, Admin, paired by loopback or bootstrap.
- At step 5 a second tab opens on Huddle already signed in, and no second copy of the app is running.

### REMOTE-02 — A second browser on the host sees the connect page, and dashboard and auth-token work as documented

**Free** · about 10 min

*Proves that a browser profile with no device key must connect, and the two recovery verbs.*

**Before you start**

- The app from REMOTE-01 is running.
- A second browser profile (a private window works) is open on the same computer.

**Steps**

1. In the second browser open the address from REMOTE-01.
2. In a plain PowerShell window run `huddle auth-token --show`.
3. Paste the token into the connect page and press **Connect**.
4. In `T-B` run `huddle auth-token --show | Out-File x.txt`.
5. Run `huddle dashboard`.

**Pass if — all of these**

- At step 1 the page is titled **Connect to Huddle** with a **Gateway token** field.
- At step 2 a 48-character hexadecimal token prints.
- At step 3 the browser opens Huddle; on loopback it needs no approval.
- At step 4 the command refuses and says standard output is redirected.
- At step 5 a signed-in tab opens in the default browser and the command prints `Opened Huddle in your browser.`.

### REMOTE-03 — Tailscale Serve: a phone gets in with no token and no approval, limited, then asks for admin

**Free** · about 15 min · needs Tailscale

*Proves verified Tailscale identity pairs a device automatically with limited scopes, and that Request admin needs approval. Needs a second device on the same tailnet.*

**Before you start**

- Tailscale is installed and signed in on this computer and on a phone, both on one tailnet, with HTTPS certificates enabled for the tailnet.
- Huddle is running with **Tailscale › Serve** saved in **Settings › Remote access** and Huddle restarted.

**Steps**

1. In **Remote access** wait for the state chip to read **Running** and copy the `ts.net` link.
2. Open the link on the phone.
3. On the phone open **Settings**.
4. Press **Request admin**.
5. On the computer find the request under **Waiting for approval** and press **Approve**.
6. On the phone reload.

**Pass if — all of these**

- At step 2 the phone opens Huddle with no token typed and no approval, and the computer lists the phone as **Limited** paired by Tailscale.
- At step 3 the phone shows "Settings need admin access" and the **Request admin** button.
- At step 5 the request shows the phone's Tailscale login.
- At step 6 the phone's Settings opens and the device is **Admin**.

### REMOTE-04 — Tailscale Serve: a shared tailnet user is marked, and revoking disconnects them

**Free** · about 15 min · needs Tailscale

*Proves a shared device is labelled **Shared user** and that Revoke ends its access. Needs a second device on a tailnet that has been shared with another Tailscale account.*

**Before you start**

- REMOTE-03 passed and a device belonging to a different Tailscale user can reach the computer through a shared node.

**Steps**

1. Open the `ts.net` link from the shared user's device.
2. On the computer open the Devices list or **Waiting for approval**.
3. Press **Revoke** on that device.
4. On the shared device reload the page.

**Pass if — all of these**

- At step 2 the row shows the shared user's login with **Shared user**, not **You**.
- At step 3 the device leaves the list.
- At step 4 the shared device lands on the connect page and cannot get into Huddle without a new handshake.

### REMOTE-05 — LAN: the QR setup link on iPhone and Android, with approval and the downgraded warnings

**Free** · about 20 min

*Proves the plain-HTTP path without Tailscale: Bind `lan`, a setup link, approval, and every place the word downgraded must appear. Needs a phone on the same Wi-Fi.*

**Before you start**

- Tailscale is off. The phone and the computer are on the same network.
- Huddle runs with `Team:Gateway:Port` set to a fixed port, such as `5101`.

**Steps**

1. In **Remote access** set **Bind** to `lan`, save, and restart Huddle. Allow the firewall prompt on **Private networks** only.
2. Read the Gateway card and the Security check.
3. Under **Pair a device** scan the QR code with the phone camera (do this once on an iPhone and once on an Android phone).
4. On the computer approve the request under **Waiting for approval**.
5. On the phone reload.

**Pass if — all of these**

- At step 2 the card says **Restart Huddle to apply** until restarted, then shows a downgraded notice, and the Security check lists `gateway.downgraded_transport`.
- At step 3 the phone opens a connect page that says it is waiting for approval and opens no Room.
- At step 4 the request row shows **LAN (downgraded)** and the scope menu is capped at your scopes.
- At step 5 the phone opens Huddle, with **Limited** access unless you chose more.
- On both phones the address begins with `http://` and a private address or a `.local` name.

### REMOTE-06 — Rate limit: ten wrong tokens lock a phone out for five minutes, and loopback is only slowed

**Free** · about 8 min

*Proves failed gateway auth is limited per address, and that the host's own browser is never locked out.*

**Before you start**

- The LAN setup from REMOTE-05 is running and the phone is not yet paired (use a private tab or clear its site data).

**Steps**

1. On the phone's connect page submit a wrong token ten times.
2. Submit the correct token on the eleventh try.
3. Wait five minutes and submit the correct token.
4. In the second browser on the computer (loopback) submit a wrong token ten times, then the correct one.

**Pass if — all of these**

- After step 1 the message names how long to wait.
- At step 2 the correct token is also refused while locked out.
- At step 3 the handshake continues (a pairing request opens).
- At step 4 each wrong attempt is slower, but the correct token is accepted at once on the eleventh try.

### REMOTE-07 — Revoking a phone with a Room streaming disconnects it at once

**Free** · about 10 min

*Proves revocation closes the open connection, not just future requests (the CVE-2026-34503 case).*

**Before you start**

- A phone is paired as in REMOTE-05 and the free lane has an Agent that streams a reply (`P-LAUNCH-UAT`).

**Steps**

1. On the phone open a Room and send a message that makes the Agent stream.
2. While it streams press **Revoke** on the phone's row on the computer.
3. Watch the phone for ten seconds.

**Pass if — all of these**

- At step 3 the phone stops receiving text within a few seconds and ends on the connect page or the reconnect notice that sends it there.
- The phone cannot reopen the Room without a new handshake and a new approval.

### REMOTE-08 — Two copies on ports 5101 and 5102 keep separate devices and no sign-outs

**Free** · about 10 min

*Proves cookies are named per instance, so two copies on one host do not sign each other out.*

**Before you start**

- Two copies of the app with different `Team:DataDir` folders, one on `--urls http://localhost:5101` and one on `http://localhost:5102`.

**Steps**

1. Open `http://localhost:5101` and sign in with its token.
2. Open `http://localhost:5102` in the same browser and sign in with its token.
3. Reload the first tab, then the second.
4. Open the Devices list in each copy.

**Pass if — all of these**

- Both tabs stay signed in after the reloads.
- Each copy's Devices list holds its own device and not the other's.

### REMOTE-09 — A hand-made Serve route is refused and shown in the Security check

**Free** · about 12 min · needs Tailscale

*Proves a route not created by Huddle never gets Tailscale sign-in and is reported. Needs a second device on the tailnet.*

**Before you start**

- Tailscale is installed and Huddle's **Tailscale** mode is `off`.

**Steps**

1. In a terminal run `tailscale serve --bg 5101`, with 5101 the app's port.
2. Open the `ts.net` address on the phone.
3. In **Remote access** read the Security check.
4. Run `tailscale serve reset` to clean up.

**Pass if — all of these**

- At step 2 the page is refused with 403 and `proxy_attribution_required` (or the origin is refused), not Huddle.
- At step 3 the Security check lists a stray route that points at Huddle's app port.

### REMOTE-10 — Funnel: refused in token mode, and in password mode a phone off the tailnet needs the password and approval

**Free** · about 20 min · needs Tailscale

*Proves Funnel always needs the password and approval, and shows the critical item. Needs a phone off the tailnet (mobile data) and a tailnet with Funnel enabled.*

**Before you start**

- Tailscale is signed in with Funnel permitted for this node.

**Steps**

1. Set **Tailscale** to `Funnel` with **Auth** at `token`, save and restart.
2. Read the Gateway card.
3. Set **Auth** to `password`, set a password, save and restart.
4. On a phone with Wi-Fi off open the Funnel link.
5. Enter the password. Look at the computer.

**Pass if — all of these**

- At step 2 the card says **Funnel needs password auth** and the Funnel route is not claimed; Huddle itself still runs.
- At step 4 the phone asks for the password and nothing else, even if it holds a device token.
- At step 5 a request waits for approval; the Security check shows `gateway.tailscale_funnel` as critical.

### REMOTE-11 — With remote access configured, the Open action and Library open and reveal are gone

**Free** · about 8 min

*Proves ADR-0070: an action that opens a file on the host is not offered once the host is reachable from elsewhere.*

**Before you start**

- A Library with a file in it and a Task with an Action that has an Open step.

**Steps**

1. With `Bind` `loopback` and Tailscale `off`, open the Task and the Library file and look for **Open** and **Reveal**.
2. Set **Bind** to `lan` (or Tailscale to `serve`), save and restart. Look again.
3. Set both back and restart. Look again.

**Pass if — all of these**

- At step 1 the Open action and the Library open and reveal controls are present.
- At step 2 none of them is shown, and a stale link to one is refused.
- At step 3 they return.

### REMOTE-12 — A Huddle crash kills the Tailscale child (Job object)

**Free** · about 10 min · needs Tailscale

*Proves the Windows Job object: if Huddle dies without cleaning up, the `tailscale serve` child dies with it. Needs Tailscale; Windows only.*

**Before you start**

- **Tailscale** is `Serve` and **Running**.

**Steps**

1. In `T-B` run `Get-Process tailscale | Select-Object Id, StartTime` and note the foreground `serve` child.
2. Kill Huddle hard: `Stop-Process -Name Agency.Huddle.App -Force`.
3. Run the `Get-Process` command again.

**Pass if — all of these**

- At step 1 one extra `tailscale` process runs beside the service.
- At step 3 that process is gone.

### REMOTE-13 — Killing the Tailscale child removes the Serve route

**Free** · about 10 min · needs Tailscale

*Proves the open point that Serve drops a route when its foreground process dies (Spec Appendix E, OP-2).*

**Before you start**

- **Tailscale** is `Serve` and **Running**.

**Steps**

1. Run `tailscale serve status --json` and note the route to `127.0.0.1`.
2. Kill the foreground `tailscale serve` child with `Stop-Process -Id <id> -Force`.
3. Run `tailscale serve status --json` again.

**Pass if — all of these**

- At step 1 a route points at Huddle's managed listener.
- At step 3 the route is gone. If it remains, report it: the Security check would show a stray route.

---

Back to [the manual test script](../manual-tests.md).
