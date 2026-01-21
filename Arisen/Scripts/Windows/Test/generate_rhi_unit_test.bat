@echo off
setlocal EnableExtensions enabledelayedexpansion

REM ============================================================================
REM ArisenEngine - Generate and Build RHIUnitTest (VS multi-config solution)
REM ----------------------------------------------------------------------------
REM Usage:
REM   generate_vulkan_test_all.bat
REM
REM This script:
REM   1) Uses environment prepared by setup-env.bat (MSVC/SDK/Ninja)
REM   2) Generates a Visual Studio multi-config solution with CMake
REM   3) Builds Debug and Release
REM ============================================================================

REM === Ensure console code page matches localized tool output ===
for /f "tokens=2 delims=:" %%I in ('chcp') do set "ORIGINAL_CP=%%I"
set "ORIGINAL_CP=!ORIGINAL_CP: =!"
if defined ARISEN_CODEPAGE (
    chcp %ARISEN_CODEPAGE% >nul
) else (
    chcp 65001 >nul
)

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=4"

REM === 配置部分 ===
set TARGET=RHITestCase
set PLATFORM=Windows

REM 根目录假设是 setup-env.bat 的上上级目录，按你项目结构改
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=!SCRIPT_DIR:~0,-1!
set ROOT_DIR=!SCRIPT_DIR!\..\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"

REM 输出工具链信息
echo CMake Program: !CMAKE_MAKE_PROGRAM!
echo Using compiler: !COMPILER_PATH!

REM ==== 1. 创建构建目录（如果不存在）====
set VS_BUILD_DIR=!ROOT_DIR!\Projects\VisualStudio\RHITestCase
if not exist "!VS_BUILD_DIR!" (
    mkdir "!VS_BUILD_DIR!"
)

set "LOG_FILE=!VS_BUILD_DIR!\build.log"
echo === RHITestCase Build Log === > "!LOG_FILE!"

REM ==== 2. 配置CMake工程（只需一次，生成多配置.sln） ====
echo === Configuring (Debug + Release) ===

for %%I in ("!LINKER_PATH!") do set "LINKER_DIR=%%~dpI"
set "PATH=!LINKER_DIR!;!PATH!"
echo CMAKE_RC_COMPILER is: !CMAKE_RC_COMPILER!
for %%I in ("!CMAKE_RC_COMPILER!") do set "RC_DIR=%%~dpI"
set "PATH=!RC_DIR!;!PATH!"

call :step "Configuring CMake (multi-config solution)" cmake -S "!ROOT_DIR!" -B "!VS_BUILD_DIR!" -DTARGET="RHITestCase" -DPLATFORM="Windows" -G "Visual Studio 17 2022" -A x64

if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 4. 编译 Debug ====
call :step "Building Debug" cmake --build "!VS_BUILD_DIR!" --config Debug
if errorlevel 1 (
    echo ERROR: Debug build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 5 编译 Release ====
call :step "Building Release" cmake --build "!VS_BUILD_DIR!" --config Release
if errorlevel 1 (
    echo ERROR: Release build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

goto :cleanup

:cleanup
if "%EXIT_CODE%"=="0" (
    echo === All builds succeeded ===
) else (
    echo Script aborted with exit code %EXIT_CODE%.
)

if defined ORIGINAL_CP chcp !ORIGINAL_CP! >nul
pause
exit /b %EXIT_CODE%

:step
set /a STEP_INDEX+=1 >nul
set "DESC=%~1"
shift /1
echo [!STEP_INDEX!/!STEP_TOTAL!] !DESC!
call :run %*
exit /b %ERRORLEVEL%

:run
echo [RUN] %*
>> "%LOG_FILE%" echo [RUN] %*
%* >> "%LOG_FILE%" 2>&1
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
    echo Command failed with exit code %RC%. Showing last 120 lines from log:
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
)
exit /b %RC%
