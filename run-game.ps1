# run-game.ps1 - Launch Tangledeep for iteration and BLOCK until it exits.
#
# Run this as a background task: the blocking wait means the task completes the
# instant the game crashes or quits, which is the wake-up signal. There is no
# separate restart verb - relaunching kills any leftover instance first, so
# "restart" is just "cancel the background task and run this again".

param(
    [switch]$Speech,
    [int]$SaveSlot = -1,
    [switch]$NoBuild,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\run-game.ps1 [-Speech] [-SaveSlot N] [-NoBuild] [-Help]"
    Write-Host "  Builds + deploys the plugin, then launches Tangledeep and blocks until it exits."
    Write-Host "  Run as a background task. The build runs AFTER the old instance is killed (so the"
    Write-Host "  plugin DLL is unlocked) and a build failure ABORTS the launch - you never run a"
    Write-Host "  stale DLL by accident."
    Write-Host "  -NoBuild: skip the build/deploy and launch whatever is already deployed (re-test"
    Write-Host "  the exact same binary, or restart from a clean state without recompiling)."
    Write-Host "  Prism/NVDA speech is OFF by default (headless/overnight-safe); spoken text is"
    Write-Host "  still captured for the dev /speech endpoint. Pass -Speech to voice via NVDA."
    Write-Host "  -SaveSlot N: once the dev server answers, load save slot N (e.g. 0) so a single"
    Write-Host "  command goes from cold launch to in-game. Uses the dev /loadsave endpoint."
    exit 0
}

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\scripts\TangledeepGameLocator.ps1"

# --- Locate the game install (same resolution as build.ps1 / setup-bepinex.ps1) ---
$Game = Resolve-TangledeepGame
$Exe = "$Game\Tangledeep.exe"
if (-not (Test-TangledeepGameDir $Game)) {
    Write-Host "ERROR: Tangledeep not found at: $Game" -ForegroundColor Red
    Write-Host "Set the TANGLEDEEP_GAME environment variable to the game folder." -ForegroundColor Red
    exit 1
}

# --- Single-instance lock ---------------------------------------------------
# Two launchers must never run at once: the loser's game can't bind the dev
# server port (8770) and dies with exit 1, while the winner externally kills the
# incumbent game so its launcher reports a spurious failure. The lock makes a
# concurrent launch REFUSE instead of stomping. To restart, properly cancel the
# running run-game background task first (its finally releases this lock), then
# relaunch. See CLAUDE.md "Restarting the game".
$LockFile = Join-Path $env:TEMP "tanglebeep-run-game.lock"
if (Test-Path $LockFile) {
    $heldPid = (Get-Content $LockFile -ErrorAction SilentlyContinue | Select-Object -First 1)
    $holder = $null
    if ($heldPid) { $holder = Get-Process -Id ([int]$heldPid) -ErrorAction SilentlyContinue }
    if ($holder) {
        Write-Host "ERROR: another run-game launcher is active (PID $heldPid)." -ForegroundColor Red
        Write-Host "Cancel that background task first, then relaunch. Do NOT start a second launcher." -ForegroundColor Red
        exit 1
    }
    # Stale lock (holder died, e.g. launcher was hard-killed): we own cleanup.
    Write-Host "Removing stale lock from dead launcher (PID $heldPid)." -ForegroundColor Yellow
    Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
}
Set-Content -Path $LockFile -Value $PID -Encoding ascii

# --- Self-cleaning restart: kill any leftover instance (e.g. orphaned by a
#     hard-killed launcher) and WAIT for it to fully release the dev server port
#     before launching. Stop-Process returns before teardown completes, so a
#     naive kill-then-start races the socket on 8770 and the new game exits 1. ---
Get-Process -Name Tangledeep -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping existing Tangledeep (PID $($_.Id))..." -ForegroundColor Yellow
    $_ | Stop-Process -Force
}
$deadline = (Get-Date).AddSeconds(15)
while ($true) {
    $alive = Get-Process -Name Tangledeep -ErrorAction SilentlyContinue
    $portFree = $false
    try { $null = Get-NetTCPConnection -LocalPort 8770 -ErrorAction Stop } catch { $portFree = $true }
    if (-not $alive -and $portFree) { break }
    if ((Get-Date) -gt $deadline) {
        Write-Host "WARNING: timed out waiting for old game/port 8770 to free; launching anyway." -ForegroundColor Yellow
        break
    }
    Start-Sleep -Milliseconds 250
}

# --- Build + deploy the plugin BEFORE launching ---------------------------
# Folded in so "restart" implies "rebuild" (cargo-run muscle memory), killing the
# recurring footgun of testing a stale deployed DLL. Order is load-bearing: the
# kill-existing step above has already taken the old game down and waited for it to
# release the plugin DLL, so the deploy's Copy-Item won't hit a locked file. A
# build/deploy failure ABORTS the launch (release the lock, non-zero exit) rather
# than falling through to run whatever stale binary is deployed - that silent-stale
# case is the whole reason this exists. -NoBuild skips it to relaunch the exact
# binary already deployed.
if (-not $NoBuild) {
    # build.ps1 signals failure two ways: an explicit `exit 1` (build error, game
    # not found) leaves $LASTEXITCODE non-zero; a deploy Copy-Item against a still-
    # locked DLL throws a terminating error (ErrorActionPreference=Stop). Catch both,
    # release the lock, and abort - never fall through to launch a stale binary.
    $buildOk = $false
    try {
        & "$PSScriptRoot\build.ps1"
        $buildOk = ($LASTEXITCODE -eq 0)
    } catch {
        Write-Host "Build/deploy error: $_" -ForegroundColor Red
    }
    if (-not $buildOk) {
        Write-Host "Build/deploy FAILED; NOT launching - the deployed DLL would be stale." -ForegroundColor Red
        Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
        exit 1
    }
} else {
    Write-Host "-NoBuild: launching the already-deployed plugin without rebuilding." -ForegroundColor Yellow
}

# The in-process dev server (eval + speech tap) is on by default in the mod now (loopback
# only); no env var is needed to enable it. Set TANGLEBEEP_NO_DEV=1 to disable it instead.

# Default: do NOT load Prism/NVDA, so headless/overnight runs (no screen reader) are robust
# - a flaky or absent NVDA can't hang or corrupt anything. Spoken text is still captured at
# the tap for the /speech endpoint. Pass -Speech to voice through NVDA instead.
if ($Speech) {
    Remove-Item Env:\TANGLEBEEP_NO_SPEECH -ErrorAction SilentlyContinue
    $speechNote = "speech: NVDA"
} else {
    $env:TANGLEBEEP_NO_SPEECH = "1"
    $speechNote = "speech: off (captured for /speech)"
}

# --- DLC: bridge DLC detection on a direct (non-Steam) launch ----------------
# We Start-Process the exe directly, so Steamworks never initializes
# (SteamManager.Initialized = false). The game's DLCManager then falls back to an
# on-disk marker check rooted at Application.dataPath ("<Game>\Tangledeep_Data"),
# but each DLC depot installs its marker one level up at the GAME ROOT
# (e.g. "<Game>\los\loschk"). That path mismatch makes owned DLC read as absent.
# Mirror each real depot marker into the fallback location so the game's own check
# finds it. Steam only downloads a DLC depot you hold a license for, so a marker's
# presence at the game root means it's owned - we mirror exactly what's on disk.
$DlcMarkers = @(
    @{ Name = "Legend of Shara"; Rel = "los\loschk" },   # EXPANSION1 / appid 953080
    @{ Name = "Dawn of Dragons"; Rel = "dod\dodchk" }    # EXPANSION2 / appid 1156710
)
foreach ($d in $DlcMarkers) {
    $root = Join-Path $Game $d.Rel
    $fallback = Join-Path $Game (Join-Path "Tangledeep_Data" $d.Rel)
    if (Test-Path $root) {
        if (-not (Test-Path $fallback)) {
            New-Item -ItemType Directory -Force -Path (Split-Path $fallback) | Out-Null
            Copy-Item $root $fallback -Force
            Write-Host "DLC: mirrored $($d.Name) marker into Tangledeep_Data for direct-launch detection." -ForegroundColor Cyan
        }
    } else {
        Write-Host "DLC: $($d.Name) depot marker not found; launching without it." -ForegroundColor Yellow
    }
}

$proc = Start-Process -FilePath $Exe -WorkingDirectory $Game -PassThru
Write-Host "Launched Tangledeep (PID $($proc.Id)), dev server on http://127.0.0.1:8770, $speechNote. Blocking until it exits..." -ForegroundColor Cyan

# Optional: drive straight into a save slot once the dev server answers, so a single command
# goes from cold launch to in-game on slot N. The game is already running, so we poll/load here
# (before the blocking WaitForExit). /loadsave needs the dev server (on by default in the mod)
# and blocks until the gameplay scene is interactive; it also corrects the focus flag. We retry
# the POST because the server answers /health at the title screen a moment before the title's
# UIManagerScript is ready to start a load.
if ($SaveSlot -ge 0) {
    Write-Host "Waiting for dev server, then loading save slot $SaveSlot..." -ForegroundColor Cyan
    $health = curl.exe -s --retry 90 --retry-connrefused --retry-delay 1 http://127.0.0.1:8770/health
    if ($health -match "ok") {
        $resp = ""
        $loaded = $false
        for ($i = 0; $i -lt 30; $i++) {
            $resp = curl.exe -s -X POST http://127.0.0.1:8770/loadsave --data-binary "$SaveSlot"
            if ($resp -like "loaded*") { Write-Host $resp -ForegroundColor Green; $loaded = $true; break }
            Start-Sleep -Seconds 1
        }
        if (-not $loaded) {
            Write-Host "WARNING: auto-load of slot $SaveSlot did not complete; last response: $resp" -ForegroundColor Yellow
        }
    } else {
        Write-Host "WARNING: dev server never became healthy; skipping auto-load of slot $SaveSlot." -ForegroundColor Yellow
    }
}

try {
    $proc.WaitForExit()
} finally {
    # Graceful stop of this launcher: take the game down with us, then release the
    # lock. (A hard kill of the launcher skips this finally entirely; the next
    # launch then sees the holder PID is dead, clears the stale lock, and kills the
    # orphaned game in its kill-existing step.)
    if (-not $proc.HasExited) {
        Write-Host "Launcher stopping; killing game (PID $($proc.Id))..." -ForegroundColor Yellow
        $proc.Kill()
    }
    Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
}

Write-Host "Tangledeep exited with code $($proc.ExitCode)." -ForegroundColor Cyan
exit $proc.ExitCode
