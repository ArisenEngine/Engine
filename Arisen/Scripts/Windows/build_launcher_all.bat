@echo off
setlocal EnableExtensions enabledelayedexpansion

set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "VSLANG=1033"

set "EXIT_CODE=0"
set "STEP_INDEX=0"
set "STEP_TOTAL=5"

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "ROOT_DIR=%SCRIPT_DIR%\..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

REM === 0. Configure Paths ===
set "TARGET=Launcher"
set "PLATFORM=Windows"
set "LAUNCHER_DIR=!ROOT_DIR!\Editor\ArisenLauncher"

REM Detect manifest (project.arisen takes precedence over manifest.json)
if exist "!LAUNCHER_DIR!\project.arisen" (
    set "MANIFEST_PATH=!LAUNCHER_DIR!\project.arisen"
) else (
    set "MANIFEST_PATH=!LAUNCHER_DIR!\manifest.json"
)

REM ==== 1. Create Build Directory ====
set "VS_BUILD_DIR=!ROOT_DIR!\Projects\VisualStudio\Launcher"
if not exist "!VS_BUILD_DIR!" mkdir "!VS_BUILD_DIR!"

set "LOG_FILE=!VS_BUILD_DIR!\build_launcher.log"
for %%F in ("!LOG_FILE!") do set "LOG_FILE_ABS=%%~fF"
echo === Arisen Launcher Build Log === > "!LOG_FILE_ABS!"

echo [Arisen] Extracting Project Name from launcher manifest...
if exist "!MANIFEST_PATH!" (
    for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$m = Get-Content -LiteralPath '!MANIFEST_PATH!' -Raw | ConvertFrom-Json; if ($m.Name) { $m.Name } else { 'ArisenLauncher' }"`) do set "PROJECT_NAME=%%P"
)
if not defined PROJECT_NAME set "PROJECT_NAME=ArisenLauncher"

REM ==== 2. Process Cleanup ====
echo === Cleaning up [!PROJECT_NAME!] processes ===
taskkill /F /IM !PROJECT_NAME!.exe /T >nul 2>&1
taskkill /F /IM !PROJECT_NAME!.Desktop.exe /T >nul 2>&1

echo [Arisen] Locating Developer Command Prompt...
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found. Is Visual Studio installed?
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
    set "VS_PATH=%%i"
)

if not defined VS_PATH (
    echo [ERROR] Visual Studio with MSBuild not found.
    exit /b 1
)

if not defined VSCMD_ARG_TGT_ARCH (
    echo [Arisen] Initializing vcvars64 environment...
    call "%VS_PATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
)

REM ==== 3. Generate Workspace via ArisenBuildTool ====
echo === Generating Launcher Workspace [!PROJECT_NAME!] ===
call :next Generating Arisen Workspace
call :run dotnet run --project "!ROOT_DIR!\External\ArisenBuildTool\ArisenBuildTool.csproj" -- --workspace "!LAUNCHER_DIR!" --engine "!ROOT_DIR!" --profile Development
if errorlevel 1 (
    echo ERROR: ArisenBuildTool generation failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 4. Build Native Components (Core, etc.) ====
echo === Building Native Components ===
call :next Building Native (Debug)
call :run cmake --build "!LAUNCHER_DIR!\.arisen\Projects\Development\Native\build" --config Debug
if errorlevel 1 (
    echo ERROR: Native Debug build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

call :next Building Native (Release)
call :run cmake --build "!LAUNCHER_DIR!\.arisen\Projects\Development\Native\build" --config Release
if errorlevel 1 (
    echo ERROR: Native Release build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

REM ==== 5. Build Managed Launcher ====
echo === Building Launcher Desktop [!PROJECT_NAME!] ===
set "SLN_PATH=!LAUNCHER_DIR!\.arisen\!PROJECT_NAME!_Development.sln"

call :next Building Managed Launcher (Debug)
call :run msbuild "!SLN_PATH!" /p:Configuration=Debug /p:Platform=x64 /m
if errorlevel 1 (
    echo ERROR: Launcher Managed Debug build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

call :next Building Managed Launcher (Release)
call :run msbuild "!SLN_PATH!" /p:Configuration=Release /p:Platform=x64 /m
if errorlevel 1 (
    echo ERROR: Launcher Managed Release build failed.
    set "EXIT_CODE=1"
    goto :cleanup
)

goto :cleanup

:cleanup
if "%EXIT_CODE%"=="0" (
    echo === All builds succeeded ===
) else (
    echo Script aborted with exit code %EXIT_CODE%. Check log: !LOG_FILE_ABS!
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
>> "!LOG_FILE_ABS!" echo [RUN] %*
%* >> "!LOG_FILE_ABS!" 2>&1
set "_EXIT_CODE=%ERRORLEVEL%"
if not "%_EXIT_CODE%"=="0" (
    echo Command failed with exit code %_EXIT_CODE%. Showing last 120 lines from log:
    powershell -NoProfile -Command "Get-Content -LiteralPath '!LOG_FILE_ABS!' -Tail 120"
)
exit /b %_EXIT_CODE%