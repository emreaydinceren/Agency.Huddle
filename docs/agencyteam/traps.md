# Traps

**Binding.** Things that fail silently, mostly on the ACP and wire side. Read
this before changing `Huddle.Acp`, `Huddle.Contracts`, the protocol version, or
the model-config path — and alongside [Rules](rules.md) before any change to
`src/Huddle.App`.

What these share: none of them produce an error. A wrong `configId`, a missing
`type` discriminator, a renamed contract property and a `$` anchor all build
clean and fail at runtime, or worse, degrade into something that looks like a
legitimate empty result. Back to the hub: [AgencyTeam.md](../AgencyTeam.md).

- **Registering a tool is not the same as the model finding it.** The system
  prompt must spell tool names `mcp__team__list_agents` and so on, in full. A
  bare name produces "no such tool exists".
- **`SetSessionConfigOptionRequest.Type` defaults to `""` and is the wire
  discriminator.** Leave it unset and the adapter receives `{"type":""}`. It must
  be set to `"select"` explicitly. Found by decompiling `dotacp.protocol`; now
  shown in `session-config-options.md`'s worked example.
- **A select's options are a union of a flat list *or* named groups.** Handle only
  the flat branch and a grouped catalog reads as **no models offered**, silently,
  with no error anywhere — a failure that looks exactly like the legitimate
  "this agent advertises none" case. `ModelConfigOptions` walks both.
- **C# does not chain two user-defined implicit conversions.** Assigning a bare
  `string` model id to `SetSessionConfigOptionRequest.Value` will not compile; it
  goes through `SessionConfigValueId` first.
- **Read `id`, write `configId`.** The ACP spec says `configId`; the adapter emits
  `id`. `dotacp` already handles both, so do not "fix" it. A related trap: an
  option's label is `name` (the schema requires `value` and `name`), *not*
  `displayName` — reading `displayName` silently yields nothing and degrades the
  label to the raw id. `session-config-options.md` now calls this out.
- **Never hardcode a `configId`.** They are adapter-defined. Read the owning
  option's own id back off the `session/new` response every time.
- **The wire contract has no explicit declaration.** There are no
  `[JsonPropertyName]` attributes anywhere in `src/`; `ProtocolJson` derives every
  wire name from its C# member name via `JsonNamingPolicy.CamelCase`. **Renaming
  a property in `Huddle.Contracts` silently changes the protocol** with no compiler
  or analyser signal. One test, in `ProtocolJsonTests`, pins the literal JSON.
- **The protocol version is a strict equality check.** `ProtocolJson` throws on
  any version that is not `ProtocolVersion.Current`, so bumping it means updating
  every client in the same commit, `tools/echo-bot.ps1` included.
- **A `Logging:LogLevel` key is a namespace prefix, so the 2026-09-12 namespace
  rename moved it and the `Team:` config root did not.** `ILogger<T>` takes its
  category from `typeof(T).FullName`, so the filters now read `"Agency.Huddle"`.
  A stale `"Team"` key matches nothing, every logger falls back to `Default`, and
  nothing anywhere reports it — in `Huddle.Console`, where `Default` is `Warning`,
  that means its Information logs simply stop appearing. The hand-written
  `Agency.Huddle.Acp.Wire` trace category in `DotAcpAgentHost` is a string, not a
  type, so it has to be kept in step by hand; no test pins it.

- **Changing `UserKind` or the SQL `CHECK` constraint needs a fresh database.**
  The schema is created with `CREATE TABLE IF NOT EXISTS`, so an existing
  `team.db` keeps its old constraint forever. Delete `App_Data` instead.
- **ACP spends real money — but only the `E2E/` folder does.** This warning used
  to say "never run `tests/Huddle.Acp.Tests`", which is broader than the truth and
  contradicts [Build, test, run](../AgencyTeam.md#build-test-run). `E2E.Enabled` gates on
  `TEAM_E2E == "1"`, so with that variable unset the project runs free and its
  eight E2E tests report as skipped. Never *set* `TEAM_E2E` unless you mean to.
- **`$` is not the end of the string in .NET.** It also matches *before* a
  trailing newline, so `^[a-z]+$` accepts `"coo\n"`. Both guards in `NameRules`
  anchor with `\A` and `\z` for that reason. Any new validation regex should too.
- **Anything that parses a Name must resolve it against the Members.** A Name may
  contain spaces, so a pattern that spells one out truncates `@Emily Lee` to
  `Emily` and then reports the wrong Name as unknown. `MentionParser` and the
  `/invite` command each learned this the same way.
- **Unsubscribe from `RoomEvents` in `Dispose`.** The hub is a singleton.
- **A system prompt and a Model are both fixed at `session/new`.** Changing
  either on a Persona means restarting the session. There is no other way, and
  the session's conversation memory goes with it.
- **The `thought_level` entry is rebuilt on every model switch.** Resolve effort
  against the `set_config_option` **response**, not the `session/new` snapshot.
  The adapter clamps a level the new model does not support back to `"default"`
  with no error, so the stale-snapshot bug presents as "the effort just didn't
  take".
- **`SetSessionConfigOptionResponse.ConfigOptions` IS that post-switch
  snapshot** — "the full set of configuration options and their current
  values". `DotAcpAgentHost` discarded this return value until now; discarding
  it again costs either an extra round trip or a wrong answer.
- **An absent `thought_level` entry is NOT the model catalog's "unknown".** For
  models, empty means "the agent never told us". For effort, empty means "this
  model offers no effort choice" — a real answer. Treating them alike puts a
  ghost option in the picker.
- **`FileSystemWatcher.IncludeSubdirectories` defaults to `false`.** Leave it
  unset and a Persona under a Team sub-folder is found once by the startup scan
  and then never reloads, no matter how often it is edited. No error, no log,
  nothing — the file simply stops mattering. This is the single easiest way to
  break the Teams directory.
- **A watcher's filename filter applies to the thing that changed, not to what
  is under it.** `new FileSystemWatcher(dir, "*.md")` never sees
  `Teams/Business` being renamed to `Teams/BusinessOps`, because the event's
  name is the *directory*. The filter is `"*"` and the narrowing happens in the
  handler for exactly this reason, with `NotifyFilters.DirectoryName` set so the
  event is raised at all. A directory *deleted* is the same hazard from the
  other side: its name has no extension, so the handler accepts extensionless
  names too.
- **`FileSystemWatcher` drops events when its buffer overflows, and only says so
  through `Error`.** The internal buffer is 8 KB by default; a `git checkout` or
  a script touching a dozen Persona files at once is enough. The OS discards the
  overflow and the app's view stays wrong until something unrelated happens to
  touch a file. `PersonaStore` subscribes to `Error` and forces a rebuild, and
  runs a 64 KB buffer to make it rarer. An unhandled `Error` is a silent,
  permanent desync.
- **Extra flags before the trailing `--` make `dotnet test` report "Zero tests
  ran".** The documented `dotnet test Huddle.slnx --` is exact. Adding
  `--nologo` or `-v minimal` ahead of the `--` exits 5 having run nothing, which
  scrolls past looking like a pass. Run the command verbatim.
- **Editing a Persona's `name:` renames the Teammate, and only the Model and
  Effort follow it.** `PersonaStore.Update` moves those two rows by hand
  precisely because nothing else would. The old Agent row, its Rooms and its
  Transcripts stay behind under the old Name — the same non-cascading semantics
  removing a Persona has, and deliberate, but it means a rename leaves a ghost
  in the Team Directory.
