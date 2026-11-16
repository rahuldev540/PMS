@echo off
setlocal
cd /d "%~dp0.."
where python >nul 2>nul
if %ERRORLEVEL% EQU 0 (
  python scripts\check_standalone.py
  if errorlevel 1 exit /b 1
) else (
  echo Python unavailable: skipping optional source guard.
)
dotnet restore Pharmacy.sln
if errorlevel 1 exit /b 1
dotnet build Pharmacy.sln --no-restore
if errorlevel 1 exit /b 1
echo Source and build checks completed successfully.
