<#
  dev-up.ps1 - Levanta Postgres + Auth + Community + API (entorno de desarrollo).
  Uso: .\scripts\dev-up.ps1 [-Watch]
  -Watch: usa `dotnet watch run` para hot reload.
  Detener: .\scripts\dev-down.ps1
#>
param(
  [switch]$Watch
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logs = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

$services = @(
  @{ Name = 'auth';      Project = 'src/Services/CoppAddresd.Auth';      Url = 'http://localhost:5123'; Port = 5123 },
  @{ Name = 'community'; Project = 'src/Services/CoppAddresd.Community'; Url = 'http://localhost:5200'; Port = 5200 },
  @{ Name = 'api';       Project = 'src/CoppAddresd.Api';                Url = 'http://localhost:5122'; Port = 5122 }
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

Write-Host ''
Write-Host '== Postgres (docker compose) ==' -ForegroundColor Cyan
$compose = Join-Path $root 'docker-compose.yaml'
if (-not (Test-Path $compose)) {
  Write-Host "Falta $compose en la raiz del repo." -ForegroundColor Red
  exit 1
}
Push-Location $root
try {
  docker compose up -d postgres
  if ($LASTEXITCODE -ne 0) { throw 'docker compose up fallo. Revisa que Docker este corriendo.' }
} finally { Pop-Location }

$deadline = (Get-Date).AddSeconds(60)
$healthy = $false
while ((Get-Date) -lt $deadline) {
  $status = docker inspect --format '{{.State.Health.Status}}' coppAddresd 2>$null
  if ($status -eq 'healthy') { $healthy = $true; break }
  Start-Sleep -Seconds 2
}
if (-not $healthy) {
  Write-Host 'Postgres no quedo healthy en 60s. Revisa Docker.' -ForegroundColor Red
  exit 1
}
Write-Host 'Postgres healthy.' -ForegroundColor Green

Write-Host ''
Write-Host '== Servicios ==' -ForegroundColor Cyan
$results = @()
$started = @()
foreach ($s in $services) {
  if (Test-Port $s.Port) {
    Write-Host ("skip {0} (ya escucha en :{1})" -f $s.Name, $s.Port) -ForegroundColor Yellow
    $results += [pscustomobject]@{ Name = $s.Name; Url = $s.Url; Status = 'Skipped' }
    continue
  }
  $runArgs = @('run', '--project', $s.Project)
  if ($Watch) { $runArgs = @('watch', 'run', '--project', $s.Project) }
  $proc = Start-Process -FilePath 'dotnet' -ArgumentList $runArgs -WorkingDirectory $root `
    -RedirectStandardOutput (Join-Path $logs ($s.Name + '.log')) `
    -RedirectStandardError (Join-Path $logs ($s.Name + '.err')) -WindowStyle Hidden -PassThru
  $proc.Id | Out-File (Join-Path $logs ($s.Name + '.pid'))
  $started += $s.Name
  Write-Host ("iniciando {0} (PID {1})..." -f $s.Name, $proc.Id)
}

Write-Host ''
Write-Host '== Esperando puertos ==' -ForegroundColor Cyan
$failed = $false
foreach ($s in $services) {
  if ($started -notcontains $s.Name) { continue }
  if (Wait-Port $s.Port 90) {
    Write-Host ("{0} listo en {1}" -f $s.Name, $s.Url) -ForegroundColor Green
    $results += [pscustomobject]@{ Name = $s.Name; Url = $s.Url; Status = 'Up' }
  } else {
    Write-Host ("{0} NO arranco en {1}" -f $s.Name, $s.Url) -ForegroundColor Red
    $results += [pscustomobject]@{ Name = $s.Name; Url = $s.Url; Status = 'FAILED' }
    $failed = $true
    $log = Join-Path $logs ($s.Name + '.log')
    if (Test-Path $log) { Write-Host '--- ultimas lineas del log ---'; Get-Content $log -Tail 15 }
  }
}
Write-Host ''
$results | Format-Table -AutoSize
if ($failed) {
  Write-Host 'Algunos servicios fallaron (revisa los logs en scripts\logs\).' -ForegroundColor Red
  exit 1
}
Write-Host 'Todo arriba. Detener con .\scripts\dev-down.ps1' -ForegroundColor Green