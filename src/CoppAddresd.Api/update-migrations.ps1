param(
    [string]$TargetMigration = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Updating AppDbContext Migrations (PostgreSQL)   " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$projectPath = "..\CoppAddresd.Infrastructure"

$argsList = @(
    "ef", "database", "update",
    "--project", $projectPath,
    "--startup-project", ".",
    "--context", "AppDbContext"
)

if (![string]::IsNullOrWhiteSpace($TargetMigration)) {
    $argsList += $TargetMigration
    Write-Host "Targeting migration: $TargetMigration" -ForegroundColor Yellow
} else {
    Write-Host "Applying latest migrations..." -ForegroundColor Green
}

& dotnet @argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nMigrations applied successfully." -ForegroundColor Green
} else {
    Write-Host "`nError applying migrations. Check database connection and history." -ForegroundColor Red
    exit $LASTEXITCODE
}
