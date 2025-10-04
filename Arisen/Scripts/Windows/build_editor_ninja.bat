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
    set "EXIT_CODE=1"
    goto :cleanup
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
    set "EXIT_CODE=1"
    goto :cleanup
)

mkdir "!ROOT_DIR!\build"


for %%I in ("!LINKER_PATH!") do set "LINKER_DIR=%%~dpI"
set "PATH=!LINKER_DIR!;!PATH!"
echo CMAKE_RC_COMPILER is: !CMAKE_RC_COMPILER!
for %%I in ("!CMAKE_RC_COMPILER!") do set "RC_DIR=%%~dpI"
set "PATH=!RC_DIR!;!PATH!"

echo CMake Command: -S "%ROOT_DIR%" ^
  -B "%ROOT_DIR%\build" ^
  -DTARGET="%TARGET%" ^
  -DPLATFORM="%PLATFORM%" ^
  -DCMAKE_BUILD_TYPE="%BUILD_CONFIG%" ^
  -DCMAKE_CXX_COMPILER="%COMPILER_PATH%" ^
  -DCMAKE_C_COMPILER="%COMPILER_PATH%" ^
  -DCMAKE_LINKER="%LINKER_PATH%" ^
  -DCMAKE_MAKE_PROGRAM="%CMAKE_MAKE_PROGRAM%" ^
  -DMT_PATH="%MT_PATH%" ^
  -DCMAKE_RC_COMPILER=`%CMAKE_RC_COMPILER%` ^
  -DCMAKE_RC_COMPILER_INIT=`rc` ^
  -G Ninja

cmake -S "%ROOT_DIR%" ^
  -B "%ROOT_DIR%\build" ^
  -DTARGET=%TARGET% ^
  -DPLATFORM=%PLATFORM% ^
  -DCMAKE_BUILD_TYPE=%BUILD_CONFIG% ^
  -DCMAKE_CXX_COMPILER=%COMPILER_PATH% ^
  -DCMAKE_C_COMPILER=%COMPILER_PATH% ^
  -DCMAKE_LINKER=%LINKER_PATH% ^
  -DCMAKE_MAKE_PROGRAM=%CMAKE_MAKE_PROGRAM% ^
  -DMT_PATH=%MT_PATH% ^
  -DCMAKE_RC_COMPILER=%CMAKE_RC_COMPILER% ^
  -DCMAKE_RC_COMPILER_INIT=rc ^
  -G Ninja

if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 3. 编译 ====
echo === Building (!BUILD_CONFIG!) ===
cmake --build "!ROOT_DIR!\build" --config !BUILD_CONFIG!
if errorlevel 1 (
    echo ERROR: Build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

echo === Build succeeded ===
goto :cleanup

:cleanup
if defined ORIGINAL_CP chcp !ORIGINAL_CP! >nul
if not "%EXIT_CODE%"=="0" (
    echo Script aborted with exit code %EXIT_CODE%.
    pause
    exit /b %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
