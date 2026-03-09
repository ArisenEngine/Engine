@echo off
setlocal EnableExtensions enabledelayedexpansion

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
set "STEP_TOTAL=5"

REM === 配置部分 ===
set TARGET=Editor
set PLATFORM=Windows

REM 根目录假设是 setup-env.bat 的上上级目录，按你项目结构改
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=!SCRIPT_DIR:~0,-1!
set ROOT_DIR=!SCRIPT_DIR!\..\..

REM 规范路径转换（绝对路径）
for %%I in ("!ROOT_DIR!") do set "ROOT_DIR=%%~fI"

REM 输出工具链信息
echo CMake Program: !CMAKE_MAKE_PROGRAM!
echo Using compiler: !COMPILER_PATH!

REM ==== 1. 创建构建目录（如果不存在）====
set VS_BUILD_DIR=!ROOT_DIR!\Projects\Visual Studio\Editor
if not exist "!VS_BUILD_DIR!" (
    mkdir "!VS_BUILD_DIR!"
)

set "LOG_FILE=!VS_BUILD_DIR!\build.log"
echo === Editor Build Log === > "!LOG_FILE!"

REM ==== 2. 配置CMake工程（只需一次，生成多配置.sln） ====
echo === Configuring (Debug + Release) ===

for %%I in ("!LINKER_PATH!") do set "LINKER_DIR=%%~dpI"
set "PATH=!LINKER_DIR!;!PATH!"
echo CMAKE_RC_COMPILER is: !CMAKE_RC_COMPILER!
for %%I in ("!CMAKE_RC_COMPILER!") do set "RC_DIR=%%~dpI"
set "PATH=!RC_DIR!;!PATH!"

call :step "Configuring CMake (multi-config solution)" cmake -S "!ROOT_DIR!" -B "!VS_BUILD_DIR!" -DTARGET="Editor" -DPLATFORM="Windows" -G "Visual Studio 17 2022" -A x64

if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 3. 添加csproj ====
call :step "Adding .csproj to solution" call "!SCRIPT_DIR!/dotnet_add_csproj_editor.bat" "!VS_BUILD_DIR!\ArisenEditor.sln" "!VS_BUILD_DIR!\Outputs"
if errorlevel 1 (
    echo ERROR: dotnet csproj add failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== group 
call :step "Grouping solution folders" python "!SCRIPT_DIR!/group_sln_cs.py" "!VS_BUILD_DIR!\ArisenEditor.sln"
if errorlevel 1 (
    echo ERROR: group sln failed.
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
set "_EXIT_CODE=%ERRORLEVEL%"
if not "%_EXIT_CODE%"=="0" (
    echo Command failed with exit code %_EXIT_CODE%. Showing last 120 lines from log:
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
)
exit /b %_EXIT_CODE%
