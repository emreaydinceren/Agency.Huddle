# Runtime architecture

How a message travels from the browser to a real Claude process and back, and
the two boundaries that must not be crossed. Read it before changing anything
that moves a Message.

For which file holds what, see [Code map](code-map.md). Back to the hub:
[AgencyTeam.md](../AgencyTeam.md).

```text
  Browser (Blazor Server circuit)
      │
      ├── /            Chat.razor ──────┐
      └── /teammates   Teammates.razor  │
                                        ↓
                              ChatService          the only writer of Messages
                                    │
                              RoomEvents           in-process pub/sub (singleton)
                                    │
                    ┌───────────────┴───────────────┐
                    ↓                               ↓
             Blazor components               AgentGateway
             (re-render)                          │
                                                  │ named pipe "team",
                                                  │ one JSON object per line
                                  ┌───────────────┴────────────────┐
                                  ↓                                ↓
                          DemoAgentHost                      PersonaRunner
                          (echo)                             (one per Persona)
                                                                   │
                                                   AppToolServer ←──┤ MCP over
                                                   (tool bodies run │ loopback HTTP
                                                    in OUR process) │
                                                                   │ ACP: JSON-RPC
                                                                   │ over child stdio
                                                                   ↓
                                                        node claude-agent-acp
                                                        (a real Claude session)
```

Two boundaries matter:

1. **The pipe.** Everything an Agent knows arrives as an Envelope. No shortcuts.
2. **The tool server.** Tool *bodies* execute inside `Team.App`, holding the real
   `ChatService`. That is what lets an Agent genuinely create a Room. The agent
   cannot reach in; it can only call the App Tools we published.

## Four flows worth tracing

**A human sends a message.** `Composer.razor` →
`ChatService.SubmitFromComposerAsync` → writes JSONL, updates SQLite →
`RoomEvents.PublishMessagePosted` → both the Blazor components (which re-render)
and `AgentGateway`, which writes a `messagePosted` Envelope to every connected
Agent member — *including* the sender's own, which discards it client-side.

**An Agent replies.** `PersonaRunner`'s read loop receives `messagePosted` →
`ReplyGate.ShouldReply` → if it passes, writes a work item to a `Channel`.
**Never call the agent from the read loop.** A single consumer calls
`PromptAsync` one item at a time; one long-lived reader over `session.Events`
accumulates `MessageChunk.Text` and completes the turn's
`TaskCompletionSource` on `TurnCompleted`. Draining per turn would race the first
chunk. The reply goes back out as an ordinary `postMessage`.

**An Agent creates a Room.** The model calls `mcp__team__create_room` → loopback
HTTP → `AppToolServer` → `CreateRoomTool.InvokeAsync`, running on a Kestrel
thread with no Blazor circuit → `ChatService.CreateRoomForAsync` → `RoomEvents` →
the sidebar updates. Unknown agent names come back as error *text*, never a
thrown exception — the model reads the text and corrects itself.

**Anyone issues an Invitation.** Three entry points, one body. The Human types
`/invite @name` (`ChatService.SubmitFromComposerAsync`) or clicks **Add
teammate** (`InviteTeammate.razor`); an Agent calls `mcp__team__invite_agent`
(`InviteAgentTool`, on a Kestrel thread). All three reach
`ChatService.InviteAsync`, which adds the Member, renames the Room after its
Agents and publishes `RoomsChanged`. Adding a fourth entry point means finding
this method, not reimplementing it.
