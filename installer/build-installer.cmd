@echo off
setlocal

set SCRIPT_DIR=%~dp0

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build-msix.ps1" -Configuration Release -Platform x64
if errorlevel 1 (
  echo Failed to build MSIX installer.
  exit /b 1
)

echo Installer build complete.
exit /b 0
