@echo off
setlocal EnableDelayedExpansion
set "SCRIPT_ROOT=%~dp0"

:: Defaults
set "MANIFEST_PATH="
set "BUILD_CONFIG=Debug"
set "TARGET_PROFILE="
set "TEST_PACKAGE_ID="

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="-m" (
    set "MANIFEST_PATH=%~2"
    shift
) else if /i "%~1"=="--manifest" (
    set "MANIFEST_PATH=%~2"
    shift
) else if /i "%~1"=="-b" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="--binding-config" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="-c" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="--config" (
    set "BUILD_CONFIG=%~2"
    shift
) else if /i "%~1"=="-p" (
    set "TARGET_PROFILE=%~2"
    shift
) else if /i "%~1"=="--profile" (
    set "TARGET_PROFILE=%~2"
    shift
) else if /i "%~1"=="-t" (
    set "TEST_PACKAGE_ID=%~2"
    shift
) else if /i "%~1"=="--package" (
    set "TEST_PACKAGE_ID=%~2"
    shift
) else (
    if not defined MANIFEST_PATH (
        set "MANIFEST_PATH=%~1"
    )
)
shift
goto parse_args
:end_parse

if "%MANIFEST_PATH%"=="" (
    if exist "!SCRIPT_ROOT!..\..\Development\PackageGame\project.arisen" (
        set "MANIFEST_PATH=!SCRIPT_ROOT!..\..\Development\PackageGame\project.arisen"
    ) else (
        set "MANIFEST_PATH=!SCRIPT_ROOT!..\..\Development\PackageGame\manifest.json"
    )
)
for %%I in ("%MANIFEST_PATH%") do set "MANIFEST_PATH=%%~fI"

if /i "!BUILD_CONFIG!"=="Release" (
    set "BINDING_BAT=run_binding_generator_release.bat"
) else (
    set "BINDING_BAT=run_binding_generator_debug.bat"
)

set "WORKSPACE_DIR=%MANIFEST_PATH%\.."
for %%I in ("!WORKSPACE_DIR!") do set "WORKSPACE_DIR=%%~fI"
set "SLN_DIR=!WORKSPACE_DIR!\.arisen"
set "BUILD_TOOL_CSPROJ=!SCRIPT_ROOT!..\..\External\ArisenBuildTool\ArisenBuildTool.csproj"
set "ENGINE_ROOT=!SCRIPT_ROOT!..\.."

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

echo [Arisen] Refreshing managed bindings...
call "!SCRIPT_ROOT!!BINDING_BAT!" --no-pause
if !errorlevel! neq 0 (
    echo [ERROR] BindingGenerator failed to refresh interop code.
    goto :fail
)

echo [Arisen] Ensuring ArisenBuildTool is compiled...
dotnet build "%BUILD_TOOL_CSPROJ%" -c Release >nul

:: Determine mode and profiles
if defined TEST_PACKAGE_ID (
    echo [Arisen] MODE: Isolated Package Test [!TEST_PACKAGE_ID!]
    set "PROFILES=Testing"
    set "MODE=IsolatedTest"
) else (
    set "MODE=ProfileLoop"
    if defined TARGET_PROFILE (
        echo [Arisen] MODE: Specific Profile [!TARGET_PROFILE!]
        set "PROFILES=!TARGET_PROFILE!"
    ) else (
        echo [Arisen] MODE: Workspace Profile Loop
        set "PROFILES="
        for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "$m = Get-Content -LiteralPath '!MANIFEST_PATH!' -Raw | ConvertFrom-Json; if ($m.Profiles) { $m.Profiles.psobject.Properties.Name } else { 'Development'; 'Production' }"`) do (
            set "PROFILES=!PROFILES! %%A"
        )
    )
)

for %%A in (!PROFILES!) do (
    echo [Arisen] --------------------------------------------------
    echo [Arisen] Processing Profile: %%A [!BUILD_CONFIG!]
    echo [Arisen] --------------------------------------------------
    
    if /i "!MODE!"=="IsolatedTest" (
        dotnet run --project "%BUILD_TOOL_CSPROJ%" -- test --package "!TEST_PACKAGE_ID!" --workspace "!WORKSPACE_DIR!" --engine "!ENGINE_ROOT!"
        set "PROJECT_NAME=!TEST_PACKAGE_ID!.TestRun"
    ) else (
        dotnet run --project "%BUILD_TOOL_CSPROJ%" -- generate -m "%MANIFEST_PATH%" -e "!ENGINE_ROOT!" --profile %%A
        
        :: Extract Project Name for this profile (it might change in manifest, but usually it's global)
        for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$m = Get-Content -LiteralPath '!MANIFEST_PATH!' -Raw | ConvertFrom-Json; if ($m.Name) { $m.Name } else { 'MyGame' }"`) do set "PROJECT_NAME=%%P"
    )
    
    if not defined PROJECT_NAME set "PROJECT_NAME=MyGame"
    set "CURRENT_SLN=%SLN_DIR%\!PROJECT_NAME!_%%A.sln"
    
    if not exist "!CURRENT_SLN!" (
        echo [ERROR] Solution file not found for profile %%A: !CURRENT_SLN!
        goto :fail
    )

    :: Cleanup running instances
    taskkill /F /IM "!PROJECT_NAME!.exe" /T 2>nul
    taskkill /F /IM "!PROJECT_NAME!.Desktop.exe" /T 2>nul

    echo [Arisen] Restoring NuGet Packages for %%A...
    dotnet restore "!CURRENT_SLN!"
    if !errorlevel! neq 0 (
        echo [ERROR] NuGet restore failed for profile %%A.
        goto :fail
    )

    :: Manual Native Build
    set "NATIVE_BUILD_DIR=%SLN_DIR%\Projects\%%A\Native\build"
    if exist "!NATIVE_BUILD_DIR!" (
        echo [Arisen] Building Native Components for %%A [!BUILD_CONFIG!]...
        cmake --build "!NATIVE_BUILD_DIR!" --config !BUILD_CONFIG!
        if !errorlevel! neq 0 (
            echo [ERROR] Native build failed for profile %%A.
            goto :fail
        )
    )

    set "LOG_FILE=%SLN_DIR%\build_%%A.log"
    for %%F in ("!LOG_FILE!") do set "LOG_FILE_ABS=%%~fF"
    echo [Arisen] MSBuild Log: !LOG_FILE_ABS!
    msbuild "!CURRENT_SLN!" /p:Configuration=!BUILD_CONFIG! /p:Platform=x64 /m /fl /flp:logfile="!LOG_FILE_ABS!";verbosity=normal
    if !errorlevel! neq 0 (
        echo [ERROR] MSBuild failed on profile: %%A
        goto :fail
    )
)

echo.
echo [Arisen] Build successful!
pause
exit /b 0

:fail
echo.
echo [ERROR] Build Pipeline aborted due to execution or compilation errors.
pause
exit /b 1
