<#
.SYNOPSIS
    Captures a computed-style snapshot of the running app, or diffs two snapshots. Proves a CSS change altered nothing visible.

.DESCRIPTION
    -Mode Capture seeds an instance (`huddle-seed --today 2026-11-01 --root <SeedRoot>`) once, lets the app start once on it
    and keeps that folder as <SeedRoot>.golden; every Capture then restores <SeedRoot> from the golden copy, so two
    Captures render the same Room ids, order and avatar colours (two fresh seeds do not: the ids are time-based). It
    starts the app on the mock adapter
    with Team:Tasks:WakeEnabled=false, its own port and a pipe name of huddle-<leaf of -SeedRoot> (huddle-css-snap by default; override with -PipeName), then drives headless Chrome (DevTools
    protocol, no npm packages) over every route in each of the 8 Theme and mode combinations at widths 1280 and 768. It
    writes one gzip JSON file per (route, Theme, width) plus snapshot-meta.json into -Out, and stops the app it started.
    It reads the rendered page only, so it does not depend on stylesheet file names. The Node half is css-snapshot.mjs.

    -Mode Diff compares two snapshot folders and reports every difference, grouped by route | Theme | width, then by
    element path and property, with a rolled-up list of the distinct changes first. It exits 0 only when there are none.

    -Mode Report makes the same comparison as Diff (same tolerance, same volatile-element filters) and writes it as a
    self-contained HTML report into -Out: index.html lists every changed (route, Theme, width), undeclared differences
    first, and pages\<name>.html shows that page's before and after screenshots side by side, a pixel-difference image
    (changed pixels magenta, the rest faded, with a pale halo so a one-pixel change shows), and the property diff.
    Images are copied into img\, so the folder opens offline. Capture both folders with -Screens to get pictures; without
    them the report still has the property diff. A pixel difference is only looked for on pages whose recorded properties
    changed, so a repaint the recorded properties cannot see is not listed.

    -Declared <file> sorts every difference as declared or undeclared. Without it, all are undeclared. The index shows
    the undeclared count first, the distinct undeclared changes, then the declared ones, and warns about any declaration
    that matched nothing (a change you promised and the build did not make). Report exits 0 when nothing is undeclared
    (no difference, or all declared) and 1 otherwise.

    DECLARED FILE FORMAT. Plain text, one declaration per line, fields separated by `|`; a line starting with `#` and
    blank lines are ignored:

        selector | property | before -> after | pages

      selector  required. Either a substring of the element path exactly as Diff prints it (for example
                `notifications-badge`), or a CSS-like chain: `tag.class.class`, `.class`, `*`, with descendants written
                by a space or `>` (both mean "somewhere below"), for example `.settings-section-title.mud-typography`
                or `.settings-panel .mud-typography`. Class order does not matter. It matches the element AND everything
                below it, so name the property to keep the declaration tight. `*` matches every element and the
                page-level notes.
      property  optional, default `*`. A computed property as Diff prints it (`margin`, `background-color`,
                `::before color`), a prefix with a trailing `*` (`margin*`, `border-*`), or `*`. Other names you can use:
                `(element added)`, `(element removed)`, `page documentHeight`, `page documentWidth`, `page finalPath`.
      before -> after   optional. Computed values, compared after collapsing white space; either side may be `*`. Omit to
                accept any value. The arrow can also be written as a Unicode arrow.
      pages     optional. A regex (case-insensitive) tested against `route | Theme | width`, for example
                `lumen-light|lumen-dark` or `settings-.* \| .* \| 1280`. Omit for every page.

    Layout knock-on is real: a margin on one element moves every later sibling, grows its ancestors and the document
    height, so declare those too (`.page-root | height`, `* | page documentHeight`) or they show as undeclared.

    A .json file is read instead as an array of objects with the same fields: selector, property, from, to, pages.
    An example is agents\scripts\css-snapshot-declared.example.txt.

    -Screens (Capture) saves one PNG per page beside its JSON, named like it (route__theme__width.png): the viewport,
    width x 900 (not the full page: the app scrolls inside its panes, so the document is the viewport), taken after the
    settle and the snapshot with motion off, so it shows exactly what was measured. Cost: see NOTES.

    Typical use for a CSS refactor: Capture a baseline before the change, make the change, rebuild, Capture a candidate,
    Diff them (or Report them). Take both on the same calendar day (the app derives overdue and relative days from today's date).

    The 8 Themes are lumen, fluent, nordic and cobalt-soft, each light and dark. A Theme is switched by writing
    appearance.json in the instance's data folder (the store watches it) and checking that a fresh page reports the
    expected data-huddle-theme marker; every page is checked again against its own marker, and a Theme that did not take
    fails the run. appearance.json is restored afterwards.

    ROUTES (about 46, from the seed's manifest.md so Room ids need no editing): /feed, /needs-you, /agents, /teams/build,
    /scheduled, /library, two open Library notes, five Rooms (group, one-to-one, Needs you, incident, Team Room, Team
    chat), /teams/Platform with each of its five tabs, /teams/Sandbox, a second Team, a Project page (Files, Tasks),
    /tasks/all-tasks, a Kanban View, a List View, /tasks/item/PLAT-0002 (read view) and its /edit page, a Request Task,
    every /settings/<tab> the Settings menu shows (read from the running app) and /settings/theme-lab. Four opened states
    are captured by JavaScript clicks: the notifications panel, the New chat panel, the Archived page (opened from its Activity bar button) and a Room's
    @ reference picker.

    RECORDED: for every element under body, a stable path (tag, sorted classes without generated ones, and the count of
    earlier siblings with the same tag and classes; ids and Blazor b-xxxxxx scope attributes are not used) and about 120
    computed properties: box, flex and grid, type, colour, border, outline, shadow, effects, cursor, transform and the
    transition and animation declarations; plus ::before and ::after (content, colour, background, box, border) where
    the element has them and ::placeholder on inputs; plus the document's scroll size and the final URL.

    SOURCE FILES: the app runs with the project folder as its content root, so it reads the skin JSON and the stylesheets
    from source, not from the build output. Editing the checkout during a Capture changes what is captured; do not edit
    src\Huddle.App while one runs (a -Build before it is only needed for code).

    EXCLUSIONS (what the snapshot browser neutralises or leaves out; none of it touches the repo):
      - Animations and transitions: `*{transition:none!important;animation:none!important;caret-color:transparent}` is
        injected in the snapshot browser only, after the transition and animation declarations were read, so what an
        animation does between frames is not seen.
      - Relative times ("5 minutes ago", "in 2 hours", "just now", a Feed card's "now") are rewritten to fixed text before measuring, because
        the seed pins dates but the app renders them against the real clock and the text width moves with them.
      - The caret, focus (the active element is blurred), hover (the mouse is never moved) and scroll positions.
      - Settling: fonts (document.fonts.ready), images, a quiet DOM (no mutation for 700 ms, at least 1.5 s, at most 20 s),
        then a double requestAnimationFrame. A page that never goes quiet is listed as a warning.
      - script, style, noscript, template, link and meta elements; ids; attributes; text content; classes that look
        generated (GUIDs, 12+ hex digits); 32-digit hex ids in the final URL (Room ids differ on every seed).
      - Every page gets a fresh browser context, so local storage, tab restore and sidebar widths start empty.
      - Chrome flags: headless, device scale 1, no hinting; scrollbars are left as Chrome draws them.

    CANNOT SEE: hover, focus, active, :focus-visible and :disabled-by-interaction states; anything that needs a keypress or
    drag; transient states (toasts, a streaming reply, a spinner mid-spin, a drag in progress); the motion itself; print and
    forced-colors media; other widths and heights; other viewports than 1280 and 768 wide by 900 high; Rooms in the
    middle of a turn; data the seed does not contain; and a rule that no element on any captured page matches.

.PARAMETER Mode        Capture, Diff or Report.
.PARAMETER Out         Capture: the folder to write. It is emptied of earlier snapshot files first; a non-snapshot folder is refused. Report: the report folder (index.html, pages\, img\); it is rewritten, and a folder holding anything else is refused.
.PARAMETER Screens     Capture: also save a PNG screenshot of each page (viewport, width x 900) beside its JSON.
.PARAMETER Pages       Diff and Report: a case-insensitive regex on `route | Theme | width`; only the matching pages are compared (for example `lumen-` for the two Lumen Themes, or `^settings-.*1280`).
.PARAMETER Declared    Report: a declared-changes file (format above). Differences it covers are declared; the rest are undeclared.
.PARAMETER Port        Capture: the port for the instance. Default 5300 (use 5300 and up).
.PARAMETER PipeName    Capture: the app's Team:PipeName. Default huddle-<leaf of -SeedRoot>, so two captures with different seed roots (css-snap and css-snap-w0) never share a pipe; the second app to start on one pipe dies.
.PARAMETER SeedRoot    Capture: the seed root. Default C:\seeds\huddle\css-snap. Wiped and rebuilt by huddle-seed.
.PARAMETER Reseed      Capture: build a new golden copy (seed, first start, copy to <SeedRoot>.golden) instead of restoring the existing one. Baselines and candidates are comparable only when taken from the same golden copy.
.PARAMETER Attach      Capture: use an instance already running on -Port instead of seeding and launching one. Needs -DataDir.
.PARAMETER DataDir     Capture with -Attach: that instance's Team:DataDir (appearance.json is written there and restored).
.PARAMETER Manifest    Capture with -Attach: the seed's manifest.md. Defaults to <DataDir>\..\manifest.md.
.PARAMETER Build       Capture: build Huddle.App, Huddle.MockAdapter and Huddle.Seeder first (needed after a CSS or code change).
.PARAMETER Themes      Capture: comma list of Theme ids to capture. Default all 8.
.PARAMETER Widths      Capture: comma list of widths. Default 1280,768.
.PARAMETER Only        Capture: a regex on the route name, to capture a few routes (see -ListRoutes).
.PARAMETER Concurrency Capture: pages in flight at once. Default 6.
.PARAMETER WarmupSeconds Capture: wait after the app answers, for Agents to connect. Default 20.
.PARAMETER Chrome      Capture: the Chrome executable. Default C:\Program Files\Google\Chrome\Application\chrome.exe.
.PARAMETER KeepRunning Capture: leave the instance it launched running (it is yours to stop).
.PARAMETER Baseline    Diff and Report: the baseline snapshot folder.
.PARAMETER Candidate   Diff and Report: the candidate snapshot folder.
.PARAMETER Full        Diff: list every element and every distinct change instead of the first 25 elements per page.
.PARAMETER MaxElements Diff: elements listed per page without -Full. Default 25.
.PARAMETER Tolerance   Diff and Report: px lengths (and a transform's translation) that differ by at most this much count as
                       equal. Default 0.25. Text in the Nordic Theme's web font measures up to about 0.15 px
                       differently between two captures of one build; a CSS change moves whole pixels. Only px
                       numbers are loosened. 0 compares exactly. The Diff also ignores
                       the notification count's badge wrapper, which is there or not depending on which page marked
                       Feed items seen first, MudBlazor's tab slider, which script sizes from a load-time measure,
                       and the scroll transform on an overflowing tab bar's wrapper (script sets it; it differs by a
                       few px between two loads of one page).

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Capture -Out C:\seeds\huddle\css-snap-out\baseline -Build
    pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Capture -Out C:\seeds\huddle\css-snap-out\candidate -Build
    pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Diff -Baseline C:\seeds\huddle\css-snap-out\baseline -Candidate C:\seeds\huddle\css-snap-out\candidate
    pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Capture -Out C:\seeds\huddle\css-snap-out\baseline -Screens
    pwsh -NoProfile -File agents/scripts/Css-Snapshot.ps1 -Mode Report -Baseline C:\seeds\huddle\css-snap-out\baseline -Candidate C:\seeds\huddle\css-snap-out\candidate -Out C:\seeds\huddle\css-snap-out\report -Declared agents\scripts\css-snapshot-declared.example.txt

.NOTES
    Exit codes: 0 captured or no differences; 1 a page failed, or differences found; 2 bad arguments or setup.
    A full Capture takes about 8 to 10 minutes and about 20 MB. Chrome and the app run only for that time.
    With -Screens a full Capture (720 pages) measured 407 s and 102 MB: 17 MB of JSON and 85 MB of PNG (about 120 KB a
    page). A Report over two such folders takes about 15 s plus about 0.1 s per changed page, and writes three PNGs
    (before, after, difference; about 0.3 MB) per changed page, so a change that touches every page makes a report of
    about 400 MB; scope it with -Only or -Themes when you only need to look.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Capture', 'Diff', 'Report')][string]$Mode,
    [string]$Out,
    [int]$Port = 5300,
    [string]$SeedRoot = 'C:\seeds\huddle\css-snap',
    [switch]$Reseed,
    [switch]$Attach,
    [string]$DataDir,
    [string]$Manifest,
    [switch]$Build,
    [string]$Themes,
    [string]$Widths,
    [string]$Only,
    [int]$Concurrency = 6,
    [int]$WarmupSeconds = 20,
    [string]$Chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe',
    [switch]$KeepRunning,
    [string]$Baseline,
    [string]$Candidate,
    [switch]$Full,
    [int]$MaxElements = 25,
    [double]$Tolerance = 0.25,
    [switch]$Screens,
    [string]$Declared,
    [string]$Pages,
    [string]$PipeName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$node = Join-Path $PSScriptRoot 'css-snapshot.mjs'
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'node is not on PATH (Node 24 is expected).' }

if ($Mode -eq 'Diff') {
    if (-not $Baseline -or -not $Candidate) { throw '-Mode Diff needs -Baseline and -Candidate.' }
    $diffArgs = @($node, 'diff', '--baseline', $Baseline, '--candidate', $Candidate, '--max-elements', $MaxElements, '--tolerance', $Tolerance.ToString([System.Globalization.CultureInfo]::InvariantCulture))
    if ($Pages) { $diffArgs += @('--pages', $Pages) }
    if ($Full) { $diffArgs += '--full' }
    & node @diffArgs
    exit $LASTEXITCODE
}

if ($Mode -eq 'Report') {
    if (-not $Baseline -or -not $Candidate) { throw '-Mode Report needs -Baseline and -Candidate.' }
    if (-not $Out) { throw '-Mode Report needs -Out (the report folder to write).' }
    if ($Declared -and -not (Test-Path -LiteralPath $Declared)) { throw "The -Declared file '$Declared' was not found." }
    $reportArgs = @($node, 'report', '--baseline', $Baseline, '--candidate', $Candidate, '--out', ([System.IO.Path]::GetFullPath($Out)), '--tolerance', $Tolerance.ToString([System.Globalization.CultureInfo]::InvariantCulture))
    if ($Declared) { $reportArgs += @('--declared', ([System.IO.Path]::GetFullPath($Declared))) }
    if ($Pages) { $reportArgs += @('--pages', $Pages) }
    & node @reportArgs
    exit $LASTEXITCODE
}

if (-not $Out) { throw '-Mode Capture needs -Out.' }
if (-not (Test-Path -LiteralPath $Chrome)) { throw "Chrome was not found at '$Chrome'." }
$Out = [System.IO.Path]::GetFullPath($Out)

$appProject = Join-Path $repoRoot 'src\Huddle.App'
$mockExe = Join-Path $repoRoot 'src\Huddle.MockAdapter\bin\Debug\net11.0\mock-acp.exe'
$seedExe = Join-Path $repoRoot 'src\Huddle.Seeder\bin\Debug\net11.0\huddle-seed.exe'

if ($Build) {
    foreach ($project in 'src\Huddle.App', 'src\Huddle.MockAdapter', 'src\Huddle.Seeder') {
        Write-Host "Building $project ..."
        & dotnet build (Join-Path $repoRoot $project) -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $project." }
    }
}

function Test-PortAnswers([int]$TestPort) {
    try { $null = Invoke-WebRequest "http://localhost:$TestPort/feed" -UseBasicParsing -TimeoutSec 3; return $true }
    catch { return ($null -ne $_.Exception.PSObject.Properties['Response']) }
}

$appProcess = $null
if ($Attach) {
    if (-not $DataDir) { throw '-Attach needs -DataDir (the running instance''s Team:DataDir).' }
    if (-not (Test-PortAnswers $Port)) { throw "Nothing answers on port $Port." }
    if (-not $Manifest) { $Manifest = Join-Path (Split-Path $DataDir -Parent) 'manifest.md' }
}
else {
    foreach ($exe in $mockExe, $seedExe) { if (-not (Test-Path -LiteralPath $exe)) { throw "Missing $exe. Build first (add -Build)." } }
    if (Test-PortAnswers $Port) { throw "Something already answers on port $Port, and it is not an instance this script started. Pick another -Port." }

    if ((Split-Path $SeedRoot -Leaf) -notlike 'css-snap*') { throw "-SeedRoot must be a folder named css-snap*, because it is wiped on every Capture (got '$SeedRoot')." }
    $DataDir = Join-Path $SeedRoot 'data'
    if (-not $PipeName) { $PipeName = 'huddle-' + (Split-Path $SeedRoot -Leaf) }
    $Manifest = Join-Path $SeedRoot 'manifest.md'
    $golden = "$SeedRoot.golden"

    # The same environment scrub as the manual-run launcher: a Claude Code or test-lane variable must not reach the app.
    foreach ($name in @(Get-ChildItem Env: | Where-Object Name -Match '^(CLAUDECODE|CLAUDE_CODE_|ANTHROPIC_|TEAM_E2E)' | ForEach-Object Name)) {
        [Environment]::SetEnvironmentVariable($name, $null)
    }
    # The logs sit beside the seed root, not in -Out: a snapshot folder holds snapshot files only.
    $appOut = "$SeedRoot.app.out.log"
    $appErr = "$SeedRoot.app.err.log"

    function Start-SnapshotApp {
        $launch = @(
            'run', '--project', $appProject, '--no-build', '--', '--urls', "http://localhost:$Port",
            "--Team:DataDir=$DataDir", "--Team:PipeName=$PipeName", '--Team:DemoAgent:Enabled=false', '--Team:Tasks:WakeEnabled=false',
            '--Team:Acp:Enabled=true', '--Team:Acp:Adapters:0:Id=mock', "--Team:Acp:Adapters:0:Command=$mockExe",
            '--Team:Acp:Adapters:0:Args:0=--mock', '--Team:Acp:Adapters:0:UsesToolNamePrefix=false'
        )
        $process = Start-Process dotnet -ArgumentList $launch -PassThru -WindowStyle Hidden -RedirectStandardOutput $appOut -RedirectStandardError $appErr
        Write-Host "Started the app (pid $($process.Id)) on port $Port."
        $deadline = (Get-Date).AddSeconds(120)
        while (-not (Test-PortAnswers $Port)) {
            if ($process.HasExited) { throw "The app exited early (code $($process.ExitCode)); see $appErr." }
            if ((Get-Date) -gt $deadline) { throw "The app did not answer on port $Port within 120 s." }
            Start-Sleep -Seconds 1
        }
        Write-Host "The app answers; waiting $WarmupSeconds s for Agents to connect ..."
        Start-Sleep -Seconds $WarmupSeconds
        return $process
    }

    function Stop-SnapshotApp($process) {
        # Only the process tree this script started; /T takes the app and the mock adapters under it.
        & taskkill /PID $process.Id /T /F 2>&1 | Out-Null
        $null = $process.WaitForExit(15000)
        Start-Sleep -Seconds 2
    }

    # The seeder gives every Room and Agent a time-based id, and the sidebar order, avatar colours and Feed chips depend
    # on them, so two fresh seeds do not render alike. Seed once, let the app finish its first start (it adds the Chief
    # of Staff), keep that folder as the golden copy, and start every Capture from an exact copy of it.
    if ($Reseed -or -not (Test-Path -LiteralPath $golden)) {
        Write-Host "Seeding $SeedRoot (run date pinned to 2026-11-01) ..."
        & $seedExe --today 2026-11-01 --root $SeedRoot | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'huddle-seed failed.' }
        $first = Start-SnapshotApp
        Stop-SnapshotApp $first
        Remove-Item -LiteralPath (Join-Path $DataDir 'appearance.json') -ErrorAction SilentlyContinue
        & robocopy $SeedRoot $golden /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy to the golden copy failed (code $LASTEXITCODE)." }
        Write-Host "Golden copy saved to $golden."
    }
    else {
        Write-Host "Restoring $SeedRoot from $golden (use -Reseed to build a new golden copy) ..."
        & robocopy $golden $SeedRoot /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy from the golden copy failed (code $LASTEXITCODE)." }
    }
    $global:LASTEXITCODE = 0
    $appProcess = Start-SnapshotApp
}

$exitCode = 1
try {

    $captureArgs = @(
        $node, 'capture', '--base-url', "http://localhost:$Port", '--out', $Out, '--data-dir', $DataDir, '--manifest', $Manifest,
        '--chrome', $Chrome, '--concurrency', $Concurrency
    )
    if ($Themes) { $captureArgs += @('--themes', $Themes) }
    if ($Widths) { $captureArgs += @('--widths', $Widths) }
    if ($Only) { $captureArgs += @('--only', $Only) }
    if ($Screens) { $captureArgs += '--screens' }
    & node @captureArgs
    $exitCode = $LASTEXITCODE
}
finally {
    if ($appProcess -and -not $KeepRunning) {
        # Only the process tree this script started; /T takes the app and the mock adapters under it.
        & taskkill /PID $appProcess.Id /T /F 2>&1 | Out-Null
        Write-Host 'Stopped the app.'
    }
}
exit $exitCode
