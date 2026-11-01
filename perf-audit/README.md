# perf-audit

The string-allocation audit of 2026-10-31. Start with [report.md](report.md).

| File | What it is |
| --- | --- |
| [report.md](report.md) | The final report: numbers, fixes, rejections, what needs a decision |
| [findings.md](findings.md) | Hot candidates ranked by their share of string allocations |
| [candidates.md](candidates.md) | Every static hit (grep over `src`), hot or not |
| [projects.md](projects.md) | Target frameworks and the pre-change build and test baseline |
| `alloc-before.md`, `alloc-after.md` | Ranked allocation reports from the 90 s traces |
| `counters-*.csv`, `counters-*.txt` | 90 s of `System.Runtime` counters, and their summary |
| `measure.ps1` | Runs one measurement end to end |
| `BenchmarkDotNet.Artifacts/results/` | Benchmark results per fix |

The raw `.nettrace` files are 30 to 70 MB and are not committed (`.gitignore`).

## Repeating a measurement

The workload runs real, paid Claude agents. Spend is capped by `--Team:Acp:TokenBudget=600000` for each app run,
and it posts one request every 15 s.

1. Build in Release: `dotnet build Huddle.slnx -c Release`, then
   `dotnet build tools/LoadDriver -c Release` and `dotnet build tools/AllocReport -c Release`.
2. Make a data folder from the software-co seed (`C:\seeds\huddle\software-co\data`). Start the app on it once
   with the settings below, connect `pwsh tools/echo-bot.ps1 -Name loadbot -Pipe huddle-perf`, then invite one
   Persona into loadbot's Room with `/invite @Grace`. Use only one: a second Agent in the Room lets replies
   wake each other. Stop everything and keep that folder as the template. Copy the template afresh for every run.
3. Start the app in Release with these settings:
   `--environment Development` (static web assets are served only there under `dotnet run`),
   `--Logging:LogLevel:Agency.Huddle=Information`, `--Team:FileLog:Enabled=false`,
   `--Team:DataDir=<copy>`, `--Team:PipeName=huddle-perf`, `--urls http://localhost:5199`,
   `--Team:DemoAgent:Enabled=false`, `--Team:Tasks:WakeEnabled=false`, `--Team:AgentMessageBudget=0`,
   `--Team:Acp:Enabled=true`, `--Team:Acp:TokenBudget=600000`.
4. Open three browser tabs on the app: the loadbot Room, `/tasks` and `/library`.
5. Run `pwsh perf-audit/measure.ps1 -Label <name>`.

Run a benchmark with:

```powershell
dotnet run -c Release --project tools/Benchmarks -- --filter "*SkillDisplayStateBenchmarks*"
```
