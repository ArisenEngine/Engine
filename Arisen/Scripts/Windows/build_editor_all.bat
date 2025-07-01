@echo off
setlocal enabledelayedexpansion

REM 根目录假设是 setup-env.bat 的上上级目录，按你项目结构改
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=!SCRIPT_DIR:~0,-1!
set ROOT_DIR=!SCRIPT_DIR!\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"

REM ==== 1. 创建构建目录（如果不存在）====
set VS_BUILD_DIR=!ROOT_DIR!\Projects\Visual Studio\Editor

REM ==== 4. 编译 Debug ====
echo === Building Debug ===
cmake --build "!VS_BUILD_DIR!" --config Debug
if errorlevel 1 (
    echo ERROR: Debug build failed.
    pause
    exit /b 1
)

REM ==== 5 编译 Release ====
echo === Building Release ===
cmake --build "!VS_BUILD_DIR!" --config Release
if errorlevel 1 (
    echo ERROR: Release build failed.
    pause
    exit /b 1
)

echo === All builds succeeded ===

pause
exit /b 0