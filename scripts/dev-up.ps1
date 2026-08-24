<#
  dev-up.ps1 - Starts Postgres + all services (Auth, Community, Gateway, Telemedicine, API) for development.
  Usage: .\scripts\dev-up.ps1 [-Watch]
  -Watch: uses `dotnet watch run` for hot reload (skips pre-build, longer wait).
  Stop: .\scripts\dev-down.ps1
#>
param(
  [switch]$Watch
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logs = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

$services = @(
  @{ Name = 'auth';         Project = 'src/Services/CoppAddresd.Auth';         Url = 'http://localhost:5123'; Port = 5123; Color = 'Cyan' },
  @{ Name = 'community';    Project = 'src/Services/CoppAddresd.Community';    Url = 'http://localhost:5200'; Port = 5200; Color = 'Green' },
  @{ Name = 'gateway';      Project = 'src/Services/CoppAddresd.Gateway';      Url = 'http://localhost:5080'; Port = 5080; Color = 'Magenta' },
  @{ Name = 'telemedicine'; Project = 'src/Services/CoppAddresd.Telemedicine'; Url = 'http://localhost:5130'; Port = 5130; Color = 'Yellow' },
  @{ Name = 'api';          Project = 'src/CoppAddresd.Api';                   Url = 'http://localhost:5122'; Port = 5122; Color = 'Blue' }
)

function Test-Port([int]$Port) {
  return (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) -ne $null
}

function Wait-Port([int]$Port, [int]$TimeoutSec) {
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  while ((Get-Date) -lt $deadline) {
    if (Test-Port $Port) { return $true }
    Start-Sleep -Seconds 2
  }
  return Test-Port $Port
}

function Draw-Banner {
  param([string]$Title, [ConsoleColor]$Color = 'Cyan')
  $width = 60
  $pad = [math]::Floor(($width - $Title.Length) / 2)
  Write-Host ''
  Write-Host ('=' * $width) -ForegroundColor $Color
  Write-Host (' ' * $pad + $Title) -ForegroundColor $Color
  Write-Host ('=' * $width) -ForegroundColor $Color
}

function Draw-ServiceRow {
  param([string]$Name, [string]$Url, [string]$Status, [ConsoleColor]$StatusColor, [ConsoleColor]$NameColor)
  $namePad = $Name.PadRight(16)
  $urlPad = $Url.PadRight(32)
  $statusPad = $Status.PadRight(10)
  Write-Host "  $namePad $urlPad $statusPad" -ForegroundColor $NameColor
}

# Wait timeout: 90s for normal startup (pre-built), 240s for watch (builds internally).
$waitTimeout = if ($Watch) { 240 } else { 90 }

Write-Host ''
Draw-Banner "COPPADRESD BACKEND - DEV ENVIRONMENT" 'Green'

Write-Host ''
Draw-Banner "POSTGRES (DOCKER)" 'Cyan'
$compose = Join-Path $root 'docker-compose.yaml'
if (Test-Path $compose) {
  Push-Location $root
  try {
    Write-Host "  Starting Postgres..." -NoNewline
    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed. Is Docker running?' }
    Write-Host " DONE" -ForegroundColor Green
  } finally { Pop-Location }

  $deadline = (Get-Date).AddSeconds(60)
  $healthy = $false
  Write-Host "  Waiting for health check..." -NoNewline
  while ((Get-Date) -lt $deadline) {
    $status = docker inspect --format '{{.State.Health.Status}}' coppAddresd 2>$null
    if ($status -eq 'healthy') { $healthy = $true; break }
    Write-Host "." -NoNewline -ForegroundColor Cyan
    Start-Sleep -Seconds 2
  }
  Write-Host ''
  if (-not $healthy) {
    Write-Host "  Postgres not healthy after 60s. Check Docker." -ForegroundColor Red
    exit 1
  }
  Write-Host "  Postgres is healthy!" -ForegroundColor Green
} else {
  Write-Host "  No docker-compose.yaml found. Skipping Postgres (assuming external DB)." -ForegroundColor Yellow
}

# Pre-build phase (skipped for -Watch: dotnet watch builds internally).
if (-not $Watch) {
  Write-Host ''
  Draw-Banner "BUILDING SOLUTION" 'Cyan'
  Write-Host "  Building all 5 projects in parallel (fail fast)..." -ForegroundColor Cyan

  $builds = @()
  foreach ($s in $services) {
    $log = Join-Path $logs ($s.Name + '.build.log')
    $p = Start-Process -FilePath 'dotnet' -ArgumentList @('build', $s.Project) `
      -WorkingDirectory $root -RedirectStandardOutput $log -RedirectStandardError $log `
      -WindowStyle Hidden -PassThru
    $builds += [pscustomobject]@{ Name = $s.Name; Proc = $p; Log = $log }
    Write-Host ("  Building {0}..." -f $s.Name.PadRight(16)) -ForegroundColor $s.Color
  }

  foreach ($b in $builds) { $b.Proc.WaitForExit() }

  $failedBuilds = $builds | Where-Object { $_.Proc.ExitCode -ne 0 }
  if ($failedBuilds.Count -gt 0) {
    Write-Host ''
    Write-Host "  BUILD FAILED. Fix the following project(s) before starting:" -ForegroundColor Red
    foreach ($b in $failedBuilds) {
      Write-Host ("    - {0} (see {1})" -f $b.Name, $b.Log) -ForegroundColor Red
    }
    Write-Host ''
    Write-Host "  Aborting dev-up. No services were started." -ForegroundColor Red
    exit 1
  }
  Write-Host "  All projects built successfully." -ForegroundColor Green
}

Write-Host ''
Draw-Banner "STARTING SERVICES" 'Cyan'

$results = @()
$started = @()
foreach ($s in $services) {
  if (Test-Port $s.Port) {
    Write-Host ("  {0} already running on :{1} (skipped)" -f $s.Name.PadRight(16), $s.Port) -ForegroundColor Yellow
    # It IS running on the port, so report as Running.
    $results += [pscustomobject]@{ Name = $s.Name; Url = $s.Url; Port = $s.Port; Status = 'Running'; Color = $s.Color }
    continue
  }
  if ($Watch) {
    $runArgs = @('watch', 'run', '--project', $s.Project)
  } else {
    $runArgs = @('run', '--no-build', '--project', $s.Project)
  }
  Write-Host ("  Starting {0}..." -f $s.Name.PadRight(16)) -NoNewline -ForegroundColor $s.Color
  $proc = Start-Process -FilePath 'dotnet' -ArgumentList $runArgs -WorkingDirectory $root `
    -RedirectStandardOutput (Join-Path $logs ($s.Name + '.log')) `
    -RedirectStandardError (Join-Path $logs ($s.Name + '.err')) -WindowStyle Hidden -PassThru
  $proc.Id | Out-File (Join-Path $logs ($s.Name + '.pid'))
  $started += $s.Name
  Write-Host " PID $($proc.Id)" -ForegroundColor $s.Color
}

Write-Host ''
Draw-Banner "WAITING FOR SERVICES" 'Cyan'

$failed = $false
foreach ($s in $services) {
  if ($started -notcontains $s.Name) { continue }
  Write-Host ("  Waiting for {0} on port {1}..." -f $s.Name.PadRight(16), $s.Port) -NoNewline -ForegroundColor $s.Color
  if (Wait-Port $s.Port $waitTimeout) {
    Write-Host " READY" -ForegroundColor Green
    $results += [pscustomobject]@{ Name = $s.Name; Url = $s.Url; Port = $s.Port; Status = 'Running'; Color = $s.Color }
  } else {
    Write-Host " FAILED" -ForegroundColor Red
    $results += [pscustomobject]@{ Name = $s.Name; Url = $s.Url; Port = $s.Port; Status = 'FAILED'; Color = 'Red' }
    $failed = $true
    $log = Join-Path $logs ($s.Name + '.log')
    if (Test-Path $log) {
      Write-Host "  --- Last 15 lines of $($s.Name).log ---" -ForegroundColor Gray
      Get-Content $log -Tail 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
  }
}

Write-Host ''
Draw-Banner "SERVICE STATUS" 'Cyan'
Write-Host "  Name             URL                                 Status" -ForegroundColor DarkGray
Write-Host "  ----             ---                                 ------" -ForegroundColor DarkGray
foreach ($r in $results) {
  Draw-ServiceRow -Name $r.Name -Url $r.Url -Status $r.Status -StatusColor $r.Color -NameColor $r.Color
}

Write-Host ''
if ($failed) {
  Draw-Banner "SOME SERVICES FAILED" 'Red'
  Write-Host "  Check logs in: $logs" -ForegroundColor Red
  exit 1
}

Draw-Banner "ALL SERVICES RUNNING" 'Green'
Write-Host "  Stop with: .\scripts\dev-down.ps1" -ForegroundColor Gray
Write-Host "  Logs in: $logs" -ForegroundColor Gray
