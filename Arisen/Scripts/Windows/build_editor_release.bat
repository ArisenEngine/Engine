@echo off
setlocal enabledelayedexpansion

REM === 配置部分 ===
set BUILD_CONFIG=Release
set TARGET=Editor
set PLATFORM=Windows

REM 根目录假设是 setup-env.bat 的上上级目录，按你项目结构改
set SCRIPT_DIR=%~dp0
REM 去掉末尾反斜杠，方便拼接
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set ROOT_DIR=%SCRIPT_DIR%\..\..

REM 规范路径转换（绝对路径）
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

REM ==== 1. 调用环境准备脚本 ====
call "%SCRIPT_DIR%\setup-env.bat"
if errorlevel 1 (
    echo ERROR: Environment setup failed. Aborting build.
    exit /b 1
)

REM setup-env.bat 会把 COMPILER_PATH 传出来
echo Using compiler: %COMPILER_PATH%

REM ==== 2. 配置CMake工程 ====
echo === Configuring (%BUILD_CONFIG%) ===
cmake -S "%ROOT_DIR%" -B "%ROOT_DIR%\build" -DTARGET=%TARGET% -DPLATFORM=%PLATFORM% -DCMAKE_BUILD_TYPE=%BUILD_CONFIG% -DCMAKE_CXX_COMPILER="%COMPILER_PATH%"
if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    exit /b 1
)

REM ==== 3. 编译 ====
echo === Building (%BUILD_CONFIG%) ===
cmake --build "%ROOT_DIR%\build" --config %BUILD_CONFIG%
if errorlevel 1 (
    echo ERROR: Build failed.
    exit /b 1
)

echo === Build succeeded ===
pause
exit /b 0
