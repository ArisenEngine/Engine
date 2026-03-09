@echo off
setlocal EnableExtensions

REM ============================================================================
REM ArisenEngine - Build CSharpEngineTest (All Configs)
REM ----------------------------------------------------------------------------
REM Usage:
REM   build_csharp_engine_test_all.bat [--no-pause]
REM ============================================================================

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"
set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=5"

if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
    shift /1
)

set TARGET=CSharpEngineTest
set PLATFORM=Windows
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "ROOT_DIR=%SCRIPT_DIR%\..\..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

set "ENV_DIR=%SCRIPT_DIR%\.."
set "ORIGINAL_SCRIPT_DIR=%SCRIPT_DIR%"
if exist "%ENV_DIR%\setup-env.bat" (
    call "%ENV_DIR%\setup-env.bat"
    if errorlevel 1 (
        echo ERROR: setup-env failed.
        exit /b 1
    )
)
set "SCRIPT_DIR=%ORIGINAL_SCRIPT_DIR%"

set VS_BUILD_DIR=%ROOT_DIR%\Projects\VisualStudio\CSharpEngineTest
if not exist "%VS_BUILD_DIR%" mkdir "%VS_BUILD_DIR%"
set "LOG_FILE=%VS_BUILD_DIR%\build.log"
echo === CSharpEngineTest Build Log === > "%LOG_FILE%"

echo === Configuring and Building All Configurations ===

REM 1. Configure
set /a STEP_INDEX+=1
echo [%STEP_INDEX%/%STEP_TOTAL%] Configuring CMake (multi-config solution)
call :run cmake -S "%ROOT_DIR%" -B "%VS_BUILD_DIR%" -DTARGET="CSharpEngineTest" -DPLATFORM="Windows" -G "Visual Studio 17 2022" -A x64
if errorlevel 1 goto :fail

REM 2. Add C# Projects
set /a STEP_INDEX+=1
echo [%STEP_INDEX%/%STEP_TOTAL%] Adding .csproj to solution
call :run call "%SCRIPT_DIR%\dotnet_add_csproj_csharp_engine_test.bat" "%VS_BUILD_DIR%\CSharpEngineTest.sln" "%VS_BUILD_DIR%\Outputs"
if errorlevel 1 goto :fail

REM 3. Group Solution
set /a STEP_INDEX+=1
echo [%STEP_INDEX%/%STEP_TOTAL%] Grouping solution folders
call :run python "%SCRIPT_DIR%\..\group_sln_cs.py" "%VS_BUILD_DIR%\CSharpEngineTest.sln"
if errorlevel 1 goto :fail

REM 4. Build Debug
set /a STEP_INDEX+=1
echo [%STEP_INDEX%/%STEP_TOTAL%] Building Debug
call :run cmake --build "%VS_BUILD_DIR%" --config Debug
if errorlevel 1 goto :fail

REM 5. Build Release
set /a STEP_INDEX+=1
echo [%STEP_INDEX%/%STEP_TOTAL%] Building Release
call :run cmake --build "%VS_BUILD_DIR%" --config Release
if errorlevel 1 goto :fail

goto :cleanup

:fail
echo ERROR: Build failed. See log: "%LOG_FILE%"
set "EXIT_CODE=1"
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
echo [RUN] %*
>> "%LOG_FILE%" echo [RUN] %*
%* >> "%LOG_FILE%" 2>&1
set "_EXIT_CODE=%ERRORLEVEL%"
if not "%_EXIT_CODE%"=="0" (
    echo Command failed with exit code %_EXIT_CODE%. Showing last 120 lines from log:
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 120"
)
exit /b %_EXIT_CODE%
