@echo off
title Briefapp Box - Uninstaller

net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo  Removendo menu de contexto do Briefapp Box...

reg delete "HKEY_CLASSES_ROOT\*\shell\BriefappSend" /f >nul 2>&1
reg delete "HKEY_CLASSES_ROOT\Directory\shell\BriefappSend" /f >nul 2>&1
reg delete "HKEY_CLASSES_ROOT\Directory\Background\shell\BriefappSend" /f >nul 2>&1

echo  [OK] Menu de contexto removido.
echo.
pause
