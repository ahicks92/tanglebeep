$TangledeepDefaultSteamPath = "C:\Program Files (x86)\Steam\steamapps\common\Tangledeep"

function Normalize-TangledeepPath {
    param([string]$Path)

    if (-not $Path) { return $null }
    $normalized = $Path.Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $null }
    return $normalized
}

function Test-TangledeepGameDir {
    param([string]$Path)

    $game = Normalize-TangledeepPath $Path
    if (-not $game) { return $false }

    return (Test-Path (Join-Path $game "Tangledeep.exe")) -and
        (Test-Path (Join-Path $game "Tangledeep_Data\Managed\Assembly-CSharp.dll"))
}

function Get-SteamLibraryPathsFromVdf {
    param([string]$Content)

    if (-not $Content) { return @() }

    $paths = @()
    [regex]::Matches($Content, '"path"\s+"([^"]+)"') | ForEach-Object {
        $paths += ($_.Groups[1].Value -replace '\\\\', '\')
    }
    return $paths
}

function Get-SteamRoots {
    $roots = @()

    $hkcuSteam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if ($hkcuSteam) { $roots += ($hkcuSteam -replace '/', '\') }

    $hklmSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    if ($hklmSteam) { $roots += $hklmSteam }

    $roots += "C:\Program Files (x86)\Steam"

    $seen = @{}
    foreach ($root in $roots) {
        $normalized = Normalize-TangledeepPath $root
        if (-not $normalized) { continue }
        $key = $normalized.ToLowerInvariant()
        if (-not $seen.ContainsKey($key)) {
            $seen[$key] = $true
            $normalized
        }
    }
}

function Get-SteamTangledeepCandidates {
    param([string[]]$SteamRoots)

    $candidates = @()
    foreach ($steam in $SteamRoots) {
        $root = Normalize-TangledeepPath $steam
        if (-not $root) { continue }

        $candidates += (Join-Path $root "steamapps\common\Tangledeep")

        $libraryFolders = Join-Path $root "steamapps\libraryfolders.vdf"
        if (Test-Path $libraryFolders) {
            $content = Get-Content $libraryFolders -Raw
            foreach ($library in Get-SteamLibraryPathsFromVdf $content) {
                $candidates += (Join-Path $library "steamapps\common\Tangledeep")
            }
        }
    }

    return $candidates
}

function Get-GogInstallPathsFromRegistry {
    $candidates = @()
    $roots = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKCU:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }

        Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
            $app = Get-ItemProperty -Path $_.PSPath -ErrorAction SilentlyContinue
            if (-not $app) { return }

            $displayName = [string]$app.DisplayName
            if (-not $displayName -or -not $displayName.ToLowerInvariant().Contains("tangledeep")) {
                return
            }

            if ($app.InstallLocation) { $candidates += $app.InstallLocation }
            if ($app.InstallDir) { $candidates += $app.InstallDir }
        }
    }

    return $candidates
}

function Get-GogTangledeepCandidates {
    param([string[]]$GogInstallPaths)

    $candidates = @()
    if ($GogInstallPaths) {
        $candidates += $GogInstallPaths
    } else {
        $candidates += Get-GogInstallPathsFromRegistry
    }

    $candidates += "C:\GOG Games\Tangledeep"
    $candidates += "C:\Program Files (x86)\GOG Galaxy\Games\Tangledeep"
    $candidates += "C:\Program Files\GOG Galaxy\Games\Tangledeep"

    return $candidates
}

function Add-TangledeepCandidate {
    param(
        [System.Collections.ArrayList]$Candidates,
        [hashtable]$Seen,
        [string]$Path
    )

    $normalized = Normalize-TangledeepPath $Path
    if (-not $normalized) { return }

    $key = $normalized.ToLowerInvariant()
    if ($Seen.ContainsKey($key)) { return }

    $Seen[$key] = $true
    [void]$Candidates.Add($normalized)
}

function Get-TangledeepGameCandidates {
    param(
        [string]$ManualGame = $env:TANGLEDEEP_GAME,
        [string[]]$SteamRoots = $(Get-SteamRoots),
        [string[]]$GogInstallPaths = $null
    )

    $candidates = New-Object System.Collections.ArrayList
    $seen = @{}

    Add-TangledeepCandidate $candidates $seen $ManualGame

    foreach ($candidate in Get-SteamTangledeepCandidates $SteamRoots) {
        Add-TangledeepCandidate $candidates $seen $candidate
    }

    foreach ($candidate in Get-GogTangledeepCandidates $GogInstallPaths) {
        Add-TangledeepCandidate $candidates $seen $candidate
    }

    return @($candidates)
}

function Resolve-TangledeepGame {
    param(
        [string]$ManualGame = $env:TANGLEDEEP_GAME,
        [string[]]$SteamRoots = $(Get-SteamRoots),
        [string[]]$GogInstallPaths = $null
    )

    $manual = Normalize-TangledeepPath $ManualGame
    if ($manual) {
        return $manual
    }

    foreach ($candidate in Get-TangledeepGameCandidates -ManualGame $null -SteamRoots $SteamRoots -GogInstallPaths $GogInstallPaths) {
        if (Test-TangledeepGameDir $candidate) {
            return $candidate
        }
    }

    return $TangledeepDefaultSteamPath
}
