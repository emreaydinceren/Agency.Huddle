---
status: proposed
date: 2026-10-30
---

# Huddle copies OpenClaw's gateway auth, device pairing and Tailscale model

Huddle answers anyone who can reach its port and has no authentication of any kind. That is safe
only because it listens on loopback, and even then a web page can reach it by DNS rebinding. People
want to use it from other devices, and an Agent can run commands on the host. OpenClaw, an
open-source personal agent gateway, solves the same problem for the same kind of user and has been
through public security review on exactly this surface. The design is in
[Huddle.RemoteAccess-Specifications.md](../Huddle.RemoteAccess-Specifications.md).

## The decision

**Huddle adopts OpenClaw's remote-access model as faithfully as a Blazor Server app allows.**

- **Gateway auth on every connection, loopback included**: a generated 48-hex token by default,
  or a password, or trusted-proxy identity, or (only over Huddle's own Tailscale Serve) a Tailscale
  identity confirmed with `tailscale whois`. `none` is allowed on loopback only.
- **A device identity in every browser**: an Ed25519 key in local storage (pure JS, so it works on
  plain HTTP) that signs OpenClaw's v3 challenge payload on every handshake.
- **Pairing**: a new device waits up to 5 minutes for an admin's approval. Loopback (no forwarded
  headers), the launcher's bootstrap token and verified Tailscale Serve pair automatically; tailnet,
  LAN, Funnel and setup links always need approval. Scopes `operator.read`, `write`, `pairing` and
  `admin`; a limited device can request admin.
- **Ingress**: loopback by default; Tailscale Serve to a dedicated random-port loopback listener held
  by a foreground `tailscale serve`; Funnel only in password mode; tailnet, LAN and custom binds as
  *downgraded* plain HTTP; `proxy_attribution_required` for proxies Huddle did not set up.
- **OpenClaw's rate limits** (10 failures in 60 s, then 5 minutes), **origin check**, **revocation that
  disconnects**, and a **Security check** modelled on `openclaw security audit`.
- **The dashboard handoff**: the desktop launcher and a `dashboard` verb open the browser with a
  single-use `#bootstrapToken`, so the Human on the host never types the token.

**Adapted, not copied:** OpenClaw authenticates one WebSocket per connection. A Huddle page is a
prerendered GET, assets and a Blazor socket, and browsers attach only cookies to a socket, so the
handshake issues a 12-hour HttpOnly session cookie, named per instance because copies run side by
side. Phones' browsers pair with **setup links** (a limited bootstrap token in a QR code) that
always need approval, since Huddle has no native app for OpenClaw's setup codes.

## Consequences

- Every browser, including the host's, now goes through a connect page once; the launcher makes it
  invisible on the host. Every hosted test client is created already connected.
- Two new dependencies: BouncyCastle.Cryptography (Ed25519 verification; .NET has none built in) and
  the vendored `@noble/ed25519`, plus QRCoder.
- Huddle depends on the Tailscale CLI when a Tailscale mode is chosen.
- The Open action and the Library's open and reveal actions disappear whenever remote access is
  configured, as rules.md requires.
- `AllowedHosts` stops being `*` now, before any of this ships.
- Deferred items are in [backlog.md](../engineering/backlog.md#hardening-remote-access).
- Built 2026-10-30 on `feat/remote-access`, not merged: the commits from `fd5739c7` on (`git log origin/main..feat/remote-access`).

**Amendment 2026-10-30: Strict local, on by default.** The owner decided that Huddle runs in **Strict local** unless
told otherwise (`Team:Gateway:StrictLocal`, default `true`). It admits only a direct loopback connection, with no
sign-in and no device identity, and forces Bind to loopback and Tailscale off at startup. This departs from OpenClaw,
which keeps its token on loopback, and from consequence 1 above: the host browser no longer meets a connect page, and
the launcher opens the plain address. The remote model in this ADR is unchanged, and applies once Strict local is
turned off in Settings and Huddle is restarted. The cost: any program or other user account on this computer can use
Huddle with no secret, which is the trust Huddle had before remote access existed. Host filtering and the Origin check
still stop DNS rebinding. See [the Spec](../Huddle.RemoteAccess-Specifications.md) §6.5a.
