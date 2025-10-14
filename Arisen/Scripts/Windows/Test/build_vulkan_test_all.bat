@echo off
setlocal EnableExtensions

REM === Skip code page and language tweaks to avoid parsing issues ===
REM set "DOTNET_CLI_UI_LANGUAGE=en-US"
REM set "VSLANG=1033"

set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=3"

REM Optional --no-pause flag
if /i "%~1"=="--no-pause" set "ARISEN_NO_PAUSE=1"

REM Resolve directories
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "ROOT_DIR=%SCRIPT_DIR%\..\..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"


set "VS_BUILD_DIR=%ROOT_DIR%\Projects\VisualStudio\VulkanTest"
set "LOG_FILE=%VS_BUILD_DIR%\build.log"

echo ROOT_DIR: %ROOT_DIR%
echo VS_BUILD_DIR: %VS_BUILD_DIR%

if not exist "%VS_BUILD_DIR%" mkdir "%VS_BUILD_DIR%"

echo === VulkanTest Build Log === > "%LOG_FILE%"

REM Configure on first run when CMakeCache.txt is absent
if not exist "%VS_BUILD_DIR%\CMakeCache.txt" (
    echo === Configuring (Debug + Release) ===
    cmake -S "%ROOT_DIR%" -B "%VS_BUILD_DIR%" -DTARGET=VulkanTest -DPLATFORM=Windows -G "Visual Studio 17 2022" -A x64 >> "%LOG_FILE%" 2>&1 || goto :fail
)

REM Build Debug
echo === Building Debug ===
cmake --build "%VS_BUILD_DIR%" --config Debug >> "%LOG_FILE%" 2>&1 || goto :fail

REM Build Release
echo === Building Release ===
cmake --build "%VS_BUILD_DIR%" --config Release >> "%LOG_FILE%" 2>&1 || goto :fail

echo === All builds succeeded ===
if /i "%~1"=="--no-pause" exit /b 0
pause
exit /b 0

:fail
echo ERROR: Build failed. See log: "%LOG_FILE%"
if exist "%LOG_FILE%" powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
if /i "%~1"=="--no-pause" exit /b 1
pause
exit /b 1