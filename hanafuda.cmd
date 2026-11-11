@echo off
setlocal
title Hanafuda Opti
set "HF_DIR=%~dp0"
set "HF_SELF=%~f0"

if /i "%~1"=="install" goto run
if /i "%~1"=="uninstall" goto run
if /i "%~1"=="list" goto run
if /i "%~1"=="help" goto run
if /i "%~1"=="consola" goto console
if exist "%HF_DIR%HanafudaOpti.exe" (
    start "" "%HF_DIR%HanafudaOpti.exe" %*
    exit /b 0
)

:console
fltmc >nul 2>&1
if not errorlevel 1 goto runconsole
powershell -NoProfile -Command "try { Start-Process -FilePath $env:HF_SELF -ArgumentList 'consola' -Verb RunAs -ErrorAction Stop } catch { exit 1 }"
if errorlevel 1 echo Hanafuda Opti necesita permisos de administrador.
exit /b

:runconsole
color 0F
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%HF_DIR%hanafuda.ps1"
exit /b %errorlevel%

:run
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%HF_DIR%hanafuda.ps1" %*
exit /b %errorlevel%
