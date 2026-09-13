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
2. **The tool server.** Tool *bodies* execute inside `Huddle.App`, holding the real
   `ChatService`. That is what lets an Agent genuinely create a Room. The agent
   cannot reach in; it can only call the App Tools we published.

## Five flows worth tracing

**A human sends a message.** `Composer.razor` →
`ChatService.SubmitFromComposerAsync` → writes JSONL, updates SQLite → resets that
Room's Budget to zero, which is what "say anything to resume" actually is →
`RoomEvents.PublishMessagePosted` → both the Blazor components (which re-render)
and `AgentGateway`, which writes a `messagePosted` Envelope to every connected
Agent member **except the sender's own** — `DeliverAsync` skips it, which is what
stops a naive client echo-looping.

**The human grants a paused Room more Budget.** `Chat.razor`'s Continue button →
`ChatService.ExtendBudgetAsync` → raises the grant by one Budget under the Room's
own semaphore → rebuilds a `MessagePostedEvent` from the Room, its Members and the
last Transcript entry → `RoomEvents.PublishMessageRedelivered`. Only
`AgentGateway` subscribes to that event, so the Agents are woken again and the Room
view does not render a Message it is already showing.

**An Agent replies.** `PersonaRunner`'s read loop receives `messagePosted` →
`ReplyGate.Decide` → `Reply` writes a work item to a `Channel`; `CatchUp` buffers
the Message for the next Mention; `BudgetExhausted` does neither, because that
Message is being held for re-delivery rather than missed.
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
