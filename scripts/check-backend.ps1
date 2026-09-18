#Requires -Version 5.1
<#
.SYNOPSIS
    Chequeo pre-commit / pre-push del backend CoppAddresd.
    Corre restore, build y todos los tests en el orden correcto:
    unitarias sin BD y luego integracion contra Postgres local.
.DESCRIPTION
    Falla rapido (fail-fast) en el primer paso rojo, con mensaje claro.
    Pre-requisitos (una sola vez):
      - docker compose up -d   (postgres 5432 + valkey, desde la carpeta Repos/)
      - rol "test" creado en Postgres (pedir el paso a paso al agente)
.EXAMPLE
    .\scripts\check-backend.ps1
.EXAMPLE
    .\scripts\check-backend.ps1 -SkipIntegration   # solo restore + build + unitarias
#>
[CmdletBinding()]
param(
    [switch]$SkipIntegration,
    [string]$TestDbConnection = "Host=127.0.0.1;Port=5432;Database=coppaddresd;Username=app_user;Password=CoppAddresdDev!2026"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Invoke-CheckStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "ROJO: $Name (exit $LASTEXITCODE)"
    }
    Write-Host "VERDE: $Name" -ForegroundColor Green
}

function Test-PostgresPort {
    $tcp = New-Object Net.Sockets.TcpClient
    try {
        $iar = $tcp.BeginConnect("127.0.0.1", 5432, $null, $null)
        if (-not $iar.AsyncWaitHandle.WaitOne(3000)) { throw "timeout" }
        $tcp.EndConnect($iar)
    }
    catch {
        throw "Postgres no responde en 127.0.0.1:5432. Arrancalo con: docker compose up -d (desde la carpeta Repos/)"
    }
    finally {
        $tcp.Close()
    }
}

Set-Location -LiteralPath $repoRoot

Invoke-CheckStep "dotnet restore" { dotnet restore }
Invoke-CheckStep "dotnet build" { dotnet build --no-restore }
Invoke-CheckStep "build Community.UnitTests (no esta en el slnx)" { dotnet build tests/CoppAddresd.Community.UnitTests --no-restore }

# --- Unitarias: SIN variable de BD (los skips son por diseno) ---
Remove-Item Env:COP_TEST_DB_CONNECTION -ErrorAction SilentlyContinue
Invoke-CheckStep "unitarias backend" { dotnet test tests/CoppAddresd.UnitTests --no-build }
Invoke-CheckStep "unitarias telemedicina" { dotnet test tests/CoppAddresd.Telemedicine.UnitTests --no-build }
Invoke-CheckStep "unitarias community" { dotnet test tests/CoppAddresd.Community.UnitTests --no-build }

if ($SkipIntegration) {
    Write-Host ""
    Write-Host "Chequeo parcial VERDE (integracion omitida por -SkipIntegration)." -ForegroundColor Green
    return
}

# --- Integracion: CON variable, requiere Postgres local ---
Test-PostgresPort
$env:COP_TEST_DB_CONNECTION = $TestDbConnection
Invoke-CheckStep "integracion backend" { dotnet test tests/CoppAddresd.IntegrationTests --no-build }
Invoke-CheckStep "integracion telemedicina" { dotnet test tests/CoppAddresd.Telemedicine.IntegrationTests --no-build }
Invoke-CheckStep "integracion community" { dotnet test tests/CoppAddresd.Community.IntegrationTests --no-build }

Write-Host ""
Write-Host "Chequeo completo VERDE: listo para commit/push." -ForegroundColor Green
