---
status: accepted
date: 2026-09-09
---

# The app hosts the named-pipe server; Agent Hosts dial in

Bots are local processes started ad hoc during agent experiments. Team listens
on one well-known named pipe (`\\.\pipe\team`) and every Agent Host connects as
a client, sends `hello`, and keeps the connection open for bidirectional JSON
lines. We chose this over an HTTP/SignalR API (which drags in ports, TLS, CORS
and auth for no gain at PoC scale) and over per-bot pipes (which would force the
app to keep a registry of pipe names and dial out). Bots therefore need zero
configuration and trivial reconnect logic, at the cost of the pipe being
local-machine only.

## Considered options

- HTTP + SignalR hub: rejected for V1; revisit when remote Agent Hosts or other
  humans are needed.
- One pipe per Bot, app connects out: rejected; requires configuration and
  discovery.
- gRPC over pipes: rejected; heavy tooling for four message types.

## Consequences

- The protocol is defined once in `Team.Contracts` and versioned in every
  envelope.
- A single Team instance per pipe name; running two splits clients
  unpredictably.
- Streaming is reserved in the envelope (`messageDelta`) so it can be added
  without a breaking change.
