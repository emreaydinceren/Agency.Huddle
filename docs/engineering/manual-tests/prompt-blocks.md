# Prompt blocks

**6 tests** · 1 free, 5 paid 💰 (cheap: an image cost about 1,000 tokens) · about 1.4 hours

Prove, in a real browser against a real Adapter, that a Library image the Human's Message names is
**seen** by the Teammate without a tool call, that a named image over a limit quietly falls back to its
path, that an image mentioned only in catch-up is not re-sent, that an Adapter Profile can turn the
whole thing off, and that an Adapter which never advertised image support gets plain path lines. The
automated suite proves the parts, and the delivering agent ran the wire-level checks live on
2026-10-01 (see below); this page proves they hold together on a screen. **Written 2026-10-01,
repaired 2026-10-09.** PROMPTBLOCKS-02 and 03 were run by the delivering agent in the app on the first
date, once each, on Haiku, and passed; results for every test are in [the tracker](tracker.md).
PROMPTBLOCKS-04 (two Teammates in one Room) and PROMPTBLOCKS-06 have not been run.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Prompt blocks spec](../../Huddle.PromptBlocks-Specifications.md); its use cases are P1,
P2, P5, P6, P11 and P18.

## Setup

Run [`P-BUILD`](common.md#p-build), then the paid lane
([`P-LAUNCH-PAID`](common.md#p-launch-paid-), including its first step that makes the Chief of Staff
cheap), with one addition to the launch line: a `--Team:Library:Roots` entry pointing at a scratch
folder. Put it after `--` so that `dotnet run` passes it to the app:

```powershell
dotnet run --project src/Huddle.App --urls http://localhost:5100 -- `
  --Team:Library:Roots:0:Name=Design `
  --Team:Library:Roots:0:Path=C:\scratch\design
```

1. Create a **scratch** Teammate with [`P-NEW-PERSONA`](common.md#p-new-persona) on the stock Claude
   Adapter, Model Haiku, Effort `low`, named `Nova`. Its one-to-one Room is the **fresh direct Room**
   the tests use.
2. In `C:\scratch\design` put these files (any image editor will do; the content matters, not the size):
   - `checkout.png`: a small PNG with the words `BANANA 4821` in white on a green banner and an orange-red
     circle below it. The words are the oracle: a model cannot guess them.
   - `receipt.png`: the same layout with the words `MANGO 7316` and a blue circle. Only PROMPTBLOCKS-05
     uses it, because no session may have seen it before.
   - `huge.png`: an RGB PNG over 3 MiB (random 24-bit colour noise at 1200 × 1200 is about 4 MB;
     grayscale noise of that size is about 1.4 MB and stays under the limit).
   - `one.png` to `six.png`: six small distinct PNGs (a solid colour each is enough).
3. Every message below names a file by its **full path**, typed or pasted, not by a link, and tells the
   Teammate not to use any tool, so that "it read the file with a tool" cannot pass for "it was sent the
   image".
4. `O-LOG` shows the app log; the Information line this page looks for reads
   `Persona '<Name>' sent image paths in room <id> as path lines instead of blocks: <Reason>=<n>.`
   (`RoomSession.cs:1763-1767`). It ends with a full stop and holds counts, never a path.
5. [`O-TOOLCALLS`](common.md#o-toolcalls) is the tool-call oracle: the folder
   `<data>\logs\tool-calls\<Name>\` does not exist until the Teammate's first tool call, and a Turn
   made no tool call when the newest line of today's file has a `time` before you pressed Send (or the
   folder is absent). The live Draft's activity rows show the same calls.

## Tests

### PROMPTBLOCKS-01 — A named image is seen, without a tool call

**Paid** 💰 · about 10 min

*Proves P1: the image travels with the message, and the Teammate reads the words and the colour out of
it.*

**Steps**

1. Open `Nova`'s Room. In the Room type, with the real path:
   `Without opening any file or using any tool, tell me the words in the green banner and the colour of the circle in C:\scratch\design\checkout.png`
2. Note the time, send it and read the reply.

**Pass if — all of these**

- The reply says `BANANA 4821` and names the circle's colour as red or orange.
- `O-TOOLCALLS` for `Nova` shows no line with a `time` after you pressed Send (or the folder is
  absent), and the Draft showed no tool-call row.
- Your Message shows the path as a Library link.

**Fail if — any of these**

- The reply says it cannot see the image or asks for the file -> no block was sent. The Adapter's
  `initialize` is not observable (`TraceWire` is forbidden, it prints a secret), so check what you can:
  `Nova`'s card shows **Adapter: claude**, and `Team:Acp:Adapters:*:PromptBlocks` is not `false`.
- The reply is right only after a file-read tool call (a new `O-TOOLCALLS` line) -> the image was not in
  the prompt.

### PROMPTBLOCKS-02 — Six images, four are seen

**Paid** 💰 · about 15 min · **Run 2026-10-01: Pass** (Haiku, one sample).

*Proves P5: the per-Turn count cap of four, and that the rest are path lines, with one Information line.*

**Steps**

1. In a fresh direct Room (a second scratch Teammate, `Kai`, on the same Adapter, Haiku / low), type one
   Message naming `one.png` to `six.png` by full path, each on its own line, and asking:
   `Without using any tool, how many pictures can you actually see, and what colour is each?`
2. Read the reply, then `O-LOG`.

**Pass if — all of these**

- The reply describes four pictures, the first four named, and does not describe the last two.
- `O-LOG` has one line ending `ImageCountCap=2.` (the full stop is part of the line).
- The Turn completed; nothing failed.

**Fail if — any of these**

- Six are described -> the cap is not applied.
- Fewer than four -> a guard withheld an image it should not have; read the line in `O-LOG` for the reason.

### PROMPTBLOCKS-03 — An image over the size limit is a path line

**Paid** 💰 · about 10 min · **Run 2026-10-01: Pass** (one sample).

*Proves P6: a file over `Team:Library:MaxImageBytes` (3 MiB, `LibraryOptions.cs:33`) is not sent, the
Turn still runs, and the reason is logged.*

**Steps**

1. In `Nova`'s Room type `Reply with the single word PATHONLY. Do not open anything. The big file is C:\scratch\design\huge.png`
2. Read the reply, then `O-LOG`.

**Pass if — all of these**

- The Teammate replies `PATHONLY` and the Turn completes.
- `O-LOG` has one Information line ending `TooLarge=1.`, and it does not contain the file's path.

**Fail if — any of these**

- The Turn fails or hangs -> an oversized image reached the Adapter, or the guard threw.
- No log line -> the withheld image was not recorded.

### PROMPTBLOCKS-04 — An image mentioned only in catch-up is not re-sent

**Paid** 💰 · about 20 min

*Proves P11: only the Message that started the Turn can put an image in the prompt.*

**Steps**

1. Make a Room with the Human and **two** scratch Teammates, `Nova` and `Kai` (Haiku / low each).
2. Type `@Nova here is the picture, just say OK: C:\scratch\design\checkout.png` and wait for Nova's reply.
3. Note the time, then type `@Kai Without opening any file or using any tool, what words are in the green banner of that picture?`
4. Read Kai's reply, then `O-TOOLCALLS` for `Kai`.

**Pass if — any of these**

- Kai does **not** say `BANANA 4821`. It may say it cannot see the picture, or ask for the path.
- Kai says `BANANA 4821` only after a file-read tool call (a `Kai` line in `O-TOOLCALLS` after Send). The
  path was in catch-up and Kai has file tools, so it read the file itself; the block was not re-sent,
  which is what P11 promises.
- And in every case Kai's Turn completed.

**Fail if — any of these**

- Kai says `BANANA 4821` with no tool call (no `Kai` line in `O-TOOLCALLS` after Send, no tool-call row
  under the Draft) -> the earlier Message's image was re-sent as a block.

### PROMPTBLOCKS-05 — An Adapter Profile can turn blocks off

**Paid** 💰 · about 20 min

*Proves P18: with `PromptBlocks: false`, an Adapter that advertised `image` is sent today's prompt.*

**Before you start**

- `P-STOP`, then relaunch the paid lane with the Library root kept and one configured Adapter that is the
  stock Claude one with the switch off. An explicit `Adapters:0` entry defaults `IsolateUserSettings` to
  `false` (`AdapterProfileOptions.cs:65`), unlike the stock profile (`AdapterCatalog.cs:62-76`), so the
  line sets it; without the Library root no path is a Library path and the test would pass for nothing:

  ```powershell
  dotnet run --project src/Huddle.App --urls http://localhost:5100 -- `
    --Team:Library:Roots:0:Name=Design `
    --Team:Library:Roots:0:Path=C:\scratch\design `
    --Team:Acp:Adapters:0:Id=claude `
    --Team:Acp:Adapters:0:Command=node `
    --Team:Acp:Adapters:0:PromptBlocks=false `
    --Team:Acp:Adapters:0:IsolateUserSettings=true
  ```

- A **new** scratch Teammate, `Wren`, whose card shows **Adapter: claude**, Haiku / low, and so a fresh
  direct Room. A resumed session still holds every image it was sent earlier, so `Nova`'s Room and
  `checkout.png` must not be reused.

**Steps**

1. In `Wren`'s Room type, with the real path:
   `Without opening any file or using any tool, tell me the words in the green banner and the colour of the circle in C:\scratch\design\receipt.png`
2. Note the time, send it and read the reply, then `O-TOOLCALLS` for `Wren`.

**Pass if — all of these**

- The Teammate does **not** say `MANGO 7316` without first calling a file tool (a `Wren` line in
  `O-TOOLCALLS` after Send), or says it cannot see the image.
- Your Message shows the path as a Library link (the path is a Library path, so the switch is what
  withheld the block).
- The Turn completed.

**Fail if — any of these**

- It says `MANGO 7316` with no tool call -> the switch is not honoured.
- The app does not start -> the Adapter entry is malformed; `O-LOG` names the key.

### PROMPTBLOCKS-06 — An Adapter that never advertised image gets a path line

**Free** · about 10 min

*Proves P2: the free mock Adapter does not advertise `image` (its `initialize` carries no
`promptCapabilities`, `FakeAcpAgent.cs:246-255`), so no block is built and the path stays a path line
(`PromptBlockPlanner.cs:127-131`), with nothing logged because the image was never eligible.*

**Before you start**

- `P-STOP`, then [`P-LAUNCH-UAT`](common.md#p-launch-uat) with the Library root added to its launch
  command: `--Team:Library:Roots:0:Name=Design --Team:Library:Roots:0:Path=C:\scratch\design`. Ada and Bo
  go **Online**. `C:\scratch\design\checkout.png` from Setup exists.

**Steps**

1. Open Ada's Room and type `Here is the picture: C:\scratch\design\checkout.png`
2. Send it and read the echoed reply, then `O-LOG`.

**Pass if — all of these**

- A reply arrives, and the echoed prompt contains the text you sent and the path
  `C:\scratch\design\checkout.png` as plain text.
- Your Message shows the path as a Library link.
- `O-LOG` has no line `sent image paths in room`.

**Fail if — any of these**

- The `O-LOG` line appears -> the planner treated an Adapter without image support as eligible.
- The Turn fails -> the Adapter was sent something it cannot take.
