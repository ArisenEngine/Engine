@echo off
setlocal EnableExtensions

set "SCRIPT_ROOT=%~dp0"
set "CONFIG=Debug"
set "CYCLES=2"
set "SKIP_FAST=0"
set "ARISEN_NO_PAUSE="
if defined CI set "ARISEN_NO_PAUSE=1"

:parse_args
if "%~1"=="" goto :run
if /i "%~1"=="--config" (
    set "CONFIG=%~2"
    shift
) else if /i "%~1"=="--cycles" (
    set "CYCLES=%~2"
    shift
) else if /i "%~1"=="--skip-fast" (
    set "SKIP_FAST=1"
) else if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
) else (
    echo [ERROR] Unknown argument: %~1
    set "EXIT_CODE=1"
    goto :finish
)
shift
goto :parse_args

:run
set "SKIP_FAST_ARG="
if "%SKIP_FAST%"=="1" set "SKIP_FAST_ARG=-SkipFast"

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%validate_stability_stress.ps1" -Configuration "%CONFIG%" -Cycles "%CYCLES%" %SKIP_FAST_ARG%
set "EXIT_CODE=%ERRORLEVEL%"

:finish
echo.
if "%EXIT_CODE%"=="0" (
    echo [Arisen] RESULT: SUCCESS
) else (
    echo [Arisen] RESULT: FAILED
)

if not defined ARISEN_NO_PAUSE (
    echo.
    echo Press any key to close this validation window...
    pause >nul
)

exit /b %EXIT_CODE%
