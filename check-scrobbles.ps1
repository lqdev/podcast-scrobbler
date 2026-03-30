#!/usr/bin/env pwsh
# ──────────────────────────────────────────────────────────────────────
# check-scrobbles.ps1 — Check scrobble state from podcast-tui's perspective
#
# Usage:  ./check-scrobbles.ps1                    # default: localhost:5099
#         ./check-scrobbles.ps1 -Watch             # poll every 5s
#         ./check-scrobbles.ps1 -Watch -Interval 3 # poll every 3s
#         ./check-scrobbles.ps1 -BaseUrl http://localhost:5000 -Token my-token
# ──────────────────────────────────────────────────────────────────────

param(
    [string]$BaseUrl = "http://localhost:5099",
    [string]$Token = "test-token",
    [string]$Username = "default",
    [switch]$Watch,
    [int]$Interval = 5
)

$ErrorActionPreference = "Stop"

# ── Helpers ──────────────────────────────────────────────────────────

function Invoke-Api {
    param([string]$Path)
    $url = "$BaseUrl$Path"
    try {
        $params = @{
            Method             = "GET"
            Uri                = $url
            ContentType        = "application/json"
            Headers            = @{ Authorization = "Token $Token" }
            SkipHttpErrorCheck = $true
        }
        $resp = Invoke-WebRequest @params
        if ($resp.StatusCode -ge 300) {
            Write-Host "    ⚠ $Path → HTTP $($resp.StatusCode)" -ForegroundColor Yellow
            return $null
        }
        return ($resp.Content | ConvertFrom-Json)
    }
    catch {
        Write-Host "    ⚠ $Path → $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Write-Header([string]$Title) {
    $ts = Get-Date -Format "HH:mm:ss"
    Write-Host ""
    Write-Host "[$ts] ━━━ $Title ━━━" -ForegroundColor Cyan
}

function Write-Field([string]$Label, [string]$Value, [string]$Color = "White") {
    Write-Host ("  {0,-20} " -f $Label) -NoNewline -ForegroundColor DarkGray
    Write-Host $Value -ForegroundColor $Color
}

function Write-None([string]$Message) {
    Write-Host "  $Message" -ForegroundColor DarkGray
}

function Format-Duration([int]$Ms) {
    if ($Ms -le 0) { return "—" }
    $s = [math]::Floor($Ms / 1000)
    $m = [math]::Floor($s / 60)
    $s = $s % 60
    if ($m -gt 0) { return "${m}m ${s}s" }
    return "${s}s"
}

function Format-Timestamp([long]$Unix) {
    if ($Unix -le 0) { return "—" }
    [DateTimeOffset]::FromUnixTimeSeconds($Unix).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
}

# ── Display Functions ────────────────────────────────────────────────

function Show-Token {
    Write-Header "Token Validation"
    $result = Invoke-Api "/1/validate-token"
    if ($null -eq $result) { return }

    if ($result.valid) {
        Write-Field "Status" "✅ Valid" "Green"
        Write-Field "User" $result.user_name
    }
    else {
        Write-Field "Status" "❌ Invalid" "Red"
    }
}

function Show-PlayingNow {
    Write-Header "Now Playing"
    $result = Invoke-Api "/1/user/$Username/playing-now"
    if ($null -eq $result) { return }

    $pn = $result.payload.playing_now
    if ($null -eq $pn) {
        Write-None "(nothing playing)"
        return
    }

    $meta = $pn.track_metadata
    Write-Field "Podcast" $meta.artist_name "Yellow"
    Write-Field "Episode" $meta.track_name "White"

    $info = $meta.additional_info
    if ($info) {
        if ($info.position_ms -and $info.duration_ms) {
            $pos = Format-Duration $info.position_ms
            $dur = Format-Duration $info.duration_ms
            $pct = [math]::Round(($info.position_ms / $info.duration_ms) * 100, 1)
            Write-Field "Position" "$pos / $dur ($pct%)" "Green"
        }
        elseif ($info.position_ms) {
            Write-Field "Position" (Format-Duration $info.position_ms)
        }
        if ($info.media_player) {
            Write-Field "Player" $info.media_player
        }
        if ($info.podcast_feed_url) {
            Write-Field "Feed URL" $info.podcast_feed_url "DarkGray"
        }
    }
}

function Show-RecentListens {
    Write-Header "Recent Listens"
    $result = Invoke-Api "/1/user/$Username/listens?count=10"
    if ($null -eq $result) { return }

    $listens = $result.payload.listens
    $total = $result.payload.count

    if ($total -eq 0) {
        Write-None "(no listens recorded yet)"
        Write-Host ""
        Write-Host "  💡 A listen is submitted after meeting both thresholds:" -ForegroundColor DarkGray
        Write-Host "     min_listen_percent AND min_listen_seconds" -ForegroundColor DarkGray
        return
    }

    Write-Host "  Showing $($listens.Count) of $total total" -ForegroundColor DarkGray
    Write-Host ""

    foreach ($listen in $listens) {
        $meta = $listen.track_metadata
        $time = Format-Timestamp $listen.listened_at

        Write-Host "  ♪ " -NoNewline -ForegroundColor Magenta
        Write-Host $meta.track_name -NoNewline -ForegroundColor White
        Write-Host " — " -NoNewline -ForegroundColor DarkGray
        Write-Host $meta.artist_name -ForegroundColor Yellow

        $detail = "    $time"
        $info = $meta.additional_info
        if ($info -and $info.duration_ms) {
            $detail += "  ⏱ $(Format-Duration $info.duration_ms)"
        }
        if ($info -and $info.percent_complete) {
            $detail += "  ($([math]::Round($info.percent_complete, 1))%)"
        }
        Write-Host $detail -ForegroundColor DarkGray
    }
}

function Show-Stats {
    Write-Header "Stats Summary"
    $result = Invoke-Api "/1/user/$Username/stats/summary"
    if ($null -eq $result) { return }

    $s = $result.payload
    Write-Field "Total Listens" $s.total_listens "Green"
    Write-Field "Unique Podcasts" $s.unique_podcasts
    Write-Field "Unique Episodes" $s.unique_episodes

    # Top podcasts
    $top = Invoke-Api "/1/user/$Username/stats/podcasts?limit=5"
    if ($top -and $top.payload.Count -gt 0) {
        Write-Host ""
        Write-Host "  Top Podcasts:" -ForegroundColor DarkGray
        foreach ($pod in $top.payload) {
            Write-Host "    $($pod.listen_count.ToString().PadLeft(3))x  " -NoNewline -ForegroundColor Green
            Write-Host $pod.podcast_name -ForegroundColor White
        }
    }
}

function Show-ListenCount {
    $result = Invoke-Api "/1/user/$Username/listen-count"
    if ($null -eq $result) { return }
    return $result.payload.count
}

# ── Main ─────────────────────────────────────────────────────────────

function Show-All {
    $border = "═" * 50
    Write-Host $border -ForegroundColor White
    Write-Host "  🎧 Podcast Scrobbler — $BaseUrl" -ForegroundColor White
    Write-Host $border -ForegroundColor White

    Show-Token
    Show-PlayingNow
    Show-RecentListens
    Show-Stats

    Write-Host ""
    Write-Host $border -ForegroundColor White
}

if ($Watch) {
    Write-Host "Watching scrobbler at $BaseUrl (Ctrl+C to stop)..." -ForegroundColor DarkGray
    $lastCount = -1
    while ($true) {
        Clear-Host
        Show-All

        $count = Show-ListenCount
        if ($null -ne $count -and $count -ne $lastCount -and $lastCount -ge 0) {
            Write-Host "  🆕 New listen detected! ($lastCount → $count)" -ForegroundColor Green
        }
        $lastCount = $count

        Write-Host "  Refreshing in ${Interval}s... (Ctrl+C to stop)" -ForegroundColor DarkGray
        Start-Sleep -Seconds $Interval
    }
}
else {
    Show-All
}
