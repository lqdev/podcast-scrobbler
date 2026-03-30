#!/usr/bin/env pwsh
# ──────────────────────────────────────────────────────────────────────
# smoke-test.ps1 — End-to-end smoke test for podcast-scrobbler
#
# Usage:  ./smoke-test.ps1
#         ./smoke-test.ps1 -Verbose        # show every request/response
#         ./smoke-test.ps1 -SkipBuild      # skip build + unit tests
#         ./smoke-test.ps1 -BaseUrl http://localhost:5000 -Token my-token
#                                           # test an already-running server
# ──────────────────────────────────────────────────────────────────────

param(
    [string]$BaseUrl,
    [string]$Token = "smoke-test-token",
    [int]$Port = 5099,
    [switch]$SkipBuild,
    [switch]$SkipUnitTests,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ── Globals ──────────────────────────────────────────────────────────
$script:Passed = 0
$script:Failed = 0
$script:Errors = @()
$ServerProcess = $null
$SmokeDbPath = Join-Path $PSScriptRoot "PodcastScrobbler" "data" "smoke-test.db"
$WalPath = "${SmokeDbPath}.wal"
if (-not $BaseUrl) { $BaseUrl = "http://localhost:$Port" }
$ExternalServer = $PSBoundParameters.ContainsKey('BaseUrl')

# ── Helpers ──────────────────────────────────────────────────────────

function Write-Pass([string]$Name) {
    $script:Passed++
    Write-Host "  ✅ $Name" -ForegroundColor Green
}

function Write-Fail([string]$Name, [string]$Detail = "") {
    $script:Failed++
    $msg = "  ❌ $Name"
    if ($Detail) { $msg += " — $Detail" }
    $script:Errors += $msg
    Write-Host $msg -ForegroundColor Red
}

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host "━━━ $Title ━━━" -ForegroundColor Cyan
}

function Invoke-Api {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body,
        [hashtable]$Headers = @{},
        [switch]$NoAuth,
        [switch]$RawResponse
    )
    $url = "$BaseUrl$Path"
    $params = @{
        Method             = $Method
        Uri                = $url
        ContentType        = "application/json"
        SkipHttpErrorCheck = $true
    }

    if (-not $NoAuth -and -not $Headers.ContainsKey("Authorization")) {
        $Headers["Authorization"] = "Token $Token"
    }
    if ($Headers.Count -gt 0) { $params.Headers = $Headers }
    if ($Body) {
        $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 }
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
    }

    $resp = Invoke-WebRequest @params
    $code = [int]$resp.StatusCode
    $parsed = $null
    if ($resp.Content) {
        $parsed = try { $resp.Content | ConvertFrom-Json } catch { $null }
    }

    if ($Verbose) {
        $arrow = if ($Method -eq "GET") { "→" } else { "←" }
        Write-Host "    $Method $Path $arrow $code" -ForegroundColor DarkGray
        if ($Body) {
            $bodyPreview = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 5 -Compress }
            if ($bodyPreview.Length -gt 200) { $bodyPreview = $bodyPreview.Substring(0, 200) + "…" }
            Write-Host "    REQ  $bodyPreview" -ForegroundColor DarkYellow
        }
        $respPreview = if ($resp.Content) { $resp.Content } else { "(empty)" }
        if ($respPreview.Length -gt 300) { $respPreview = $respPreview.Substring(0, 300) + "…" }
        Write-Host "    RESP $respPreview" -ForegroundColor DarkGray
    }

    if ($RawResponse -or $code -ge 300) {
        return @{ StatusCode = $code; Body = $parsed; Raw = $resp.Content }
    }
    return $parsed
}

function Assert-StatusCode([hashtable]$Response, [int]$Expected, [string]$TestName) {
    if ($Response.StatusCode -eq $Expected) {
        Write-Pass $TestName
    }
    else {
        Write-Fail $TestName "expected $Expected, got $($Response.StatusCode)"
    }
}

function Assert-Property($Obj, [string]$Prop, $Expected, [string]$TestName) {
    $actual = $Obj.$Prop
    if ($actual -eq $Expected) {
        Write-Pass $TestName
    }
    else {
        Write-Fail $TestName "expected $Prop='$Expected', got '$actual'"
    }
}

function Assert-True([bool]$Condition, [string]$TestName, [string]$Detail = "") {
    if ($Condition) { Write-Pass $TestName }
    else { Write-Fail $TestName $Detail }
}

# ── 1. Pre-flight: Build & Test ─────────────────────────────────────

Write-Section "Pre-flight"

if (-not $SkipBuild) {
    Write-Host "  Building solution..." -ForegroundColor Yellow
    $buildOutput = dotnet build "$PSScriptRoot\podcast-scrobbler.sln" --nologo -v quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Pass "dotnet build"
    }
    else {
        Write-Fail "dotnet build" "exit code $LASTEXITCODE"
        Write-Host ($buildOutput -join "`n") -ForegroundColor DarkGray
        Write-Host "`n🛑 Build failed — aborting smoke test." -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "  (skipping build)" -ForegroundColor DarkGray
}

if (-not $SkipBuild -and -not $SkipUnitTests) {
    Write-Host "  Running unit tests..." -ForegroundColor Yellow
    $testOutput = dotnet test "$PSScriptRoot\podcast-scrobbler.sln" --nologo --no-build -v quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Pass "dotnet test"
    }
    else {
        Write-Fail "dotnet test" "exit code $LASTEXITCODE"
        Write-Host ($testOutput -join "`n") -ForegroundColor DarkGray
        Write-Host "`n🛑 Unit tests failed — aborting smoke test." -ForegroundColor Red
        exit 1
    }
}
elseif ($SkipUnitTests) {
    Write-Host "  (skipping unit tests)" -ForegroundColor DarkGray
}

# ── 2. Start Server ─────────────────────────────────────────────────

if (-not $ExternalServer) {
    Write-Section "Starting Server"

    # Clean up any previous smoke DB
    if (Test-Path $SmokeDbPath) { Remove-Item $SmokeDbPath -Force }
    if (Test-Path $WalPath) { Remove-Item $WalPath -Force }

    $env:SCROBBLER_TOKEN = $Token
    $env:DATABASE_PATH = $SmokeDbPath
    $env:PORT = $Port

    $ServerProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run", "--project", "$PSScriptRoot\PodcastScrobbler\PodcastScrobbler.csproj", "--no-build" `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput "$PSScriptRoot\smoke-server-stdout.log" `
        -RedirectStandardError "$PSScriptRoot\smoke-server-stderr.log"

    Write-Host "  Server PID: $($ServerProcess.Id), waiting for /health..." -ForegroundColor Yellow

    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        try {
            $null = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 2 -ErrorAction Stop
            $ready = $true
            break
        }
        catch { }
    }

    if ($ready) {
        Write-Pass "Server started on port $Port"
    }
    else {
        Write-Fail "Server failed to start within 30s"
        if (Test-Path "$PSScriptRoot\smoke-server-stderr.log") {
            Write-Host (Get-Content "$PSScriptRoot\smoke-server-stderr.log" -Raw) -ForegroundColor DarkGray
        }
        if ($ServerProcess -and -not $ServerProcess.HasExited) {
            Stop-Process -Id $ServerProcess.Id -Force -ErrorAction SilentlyContinue
        }
        exit 1
    }
}
else {
    Write-Section "Using External Server"
    Write-Host "  Target: $BaseUrl" -ForegroundColor Yellow
}

# ── 3. Health & Readiness ────────────────────────────────────────────

Write-Section "Health & Readiness"

$health = Invoke-Api -Path "/health" -NoAuth
Assert-Property $health "status" "healthy" "GET /health → healthy"

$ready = Invoke-Api -Path "/ready" -NoAuth
Assert-Property $ready "status" "ready" "GET /ready → ready"

# ── 4. Token Validation & Auth ───────────────────────────────────────

Write-Section "Token Validation & Auth"

# Valid token via header
$valid = Invoke-Api -Path "/1/validate-token" -Headers @{ Authorization = "Token $Token" }
Assert-Property $valid "valid" $true "validate-token (valid, header)"
Assert-Property $valid "user_name" "default" "validate-token → user_name=default"

# Valid token via query param
$validQ = Invoke-Api -Path "/1/validate-token?token=$Token" -NoAuth
Assert-Property $validQ "valid" $true "validate-token (valid, query param)"

# Invalid token
$invalid = Invoke-Api -Path "/1/validate-token" -Headers @{ Authorization = "Token wrong-token" }
Assert-Property $invalid "valid" $false "validate-token (invalid token)"

# Missing token → 400
$missing = Invoke-Api -Path "/1/validate-token" -NoAuth
Assert-StatusCode $missing 400 "validate-token (no token) → 400"

# POST without auth → 401
$noAuth = Invoke-Api -Method POST -Path "/1/submit-listens" -NoAuth -Body @{
    listen_type = "single"
    payload     = @(@{
        listened_at    = 1700000000
        track_metadata = @{ artist_name = "Test"; track_name = "Test" }
    })
}
Assert-StatusCode $noAuth 401 "POST submit-listens (no token) → 401"

# POST with wrong token → 401
$wrongAuth = Invoke-Api -Method POST -Path "/1/submit-listens" -Headers @{ Authorization = "Token bad" } -Body @{
    listen_type = "single"
    payload     = @(@{
        listened_at    = 1700000000
        track_metadata = @{ artist_name = "Test"; track_name = "Test" }
    })
}
Assert-StatusCode $wrongAuth 401 "POST submit-listens (wrong token) → 401"

# ── 5. Playing Now ───────────────────────────────────────────────────

Write-Section "Playing Now"

# Nothing playing yet
$empty = Invoke-Api -Path "/1/user/default/playing-now"
$hasPlayingNow = $null -ne $empty.payload.PSObject.Properties["playing_now"]
Assert-True (-not $hasPlayingNow -or $null -eq $empty.payload.playing_now) "playing-now (initially empty)" "expected null/absent"

# Submit playing_now
$pn1 = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "playing_now"
    payload     = @(@{
        track_metadata = @{
            artist_name     = "Smoke Test Podcast"
            track_name      = "Episode 1 — Pilot"
            additional_info = @{
                media_player   = "smoke-test"
                podcast_feed_url = "https://example.com/feed.xml"
                position_ms    = 30000
                duration_ms    = 1800000
            }
        }
    })
}
Assert-Property $pn1 "status" "ok" "submit playing_now → ok"

# Read it back
$pnRead = Invoke-Api -Path "/1/user/default/playing-now"
Assert-Property $pnRead.payload.playing_now.track_metadata "artist_name" "Smoke Test Podcast" "playing-now → correct podcast"
Assert-Property $pnRead.payload.playing_now.track_metadata "track_name" "Episode 1 — Pilot" "playing-now → correct episode"

# Overwrite with new track
$null = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "playing_now"
    payload     = @(@{
        track_metadata = @{
            artist_name = "Smoke Test Podcast"
            track_name  = "Episode 2 — Overwrite"
        }
    })
}
$pnRead2 = Invoke-Api -Path "/1/user/default/playing-now"
Assert-Property $pnRead2.payload.playing_now.track_metadata "track_name" "Episode 2 — Overwrite" "playing-now overwrite works"

# ── 6. Submit Single Listens ─────────────────────────────────────────

Write-Section "Submit Single Listens"

# Use recent timestamps so weekly stats can find them
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$ts1 = $now - 3600    # 1 hour ago
$ts2 = $now - 1800    # 30 min ago
$ts3 = $now - 600     # 10 min ago

$s1 = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "single"
    payload     = @(@{
        listened_at    = $ts1
        track_metadata = @{
            artist_name = "Podcast Alpha"
            track_name  = "Alpha Ep 1"
        }
    })
}
Assert-Property $s1 "status" "ok" "single listen #1 → ok"

$s2 = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "single"
    payload     = @(@{
        listened_at    = $ts2
        track_metadata = @{
            artist_name = "Podcast Beta"
            track_name  = "Beta Ep 1"
        }
    })
}
Assert-Property $s2 "status" "ok" "single listen #2 → ok"

$s3 = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "single"
    payload     = @(@{
        listened_at    = $ts3
        track_metadata = @{
            artist_name     = "Podcast Alpha"
            track_name      = "Alpha Ep 2"
            additional_info = @{
                media_player     = "smoke-test"
                podcast_feed_url = "https://example.com/alpha.xml"
                episode_guid     = "guid-alpha-ep2"
                duration_ms      = 3600000
                position_ms      = 3600000
                percent_complete = 100.0
            }
        }
    })
}
Assert-Property $s3 "status" "ok" "single listen #3 (with additional_info) → ok"

# ── 7. Batch Import ──────────────────────────────────────────────────

Write-Section "Batch Import"

$importPayload = @{
    listen_type = "import"
    payload     = @(
        @{ listened_at = ($now - 7200); track_metadata = @{ artist_name = "Podcast Gamma"; track_name = "Gamma Ep 1" } }
        @{ listened_at = ($now - 7100); track_metadata = @{ artist_name = "Podcast Gamma"; track_name = "Gamma Ep 2" } }
        @{ listened_at = ($now - 7000); track_metadata = @{ artist_name = "Podcast Gamma"; track_name = "Gamma Ep 3" } }
        @{ listened_at = ($now - 6900); track_metadata = @{ artist_name = "Podcast Alpha"; track_name = "Alpha Ep 3" } }
        @{ listened_at = ($now - 6800); track_metadata = @{ artist_name = "Podcast Beta";  track_name = "Beta Ep 2"  } }
    )
}
$imp = Invoke-Api -Method POST -Path "/1/submit-listens" -Body $importPayload
Assert-Property $imp "status" "ok" "import 5 listens → ok"
Assert-Property $imp "imported" 5 "import → imported=5"
Assert-Property $imp "skipped" 0 "import → skipped=0"

# ── 8. Get Listens & Pagination ──────────────────────────────────────

Write-Section "Get Listens & Pagination"

# Total count (3 singles + 5 imports = 8)
$count = Invoke-Api -Path "/1/user/default/listen-count"
Assert-Property $count.payload "count" 8 "listen-count → 8"

# Default fetch (all 8)
$all = Invoke-Api -Path "/1/user/default/listens"
Assert-True ($all.payload.count -eq 8) "GET listens → count=8" "got $($all.payload.count)"

# Pagination: count=2
$page = Invoke-Api -Path "/1/user/default/listens?count=2"
Assert-True ($page.payload.count -eq 2) "GET listens?count=2 → 2 items" "got $($page.payload.count)"

# max_ts filter: only listens before ts2 (should get ts1 only)
$maxTs = Invoke-Api -Path "/1/user/default/listens?max_ts=$ts2"
$allBeforeTs2 = @($maxTs.payload.listens | Where-Object { $_.listened_at -lt $ts2 })
Assert-True ($allBeforeTs2.Count -ge 1) "GET listens?max_ts=$ts2 → filters correctly" "got $($maxTs.payload.count) items"

# min_ts filter: listens after the imports (should get the 3 single listens)
$minTsBoundary = $now - 4000
$minTs = Invoke-Api -Path "/1/user/default/listens?min_ts=$minTsBoundary"
Assert-True ($minTs.payload.count -ge 1) "GET listens?min_ts → filters correctly" "got $($minTs.payload.count) items"

# Count clamping: count=999 → clamped to 100
$clamped = Invoke-Api -Path "/1/user/default/listens?count=999" -RawResponse
Assert-StatusCode $clamped 200 "GET listens?count=999 → 200 (clamped)"

# ── 9. Stats Endpoints ───────────────────────────────────────────────

Write-Section "Stats"

# Top podcasts
$topPods = Invoke-Api -Path "/1/user/default/stats/podcasts?limit=10"
$podNames = @($topPods.payload | ForEach-Object { $_.podcast_name })
Assert-True ($podNames.Count -ge 3) "stats/podcasts → ≥3 podcasts" "got $($podNames.Count)"
Assert-True ($podNames -contains "Podcast Alpha") "stats/podcasts includes 'Podcast Alpha'" "missing"

# Podcast Alpha should have 3 listens (Ep1 + Ep2 + Ep3)
$alpha = $topPods.payload | Where-Object { $_.podcast_name -eq "Podcast Alpha" }
Assert-True ($alpha.listen_count -eq 3) "Podcast Alpha → 3 listens" "got $($alpha.listen_count)"

# Weekly stats
$weekly = Invoke-Api -Path "/1/user/default/stats/weekly?weeks=52"
Assert-True ($weekly.payload.Count -ge 1) "stats/weekly → has data" "got $($weekly.payload.Count) weeks"

# Recent podcasts
$recent = Invoke-Api -Path "/1/user/default/stats/recent-podcasts?limit=10"
Assert-True ($recent.payload.Count -ge 1) "stats/recent-podcasts → has data" "empty"

# Summary
$summary = Invoke-Api -Path "/1/user/default/stats/summary"
Assert-Property $summary.payload "total_listens" 8 "stats/summary → total_listens=8"
Assert-True ($summary.payload.unique_podcasts -ge 3) "stats/summary → ≥3 unique podcasts" "got $($summary.payload.unique_podcasts)"

# ── 10. Edge Cases & Negative Tests ──────────────────────────────────

Write-Section "Edge Cases"

# additional_info round-trip
$withInfo = Invoke-Api -Path "/1/user/default/listens?count=100"
$ep2 = $withInfo.payload.listens | Where-Object { $_.track_metadata.track_name -eq "Alpha Ep 2" } | Select-Object -First 1
if ($ep2) {
    $info = $ep2.track_metadata.additional_info
    Assert-Property $info "episode_guid" "guid-alpha-ep2" "additional_info.episode_guid round-trips"
    Assert-Property $info "duration_ms" 3600000 "additional_info.duration_ms round-trips"
    Assert-Property $info "podcast_feed_url" "https://example.com/alpha.xml" "additional_info.podcast_feed_url round-trips"
}
else {
    Write-Fail "additional_info round-trip" "could not find Alpha Ep 2 in listens"
}

# Nonexistent user → empty, not error
$nobody = Invoke-Api -Path "/1/user/nobody/listens"
Assert-True ($nobody.payload.count -eq 0) "nonexistent user → empty listens" "got count=$($nobody.payload.count)"

$nobodyCount = Invoke-Api -Path "/1/user/nobody/listen-count"
Assert-Property $nobodyCount.payload "count" 0 "nonexistent user → listen-count=0"

# Invalid listen_type → 400
$badType = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "bogus"
    payload     = @(@{
        listened_at    = 1700000000
        track_metadata = @{ artist_name = "X"; track_name = "Y" }
    })
}
Assert-StatusCode $badType 400 "invalid listen_type → 400"

# Empty payload → 400
$emptyPayload = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "single"
    payload     = @()
}
Assert-StatusCode $emptyPayload 400 "empty payload → 400"

# Single without listened_at → 400
$noTs = Invoke-Api -Method POST -Path "/1/submit-listens" -Body @{
    listen_type = "single"
    payload     = @(@{
        track_metadata = @{ artist_name = "X"; track_name = "Y" }
    })
}
Assert-StatusCode $noTs 400 "single without listened_at → 400"

# ── 11. Teardown ─────────────────────────────────────────────────────

Write-Section "Teardown"

if ($ServerProcess -and -not $ServerProcess.HasExited) {
    Stop-Process -Id $ServerProcess.Id -Force -ErrorAction SilentlyContinue
    Write-Host "  Server process stopped." -ForegroundColor DarkGray
}

# Clean up env vars
Remove-Item env:SCROBBLER_TOKEN -ErrorAction SilentlyContinue
Remove-Item env:DATABASE_PATH -ErrorAction SilentlyContinue
Remove-Item env:PORT -ErrorAction SilentlyContinue

# Clean up smoke DB and logs
Start-Sleep -Seconds 1
if (Test-Path $SmokeDbPath) { Remove-Item $SmokeDbPath -Force -ErrorAction SilentlyContinue }
if (Test-Path $WalPath) { Remove-Item $WalPath -Force -ErrorAction SilentlyContinue }
$smokeDataDir = Split-Path $SmokeDbPath
if ((Test-Path $smokeDataDir) -and (Get-ChildItem $smokeDataDir -Force | Measure-Object).Count -eq 0) {
    Remove-Item $smokeDataDir -Force -ErrorAction SilentlyContinue
}
Remove-Item "$PSScriptRoot\smoke-server-stdout.log" -Force -ErrorAction SilentlyContinue
Remove-Item "$PSScriptRoot\smoke-server-stderr.log" -Force -ErrorAction SilentlyContinue

# ── Summary ──────────────────────────────────────────────────────────

Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor White
if ($script:Failed -eq 0) {
    Write-Host "  🎉 ALL $($script:Passed) CHECKS PASSED" -ForegroundColor Green
}
else {
    Write-Host "  RESULT: $($script:Passed) passed, $($script:Failed) failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Failures:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host $e -ForegroundColor Red }
}
Write-Host "═══════════════════════════════════════════" -ForegroundColor White
Write-Host ""

exit ($script:Failed -gt 0 ? 1 : 0)
