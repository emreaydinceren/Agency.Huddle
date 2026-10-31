# Remote access

**Status:** built 2026-10-30 on `feat/remote-access`, not merged; a faithful port of OpenClaw's gateway model. Where the build differs from the first design, the text below is already corrected and [Deviations from the first design](#deviations-from-the-first-design) lists each change · **Decision:**
[ADR-0070](adr/0070-huddle-copies-openclaws-gateway-auth-device-pairing-and-tailscale-model.md) · **Extends:** Settings
(`G`, [wireframes.md](engineering/wireframes.md) Figure 6), the desktop host, the Feed
([Huddle.Feed-Specifications.md](Huddle.Feed-Specifications.md)) · **Deferred:** [backlog.md](engineering/backlog.md#hardening-remote-access)
· **Vocabulary:** [language.md](engineering/language.md)

This spec lets the Human use Huddle from **another device** (a phone, a tablet, a second computer) by copying the
remote-access model of **OpenClaw**, the open-source personal agent gateway, as closely as Huddle's shape allows.
OpenClaw solves the same problem for the same kind of user: one person, one machine running agents that can touch the
file system, and a wish to reach it from elsewhere without turning it into a hosted service. Its model has been through
public security review (two CVEs on exactly this surface are cited below), and the product owner chose to follow it
rather than invent one.

**The model in five lines.**

1. Huddle is a **gateway** with one **shared secret** (a token by default, or a password). Every browser presents it,
   **including the browser on the host itself**.
2. Every browser also holds an **Ed25519 device identity** and signs a fresh server challenge on every connection.
3. A new device must be **paired**: approved by an admin session, unless it connects from loopback or through
   verified Tailscale Serve, where pairing is automatic.
4. Huddle listens on **loopback** by default. **Tailscale Serve** is the recommended way in (HTTPS, tailnet only);
   **Funnel** is public and needs password auth; binding to the **tailnet** or **LAN** address works over plain HTTP,
   which is called **downgraded**.
5. Failed authentication is **rate-limited**, origins are **checked**, and **revoking** a device disconnects it at once.

It is for whoever builds or reviews any of this: the gateway auth, the connect handshake, pairing, the Tailscale
integration, the CLI verbs, or the **Remote access** section in Settings.

**Sources.** Every OpenClaw rule below was checked on 2026-10-30 against [docs.openclaw.ai](https://docs.openclaw.ai)
and the `openclaw/openclaw` source on GitHub. OpenClaw's own docs say the code wins where they disagree. Where the
behaviour is not documented, this spec says **(not in OpenClaw)** and makes a choice; Appendix E lists those choices.

## Contents

- [1. Goal](#1-goal)
- [2. Use cases](#2-use-cases)
- [3. Non-goals](#3-non-goals)
- [4. Design principles and threat model](#4-design-principles-and-threat-model)
- [5. Architecture overview](#5-architecture-overview)
- [6. Components](#6-components)
- [7. Data model and storage](#7-data-model-and-storage)
- [8. Core algorithms](#8-core-algorithms)
- [9. Startup and runtime changes](#9-startup-and-runtime-changes)
- [10. Background work](#10-background-work)
- [11. Performance](#11-performance)
- [12. Edge cases and failure modes](#12-edge-cases-and-failure-modes)
- [13. End-to-end flows](#13-end-to-end-flows)
- [14. Decisions and rationale](#14-decisions-and-rationale)
- [Deviations from the first design](#deviations-from-the-first-design)
- [Appendix A. Configuration](#appendix-a-configuration)
- [Appendix B. The Remote access section (`G10`)](#appendix-b-the-remote-access-section-g10)
- [Appendix C. Delivery plan, tests first](#appendix-c-delivery-plan-tests-first)
- [Appendix D. Manual tests](#appendix-d-manual-tests)
- [Appendix E. Open points and choices not in OpenClaw](#appendix-e-open-points-and-choices-not-in-openclaw)
- [Appendix F. Documents to change when it is built](#appendix-f-documents-to-change-when-it-is-built)

## Vocabulary

Added to [language.md](engineering/language.md) when built. Until then this table owns them. OpenClaw's own word is
kept wherever Huddle has no clash.

| Term | Meaning | Avoid |
| --- | --- | --- |
| **Gateway** | Huddle's HTTP surface as a whole, as OpenClaw names its own: every page, asset, endpoint and the `/_blazor` connection | Server, API |
| **Gateway auth** | The shared secret every connection presents: the **gateway token** (48 hex characters, the default) or the **gateway password** (`Auth:Mode`), or Tailscale identity, or trusted-proxy identity | Login |
| **Device identity** | An Ed25519 key pair the browser generates and keeps in its local storage. The **device id** is the SHA-256 hex of the public key | Fingerprint, client id |
| **Connect handshake** | `/auth`: the browser asks for a challenge, signs it with its device key together with its credential, and receives a **session** | Login, sign-in |
| **Session** | The proof of one successful handshake: an HttpOnly cookie Huddle's pages and `/_blazor` carry. Huddle's equivalent of an authenticated OpenClaw WebSocket | Login session |
| **Pairing** | A device becoming known. A **pairing request** waits up to 5 minutes for an admin's **Approve** or **Reject**, unless auto-approved | Registration |
| **Device token** | The per-device credential issued on approval, scoped to a role and scopes, kept by the browser in local storage and presented on later handshakes instead of the gateway token | API key |
| **Scope** | What a session may do: `operator.read`, `operator.write`, `operator.pairing`, `operator.admin` (§6.7) | Permission, role |
| **Limited access** | A device without `operator.admin`. It may **Request admin**, which needs approval | Read-only |
| **Bootstrap token** | A single-use credential that opens one browser already authenticated: minted for the host's own browser by the desktop launcher or `dashboard` verb, or for another device as a **setup link** | Magic link |
| **Ingress** | How a request arrives: **loopback**, **managed** (through Huddle's own Tailscale Serve or Funnel), **direct** (a tailnet or LAN bind), or **trusted proxy** | Transport |
| **Managed listener** | The extra loopback listener on a random port that only Huddle's own `tailscale serve` points at | Proxy port |
| **Downgraded** | OpenClaw's word for plain HTTP off loopback: it works, but nothing is encrypted and the page can be altered in transit | Insecure mode |
| **Instance id** | A random id stored once per `{DataDir}`, part of every cookie name, because cookies ignore ports and copies run side by side | Port |

## 1. Goal

1. **The same protection as OpenClaw once remote access is on.** A shared secret, a per-device key and pairing
   approval protect every connection, with OpenClaw's rate limits, origin rules and revocation. By default Huddle runs
   in **Strict local** (§6.5a): only this computer can use it, with no sign-in. The owner chose this over OpenClaw's
   token on loopback.
2. **The same ways in as OpenClaw.** Loopback by default; Tailscale Serve as the recommended remote path; Funnel only
   with password auth; tailnet and LAN binds as downgraded options; trusted-proxy auth for people who already run a
   proxy.
3. **No friction on the host.** By default the host browser needs no token at all (Strict local). With Strict local
   off, the desktop launcher opens the browser already authenticated (OpenClaw's dashboard handoff), so a Human who
   never leaves the host never types a token.
4. **Side-by-side copies keep working.** Copies started with different `--urls` ports have separate secrets, devices,
   sessions, cookies and Tailscale ports. Browser storage is per origin, and origins include the port, so device keys
   separate on their own; cookies do not, and carry the Instance id.
5. **A phone can pair without typing 48 hex characters.** Over Tailscale it needs nothing typed; elsewhere a QR setup
   link carries a single-use bootstrap token.

## 2. Use cases

| # | Who | What they do | What must be true |
| --- | --- | --- | --- |
| U-1 | Human on the host | Starts the installed app | The browser opens on Huddle, admin, nothing typed. Strict local (default): the plain address, no sign-in. Strict local off: authenticated by a bootstrap token (§6.9) |
| U-2 | Human on the host, second browser | Opens `http://127.0.0.1:5101` in Firefox | Strict local (default): Huddle opens, no sign-in. Strict local off: the connect page asks for the gateway token; `Agency.Huddle.App.exe dashboard` (or the token from `auth-token --show`) gets in; loopback auto-approves |
| U-3 | Human with Tailscale | Chooses Tailscale Serve, restarts, opens `https://desktop-a.tail1234.ts.net:5101` on the phone | No token, no approval: verified Tailscale identity stands in for gateway auth and pairs automatically with limited access |
| U-4 | Human without Tailscale | Chooses LAN (downgraded), restarts, shows **Pair a device** QR, scans it with the iPad | The iPad's connect page redeems the setup link, waits; the host's Feed shows the request; **Approve**; the iPad is in with limited access |
| U-5 | Phone with limited access | Opens Settings | "Settings need admin access." **Request admin** → an approval on the host → admin |
| U-6 | Someone on the Wi-Fi | Opens the LAN address | The connect page; without the token, nothing; 10 wrong tries in 60 s → locked out 5 minutes |
| U-7 | Human who lost a phone | **Revoke** in Devices | The phone's open Huddle disconnects at once; its key and token no longer work |
| U-8 | Developer with copies on 5101 and 5102 | Uses both from one laptop | Separate tokens, devices and sessions; neither signs the other out |
| U-9 | Human who wants access from anywhere | Chooses Funnel | Refused until `Auth:Mode` is `password`; then every visitor needs the password, their device must be approved, and the Security check shows a critical item |
| U-10 | Someone who ran `tailscale serve 5101` by hand | Opens it from another device | 403 `proxy_attribution_required`; the Security check lists the stray route |
| U-11 | A web page in the Human's browser | Tries to reach `localhost:5101` | Refused by the origin check, and it has no token anyway |

## 3. Non-goals

| Not in this version | Why | Later? |
| --- | --- | --- |
| Huddle's own TLS | OpenClaw builds no TLS for browsers either; HTTPS comes from Tailscale Serve | [Backlog](engineering/backlog.md#hardening-remote-access) H-1 |
| A read-only device | OpenClaw has `operator.read` without `operator.write`; Huddle's UI has no read-only mode, so V1 always grants both together (§6.7) | Backlog H-2 |
| OpenClaw's finer scopes (`operator.sessions.*`, `operator.approvals`, `operator.questions`, `operator.talk*`) | No Huddle feature maps to them separately | Not planned |
| Native nodes and mobile apps, setup codes with TLS pins | Huddle has only a browser client | Not planned |
| Turning things on without a restart | The bind and Tailscale mode take effect at the next start, as in OpenClaw | V2 |
| A persistent switch that disables device identity | OpenClaw retired `dangerouslyDisableDeviceAuth` and `allowInsecureAuth` after CVE-2026-32034. Huddle never adds one | Never |
| Several Humans | OpenClaw: "one trust boundary per gateway". Huddle has one Human | Never |

## 4. Design principles and threat model

### Principles

| # | Principle | OpenClaw source |
| --- | --- | --- |
| P-1 | **Every connection presents gateway auth and a device signature**, loopback included, unless Strict local is on (§6.5a) | Gateway auth runs first; a loopback connection still needs the token. Strict local is not in OpenClaw |
| P-2 | **No switch turns device identity off** | `dangerouslyDisableDeviceAuth` is retired and inert |
| P-3 | **Loopback means a direct socket with no forwarded headers**, and earns only auto-approval, never a skip of the secret | `proxy_attribution_required`; auto-approval for direct `127.0.0.1` with no forwarded headers |
| P-4 | **Tailscale identity is believed only on the managed listener**, only when `whois` agrees | `allowTailscale`, managed-listener provenance |
| P-5 | **Remote devices are approved by someone who already has the scope** | `operator.pairing` approves only scopes it holds; admin needs admin |
| P-6 | **Plain HTTP off loopback is called downgraded wherever it shows** | "Plain HTTP remains a downgraded transport." |
| P-7 | **Revocation disconnects** | Fix for CVE-2026-34503 |
| P-8 | **Huddle's own additions are marked** | — |

### Threat model

| Threat | Covered? | How |
| --- | --- | --- |
| A stranger on the network | Yes | No gateway secret, no session; lockout after 10 failures in 60 s |
| Any program or other user account on this computer | **No, with Strict local on (default)** | It can use Huddle with no secret, as before remote access existed. With Strict local off, it needs the token (§6.5) |
| A leaked gateway token | Partly | A new device still needs approval (except on loopback, which needs a local account). Rotate the token (§6.5). With Strict local on there is no remote path to attack |
| A stolen phone | Yes | Revoke disconnects it; its device token and key are dead |
| A malicious web page in the Human's browser | Yes | Origin check (§8.7); no token in reach (sessionStorage and HttpOnly cookies are per origin); `SameSite` |
| DNS rebinding | Yes | Origin check plus host filtering, also with Strict local on (where there is no token to lack) |
| A proxy someone set up on the host | Yes | Forwarded headers on a loopback socket → 403 `proxy_attribution_required` |
| Forged Tailscale headers | Yes | Read only on the managed listener and confirmed with `whois` |
| Sniffing on a downgraded bind | **No** | The token and session travel in clear on the LAN or tailnet IP. Serve is the answer; the UI says so |
| Untrusted code on the host | Out of scope | OpenClaw: turn off tokenless Serve if untrusted local code may run, since it can reach the managed listener |
| The public internet (Funnel) | Yes | Password mode required; rate limits; approval; a critical Security check item |

## 5. Architecture overview

```text
 Host browser            Phone on the tailnet                  Phone on the Wi-Fi                Internet (Funnel)
 http://127.0.0.1:5101   https://desktop-a.tail1234.ts.net:5101 http://192.0.2.20:5101          https://…ts.net:443
       │                        │                                     │                                 │
       │                 ┌──────▼──────────────┐                      │                 ┌───────────────▼─────────┐
       │                 │ tailscaled (Serve)  │ TLS, identity hdrs,  │                 │ tailscaled (Funnel)     │
       │                 │                     │ X-Forwarded-*        │                 │ Tailscale-Funnel-Request│
       │                 └──────┬──────────────┘                      │                 └───────────────┬─────────┘
┌──────▼────────────────────────▼─────────────────────────────────────▼─────────────────────────────────▼───────────┐
│ Kestrel: app listener 127.0.0.1:5101 (or a tailnet / LAN / custom bind) · managed listener 127.0.0.1:{random}       │
├────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Host filtering → Origin check (§8.7) → Ingress classifier (§8.6)                                                     │
│ GatewayGate:                                                                                                         │
│   /auth, /auth/*, /auth.js, /auth.css ─► ConnectEndpoints: challenge → verify gateway auth + device signature →      │
│                                         pairing (auto / pending / approved) → session cookie + device token         │
│   valid session (cookie) ──────────────► pass, as Device n with its scopes                                           │
│   otherwise ───────────────────────────► 302 /auth?return=… (HTML GET) or 401                                        │
│   /_gateway/* (CLI) ───────────────────► loopback only, gateway token in the header                                  │
├────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Today's app. /settings/* needs operator.admin. /_blazor connections are tracked per device for revocation           │
└────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
   │                 │                      │                     │                     │                  │
┌──▼─────────┐ ┌─────▼───────────┐ ┌────────▼────────┐ ┌──────────▼────────┐ ┌──────────▼───────┐ ┌────────▼────────┐
│ Gateway    │ │ AuthRateLimiter │ │ PairingService  │ │ DeviceRegistry    │ │ SessionRegistry  │ │ Tailscale       │
│ Settings   │ │ scope:ip keys,  │ │ pending (5 min),│ │ team.db: devices, │ │ memory: session  │ │ Controller      │
│ token,     │ │ 10/60 s → 5 min │ │ auto-approve,   │ │ device tokens     │ │ hash → device,   │ │ status, serve   │
│ password   │ │                 │ │ approve/reject  │ │ (hashed), scopes  │ │ circuits; Abort  │ │ claim, whois    │
└────────────┘ └─────────────────┘ └─────────────────┘ └───────────────────┘ └──────────────────┘ └─────────────────┘
                                          │
                                 IFeedPublisher: PairingRequested (notify), DevicePaired, DeviceRevoked, AdminRequested
```

**Reading the picture.** Every request, from any ingress, passes the same gate. The only unauthenticated routes are
the connect page and its handshake endpoints. A valid session cookie is the only thing that admits a page, an asset or
a Blazor connection, and a session exists only after a signed handshake.

## 6. Components

New code lives in `src/Huddle.App/Gateway/` (namespace `Agency.Huddle.App.Gateway`) unless stated. Each component
gives its purpose, responsibilities, implementation notes, constraints and V2 notes.

### 6.1 `GatewaySettings`, the token and its store

**Purpose.** The gateway's configuration, readable before the app is built, and the shared secret.

**Responsibilities.**

- Settings come from `Team:Gateway` (Appendix A), overridden by what an admin saved in Settings, stored in `team.db`
  table `gateway_settings` (§7.1). `ReadForStartup(string dataDir, IConfiguration)` runs in `Program.cs` before
  `builder.Build()`, beside the desktop hand-off.
- **The gateway token** resolves as OpenClaw's does: `Team:Gateway:Auth:Token`, else the environment variable
  `HUDDLE_GATEWAY_TOKEN`, else the stored token. On first start with none, Huddle **generates one and stores it**
  (OpenClaw's onboarding generates a token even for loopback). Generation is OpenClaw's: 24 random bytes as 48
  lowercase hex characters.
- Startup refuses a blank token or one of the literals `undefined`, `null` or `__OPENCLAW_REDACTED__`-style
  placeholders, and the Security check warns under 24 characters.
- **Rotation** (`auth-token --rotate`, or **Rotate** in Settings) replaces the stored token and hot-applies when the
  auth mode does not change. Existing sessions and device tokens stay valid: they are separate credentials.
- **The gateway password** (`Auth:Mode = password`) comes from `Team:Gateway:Auth:Password`, `HUDDLE_GATEWAY_PASSWORD`,
  or a password set in Settings, which is stored as PBKDF2-SHA256 (600,000 iterations, 16-byte salt). It is never
  shown again.
- **The Instance id**, created once.

**Constraints.** The token is never logged, never put in a URL Huddle generates (bootstrap tokens are, §6.9), and
shown only by `auth-token --show` on a terminal (OpenClaw refuses to print it to a pipe) or once in Settings behind a
**Show** button for an admin session on loopback.

### 6.2 Listeners and bind modes

**Purpose.** OpenClaw's `gateway.bind`, on Huddle's port.

| `Bind` | App listener | Note |
| --- | --- | --- |
| `loopback` (default) | As configured today (`127.0.0.1:{port}` or `localhost`) | |
| `tailnet` | `127.0.0.1:{port}` and the Tailscale IPv4 `100.x.y.z:{port}` | Downgraded. Falls back to loopback when no tailnet address exists, as OpenClaw does |
| `lan` | One any-address (dual-mode) listener on `{port}` | Downgraded. `0.0.0.0` and `[::]` on one port fails on Windows, so Huddle binds `ListenAnyIP` once |
| `auto` | `0.0.0.0` inside a container (`DOTNET_RUNNING_IN_CONTAINER`), loopback elsewhere | OpenClaw's container default |
| `custom` | `CustomBindHost:{port}` | |

- **Port.** The app's own port. A non-loopback bind on the installed app's `127.0.0.1:0` needs `Team:Gateway:Port`;
  without it Huddle starts loopback-only and says so.
- **Refuse open binds.** A non-loopback bind with `Auth:Mode = none` does not bind; it starts loopback-only, and the
  Security check shows `gateway.bind_no_auth` as critical.
- **The managed listener.** When `Tailscale:Mode` is `serve` or `funnel`, Huddle adds `127.0.0.1:0`. Kestrel writes the
  bound port back into the `ListenOptions` endpoint, so `GatewayListener` records it and hands it to the Tailscale
  controller and to the classifier. The port is read after start (`StartedAsync`); the first `KestrelAppHost` test
  proved Kestrel writes it back. `GatewayListener` (singleton) records the bound managed `IPEndPoint`, and
  `KindOf(ConnectionInfo)` returns `Managed` when the connection's local address (IPv4-mapped form normalised) and port
  match it, else `App`. There is no `ListenOptions.Use` and no listener feature.
- **The listener plan.** `GatewayListenerPlan` is pure and Domain-tested. Calling `Listen*` in code makes Kestrel
  ignore every configured URL, so the plan re-applies and de-duplicates them. Sources, in order: `Configuration["urls"]`
  (covers `ASPNETCORE_URLS`, `DOTNET_URLS`, `--urls` and launch settings), else `http_ports` and `https_ports`
  (`*:{port}`), else `http://localhost:5000`. Each is parsed with `BindingAddress.Parse`: `localhost` →
  `ListenLocalhost(p)`; an IP literal → `Listen(ip, p)`; `*`, `+` or another name → `ListenAnyIP(p)`; `https` adds
  `UseHttps()`; `unix:` and `pipe:` make remote access unavailable (logged, loopback-only). `ListenLocalhost(0)` throws,
  so the plan never emits it. A configured loopback URL on the same port as an any-address bind is dropped. `tailnet`
  adds `Listen(100.x.y.z, AppPort)`, the address taken from the machine's unicast addresses in `100.64.0.0/10`. Each
  non-zero port is probed with an `ExclusiveAddressUse` socket, disposed at once; a busy port starts the app
  loopback-only with the reason recorded (the race between probe and bind is accepted). `GatewayListener` makes no
  `Listen*` call only when `Bind = loopback`, Tailscale is off and desktop mode is off, so Kestrel binds exactly as today.
- **Desktop mode has a stable port.** The installer binds `127.0.0.1:0`, which would change the browser's origin, and so
  its device key, on every launch. In desktop mode the plan replaces `127.0.0.1:0` with
  `127.0.0.1:{DesktopPortChoice.Choose(stored, IsFree)}`: the first port Huddle got is stored in `gateway_settings`
  (`desktop_port`) and bound on the next start, falling back to `0` when busy (and stored again).
  `Team:Gateway:Port`, when set, wins; a non-loopback bind in desktop mode still needs it. Devices paired through
  loopback or bootstrap and unseen for 30 days are deleted by housekeeping.
- `GatewayListener.AppUrl` is the first app endpoint, an any-address mapped to `127.0.0.1`. `DesktopLifetimeService.Publish`
  uses it when set, else `IServerAddressesFeature` skipping the managed endpoint, so the instance record and the browser
  opener always use the app listener. The dev console skips the managed endpoint too.
- A busy port or a missing tailnet address starts the app loopback-only and reports why. Startup never fails over it.

**Constraints.** The first non-loopback bind triggers the Windows or macOS firewall prompt; the section says to allow
**Private networks**.

### 6.3 `TailscaleController`

**Purpose.** What OpenClaw's `src/infra/tailscale.ts` does, through the Tailscale CLI.

| Operation | Command | Notes |
| --- | --- | --- |
| Status | `tailscale status --json` | `BackendState`, `Self.DNSName`, `Self.TailscaleIPs`, the node owner. 3 attempts 500 ms apart (OpenClaw's constants). The JSON format is undocumented ("subject to change"); tests pin a captured sample |
| Claim | `tailscale serve --bg=false --yes --https={tsPort} http://127.0.0.1:{managed}`; Funnel: `tailscale funnel --bg=false --yes --https={tsPort} …` | A foreground child process holds the route. 15 s start timeout. If the output says "listener already exists for port N", stop with **Tailscale port N belongs to something else** (OpenClaw's `TailscaleRouteOwnershipConflictError`), never take it over |
| Release | Kill the child's process tree at once (`Process.Kill(entireProcessTree: true)`; Windows has no Ctrl-C) | No `tailscale serve reset`, ever. On Windows the child is assigned to a Job object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` (`[LibraryImport]`, which needs `AllowUnsafeBlocks` in `Huddle.App.csproj`), so a Huddle crash does not orphan it. That the route disappears when the child dies is checked by hand (manual tests) |
| Restart | On an unexpected exit, retry with backoff from 1 s to 1 min | As OpenClaw |
| Who | `tailscale whois --json {ip}` | `UserProfile.LoginName`, `Node` (tags). Cached per `IPAddress` for 60 s, failures for 5 s |
| Stray routes | `tailscale serve status --json` every 30 s | Routes that point at Huddle's **app** port were set up by hand; listed in the Security check |

- **Tailscale HTTPS port:** `Team:Gateway:Tailscale:HttpsPort`, else the app's own fixed port (copies on 5101 and 5102
  get `…ts.net:5101` and `:5102`), else Tailscale's default. Funnel accepts only 443, 8443 and 10000.
- The CLI is `tailscale` on `PATH`, else the platform default path, else `Team:Gateway:Tailscale:Command`. All calls go
  through `ITailscaleCli`: `RunAsync(args, timeout, ct)` returns exit code, stdout and stderr; `StartAsync(args, ct)`
  returns an `ITailscaleChild` (`Id`, `Lines`, `Exited`, `KillAsync()`), so tests fake both with channels. The child runs
  with `UseShellExecute=false`, `CreateNoWindow=true` and all three streams redirected, stdout and stderr read into one
  channel. The claim succeeds when `tailscale serve status --json` shows `tsPort → http://127.0.0.1:{managed}` within
  15 s; it fails on a line containing `listener already exists for port` or on the enable-HTTPS link.
- `TailscaleServeClaim` owns the child, so it is `IAsyncDisposable`.

**Constraints.** `Auth:AllowTailscale` applies only to routes Huddle created. A hand-made route never gets
tokenless auth (OpenClaw: "`allowTailscale` is ignored on externally managed Serve routes").

### 6.4 Host filtering

**Purpose.** ASP.NET Core's host filter as a second line behind the origin check.

- **Phase 0, ships alone and now:** `AllowedHosts` in `appsettings.json` becomes `localhost;127.0.0.1;[::1]` (it is
  `*` today). The installer already sets `localhost;127.0.0.1`.
- While bound beyond loopback: add the bound addresses, `{name}.local` (not the bare machine name),
  `CustomBindHost`, and `Team:Gateway:ExtraHosts`. An any-address bind adds every non-loopback unicast address (IPv6
  bracketed, no zone), refreshed on `NetworkChange.NetworkAddressChanged`. A default port is stripped before comparison.
- The dynamic hosts are added in `services.PostConfigure<HostFilteringOptions>`, not `Configure`, so they extend what
  `AllowedHosts` produced; the trailing dot of `Self.DNSName` is stripped.
- While Serve or Funnel runs: add the MagicDNS name. Serve keeps the browser's original `Host`
  (`desktop-a.tail1234.ts.net:5101`; Tailscale's `serve.go` sets `r.Out.Host = r.In.Host`). The name is only known
  after `tailscale status`, which runs after start, so the list is fed through an
  `IOptionsChangeTokenSource<HostFilteringOptions>` that the controller signals. `HostFilteringMiddleware` reads
  `IOptionsMonitor` and follows the change.

### 6.5 Gateway auth and the rate limiter

**Purpose.** OpenClaw's `gateway.auth`.

| `Auth:Mode` | The connection presents | Note |
| --- | --- | --- |
| `token` (default) | The gateway token, or a device token, or a bootstrap token | |
| `password` | The gateway password, or a device token | Required for Funnel |
| `trusted-proxy` | Nothing; identity comes from the proxy (§6.10) | |
| `none` | Nothing | **Loopback only**. Device identity is still required (P-2). Security check: `gateway.loopback_no_auth`. The hosted test factory uses it (§C) |

- Over the managed listener with `AllowTailscale` (default true in Serve mode), a verified Tailscale identity **stands
  in for gateway auth**, not for device identity. Funnel traffic never has an identity and always needs the password.
- **The credential a handshake may present**, in OpenClaw's client order: the device token for this device, else the
  gateway token (or password), else a bootstrap token.
- **`AuthRateLimiter`** is OpenClaw's `auth-rate-limit.ts`: keys `{scope}:{ip}` with scopes `shared-secret`,
  `device-token`, `bootstrap-token`, `device-signature`, `pairing`; **10 failures in 60 s → locked out 5 minutes**,
  answered with 429 `AUTH_RATE_LIMITED` and `retryAfterMs`. Loopback is exempt from lockout but each failure adds a
  progressive delay (250 ms doubling, at most 5 s; **not in OpenClaw**, which does not document the curve). For
  browser-origin failures the key is `browser-origin:{origin}` and loopback is not exempt. At most 10,000 entries,
  pruned every 60 s. The client address is the socket's, or the managed listener's `X-Forwarded-For`, or a trusted
  proxy's.
- **Pre-auth connections:** at most 128 concurrent unauthenticated requests per address; more get 503. Loopback is not
  exempt.

### 6.5a Strict local (default)

**Purpose.** A switch that turns sign-in off and serves only this computer. **Not in OpenClaw**: the owner decided it
on 2026-10-30, and it is on by default (`Team:Gateway:StrictLocal`, default `true`; an admin-saved override wins).

While it is on:

- **The gate admits only the Loopback ingress** (§8.6): a direct loopback socket with no forwarding headers. Every other
  request gets 403 `strict_local`, whatever Bind, Tailscale or trusted proxies say. Host filtering and the Origin check
  stay as they are, so DNS rebinding is still stopped.
- **No sign-in.** A loopback request gets the host principal with no session: `huddle:viewer=host`,
  `huddle:ingress=loopback`, every scope including `operator.admin`, `NameIdentifier` `local`. There is no `/auth`
  redirect and no device identity. The hub guard does not abort host-principal connections.
- **`/auth`** redirects to `/` (or the safe `return` path). `/auth/connect` and `/auth/challenge` answer 403
  `strict_local` for non-loopback callers. `/auth/status` reports `authenticated: true` for a loopback caller.
- **Remote options are forced off at startup:** Bind is loopback, Tailscale mode is off, trusted proxies are ignored.
  The listener plan behaves as for loopback, and `IRemoteAccessState.IsConfigured` is false, so Open and Browse stay
  available (D-5).
- **The launcher, the second-launch hand-off, `dashboard` and the dev console** open or print the plain address with no
  `#bootstrapToken`. `/_desktop/*` and `/_gateway/*` keep their own loopback and token checks; `devices` and
  `auth-token` still work.
- **The Security check** raises one Info item, `gateway.strict_local`, and none of the remote-exposure items (§8.9).

**Switching it on** (admin, from Settings) takes effect at once: the gate refuses non-loopback requests, every session
ends and every device's Blazor connection aborts. Listeners stay bound until the next start but serve nothing; the
section says "Restart Huddle to close the network listeners." while one is still running.

**Switching it off** is saved and applies at the next start, because the bind and the auth model change. Only the host
browser can do it, because it is the only browser Strict local lets in.

**Settings.** The **Strict local** switch sits at the top of the Gateway card (§6.12). While it is on, the other
Gateway controls are disabled, Pair a device and Waiting for approval are not drawn, and one line says "Turn off Strict
local to set up remote access." The Devices list stays.

**Cost.** Any program or other user account on this computer can use Huddle with no secret. That is the trust Huddle had
before remote access existed.

### 6.6 Device identity and the connect handshake

**Purpose.** OpenClaw's challenge-signed device identity, adapted to a server-rendered app.

**The browser side** (`wwwroot/auth.js`, a plain script; the `/auth` page is not a Blazor page, D-6):

- On first use, a 32-byte seed from `crypto.getRandomValues`, the public key from `@noble/ed25519`
  (`getPublicKeyAsync`), both base64url without padding, stored in `localStorage` under
  `huddle-device-identity-v1` as `{version, deviceId, publicKey, privateKey, createdAtMs}`. `deviceId` is the SHA-256
  hex of the public key (with `crypto.subtle` when present, else `@noble/hashes`). Pure JS, so it works on plain HTTP,
  as OpenClaw's does. If storage is blocked, the identity lives for the page only.
- Device tokens are stored in `localStorage` under `huddle-device-token-v1`:
  `{version, deviceId, role, scopes, token, updatedAtMs}`, and used only when `deviceId` matches.
- A gateway token typed on the connect page is kept in **sessionStorage** for that tab and origin only, never in
  localStorage. A password is kept in memory only. Both as OpenClaw.
- `@noble/ed25519` and `@noble/hashes` (MIT) are vendored into `wwwroot/lib/noble/` like `codemirror` and
  `emoji-picker`.

**The handshake.**

```text
GET  /auth/challenge                      → { nonce (32 bytes, base64url), ts (server ms) }
POST /auth/connect { deviceId, publicKey, signature, signedAt, nonce, platform, deviceFamily,
                     role: "operator", scopes: [...], auth: { token | password | deviceToken | bootstrapToken } }
```

The signed payload is OpenClaw's v3 format, with Huddle's client id:

```text
v3|{deviceId}|huddle-web|webchat|operator|{scopes comma-joined}|{signedAtMs}|{credential or ""}|{nonce}|{platform}|{deviceFamily}
```

**The server** checks, in order, each failure counted by the rate limiter and answered with OpenClaw's code:

1. The nonce was issued by this gateway, is unused and is under 60 s old (**not in OpenClaw**: its window is not
   documented) → else `DEVICE_AUTH_NONCE_REQUIRED` or `DEVICE_AUTH_NONCE_MISMATCH`.
2. `signedAt` within ±60 s of the server clock → else `DEVICE_AUTH_SIGNATURE_EXPIRED`.
3. `deviceId == hex(SHA-256(publicKey))` → else `DEVICE_AUTH_DEVICE_ID_MISMATCH`; a valid 32-byte key → else
   `DEVICE_AUTH_PUBLIC_KEY_INVALID`.
4. The Ed25519 signature over the payload → else `DEVICE_AUTH_SIGNATURE_INVALID`. .NET has no Ed25519 in the base
   library, so verification uses **BouncyCastle.Cryptography** (MIT, managed; new in `Directory.Packages.props`).
5. Gateway auth (§6.5): the credential, or Tailscale identity, or trusted proxy.
6. Pairing (§6.7): paired → continue; auto-approvable → pair now; otherwise → 200
   `{ "code": "PAIRING_REQUIRED", requestId, deviceId }`.
7. Issue a **session** (§6.8) and, on first pairing or rotation, a **device token**; answer
   `{ deviceToken?, scopes, return }` with `Set-Cookie`.

**The connect page.** `GET /auth` shows: "Connect to Huddle on DESKTOP-A (port 5101)". If the browser holds a device
token, it connects without showing anything and returns to `return`. Otherwise, a **Gateway token** (or **Password**)
field and **Connect**, unless a `#bootstrapToken=` or `#token=` fragment provides one (the fragment is read, then
cleared with `history.replaceState`). Over verified Tailscale Serve: "Signed in to Tailscale as `ada@example.com`" and
no field. While pairing: "Waiting for approval… Approve this device in Settings › Remote access on a device with admin
access, or run `Agency.Huddle.App.exe devices approve`", the request id, and **Check now**; it also polls
`/auth/pairing/{requestId}` every 2 s. Rejected or expired: OpenClaw's wording, and no automatic retry.

### 6.7 Pairing, roles and scopes

**Purpose.** OpenClaw's device pairing for the operator role.

**Scopes** (the Huddle subset of OpenClaw's):

| Scope | Lets a session | Huddle meaning |
| --- | --- | --- |
| `operator.read` | See | Granted with `operator.write` in V1: Huddle has no read-only mode (H-2) |
| `operator.write` | Use the app | Rooms, Agents, Tasks, Library: everything outside Settings |
| `operator.pairing` | Approve and reject devices with scopes it holds | |
| `operator.admin` | Everything; implies every scope | All of `/settings/*`, including Remote access; approving admin requests |

A device **without** `operator.admin` has **limited access**.

**Pairing requests.** A handshake from an unpaired device that is not auto-approved opens a request holding the device
id, public key, requested scopes, name (from `platform` and `deviceFamily`, editable later), address, user agent,
ingress and Tailscale login if any. It expires after **5 minutes**. A new handshake from the same device with different
scopes or key supersedes it with a new request id. At most 10 pending requests (**not in OpenClaw**); beyond that,
`PAIRING_QUEUE_FULL`. A request posts `PairingRequested` to the Feed as a Notification.

**Auto-approval** (OpenClaw's rules):

| Connection | Pairs automatically? | Scopes |
| --- | --- | --- |
| Loopback: direct socket, `127.0.0.1` or `::1`, no forwarded headers, `Host` and `Origin` loopback, gateway auth passed | Yes (`AutoApproveLocal`, default true; OpenClaw's `autoApproveLocal` kill switch) | Admin |
| Bootstrap token from the desktop launcher or `dashboard` | Yes, bound to the device that redeems it | Admin |
| Managed Serve with a verified Tailscale identity and `AllowTailscale` | Yes | Limited (**not in OpenClaw**: it does not document the scopes) |
| Setup link (QR) | No: needs approval | Limited at most |
| Tailnet bind, LAN bind, custom bind, Funnel, same-host tailnet or LAN address | No: needs approval | Requested, capped by the approver |
| Trusted proxy | Only with `TrustedProxy:DeviceAutoApprove:Enabled`, capped by `TrustedProxy:DeviceAutoApprove:Scopes` (default `operator.read, operator.write`) | |

**Approval.** From Settings › Remote access (an admin session, which holds `operator.pairing`), or
`Agency.Huddle.App.exe devices approve <requestId>` on the host. An approver can grant only scopes it holds; admin
requests need an admin approver. **Reject** creates no permanent ban, as in OpenClaw.

**Request admin.** A limited device's Settings page offers **Request admin**: a new handshake asking for
`operator.admin` opens a request (`AdminRequested` in the Feed) that an admin approves.

**Revoke, remove, rotate.** Revoke removes a role's token; Remove deletes the device; Rotate issues a new device token
within the approved scopes. Each **ends the device's sessions and aborts its Blazor connections** (§6.8).

### 6.8 Sessions, the gate and revocation

**Purpose.** Carry one handshake's result to every request a Blazor app makes.

**Why sessions, not per-request signatures.** OpenClaw authenticates one WebSocket per connection. A Huddle page is a
prerendered HTTP GET, then static assets, then `/_blazor` negotiate and a WebSocket. Browsers send cookies on all of
them and can attach nothing else to a WebSocket. So the handshake ends in an HttpOnly **session cookie**, and the
session is Huddle's equivalent of OpenClaw's authenticated socket.

- **Cookie:** `huddle_session_{instanceId}` = 32 random bytes, base64url; HttpOnly, `SameSite=Lax`, `Path=/`, `Secure`
  over Serve and Funnel, no `Max-Age` (it ends with the browser). The server keeps SHA-256 → `{deviceId, scopes,
  ingress, tailscaleLogin, createdAt, lastSeen}` in memory.
- **Lifetime:** 12 hours, then the next HTML request redirects to `/auth`, which reconnects silently with the stored
  device token. A restart drops all sessions the same way. (**Not in OpenClaw**, which has no session concept.)
- **The gate** (`GatewayGate` + the `HuddleGateway` authentication scheme, registered first in `Program.cs`): the
  origin check (§8.7), the classifier (§8.6), then: `/auth`, `/auth/*`, `auth.js`, `auth.css` and the vendored noble
  files pass unauthenticated; `/_gateway/*` is loopback-only and checks the gateway token in `Authorization: Bearer`;
  `/health` is loopback-only and unauthenticated (liveness for `test-health.ps1`); `/_desktop/*` passes without a
  session **only when the classifier says Loopback** (else 403) and keeps its own instance-token check
  (`X-Huddle-Token`); everything else needs a valid session. A session from the managed listener must still arrive
  there with the same verified Tailscale login it was issued to. Otherwise, an HTML `GET` → `302 /auth?return={path}`
  (a local path: starts with `/`, not `//`, query kept); anything else → 401.
- **Middleware order.** `WebApplication` inserts `UseRouting`, `UseAuthentication` and `UseAuthorization` ahead of
  user middleware unless the code calls them, so `Program.cs` calls them explicitly, in this order, before
  `UseAntiforgery`: `UseGatewayGate()` (origin check, classifier, pre-auth limit; the ingress goes into
  `HttpContext.Items`), `UseAuthentication()`, `UseAuthorization()`, `UseGatewaySessionRequirement()` (302 `/auth` for an
  HTML GET, else 401), `UseAntiforgery()`. The `HuddleGateway` handler reads the ingress from `HttpContext.Items`.
  The classifier runs once, in the gate, with `GatewayOptions.TrustedProxies`.
- **The principal** carries `ClaimTypes.NameIdentifier` = the device id (antiforgery binds to it), `huddle:session` =
  the session hash, `huddle:scope` (one per scope) and `huddle:ingress`.
- **Effective scheme.** After classification, for managed ingress, or trusted-proxy ingress with a valid
  `X-Forwarded-Proto`, the gate sets `Request.Scheme` from it, so the session cookie is `Secure` over Serve and Funnel
  and redirects are `https`. `RemoteIpAddress` is left alone. §8.8 uses this effective scheme.
- **A dead session must not strand a tab.** `GET /auth/status` (unauthenticated) returns `{ "authenticated": bool }`.
  `ReconnectModal.razor.js` calls it in its `failed` branch and both `catch` blocks, and when the answer is false goes to
  `/auth?return=` plus the current path and query.
- **Scopes in the app.** The scheme puts `huddle:device`, `huddle:scope` claims and `huddle:ingress` on the principal.
  Blazor carries them into the circuit through `ServerAuthenticationStateProvider`. `Settings.razor` renders only for
  `operator.admin`; otherwise it shows "Settings need admin access" and **Request admin**. Services that change remote
  access check the scope themselves.
- **Revocation disconnects** (OpenClaw's fix for CVE-2026-34503): a global `IHubFilter`, `GatewayConnectionGuard`,
  remembers each connection's device in `OnConnectedAsync` and calls `Abort()` on all of them when the device is
  revoked, removed or rotated. `InvokeMethodAsync` re-checks to close the race. Revocation also deletes the device's
  sessions. `GatewayConnectionGuard` is a singleton; it stores the session hash per connection and, in
  `InvokeMethodAsync`, re-runs the in-memory §8.5 checks (expiry, device still paired, token not rotated; no whois) and
  aborts on failure. **Confirmed by the slice 4 spike** (raw Blazor protocol on `TestServer`): a global `IHubFilter`
  reaches `ComponentHub`, and `Abort()` closes the socket in about 1.3 s. `OnConnectedAsync` can run after the handshake
  reply, so tests poll `GatewayConnectionGuard.ConnectionCount(deviceId)`. The `CircuitHandler` fallback was not needed.
  Housekeeping's `PruneStale` does not notify the guard: pruned devices are 30 days unseen and their sessions are
  already gone.

### 6.9 The dashboard handoff, bootstrap tokens and setup links

**Purpose.** OpenClaw's `openclaw dashboard`: open a browser already authenticated, without the shared token in a URL.

- A **bootstrap token** is 32 random bytes, single use, valid **10 minutes**, and **bound to the first device identity
  that redeems it**: a second device, or another browser profile, cannot use it. It is carried in the URL **fragment**,
  `#bootstrapToken=…`, which never reaches a server log, and cleared after load.
- **The desktop launcher** (`DesktopLifetimeService`) mints an admin bootstrap token at start and opens
  `http://127.0.0.1:{port}/auth?return=%2F#bootstrapToken=…`. Every bootstrap URL (launcher, hand-off, `dashboard`, the
  dev console) opens `/auth`, never `/`. A **second launch** (`DesktopHandOff`) asks the running instance for a
  fresh one through `POST /_desktop/bootstrap`, authenticated like `/_desktop/stop` with the **instance token** from
  `instance.json`, then opens that URL. If that call fails (an older copy, 401, 404 or 405), it opens `{url}/auth`
  instead. If `auth.js` holds a device token while a bootstrap fragment is present, it connects with the device token
  and posts the bootstrap token to `/auth/bootstrap/burn` so it cannot be used later. A bootstrap token is accepted in
  password mode too, because the launcher always uses one.
- **`Agency.Huddle.App.exe dashboard`** is desktop-only: it finds the running copy through `instance.json` in its
  `{DataDir}` and prints the URL with `--json`. With no running desktop copy it prints "No desktop copy is running." and
  exits 1. `devices …` reads `instance.json`, or takes `--url http://127.0.0.1:{port}` for a `dotnet run` copy. The
  launcher sets `Team__DataDir`; the bare exe does not, so user-facing text says `huddle devices approve …`.
- **Development runs** (`dotnet run`, no desktop mode) write `Huddle is ready: http://localhost:5100/#bootstrapToken=…`
  to the console with `Console.Out`, never through `ILogger`, so the file log never holds it.
- **Setup links** are Huddle's browser version of OpenClaw's setup codes. **Pair a device** in Settings (admin) mints a
  **limited** bootstrap token and shows it as a QR code and a link for the current address:
  `https://desktop-a…ts.net:5101/auth#bootstrapToken=…` over Serve, or `http://192.0.2.20:5101/auth#…` on a LAN
  bind. As with OpenClaw's setup codes, a setup link to a non-loopback address must be `https` unless the host is a
  private IP literal, `.local` or `.ts.net`. Redeeming it **opens a pairing request**; it never auto-approves. QR codes
  are rendered with **QRCoder** (MIT) as a PNG data URI.

### 6.10 Trusted-proxy auth

**Purpose.** OpenClaw's `trusted-proxy` mode, for someone who already runs an identity-aware proxy.

- `Auth:Mode = trusted-proxy` and `TrustedProxies` lists the proxy's source addresses. A request from one of them must
  carry a non-loopback `X-Forwarded-For` (the proxy must rebuild it) and the user header named in
  `TrustedProxy:UserHeader`; otherwise 403 `proxy_attribution_required`.
- Gateway auth is satisfied by the proxy; **device identity is still required**, and pairing follows
  `TrustedProxy:DeviceAutoApprove` (§6.7). The origin check still applies (`trusted_proxy_origin_not_allowed`).
- The classifier takes `requireProxyAttribution` and `userHeader`: a trusted-proxy source in `trusted-proxy` mode needs a
  single non-loopback `X-Forwarded-For` and a non-empty user header, else `Rejected(proxy_attribution_required)`. The
  result carries `ProxyUser`.
- A loopback request in this mode may still use a bootstrap or device token, so the launcher is not locked out.
- No Tailscale identity is believed through a trusted proxy (no `whois`).
- Listing `127.0.0.1` means same-host processes may supply attribution; the Security check warns.
- `AllowRealIpFallback` (default false) stays false; Huddle never reads `X-Real-IP` as identity.

### 6.11 Funnel

- `Tailscale:Mode = funnel` **is refused unless `Auth:Mode` is `password`**: the Funnel claim is not made and the
  section reports **Funnel needs password auth**; the app itself still starts.
- When the ingress is Funnel, the password check is mandatory: a device token or bootstrap token never replaces it.
- Funnel requests are recognised by Tailscale's `Tailscale-Funnel-Request: ?1` header on the managed listener. They have
  no identity, so they always present the password, are rate-limited per `X-Forwarded-For`, and always need approval.
- A tailnet peer using the Funnel URL arrives with an identity but still presents the password, as in OpenClaw.
- Ports 443, 8443 or 10000. The Security check shows `gateway.tailscale_funnel` as **critical** while it runs.

### 6.12 Settings › Remote access (`G10`)

**Purpose.** OpenClaw's Control UI gateway settings and Devices panel. Admin only.

- **Gateway:** Bind, Auth mode, token (**Show** on loopback only, **Rotate**), password (set or change), Tailscale mode,
  Tailscale HTTPS port, **Use Tailscale sign-in**, trusted proxies, allowed origins, public origin. Each change says
  whether it applies now or at the next start, and the section shows the stored and running values side by side.
- **Status:** what is running, at which URLs, and the next step when something is wrong (Tailscale missing or signed
  out, HTTPS off for the tailnet with Tailscale's link, port conflict, needs a port, bind refused without auth).
- **Waiting for approval:** each pairing and admin request with the device name, address, ingress, Tailscale login
  (marked **You** when it is the node owner's, else **Shared user**), the requested scopes, a countdown, **Approve**
  (with a scope choice capped at the approver's) and **Reject**.
- **Pair a device:** the setup link and QR (§6.9).
- **Devices:** name (rename), scopes (**Limited** or **Admin**), paired via, last address, last seen, **This device**,
  **Rotate**, **Revoke**, **Remove** (inline confirm, as `ResetAllControl`).
- **Security check:** the OpenClaw audit items that apply (§8.9), each with its severity and fix.

A limited device sees "Settings need admin access" and **Request admin** instead of every Settings section.

**Implementation notes.** `SettingsTab.RemoteAccess` (`/settings/remoteaccess`, "Remote access",
`Icons.Material.Outlined.Devices`) after Library; `RemoteAccessPanel` in `Components/Settings/RemoteAccess/`, built from
`SettingsIntro`, `StoredAt`, `MudSelect`, `MudSwitch`, `MudNumericField`, `MudPaper` Outlined cards as `HelperCard` and
the inline confirm of `ResetAllControl`. Every change goes through `GatewayAdminService`, which checks
`operator.admin` itself.

### 6.13 CLI verbs

Like `stop`, these run before `WebApplication.CreateBuilder` (`Program.cs` already has `args is ["stop"]`).

| Verb | Does | Talks to |
| --- | --- | --- |
| `auth-token --show` | Prints the resolved gateway token. Refuses when output is redirected | `{DataDir}` and configuration, no running app needed |
| `auth-token --rotate` | Replaces the stored token | `{DataDir}`; a running copy picks it up on its next read |
| `dashboard [--json]` | Mints a bootstrap token in the running copy and opens the browser | `POST /_desktop/bootstrap` with the instance token |
| `devices list` | Devices and pending requests | `GET /_gateway/devices`, loopback, `Authorization: Bearer {gateway token}` |
| `devices approve <requestId> [--scope …]` / `reject <requestId>` | OpenClaw's: with no id it only previews and exits 1 | `POST /_gateway/devices/requests/{id}/…` |
| `devices revoke` / `remove` / `rotate --device <id>` | As §6.7 | `POST /_gateway/devices/{id}/…` |

### 6.14 Feed items

| Source type | When | Notify |
| --- | --- | --- |
| `PairingRequested` | A pairing request opens | **Yes**, resolved when approved, rejected or expired |
| `AdminRequested` | A limited device asks for admin | **Yes**, resolved likewise |
| `DevicePaired` | Approved or auto-approved from a non-loopback ingress | No (Activity) |
| `DeviceRevoked` | Revoked, removed or rotated | No (Activity) |

Each needs an entry in `FeedSourceType`, its wire name in `FeedSourceTypes.ToWire`, a `FeedPayload` record registered
in `FeedPayloads.TypeOf`, and a Category (OP-6). Posted through `IFeedPublisher` with the actor `Huddle`
([ADR-0065](adr/0065-huddle-posts-as-itself.md)).

### 6.15 What changes elsewhere in Huddle

- **The Open action** ([rules.md](engineering/rules.md) row "The Open action launches a process on the server") must be
  "removed rather than gated" if Huddle is ever used remotely. It is therefore **not rendered, and its handler refuses,
  whenever `Bind` is not `loopback` or `Tailscale:Mode` is not `off`**, for every session. With remote access off it
  is unchanged. The same applies to the Library's open and reveal actions and `FolderPickerDialog`. (A narrower rule,
  "only for non-loopback sessions", needs rules.md amended first; OP-7.)
- **`/_desktop/*` endpoints** accept only the loopback ingress as the classifier decides it. A managed-listener request
  also arrives from `127.0.0.1`, so a bare `IPAddress.IsLoopback` check is no longer enough anywhere.
- **The hosted tests** (`TeamWebApplicationFactory`) run with `Auth:Mode = none` (§C).

## 7. Data model and storage

### 7.1 `team.db` tables

```sql
CREATE TABLE IF NOT EXISTS gateway_settings (
    id            INTEGER PRIMARY KEY CHECK (id = 1),
    instance_id   TEXT NOT NULL,          -- 8 lowercase hex, set once
    token         TEXT NULL,              -- the generated gateway token (48 hex); NULL when configuration supplies it
    password_hash BLOB NULL,              -- salt || PBKDF2-SHA256, when set in Settings
    overrides     TEXT NOT NULL,          -- JSON: the Team:Gateway keys an admin saved (Appendix A)
    updated_at    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS gateway_devices (
    device_id       TEXT PRIMARY KEY,     -- SHA-256 hex of the public key
    public_key      BLOB NOT NULL,        -- 32 bytes
    name            TEXT NOT NULL,        -- 1..60 chars, untrusted
    scopes          TEXT NOT NULL,        -- comma-separated, sorted
    token_hash      BLOB NULL UNIQUE,     -- SHA-256 of the current device token; NULL after revoke
    paired_via      TEXT NOT NULL,        -- loopback | bootstrap | tailscale | approval | trusted-proxy
    tailscale_login TEXT NULL,
    paired_at       TEXT NOT NULL,
    last_seen_at    TEXT NOT NULL,
    last_seen_from  TEXT NOT NULL,
    user_agent      TEXT NULL
);
```

The token is stored in clear because Huddle must present it to `auth-token --show` and compare it; it lives in the
same file as everything else the Human owns, as OpenClaw's lives in `openclaw.json`. Device tokens are stored hashed.

### 7.2 In memory

| Store | Holds | Lost on restart |
| --- | --- | --- |
| Nonces | Issued challenges, 60 s | Yes |
| Pairing requests | §6.7, 5 minutes, at most 10 | Yes; waiting pages show "expired" |
| Bootstrap tokens | Hash → {scopes, bound device, expiry} | Yes |
| Sessions | Hash → device, scopes, ingress, login | Yes; browsers reconnect silently |
| Rate limiter | `{scope}:{ip}` counters | Yes |
| Connection guard | Device → hub connections | Yes |

Registries take an optional trailing `TimeProvider? timeProvider = null`, defaulting to `TimeProvider.System`. Records
that change state (`PairingRequest`) are classes mutated only under the registry's `Lock`.

### 7.3 Browser storage (per origin, so per copy)

| Key | Store | Holds |
| --- | --- | --- |
| `huddle-device-identity-v1` | localStorage | The device key pair and id |
| `huddle-device-token-v1` | localStorage | The device token, role and scopes |
| `huddle-gateway-token` | sessionStorage | A typed gateway token, this tab only |

### 7.4 Cookies

| Cookie | Lifetime | Attributes |
| --- | --- | --- |
| `huddle_session_{instanceId}` | Browser session; server-side 12 h | HttpOnly, `SameSite=Lax`, `Path=/`, `Secure` over HTTPS |

There is one cookie. The device token stays in local storage, as in OpenClaw, and travels only in the handshake body.

## 8. Core algorithms

### 8.1 Ports

```text
AppPort         = Team:Gateway:Port ?? the single non-zero configured port ?? null (a non-loopback bind needs one)
TailscalePort   = Tailscale:HttpsPort ?? AppPort ?? Tailscale's default
Funnel and TailscalePort ∉ {443, 8443, 10000} → refuse
```

### 8.2 Generating secrets

Gateway token: 24 bytes → 48 lowercase hex. Device token, session, bootstrap token, nonce: 32 bytes → base64url.
All from `RandomNumberGenerator`. Compared as SHA-256 hashes with `CryptographicOperations.FixedTimeEquals`.

### 8.3 Connect

```text
POST /auth/connect:
  limiter.Check(scope of the credential, client address)         → 429 AUTH_RATE_LIMITED
  nonce, signedAt, deviceId, public key, signature (§6.6 1-4)     → DEVICE_AUTH_* ; limiter.Fail
  gateway auth: managed+identity+AllowTailscale | trusted proxy | credential matches → else limiter.Fail, 401
  device = registry.Find(deviceId)
  device is paired and the credential is its device token (or gateway auth passed) and requested ⊆ device.scopes
        → session
  requested ⊄ device.scopes or device unknown:
        auto-approve rule (§6.7) applies → pair or widen; session
        bootstrap token → mark used, bind to deviceId; admin bootstrap → pair as admin; setup link → request
        otherwise → request (or supersede) → PAIRING_REQUIRED
  session: cookie; deviceToken when newly paired or rotated; DevicePaired to the Feed when not loopback
```

### 8.4 Waiting and resolution

`GET /auth/pairing/{requestId}` returns `pending`, `approved`, `rejected` or `expired`. On `approved`, the page runs the
handshake again; approval alone grants nothing, as OpenClaw's `device.pair.resolved` does not.

### 8.5 Checking a session

```text
cookie → SHA-256 → sessions.TryGet; missing or older than 12 h → none
device still paired and token not rotated since the session began → else none
session.ingress is managed and session.TailscaleLogin is set:
    whois(X-Forwarded-For).LoginName must equal it (ignoring case) → else none
Funnel request and the session was not issued over Funnel with the password → none
touch lastSeen (written to team.db at most once a minute)
```

### 8.6 Classifying the ingress

```text
listener is Managed:
    X-Forwarded-For (one non-loopback address), X-Forwarded-Proto, X-Forwarded-Host all present
        → else 403 proxy_attribution_required
    funnel   = header Tailscale-Funnel-Request == "?1"
    identity = !funnel and Tailscale-User-Login present and whois(XFF).LoginName equals it ? login : none
    → Managed(client = XFF, identity, funnel)
socket address is loopback (IPv4-mapped normalised) or null:
    any of X-Forwarded-For, Forwarded, X-Real-IP, Tailscale-User-* present
        → client in TrustedProxies ? TrustedProxy : 403 proxy_attribution_required
    → Loopback
socket address in TrustedProxies → TrustedProxy (§6.10)
→ Direct(socket address)
```

`null` is the in-memory test server, named pipes and Unix sockets, and is treated as loopback, as
`DesktopStopEndpoint` already does.

### 8.7 Origin check

OpenClaw's `checkBrowserOrigin`, run for every request that carries `Origin`. `Origin` is **required** only on non-GET
`/_blazor` and `/auth/*` requests and on WebSocket upgrades; browsers send none on same-origin GETs (the `/auth` page,
`GET /auth/status`, Blazor long-poll GETs), so an absent `Origin` on GET, HEAD or OPTIONS passes. Test WebSocket clients
set `Origin` explicitly.

1. Behind remote ingress (managed or trusted proxy) with `PublicOrigin` set: only an exact `PublicOrigin` match passes.
2. Parse strictly (`Uri.TryCreate`): `null`, any path other than `/`, queries, fragments and credentials are refused.
3. `AllowedOrigins` (lower-cased): an exact match or `*` passes. When unset, it defaults to `PublicOrigin`.
4. `DangerouslyAllowHostHeaderOriginFallback`: if set, the Origin's host equal to `Host` passes. The Security check
   flags it.
5. **Default same-origin:** the Origin's host **and port** (the default for the scheme when absent) equal those of
   `Host`, **and** that host is loopback, a private or loopback IP literal, or ends in `.local` or `.ts.net`.
6. The ingress is Loopback (the classifier's verdict, not the socket) and the Origin is loopback: passes.
7. Otherwise 403 "origin not allowed".

Serve keeps the original `Host`, so a Serve page passes rule 5 through `.ts.net`. A LAN bind passes through the private
IP literal. A bare machine name (`http://desktop-a:5101`) does not, and needs `AllowedOrigins`.

### 8.8 Plain-HTTP rule

OpenClaw allows plain `ws://` only to loopback, private IP literals, `.local` and `*.ts.net`. Huddle applies the same
rule to the whole gateway: an `http` request whose `Host` is anything else is refused with a page explaining that
plain HTTP is only for local and private addresses. Everything off loopback over `http` is labelled **downgraded**. The
rule uses the effective scheme (§6.8). Hosts listed explicitly in `CustomBindHost` or `ExtraHosts` are exempt, and
labelled downgraded. The automatic host list drops the bare `Dns.GetHostName()` and keeps `{name}.local`.

### 8.9 Security check

OpenClaw's `openclaw security audit` items that apply to Huddle, computed on demand for the Settings section:

| Id | Severity | Condition |
| --- | --- | --- |
| `gateway.bind_no_auth` | Critical | A non-loopback bind with `Auth:Mode = none` (the bind is refused) |
| `gateway.loopback_no_auth` | Warning | `Auth:Mode = none` |
| `gateway.tailscale_funnel` | Critical | Funnel is running |
| `gateway.tailscale_serve` | Info | Serve is running |
| `gateway.token_too_short` / `password_too_short` | Warning | Under 24 characters / under 12 |
| `gateway.control_ui.allowed_origins_required` | Warning | Non-loopback bind and no `AllowedOrigins` or `PublicOrigin`, with a bare-name host in use |
| `gateway.control_ui.allowed_origins_wildcard` | Warning | `AllowedOrigins` contains `*` |
| `gateway.control_ui.host_header_origin_fallback` | Warning | The dangerous fallback is on |
| `gateway.trusted_proxy_loopback` | Warning | `TrustedProxies` contains a loopback address |
| `gateway.downgraded_transport` | Warning | A tailnet, LAN or custom bind is active |
| `gateway.stray_serve_route` | Warning | A Serve route points at the app port (Huddle's addition) |
| `gateway.strict_local` | Info | Strict local is on: no sign-in, only this computer (Huddle's addition, §6.5a). While it is on, the items about remote exposure are not raised |

### 8.10 Error handling

| Failure | Behaviour |
| --- | --- |
| `team.db` unreadable at startup | Loopback only, `Auth:Mode = token` with an ephemeral in-memory token that cannot be recovered (OpenClaw's behaviour); the console says how to fix it |
| Tailscale missing, signed out, timing out | Status names it; Serve and Funnel do not run; the app starts |
| `tailscale serve` exits | Restart with backoff |
| `whois` fails | No identity for that request; a session bound to a login is refused until `whois` works |
| Ed25519 verification throws on malformed input | `DEVICE_AUTH_SIGNATURE_INVALID`, counted |

## 9. Startup and runtime changes

| Change | Takes effect |
| --- | --- |
| `Bind`, `Port`, `CustomBindHost`, `Tailscale:Mode`, `Tailscale:HttpsPort` | Next start |
| `Auth:Mode` | Next start (OpenClaw: a mode change needs a restart) |
| Token rotation, password change (same mode) | At once; sessions and device tokens stay |
| `AllowTailscale`, `AllowedOrigins`, `PublicOrigin`, `TrustedProxies`, rate limits | At once |
| Approve, reject, revoke, remove, rotate, rename | At once |
| Restart | Sessions, nonces, requests, bootstrap tokens and limiter state are gone; devices, tokens and settings stay |

## 10. Background work

| Worker | Runs | Does |
| --- | --- | --- |
| `TailscaleServeClaim` | While Serve or Funnel is chosen | Holds the foreground process; backoff restart; release at shutdown |
| `TailscaleStatusPoller` | Every 30 s while a Tailscale mode is chosen | Status, MagicDNS name (feeds host filtering), owner login, stray routes |
| `GatewayHousekeeping` | Every 60 s | Prunes the limiter, expired nonces, requests, bootstrap tokens and sessions; flushes last-seen |

## 11. Performance

| Path | Cost |
| --- | --- |
| Any request with a session | One SHA-256 and one dictionary lookup; over Serve, a `whois` cache hit |
| A handshake | One Ed25519 verification (~0.1 ms managed), one lookup, at most one `team.db` write |
| A silent reconnect after 12 h or a restart | One page round trip through `/auth` |
| `/_blazor` traffic | Nothing per message; the guard checks at connect and on revocation |

## 12. Edge cases and failure modes

| # | Case | Behaviour |
| --- | --- | --- |
| E-1 | Copies on 5101 and 5102 | Separate origins (separate keys and tokens in the browser), separate cookies (Instance id), separate Tailscale ports |
| E-2 | Two copies want one Tailscale port | The second reports the ownership conflict and does not take it |
| E-3 | Browser storage cleared or a private window | A new device: loopback re-pairs automatically; elsewhere it needs approval again; the old device row stays until removed |
| E-4 | Storage blocked | A page-lifetime identity; every reload is a new device (OpenClaw does the same) |
| E-5 | The host browser opens `http://192.0.2.20:5101` or the Serve URL | Not loopback: needs approval (OpenClaw: same-host LAN or tailnet counts as remote) |
| E-6 | A bootstrap URL opened in a second browser after the first redeemed it | Refused; that browser uses the token or `dashboard` |
| E-7 | A bootstrap URL opened after 10 minutes | Refused; `dashboard` mints another |
| E-8 | Device approved while its page is closed | Next visit connects with the gateway credential and finds the device paired |
| E-9 | A limited device opens a Settings bookmark | "Settings need admin access" and **Request admin** |
| E-10 | The only admin device is lost and no browser on the host is open | `dashboard` on the host (loopback auto-approves as admin) |
| E-11 | `Auth:Mode = none` and a LAN bind | The bind is refused; loopback only; critical Security check item |
| E-12 | Clock jump on the host | Nonces and signatures outside ±60 s fail; the page retries with a fresh challenge once |
| E-13 | A hostile device name | From `platform` and `deviceFamily`, trimmed, rendered as text; renamed by the Human |
| E-14 | Someone runs `tailscale serve 5101` by hand | Forwarded headers on a loopback socket → 403 `proxy_attribution_required`; listed as a stray route |
| E-15 | A proxy that adds no forwarding headers | Looks like loopback: it still needs the gateway token and a device signature, and auto-approves only if `Host` and `Origin` are loopback. It gains no more than a local process has |

## 13. End-to-end flows

### 13.1 The host, every day

```text
Launcher starts Huddle → mints admin bootstrap token → opens http://127.0.0.1:5101/#bootstrapToken=…
/ → no session → 302 /auth?return=/ → auth.js: new identity, bootstrap from the fragment, fragment cleared
GET /auth/challenge → sign v3 payload → POST /auth/connect {bootstrapToken}
  → bootstrap valid, bound to this deviceId, admin → paired (loopback) → session cookie + device token → /
Next day: /auth reconnects silently with the device token. Nothing typed, ever.
```

### 13.2 A phone over Tailscale Serve

```text
Settings: Tailscale mode Serve → restart → managed 127.0.0.1:52817; tailscale serve --bg=false --yes --https=5101 …
Phone: https://desktop-a.tail1234.ts.net:5101 → tailscaled TLS, Tailscale-User-Login, X-Forwarded-* → managed listener
  → whois(100.101.102.103) = ada@example.com ✓ → no session → /auth: "Signed in to Tailscale as ada@example.com"
  → handshake with no credential: identity stands in for gateway auth; AllowTailscale auto-pairs, limited
  → session (Secure) + device token → Huddle. Settings → Request admin → host approves → admin.
```

### 13.3 An iPad on the LAN (downgraded)

```text
Settings: Bind lan, port 5101 → restart → firewall prompt → allow Private
Settings › Pair a device → QR http://192.0.2.20:5101/auth#bootstrapToken=… (limited)
iPad scans → handshake with the setup link's bootstrap → PAIRING_REQUIRED → waiting page
Host: Feed bell "iPad wants to use Huddle" → Approve (Limited) → iPad's poll sees approved → handshake again → in
```

## 14. Decisions and rationale

### How Huddle maps onto OpenClaw

| OpenClaw | Huddle | Same? |
| --- | --- | --- |
| `gateway.bind` loopback, tailnet, lan, auto, custom | `Team:Gateway:Bind`, same values and defaults | Same |
| Port 18789 | The app's own `--urls` port | Adapted: copies run side by side |
| `gateway.auth.mode` token, password, trusted-proxy, none | `Auth:Mode`, same values; `none` is loopback-only | Same |
| Token generated at onboarding, 48 hex; `auth-token --show` needs a TTY; `doctor --generate-gateway-token` | Generated at first start, same format; `auth-token --show` and `--rotate` | Same |
| Loopback still needs the token | Same when Strict local is off | Same |
| (none) | `Team:Gateway:StrictLocal`, default `true`: no sign-in, only this computer (§6.5a) | **Not in OpenClaw — owner decision.** OpenClaw keeps its token on loopback; Huddle trades that for no sign-in on this computer, and only this computer |
| Ed25519 device identity in pure JS, in localStorage; id = SHA-256 of the public key | Same, `@noble/ed25519` | Same |
| Challenge, v3 signed payload, `DEVICE_AUTH_*` errors | Same payload with client id `huddle-web` | Same |
| Credential order: device token, shared token, password | Same, plus bootstrap token | Same |
| Shared token in sessionStorage; password in memory | Same | Same |
| An authenticated WebSocket per connection | A 12-hour HttpOnly session cookie issued by the handshake | **Adapted**: Blazor's page loads and its socket need a cookie (D-1) |
| `PAIRING_REQUIRED`, wait, approve or reject, 5-minute expiry, approval grants nothing until reconnect | Same | Same |
| Loopback auto-approval with no forwarded headers; `autoApproveLocal` | Same | Same |
| Tailscale Serve: verified identity skips the token and pairing | Same; scopes granted are limited | Scopes not in OpenClaw |
| Tailnet, LAN and same-host LAN/tailnet always need approval | Same | Same |
| Scopes `operator.read/write/pairing/admin` and more; limited access; Request admin | The four that map; read and write granted together | Subset (H-2) |
| `openclaw devices list/approve/reject/revoke/remove/rotate` | `Agency.Huddle.App.exe devices …` and Settings | Same |
| `openclaw dashboard`: single-use bootstrap in `#bootstrapToken`, bound to the redeeming device | Same, also used by the desktop launcher | Same |
| Setup codes for mobile apps and nodes (not browsers) | Setup links: a limited bootstrap token in a QR, always needs approval | **Adapted**: Huddle has only browsers (D-4) |
| Rate limit 10 / 60 s → 5 min; loopback exempt but delayed; `browser-origin:` keys; 128 pre-auth per IP | Same | Same, delay curve not in OpenClaw |
| `checkBrowserOrigin`, `allowedOrigins`, `publicOrigin`, host-header fallback flag | Same rules | Same |
| `proxy_attribution_required` for an unconfigured same-host proxy | Same | Same |
| Managed Serve on a dedicated ephemeral loopback listener; `serve --bg=false --yes`; ownership conflict; release, no reset; backoff | Same | Same |
| `allowTailscale` with `whois` on `X-Forwarded-For`; ignored on external routes | Same | Same |
| Funnel only with password mode; ports 443, 8443, 10000 | Same; recognised by `Tailscale-Funnel-Request` | Same |
| `trusted-proxy` with `trustedProxies`, device auto-approve opt-in, `allowRealIpFallback` false | Same | Same |
| Revocation disconnects live sessions | Same, via a hub filter (spike first) | Same |
| `openclaw security audit` | The Security check in Settings | Subset |
| No switch disables device identity | Same | Same |

### Decisions

**D-1. A session cookie carries the handshake's result.** OpenClaw signs once per WebSocket. Huddle's pages, assets and
Blazor socket are separate HTTP requests, and a browser can attach only cookies to a WebSocket. A session minted only
by a signed handshake, bound to its device and revoked with it, keeps OpenClaw's property ("every connection proved the
key") at the granularity Blazor allows.

**D-2. Loopback needs the token too, and the launcher hides it.** OpenClaw's reason applies to Huddle: any local
process, or a web page by DNS rebinding, can reach loopback. The bootstrap handoff makes this invisible to the Human.

**D-3. Revocation is immediate.** OpenClaw shipped without it and had to fix it (CVE-2026-34503). Huddle starts with it.

**D-4. Setup links reuse OpenClaw's bootstrap tokens for phones' browsers.** Without them, pairing a phone over a LAN
means typing 48 hex characters. They grant at most limited access and always need approval, which keeps them weaker
than OpenClaw's own setup codes.

**D-5. The Open action goes whenever remote access is configured.** rules.md says it must be removed rather than
gated once Huddle is used remotely; that is the literal reading.

**D-6. The connect page is not a Blazor page.** Every route renders `InteractiveServer` (`App.razor`), so a component
page would open a Blazor connection before authentication, and an interactive page cannot set an HttpOnly cookie.

**D-7. Phase 0 tightens `AllowedHosts` now**, alone: it closes today's DNS-rebinding exposure before any of this exists.

## Deviations from the first design

Found in review during delivery and settled; the sections above already say the corrected thing. Each line names the
change and where it lives.

- **/_desktop/* gate (C-1).** `/_desktop/*` passes the gate without a session only when the ingress is Loopback; the
  instance-token check stays. §6.8.
- **Test seam (C-2).** A server-side `IGatewaySessionResolver` replaces "a handshake per client"; the factory does not
  set `Auth:Mode = none`. Appendix C.
- **Middleware order (C-3).** Authentication and authorization are called explicitly after the gate. §6.8.
- **Stable principal (C-4).** `NameIdentifier` is the device id. §6.8.
- **Socket re-check (C-5, C-6).** The singleton guard re-checks the session on every hub invocation. §6.8.
- **Dead sessions (C-7).** `GET /auth/status` and a redirect from the reconnect modal. §6.8.
- **Revocation spike (C-8).** Run on the raw Blazor protocol; confirmed. Appendix E, OP-1.
- **Bootstrap URLs (C-9, C-12).** Always `/auth?return=%2F#bootstrapToken=…`; a hand-off falls back to `{url}/auth`;
  `/auth/bootstrap/burn`. §6.9.
- **CLI without `instance.json` (C-10).** `dashboard` is desktop-only; `devices` takes `--url`. §6.9, §6.13.
- **Stable desktop port (C-11, D-4).** Stored in `gateway_settings.desktop_port`. §6.2.
- **Settings and bUnit (C-13).** `Settings.razor` reads the cascaded authentication state and fails closed;
  `MudBunitContext` signs in an admin. §6.12.
- **Small fixes (C-14).** Local `return=` paths only; Request admin navigates with `forceLoad: true`;
  `auth-token --show` refuses redirected output (also true under mintty and Git Bash); an unknown first argument prints
  usage and exits 2; the credential of `/_desktop/*` is called the instance token. §6.9, §6.13.
- **Origin presence (D-1).** `Origin` is required only on non-GET `/_blazor` and `/auth/*` and on upgrades. §8.7.
- **Managed listener lookup (D-2, D-5).** Found by local endpoint, classified once, port read after start. §6.2.
- **Listener plan (D-3).** Pure, re-applies every configured URL, one any-address listener for `lan`. §6.2.
- **Test host (D-6).** `KestrelAppHost` binds port 0 through `urls`. Appendix C.
- **Host filtering (D-7, D-10).** `PostConfigure`, no bare machine name, explicit hosts exempt from §8.8. §6.4, §8.8.
- **Effective scheme (D-8).** Set from `X-Forwarded-Proto` on managed and trusted-proxy ingress. §6.8.
- **Origin rules (D-9).** Host and port compared; rule 6 uses the ingress; rule 2 strict. §8.7.
- **Tailscale processes (D-11).** Kill the tree at once; Job object on Windows; `ITailscaleChild`. §6.3.
- **Trusted-proxy attribution (D-12, D-14).** In the classifier; a loopback request may still use a token. §6.10.
- **Funnel password (D-13).** Mandatory for Funnel ingress; a refused claim does not stop the app. §6.11.
- **Bootstrap in password mode.** Accepted, because the launcher always uses one. §6.9.
- **Case.** Tailscale logins compare ignoring case everywhere, §8.6 included.
- **Strict local (SL, 2026-10-30).** On by default: no sign-in, only the Loopback ingress, remote options forced off at
  startup. Switching it on applies at once; off, at the next start. The Settings page can show Bind and Tailscale as
  pending while it is on, because the running options are forced and the stored ones keep their values. §6.5a.
- **Project setting.** `Huddle.App.csproj` gained `AllowUnsafeBlocks` for the Job object imports (SYSLIB1062).

D-5 (Kestrel writes the bound port back) and D-7 (`PostConfigure` host filtering follows live updates) were marked
unverified in the review; both held when their first tests ran.

## Appendix A. Configuration

All under `Team:Gateway` (`GatewayOptions`). Values an admin saves in Settings win. Added to
[Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md) when built.

| Key | Default | OpenClaw |
| --- | --- | --- |
| `StrictLocal` | `true` | — (owner decision; §6.5a) |
| `Bind` | `loopback` | `gateway.bind` |
| `CustomBindHost` | none | `gateway.customBindHost` |
| `Port` | the app's own fixed port | `gateway.port` |
| `Auth:Mode` | `token` | `gateway.auth.mode` |
| `Auth:Token` | generated | `gateway.auth.token`; env `HUDDLE_GATEWAY_TOKEN` |
| `Auth:Password` | none | `gateway.auth.password`; env `HUDDLE_GATEWAY_PASSWORD` |
| `Auth:AllowTailscale` | `true` with Serve | `gateway.auth.allowTailscale` |
| `Auth:RateLimit:MaxAttempts`, `WindowSeconds`, `LockoutSeconds`, `ExemptLoopback` | 10, 60, 300, true | `gateway.auth.rateLimit` |
| `AutoApproveLocal` | `true` | `gateway.nodes.pairing.autoApproveLocal` |
| `Tailscale:Mode` | `off` | `gateway.tailscale.mode` |
| `Tailscale:HttpsPort` | the app port | — |
| `Tailscale:Command` | `tailscale` | — |
| `TrustedProxies` | `[]` | `gateway.trustedProxies` |
| `TrustedProxy:UserHeader` | none | `gateway.auth.trustedProxy` |
| `TrustedProxy:DeviceAutoApprove:Enabled`, `Scopes` | false, `operator.read, operator.write` | `gateway.auth.trustedProxy.deviceAutoApprove` |
| `AllowRealIpFallback` | `false` | `gateway.allowRealIpFallback` |
| `PublicOrigin` | none | `gateway.publicOrigin` |
| `AllowedOrigins` | `[]` (then `PublicOrigin`) | `gateway.controlUi.allowedOrigins` |
| `DangerouslyAllowHostHeaderOriginFallback` | `false` | `gateway.controlUi.dangerouslyAllowHostHeaderOriginFallback` |
| `ExtraHosts` | `[]` | — (host filtering) |

And at the root: `AllowedHosts` became `localhost;127.0.0.1;[::1]` (Phase 0, its own commit). The stored `gateway_settings`
row also holds `desktop_port` (§6.2). The gateway and the CLI also read `HUDDLE_GATEWAY_TOKEN` and `HUDDLE_GATEWAY_PASSWORD`.

## Appendix B. The Remote access section (`G10`)

```text
┌ Remote access ─────────────────────────────────────────────────────────────────────────────┐
│ Reach Huddle from other devices. Every device needs the gateway secret and your approval.   │ G10
│ ┌ Gateway ─────────────────────────────────────────────────────────────────────── G10a ┐  │
│ │ Bind [loopback ▾]   Auth [token ▾]   Token ••••••••  [Show] [Rotate]                  │  │
│ │ Tailscale [Serve ▾] port 5101   [■] Use Tailscale sign-in        ● Running             │  │
│ │ https://desktop-a.tail1234.ts.net:5101   [Copy link]                                    │  │
│ │ Restart Huddle to apply: Bind lan → loopback                                           │  │
│ └────────────────────────────────────────────────────────────────────────────────────────┘  │
│ Waiting for approval (1)                                                          G10e      │
│  iPad · Safari  192.0.2.34  LAN (downgraded)  asks Limited  4:31  [Approve ▾] [Reject]    │
│ Pair a device                                                                     G10b      │
│  ██ QR ██  Scan to pair. Works once, for 10 minutes. You will still approve it.  [New link] │
│ Devices (3)                                                                       G10d      │
│  This computer (Edge)   Admin    loopback        seen now             [Rotate] [Revoke]     │
│  iPhone                 Limited  Tailscale ada@example.com  seen 2 h  [Rotate] [Revoke]     │
│ Security check                                                                    G10f      │
│  ⚠ gateway.downgraded_transport  A LAN bind is active: traffic is not encrypted. Use Serve. │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

| Code | Container | Notes |
| --- | --- | --- |
| `G10` | `RemoteAccessPanel` | Admin only; limited sessions see the **Request admin** notice instead of every Settings section |
| `G10a` | Gateway card | The settings of §6.12 with stored and running values, the state chip with an icon, URLs with **Copy link**, Tailscale's messages and links |
| `G10e` | Waiting for approval | One row per request: name, address, ingress in words (**LAN (downgraded)**, **Tailscale address (downgraded)** or **Network (downgraded)** for a direct connection on a lan, tailnet or custom bind; **Tailscale**, **Tailscale Funnel**, **This computer**, **Proxy**), Tailscale login with **You** or **Shared user**, requested scopes, countdown, **Approve** with a scope menu capped at the approver's, **Reject** |
| `G10b` | Pair a device | QR at 160px on a white plate, the link, **New link**, the sentence that approval still follows |
| `G10d` | Devices | Name (rename), Limited or Admin, how paired, last address, last seen, **This device**, **Rotate**, **Revoke**, **Remove** with inline confirm |
| `G10f` | Security check | §8.9 items with severity icon, the condition and the fix |

## Appendix C. Delivery plan, tests first

Every implementation task follows a test task that is written first and seen to fail (red) before the code makes it
pass (green), as [agents/Testing.md](../agents/Testing.md) requires. Pure logic goes in `Huddle.Tests.Domain`, the
pipeline in `Huddle.Tests.Hosted`, components in `Huddle.Tests.Components`.

**Harness.** `TeamWebApplicationFactory` is `TestServer` only (no sockets, `RemoteIpAddress` null). Session resolution
sits behind the internal `IGatewaySessionResolver`. The factory has `public bool ConnectedAsAdmin { get; init; } = true;`
and, when true, `ConfigureTestServices` replaces the resolver with `LoopbackAdminSessionResolver`, a fixed paired
loopback admin session for every request. Every client path inherits it (`CreateClient`, `CreateDefaultClient`,
`Server.CreateClient`, `Server.CreateWebSocketClient`, `WithWebHostBuilder`), so every existing hosted test keeps passing
unchanged. The factory does not change `Auth:Mode`. No configuration key reaches the seam, so it cannot be switched on in
production; one hosted test proves it (real resolver, no session → 302 `/auth`). Gateway tests set
`ConnectedAsAdmin = false`, use the real resolver and `GatewayTestClient` (a handshake with a test device key, signing
with BouncyCastle), simulate addresses and the managed listener with a test startup filter that sets
`Connection.LocalIpAddress` and `LocalPort` to an endpoint registered in a test `GatewayListener`, and fake
`ITailscaleCli`. `MudBunitContext` signs in an admin principal by default (`SignInWithScopes` changes it). Tests that
need real sockets (binds, the managed listener, the WebSocket abort) use `KestrelAppHost` in
`tests/Huddle.Tests.Common/Gateway/`, which calls `UseKestrel()` with `UseSetting("urls", "http://127.0.0.1:0")` so the
listener plan is the only source of endpoints (never `UseKestrel(port)`) and builds its own `HttpClient` from
`GatewayListener.AppUrl`. All ports are 0; the lan socket test carries the `FirewallPrompt` trait so it can be skipped.

| Slice | Test task first | Then |
| --- | --- | --- |
| **0. Host check** (ships alone) | Hosted: `Host: evil.example` → 400; `localhost`, `127.0.0.1`, `[::1]` → 200 | `AllowedHosts` |
| **1. Secrets and settings** | Domain: token format and generation, resolution order (config, env, stored), placeholder rejection, rotation, PBKDF2 password, Instance id stable, `ReadForStartup` | `GatewaySettings`, store |
| **2. Rate limiter and classifier** | Domain: 10/60 s → 5 min, loopback delay without lockout, `browser-origin:` keys with no loopback exemption, pruning at 10,000, `retryAfterMs`. Hosted: the §8.6 table (managed full headers, managed missing XFF → 403, Funnel header, loopback with forwarded headers → 403, trusted proxy, IPv4-mapped loopback) | `AuthRateLimiter`, classifier |
| **3. Handshake** | Domain: v3 payload bytes against a fixed vector; each `DEVICE_AUTH_*` failure; nonce single use and 60 s; ±60 s skew. Hosted: connect with token, password, device token, bootstrap; wrong token counted; `none` only on loopback; session cookie attributes exactly as §7.4 | `ConnectEndpoints`, `auth.js` (noble vendored), BouncyCastle |
| **4. Gate, sessions and revocation** | Hosted: every route without a session → 302 or 401, `/health` loopback only, `/_desktop/*` refused from the managed listener, `/_gateway/*` needs the bearer token; session expiry 12 h; Tailscale-bound session refused with a different `whois` login; `/settings` without admin → notice. Real sockets: **spike** — revoke closes the device's `/_blazor` WebSocket within 2 s | `GatewayGate`, scheme, `SessionRegistry`, `GatewayConnectionGuard` (or the fallback) |
| **5. Pairing** | Domain: request open, supersede, 5-minute expiry, cap 10, approve within the approver's scopes, admin needs admin, reject without ban, auto-approve table (§6.7), Request admin. Hosted: `PAIRING_REQUIRED` → approve → handshake → in; approval alone grants nothing | `PairingService`, `DeviceRegistry` |
| **6. Bootstrap and CLI** | Domain: bootstrap single use, 10 minutes, bound to the first device; setup links limited and never auto-approved; https rule for non-private hosts. Hosted: `/_desktop/bootstrap` with the instance token; `/_gateway/devices` verbs; `auth-token --show` refuses redirected output; desktop record excludes the managed endpoint and maps `[::]` | Launcher handoff, `dashboard`, `devices`, `auth-token` verbs, console URL for dev runs |
| **7. Binds and origin** | Domain: §8.1 ports; §8.7 origin table row by row; §8.8 plain-HTTP rule. Real sockets: lan and tailnet binds, refused bind with `none`, busy port → loopback-only | `GatewayListener`, origin check |
| **8. Tailscale** | Domain with a fake CLI: status parsing from a captured sample, ownership conflict, enable-HTTPS link, backoff 1 → 60 s, release without reset, `whois` cache by `IPAddress`, stray routes, MagicDNS name fed to host filtering (a Serve request with `Host: …ts.net:5101` passes). Hosted: managed listener only in Serve and Funnel | `TailscaleController`, claim, poller |
| **9. Funnel and trusted proxy** | Funnel refused without password mode; ports; `Tailscale-Funnel-Request` traffic always needs the password and approval. Trusted proxy: attribution required, auto-approve opt-in and cap, origin still checked | Both modes |
| **10. Settings, Feed, Security check, Open action** | bUnit: each `G10` part, limited-device notice, scope menu capped, Security check items. Domain: the four Feed payloads round-trip through `FeedSourceTypes.ToWire` and `FeedPayloads`; notify and resolve. bUnit: Open, open and reveal not rendered while remote access is configured, and their handlers refuse | `RemoteAccessPanel`, `GatewayAdminService`, Feed, Open-action removal |
| **11. Documents** | — | Appendix F; `Check-All.ps1` |

## Appendix D. Manual tests

A new page `engineering/manual-tests/remote-access.md`, written when built. None spends money.

| Id | Test |
| --- | --- |
| REMOTE-01 | Installed app: the browser opens authenticated; a second launch opens another authenticated tab |
| REMOTE-02 | Second browser on the host: the connect page; `dashboard` gets in; `auth-token --show` works only on a terminal |
| REMOTE-03 | Serve: the phone gets in with no token and no approval, limited; **Request admin** → approve → admin |
| REMOTE-04 | Serve: a shared tailnet user is marked **Shared user**; revoke |
| REMOTE-05 | LAN: QR setup link on iPhone and Android; approval; downgraded warnings everywhere they belong |
| REMOTE-06 | Rate limit: 10 wrong tokens from a phone → locked out 5 minutes; loopback slowed, not locked |
| REMOTE-07 | Revoke a phone with a Room streaming: disconnected at once |
| REMOTE-08 | Two copies on 5101 and 5102 from one laptop: separate devices, no sign-outs |
| REMOTE-09 | `tailscale serve --bg 5101` by hand: 403 `proxy_attribution_required`; the stray route in the Security check |
| REMOTE-10 | Funnel: refused in token mode; in password mode a phone off the tailnet needs the password and approval; critical item shown |
| REMOTE-11 | With remote access configured, the Open action and Library open/reveal are gone; turned off, they return |

## Appendix E. Open points and choices not in OpenClaw

| # | Point | Choice until checked |
| --- | --- | --- |
| OP-1 | Does a global `IHubFilter` reach Blazor's `ComponentHub`? | **Settled: confirmed** by the slice 4 spike; `Abort()` closes the socket in about 1.3 s; no fallback needed |
| OP-2 | Is a foreground `serve` route removed when its process is killed on Windows? | Yes (OpenClaw relies on it); still open: checked by hand ([manual tests](engineering/manual-tests/remote-access.md), D2 checks) |
| OP-3 | Does a second claim on a port fail with "listener already exists"? | Yes (OpenClaw's conflict error depends on it) |
| OP-4 | Scopes for a verified-Serve auto-pairing | Limited (OpenClaw does not document them) |
| OP-5 | Nonce lifetime and signature skew | 60 s and ±60 s (OpenClaw does not document them) |
| OP-6 | Feed Category for the four items | **Settled:** a new **Devices** Category |
| OP-7 | The Open action: remove whenever remote access is configured (as written), or only for non-loopback sessions | **Settled: as written**, in [rules.md](engineering/rules.md) |
| OP-8 | Loopback failure delay curve; pending-request cap of 10; 12-hour sessions | Huddle's choices (not in OpenClaw) |
| OP-9 | Packages: BouncyCastle.Cryptography, QRCoder, vendored `@noble/ed25519` and `@noble/hashes` | Record them in [backlog.md](engineering/backlog.md)'s packages list when added; check the vulnerable-dependency job |

## Appendix F. Documents to change when it is built

- [Index.md](Index.md): the feature row's status, manual tests, a §5 row for `remote-access.md`.
- [language.md](engineering/language.md): the Vocabulary terms.
- [rules.md](engineering/rules.md): the Open-action row (D-5, OP-7), and a new row: "Whether a request is local is
  decided only by the gateway's ingress classifier; never test `IPAddress.IsLoopback` on your own."
- [Huddle.EngineeringGuide.md](Huddle.EngineeringGuide.md): Appendix A, the CLI verbs, `AllowedHosts`.
- [wireframes.md](engineering/wireframes.md): `G2a` gains Remote access; `G3` lists `RemoteAccessPanel`; rows `G10`
  to `G10f`; the connect page.
- [Huddle.UserGuide.md](Huddle.UserGuide.md): "Using Huddle from another device". It must say plainly that
  **Tailscale is not required when the devices share a network**: the LAN bind works on its own, step by step (Bind
  `lan`, restart, allow the firewall prompt on Private networks, **Pair a device**, scan, approve). It must also say
  what that costs: it works on that network only, traffic is not encrypted (fine at home, risky on shared office or
  café Wi-Fi), and the PC's address can change, so reserve it on the router (DHCP reservation) or use
  `http://{machine-name}.local:{port}`.
- [code-map.md](engineering/code-map.md): the `Gateway/` folder.
- [known-limits.md](engineering/known-limits.md): downgraded binds, no read-only device, restart to apply.
- [installer.md](engineering/installer.md): the launcher's bootstrap handoff; a non-loopback bind on an installed copy
  needs `Team:Gateway:Port`.
- [testing.md](engineering/testing.md): the factory's `Auth:Mode = none`, `GatewayTestClient`, `KestrelAppHost`.
