@echo off

REM =========================================================================
REM  Binding Generator Script
REM  Initializes MSVC environment, then builds and runs the binding generator.
REM =========================================================================

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

if /i "%~1"=="--no-pause" (
    set "ARISEN_NO_PAUSE=1"
    shift /1
)


echo === Initializing MSVC Environment ===
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

call "%SCRIPT_DIR%\setup-env.bat"
if errorlevel 1 exit /b 1

set "ARISEN_TARGET=BindingGenerator"
set "ARISEN_PLATFORM=Windows"
set "ROOT_DIR=%SCRIPT_DIR%\..\.."

REM 规范路径转换（绝对路径）
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

set "VS_BUILD_DIR=%ROOT_DIR%\Projects\VisualStudio\BindingGenerator"
if not exist "%VS_BUILD_DIR%" mkdir "%VS_BUILD_DIR%"

set "LOG_FILE=%VS_BUILD_DIR%\build.log"
echo === Binding Generator Build Log === > "%LOG_FILE%"

echo [1/3] Configuring CMake...
echo [RUN] cmake -S "%ROOT_DIR%" -B "%VS_BUILD_DIR%" -DTARGET="%ARISEN_TARGET%" -DPLATFORM="%ARISEN_PLATFORM%" -G "Visual Studio 17 2022" -A x64 >> "%LOG_FILE%"
cmake -S "%ROOT_DIR%" -B "%VS_BUILD_DIR%" -DTARGET="%ARISEN_TARGET%" -DPLATFORM="%ARISEN_PLATFORM%" -G "Visual Studio 17 2022" -A x64 >> "%LOG_FILE%" 2>&1
if errorlevel 1 goto :fail

echo [2/3] Building and Running GenerateAutoBinding (Release)...
echo [RUN] cmake --build "%VS_BUILD_DIR%" --config Release --target GenerateAutoBinding >> "%LOG_FILE%"
cmake --build "%VS_BUILD_DIR%" --config Release --target GenerateAutoBinding >> "%LOG_FILE%" 2>&1
if errorlevel 1 goto :fail

echo [3/3] Verifying generated output...
set "BINDING_OUTPUT_DIR=%ROOT_DIR%\Packages.Generated"
set "CS_COUNT=0"
if exist "%BINDING_OUTPUT_DIR%" (
    for /r "%BINDING_OUTPUT_DIR%" %%F in (*.cs) do set /a CS_COUNT+=1
)

if "%CS_COUNT%" == "0" goto :fail

echo Generated %CS_COUNT% C# binding file(s) in Packages.Generated.
echo === Binding generation succeeded ===
echo.
if not defined ARISEN_NO_PAUSE pause
exit /b 0

:fail
echo.
echo ============================================================
echo   PROCESS FAILED. Showing last 50 lines from log:
echo ============================================================
powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG_FILE%' -Tail 50"
if not defined ARISEN_NO_PAUSE pause
exit /b 1
