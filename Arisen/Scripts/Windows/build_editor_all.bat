@echo off
setlocal EnableExtensions enabledelayedexpansion

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=5"

REM === 配置部分 ===
set TARGET=Editor
set PLATFORM=Windows

REM 根目录
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=!SCRIPT_DIR:~0,-1!
set ROOT_DIR=!SCRIPT_DIR!\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"

REM ==== 1. 创建构建目录（如果不存在）====
set VS_BUILD_DIR=!ROOT_DIR!\Projects\Visual Studio\Editor
if not exist "!VS_BUILD_DIR!" (
    mkdir "!VS_BUILD_DIR!"
)

set "LOG_FILE=!VS_BUILD_DIR!\build.log"
echo === Editor Build Log === > "!LOG_FILE!"

REM ==== 2. 配置CMake工程（只需一次，生成多配置.sln） ====
echo === Configuring (Debug + Release) ===
call :next Configuring CMake (multi-config solution)
call :run cmake -S "!ROOT_DIR!" -B "!VS_BUILD_DIR!" -DTARGET="!TARGET!" -DPLATFORM="!PLATFORM!" -G "Visual Studio 17 2022" -A x64
if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 3. 添加 C# 工程到 sln ====
call :next Adding .csproj to solution
call :run cmd /d /v:off /c ""!SCRIPT_DIR!\dotnet_add_csproj_editor.bat" "!VS_BUILD_DIR!\ArisenEditor.sln" "!VS_BUILD_DIR!\Outputs""
if errorlevel 1 (
    echo ERROR: dotnet csproj add failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== group sln ====
call :next Grouping solution folders
call :run python "!SCRIPT_DIR!/group_sln_cs.py" "!VS_BUILD_DIR!\ArisenEditor.sln"
if errorlevel 1 (
    echo ERROR: group sln failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 4. 编译 Debug ====
call :next Building Debug
call :run cmake --build "!VS_BUILD_DIR!" --config Debug
if errorlevel 1 (
    echo ERROR: Debug build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 5. 编译 Release ====
call :next Building Release
call :run cmake --build "!VS_BUILD_DIR!" --config Release
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

echo.
echo (Build finished. Press any key to close...)
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