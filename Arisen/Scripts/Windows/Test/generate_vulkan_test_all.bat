@echo off
setlocal enabledelayedexpansion

REM === 配置部分 ===
set TARGET=VulkanTest
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
set VS_BUILD_DIR=!ROOT_DIR!\Projects\Visual Studio\VulkanTest
if not exist "!VS_BUILD_DIR!" (
    mkdir "!VS_BUILD_DIR!"
)

REM ==== 2. 配置CMake工程（只需一次，生成多配置.sln） ====
echo === Configuring (Debug + Release) ===

for %%I in ("!LINKER_PATH!") do set "LINKER_DIR=%%~dpI"
set "PATH=!LINKER_DIR!;!PATH!"
echo CMAKE_RC_COMPILER is: !CMAKE_RC_COMPILER!
for %%I in ("!CMAKE_RC_COMPILER!") do set "RC_DIR=%%~dpI"
set "PATH=!RC_DIR!;!PATH!"

cmake -S "!ROOT_DIR!" ^
  -B "!VS_BUILD_DIR!" ^
  -DTARGET="VulkanTest" ^
  -DPLATFORM="Windows" ^
  -G "Visual Studio 17 2022" -A x64

if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    pause
    exit /b 1
)

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
