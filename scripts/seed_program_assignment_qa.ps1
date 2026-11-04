param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Development')]
    [string]$Environment
)
# SOLO DESARROLLO LOCAL. Sin hosts remotos, secretos, migraciones ni arranque de API.
$ErrorActionPreference = 'Stop'
if ($env:ASPNETCORE_ENVIRONMENT -eq 'Production' -or $env:DOTNET_ENVIRONMENT -eq 'Production') {
    throw 'No se permite ejecutar el fixture desde un entorno Production.'
}
& docker inspect --format '{{.State.Running}}' coppAddresd | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falta el contenedor local coppAddresd.' }
$savedEncoding = $OutputEncoding
try {
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'seed_program_assignment_qa.sql') -Raw -Encoding UTF8 |
        & docker exec -i coppAddresd psql -X -U app_user -d coppaddresd -v ON_ERROR_STOP=1 -v "qa_environment=$Environment"
    if ($LASTEXITCODE -ne 0) { throw 'Falló el seed QA; la transacción no fue confirmada.' }
}
finally { $OutputEncoding = $savedEncoding }
