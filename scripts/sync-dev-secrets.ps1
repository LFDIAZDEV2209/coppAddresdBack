<#
  sync-dev-secrets.ps1 - Sincroniza la configuracion de desarrollo con los secretos
  de AWS Secrets Manager (nombres legacy: ApiDevelopment, AuthDevelopment, ...).

  Fuente de verdad local (lo que corre en la maquina del dev):
    - appsettings.Development.json de cada servicio .NET (gitignoreado).
    - ai-service\.env y coppaddresd-front\.env.local (gitignoreados).

  Flujo de onboarding de un dev nuevo:
    1. docker compose up -d (raiz del workspace)
    2. sync-dev-secrets.ps1 -Pull            (descarga los secretos a los archivos locales)
    3. .\scripts\dev-up.ps1

  Modos:
    (default)     Compara local vs secreto (salida enmascarada). Exit 1 si hay diferencias.
    -Push         Sube el archivo local al secreto (solo si difiere; -Force lo omite del check).
    -Pull         Descarga el secreto al archivo local si no existe.
    -OverwriteLocal  Con -Pull: sobrescribe el archivo local existente.

  Ejemplos:
    .\scripts\sync-dev-secrets.ps1
    .\scripts\sync-dev-secrets.ps1 -Push -Force
    .\scripts\sync-dev-secrets.ps1 -Pull -OverwriteLocal
#>
param(
  [switch]$Push,
  [switch]$Pull,
  [switch]$Force,
  [switch]$OverwriteLocal,
  [string]$Region = 'us-east-2'
)
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path          # coppAddresdBack
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path   # Repos

# Correspondencia secreto AWS <-> archivo local. Los nombres de secretos
# terminan en Development/Dev por creacion manual historica; NO renombrar sin
# revisar consumidores (DEVOPS_INFRASTRUCTURE.md seccion 27).
$secrets = @(
  @{ Name = 'ApiDevelopment';          Type = 'json'; File = (Join-Path $root 'src\CoppAddresd.Api\appsettings.Development.json') },
  @{ Name = 'AuthDevelopment';         Type = 'json'; File = (Join-Path $root 'src\Services\CoppAddresd.Auth\appsettings.Development.json') },
  @{ Name = 'GatewayDevelopment';      Type = 'json'; File = (Join-Path $root 'src\Services\CoppAddresd.Gateway\appsettings.Development.json') },
  @{ Name = 'TelemedicineDevelopment'; Type = 'json'; File = (Join-Path $root 'src\Services\CoppAddresd.Telemedicine\appsettings.Development.json') },
  @{ Name = 'ComunnityDev';            Type = 'json'; File = (Join-Path $root 'src\Services\CoppAddresd.Community\appsettings.Development.json') },
  @{ Name = 'AiServiceDevelopment';    Type = 'env';  File = (Join-Path $repoRoot 'ai-service\.env') },
  @{ Name = 'ERPdevelopment';          Type = 'env';  File = (Join-Path $repoRoot 'coppaddresd-front\.env.local') }
)

if (-not (Get-Command aws -CommandType Application -ErrorAction SilentlyContinue)) {
  Write-Error "AWS CLI no encontrado en PATH. Instala AWS CLI v2 y configura el perfil."
}

# Construye el payload del secreto desde el archivo local.
# json: se sube el archivo tal cual. env: se serializa KEY=VALOR a objeto JSON
# (sin comentarios), manteniendo el shape que ya usan estos secretos.
function Get-LocalPayload {
  param($s)
  if ($s.Type -eq 'env') {
    $h = [ordered]@{}
    Get-Content $s.File -Encoding UTF8 | ForEach-Object {
      if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
      $i = $_.IndexOf('=')
      if ($i -lt 1) { return }
      $h[$_.Substring(0, $i).Trim()] = $_.Substring($i + 1)
    }
    return ($h | ConvertTo-Json -Compress)
  }
  return (Get-Content $s.File -Raw -Encoding UTF8).Trim()
}

function Get-SecretPayload {
  param($Name)
  $val = aws secretsmanager get-secret-value --secret-id $Name --region $Region --query SecretString --output text 2>$null
  if ($LASTEXITCODE -ne 0) { throw "No se pudo leer el secreto '$Name' (revisa credenciales AWS)." }
  return ([string]$val).Trim()
}

# Aplana un objeto JSON a claves 'A.B.C'. Comparacion recursiva estable.
function Flatten($o, $p, $dict) {
  if ($null -eq $o) { $dict[$p.TrimEnd('.')] = $null; return }
  if ($o -is [System.Management.Automation.PSCustomObject]) {
    foreach ($pr in $o.PSObject.Properties) { Flatten $pr.Value ($p + $pr.Name + '.') $dict }
  } elseif ($o -is [System.Collections.IEnumerable] -and -not ($o -is [string]) -and -not ($o -is [System.ValueType])) {
    $i = 0
    foreach ($e in $o) { Flatten $e ($p + $i + '.') $dict; $i++ }
  } else {
    $dict[$p.TrimEnd('.')] = $o
  }
}

function Parse-Payload {
  param([string]$payload, [string]$type)
  if ($type -eq 'env') { return ($payload | ConvertFrom-Json) }
  return ($payload | ConvertFrom-Json)
}

# Claves cuyo valor se enmascara en la salida (nunca imprime secretos).
function Mask {
  param($key, $val)
  if ($key -match '(?i)(secret|password|token|key|connection|database_url|sid)') {
    $len = if ($null -eq $val) { 0 } else { ([string]$val).Length }
    return "<REDACTADO len=$len>"
  }
  $s = if ($null -eq $val) { '' } else { [string]$val }
  if ($s.Length -gt 80) { $s = $s.Substring(0, 77) + '...' }
  return $s
}

function Compare-Secret {
  param($s, $secretPayload, $localPayload)
  $sec = @{}; $loc = @{}
  Flatten (Parse-Payload $secretPayload $s.Type) '' $sec
  Flatten (Parse-Payload $localPayload $s.Type) '' $loc
  $onlyLocal  = @($loc.Keys  | Where-Object { -not $sec.ContainsKey($_) })
  $onlySecret = @($sec.Keys  | Where-Object { -not $loc.ContainsKey($_) })
  $diffs      = @($sec.Keys | Where-Object { $loc.ContainsKey($_) -and ([string]$sec[$_]) -ne ([string]$loc[$_]) })
  if ($onlyLocal.Count -eq 0 -and $onlySecret.Count -eq 0 -and $diffs.Count -eq 0) { return $null }
  $report = New-Object System.Collections.Generic.List[string]
  foreach ($k in ($onlyLocal | Sort-Object))  { $report.Add("    + solo en LOCAL  : $k = $(Mask $k $loc[$k])") }
  foreach ($k in ($onlySecret | Sort-Object)) { $report.Add("    - solo en AWS   : $k = $(Mask $k $sec[$k])") }
  foreach ($k in ($diffs | Sort-Object))      { $report.Add("    ~ difiere       : $k  (aws=$(Mask $k $sec[$k]), local=$(Mask $k $loc[$k]))") }
  return $report
}

$dirty = $false
foreach ($s in $secrets) {
  $label = "{0,-26} -> {1}" -f $s.Name, (Split-Path $s.File -Leaf)
  if (-not (Test-Path $s.File)) {
    Write-Host ("{0}: ARCHIVO LOCAL NO EXISTE (usa -Pull para descargarlo)" -f $label) -ForegroundColor Yellow
    $dirty = $true
    continue
  }
  $localPayload = Get-LocalPayload $s
  $secretPayload = Get-SecretPayload -Name $s.Name
  $report = Compare-Secret $s $secretPayload $localPayload

  if ($Pull) {
    if (-not (Test-Path $s.File) -or $OverwriteLocal) {
      if ($s.Type -eq 'env') {
        # Reconstruye .env con lineas KEY=VALUE desde el JSON del secreto.
        $obj = $secretPayload | ConvertFrom-Json
        $lines = @("# Restaurado desde Secrets Manager ($($s.Name)) por sync-dev-secrets.ps1")
        foreach ($pr in $obj.PSObject.Properties) { $lines += ("{0}={1}" -f $pr.Name, $pr.Value) }
        [IO.File]::WriteAllLines($s.File, $lines, (New-Object System.Text.UTF8Encoding($false)))
      } else {
        [IO.File]::WriteAllText($s.File, $secretPayload + "`n", (New-Object System.Text.UTF8Encoding($false)))
      }
      Write-Host ("{0}: DESCARGADO" -f $label) -ForegroundColor Green
    } else {
      Write-Host ("{0}: existe; usa -OverwriteLocal para sobrescribir" -f $label) -ForegroundColor Yellow
    }
    continue
  }

  if ($null -eq $report) {
    Write-Host ("{0}: OK (sin diferencias)" -f $label) -ForegroundColor Green
    continue
  }

  $dirty = $true
  Write-Host ("{0}: DIFIERE" -f $label) -ForegroundColor Yellow
  foreach ($line in $report) { Write-Host $line }

  if ($Push) {
    $tmp = Join-Path $env:TEMP ("secretsync-" + $s.Name + ".json")
    [IO.File]::WriteAllText($tmp, $localPayload, (New-Object System.Text.UTF8Encoding($false)))
    try {
      aws secretsmanager put-secret-value --secret-id $s.Name --region $Region --secret-string "file://$($tmp -replace '\\','/')"
      if ($LASTEXITCODE -ne 0) { throw "put-secret-value fallo para $($s.Name)" }
      $exitCode = 0  # diferencias resueltas por el push
      Write-Host ("{0}: ACTUALIZADO EN AWS" -f $label) -ForegroundColor Green
    } finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
  }
}

if ($dirty -and -not $Push) {
  Write-Host ''
  Write-Host "Hay diferencias. Para subir el estado local: .\scripts\sync-dev-secrets.ps1 -Push -Force" -ForegroundColor Cyan
}
if ($dirty) { exit 1 } else { exit 0 }
