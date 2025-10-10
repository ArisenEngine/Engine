@echo off
setlocal enabledelayedexpansion

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

REM 根目录假设是 setup-env.bat 的上上级目录，按你项目结构改
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=!SCRIPT_DIR:~0,-1!
set ROOT_DIR=!SCRIPT_DIR!\..\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"


set VS_BUILD_DIR=!ROOT_DIR!\Projects\VisualStudio\VulkanTest

REM ==== 4. 编译 Debug ====
echo === Building Debug ===
cmake --build "!VS_BUILD_DIR!" --config Debug
if errorlevel 1 (
    echo ERROR: Debug build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 5 编译 Release ====
echo === Building Release ===
cmake --build "!VS_BUILD_DIR!" --config Release
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