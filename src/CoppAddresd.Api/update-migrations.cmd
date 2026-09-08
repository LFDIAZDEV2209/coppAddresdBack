@echo off
setlocal
cd /d "%~dp0"
echo ==================================================
echo   Updating AppDbContext Migrations (PostgreSQL)
echo ==================================================

if "%~1"=="" (
    dotnet ef database update --project ..\CoppAddresd.Infrastructure --startup-project . --context AppDbContext
) else (
    dotnet ef database update %1 --project ..\CoppAddresd.Infrastructure --startup-project . --context AppDbContext
)

if %ERRORLEVEL% equ 0 (
    echo.
    echo Migrations applied successfully.
) else (
    echo.
    echo Error applying migrations.
)
exit /b %ERRORLEVEL%
