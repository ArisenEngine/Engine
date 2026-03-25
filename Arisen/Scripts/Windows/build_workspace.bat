@echo off
setlocal EnableDelayedExpansion

:: Defaults
set "MANIFEST_PATH="
set "BINDING_CONFIG=debug"

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="-m" (
    set "MANIFEST_PATH=%~2"
    shift
) else if /i "%~1"=="--manifest" (
    set "MANIFEST_PATH=%~2"
    shift
) else if /i "%~1"=="-b" (
    set "BINDING_CONFIG=%~2"
    shift
) else if /i "%~1"=="--binding-config" (
    set "BINDING_CONFIG=%~2"
    shift
) else (
    if not defined MANIFEST_PATH set "MANIFEST_PATH=%~1"
)
shift
goto parse_args
:end_parse

if "%MANIFEST_PATH%"=="" set "MANIFEST_PATH=%~dp0..\..\Development\PackageGame\manifest.json"

if /i "%BINDING_CONFIG%"=="release" (
    set "BINDING_BAT=run_binding_generator_release.bat"
) else (
    set "BINDING_BAT=run_binding_generator_debug.bat"
)

set "SLN_DIR=%MANIFEST_PATH%\..\.arisen"
set "SLN_PATH=%SLN_DIR%\PackageGame.sln"
set "BUILD_TOOL_CSPROJ=%~dp0..\..\External\ArisenBuildTool\ArisenBuildTool.csproj"

echo [Arisen] Locating Developer Command Prompt...
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found. Is Visual Studio installed?
    goto :fail
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
    set "VS_PATH=%%i"
)

if not defined VS_PATH (
    echo [ERROR] Visual Studio with MSBuild not found.
    goto :fail
)

if not defined VSCMD_ARG_TGT_ARCH (
    echo [Arisen] Initializing vcvars64 environment...
    call "%VS_PATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
)

echo [Arisen] Synchronizing C# Bindings via BindingGenerator (!BINDING_BAT!)...
call "%~dp0!BINDING_BAT!" --no-pause
if %errorlevel% neq 0 (
    echo [ERROR] BindingGenerator failed to refresh interop code.
    goto :fail
)

set "ENGINE_ROOT=%~dp0..\.."
echo [Arisen] Generating Workspace with ArisenBuildTool...
dotnet run --project "%BUILD_TOOL_CSPROJ%" -- generate -m "%MANIFEST_PATH%" -e "%ENGINE_ROOT%"
if %errorlevel% neq 0 (
    echo [ERROR] ArisenBuildTool failed to generate workspace.
    goto :fail
)

echo [Arisen] Restoring NuGet Packages...
dotnet restore "%SLN_PATH%"
if %errorlevel% neq 0 (
    echo [ERROR] NuGet restore failed.
    goto :fail
)

echo [Arisen] Discovering profiles from manifest.json...
for /f "usebackq delims=" %%A in (`powershell -Command "$m = Get-Content '%MANIFEST_PATH%' -Raw | ConvertFrom-Json; if($m.Profiles) { $m.Profiles.psobject.properties.name } else { 'Development'; 'Production' }"`) do (
    echo [Arisen] --------------------------------------------------
    echo [Arisen] Compiling Profile: %%A
    echo [Arisen] --------------------------------------------------
    set "LOG_FILE=%MANIFEST_PATH%\..\build_%%A.log"
    echo [Arisen] Logging MSBuild completely to: !LOG_FILE!
    msbuild "%SLN_PATH%" /p:Configuration=%%A /p:Platform=x64 /m /fl /flp:logfile="!LOG_FILE!";verbosity=normal
    if errorlevel 1 (
        echo [ERROR] MSBuild failed on profile: %%A
        goto :fail
    )
)

echo.
echo [Arisen] Build successful for all runtime profiles!
pause
exit /b 0

:fail
echo.
echo [ERROR] Build Pipeline aborted due to execution or compilation errors.
pause
exit /b 1
