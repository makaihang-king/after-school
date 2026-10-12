@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SummerAfterSchool\Tools\launch-game.ps1"
if errorlevel 1 (
  echo Game launch failed. Check SummerAfterSchool\Logs\launcher.log.
  pause
  exit /b 1
)
exit /b 0
