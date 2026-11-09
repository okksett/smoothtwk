@echo off
setlocal
title Hanafuda Opti
set "HF_DIR=%~dp0"
set "HF_SELF=%~f0"
set "HF_ARGS=%*"

if /i "%~1"=="install" goto run
if /i "%~1"=="uninstall" goto run
if /i "%~1"=="list" goto run
if /i "%~1"=="help" goto run

fltmc >nul 2>&1
if not errorlevel 1 goto run

powershell -NoProfile -Command "try { if ($env:HF_ARGS) { Start-Process -FilePath $env:HF_SELF -ArgumentList $env:HF_ARGS -Verb RunAs -ErrorAction Stop } else { Start-Process -FilePath $env:HF_SELF -Verb RunAs -ErrorAction Stop } } catch { exit 1 }"
if errorlevel 1 echo Hanafuda Opti necesita permisos de administrador.
exit /b

:run
color 0F
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%HF_DIR%hanafuda.ps1" %*
exit /b %errorlevel%
