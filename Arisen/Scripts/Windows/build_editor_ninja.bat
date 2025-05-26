@echo off
setlocal enabledelayedexpansion

REM === 配置部分 ===
set BUILD_CONFIG=Release
set TARGET=Editor
set PLATFORM=Windows

REM 根目录假设是 setup-env.bat 的上上级目录，按你项目结构改
set SCRIPT_DIR=%~dp0
REM 去掉末尾反斜杠，方便拼接
set "SCRIPT_DIR=!SCRIPT_DIR:~0,-1!"
set ROOT_DIR=!SCRIPT_DIR!\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"

REM ==== 1. 调用环境准备脚本 ====
call "!SCRIPT_DIR!\setup-env.bat"
if errorlevel 1 (
    echo ERROR: Environment setup failed. Aborting build.
    pause
    exit /b 1
)
call "!SCRIPT_DIR!\env-vars.bat"

echo CMake Program: !CMAKE_MAKE_PROGRAM!

REM setup-env.bat 会把 COMPILER_PATH 传出来
echo Using compiler: !COMPILER_PATH!

REM ==== 2. 配置CMake工程 ====
echo === Configuring (!BUILD_CONFIG!) ===

:: 清理构建目录
if exist "!ROOT_DIR!\build" (
    echo Removing build directory...
    rmdir /s /q "!ROOT_DIR!\build"
)

if exist "!ROOT_DIR!\build" (
    echo ERROR: Failed to remove build directory.
    pause
    exit /b 1
)

mkdir "!ROOT_DIR!\build"


for %%I in ("!LINKER_PATH!") do set "LINKER_DIR=%%~dpI"
set "PATH=!LINKER_DIR!;!PATH!"
echo CMAKE_RC_COMPILER is: !CMAKE_RC_COMPILER!
for %%I in ("!CMAKE_RC_COMPILER!") do set "RC_DIR=%%~dpI"
set "PATH=!RC_DIR!;!PATH!"

echo CMake Command: cmake -S "%ROOT_DIR%" ^
  -B "%ROOT_DIR%\build" ^
  -DTARGET="%TARGET%" ^
  -DPLATFORM="%PLATFORM%" ^
  -DCMAKE_BUILD_TYPE="%BUILD_CONFIG%" ^
  -DCMAKE_CXX_COMPILER="%COMPILER_PATH%" ^
  -DCMAKE_C_COMPILER="%COMPILER_PATH%" ^
  -DCMAKE_LINKER="%LINKER_PATH%" ^
  -DCMAKE_MAKE_PROGRAM="%CMAKE_MAKE_PROGRAM%" ^
  -DMT_PATH="%MT_PATH%" ^
  -DCMAKE_RC_COMPILER="%CMAKE_RC_COMPILER%" ^
  -G Ninja

@REM cmake -S "%ROOT_DIR%" ^
@REM   -B "%ROOT_DIR%\build" ^
@REM   -DTARGET="%TARGET%" ^
@REM   -DPLATFORM="%PLATFORM%" ^
@REM   -DCMAKE_BUILD_TYPE="%BUILD_CONFIG%" ^
@REM   -DCMAKE_CXX_COMPILER="%COMPILER_PATH%" ^
@REM   -DCMAKE_C_COMPILER="%COMPILER_PATH%" ^
@REM   -DCMAKE_LINKER="%LINKER_PATH%" ^
@REM   -DCMAKE_MAKE_PROGRAM="%CMAKE_MAKE_PROGRAM%" ^
@REM   -DMT_PATH="%MT_PATH%" ^
@REM   -DCMAKE_RC_COMPILER="%CMAKE_RC_COMPILER%" ^
@REM   -DCMAKE_RC_COMPILER_INIT="rc" ^
@REM   -G Ninja

cmake -S "../../" ^
  -B "../../build" ^
  -DTARGET="Editor" ^
  -DPLATFORM="Windows" ^
  -DCMAKE_BUILD_TYPE="Release" ^
  -G "Visual Studio 17 2022" -A x64

if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    pause
    exit /b 1
)

REM ==== 3. 编译 ====
echo === Building (!BUILD_CONFIG!) ===
cmake --build "!ROOT_DIR!\build" --config !BUILD_CONFIG!
if errorlevel 1 (
    echo ERROR: Build failed.
    exit /b 1
    pause
)

echo === Build succeeded ===
pause
exit /b 0
