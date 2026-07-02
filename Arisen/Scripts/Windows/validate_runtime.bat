@echo off
setlocal EnableExtensions

set "SCRIPT_ROOT=%~dp0"
for %%I in ("%SCRIPT_ROOT%..\..") do set "ENGINE_ROOT=%%~fI"
for %%I in ("%ENGINE_ROOT%\..") do set "REPO_ROOT=%%~fI"

set "PROFILES=Development Production RHIVulkanTesting"
set "CONFIG=Debug"
set "FRAMES=1"
set "RUN_FAST=1"
set "ARISEN_NO_PAUSE="
if defined CI set "ARISEN_NO_PAUSE=1"

:parse_args
if "%~1"=="" goto :end_parse
if /i "%~1"=="--profile" (
    set "PROFILES=%~2"
    shift
) else if /i "%~1"=="--config" (
    set "CONFIG=%~2"
    shift
) else if /i "%~1"=="--frames" (
    set "FRAMES=%~2"
    shift
) else if /i "%~1"=="--skip-fast" (
    set "RUN_FAST=0"
) else if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
) else (
    echo [ERROR] Unknown argument: %~1
    set "EXIT_CODE=1"
    goto :finish_no_pop
)
shift
goto :parse_args
:end_parse

pushd "%REPO_ROOT%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to enter repository root: %REPO_ROOT%
    set "EXIT_CODE=1"
    goto :finish_no_pop
)

echo [Arisen] Runtime validation started.
echo [Arisen] Repository root: %REPO_ROOT%
echo [Arisen] Profiles: %PROFILES%
echo [Arisen] Configuration: %CONFIG%
echo [Arisen] Smoke frames: %FRAMES%

if "%RUN_FAST%"=="1" (
    call "%SCRIPT_ROOT%validate_fast.bat" --no-pause
    if errorlevel 1 goto :fail
)

for %%P in (%PROFILES%) do (
    call :validate_profile "%%P"
    if errorlevel 1 goto :fail
)

echo [Arisen] Runtime validation succeeded.
set "EXIT_CODE=0"
goto :finish

:validate_profile
set "CURRENT_PROFILE=%~1"
echo.
echo [Arisen] --------------------------------------------------
echo [Arisen] Runtime profile smoke: %CURRENT_PROFILE% [%CONFIG%]
echo [Arisen] --------------------------------------------------

call "%SCRIPT_ROOT%build_workspace.bat" --manifest "%ENGINE_ROOT%\Development\PackageGame\manifest.json" --profile "%CURRENT_PROFILE%" --config "%CONFIG%" --no-pause
if errorlevel 1 exit /b 1

set "BIN_DIR=%ENGINE_ROOT%\Development\PackageGame\.arisen\bin\%CURRENT_PROFILE%\%CONFIG%"
set "EXE_PATH=%BIN_DIR%\PackageGame.exe"
set "RESOLVED_MANIFEST=%BIN_DIR%\manifest.resolved.json"

if not exist "%RESOLVED_MANIFEST%" (
    echo [ERROR] Resolved manifest not found: %RESOLVED_MANIFEST%
    exit /b 1
)

if not exist "%EXE_PATH%" (
    echo [ERROR] Runtime executable not found: %EXE_PATH%
    exit /b 1
)

echo [Arisen] Running runtime smoke: %EXE_PATH%
pushd "%BIN_DIR%" >nul
"%EXE_PATH%" --workspace "%ENGINE_ROOT%\Development\PackageGame" --profile "%CURRENT_PROFILE%" --smoke --frames "%FRAMES%"
set "SMOKE_EXIT=%ERRORLEVEL%"
popd >nul

if not "%SMOKE_EXIT%"=="0" (
    echo [ERROR] Runtime smoke for profile %CURRENT_PROFILE% failed with exit code %SMOKE_EXIT%.
    exit /b 1
)

echo [Arisen] Runtime profile smoke passed: %CURRENT_PROFILE%
exit /b 0

:fail
set "EXIT_CODE=1"
echo.
echo [Arisen] Runtime validation failed.

:finish
popd >nul

:finish_no_pop
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
