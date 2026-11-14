---
name: process-api-net11
description: >
  The System.Diagnostics.Process APIs new in .NET 11 (Process.Run[Async], Process.RunAndCaptureText[Async],
  Process.StartAndForget, ReadAllText/Bytes/Lines[Async], ProcessStartInfo.KillOnParentExit, InheritedHandles,
  StartDetached), applied to Huddle's house style. USE FOR: writing or changing code under src/ that starts a
  child process, captures its stdout/stderr, ties a child's lifetime to the app, or replaces hand-rolled
  ReadToEndAsync + WaitForExitAsync + Kill(entireProcessTree) code or a kill-on-close Job object (ProcessTailscaleCli
  is the done example). DO NOT USE FOR: the ACP agent's stdio transport (AgentProcessLauncher streams JSON-RPC
  over the pipes for the whole session; none of the capture APIs fit) or opening a URL/file with UseShellExecute.
license: MIT
---

# Process APIs new in .NET 11

Adapted from `dotnet/skills` (`plugins/dotnet11/skills/process-api-net11`, MIT, see
`../LICENSE-dotnet-skills`). The API facts are upstream's; the Huddle sections and the code shapes are ours.
Every sample here follows `agents/CSharpPrinciples.md`: no `!`, `ConfigureAwait(false)` in `src`, explicit types,
the token passed down.

## Where Huddle starts processes

| File | Today | What .NET 11 offers |
| --- | --- | --- |
| `src/Huddle.App/Gateway/ProcessTailscaleCli.cs` | **Done (2026-11-13).** `BuildStartInfo` sets `KillOnParentExit` on Windows and Linux (it replaced a `LibraryImport` Job object and `AllowUnsafeBlocks`); `RunAsync` reads with `ReadAllTextAsync` and keeps its own `Kill(entireProcessTree: true)` in `finally` | The reference shape for a new process call |
| `src/Huddle.Acp/Hosting/AgentProcessLauncher.cs` | Long-lived child whose stdin/stdout carry the ACP session | Only `KillOnParentExit` is relevant. **ACP effort's subtree**: propose it there, do not edit it from the chat side |
| `src/Huddle.App/Desktop/SystemBrowserOpener.cs`, `src/Huddle.App/Gateway/HostActions.cs` | `UseShellExecute = true` to open a URL or file | Nothing. Every new API below requires `UseShellExecute = false` |

## The new types

| Type | Kind | Members |
| --- | --- | --- |
| `ProcessExitStatus` | sealed class | `int ExitCode`, `bool Canceled`, `PosixSignal? Signal` |
| `ProcessTextOutput` | sealed class | `ProcessExitStatus ExitStatus`, `string StandardOutput`, `string StandardError`, `int ProcessId` |
| `ProcessOutputLine` | readonly struct | `string Content`, `bool StandardError` |

There is no `Success` property: read `ExitCode` against the command's own contract.

## The new methods

All `ProcessStartInfo` overloads need `UseShellExecute = false` (the .NET default).

```csharp
// Start, wait, return the exit status. Output is not captured; silent: true sends it to the null device.
public static ProcessExitStatus Run(string fileName, IEnumerable<string>? arguments = null, bool silent = false, TimeSpan? timeout = null)
public static Task<ProcessExitStatus> RunAsync(string fileName, IEnumerable<string>? arguments = null, bool silent = false, CancellationToken cancellationToken = default)
public static Task<ProcessExitStatus> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)

// Start, capture stdout and stderr without the pipe-buffer deadlock, wait. With a ProcessStartInfo you must set
// RedirectStandardOutput and RedirectStandardError yourself: the BCL does not modify the instance it was given.
public static ProcessTextOutput RunAndCaptureText(string fileName, IEnumerable<string>? arguments = null, TimeSpan? timeout = null)
public static Task<ProcessTextOutput> RunAndCaptureTextAsync(string fileName, IEnumerable<string>? arguments = null, CancellationToken cancellationToken = default)
public static Task<ProcessTextOutput> RunAndCaptureTextAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)

// Start, return the PID, release every handle. Unsupplied standard handles go to the null device. Throws
// InvalidOperationException if UseShellExecute or any Redirect* flag is true.
public static int StartAndForget(string fileName, IEnumerable<string>? arguments = null)
public static int StartAndForget(ProcessStartInfo startInfo)

// Instance methods on a started Process whose stdout and stderr are both redirected: read both without deadlock.
public Task<(string StandardOutput, string StandardError)> ReadAllTextAsync(CancellationToken cancellationToken = default)
public Task<(byte[] StandardOutput, byte[] StandardError)> ReadAllBytesAsync(CancellationToken cancellationToken = default)
public IAsyncEnumerable<ProcessOutputLine> ReadAllLinesAsync(CancellationToken cancellationToken = default)
```

On timeout or cancellation, `Run*` and `RunAndCaptureText*` **kill the process**. `Process.Dispose` never does;
that misconception is why `StartAndForget` exists.

## The new `ProcessStartInfo` properties

| Property | Does | Constraints |
| --- | --- | --- |
| `KillOnParentExit` | The child dies when this process exits, including a crash or a hard kill | Windows, Linux and Android only. Needs `UseShellExecute = false` |
| `InheritedHandles` (`IList<SafeHandle>?`) | Exactly which handles the child inherits; an empty list means only stdin/stdout/stderr | `SafeFileHandle` and `SafePipeHandle` only. Do not mark the handles inheritable yourself. Two concurrent starts must not pass the same handle. Cannot combine with `UserName`. Can make starts much slower on Unix without native support. On Windows it takes no global lock, so parallel starts scale |
| `StartDetached` | The child leaves the parent's terminal or job session and outlives it | Needs `UseShellExecute = false` |

## House shapes

### Run a CLI and read its output

```csharp
ProcessStartInfo startInfo = new(this.executablePath, arguments)
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};

using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
limit.CancelAfter(timeout);

try
{
    ProcessTextOutput result = await Process.RunAndCaptureTextAsync(startInfo, limit.Token).ConfigureAwait(false);
    return (result.ExitStatus.ExitCode, result.StandardOutput, result.StandardError);
}
catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
{
    // The limit fired, not the caller; the process has already been killed.
    throw new TimeoutException($"tool did not finish within {timeout}.", ex);
}
```

`ProcessTailscaleCli` uses the instance form instead (`Process.Start`, `ReadAllTextAsync`, `WaitForExitAsync`, an
explicit tree kill in `finally`) because the tree kill was proven only on Windows; see the traps.

### Tie a child to the app's lifetime

Guard the **assignment**, not just the value, and decide what an unsupported OS (macOS, which Huddle ships to) gets:

```csharp
if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
{
    startInfo.KillOnParentExit = true;
}
```

## Traps before replacing working code

Probed on Windows, SDK 11.0.100, 2026-11-13:

- **Cancellation throws.** A cancelled `RunAndCaptureTextAsync` throws `OperationCanceledException`; it does not
  return `ExitStatus.Canceled = true`. A `catch ... when (!cancellationToken.IsCancellationRequested)` still tells a
  timeout from the caller.
- **Grandchildren die on Windows.** Both a cancelled `RunAndCaptureTextAsync` and `KillOnParentExit` (parent ended
  by `Environment.FailFast`) killed a grandchild started with `detached: true`; a plain `Kill()` left it running.
  **Not probed on Linux or macOS.** Keep an explicit `Kill(entireProcessTree: true)` where a grandchild matters.
- **A probe with node needs `detached: true`.** On Windows libuv puts node's own children in a kill-on-close job, so a
  non-detached grandchild dies with any kill and the probe proves nothing.
- **`KillOnParentExit` is `[SupportedOSPlatform]` windows, linux, android.** `CA1416` fails the build on an
  unguarded read or write, and `Assert.SkipUnless` in a test does not count as a guard: put the access inside an
  `if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())`.
- **`RedirectStandardInput = true` is accepted** by the capture APIs: the child sees an open pipe that never reaches
  EOF, as with a plain `Process.Start`. Without it the child inherits Huddle's stdin.
- **`KillOnParentExit` is not on macOS.** Record what a crash leaves behind there in `known-limits.md`.
- **A behaviour change is a test change.** Pin the timeout-kill and the parent-exit kill with tests that fail on the
  old code path, per `agents/Testing.md`.
- `[LibraryImport]` code you delete may be the only user of `AllowUnsafeBlocks` or a `NativeMethods` suppression;
  remove those with it.

## More

- [Process class](https://learn.microsoft.com/dotnet/api/system.diagnostics.process)
- [ProcessStartInfo class](https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo)
