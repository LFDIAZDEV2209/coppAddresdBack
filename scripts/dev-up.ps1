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
  Write-Host "  Building all 5 projects (fail fast)..." -ForegroundColor Cyan

  $failed = $false
  foreach ($s in $services) {
    $outLog = Join-Path $logs ($s.Name + '.build.log')
    $errLog = Join-Path $logs ($s.Name + '.build.err')
    Write-Host ("  Building {0}..." -f $s.Name.PadRight(16)) -NoNewline -ForegroundColor $s.Color
    & dotnet build $s.Project *> $outLog
    if ($LASTEXITCODE -ne 0) {
      Write-Host " FAILED" -ForegroundColor Red
      $failed = $true
      Write-Host ("    See {0}" -f $outLog) -ForegroundColor Red
    } else {
      Write-Host " OK" -ForegroundColor Green
    }
  }

  if ($failed) {
    Write-Host ''
    Write-Host "  BUILD FAILED. Fix the project(s) above before starting." -ForegroundColor Red
    Write-Host ''
    Write-Host "  Aborting dev-up. No services were started." -ForegroundColor Red
    exit 1
  }
  Write-Host "  All projects built successfully." -ForegroundColor Green
}

Write-Host ''
Draw-Banner "AI SERVICE (PYTHON/FastAPI)" 'DarkCyan'
$AiRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\ai-service') -ErrorAction SilentlyContinue).Path
$AiName = 'ai'
$AiColor = 'DarkCyan'
$AiUrl = 'http://localhost:8000'
$AiStarted = $false
$AiResult = $null
if (-not $AiRoot -or -not (Test-Path $AiRoot)) {
  Write-Host "  ai-service repo not found (expected at ../ai-service). Skipped." -ForegroundColor Yellow
  $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'Missing'; Color = 'Yellow' }
} elseif (-not (Test-Path (Join-Path $AiRoot '.env'))) {
  Write-Host "  ai-service: .env missing (cp .env.example .env). Skipped." -ForegroundColor Yellow
  $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'Skipped'; Color = 'Yellow' }
} elseif (-not (Test-Path (Join-Path $AiRoot '.venv'))) {
  Write-Host "  ai-service: .venv missing (run 'uv sync' in ai-service). Skipped." -ForegroundColor Yellow
  $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'Skipped'; Color = 'Yellow' }
} elseif (Test-Port 8000) {
  Write-Host ("  {0} already running on :8000 (skipped)" -f $AiName.PadRight(16)) -ForegroundColor Yellow
  $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'Running'; Color = $AiColor }
} else {
  Write-Host ("  Starting {0}..." -f $AiName.PadRight(16)) -NoNewline -ForegroundColor $AiColor
  $uvPath = (Get-Command uv -CommandType Application -ErrorAction SilentlyContinue).Source
  if (-not $uvPath) {
    $candidate = Join-Path $env:USERPROFILE '.local\bin\uv.exe'
    if (Test-Path $candidate) { $uvPath = $candidate }
  }
  if (-not $uvPath) {
    Write-Host " 'uv' not found on PATH. Install it (https://astral.sh/uv) or add it to PATH." -ForegroundColor Red
    $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'Skipped'; Color = 'Yellow' }
  } else {
    $proc = Start-Process -FilePath $uvPath -ArgumentList @('run', 'python', 'run_dev.py') -WorkingDirectory $AiRoot `
      -RedirectStandardOutput (Join-Path $logs ($AiName + '.log')) `
      -RedirectStandardError (Join-Path $logs ($AiName + '.err')) -WindowStyle Hidden -PassThru
    $proc.Id | Out-File (Join-Path $logs ($AiName + '.pid'))
    $AiStarted = $true
    Write-Host " PID $($proc.Id)" -ForegroundColor $AiColor
  }
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
    $err = Join-Path $logs ($s.Name + '.err')
    $errText = if (Test-Path $err) { Get-Content $err -Raw } else { '' }
    if ($errText -match 'Application Control policy has blocked') {
      Write-Host "  Windows Application Control (Smart App Control / WDAC) blocked the executable." -ForegroundColor Yellow
      Write-Host "  Allow the repo path in Windows Security > App & browser control, or disable Smart App Control." -ForegroundColor Yellow
    } elseif ($errText -match 'not recognize|not a valid|cannot find') {
      Write-Host "  The process failed to start. Check $err" -ForegroundColor Yellow
    }
    if (Test-Path $log) {
      Write-Host "  --- Last 15 lines of $($s.Name).log ---" -ForegroundColor Gray
      Get-Content $log -Tail 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
  }
}

# AI service wait (non-fatal: backend has circuit breaker if it is down).
if ($AiStarted -and $AiResult) {
  Write-Host ("  Waiting for {0} on port {1}..." -f 'ai'.PadRight(16), 8000) -NoNewline -ForegroundColor $AiColor
  if (Wait-Port 8000 $waitTimeout) {
    $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'Running'; Color = $AiColor }
    Write-Host " READY" -ForegroundColor Green
  } else {
    $AiResult = [pscustomobject]@{ Name = 'ai'; Url = $AiUrl; Port = 8000; Status = 'FAILED'; Color = 'Red' }
    Write-Host " FAILED (non-fatal)" -ForegroundColor Red
    $log = Join-Path $logs 'ai.log'
    if (Test-Path $log) {
      Write-Host "  --- Last 15 lines of ai.log ---" -ForegroundColor Gray
      Get-Content $log -Tail 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
  }
}

Write-Host ''
Draw-Banner "SERVICE STATUS" 'Cyan'
Write-Host "  Name             URL                                 Status" -ForegroundColor DarkGray
Write-Host "  ----             ---                                 ------" -ForegroundColor DarkGray
if ($AiResult) { $results += $AiResult }
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
Write-Host "  View logs: .\scripts\dev-logs.ps1 <service> [-Follow] [-Err]" -ForegroundColor Gray
Write-Host "  Services: auth, community, gateway, telemedicine, api, ai, postgres" -ForegroundColor Gray
