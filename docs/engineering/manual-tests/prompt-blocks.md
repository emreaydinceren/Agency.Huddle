# Prompt blocks

Prove, in a real browser against a real Adapter, that a Library image the Human's Message names is
**seen** by the Teammate without a tool call, that a named image over a limit quietly falls back to its
path, that an image mentioned only in catch-up is not re-sent, and that an Adapter Profile can turn the
whole thing off. The automated suite proves the parts, and the delivering agent ran the wire-level
checks live (see below); this page proves they hold together on a screen. **Written 2026-10-01.**
PROMPTBLOCKS-01, 02, 03 and 05 were run by the delivering agent in the app on that date, once each, on
Haiku, and passed; PROMPTBLOCKS-04 (two Teammates in one Room) was not run.

**5 tests** · all paid 💰 (cheap: an image cost about 1,000 tokens) · about 1 hour.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below. The
design is [the Prompt blocks spec](../../Huddle.PromptBlocks-Specifications.md); its use cases are P1,
P5, P6, P11 and P18.

## Setup

Run [`P-BUILD`](common.md#p-build) then the paid lane from [Common procedures](common.md), with these
additions to the launch line (one `--Team:Library:Roots` entry pointing at a scratch folder):

```powershell
dotnet run --project src/Huddle.App --urls http://localhost:5100 `
  --Team:Library:Roots:0:Name=Design `
  --Team:Library:Roots:0:Path=C:\scratch\design
```

1. Use a **scratch** Teammate on the stock Claude Adapter, Haiku / low, and a **fresh direct Room**.
2. In `C:\scratch\design` put these files (any image editor will do; the content matters, not the size):
   - `checkout.png`: a small PNG with the words `BANANA 4821` in white on a green banner and an orange-red
     circle below it. The words are the oracle: a model cannot guess them.
   - `huge.png`: a PNG over 3 MiB (random noise at 1200 × 1200 is about 4 MB).
   - `one.png` to `six.png`: six small distinct PNGs (a solid colour each is enough).
3. Every message below names a file by its **full path**, typed or pasted, not by a link, and tells the
   Teammate not to use any tool, so that "it read the file with a tool" cannot pass for "it was sent the
   image".
4. `O-LOG` shows the app log; the Information lines this page looks for are
   `sent image paths in room … as path lines instead of blocks: …`.

## Tests

### PROMPTBLOCKS-01 — A named image is seen, without a tool call

**Paid** 💰 · about 10 min · **Run 2026-10-01: Pass** (Haiku, one sample).

*Proves P1: the image travels with the message, and the Teammate reads the words and the colour out of
it.*

**Steps**

1. In the Room type, with the real path:
   `Without opening any file or using any tool, tell me the words in the green banner and the colour of the circle in C:\scratch\design\checkout.png`
2. Send it and read the reply.

**Pass if — all of these**

- The reply says `BANANA 4821` and names the circle's colour as red or orange.
- The app log shows no tool call for a file read on that Turn.
- Your Message shows the path as a Library link.

**Fail if — any of these**

- The reply says it cannot see the image or asks for the file -> no block was sent. Check that the
  Adapter advertised `image` (`O-WIRE`, `initialize`) and that `Team:Acp:Adapters:*:PromptBlocks` is not `false`.
- The reply is right only after a file-read tool call -> the image was not in the prompt.

### PROMPTBLOCKS-02 — Six images, four are seen

**Paid** 💰 · about 15 min · **Run 2026-10-01: Pass** (Haiku, one sample).

*Proves P5: the per-Turn count cap of four, and that the rest are path lines, with one Information line.*

**Steps**

1. Type one Message naming `one.png` to `six.png` by full path, each on its own line, and asking:
   `Without using any tool, how many pictures can you actually see, and what colour is each?`
2. Read the reply, then `O-LOG`.

**Pass if — all of these**

- The reply describes four pictures, the first four named, and does not describe the last two.
- `O-LOG` has one line ending `ImageCountCap=2`.
- The Turn completed; nothing failed.

**Fail if — any of these**

- Six are described -> the cap is not applied.
- Fewer than four -> a guard withheld an image it should not have; read the line in `O-LOG` for the reason.

### PROMPTBLOCKS-03 — An image over the size limit is a path line

**Paid** 💰 · about 10 min · **Run 2026-10-01: Pass** (one sample).

*Proves P6: a file over `Team:Library:MaxImageBytes` is not sent, the Turn still runs, and the reason is logged.*

**Steps**

1. Type `Reply with the single word PATHONLY. Do not open anything. The big file is C:\scratch\design\huge.png`
2. Read the reply, then `O-LOG`.

**Pass if — all of these**

- The Teammate replies `PATHONLY` and the Turn completes.
- `O-LOG` has one Information line ending `TooLarge=1`, and it does not contain the file's path.

**Fail if — any of these**

- The Turn fails or hangs -> an oversized image reached the Adapter, or the guard threw.
- No log line -> the withheld image was not recorded.

### PROMPTBLOCKS-04 — An image mentioned only in catch-up is not re-sent

**Paid** 💰 · about 20 min

*Proves P11: only the Message that started the Turn can put an image in the prompt.*

**Steps**

1. Make a Room with the Human and **two** scratch Teammates, `Nova` and `Kai`.
2. Type `@Nova here is the picture, just say OK: C:\scratch\design\checkout.png` and wait for Nova's reply.
3. Type `@Kai Without opening any file or using any tool, what words are in the green banner of that picture?`

**Pass if — all of these**

- Kai does **not** say `BANANA 4821`. It may say it cannot see the picture, or ask for the path.
- Kai's Turn completed.

**Fail if — any of these**

- Kai says `BANANA 4821` with no tool call -> the earlier Message's image was re-sent as a block.

### PROMPTBLOCKS-05 — An Adapter Profile can turn blocks off

**Paid** 💰 · about 20 min · **Run 2026-10-01: Pass** (Haiku, one sample; use an image the session has not seen, because a resumed session still holds earlier ones).

*Proves P18: with `PromptBlocks: false`, an Adapter that advertised `image` is sent today's prompt.*

**Before you start**

- `P-STOP`, then relaunch with one configured Adapter that is the stock Claude one with the switch off:
  add `--Team:Acp:Adapters:0:Id=claude --Team:Acp:Adapters:0:Command=node --Team:Acp:Adapters:0:PromptBlocks=false`.
- A Teammate whose card shows **Adapter: claude**, Haiku / low.

**Steps**

1. Repeat PROMPTBLOCKS-01's message exactly.

**Pass if — all of these**

- The Teammate does **not** say `BANANA 4821` without first calling a file tool, or says it cannot see
  the image.
- The Turn completed.

**Fail if — any of these**

- It says `BANANA 4821` with no tool call -> the switch is not honoured.
- The app does not start -> the Adapter entry is malformed; `O-LOG` names the key.
