@echo off
cd /d "%~dp0"
if not exist "AuraFolders.exe" (
    echo Compiling AuraFolders...
    powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1"
)
start "" "%~dp0AuraFolders.exe"
