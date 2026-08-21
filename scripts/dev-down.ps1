<#
  dev-down.ps1 - Detiene Auth + Community + API iniciados con dev-up.ps1.
  Postgres (docker compose) queda arriba; detenerlo con: docker compose down
#>
$ErrorActionPreference = 'Stop'

$logs = Join-Path $PSScriptRoot 'logs'
$files = Get-ChildItem $logs -Filter '*.pid' -ErrorAction SilentlyContinue
if (-not $files) {
  Write-Host 'No hay servicios registrados (nada que detener).' -ForegroundColor Yellow
  exit 0
}
foreach ($f in $files) {
  $name = $f.BaseName
  $id = [int](Get-Content $f.FullName -Raw)
  if (Get-Process -Id $id -ErrorAction SilentlyContinue) {
    taskkill /PID $id /T /F 2>&1 | Out-Null
    Write-Host ("detenido {0} (PID {1})" -f $name, $id) -ForegroundColor Green
  } else {
    Write-Host ("{0} ya no corria (PID {1})" -f $name, $id) -ForegroundColor Yellow
  }
  Remove-Item $f.FullName -Force
}
Write-Host 'Postgres queda arriba (detener con: docker compose down).'