@echo off
setlocal EnableExtensions

REM ============================================================================
REM ArisenEngine - Generate and Build ArisenEngineTest (VS multi-config solution)
REM ----------------------------------------------------------------------------
REM Usage:
REM   generate_arisen_engine_test_all.bat [--no-pause]
REM
REM This script:
REM   1) Loads toolchain environment (MSVC/SDK/LLVM/Ninja) via setup-env.bat
REM   2) Generates a Visual Studio multi-config solution with CMake
REM   3) Adds managed .csproj projects into the solution
REM   4) Groups solution folders and builds Debug and Release
REM
REM Options:
REM   --no-pause   Do not pause at the end (useful for CI/IDE terminals)
REM ============================================================================

REM No code page change

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=5"

REM Optional --no-pause flag
if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
    shift /1
)

REM === Config section ===
set TARGET=CSharpEngineTest
set PLATFORM=Windows

REM Script and root directories
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "ROOT_DIR=%SCRIPT_DIR%\..\..\.."

REM Normalize to absolute path
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

REM ==== Prepare environment (compiler/linker/Ninja/RC) ====
set "ENV_DIR=%SCRIPT_DIR%\.."
if exist "%ENV_DIR%\setup-env.bat" (
    call "%ENV_DIR%\setup-env.bat"
    if errorlevel 1 (
        echo ERROR: setup-env failed.
        set "EXIT_CODE=1"
        goto :cleanup
    )
    if exist "%ENV_DIR%\env-vars.bat" (
        REM env-vars.bat exports COMPILER_PATH/LINKER_PATH/CMAKE_MAKE_PROGRAM/etc.
        call "%ENV_DIR%\env-vars.bat"
    )
) else (
    echo WARNING: setup-env.bat not found at %ENV_DIR%
)

REM Toolchain info (for diagnostics)
echo CMake Program: %CMAKE_MAKE_PROGRAM%
echo Using compiler: %COMPILER_PATH%

REM ==== 1. Create build directory if not exists ====
set VS_BUILD_DIR=%ROOT_DIR%\Projects\VisualStudio\CSharpEngineTest
if not exist "%VS_BUILD_DIR%" (
    mkdir "%VS_BUILD_DIR%"
)

REM Log file for all invoked commands
set "LOG_FILE=%VS_BUILD_DIR%\build.log"
echo === CSharpEngineTest Build Log === > "%LOG_FILE%"

REM ==== 2. Configure CMake (multi-config .sln) ====
echo === Configuring (Debug + Release) ===

REM Ensure linker/rc are on PATH for CMake
for %%I in ("%LINKER_PATH%") do set "LINKER_DIR=%%~dpI"
set "PATH=%LINKER_DIR%;%PATH%"
echo CMAKE_RC_COMPILER is: %CMAKE_RC_COMPILER%
for %%I in ("%CMAKE_RC_COMPILER%") do set "RC_DIR=%%~dpI"
set "PATH=%RC_DIR%;%PATH%"

set /a STEP_INDEX+=1 >nul
echo [%STEP_INDEX%/%STEP_TOTAL%] Configuring CMake (multi-config solution)
call :run cmake -S "%ROOT_DIR%" -B "%VS_BUILD_DIR%" -DTARGET="CSharpEngineTest" -DPLATFORM="Windows" -G "Visual Studio 17 2022" -A x64

if errorlevel 1 (
    echo ERROR: CMake configuration failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 3. Add csproj ====
set /a STEP_INDEX+=1 >nul
echo [%STEP_INDEX%/%STEP_TOTAL%] Adding .csproj to solution
call :run call "%SCRIPT_DIR%\dotnet_add_csproj_csharp_engine_test.bat" "%VS_BUILD_DIR%\CSharpEngineTest.sln" "%VS_BUILD_DIR%\Outputs"
if errorlevel 1 (
    echo ERROR: dotnet csproj add failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== group (arrange solution folders) ====
set /a STEP_INDEX+=1 >nul
echo [%STEP_INDEX%/%STEP_TOTAL%] Grouping solution folders
call :run python "%SCRIPT_DIR%\..\group_sln_cs.py" "%VS_BUILD_DIR%\CSharpEngineTest.sln"
if errorlevel 1 (
    echo ERROR: group sln failed.
    set "EXIT_CODE=1"
    goto :cleanup
)


REM ==== 4. Build Debug ====
set /a STEP_INDEX+=1 >nul
echo [%STEP_INDEX%/%STEP_TOTAL%] Building Debug
call :run cmake --build "%VS_BUILD_DIR%" --config Debug
if errorlevel 1 (
    echo ERROR: Debug build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 5. Build Release ====
set /a STEP_INDEX+=1 >nul
echo [%STEP_INDEX%/%STEP_TOTAL%] Building Release
call :run cmake --build "%VS_BUILD_DIR%" --config Release
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

if not defined ARISEN_NO_PAUSE pause
exit /b %EXIT_CODE%

:run
REM Echo the command and log it before execution
echo [RUN] %*
>> "%LOG_FILE%" echo [RUN] %*
%* >> "%LOG_FILE%" 2>&1
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
    echo Command failed with exit code %RC%. Showing last 120 lines from log:
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
)
exit /b %RC%
