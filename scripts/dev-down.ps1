<#
  dev-down.ps1 - Stops all services (Auth, Community, Gateway, Telemedicine, API) by PORT.
  Catches skipped, manually-started and stale-PID services. Postgres (docker compose)
  stays up; stop it with: docker compose down
#>
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logs = Join-Path $PSScriptRoot 'logs'

# Ports the dev environment owns (mirrors dev-up.ps1).
$ports = @(
  @{ Name = 'auth';         Port = 5123 },
  @{ Name = 'community';    Port = 5200 },
  @{ Name = 'gateway';      Port = 5080 },
  @{ Name = 'telemedicine'; Port = 5130 },
  @{ Name = 'api';          Port = 5122 }
)

function Draw-Banner {
  param([string]$Title, [ConsoleColor]$Color = 'Cyan')
  $width = 50
  $pad = [math]::Floor(($width - $Title.Length) / 2)
  Write-Host ''
  Write-Host ('=' * $width) -ForegroundColor $Color
  Write-Host (' ' * $pad + $Title) -ForegroundColor $Color
  Write-Host ('=' * $width) -ForegroundColor $Color
}

Draw-Banner "STOPPING SERVICES" 'Cyan'

foreach ($p in $ports) {
  $listenerPid = Get-NetTCPConnection -LocalPort $p.Port -State Listen -ErrorAction SilentlyContinue `
    | Select-Object -First 1 -ExpandProperty OwningProcess
  if ($listenerPid) {
    & taskkill /PID $listenerPid /T /F 2>$null | Out-Null
    Write-Host ("  Stopped {0} (port :{1})" -f $p.Name.PadRight(16), $p.Port) -ForegroundColor Green
  } else {
    Write-Host ("  Already stopped {0} (port :{1})" -f $p.Name.PadRight(16), $p.Port) -ForegroundColor Yellow
  }
}

# Clean up any leftover pid files (stale or from skipped services).
if (Test-Path $logs) {
  Get-ChildItem $logs -Filter '*.pid' -ErrorAction SilentlyContinue | Remove-Item -Force
}

Write-Host ''
Write-Host 'Postgres stays up (stop with: docker compose down).' -ForegroundColor Gray
