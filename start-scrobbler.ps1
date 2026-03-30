#!/usr/bin/env pwsh
# ──────────────────────────────────────────────────────────────────────
# start-scrobbler.ps1 — Start podcast-scrobbler for integration testing
#
# Usage:  .\start-scrobbler.ps1
#         .\start-scrobbler.ps1 -Port 5099 -Token "my-token"
# ──────────────────────────────────────────────────────────────────────

param(
    [int]$Port = 5099,
    [string]$Token = "test-token"
)

$ErrorActionPreference = "Stop"

$env:SCROBBLER_TOKEN = $Token
$env:PORT = $Port

Write-Host "Starting podcast-scrobbler on port $Port (token: $Token)" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""

dotnet run --project "$PSScriptRoot\PodcastScrobbler\PodcastScrobbler.csproj"
