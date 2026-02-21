@echo off
setlocal EnableExtensions enabledelayedexpansion

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=3"

REM === 配置部分 ===
set TARGET=BindingGenerator
set PLATFORM=Windows

REM 根目录
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=!SCRIPT_DIR:~0,-1!
set ROOT_DIR=!SCRIPT_DIR!\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"

REM ==== 1. 创建构建目录（如果不存在）====
set VS_BUILD_DIR=!ROOT_DIR!\Projects\VisualStudio\BindingGenerator
if not exist "!VS_BUILD_DIR!" (
    mkdir "!VS_BUILD_DIR!"
)

set "LOG_FILE=!VS_BUILD_DIR!\build.log"
echo === Binding Generator Build Log === > "!LOG_FILE!"

REM ==== 2. 配置CMake工程 ====
echo === Configuring Standalone Binding Generator ===
call :next Configuring CMake
call :run cmake -S "!ROOT_DIR!" -B "!VS_BUILD_DIR!" -DTARGET="!TARGET!" -DPLATFORM="!PLATFORM!" -G "Visual Studio 17 2022" -A x64
if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 3. 编译并执行 GenerateAutoBinding ====
call :next Building and Running GenerateAutoBinding
call :run cmake --build "!VS_BUILD_DIR!" --config Debug --target GenerateAutoBinding
if errorlevel 1 (
    echo ERROR: Binding generation failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

goto :cleanup

:cleanup
if "%EXIT_CODE%"=="0" (
    echo === Binding generation succeeded ===
) else (
    echo Script aborted with exit code %EXIT_CODE%.
)

echo.
echo (Process finished. Press any key to close...)
pause >nul
exit /b %EXIT_CODE%

:next
set /a STEP_INDEX+=1 >nul
echo [!STEP_INDEX!/!STEP_TOTAL!] %*
exit /b 0

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
