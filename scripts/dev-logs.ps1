<#
  dev-logs.ps1 - View dev service logs.
  Usage:
    .\scripts\dev-logs.ps1                  # list services + which are running + log files present
    .\scripts\dev-logs.ps1 <service>        # last 50 lines of <service>.log
    .\scripts\dev-logs.ps1 <service> -Follow   # live tail (Ctrl+C to exit)
    .\scripts\dev-logs.ps1 <service> -Err       # view <service>.err instead of .log
    .\scripts\dev-logs.ps1 <service> -Lines 200 # last 200 lines
  Services: auth, community, gateway, telemedicine, api, ai, postgres
  Logs live in scripts/logs (already gitignored). Postgres is a Docker container.
#>
param(
  [string]$Service = '',
  [switch]$Follow,
  [switch]$Err,
  [int]$Lines = 50
)
$ErrorActionPreference = 'Stop'

$logs = Join-Path $PSScriptRoot 'logs'

$Services = @{
  auth         = @{ Port = 5123; Color = 'Cyan';    Docker = $false }
  community    = @{ Port = 5200; Color = 'Green';   Docker = $false }
  gateway      = @{ Port = 5080; Color = 'Magenta'; Docker = $false }
  telemedicine = @{ Port = 5130; Color = 'Yellow';  Docker = $false }
  api          = @{ Port = 5122; Color = 'Blue';    Docker = $false }
  ai           = @{ Port = 8000; Color = 'DarkCyan';Docker = $false }
  postgres     = @{ Port = 5432; Color = 'White';   Docker = $true  }
}

function Draw-Banner {
  param([string]$Title, [ConsoleColor]$Color = 'Cyan')
  $width = 50
  $pad = [math]::Floor(($width - $Title.Length) / 2)
  Write-Host ''
  Write-Host ('=' * $width) -ForegroundColor $Color
  Write-Host (' ' * $pad + $Title) -ForegroundColor $Color
  Write-Host ('=' * $width) -ForegroundColor $Color
}

# --- List mode ---
if ($Service -eq '') {
  Draw-Banner "DEV SERVICE LOGS" 'Cyan'
  Write-Host "  Usage: .\scripts\dev-logs.ps1 <service> [-Follow] [-Err] [-Lines N]" -ForegroundColor Gray
  Write-Host "  Services:" -ForegroundColor Cyan
  foreach ($k in $Services.Keys) {
    $s = $Services[$k]
    $running = (Get-NetTCPConnection -LocalPort $s.Port -State Listen -ErrorAction SilentlyContinue) -ne $null
    $base = Join-Path $logs $k
    $hasLog = (Test-Path ($base + '.log')) -or (Test-Path ($base + '.err'))
    $status = if ($running) { 'RUNNING' } else { 'stopped' }
    $mark = if ($hasLog) { 'log available' } else { 'no log' }
    Write-Host ("    {0,-12} :{1,-5} {2,-8} {3}" -f $k, $s.Port, $status, $mark) -ForegroundColor $s.Color
  }
  Write-Host ''
  Write-Host "  Examples:" -ForegroundColor Cyan
  Write-Host "    .\scripts\dev-logs.ps1 api            # last 50 lines of api.log" -ForegroundColor Gray
  Write-Host "    .\scripts\dev-logs.ps1 api -Follow    # live tail" -ForegroundColor Gray
  Write-Host "    .\scripts\dev-logs.ps1 ai -Err        # ai.err (Python writes to stderr)" -ForegroundColor Gray
  Write-Host "    .\scripts\dev-logs.ps1 postgres       # docker logs coppAddresd" -ForegroundColor Gray
  exit 0
}

if (-not $Services.ContainsKey($Service)) {
  Write-Host "  Unknown service '$Service'. Valid: $($Services.Keys -join ', ')" -ForegroundColor Red
  exit 1
}

$s = $Services[$Service]

# --- Docker service (postgres) ---
if ($s.Docker) {
  if ($Follow) {
    docker logs -f coppAddresd
  } else {
    docker logs --tail $Lines coppAddresd
  }
  exit $LASTEXITCODE
}

# --- .NET / Python services (log files in scripts/logs) ---
$base = Join-Path $logs $Service
$logFile = if ($Err) { $base + '.err' } else { $base + '.log' }
# Fallback to .err when .log is missing or empty (uvicorn/Python writes to stderr).
if (-not $Err -and (-not (Test-Path $logFile) -or ((Get-Item $logFile).Length -eq 0)) -and (Test-Path ($base + '.err'))) {
  $logFile = $base + '.err'
}
if (-not (Test-Path $logFile)) {
  Write-Host "  No log for '$Service' ($logFile)." -ForegroundColor Yellow
  Write-Host "  Start it with .\scripts\dev-up.ps1 (or dev-up.sh)." -ForegroundColor Gray
  exit 1
}

Draw-Banner "$Service log" $s.Color
if ($Follow) {
  Get-Content -Path $logFile -Wait -Tail 30
} else {
  Get-Content -Path $logFile -Tail $Lines
}
