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

echo [1/2] Refreshing Source Interop with BindingGenerator (Debug)...
set "GEN_CSPROJ=%ROOT_DIR%\BindingGenerator\BindingGenerator.csproj"
set "GEN_LOG=%ROOT_DIR%\Projects\VisualStudio\BindingGenerator\build.log"

if not exist "%ROOT_DIR%\Projects\VisualStudio\BindingGenerator" mkdir "%ROOT_DIR%\Projects\VisualStudio\BindingGenerator"
echo === Binding Generator Build Log === > "%GEN_LOG%"

echo [RUN] dotnet run --project "%GEN_CSPROJ%" --configuration Debug -- --source_code "%ROOT_DIR%\Development" --output "%ROOT_DIR%\Development" >> "%GEN_LOG%"
dotnet run --project "%GEN_CSPROJ%" --configuration Debug -- --source_code "%ROOT_DIR%\Development" --output "%ROOT_DIR%\Development" >> "%GEN_LOG%" 2>&1
if errorlevel 1 goto :fail

echo [3/3] Binding Generation process completed.
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
